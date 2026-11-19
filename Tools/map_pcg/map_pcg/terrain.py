"""Final view-only terrain layer. Its random stream never touches earlier layers."""
import numpy as np
from . import fields as F


def sample(data, x, z):
    h = np.asarray(data['heightsMm'], dtype=float).reshape(data['depth'], data['width']) / 1000
    gx = np.clip((np.asarray(x) - data['originX']) / data['cell'], 0, data['width'] - 1)
    gz = np.clip((np.asarray(z) - data['originZ']) / data['cell'], 0, data['depth'] - 1)
    i = np.minimum(np.floor(gx).astype(int), data['width'] - 2)
    j = np.minimum(np.floor(gz).astype(int), data['depth'] - 2)
    fx, fz = gx - i, gz - j
    return np.where(fx >= fz,
                    h[j, i] + fx * (h[j, i+1] - h[j, i]) + fz * (h[j+1, i+1] - h[j, i+1]),
                    h[j, i] + fz * (h[j+1, i] - h[j, i]) + fx * (h[j+1, i+1] - h[j+1, i]))


def gradients(h, cell=1):
    lower = np.stack((h[:-1, 1:] - h[:-1, :-1], h[1:, 1:] - h[:-1, 1:]), -1) / cell
    upper = np.stack((h[1:, 1:] - h[1:, :-1], h[1:, :-1] - h[:-1, :-1]), -1) / cell
    return lower, upper


def vertex_curvature(h, cell=1):
    lower, upper = gradients(h, cell)
    incident = []
    for g, offsets in [(lower, [(0,0),(0,1),(1,1)]), (upper, [(0,0),(1,0),(1,1)])]:
        for row, col in offsets:
            out = np.full((*h.shape, 2), np.nan)
            out[row:row+h.shape[0]-1, col:col+h.shape[1]-1] = g
            incident.append(out)
    maximum = 0.
    for i, a in enumerate(incident):
        for b in incident[i+1:]:
            distance = np.linalg.norm(a-b, axis=-1)
            finite = distance[np.isfinite(distance)]
            if finite.size:
                maximum = max(maximum, float(finite.max()))
    return maximum


def coordinates(data):
    return np.meshgrid(data['originX'] + np.arange(data['width']) * data['cell'],
                       data['originZ'] + np.arange(data['depth']) * data['cell'])


def clipped_relief(data, half_diagonal=32):
    """Extrema of triangles clipped to the diamond, including edge crossings."""
    h = np.asarray(data['heightsMm']).reshape(data['depth'], data['width']) / 1000.
    x, z = coordinates(data)
    vertices = np.stack((x, h, z), -1)
    triangles = np.concatenate((np.stack((vertices[:-1,:-1], vertices[:-1,1:], vertices[1:,1:]), -2).reshape(-1,3,3),
                                np.stack((vertices[:-1,:-1], vertices[1:,1:], vertices[1:,:-1]), -2).reshape(-1,3,3)))
    values = h[np.abs(x) + np.abs(z) <= half_diagonal].tolist()
    normals = [(1,1), (-1,1), (-1,-1), (1,-1)]
    inside = np.abs(triangles[:,:,0]) + np.abs(triangles[:,:,2]) <= half_diagonal
    candidates = ~inside.all(axis=1)
    for nx, nz in normals:
        candidates &= ~(nx * triangles[:,:,0] + nz * triangles[:,:,2] > half_diagonal).all(axis=1)
    for triangle in triangles[candidates]:
        polygon = list(triangle)
        for nx, nz in normals:
            clipped = []
            for k, a in enumerate(polygon):
                b = polygon[(k+1) % len(polygon)]
                da, db = nx*a[0] + nz*a[2] - half_diagonal, nx*b[0] + nz*b[2] - half_diagonal
                if da <= 0:
                    clipped.append(a)
                if (da <= 0) != (db <= 0):
                    clipped.append(a + (b-a) * (da / (da-db)))
            polygon = clipped
        values.extend(p[1] for p in polygon)
    return float(max(values)-min(values)) if values else 0.


def metrics(data, hill_count=0):
    h = np.asarray(data['heightsMm']).reshape(data['depth'], data['width']) / 1000.
    x, z = coordinates(data)
    lo, up = gradients(h, data['cell'])
    return dict(maxSlope=float(max(np.linalg.norm(lo, axis=-1).max(), np.linalg.norm(up, axis=-1).max())),
                maxVertexCurvature=vertex_curvature(h, data['cell']), relief=clipped_relief(data),
                startWindowAbsHeight=float(np.abs(h[(np.abs(x)<=10) & (np.abs(z)<=11.25)]).max()), hillCount=hill_count)


def protection(L, x, z):
    # Expand each mask by a cell diagonal, so every triangle intersecting a protected
    # continuous region has all three vertices at zero (not just its centre).
    diagonal = np.sqrt(2)
    mask = np.abs(x) + np.abs(z) <= 6 + 2
    mask |= F.bilinear(L.grid, L.water_sdf, x, z) <= 1.5 + diagonal
    for it in L.placements:
        if it['prop'] == 'bridge':
            mask |= np.hypot(x-it['x'], z-it['y']) <= 3 + diagonal
    if L.camp is not None:
        mask |= np.hypot(x-L.camp[0], z-L.camp[1]) <= 3.0 + 1 + diagonal
    return mask


def check(L, data=None):
    data = data if data is not None else getattr(L, 'terrain', None)
    if data is None:
        return []
    cfg = L.cfg['terrain']
    m = metrics(data)
    h = np.asarray(data['heightsMm']).reshape(data['depth'], data['width']) / 1000.
    x, z = coordinates(data)
    out = []
    for key, limit in [('maxSlope',cfg['maxSlope']), ('maxVertexCurvature',cfg['maxVertexCurvature']),
                       ('relief',cfg['relief']), ('startWindowAbsHeight',.25)]:
        if m[key] > limit + 1e-9:
            out.append('terrain: %s %.6f > %.6f' % (key,m[key],limit))
    if np.any(h[protection(L,x,z)] != 0):
        out.append('terrain: protected start/water/bridge/camp not flat')
    # Check actual along/across path directions in world metres, including branch.
    for poly in [L.path, L.branch]:
        if poly is None:
            continue
        points, _ = F.resample(poly,.25)
        tangent = np.diff(points,axis=0)
        length = np.linalg.norm(tangent,axis=-1)
        direction = tangent / np.maximum(length[:,None],1e-9)
        mid = (points[:-1]+points[1:])*.5
        normal = np.stack((-direction[:,1],direction[:,0]),-1)
        for d, limit, label in [(direction,cfg['pathSlope'],'along'),(normal,cfg['pathCrossSlope'],'across')]:
            a,b=mid-d*.125,mid+d*.125
            slope=np.abs(sample(data,b[:,0],b[:,1])-sample(data,a[:,0],a[:,1]))/.25
            if np.any(slope>limit+1e-9):
                out.append('terrain: path %s slope %.6f > %.6f' % (label,float(slope.max()),limit))
    for it in L.placements:
        if not it['group'].startswith('wall_'):
            continue
        # Wall direction is encoded in yaw; enforce all nearby triangle slopes <= .1,
        # which also bounds every direction along the 0.8m strip.
        near=(np.abs(x-it['x'])<=1.8)&(np.abs(z-it['y'])<=1.8)
        lo,up=gradients(h,data['cell'])
        if np.any(np.maximum(np.linalg.norm(lo,axis=-1),np.linalg.norm(up,axis=-1))[near[:-1,:-1]]>.1+1e-9):
            out.append('terrain: wall strip slope > .1')
            break
    return out


def generate(L):
    cfg=L.cfg['terrain']
    rng=np.random.default_rng(L.seed+180004)
    data=dict(cell=cfg['cell'],originX=-36.,originZ=-36.,width=73,depth=73,
              generator=dict(tool='Tools/map_pcg',layer='terrain',seed=L.seed))
    x,z=coordinates(data)
    h=np.zeros_like(x)
    hill_count=int(rng.integers(2,5))
    for i in range(hill_count):
        angle=rng.uniform(0,2*np.pi)
        distance=rng.uniform(10,23)
        cx,cz=np.cos(angle)*distance,np.sin(angle)*distance
        radius=rng.uniform(4,8)
        h+=rng.uniform(.25,.45)*np.exp(-((x-cx)**2+(z-cz)**2)/(2*radius**2))
    h+=.10*(2*F.value_noise(x,z,16,L.seed+180004)-1)
    # A raised far-side plateau with a slope band; isolated random stream.
    for _ in range(int(rng.integers(0,3))):
        cx,cz=rng.uniform(-8,8),rng.uniform(20,26)
        distance=np.hypot(x-cx,z-cz)
        height=rng.uniform(.3,.45)
        t=np.clip((distance-rng.uniform(2,4))/(height/cfg['maxSlope']*2),0,1)
        h+=height*(1-t*t*(3-2*t))
    fixed=protection(L,x,z)
    # Smoothstep distance fade into exact-zero regions. No edits to prior layout.
    distance=np.full(x.shape,100.)
    for j,i in zip(*np.nonzero(fixed)):
        distance=np.minimum(distance,np.hypot(x-x[j,i],z-z[j,i]))
    t=np.clip(distance/5,0,1)
    h*=t*t*(3-2*t)
    h[fixed]=0
    # Deterministic neighbour relaxation followed by mm rounding; validate the actual
    # exported array every iteration. Strong global .078 slope also bounds path/walls.
    for iteration in range(200):
        mm=np.rint(h*1000).astype(int)
        data['heightsMm']=mm.ravel().tolist()
        m=metrics(data)
        if m['relief']<=cfg['relief'] and m['maxSlope']<=.078 and m['maxVertexCurvature']<=cfg['maxVertexCurvature']-.002 and m['startWindowAbsHeight']<=.25:
            L.terrain=data
            L.terrain_hill_count=hill_count
            L.terrain_iterations=iteration
            failures=check(L)
            if failures:
                raise RuntimeError('; '.join(failures))
            return
        padded=np.pad(h,1,mode='edge')
        h=(h*4+padded[1:-1,:-2]+padded[1:-1,2:]+padded[:-2,1:-1]+padded[2:,1:-1])/8
        h[fixed]=0
        scale=min(1.,cfg['relief']/max(m['relief'],1e-9),.25/max(m['startWindowAbsHeight'],1e-9))
        h*=scale
    raise RuntimeError('[terrain] neighbour relaxation did not converge in 200 iterations')

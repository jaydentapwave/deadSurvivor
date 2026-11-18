# 크레딧 0 형태 만들기(Blender) — 시범(hd2d-3d-pilot/tools/build.py)을 이어서 나머지 배경 3D를 짓는다.
# Blender -b --factory-startup --python-exit-code 1 -P build.py -- <kind> <outdir>
# kind: tree_v1 tree_v2 tree_v3 bush_v1 bush_v2 boulder_v1 boulder_v2 stone_v1 stone_v2 wall_a wall_b
#       grass_v1 grass_v2 flower_white flower_yellow flower_purple
# 결과: <outdir>/mesh.json(삼각형 uv·위치·면 법선·꼭짓점 법선·부위), work.blend
# 축: Blender Z 위, 앞(카메라 쪽, Unity +Z) = Blender -Y. 피벗 바닥 가운데, 가장 낮은 점 z=0.
# CPU fallback is wall-only; avoids GPU initialisation on restricted hosts.
import sys
if '--cpu-wall' in sys.argv:
    from wall_cpu import build
    args = sys.argv[sys.argv.index('--cpu-wall') + 1:]
    build(*args)
    raise SystemExit(0)

import bpy, bmesh, sys, json, math, random
from mathutils import Vector, noise

argv = sys.argv[sys.argv.index('--') + 1:]
kind, outdir = argv[0], argv[1]
DENS = 48.0
random.seed(20261118)
bpy.ops.wm.read_factory_settings(use_empty=True)

parts = []  # (object, part name, smooth)

def clump(name, c, r, sq=0.86, amp=0.17, seed=0, sub=2, teeth=0.0):
    """잎 덩어리. teeth: 꼭짓점마다 고정 시드로 밀고 당겨 실루엣 톱니를 만든다(판독: 덤불 톱니 적음)."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=r, location=c)
    o = bpy.context.active_object; o.name = name
    off = Vector((seed * 3.1, seed * 1.7, seed * 2.3))
    rnd = random.Random(seed * 7919 + 13)
    for v in o.data.vertices:
        d = v.co.normalized()
        n = noise.noise(d * 1.8 + off) * 0.5 + noise.noise(d * 4.0 + off) * 0.5
        t = 0.0
        if teeth > 0 and d.z > -0.55:
            t = teeth * (1.0 if rnd.random() < 0.45 else -0.45)
        v.co = d * r * (1 + amp * n + t)
        v.co.z *= sq
    return o

def trunk(h, r0, r1, flare):
    bm = bmesh.new()
    rings = [(0.0, r0 + flare), (0.10, r0 + flare * 0.35), (h * 0.55, (r0 + r1) / 2), (h, r1)]
    N = 7; vs = []
    for z, r in rings:
        ring = []
        for k in range(N):
            a = 2 * math.pi * k / N + 0.2
            rr = r * (1.25 if (z == 0.0 and k % 2 == 0) else 1.0)   # 뿌리 퍼짐
            ring.append(bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, z)))
        vs.append(ring)
    for i in range(len(vs) - 1):
        for k in range(N):
            bm.faces.new((vs[i][k], vs[i][(k + 1) % N], vs[i + 1][(k + 1) % N], vs[i + 1][k]))
    me = bpy.data.meshes.new('trunk'); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('trunk', me); bpy.context.collection.objects.link(o)
    return o

def branch(p0, p1, r):
    d = Vector(p1) - Vector(p0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=5, radius=r, depth=d.length, location=(Vector(p0) + Vector(p1)) / 2, end_fill_type='NOTHING')
    o = bpy.context.active_object
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = d.to_track_quat('Z', 'Y')
    return o

def hull(name, pts, drop_bottom=True):
    bm = bmesh.new()
    for p in pts: bm.verts.new(p)
    bmesh.ops.convex_hull(bm, input=bm.verts)
    if drop_bottom:   # 땅에 닿는 밑면은 보이지 않는다(삼각형 아낌)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z < -0.97 and all(v.co.z < 1e-4 for v in f.verts)], context='FACES_ONLY')
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    return o

def rock_pts(cx, cy, rx, ry, h, tilt, n=16, seed=1, base=7, taper=0.62):
    rnd = random.Random(seed); pts = []
    for i in range(n):
        z = (i + 0.5) / n; a = i * 2.39996 + rnd.uniform(-.3, .3)
        rad = (1 - taper * z ** 1.2) * rnd.uniform(0.80, 1.06)      # 위로 갈수록 좁게(벽이 아니라 비탈 면)
        x = cx + math.cos(a) * rx * rad; y = cy + math.sin(a) * ry * rad
        top = h * (1 + tilt * (x - cx) / rx)
        pts.append((x, y, min(z * h * 1.25, top)))
    for k in range(base):  # 넓은 밑면
        a = k * 2 * math.pi / base + rnd.uniform(-.2, .2)
        pts.append((cx + math.cos(a) * rx * rnd.uniform(.9, 1.02), cy + math.sin(a) * ry * rnd.uniform(.9, 1.02), 0.0))
    return pts

def box(name, x0, x1, y0, y1, z0, z1, drop_bottom=True):
    bm = bmesh.new()
    v = [bm.verts.new((x, y, z)) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)]
    F = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    for i, f in enumerate(F):
        if drop_bottom and i == 0: continue
        bm.faces.new([v[k] for k in f])
    bm.normal_update()
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    return o

def blade(name, base, h, w, lean, yaw, seed):
    """풀잎 한 장: 바닥 세모(폭 w) → 기운 꼭짓점. 옆면 3장(3삼각형)."""
    bm = bmesh.new(); rnd = random.Random(seed)
    bx, by = base
    b = [bm.verts.new((bx + math.cos(yaw + k * 2.094) * w * (1.0 if k == 0 else 0.55), by + math.sin(yaw + k * 2.094) * w * (1.0 if k == 0 else 0.55), 0.0)) for k in range(3)]
    tip = bm.verts.new((bx + math.cos(yaw) * lean, by + math.sin(yaw) * lean, h))
    for k in range(3):
        bm.faces.new((b[k], b[(k + 1) % 3], tip))
    bm.normal_update()
    for f in bm.faces:   # 바깥을 보게
        c = f.calc_center_median()
        if f.normal.dot(c - Vector((bx, by, h * 0.3))) < 0: f.normal_flip()
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    return o

def cone(name, base, r, h, n):
    """낮은 뿔(옆면 n장, 밑면 없음)."""
    bm = bmesh.new(); bx, by = base
    ring = [bm.verts.new((bx + math.cos(k * 2 * math.pi / n) * r, by + math.sin(k * 2 * math.pi / n) * r, 0.0)) for k in range(n)]
    tip = bm.verts.new((bx, by, h))
    for k in range(n):
        bm.faces.new((ring[k], ring[(k + 1) % n], tip))
    bm.normal_update()
    for f in bm.faces:
        if f.normal.dot(f.calc_center_median() - Vector((bx, by, h * 0.3))) < 0: f.normal_flip()
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    return o

def octa(name, c, r, sq=0.6):
    """꽃송이: 납작한 팔면체(8삼각형)."""
    bm = bmesh.new(); cx, cy, cz = c
    P = [bm.verts.new((cx + dx * r, cy + dy * r, cz + dz * r * sq)) for dx, dy, dz in
         ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))]
    for a, b in ((0, 2), (2, 1), (1, 3), (3, 0)):
        bm.faces.new((P[a], P[b], P[4])); bm.faces.new((P[b], P[a], P[5]))
    bm.normal_update()
    for f in bm.faces:
        if f.normal.dot(f.calc_center_median() - Vector(c)) < 0: f.normal_flip()
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    return o

# ---------------------------------------------------------------- 종류별 형태
if kind == 'tree_v1':   # 시범 나무 그대로(잎 덩어리 4 + 작은 뭉치 5)
    parts.append((trunk(0.86, 0.12, 0.085, 0.10), 'wood', False))
    parts.append((branch((0.05, 0, 0.55), (0.30, -0.04, 0.72), 0.04), 'wood', False))
    for nm, c, r, s in (('c_top', (0.0, 0.05, 1.24), 0.46, 1), ('c_left', (-0.40, 0.02, 0.99), 0.35, 2),
                        ('c_right', (0.39, -0.03, 0.97), 0.36, 3), ('c_front', (0.04, -0.30, 0.80), 0.29, 4)):
        parts.append((clump(nm, c, r, seed=s), 'leaf', True))
    for i, (c, r) in enumerate((((-0.62, -0.05, 1.12), 0.15), ((0.20, -0.12, 1.55), 0.16), ((0.62, -0.10, 1.12), 0.15),
                                ((-0.22, -0.30, 1.30), 0.15), ((-0.25, -0.32, 0.86), 0.14))):
        parts.append((clump(f's{i}', c, r, amp=0.12, seed=20 + i, sub=1), 'leaf', True))
elif kind == 'tree_v2':   # 키 크고 좁게(약 2.0m): 잎 덩어리를 세로로 쌓음
    parts.append((trunk(1.12, 0.11, 0.075, 0.09), 'wood', False))
    parts.append((branch((-0.03, 0, 0.70), (-0.24, 0.03, 0.88), 0.035), 'wood', False))
    for nm, c, r, s in (('c_top', (0.02, 0.04, 1.68), 0.34, 41), ('c_midl', (-0.24, 0.03, 1.38), 0.31, 42),
                        ('c_midr', (0.25, -0.02, 1.34), 0.30, 43), ('c_low', (0.0, -0.22, 1.08), 0.30, 44),
                        ('c_back', (0.05, 0.20, 1.14), 0.27, 45)):
        parts.append((clump(nm, c, r, sq=0.92, seed=s), 'leaf', True))
    for i, (c, r) in enumerate((((-0.42, -0.04, 1.22), 0.14), ((0.10, -0.10, 1.95), 0.13), ((0.43, -0.08, 1.16), 0.13),
                                ((-0.15, -0.26, 1.55), 0.13))):
        parts.append((clump(f's{i}', c, r, amp=0.12, seed=50 + i, sub=1), 'leaf', True))
elif kind == 'tree_v3':   # 넓게(약 1.9m 폭, 1.6m 높이)
    parts.append((trunk(0.74, 0.13, 0.095, 0.11), 'wood', False))
    parts.append((branch((-0.05, 0, 0.50), (-0.36, 0.02, 0.70), 0.045), 'wood', False))
    parts.append((branch((0.06, 0, 0.56), (0.38, -0.02, 0.72), 0.04), 'wood', False))
    for nm, c, r, s in (('c_top', (0.0, 0.06, 1.17), 0.42, 61), ('c_left', (-0.56, 0.02, 0.96), 0.37, 62),
                        ('c_right', (0.57, -0.02, 0.93), 0.36, 63), ('c_front', (0.04, -0.30, 0.82), 0.30, 64)):
        parts.append((clump(nm, c, r, sq=0.82, seed=s), 'leaf', True))
    for i, (c, r) in enumerate((((-0.86, -0.04, 0.98), 0.14), ((0.88, -0.06, 0.92), 0.14), ((-0.25, -0.10, 1.47), 0.15),
                                ((0.30, -0.12, 1.42), 0.14), ((-0.30, -0.32, 0.80), 0.13))):
        parts.append((clump(f's{i}', c, r, amp=0.12, seed=70 + i, sub=1), 'leaf', True))
elif kind in ('bush_v1', 'bush_v2'):
    # r2(방법 바꿈): 큰 둥근 덩어리 3개 → 작은 잎 뭉치(ico 1단, 20삼각) 여럿을 둥근 무더기로 쌓음.
    # 판독 1회차 "둥근 덩이 윤곽이 매끈해 이끼 낀 바위처럼 읽힘" → 실루엣이 잎 뭉치 톱니로 깨지게.
    rnd = random.Random(1101 if kind == 'bush_v1' else 1102)
    n, RX, RY, HT, r0 = (14, 0.52, 0.40, 0.70, 0.19) if kind == 'bush_v1' else (10, 0.38, 0.30, 0.52, 0.15)
    for i in range(n):
        f = (i + 0.5) / n; a = i * 2.39996 + rnd.uniform(-.3, .3)
        rr = math.sqrt(1 - f * 0.85)                      # 아래는 넓게, 위는 좁게(둥근 무더기)
        c = (math.cos(a) * RX * rr * 0.80, math.sin(a) * RY * rr * 0.80, HT * (0.18 + 0.62 * f))
        r = r0 * rnd.uniform(0.85, 1.15) * (1.0 if f < 0.8 else 0.85)
        parts.append((clump(f'k{i}', c, r, sq=0.9, amp=0.10, seed=1200 + i + (50 if kind == 'bush_v2' else 0), sub=1, teeth=0.20), 'leaf', True))
elif kind == 'boulder_v1':   # r3: 뭉툭한 덩어리 + 각도 기준 매끈 음영(48° 넘는 큰 꺾임만 날카롭게) → 셰이더 3단 띠가 큰 판으로
    parts.append((hull('main', rock_pts(-0.10, 0.0, 0.52, 0.42, 0.74, -0.18, 24, 111, base=9, taper=0.55)), 'rock', True))   # r4: 점 24(면을 잘게, 매끈 음영)
    parts.append((hull('shoulder', rock_pts(0.36, -0.08, 0.27, 0.30, 0.42, -0.25, 14, 112, base=5, taper=0.55)), 'rock', True))
elif kind == 'boulder_v2':   # 작은 큰 바위(약 0.65m)
    parts.append((hull('main', rock_pts(0.0, 0.0, 0.33, 0.27, 0.48, -0.20, 12, 121, base=6, taper=0.55)), 'rock', True))
elif kind == 'stone_v1':   # 작은 돌 0.48m
    parts.append((hull('main', rock_pts(0.0, 0.0, 0.27, 0.20, 0.25, -0.22, 12, 133, base=7, taper=0.5)), 'rock', True))
elif kind == 'stone_v2':   # 작은 돌 0.56m, 두 덩어리
    parts.append((hull('main', rock_pts(-0.05, 0.0, 0.23, 0.19, 0.26, -0.20, 9, 141, base=6, taper=0.5)), 'rock', True))
    parts.append((hull('chip', rock_pts(0.19, -0.04, 0.10, 0.10, 0.13, -0.1, 5, 142, base=4, taper=0.5)), 'rock', True))
elif kind in ('wall_a', 'wall_b'):
    # r3 wall-only: two courses of large stones, solid outward faces and broad top bevels.
    # Keep the adopted 1m run, 0.30m depth, exact height and ground-centred pivot.
    height = 0.513 if kind == 'wall_a' else 0.521
    courses = [(0.0, 0.255, [-.5, -.17, .19, .5]),
               (0.255, height, [-.5, .03, .5] if kind == 'wall_a' else [-.5, -.28, .16, .5])]
    for ri, (z0, z1, xs) in enumerate(courses):
        for bi, (x0, x1) in enumerate(zip(xs, xs[1:])):
            # Bevel only the long top edges: six-point YZ profile, 20 triangles/block.
            # Contact faces remain flush at x=+-0.5, so adjacent 1m runs still meet.
            top = z1 if ri == 0 or bi == 0 else z1 - (0.018 if bi % 2 else 0.009)
            bevel = .035
            profile = [(-.15,z0),(.15,z0),(.15,top-bevel),(.15-bevel,top),(-.15+bevel,top),(-.15,top-bevel)]
            pts = [(x,y,z) for x in (x0,x1) for y,z in profile]
            o = hull(f'b{ri}_{bi}', pts, drop_bottom=False)
            bm = bmesh.new(); bm.from_mesh(o.data)
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            bm.to_mesh(o.data); bm.free()
            parts.append((o, 'brick', False))
elif kind in ('grass_v1', 'grass_v2'):
    # 풀포기 r2: 가운데 낮은 뿔 덩어리 + 둘레 넓은 풀잎(바닥 폭 0.07m 넘게). 1회차 가는 잎(0.04m)은 외곽선에 다 묻혀 갈색 선만 보였다.
    rnd = random.Random(301 if kind == 'grass_v1' else 302)
    n, R, H, rc, hc = (5, 0.09, 0.20, 0.09, 0.11) if kind == 'grass_v1' else (7, 0.13, 0.26, 0.12, 0.14)
    parts.append((cone('core', (0, 0), rc, hc, 5), 'blade', False))
    for i in range(n):
        a = i * 2 * math.pi / n + rnd.uniform(-.35, .35); rr = R * rnd.uniform(0.6, 1.0)
        bx, by = math.cos(a) * rr * 0.5, math.sin(a) * rr * 0.5
        h = H * rnd.uniform(0.7, 1.0)
        parts.append((blade(f'g{i}', (bx, by), h, rnd.uniform(0.07, 0.085), rr, a, 400 + i), 'blade', False))
elif kind.startswith('flower_'):
    # 꽃 r2: 잎 덩어리 뿔 + 잎 3장 + 큰 꽃송이 3(지름 0.13~0.15m ≈ 6~7px)
    rnd = random.Random({'flower_white': 501, 'flower_yellow': 502, 'flower_purple': 503}[kind])
    parts.append((cone('core', (0, 0), 0.10, 0.10, 5), 'stem', False))
    for i in range(3):
        a = i * 2.094 + rnd.uniform(-.4, .4)
        parts.append((blade(f'l{i}', (math.cos(a) * 0.05, math.sin(a) * 0.05), rnd.uniform(0.13, 0.17), 0.07, 0.13, a, 600 + i), 'stem', False))
    for i, (x, y, z) in enumerate(((-0.09, 0.03, 0.22), (0.095, 0.0, 0.19), (0.0, -0.10, 0.14))):
        parts.append((octa(f'h{i}', (x, y, z), 0.095 if i < 2 else 0.085), 'petal', False))
else:
    raise SystemExit('unknown kind ' + kind)

# 합치기 + 바닥 자르기(z<0 → 0) + 삼각형화
for o, _, _ in parts:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    me = o.data
    for v in me.vertices:
        if v.co.z < 0: v.co.z = 0.0
    for p in me.polygons: p.use_smooth = [s for (oo, _, s) in parts if oo == o][0]
for gi, (o, _, _) in enumerate(parts):   # 원래 조각 번호(벽돌·꽃송이 단위 칠에 씀)
    at = o.data.attributes.new('grp', 'INT', 'FACE')
    for p in o.data.polygons: at.data[p.index].value = gi
mats = {}
for o, pname, _ in parts:
    if pname not in mats:
        mats[pname] = bpy.data.materials.new(pname)
    o.data.materials.clear(); o.data.materials.append(mats[pname])
bpy.ops.object.select_all(action='DESELECT')
for o, _, _ in parts: o.select_set(True)
bpy.context.view_layer.objects.active = parts[0][0]
bpy.ops.object.join()
T = bpy.context.active_object; T.name = 'prop'
bm = bmesh.new(); bm.from_mesh(T.data)
if not kind.startswith(('wall_', 'grass_', 'flower_')):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
# 땅 밑으로 눌린 납작한 면(넓이 0)과 아래를 보는 바닥 면 지우기
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_area() < 1e-7 or (f.normal.z < -0.97 and all(v.co.z < 1e-4 for v in f.verts))], context='FACES_ONLY')
bmesh.ops.triangulate(bm, faces=bm.faces)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]; zs = [v.co.z for v in bm.verts]
if kind.startswith('wall_'):
    c = Vector((0.0, (min(ys) + max(ys)) / 2, min(zs)))   # 토막 이음: x 가운데는 0 그대로(양 끝 ±0.5)
else:
    c = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
for v in bm.verts: v.co -= c
bm.to_mesh(T.data); bm.free()

if kind.startswith(('boulder', 'stone')):
    T.data.set_sharp_from_angle(angle=math.radians(48))   # 큰 꺾임만 날카롭게, 나머지 매끈
# --- UV: 시범·다리·천막과 같은 방식(35° 섬, 세계 축 맞춤 평면 투영, 48텍셀/m, 정수 원점, 3텍셀 간격)
T.data.uv_layers.new(name='UVMap')
bm = bmesh.new(); bm.from_mesh(T.data); bm.faces.ensure_lookup_table()
uvl = bm.loops.layers.uv.active
COS = math.cos(math.radians(35))
def vk(v): return (round(v.co.x, 4), round(v.co.y, 4), round(v.co.z, 4))
edge_faces = {}
for f in bm.faces:
    for e in f.edges:
        edge_faces.setdefault(tuple(sorted((vk(e.verts[0]), vk(e.verts[1])))), []).append(f.index)
def nbrs(f):
    for e in f.edges:
        for g in edge_faces[tuple(sorted((vk(e.verts[0]), vk(e.verts[1]))))]:
            yield bm.faces[g]
isl = [-1] * len(bm.faces); islands = []
for s0 in sorted(range(len(bm.faces)), key=lambda i: -bm.faces[i].calc_area()):
    if isl[s0] >= 0: continue
    seed_n = bm.faces[s0].normal.copy(); idx = len(islands); members = [s0]; isl[s0] = idx; stack = [s0]
    while stack:
        f = bm.faces[stack.pop()]
        for g in nbrs(f):
            if isl[g.index] < 0 and g.normal.dot(seed_n) > COS and g.material_index == f.material_index:
                isl[g.index] = idx; members.append(g.index); stack.append(g.index)
    islands.append(members)
Z = Vector((0, 0, 1)); X = Vector((1, 0, 0)); rects = []
for members in islands:
    n = Vector((0, 0, 0))
    for i in members: n += bm.faces[i].normal * bm.faces[i].calc_area()
    n.normalize()
    if abs(n.z) > 0.8:
        u = X - n * n.dot(X); u.normalize()
    else:
        u = Z.cross(n); u.normalize()
    v = n.cross(u)
    if v.z < 0 and abs(n.z) <= 0.8: v = -v
    pts = {}
    for i in members:
        for l in bm.faces[i].loops:
            p = l.vert.co; pts[l] = (p.dot(u) * DENS, p.dot(v) * DENS)
    us = [q[0] for q in pts.values()]; vs_ = [q[1] for q in pts.values()]
    u0, v0 = math.floor(min(us)), math.floor(min(vs_))
    rects.append([members, pts, u0, v0, math.ceil(max(us)) - u0, math.ceil(max(vs_)) - v0])
GAP = 3
def shelf(W):
    x = GAP; y = GAP; rowh = 0; place = []
    for r in sorted(range(len(rects)), key=lambda i: -rects[i][5]):
        w, h = rects[r][4], rects[r][5]
        if x + w + GAP > W:
            x = GAP; y += rowh + GAP; rowh = 0
        place.append((r, x, y)); x += w + GAP; rowh = max(rowh, h)
    return place, y + rowh + GAP
for W, H in ((32, 32), (64, 32), (64, 64), (128, 64), (128, 128), (256, 128), (256, 256), (512, 256), (512, 512), (1024, 512)):
    place, used = shelf(W)
    if used <= H and all(rects[r][4] + 2 * GAP <= W for r, _, _ in place): break
for r, x, y in place:
    members, pts, u0, v0, w, h = rects[r]
    for l, (pu, pv) in pts.items():
        l[uvl].uv = ((pu - u0 + x) / W, (pv - v0 + y) / H)
A3 = sum(f.calc_area() for f in bm.faces)
bm.to_mesh(T.data); bm.free()

me = T.data
me.calc_loop_triangles()
uvd = me.uv_layers.active.data
names = [m.name for m in me.materials]
grp = me.attributes['grp'].data
tris = []
for t in me.loop_triangles:
    tris.append({'uv': [list(uvd[li].uv) for li in t.loops], 'p': [list(me.vertices[vi].co) for vi in t.vertices],
                 'n': list(t.normal), 'vn': [list(me.corner_normals[li].vector) for li in t.loops],
                 'part': names[t.material_index], 'grp': grp[t.polygon_index].value})
co = [v.co for v in me.vertices]
bounds = [[min(c[i] for c in co) for i in range(3)], [max(c[i] for c in co) for i in range(3)]]
json.dump({'kind': kind, 'S': W, 'SH': H, 'A3': A3, 'islands': len(islands), 'bounds': bounds, 'tris': tris}, open(outdir + '/mesh.json', 'w'))
bpy.ops.wm.save_as_mainfile(filepath=outdir + '/work.blend')
print('BUILT', kind, 'tris', len(tris), 'verts', len(me.vertices), 'tex', W, H, 'islands', len(islands), 'bounds', [[round(x, 3) for x in b] for b in bounds])

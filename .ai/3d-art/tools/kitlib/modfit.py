"""Pure geometry for the Blender post-process steps (numpy only, runs with or without Blender; selftest covers it).

- terrain module fit: scale a model to an exact box, then snap vertices near the cell boundary onto the boundary plane
  (art standard 3.2 "이음새 규칙": bounds exact, vertices within 2 cm of x = +-0.5 welded; corner modules also z).
  Coordinates are Unity space: x right, y up, z forward. The module origin is the cell centre, top face y = 0.
- hand-in-body measure: share of hand vertices inside the torso's convex hull (planes from the hull faces).
- character motion: which bones the armfix holds at their bind rotation (fingers, wrists).
"""
import numpy as np

MAX_DISTORTION = 0.15   # an axis scale further than this from the three axes' median (ratio to the median) means Tripo's shape is off; warn
WELD_TOL = 0.02   # 3.2 이음새 규칙: 2 cm
CELL = 1.0        # 3.1 격자: 1 cell = 1 m


def target_box(height, bury, width=CELL, depth=CELL):
    """box a cliff module must fill: x/z = the cell, y from -(height + bury) to 0 (top face at y = 0)"""
    lo = np.array([-width / 2, -(height + bury), -depth / 2])
    hi = np.array([width / 2, 0.0, depth / 2])
    return lo, hi


def rotate_yaw(points, degrees):
    """rotate points about the Y axis (fit frame): x' = x cos + z sin, z' = -x sin + z cos, so +90 turns +X into -Z and -X into +Z.
    Used before the fit when Tripo made the module on the wrong axis (stairs along x, a 45 degree corner piece)."""
    t = np.radians(degrees)
    c, s = np.cos(t), np.sin(t)
    p = np.asarray(points, float)
    return np.c_[p[:, 0] * c + p[:, 2] * s, p[:, 1], -p[:, 0] * s + p[:, 2] * c]


def fit_to_box(points, lo, hi):
    """per-axis scale + shift so the points' bounds equal [lo, hi] exactly"""
    p = np.asarray(points, float)
    mn, mx = p.min(0), p.max(0)
    span = mx - mn
    if (span <= 1e-9).any():
        raise ValueError('FLAT_MODEL')
    out = (p - mn) / span * (hi - lo) + lo
    return out, dict(scale=(hi - lo) / span, before=dict(min=mn, max=mx))


def weld_planes(points, lo, hi, axes, tol=WELD_TOL):
    """snap vertices within tol of the lo/hi plane on each axis in `axes` onto that plane.
    Returns (points, count of vertices that actually moved, per-plane counts)."""
    p = np.array(points, float)
    moved = np.zeros(len(p), bool)
    per = {}
    for a in axes:
        for name, plane in (('lo', lo[a]), ('hi', hi[a])):
            d = np.abs(p[:, a] - plane)
            near = d <= tol
            shifted = near & (d > 1e-9)
            p[near, a] = plane
            moved |= shifted
            per[f'{"xyz"[a]}_{name}'] = dict(within_tol=int(near.sum()), moved=int(shifted.sum()))
    return p, int(moved.sum()), per


def fit_module(points, height, bury, corner=False, tol=WELD_TOL, width=CELL, depth=CELL, max_distortion=MAX_DISTORTION, pre_yaw=0.0):
    """whole step: exact box, then weld x = +-width/2 (and z = +-depth/2 for a corner module)"""
    lo, hi = target_box(height, bury, width, depth)
    if pre_yaw:
        points = rotate_yaw(points, pre_yaw)
    p, info = fit_to_box(points, lo, hi)
    axes = (0, 2) if corner else (0,)
    p, moved, per = weld_planes(p, lo, hi, axes, tol)
    # distortion = how unevenly the axes were stretched (each axis scale / median axis scale); the absolute scale only says Tripo
    # made the model bigger or smaller and is recorded as information
    ratio = info['scale'] / np.median(info['scale'])
    warnings = [f'SCALE_DISTORTION:{"xyz"[i]}={ratio[i]:.4f}' for i in range(3) if abs(ratio[i] - 1) > max_distortion]
    return p, dict(box_lo=lo, box_hi=hi, scale=info['scale'], scale_ratio=ratio, warnings=warnings, before=info['before'], welded_axes=list(axes),
                   moved_vertices=moved, planes=per, after=dict(min=p.min(0), max=p.max(0)))


def hull_planes(points, faces):
    """outward unit normals + one point per triangle of a convex hull given as vertex array + triangle indices.
    Orientation is fixed against the centroid, so face winding does not matter."""
    p = np.asarray(points, float)
    f = np.asarray(faces, int)
    c = p.mean(0)
    n = np.cross(p[f[:, 1]] - p[f[:, 0]], p[f[:, 2]] - p[f[:, 0]])
    ln = np.linalg.norm(n, axis=1)
    keep = ln > 1e-12
    n, f = n[keep] / ln[keep, None], f[keep]
    q = p[f[:, 0]]
    flip = ((q - c) * n).sum(1) < 0
    n[flip] *= -1
    return n, q


def inside_convex(pts, normals, anchors, eps=0.0):
    """boolean per point: inside every hull half-space"""
    d = ((np.asarray(pts, float)[:, None, :] - anchors[None]) * normals[None]).sum(2)
    return (d <= eps).all(1)


def box_hull(lo, hi):
    """a box as hull data (8 verts, 12 triangles) - for tests"""
    lo, hi = np.asarray(lo, float), np.asarray(hi, float)
    v = np.array([[x, y, z] for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])])
    quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    tris = [t for q in quads for t in ((q[0], q[1], q[2]), (q[0], q[2], q[3]))]
    return v, np.array(tris)


FINGERS = ('Thumb', 'Index', 'Middle', 'Ring', 'Pinky')
WRISTS = ('Left_Hand', 'Right_Hand')


def rest_bones(bone_names, fingers=True, wrists=True):
    """(finger bones, wrist bones) the armfix holds at bind rotation on every frame. Fingers: any name containing a finger word
    (K7: the mitten fist has no modelled fingers). Wrists: exactly Left_Hand / Right_Hand when present (S7 r2: the retarget turns
    Left_Hand 112-135 deg from bind and only the finger counter-rotation hid it; resting the fingers alone sinks the mitten into
    the hips). A finger bone is never a wrist bone."""
    names = list(bone_names)
    f = [n for n in names if fingers and any(k in n for k in FINGERS)]
    w = [n for n in names if wrists and n in WRISTS and n not in f]
    return f, w


def part_names(label, n):
    """object names for the n meshes of one attached part (attach_props): 'Hat' for one mesh, 'Hat_1'.. for several. The Unity build
    gives renderers named <label> or <label>_<k> the part's own material (catalog 'parts'), so a hat is not painted with the body atlas."""
    return [label] if n == 1 else [f'{label}_{k + 1}' for k in range(n)]


def selftest():
    rng = np.random.default_rng(3)
    # 1. a Tripo-like tile: 0.998 x 0.985 x 1.0 with jitter near the sides -> exact box, seams welded
    p = rng.uniform([-0.499, -0.4923, -0.5], [0.499, 0.4923, 0.5], (500, 3))
    p[:5, 0] = [-0.4999, -0.49, 0.495, 0.4999, 0.45]
    p[:5, 1] = [-0.4923, 0.4923, 0.1, 0.2, 0.3]            # x extremes for the bounds
    out, info = fit_module(p, 1.25, 0.05)
    assert np.allclose(info['after']['min'], [-0.5, -1.30, -0.5]) and np.allclose(info['after']['max'], [0.5, 0.0, 0.5]), info['after']
    assert (np.abs(out[:, 0]) <= 0.5 + 1e-12).all()
    # every vertex that was within 2 cm of a side plane after the fit is now exactly on it
    lo, hi = target_box(1.25, 0.05)
    scaled, _ = fit_to_box(p, lo, hi)
    near = np.abs(np.abs(scaled[:, 0]) - 0.5) <= WELD_TOL
    assert near.sum() > 0 and (np.abs(out[near, 0]) == 0.5).all() and info['moved_vertices'] == int((near & (np.abs(np.abs(scaled[:, 0]) - 0.5) > 1e-9)).sum())
    # vertices farther than 2 cm are untouched
    far = ~near
    assert np.allclose(out[far], scaled[far])
    # straight module: z is not welded; corner module: z welded too
    assert info['welded_axes'] == [0]
    _, ic = fit_module(p, 1.9, 0.05, corner=True)
    assert ic['welded_axes'] == [0, 2] and ic['moved_vertices'] >= info['moved_vertices'] and np.isclose(ic['after']['min'][1], -1.95)
    # distortion warning: only axes whose scale / median scale is >15% off (here y: 0.985 tall -> 1.3 = x1.32 vs x1.0), never stops by itself
    assert info['warnings'] == ['SCALE_DISTORTION:y=%.4f' % info['scale_ratio'][1]] and info['scale_ratio'][1] > 1.15, info['warnings']
    # a uniform enlargement (all axes x2 or x0.26) is not a distortion: absolute scale far from 1, ratios all 1
    _, iu = fit_module(np.array([[-0.25, -0.65, -0.25], [0.25, 0.0, 0.25], [0.1, -0.1, 0.0]]), 1.25, 0.05)
    assert iu['warnings'] == [] and np.allclose(iu['scale'], 2.0), (iu['warnings'], iu['scale'])
    # the S5 collapsed cliff (0.262 x 1 x 0.297 -> x3.82, y1.30, z3.37): ratios 1.13, 0.34, 1.0 -> y is the odd axis
    _, ic2 = fit_module(np.array([[-0.131, -1.0, -0.1485], [0.131, 0.0, 0.1485]]), 1.25, 0.05)
    assert [w.split('=')[0] for w in ic2['warnings']] == ['SCALE_DISTORTION:y'] and ic2['scale'][0] > 3.8, ic2['warnings']
    _, iw = fit_module(rng.uniform(lo + 0.05, hi - 0.05, (50, 3)) * [1.0, 1.1, 1.0] * 0 + np.vstack([lo * 1.1, hi * 0.9, rng.uniform(lo, hi, (48, 3)) * 0.95]), 1.25, 0.05)
    assert iw['warnings'] == [], iw['warnings']          # 5..14% off on every axis: no warning
    # pre_yaw: a module made along x (stairs, length 2) is turned onto z before the fit; +90 maps -X to +Z, 180 flips both
    L = np.array([[-1.0, 0, 0], [1, 0, 0], [-1, 1, 0.3], [0, 0.5, 0.5], [1, 0, 0.3]])
    r90 = rotate_yaw(L, 90)
    assert np.allclose(r90[0], [0, 0, 1]) and np.allclose(r90[1], [0, 0, -1]) and np.allclose(r90[:, 1], L[:, 1])
    assert np.allclose(rotate_yaw(L, 180)[0], [1, 0, 0]) and np.allclose(rotate_yaw(L, 180)[:, 2], -L[:, 2])
    _, ir = fit_module(L, 1.25, 0.05, width=1.0, depth=2.0, pre_yaw=90)
    assert np.allclose(ir['after']['min'], [-0.5, -1.30, -1.0]) and np.allclose(ir['after']['max'], [0.5, 0.0, 1.0]), ir['after']
    # 2. exact box already -> nothing moves; flat model refused
    box = rng.uniform(lo + 0.05, hi - 0.05, (50, 3)); box[0], box[1] = lo, hi
    o2, i2 = fit_module(box, 1.25, 0.05)
    assert np.allclose(o2, box) and i2['moved_vertices'] == 0
    try:
        fit_module(np.zeros((4, 3)), 1.25, 0.05); raise AssertionError('flat accepted')
    except ValueError:
        pass
    # 3. hull containment: unit box, points inside / on / outside; reversed winding must not matter
    v, t = box_hull([-1, -1, -1], [1, 1, 1])
    n, a = hull_planes(v, t)
    pts = np.array([[0, 0, 0], [0.99, 0.99, -0.99], [1.01, 0, 0], [0, -1.2, 0], [0, 0, 5]])
    assert inside_convex(pts, n, a).tolist() == [True, True, False, False, False]
    n2, a2 = hull_planes(v, t[:, ::-1])
    assert inside_convex(pts, n2, a2).tolist() == [True, True, False, False, False]
    # 4. armfix rest bones: both on / one off / missing wrist / no false wrist match on finger or forearm names
    bones = ['Hips', 'Left_LowerArm', 'Left_Hand', 'Left_ThumbProximal', 'Left_IndexDistalEnd', 'Right_Hand', 'Right_PinkyDistal', 'Left_HandTwist']
    assert rest_bones(bones) == (['Left_ThumbProximal', 'Left_IndexDistalEnd', 'Right_PinkyDistal'], ['Left_Hand', 'Right_Hand'])
    assert rest_bones(bones, fingers=False) == ([], ['Left_Hand', 'Right_Hand'])
    assert rest_bones(bones, wrists=False) == (['Left_ThumbProximal', 'Left_IndexDistalEnd', 'Right_PinkyDistal'], [])
    assert rest_bones([b for b in bones if b != 'Right_Hand'])[1] == ['Left_Hand']
    assert rest_bones(['Hips', 'Spine'], True, True) == ([], [])
    assert part_names('Hat', 1) == ['Hat'] and part_names('Prop', 3) == ['Prop_1', 'Prop_2', 'Prop_3'] and part_names('Hat', 0) == []
    return 'modfit OK (box fit exact, <=2cm seam weld straight/corner, untouched beyond tol, hull containment, armfix rest bones, part names)'


if __name__ == '__main__':
    print(selftest())

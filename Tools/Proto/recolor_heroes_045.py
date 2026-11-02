"""045 stage 3 v2: recolour the one base hero into the six identity colours (design D2 + D2 supplement). Blender headless:
  Blender -b --factory-startup -P Tools/Proto/recolor_heroes_045.py -- <asset dir> mask|apply
Reads <asset>/retarget/model.fbx (skinned mesh, UVs) and <asset>/unity-input/basecolor.png (base colour #d79b68).
Writes <asset>/recolor/<hero>.png for the six heroes, recolor.json and an overlay render sheet for the visual check.

Contract:
- Allowed parts (geometry, independent of colour): faces whose dominant bone is the torso (Hips/Spine/Chest/UpperChest),
  shoulders, upper/lower arms, upper/lower legs, and Head faces of the hair (above the hairline, or facing backward/up).
  Side-of-head faces below the hairline are split per texel by 3D position: texels more than 10% of the head depth
  behind the face plane may be recoloured, the front part is protected (visual review: skin is about the front third).
- Cluster seeds come from this model (hair top, front face centre, an eye), not fixed colours (the generated skin is
  darker than the concept); identity/skin centres closer than 10 (Lab) -> BLOCKED.
  Protected parts: hands and fingers, feet and toes, neck, eyes, and Head faces of the face (front/side, below the hairline).
  The hairline height comes from rays cast at the front of the head (lowest identity-coloured hit above the eyes).
- Colour clusters (Lab k-means, k=4, on texels used by any face): identity / skin / dark / other. Skin and dark texels are
  protected wherever they are. Target = allowed-face texels in the identity cluster, minus every texel that intersects a
  protected face (exact texel-square / triangle separating-axis test) and minus skin/dark texels.
- Ambiguous texels (allowed faces, cluster 'other') above 1% of allowed texels -> BLOCKED.
- Bruno (base colour) is a byte copy. Others: hue/saturation := target's, V' = min(1, V * V_target / V_median_identity).
- Every saved PNG is re-read: changed texels within target, protected and alpha changes 0, per-part changes 0 (face, hands,
  feet, neck, skin cluster, dark cluster), saved RGB within 1/255 per channel of the expected recoloured RGB for unclipped
  target texels (8-bit quantization makes a 2-degree hue limit impossible for low-saturation colours such as hans;
  hue/sat/V errors are still reported), clipped texels <= 0.5% of target.
- Counterexample: a copy whose face texels are painted the base colour is recoloured; those texels must stay unchanged.
"""
import bpy, colorsys, hashlib, json, math, sys
from pathlib import Path
from mathutils import Vector
import numpy as np

args = sys.argv[sys.argv.index('--') + 1:]; asset = Path(args[0]).resolve(); MODE = args[1] if len(args) > 1 else 'mask'
# impl review v2 r001 #4: two phases. 'mask' writes the masks + overlay for an independent review and stops; 'apply'
# recolours only with the reviewed mask (recolor-mask/mask-review.json must name the same mask sha256 and accept it).
MASKDIR = asset / 'recolor-mask'
OUT = MASKDIR if MODE == 'mask' else asset / 'recolor'
if MODE not in ('mask', 'apply'): raise SystemExit('MODE')
if OUT.exists(): raise SystemExit('OUTPUT_EXISTS')
if MODE == 'apply':
    rv = json.loads((MASKDIR / 'mask-review.json').read_text()) if (MASKDIR / 'mask-review.json').exists() else None
    mj = json.loads((MASKDIR / 'mask.json').read_text()) if (MASKDIR / 'mask.json').exists() else None
    if not rv or not mj or rv.get('verdict') not in ('ACCEPT', 'ACCEPT_WITH_NOTE') or rv.get('mask_sha256') != mj.get('mask_sha256') \
            or hashlib.sha256((MASKDIR / 'masks.npz').read_bytes()).hexdigest() != mj.get('mask_sha256'):
        raise SystemExit('MASK_NOT_REVIEWED')
OUT.mkdir()
HEROES = dict(bruno='#d79b68', hans='#7f9b82', lena='#c6a660', yuki='#91b7c2', bolt='#a590b8', mira='#cd8274')
BASE = HEROES['bruno']
rep = dict(scope='045 stage 3 v2 identity recolour (local, no provider call)', asset=asset.name, heroes=HEROES, mode=MODE)


def finish(status, **kw):
    rep.update(status=status, **kw); (OUT / ('mask.json' if MODE == 'mask' else 'recolor.json')).write_text(json.dumps(rep, indent=2, ensure_ascii=False) + '\n')
    print('RESULT ' + json.dumps({k: v for k, v in rep.items() if k in ('status', 'reason', 'counts', 'heroes_check', 'counterexample')}, ensure_ascii=False))
    if status != 'PASS': raise SystemExit(status + ':' + str(kw.get('reason', '')))


def hex_rgb(h): return np.array([int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)])


bpy.ops.wm.read_factory_settings(use_empty=True)
MODEL = asset / 'fixed/model.fbx' if (asset / 'fixed/model.fbx').exists() else asset / 'retarget/model.fbx'   # facing-fixed rig (S3-14)
rep['model'] = str(MODEL); rep['model_sha256'] = hashlib.sha256(MODEL.read_bytes()).hexdigest()
bpy.ops.import_scene.fbx(filepath=str(MODEL))
arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE'); mesh = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
for o in bpy.context.scene.objects:
    if o.animation_data: o.animation_data.action = None
arm.data.pose_position = 'REST'
for md in mesh.modifiers:                     # ray casts must hit the undeformed bind mesh used for the UV lookup
    if md.type == 'ARMATURE': md.show_viewport = False
bpy.context.view_layer.update()
src_png = asset / 'unity-input/basecolor.png'
img = bpy.data.images.load(str(src_png)); W, H = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)
rep.update(src_sha256=hashlib.sha256(src_png.read_bytes()).hexdigest(), texture=[W, H])


def bone(n): return arm.matrix_world @ arm.data.bones[n].head_local


def front_from(l, r):
    right = bone(r) - bone(l); right.z = 0; right.normalize(); return Vector((0, 0, 1)).cross(right)


fe, fh = front_from('Left_Eye', 'Right_Eye'), front_from('Left_Hand', 'Right_Hand')
if math.degrees(fe.angle(fh)) > 20: finish('BLOCKED', reason='FACING_AMBIGUOUS')
front = (fe + fh).normalized(); eye_z = (bone('Left_Eye').z + bone('Right_Eye').z) / 2

# --- dominant bone per triangle -------------------------------------------------------------------------------------
me = mesh.data; me.calc_loop_triangles(); uvl = me.uv_layers.active.data; M = mesh.matrix_world; N = M.to_3x3()
gname = {g.index: g.name for g in mesh.vertex_groups}
def dominant(t):
    acc = {}
    for vi in t.vertices:
        for g in me.vertices[vi].groups:
            acc[gname.get(g.group, '?')] = acc.get(gname.get(g.group, '?'), 0) + g.weight
    return max(acc, key=acc.get) if acc else '?'


TORSO = {'Hips', 'Spine', 'Chest', 'UpperChest'}
LIMB = {f'{s}_{b}' for s in ('Left', 'Right') for b in ('Shoulder', 'UpperArm', 'LowerArm', 'UpperLeg', 'LowerLeg')}
FINGERS = ('Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little', 'Pinky')
def protected_bone(b): return any(k in b for k in FINGERS + ('Foot', 'Toe', 'Neck', 'Eye', 'Jaw'))

# hairline: cast rays at the front of the head from above the eyes downward; the lowest hit that is identity-coloured
dg = bpy.context.evaluated_depsgraph_get(); Mi = M.inverted(); tris_by_poly = {}
for t in me.loop_triangles: tris_by_poly.setdefault(t.polygon_index, []).append(t)
from mathutils.geometry import barycentric_transform
def tex_at(loc, idx):
    lp = Mi @ loc; best = None
    for t in tris_by_poly.get(idx, []):          # the triangle that contains the hit point
        A, B, C = (me.vertices[i].co for i in t.vertices)
        w = barycentric_transform(lp, A, B, C, Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
        if best is None or min(w) > best[0]: best = (min(w), t)
    if best is None: return None
    t = best[1]; A, B, C = (me.vertices[i].co for i in t.vertices)
    uv = barycentric_transform(lp, A, B, C, *(Vector((uvl[li].uv[0], uvl[li].uv[1], 0)) for li in t.loops))
    return px[min(H - 1, max(0, int(uv.y * H))), min(W - 1, max(0, int(uv.x * W))), :3]
base = hex_rgb(BASE); top_z = max((M @ v.co).z for v in me.vertices)
side = Vector((0, 0, 1)).cross(front)
def ray_hit(o, d):
    ok, loc, nrm, idx, obj, _ = bpy.context.scene.ray_cast(dg, o, d)
    return tex_at(loc, idx) if ok and obj == mesh else None
hc = bone('Head'); hair_ref = ray_hit(Vector((hc.x, hc.y, top_z + 1)), Vector((0, 0, -1)))
face_ref = ray_hit(Vector((hc.x, hc.y, (eye_z + bone('Neck').z) / 2)) + front * 2, -front)
eye_ref = ray_hit(Vector(bone('Left_Eye')) + front * 2, -front)
if hair_ref is None or face_ref is None or eye_ref is None or np.abs(hair_ref - face_ref).sum() < 0.15: finish('BLOCKED', reason='HAIR_FACE_REFERENCE')
rep.update(hair_ref_rgb=[round(float(x) * 255) for x in hair_ref], face_ref_rgb=[round(float(x) * 255) for x in face_ref], eye_ref_rgb=[round(float(x) * 255) for x in eye_ref])
hair_hits = []
for k in range(41):
    z = eye_z + (top_z - eye_z) * k / 40
    for sx in (-0.06, 0.0, 0.06):
        c = ray_hit(Vector((hc.x, hc.y, z)) + side * sx + front * 2, -front)
        # nearest of hair / face / eye reference (the eyes are dark and must not count as hair)
        if c is not None and np.abs(c - hair_ref).sum() < min(np.abs(c - face_ref).sum(), np.abs(c - eye_ref).sum()): hair_hits.append(z)
hair_hits = [z for z in hair_hits if z > eye_z + 0.01]
if not hair_hits: finish('BLOCKED', reason='NO_HAIRLINE')
hairline = min(hair_hits)
rep.update(eye_z=round(eye_z, 4), hairline_z=round(hairline, 4), top_z=round(top_z, 4))

parts = {}   # triangle index -> part name
for t in me.loop_triangles:
    b = dominant(t); c = M @ (sum((me.vertices[i].co for i in t.vertices), Vector()) / 3); n = (N @ t.normal).normalized()
    if protected_bone(b): parts[t.index] = 'hand' if any(k in b for k in FINGERS) else ('foot' if ('Foot' in b or 'Toe' in b) else 'neck')
    elif b == 'Head':
        hair = c.z >= hairline - 1e-4 or n.dot(front) < -0.2 or n.z > 0.7
        # side of the head below the hairline: front part skin, back part hair inside the same face -> per-texel rule
        parts[t.index] = 'hair' if hair else ('side' if abs(n.dot(front)) <= 0.5 else 'face')
    elif b in TORSO: parts[t.index] = 'torso'
    elif b in LIMB: parts[t.index] = 'limb'
    else: parts[t.index] = 'other_bone:' + b
unknown = sorted({p for p in parts.values() if p.startswith('other_bone')})
if unknown: finish('BLOCKED', reason='UNCLASSIFIED_BONES', unknown=unknown)
ALLOWED = {'hair', 'torso', 'limb'}
head_f = [((M @ me.vertices[i].co).dot(front)) for t in me.loop_triangles if dominant(t) == 'Head' for i in t.vertices]
f_front, f_back = max(head_f), min(head_f); SIDE_CUT = f_front - 0.10 * (f_front - f_back)
rep.update(head_depth=round(f_front - f_back, 4), side_cut_from_front=0.10)


# --- exact conservative raster (texel squares intersecting a triangle) ---------------------------------------------------
def raster_exact(mask, uvs):
    p = np.array([(u * W, v * H) for u, v in uvs], dtype=np.float64)
    x0 = max(0, int(np.floor(p[:, 0].min())) - 1); x1 = min(W - 1, int(np.floor(p[:, 0].max())) + 1)
    y0 = max(0, int(np.floor(p[:, 1].min())) - 1); y1 = min(H - 1, int(np.floor(p[:, 1].max())) + 1)
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1, dtype=np.float64), np.arange(y0, y1 + 1, dtype=np.float64))
    hit = (xs <= p[:, 0].max()) & (xs + 1 >= p[:, 0].min()) & (ys <= p[:, 1].max()) & (ys + 1 >= p[:, 1].min())
    cx, cy = xs + .5, ys + .5
    for k in range(3):
        a, b = p[k], p[(k + 1) % 3]; nx, ny = -(b[1] - a[1]), b[0] - a[0]
        if nx == 0 and ny == 0: continue
        proj = p[:, 0] * nx + p[:, 1] * ny; r = 0.5 * (abs(nx) + abs(ny)); c = cx * nx + cy * ny
        hit &= (c + r >= proj.min()) & (c - r <= proj.max())
    mask[y0:y1 + 1, x0:x1 + 1] |= hit


def raster_points(mask, uvs, step=0.05):
    p = np.array([(u * W, v * H) for u, v in uvs], dtype=np.float64)
    L = max(np.linalg.norm(p[1] - p[0]), np.linalg.norm(p[2] - p[1]), np.linalg.norm(p[0] - p[2]))
    n = int(np.ceil(L / step)) + 1; i, j = np.meshgrid(np.arange(n + 1), np.arange(n + 1)); keep = i + j <= n
    pts = p[0] + np.outer(i[keep] / n, p[1] - p[0]) + np.outer(j[keep] / n, p[2] - p[0])
    mask[np.clip(np.floor(pts[:, 1]).astype(int), 0, H - 1), np.clip(np.floor(pts[:, 0]).astype(int), 0, W - 1)] = True


def clip_poly(poly, front_side):
    """Sutherland-Hodgman on (f, u, v) vertices against f >= SIDE_CUT (front piece) or f <= SIDE_CUT (back piece)"""
    keep = (lambda f: f >= SIDE_CUT) if front_side else (lambda f: f <= SIDE_CUT); out = []
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]; ia, ib = keep(a[0]), keep(b[0])
        if ia: out.append(a)
        if ia != ib:
            t = (SIDE_CUT - a[0]) / (b[0] - a[0]); out.append(tuple(a[k] + t * (b[k] - a[k]) for k in range(3)))
    return out


def raster_side(allow, deny, deny_pts, uvs, wpos):
    """impl review v2 r001 #2: split the side triangle at the plane f = SIDE_CUT. The front piece is protected with the
    exact texel/triangle test (every texel square it touches); the back piece may be recoloured; protection wins where
    they share texels. deny_pts gets a point-sampled copy of the front piece for the independent cross-check."""
    poly = [(wpos[k], uvs[k][0], uvs[k][1]) for k in range(3)]
    for front_side, mask in ((True, deny), (False, allow)):
        piece = clip_poly(poly, front_side)
        for k in range(1, len(piece) - 1):
            tri = [(piece[0][1], piece[0][2]), (piece[k][1], piece[k][2]), (piece[k + 1][1], piece[k + 1][2])]
            raster_exact(mask, tri)
            if front_side: raster_points(deny_pts, tri)


part_mask = {k: np.zeros((H, W), bool) for k in ('hair', 'torso', 'limb', 'face', 'side', 'hand', 'foot', 'neck')}
side_allow = np.zeros((H, W), bool); side_deny = np.zeros((H, W), bool); side_deny_pts = np.zeros((H, W), bool)
protected = np.zeros((H, W), bool); protected_pts = np.zeros((H, W), bool); used = np.zeros((H, W), bool)
for t in me.loop_triangles:
    uvs = [tuple(uvl[li].uv) for li in t.loops]; part = parts[t.index]
    raster_exact(part_mask[part], uvs); raster_exact(used, uvs)
    if part == 'side':
        raster_side(side_allow, side_deny, side_deny_pts, uvs, [(M @ me.vertices[i].co).dot(front) for i in t.vertices])
    elif part not in ALLOWED: raster_exact(protected, uvs); raster_points(protected_pts, uvs)
protected |= side_deny                      # front piece of the side faces is protected like the face
allowed = (part_mask['hair'] | part_mask['torso'] | part_mask['limb'] | side_allow) & ~protected
# independent protected region (point samples of every protected face and of the front piece of every side face; built
# without the exact raster): used for the cross-check, the counterexample and the per-hero output check
indep = protected_pts | side_deny_pts
if (indep & ~protected).any(): finish('FAIL', reason='PROTECTION_MASK_MISSES_POINTS')


# --- Lab k-means on used texels ---------------------------------------------------------------------------------------
def to_lab(rgb):
    c = np.where(rgb > 0.04045, ((rgb + 0.055) / 1.055) ** 2.4, rgb / 12.92)
    X = c @ np.array([[0.4124, 0.2126, 0.0193], [0.3576, 0.7152, 0.1192], [0.1805, 0.0722, 0.9505]])
    X = X / np.array([0.95047, 1.0, 1.08883]); f = np.where(X > 0.008856, np.cbrt(X), 7.787 * X + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


lab = to_lab(px[..., :3]); samp = lab[used][::5]
# seeds from this model: identity = hair top, skin = front face centre between eyes and chin, dark = an eye
def ray_col(o, d):
    ok, loc, nrm, idx, obj, _ = bpy.context.scene.ray_cast(dg, o, d)
    return tex_at(loc, idx) if ok and obj == mesh else None
head_top_c = ray_col(Vector((0, 0, top_z + 1)), Vector((0, 0, -1)))
chin_z = min((M @ me.vertices[i].co).z for t in me.loop_triangles if parts[t.index] == 'face' for i in t.vertices)
skin_c = ray_col(Vector((0, 0, (eye_z + chin_z) / 2)) + front * 2, -front)
eye_c = ray_col(Vector(bone('Left_Eye')) + front * 2, -front)
if head_top_c is None or skin_c is None or eye_c is None: finish('BLOCKED', reason='SEED_RAY_MISSED')
rep['seeds_rgb'] = dict(identity=[round(float(x) * 255) for x in head_top_c], skin=[round(float(x) * 255) for x in skin_c], dark=[round(float(x) * 255) for x in eye_c])
seeds = to_lab(np.array([head_top_c, skin_c, eye_c, np.array([.5, .5, .5])]))   # identity, skin, dark, other
cent = seeds.copy()
for _ in range(25):
    lab_d = ((samp[:, None, :] - cent[None, :, :]) ** 2).sum(-1); lbl = lab_d.argmin(1)
    for k in range(4):
        if (lbl == k).any(): cent[k] = samp[lbl == k].mean(0)
cl = ((lab[..., None, :] - cent[None, None, :, :]) ** 2).sum(-1).argmin(-1)
NAMES = ['identity', 'skin', 'dark', 'other']; cluster = {n: (cl == i) & used for i, n in enumerate(NAMES)}
rep['clusters'] = {n: dict(lab=[round(float(x), 1) for x in cent[i]], texels=int(cluster[n].sum())) for i, n in enumerate(NAMES)}
if np.linalg.norm(cent[0] - seeds[0]) > 25: finish('BLOCKED', reason='IDENTITY_CLUSTER_DRIFTED')
rep['cluster_separation_identity_skin_lab'] = round(float(np.linalg.norm(cent[0] - cent[1])), 2)
if rep['cluster_separation_identity_skin_lab'] < 10: finish('BLOCKED', reason='SKIN_IDENTITY_NOT_SEPARABLE')

target = allowed & cluster['identity'] & ~protected & ~cluster['skin'] & ~cluster['dark']
ambiguous = allowed & ~protected & cluster['other']
rep['counts'] = dict(allowed=int(allowed.sum()), protected=int(protected.sum()), target=int(target.sum()), ambiguous=int(ambiguous.sum()),
                     **{f'part_{k}': int(v.sum()) for k, v in part_mask.items()})
if ambiguous.sum() > 0.01 * max(1, allowed.sum()): finish('BLOCKED', reason='AMBIGUOUS_TEXELS')
if target.sum() < 0.3 * allowed.sum(): finish('BLOCKED', reason='TARGET_TOO_SMALL')


# --- counterexample (impl review v2 r001 #2): paint the INDEPENDENT protected region with the base colour, recompute
# the clusters on that copy, and check the target never reaches it; a deliberately broken mask (side faces fully
# allowed, no protection) must reach it, proving the counterexample can fail
def target_for(img, allowed_m, protected_m):
    lab_f = to_lab(img[..., :3]); c_f = ((lab_f[..., None, :] - cent[None, None, :, :]) ** 2).sum(-1).argmin(-1)
    return allowed_m & (c_f == 0) & used & ~protected_m & ~((c_f == 1) | (c_f == 2))
fake = px.copy(); fake[..., :3][indep] = hex_rgb(BASE)
t_ok = target_for(fake, allowed, protected)
broken_allowed = part_mask['hair'] | part_mask['torso'] | part_mask['limb'] | part_mask['side'] | part_mask['face']
t_bad = target_for(fake, broken_allowed, np.zeros_like(protected))
rep['counterexample'] = dict(independent_texels=int(indep.sum()), target_hits_with_mask=int((t_ok & indep).sum()),
                             target_hits_with_broken_mask=int((t_bad & indep).sum()))
rep['counterexample']['status'] = 'PASS' if indep.sum() > 0 and rep['counterexample']['target_hits_with_mask'] == 0 and rep['counterexample']['target_hits_with_broken_mask'] > 0 else 'FAIL'

if MODE == 'mask':
    npz = OUT / 'masks.npz'
    np.savez_compressed(npz, target=target, allowed=allowed, protected=protected, ambiguous=ambiguous, used=used, indep=indep,
                        skin=cluster['skin'], dark=cluster['dark'], **{f'part_{k}': v for k, v in part_mask.items()}, side_deny=side_deny)
    rep['mask_sha256'] = hashlib.sha256(npz.read_bytes()).hexdigest()
    # overlay for the independent review: recoloured target = magenta, protected = cyan
    ov = px.copy(); ov[..., :3][target] = ov[..., :3][target] * 0.4 + np.array([1, 0, 1]) * 0.6
    pm = protected & used; ov[..., :3][pm] = ov[..., :3][pm] * 0.5 + np.array([0, 1, 1]) * 0.5
    ovp = OUT / 'overlay-texture.png'
    im = bpy.data.images.new('ov', W, H, alpha=True); im.pixels = ov.astype(np.float32).ravel().tolist(); im.filepath_raw = str(ovp); im.file_format = 'PNG'; im.save()
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.samples = 16; sc.render.resolution_x = sc.render.resolution_y = 360
    wd = bpy.data.worlds.new('w'); sc.world = wd; wd.use_nodes = True; wd.node_tree.nodes['Background'].inputs[0].default_value = (.6, .6, .6, 1); wd.node_tree.nodes['Background'].inputs[1].default_value = 1.0
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = top_z * 1.5
    ovimg = bpy.data.images.load(str(ovp))
    for m in mesh.data.materials:
        for nd in m.node_tree.nodes:
            if nd.type == 'TEX_IMAGE': nd.image = ovimg
        m.node_tree.update_tag()
    shots = []
    for name, yaw, pitch in (('front', 0, 55), ('left', 90, 55), ('back', 180, 55), ('right', 270, 55), ('front0', 0, 0), ('left0', 90, 0)):
        y = math.radians(yaw); f = Vector((front.x * math.cos(y) - front.y * math.sin(y), front.x * math.sin(y) + front.y * math.cos(y), 0))
        d = (f * math.cos(math.radians(pitch)) + Vector((0, 0, math.sin(math.radians(pitch))))).normalized()
        cam.location = Vector((0, 0, top_z / 2)) + d * 10; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = str(OUT / f'overlay-{name}.png'); bpy.ops.render.render(write_still=True); shots.append(f'overlay-{name}.png')
    rep['overlay'] = shots; rep['next'] = 'independent review of the overlay -> recolor-mask/mask-review.json {verdict, mask_sha256} -> apply'
    finish('PASS' if rep['counterexample']['status'] == 'PASS' else 'FAIL', reason='' if rep['counterexample']['status'] == 'PASS' else 'COUNTEREXAMPLE')
    sys.exit(0)

# apply: the masks are taken from the reviewed file (same sha256), not recomputed
Z = np.load(MASKDIR / 'masks.npz'); target, protected, indep = Z['target'], Z['protected'], Z['indep']
rep['mask_sha256'] = mj['mask_sha256']; rep['mask_review'] = rv
same_now = bool((Z['target'] == (allowed & cluster['identity'] & ~protected & ~cluster['skin'] & ~cluster['dark'])).all())
rep['mask_recomputed_identical'] = same_now
if not same_now: finish('BLOCKED', reason='MASK_CHANGED_SINCE_REVIEW')

# --- recolour ---------------------------------------------------------------------------------------------------------
def hsv_img(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]; mx = rgb.max(-1); mn = rgb.min(-1); d = mx - mn
    h = np.zeros_like(mx); nz = d > 1e-9
    rr = nz & (mx == r); gg = nz & (mx == g) & ~rr; bb = nz & ~rr & ~gg
    h[rr] = ((g - b)[rr] / d[rr]) % 6; h[gg] = (b - r)[gg] / d[gg] + 2; h[bb] = (r - g)[bb] / d[bb] + 4
    return np.stack([h / 6, np.where(mx > 1e-9, d / np.maximum(mx, 1e-9), 0), mx], -1)


def rgb_img(hsv):
    h, s, v = hsv[..., 0] * 6, hsv[..., 1], hsv[..., 2]; i = np.floor(h).astype(int) % 6; f = h - np.floor(h)
    p, q, t = v * (1 - s), v * (1 - s * f), v * (1 - s * (1 - f))
    out = np.zeros(hsv.shape); sel = [(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]
    for k, (a, b, c) in enumerate(sel):
        m = i == k; out[..., 0][m], out[..., 1][m], out[..., 2][m] = a[m], b[m], c[m]
    return out


HSV = hsv_img(px[..., :3].astype(np.float64)); vmed = float(np.median(HSV[..., 2][target]))
rep['v_median_identity'] = round(vmed, 4)


def recolour(src_px, hexcol, tmask):
    th, ts, tv = colorsys.rgb_to_hsv(*hex_rgb(hexcol)); hsv = hsv_img(src_px[..., :3].astype(np.float64))
    newv = hsv[..., 2] * tv / vmed; clipped = tmask & (newv > 1.0)
    hsv2 = hsv.copy(); hsv2[..., 0][tmask] = th; hsv2[..., 1][tmask] = ts; hsv2[..., 2][tmask] = np.minimum(1.0, newv[tmask])
    out = src_px.copy(); out[..., :3][tmask] = rgb_img(hsv2)[tmask]
    return out, clipped, (th, ts, tv)


def save_png(arr, path):
    im = bpy.data.images.new(path.stem, W, H, alpha=True); im.pixels = arr.astype(np.float32).ravel().tolist()
    im.filepath_raw = str(path); im.file_format = 'PNG'; im.save(); bpy.data.images.remove(im)
    back = bpy.data.images.load(str(path)); a = np.array(back.pixels[:], dtype=np.float32).reshape(H, W, 4); bpy.data.images.remove(back); return a


checks = {}
for hero, col in HEROES.items():
    path = OUT / f'{hero}.png'
    if hero == 'bruno':
        path.write_bytes(src_png.read_bytes()); same = hashlib.sha256(path.read_bytes()).hexdigest() == rep['src_sha256']
        checks[hero] = dict(copy=True, identical=same); continue
    out, clipped, (th, ts, tv) = recolour(px, col, target); disk = save_png(out, path)
    ch = np.abs(disk[..., :3] - px[..., :3]).max(-1) > 0.5 / 255
    hsvd = hsv_img(disk[..., :3].astype(np.float64)); ok_t = target & ~clipped & (hsvd[..., 2] > 0.05)
    # 8-bit quantization moves the hue of a low-saturation colour by more than 2 deg (hans, S=0.18), so the judged check
    # is the saved RGB against the expected RGB within 1/255 per channel (a quantization-aware criterion, not uniformly
    # stricter than the HSV one; impl review v2 r001 #6); hue/sat/V errors are reported only
    exp_hsv = HSV.copy(); exp_hsv[..., 0] = th; exp_hsv[..., 1] = ts; exp_hsv[..., 2] = np.minimum(1.0, HSV[..., 2] * tv / vmed)
    rgb_err = np.abs(disk[..., :3].astype(np.float64) - rgb_img(exp_hsv))[target & ~clipped].max() if (target & ~clipped).any() else 0
    hue_err = np.abs(((hsvd[..., 0][ok_t] - th) * 360 + 180) % 360 - 180); sat_err = np.abs(hsvd[..., 1][ok_t] - ts)
    v_exp = np.minimum(1.0, HSV[..., 2] * tv / vmed); v_err = np.abs(hsvd[..., 2] - v_exp)[ok_t]
    c = dict(changed=int(ch.sum()), outside_target=int((ch & ~target).sum()), protected=int((ch & protected).sum()),
             alpha=int((np.abs(disk[..., 3] - px[..., 3]) > 0.5 / 255).sum()), clipped=int(clipped.sum()),
             hue_err_max=round(float(hue_err.max()), 3) if hue_err.size else 0, sat_err_max=round(float(sat_err.max()), 4) if sat_err.size else 0,
             v_err_max=round(float(v_err.max()), 5) if v_err.size else 0, rgb_err_max_255=round(float(rgb_err) * 255, 3),
             **{f'part_{k}_changed': int((ch & part_mask[k] & ~allowed).sum()) for k in ('face', 'hand', 'foot', 'neck')},
             side_front_changed=int((ch & Z['side_deny']).sum()), independent_changed=int((ch & indep).sum()),
             skin_changed=int((ch & cluster['skin']).sum()), dark_changed=int((ch & cluster['dark']).sum()), sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    # hue checks are only meaningful for chromatic targets; low-saturation pixels have unstable hue
    c['pass'] = (c['outside_target'] == 0 and c['protected'] == 0 and c['alpha'] == 0 and c['clipped'] <= 0.005 * target.sum()
                 and c['rgb_err_max_255'] <= 1.0 + 1e-3
                 and all(c[f'part_{k}_changed'] == 0 for k in ('face', 'hand', 'foot', 'neck')) and c['side_front_changed'] == 0 and c['independent_changed'] == 0
                 and c['skin_changed'] == 0 and c['dark_changed'] == 0)
    checks[hero] = c
rep['heroes_check'] = {k: v.get('pass', v.get('identical')) for k, v in checks.items()}; rep['heroes_detail'] = checks

ok = all(rep['heroes_check'].values()) and rep['counterexample']['status'] == 'PASS'
finish('PASS' if ok else 'FAIL', reason='' if ok else 'CHECK_FAILED')

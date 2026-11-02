"""045 stage 3: remove the eyes Tripo painted on the BACK of the hero's head (single front image -> mirrored face).
Blender headless, no provider calls:
  Blender -b --factory-startup -P Tools/Proto/fix_hero_back_eyes_045.py -- <asset dir>
Reads <asset>/retarget/model.fbx (rig + UVs) and <asset>/unity-input/basecolor.png. Front direction comes from the eye and
hand bones (they must agree within 20 deg). Texels used by head faces (above the neck bone) whose normal points backward
(dot < -0.3) are the back-head mask; dark texels (luminance < 0.3) inside it, grown by 2 px to cover anti-aliased edges,
are painted one flat colour: the median of the light (lum > 0.45) back-head texels at eye height (attempt 1 diffused from
island borders and pulled in dark gutter colours). Independent visual review (design-reviewer, D1): a skin band left on
the back of the head still reads as a face, so the back-facing (dot < -0.2) skin-like texels at eye height are painted the
hair (identity) colour = median of light back-head texels above that band. Codex review s3 r001 #1: every face that is not
allowed to change (body, hands, sides, front, below the neck) is protected; texels shared with a protected face are left
unchanged (stop if an eye texel is shared), and the saved PNG is re-read to prove nothing outside the target changed. Writes <asset>/unity-input/basecolor-fixed.png, back-eyes-fix.json and before/after renders.
"""
import bpy, hashlib, json, math, sys
from pathlib import Path
from mathutils import Vector
import numpy as np

asset = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
src_png = asset / 'unity-input/basecolor.png'; out_png = asset / 'unity-input/basecolor-fixed.png'
rep_path = asset / 'unity-input/back-eyes-fix.json'
if out_png.exists() or rep_path.exists():
    raise SystemExit('OUTPUT_EXISTS')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(asset / 'retarget/model.fbx'))
arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
mesh = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
for o in bpy.context.scene.objects:
    if o.animation_data: o.animation_data.action = None
arm.data.pose_position = 'REST'; bpy.context.view_layer.update()


def bone(n):
    return arm.matrix_world @ arm.data.bones[n].head_local


def front_from(l, r):
    right = bone(r) - bone(l); right.z = 0; right.normalize()
    return Vector((0, 0, 1)).cross(right)   # Blender is right-handed, Z up: front = up x right


f_eyes, f_hands = front_from('Left_Eye', 'Right_Eye'), front_from('Left_Hand', 'Right_Hand')
agree = math.degrees(f_eyes.angle(f_hands))
if agree > 20:
    raise SystemExit(f'FACING_AMBIGUOUS:{agree:.1f}')
front = (f_eyes + f_hands).normalized(); neck_z = bone('Neck').z

img = bpy.data.images.load(str(src_png)); W, H = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)   # row 0 = bottom (Blender), same as UV v=0
me = mesh.data; me.calc_loop_triangles(); uv = me.uv_layers.active.data; M = mesh.matrix_world; N = M.to_3x3()
back = np.zeros((H, W), bool); frontm = np.zeros((H, W), bool); nb = nf = 0; back_tris = []; rear_tris = []


def raster(mask, uvs, e=-0.02):   # e < 0: include texels touching the edge; e > 0: strict interior only
    p = [(u * W, v * H) for u, v in uvs]
    x0 = max(0, int(min(q[0] for q in p)) - 1); x1 = min(W - 1, int(max(q[0] for q in p)) + 1)
    y0 = max(0, int(min(q[1] for q in p)) - 1); y1 = min(H - 1, int(max(q[1] for q in p)) + 1)
    (ax, ay), (bx, by), (cx, cy) = p
    den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(den) < 1e-9: return
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
    w0 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / den; w1 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / den; w2 = 1 - w0 - w1
    mask[y0:y1 + 1, x0:x1 + 1] |= (w0 >= e) & (w1 >= e) & (w2 >= e)


alltris = []   # (index, centre z, dot with front, uvs) of every triangle, any height: the protection mask needs all of them
def raster_exact(mask, uvs):
    """conservative: every texel square [x,x+1]x[y,y+1] that intersects the triangle (touching counts), by the
    separating-axis test on the box axes and the three edge normals (impl review s3 r003 #1: a barycentric margin at
    the texel centre misses texels that a thin triangle only clips)."""
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
    """independent cross-check: texels containing points sampled densely along the edges and inside the triangle"""
    p = np.array([(u * W, v * H) for u, v in uvs], dtype=np.float64)
    L = max(np.linalg.norm(p[1] - p[0]), np.linalg.norm(p[2] - p[1]), np.linalg.norm(p[0] - p[2]))
    n = int(np.ceil(L / step)) + 1; i, j = np.meshgrid(np.arange(n + 1), np.arange(n + 1)); keep = i + j <= n
    u_, v_ = (i[keep] / n), (j[keep] / n); pts = p[0] + np.outer(u_, p[1] - p[0]) + np.outer(v_, p[2] - p[0])
    xi = np.clip(np.floor(pts[:, 0]).astype(int), 0, W - 1); yi = np.clip(np.floor(pts[:, 1]).astype(int), 0, H - 1)
    mask[yi, xi] = True


for t in me.loop_triangles:
    c = M @ (sum((me.vertices[i].co for i in t.vertices), Vector()) / 3)
    n = (N @ t.normal).normalized(); d = n.dot(front)
    uvs = [tuple(uv[li].uv) for li in t.loops]
    alltris.append((t.index, c.z, d, uvs))
    if c.z <= neck_z: continue
    if d < -0.2: rear_tris.append((c.z, uvs))
    if d < -0.3: raster(back, uvs); nb += 1; back_tris.append((c.z, uvs))
    elif d > 0.3: raster(frontm, uvs); nf += 1

lum = px[..., 0] * .299 + px[..., 1] * .587 + px[..., 2] * .114
dark = lum < 0.3


def grow(m, r):
    g = m.copy()
    for _ in range(r):
        g = g | np.roll(g, 1, 0) | np.roll(g, -1, 0) | np.roll(g, 1, 1) | np.roll(g, -1, 1)
    return g


target = grow(back & dark, 2) & back & ~frontm
shared = int((back & frontm & dark).sum())
report = dict(scope='045 stage 3 hero back-of-head eye removal (texture only, no provider call)', asset=asset.name,
              src_sha256=hashlib.sha256(src_png.read_bytes()).hexdigest(), texture=[W, H], facing_agreement_deg=round(agree, 2),
              head_back_faces=nb, head_front_faces=nf, back_texels=int(back.sum()), front_texels=int(frontm.sum()),
              dark_back_texels=int((back & dark).sum()), dark_texels_shared_with_front=shared, target_texels=int(target.sum()))
if shared > 0:
    report['status'] = 'BLOCKED'; rep_path.write_text(json.dumps(report, indent=2) + '\n'); raise SystemExit('SHARED_UV_WITH_FRONT')
if report['target_texels'] == 0:
    report['status'] = 'NOTHING_TO_FIX'; rep_path.write_text(json.dumps(report, indent=2) + '\n'); raise SystemExit('NOTHING_TO_FIX')
# eye height = heights of back-head faces that carry dark texels; the fill colour = median of light back texels there
eye_z = []
for z, uvs in back_tris:
    m = np.zeros((H, W), bool); raster(m, uvs)
    if (m & dark).sum() > 0.2 * max(1, m.sum()): eye_z.append(z)
if not eye_z:
    report['status'] = 'FAIL'; rep_path.write_text(json.dumps(report, indent=2) + '\n'); raise SystemExit('NO_EYE_FACES')
band = np.zeros((H, W), bool); zlo, zhi = min(eye_z) - 0.06, max(eye_z) + 0.06
for z, uvs in back_tris:
    if zlo <= z <= zhi: raster(band, uvs)
src = band & ~grow(dark, 3) & (lum > 0.45)
if src.sum() < 500:
    report['status'] = 'FAIL'; rep_path.write_text(json.dumps(report, indent=2) + '\n'); raise SystemExit('TOO_FEW_SKIN_TEXELS')
skin_rgb = np.median(px[..., :3][src], axis=0)
hair = np.zeros((H, W), bool)
for z, uvs in back_tris:
    if z > zhi + 0.02: raster(hair, uvs)
hsrc = hair & ~grow(dark, 3) & (lum > 0.45) & ~frontm
if hsrc.sum() < 500:
    report['status'] = 'FAIL'; rep_path.write_text(json.dumps(report, indent=2) + '\n'); raise SystemExit('TOO_FEW_HAIR_TEXELS')
hair_rgb = np.median(px[..., :3][hsrc], axis=0)
if np.abs(hair_rgb - skin_rgb).sum() < 0.08:
    report['status'] = 'FAIL'; rep_path.write_text(json.dumps(report, indent=2) + '\n'); raise SystemExit('HAIR_EQUALS_SKIN')
rear_band = np.zeros((H, W), bool)
for z, uvs in rear_tris:
    if zlo <= z <= zhi: raster(rear_band, uvs)
rgb = px[..., :3]; skinlike = (np.abs(rgb - skin_rgb).sum(axis=2) < np.abs(rgb - hair_rgb).sum(axis=2)) & ~dark
target = (target | (rear_band & skinlike)) & ~frontm
# impl review s3 r001 #1: protection = interior texels of EVERY face that is not allowed to change (body, hands, sides,
# front, anything below the neck). Allowed = back-head faces (dot < -0.3, above the neck) and rear faces at eye height.
allowed_idx = set(); protected = np.zeros((H, W), bool); protected_pts = np.zeros((H, W), bool)
for i, z, d, uvs in alltris:
    if z > neck_z and (d < -0.3 or (d < -0.2 and zlo <= z <= zhi)): allowed_idx.add(i)
for i, z, d, uvs in alltris:
    if i in allowed_idx: continue
    raster_exact(protected, uvs)   # every texel square a protected face intersects (r002 #1, r003 #1)
    raster_points(protected_pts, uvs)
# texels an allowed face shares with a protected face (overlapping UV islands and shared island borders; r001 run: 377
# interior + 215 border texels with 6 side-of-head faces, r003: 58 more texels clipped by thin faces) are left unchanged. Eye (dark) texels may never
# be shared.
excluded = target & protected; excluded_dark = int((excluded & dark).sum())
target = target & ~protected
shared_protected = int((target & protected).sum())
report.update(allowed_faces=len(allowed_idx), protected_faces=len(alltris) - len(allowed_idx), protected_texels=int(protected.sum()),
              excluded_shared_texels=int(excluded.sum()), excluded_shared_dark_texels=excluded_dark,
              target_texels_inside_protected=shared_protected,
              crosscheck_point_texels=int(protected_pts.sum()), crosscheck_points_outside_protected=int((protected_pts & ~protected).sum()))
if report['crosscheck_points_outside_protected'] > 0:
    report['status'] = 'FAIL'; rep_path.write_text(json.dumps(report, indent=2) + '\n'); raise SystemExit('PROTECTION_MASK_MISSES_POINTS')
if excluded_dark > 0:
    report['status'] = 'BLOCKED'; rep_path.write_text(json.dumps(report, indent=2) + '\n'); raise SystemExit('EYE_TEXELS_SHARED_WITH_PROTECTED')
if shared_protected > 0:
    report['status'] = 'BLOCKED'; rep_path.write_text(json.dumps(report, indent=2) + '\n'); raise SystemExit('TARGET_SHARES_PROTECTED_UV')
report.update(eye_height_range=[round(min(eye_z), 3), round(max(eye_z), 3)], skin_source_texels=int(src.sum()), hair_source_texels=int(hsrc.sum()),
              skin_rgb_8bit=[int(round(float(x) * 255)) for x in skin_rgb], hair_rgb_8bit=[int(round(float(x) * 255)) for x in hair_rgb],
              rear_band_skin_texels=int((rear_band & skinlike & ~frontm).sum()), target_texels=int(target.sum()))
out = px.copy(); out[..., :3][target] = hair_rgb
changed = np.abs(out - px).sum(axis=2) > 1e-6
report.update(changed_texels=int(changed.sum()), changed_outside_target=int((changed & ~target).sum()),
              front_texels_changed=int((changed & frontm).sum()))
fixed = bpy.data.images.new('fixed', W, H, alpha=True); fixed.pixels = out.ravel().tolist()
fixed.filepath_raw = str(out_png); fixed.file_format = 'PNG'; fixed.save()
report['fixed_sha256'] = hashlib.sha256(out_png.read_bytes()).hexdigest()
# re-read the saved PNG: every texel that differs from the source must be a target texel and none may be protected
disk = bpy.data.images.load(str(out_png)); dpx = np.array(disk.pixels[:], dtype=np.float32).reshape(H, W, 4)
dchanged = np.abs(dpx[..., :3] - px[..., :3]).max(axis=2) > 0.5 / 255
report.update(disk_changed_texels=int(dchanged.sum()), disk_changed_outside_target=int((dchanged & ~target).sum()),
              disk_changed_protected=int((dchanged & protected).sum()), disk_alpha_changed=int((np.abs(dpx[..., 3] - px[..., 3]) > 0.5 / 255).sum()))

# before/after renders (rest pose) from the back and the front at the game pitch (55 deg)
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.samples = 16; sc.cycles.device = 'CPU'   # EEVEE (background) kept the first texture
sc.render.resolution_x = sc.render.resolution_y = 320
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (.55, .62, .45, 1); w.node_tree.nodes['Background'].inputs[1].default_value = .6
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sc.collection.objects.link(sun); sun.data.energy = 3; sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'
pts = [M @ v.co for v in me.vertices]; lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
ctr = (lo + hi) / 2; cam.data.ortho_scale = (hi.z - lo.z) * 1.6
texs = [nd for m in mesh.data.materials if m and m.use_nodes for nd in m.node_tree.nodes if nd.type == 'TEX_IMAGE']   # every image node
if not texs: raise SystemExit('NO_IMAGE_NODE')
renders = []
for tag, im in (('before', img), ('after', fixed)):
    for nd in texs: nd.image = im
    for m in mesh.data.materials:
        if m: m.node_tree.update_tag()
    bpy.context.view_layer.update()
    for side, sgn in (('back', -1), ('front', 1)):
        d = (front * sgn * math.cos(math.radians(55)) + Vector((0, 0, math.sin(math.radians(55))))).normalized()
        cam.location = ctr + d * 10; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        p = asset / f'unity-input/back-eyes-{tag}-{side}55.png'; sc.render.filepath = str(p); bpy.ops.render.render(write_still=True); renders.append(p.name)
ok = (report['changed_outside_target'] == 0 and report['front_texels_changed'] == 0 and report['disk_changed_outside_target'] == 0
      and report['disk_changed_protected'] == 0 and report['disk_alpha_changed'] == 0 and report['target_texels_inside_protected'] == 0)
report.update(renders=renders, status='PASS' if ok else 'FAIL')
rep_path.write_text(json.dumps(report, indent=2) + '\n')
print('RESULT ' + json.dumps({k: v for k, v in report.items() if k not in ('renders',)}))

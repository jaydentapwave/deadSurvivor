"""045 stage 2: normalise one downloaded Tripo GLB for the 043 repaint tool (Blender headless).

Blender -b --factory-startup -P Tools/Proto/process_tripo_045.py -- <asset dir>
Reads <asset>/request-frozen.json (spec: fit axis/target/ranges/face_limit) and the single GLB under <asset>/raw.
Rules (045 stage-2 design): exactly 1 material and 1 base-color image (else BLOCKED, no auto-bake); meshes joined;
uniform scale on the fit axis, other axes must fall inside the frozen ranges (else BLOCKED); origin bottom-centre;
normal maps are ignored. Writes <asset>/delivery/{model-sized.glb, basecolor.png, metrics.json, preview.png}.
"""
import bpy, bmesh, hashlib, json, sys
from pathlib import Path
from mathutils import Vector

ROOT = Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor')
ALLOWED = [ROOT / 'output/keyart-target-045' / r / 'tripo' for r in ('r002', 'r003')]   # never Assets/ (S2D4)
arg = Path(sys.argv[sys.argv.index('--') + 1])
asset = (arg if arg.is_absolute() else ROOT / arg)


def refuse(reason):
    print(json.dumps(dict(status='BLOCKED', reason=reason, asset=str(arg))))
    sys.exit(1)


# output boundary: the asset dir must be a real (non-link) direct child of an allowed tripo folder, no link on the way
if any(p.is_symlink() for p in [asset, *asset.parents]):
    refuse('SYMLINK_IN_PATH')
if asset.resolve().parent not in [a.resolve() for a in ALLOWED] or not asset.is_dir():
    refuse('OUTSIDE_ALLOWED_DIR')
asset = asset.resolve()
if (asset / 'delivery').exists() or (asset / 'delivery').is_symlink():
    refuse('DELIVERY_EXISTS')
req = json.loads((asset / 'request-frozen.json').read_text())
spec = req['spec']
out = asset / 'delivery'
out.mkdir(exist_ok=False)
metrics = dict(scope='045 stage 2 local Blender normalisation (not Unity/game acceptance)', asset=req['asset'])


def finish(status, **kw):
    metrics.update(status=status, **kw)
    (out / 'metrics.json').write_text(json.dumps(metrics, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(dict(status=status, **{k: v for k, v in kw.items() if k != 'detail'})))
    if status != 'PASS':
        sys.exit(1)


raws = sorted((asset / 'raw').rglob('*.glb'))
if len(raws) != 1:
    finish('BLOCKED', reason='GLB_COUNT', count=len(raws))
raw = raws[0]
metrics.update(raw=str(raw), raw_sha256=hashlib.sha256(raw.read_bytes()).hexdigest())
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(raw))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes:
    finish('BLOCKED', reason='NO_MESH')
mats = sorted({m.name for o in meshes for m in o.data.materials if m})


def base_image(mat):
    if not mat.use_nodes:
        return None
    bs = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if not bs:
        return None
    def up(sock):
        for link in sock.links:
            n = link.from_node
            if n.type == 'TEX_IMAGE':
                return n.image
            for i in n.inputs:
                r = up(i)
                if r:
                    return r
        return None
    return up(bs.inputs['Base Color'])


images = {base_image(bpy.data.materials[m]) for m in mats} - {None}
metrics.update(materials=mats, base_images=[i.name for i in images], all_images=[i.name for i in bpy.data.images if i.type == 'IMAGE'])
if len(mats) != 1 or len(images) != 1:
    finish('BLOCKED', reason='NOT_SINGLE_MATERIAL_OR_IMAGE')

for o in meshes:
    m = o.matrix_world.copy()
    for v in o.data.vertices:
        v.co = m @ v.co
    o.matrix_world.identity()
for o in meshes:
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=list(bm.edges))
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(o.data); bm.free(); o.data.update()
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.name = req['asset']
# 045 stage 2 (implementation change): local decimation for many-instance props to fit the screen budget
# (stone: ~25 per ring x 6 rings). Collapse keeps UVs; no provider task. Recorded in metrics.
DECIMATE = {'stone045': 160}
if req['asset'] in DECIMATE:
    t0 = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    mod = obj.modifiers.new('045_screen_budget', 'DECIMATE'); mod.ratio = DECIMATE[req['asset']] / t0; mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bm = bmesh.new(); bm.from_mesh(obj.data)   # collapse can leave vertices no face uses; remove them before measuring
    bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=list(bm.edges))
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(obj.data); bm.free(); obj.data.update()
    metrics['decimation'] = dict(before=t0, after=sum(len(p.vertices) - 2 for p in obj.data.polygons), target=DECIMATE[req['asset']], method='Blender collapse, UV kept', loose_removed=len(loose))


def bounds():
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    lo = Vector(tuple(min(p[i] for p in pts) for i in range(3))); hi = Vector(tuple(max(p[i] for p in pts) for i in range(3)))
    return lo, hi, hi - lo


lo, hi, size = bounds()
tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
metrics.update(raw_blender_xyz=list(size), triangles=tris)
# Blender is Z-up after glTF import: height = z, horizontal = x/y
hor = sorted([size.x, size.y], reverse=True)
s = spec['target'] / (hor[0] if spec['fit'] == 'longest_horizontal' else size.z)
center = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
for v in obj.data.vertices:
    v.co = (v.co - center) * s
obj.data.update()
lo, hi, size = bounds()
dims = dict(height=size.z, longest_horizontal=max(size.x, size.y), short_horizontal=min(size.x, size.y))
bad = {k: dims[k] for k, (a, b) in spec['ranges'].items() if not (a <= dims[k] <= b)}
metrics.update(scale=s, delivery_dims=dims, ranges=spec['ranges'])
# a user-approved exception (separate file; the frozen request is never edited) may cover specific dimensions only
acc_path = asset / 'user-accept.json'
acc = json.loads(acc_path.read_text())['accept'] if acc_path.exists() else {}
waived = {k: v for k, v in bad.items() if k in acc and v <= acc[k]['accepted_max']}
if waived:
    metrics.update(user_accepted=waived, user_accept_sha256=hashlib.sha256(acc_path.read_bytes()).hexdigest())
bad = {k: v for k, v in bad.items() if k not in waived}
if bad:
    finish('BLOCKED', reason='OUT_OF_RANGE', out_of_range=bad)
if tris > int(spec['face_limit'] * 1.2):
    finish('BLOCKED', reason='TRIANGLE_BUDGET', limit=int(spec['face_limit'] * 1.2))
img = next(iter(images))
img.filepath_raw = str(out / 'basecolor.png'); img.file_format = 'PNG'; img.save()
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(out / 'model-sized.glb'), export_format='GLB', use_selection=True, export_yup=True)
# re-import check
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(out / 'model-sized.glb'))
again = [o for o in bpy.context.scene.objects if o.type == 'MESH']
obj = again[0] if len(again) == 1 else None
if not obj or not obj.data.uv_layers:
    finish('FAIL', reason='REIMPORT_MESH_OR_UV')
lo2, hi2, size2 = bounds()
checks = dict(one_mesh=len(again) == 1, uv=bool(obj.data.uv_layers), bottom_zero=abs(lo2.z) < 1e-3,
              centred=abs((lo2.x + hi2.x) / 2) < 1e-3 and abs((lo2.y + hi2.y) / 2) < 1e-3,
              size_same=all(abs(size2[i] - size[i]) < 1e-3 for i in range(3)))
finish('PASS' if all(checks.values()) else 'FAIL', checks=checks, glb_sha256=hashlib.sha256((out / 'model-sized.glb').read_bytes()).hexdigest(),
       basecolor_sha256=hashlib.sha256((out / 'basecolor.png').read_bytes()).hexdigest())

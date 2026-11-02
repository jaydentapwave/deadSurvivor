"""043: repaint a Tripo delivery model onto the shared palette (Blender headless).

Usage:
  Blender -b --factory-startup -P Tools/Art/repaint_tripo_043.py -- \
      <model-sized.glb> <basecolor.png> <palette.json> <out_dir> <rows comma list> \
      [<base cell index> <lightness compress 0..1>]

Per face: average the Tripo base color at the loop UVs and the centroid. Optional colour
alignment: move the asset's area-weighted per-channel OKLab median onto the base cell and
scale every face's deviation from it by the compress factor (removes Tripo's baked value range while keeping
which part is which colour). Then nearest palette cell in OKLab (restricted to the given
palette rows) -> 3 passes of neighbour majority smoothing -> every loop UV of the face is
set to that cell's centre. The Tripo texture is
dropped; geometry is not changed. Exports model.fbx with the same FBX settings as the 038
delivery (axis_forward=-Z, axis_up=Y, apply_unit_scale) plus repaint.json and a preview.
"""
import collections
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np

argv = sys.argv[sys.argv.index('--') + 1:]
src, base_png, palette_json, out = Path(argv[0]), Path(argv[1]), Path(argv[2]), Path(argv[3])
rows = [int(r) for r in argv[4].split(',')]
base_cell = int(argv[5]) if len(argv) > 5 else None
compress = float(argv[6]) if len(argv) > 6 else 1.0
out.mkdir(parents=True, exist_ok=True)
for target in ('model.fbx', 'repaint.json', 'preview.png'):
    if (out / target).exists():
        raise SystemExit(f'OUTPUT_EXISTS:{out / target}')


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def srgb_to_oklab(rgb):
    lin = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    m1 = np.array([[0.4122214708, 0.5363325363, 0.0514459929],
                   [0.2119034982, 0.6806995451, 0.1073969566],
                   [0.0883024619, 0.2817188376, 0.6299787005]])
    lms = np.cbrt(lin @ m1.T)
    m2 = np.array([[0.2104542553, 0.7936177850, -0.0040720468],
                   [1.9779984951, -2.4285922050, 0.4505937099],
                   [0.0259040371, 0.7827717662, -0.8086757660]])
    return lms @ m2.T


pal = json.loads(palette_json.read_text())
cells = [c for c in pal['cells'] if c['row'] in rows]
cell_lab = np.array([c['oklab'] for c in cells])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(src))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if len(meshes) != 1:
    raise SystemExit(f'EXPECTED_ONE_MESH:{len(meshes)}')
obj = meshes[0]
me = obj.data
me.calc_loop_triangles()
tris_before = len(me.loop_triangles)
verts_before = np.empty(len(me.vertices) * 3)
me.vertices.foreach_get('co', verts_before)
bbox_before = (verts_before.reshape(-1, 3).min(0).tolist(), verts_before.reshape(-1, 3).max(0).tolist())

img = bpy.data.images.load(str(base_png))
w, h = img.size
px = np.empty(w * h * 4, dtype=np.float32)
img.pixels.foreach_get(px)
px = px.reshape(h, w, 4)[:, :, :3]  # byte PNG: stored sRGB values / 255, row 0 = bottom

uv_layer = me.uv_layers.active
if uv_layer is None:
    raise SystemExit('NO_UV')
uvs = np.empty(len(me.loops) * 2)
uv_layer.data.foreach_get('uv', uvs)
uvs = uvs.reshape(-1, 2)


def sample(uv):
    x = np.clip((uv[:, 0] % 1.0) * w, 0, w - 1).astype(int)
    y = np.clip((uv[:, 1] % 1.0) * h, 0, h - 1).astype(int)
    return px[y, x]


n = len(me.polygons)
face_rgb = np.zeros((n, 3))
for p in me.polygons:
    luv = uvs[p.loop_start:p.loop_start + p.loop_total]
    samples = np.vstack([luv, luv.mean(0, keepdims=True), (luv + luv.mean(0)) * 0.5])
    face_rgb[p.index] = sample(samples).mean(0)
face_lab = srgb_to_oklab(np.clip(face_rgb, 0, 1))
face_area = np.array([p.area for p in me.polygons])
value_alignment = None
if base_cell is not None:
    def wmedian(v):
        o = np.argsort(v)
        c = np.cumsum(face_area[o])
        return float(v[o[np.searchsorted(c, c[-1] / 2)]])
    median_lab = np.array([wmedian(face_lab[:, k]) for k in range(3)])
    target_lab = np.array(next(c for c in pal['cells'] if c['index'] == base_cell)['oklab'])
    face_lab = target_lab + (face_lab - median_lab) * compress
    value_alignment = {'base_cell': base_cell, 'median_oklab_before': [round(float(x), 4) for x in median_lab], 'target_oklab': target_lab.tolist(), 'compress': compress, 'rule': 'area-weighted per-channel OKLab median moved onto the base cell; deviations scaled by compress'}
label = np.argmin(((face_lab[:, None, :] - cell_lab[None]) ** 2).sum(-1), axis=1)

# face adjacency through shared edges
edge_faces = collections.defaultdict(list)
for p in me.polygons:
    for ek in p.edge_keys:
        edge_faces[ek].append(p.index)
neighbours = [set() for _ in range(n)]
for fs in edge_faces.values():
    for a in fs:
        for b in fs:
            if a != b:
                neighbours[a].add(b)
changed_total = 0
for _ in range(3):
    new = label.copy()
    for i in range(n):
        if not neighbours[i]:
            continue
        cnt = collections.Counter(label[j] for j in neighbours[i])
        top, c = cnt.most_common(1)[0]
        if top != label[i] and c >= cnt.get(label[i], 0) + 1:
            new[i] = top
    changed_total += int((new != label).sum())
    label = new

# palette UV map replaces the Tripo UVs
pal_uv = me.uv_layers.new(name='PaletteUV')
new_uv = np.empty((len(me.loops), 2))
for p in me.polygons:
    new_uv[p.loop_start:p.loop_start + p.loop_total] = cells[label[p.index]]['uv']
pal_uv.data.foreach_set('uv', new_uv.ravel())
for layer in [l for l in me.uv_layers if l.name != 'PaletteUV']:
    me.uv_layers.remove(layer)
me.uv_layers.active = me.uv_layers['PaletteUV']

# one placeholder material; Unity assigns the 043 material
obj.data.materials.clear()
mat = bpy.data.materials.new('LowPoly043')
mat.use_nodes = True
tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
pal_img = bpy.data.images.load(str(palette_json.parent / 'palette.png'))
tex.image = pal_img
tex.interpolation = 'Closest'
bsdf = mat.node_tree.nodes.get('Principled BSDF')
mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value = 0.9
obj.data.materials.append(mat)

me.calc_loop_triangles()
verts_after = np.empty(len(me.vertices) * 3)
me.vertices.foreach_get('co', verts_after)
if len(me.loop_triangles) != tris_before or not np.array_equal(verts_before, verts_after):
    raise SystemExit('GEOMETRY_CHANGED')

bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(filepath=str(out / 'model.fbx'), use_selection=True, object_types={'MESH'},
                         apply_unit_scale=True, axis_forward='-Z', axis_up='Y', path_mode='STRIP',
                         embed_textures=False, bake_anim=False)

# preview render (workbench, texture colours)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'TEXTURE'
scene.render.resolution_x, scene.render.resolution_y = 900, 600
scene.render.film_transparent = False
cam_data = bpy.data.cameras.new('cam')
cam_data.type = 'ORTHO'
lo, hi = np.array(bbox_before[0]), np.array(bbox_before[1])
size = float((hi - lo).max())
cam_data.ortho_scale = size * 1.4
cam = bpy.data.objects.new('cam', cam_data)
scene.collection.objects.link(cam)
center = (lo + hi) / 2
import mathutils
direction = mathutils.Vector((0.0, -1.0, 1.2)).normalized()
cam.location = mathutils.Vector(center.tolist()) + direction * size * 3
cam.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam
scene.render.filepath = str(out / 'preview.png')
bpy.ops.render.render(write_still=True)

counts = collections.Counter(int(l) for l in label)
record = {
    'status': 'PASS_REPAINTED_GEOMETRY_UNCHANGED',
    'source_glb': str(src), 'source_glb_sha256': sha(src),
    'source_basecolor': str(base_png), 'source_basecolor_sha256': sha(base_png),
    'palette_sha256': pal['palette_sha256'], 'allowed_rows': rows, 'value_alignment': value_alignment,
    'triangles': tris_before, 'faces': n, 'bbox_blender': bbox_before,
    'smoothing': {'passes': 3, 'rule': 'switch to neighbour mode if it beats own label by >=1', 'faces_changed': changed_total},
    'cells_used': [{'index': cells[k]['index'], 'hex': cells[k]['hex'], 'faces': v} for k, v in counts.most_common()],
    'uv': 'single UV map PaletteUV, every loop at its cell centre; Tripo UVs and textures removed',
    'export': {'file': 'model.fbx', 'sha256': sha(out / 'model.fbx'), 'settings': 'apply_unit_scale, axis_forward=-Z, axis_up=Y, same as 038 delivery'},
    'blender': bpy.app.version_string,
}
(out / 'repaint.json').write_text(json.dumps(record, indent=2))
print('REPAINT_RESULT ' + json.dumps({'status': record['status'], 'triangles': tris_before, 'faces_changed': changed_total,
                                      'cells': [(c['hex'], c['faces']) for c in record['cells_used']]}))

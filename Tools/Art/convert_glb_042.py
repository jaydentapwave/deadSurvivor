"""042: convert a Tripo GLB to FBX + base color PNG for Unity (Blender headless).

Usage: Blender -b --factory-startup -P Tools/Art/convert_glb_042.py -- <in.glb> <out_dir>
Writes <out_dir>/model.fbx, <out_dir>/basecolor.png (if the GLB has one), <out_dir>/convert.json.
No scaling or re-topology: geometry is exported as imported. Unity normalizes size on a wrapper root.
"""
import json
import sys
from pathlib import Path

import bpy

argv = sys.argv[sys.argv.index('--') + 1:]
src, out = Path(argv[0]), Path(argv[1])
out.mkdir(parents=True, exist_ok=True)
for target in (out / 'model.fbx', out / 'basecolor.png', out / 'convert.json'):
    if target.exists():
        raise SystemExit(f'OUTPUT_EXISTS:{target}')

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(src))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes:
    raise SystemExit('NO_MESH')

images = []
for o in meshes:
    for slot in o.material_slots:
        if not slot.material or not slot.material.use_nodes:
            continue
        for node in slot.material.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image and node.image not in images:
                images.append(node.image)
base = None
for img in images:
    links = [l for o in meshes for s in o.material_slots if s.material and s.material.use_nodes
             for l in s.material.node_tree.links if l.from_node.type == 'TEX_IMAGE' and l.from_node.image == img]
    if any(l.to_socket.name == 'Base Color' for l in links):
        base = img
        break
if base is not None:
    base.filepath_raw = str(out / 'basecolor.png')
    base.file_format = 'PNG'
    base.save()

tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out / 'model.fbx'), use_selection=True, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, path_mode='COPY', embed_textures=False,
                         add_leaf_bones=False, bake_anim=False)
info = {
    'source': str(src), 'blender': bpy.app.version_string, 'meshes': len(meshes), 'triangles': tris,
    'images': [i.name for i in images], 'basecolor': base.name if base else None,
    'size': [i.size[:] for i in images][:4],
}
(out / 'convert.json').write_text(json.dumps(info, indent=1))
print('CONVERT_OK', json.dumps(info))

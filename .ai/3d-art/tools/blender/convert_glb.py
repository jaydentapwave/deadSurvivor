"""Tripo GLB -> FBX + base color / normal PNG for the 3dkit pipeline (Blender headless).

Adapted from an earlier project's Tools/Art/convert_glb_042.py (2026-11-03, the kit's development project S2): adds the normal map, because the
midpoly profile asks albedo + normal for hero / prop_large / map. 3dkit has no converter yet (moved into the kit 2026-11-04).
Usage: Blender -b --factory-startup -P .ai/3d-art/tools/blender/convert_glb.py -- <in.glb> <out_dir>
Writes <out_dir>/model.fbx, basecolor.png, normal.png (when present), convert.json. No scaling or re-topology.
"""
import json
import sys
from pathlib import Path

import bpy

argv = sys.argv[sys.argv.index('--') + 1:]
src, out = Path(argv[0]), Path(argv[1])
out.mkdir(parents=True, exist_ok=True)
for target in (out / 'model.fbx', out / 'basecolor.png', out / 'normal.png', out / 'convert.json'):
    if target.exists():
        raise SystemExit(f'OUTPUT_EXISTS:{target}')

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(src))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes:
    raise SystemExit('NO_MESH')


def feeding(img):
    """names of the sockets this image feeds, following through a Normal Map node"""
    names = []
    for o in meshes:
        for s in o.material_slots:
            if not (s.material and s.material.node_tree):
                continue
            for l in s.material.node_tree.links:
                if l.from_node.type == 'TEX_IMAGE' and l.from_node.image == img:
                    names.append(l.to_socket.name)
                    if l.to_node.type == 'NORMAL_MAP':
                        names.append('Normal')
    return names


images = []
for o in meshes:
    for slot in o.material_slots:
        if slot.material and slot.material.node_tree:
            for node in slot.material.node_tree.nodes:
                if node.type == 'TEX_IMAGE' and node.image and node.image not in images:
                    images.append(node.image)

saved = {}
for img in images:
    f = feeding(img)
    role = 'basecolor' if 'Base Color' in f else 'normal' if 'Normal' in f else None
    if role and role not in saved:
        img.filepath_raw = str(out / f'{role}.png')
        img.file_format = 'PNG'
        img.save()
        saved[role] = dict(image=img.name, size=list(img.size), feeds=f)

tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out / 'model.fbx'), use_selection=True, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, path_mode='COPY', embed_textures=False,
                         add_leaf_bones=False, bake_anim=False)
info = {'source': str(src), 'blender': bpy.app.version_string, 'meshes': len(meshes), 'triangles': tris,
        'images': [dict(name=i.name, size=list(i.size), feeds=feeding(i)) for i in images], 'saved': saved}
(out / 'convert.json').write_text(json.dumps(info, indent=1))
print('CONVERT_OK', json.dumps(info))

"""043: front/back preview of a repainted Tripo FBX with the palette (Blender headless).

Usage: Blender -b --factory-startup -P Tools/Art/preview_043.py -- <model.fbx> <palette.png> <out_dir>
Writes preview-front.png, preview-back.png and preview.json (sha256 of inputs). Read-only on inputs.
"""
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import mathutils

argv = sys.argv[sys.argv.index('--') + 1:]
fbx, palette, out = Path(argv[0]), Path(argv[1]), Path(argv[2])
out.mkdir(parents=True, exist_ok=True)
for name in ('preview-front.png', 'preview-back.png', 'preview.json'):
    if (out / name).exists():
        raise SystemExit(f'OUTPUT_EXISTS:{out / name}')

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(fbx))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if len(meshes) != 1:
    raise SystemExit(f'EXPECTED_ONE_MESH:{len(meshes)}')
obj = meshes[0]
mat = bpy.data.materials.new('palette')
mat.use_nodes = True
tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = bpy.data.images.load(str(palette))
tex.interpolation = 'Closest'
mat.node_tree.links.new(tex.outputs['Color'], mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
obj.data.materials.clear()
obj.data.materials.append(mat)

corners = [obj.matrix_world @ mathutils.Vector(c) for c in obj.bound_box]
lo = mathutils.Vector([min(c[i] for c in corners) for i in range(3)])
hi = mathutils.Vector([max(c[i] for c in corners) for i in range(3)])
center, size = (lo + hi) / 2, max(hi - lo)

scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'TEXTURE'
scene.render.resolution_x, scene.render.resolution_y = 900, 600
world = bpy.data.worlds.new('w')
scene.world = world
world.color = (0.82, 0.84, 0.86)
cam_data = bpy.data.cameras.new('cam')
cam_data.type = 'ORTHO'
cam_data.ortho_scale = size * 1.5
cam = bpy.data.objects.new('cam', cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
record = {'fbx': str(fbx), 'fbx_sha256': hashlib.sha256(fbx.read_bytes()).hexdigest(),
          'palette_sha256': hashlib.sha256(palette.read_bytes()).hexdigest(),
          'blender': bpy.app.version_string, 'views': {}}
# Blender: -Y is the model front after the 038 normalisation (Unity +Z forward); back = +Y.
for name, direction in (('front', mathutils.Vector((0.0, -1.0, 0.8))), ('back', mathutils.Vector((0.0, 1.0, 0.8)))):
    d = direction.normalized()
    cam.location = center + d * size * 3
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str(out / f'preview-{name}.png')
    bpy.ops.render.render(write_still=True)
    record['views'][name] = {'file': f'preview-{name}.png', 'direction': list(direction),
                             'sha256': hashlib.sha256((out / f'preview-{name}.png').read_bytes()).hexdigest()}
(out / 'preview.json').write_text(json.dumps(record, indent=2))
print('PREVIEW_RESULT ' + json.dumps({k: v['sha256'][:12] for k, v in record['views'].items()}))

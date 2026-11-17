"""UV-space geometry of a model (Blender headless): every triangle's UVs, Unity-space corner positions and face normal,
for texture work that needs to know where a texel sits on the model (prep color_fit split "height").
Usage: Blender -b --factory-startup -P .ai/3d-art/tools/blender/uv_geometry.py -- <in.fbx> <out.npz>
Writes <out.npz> with uv (T,3,2), pos (T,3,3) and normal (T,3) in Unity space (x, y up, z); prints UVGEO_OK <json>.
Rasterising to a texel map is kitlib/texture.py uv_geometry_maps (selftested without Blender).
"""
import json
import sys
from pathlib import Path

import bpy
import numpy as np

argv = sys.argv[sys.argv.index('--') + 1:]
src, out = Path(argv[0]), Path(argv[1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(src))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes:
    raise SystemExit('NO_MESH')
uvs, pos, nrm = [], [], []
for o in meshes:
    me = o.data
    if not me.uv_layers:
        raise SystemExit('NO_UV:' + o.name)
    me.calc_loop_triangles()
    uv = me.uv_layers.active.data
    mw = np.array(o.matrix_world)
    rot = mw[:3, :3]
    for t in me.loop_triangles:
        uvs.append([tuple(uv[li].uv) for li in t.loops])
        p = [mw @ np.r_[np.array(me.vertices[vi].co), 1.0] for vi in t.vertices]
        pos.append([(q[0], q[2], -q[1]) for q in p])              # Blender (x, y, z) -> Unity (x, z, -y)
        n = rot @ np.array(t.normal)
        n = n / max(np.linalg.norm(n), 1e-9)
        nrm.append((n[0], n[2], -n[1]))
uvs, pos, nrm = np.array(uvs, np.float32), np.array(pos, np.float32), np.array(nrm, np.float32)
out.parent.mkdir(parents=True, exist_ok=True)
np.savez_compressed(out, uv=uvs, pos=pos, normal=nrm)
print('UVGEO_OK', json.dumps(dict(triangles=len(uvs), y=[round(float(pos[..., 1].min()), 4), round(float(pos[..., 1].max()), 4)],
                                  uv=[round(float(uvs.min()), 4), round(float(uvs.max()), 4)], out=str(out))))

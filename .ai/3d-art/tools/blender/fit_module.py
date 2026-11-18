"""Terrain module fit (Blender headless): exact bounds + cell-boundary weld (art standard 3.2 "이음새 규칙").
Usage: Blender -b --factory-startup -P .ai/3d-art/tools/blender/fit_module.py -- <in.fbx> <out_dir> <height> <bury> [--corner] [--tol 0.02] [--strict] [--width 1] [--depth 1] [--pre-yaw 0]
Writes <out_dir>/model.fbx and fit.json (bounds before/after, vertices welded). Unity space: origin = cell centre, top face y = 0.
Logic is kitlib/modfit.py (selftested without Blender). Textures are not copied: the catalog keeps pointing at the source images.
"""
import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from kitlib import modfit  # noqa: E402

argv = sys.argv[sys.argv.index('--') + 1:]
src, out = Path(argv[0]), Path(argv[1])
height, bury = float(argv[2]), float(argv[3])
corner = '--corner' in argv
tol = float(argv[argv.index('--tol') + 1]) if '--tol' in argv else modfit.WELD_TOL
width = float(argv[argv.index('--width') + 1]) if '--width' in argv else modfit.CELL   # stairs: 1 x 2 cells
depth = float(argv[argv.index('--depth') + 1]) if '--depth' in argv else modfit.CELL
pre_yaw = float(argv[argv.index('--pre-yaw') + 1]) if '--pre-yaw' in argv else 0.0   # degrees about Y before the fit (modfit.rotate_yaw)
out.mkdir(parents=True, exist_ok=True)
for t in (out / 'model.fbx', out / 'fit.json'):
    if t.exists():
        t.unlink()


def import_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def unity_points(meshes):
    """world vertices, Blender (x, y, z) -> Unity (x, z, -y)"""
    parts = []
    for o in meshes:
        n = len(o.data.vertices)
        a = np.empty(n * 3, np.float64)
        o.data.vertices.foreach_get('co', a)
        w = np.array(o.matrix_world) @ np.c_[a.reshape(-1, 3), np.ones(n)].T
        parts.append(np.c_[w[0], w[2], -w[1]])
    return parts


meshes = import_fbx(src)
if not meshes:
    raise SystemExit('NO_MESH')
parts = unity_points(meshes)
allp = np.concatenate(parts)
fitted, info = modfit.fit_module(allp, height, bury, corner=corner, tol=tol, width=width, depth=depth, pre_yaw=pre_yaw)
i = 0
for o, part in zip(meshes, parts):
    q = fitted[i:i + len(part)]
    i += len(part)
    local = np.c_[q[:, 0], -q[:, 2], q[:, 1]]          # Unity -> Blender world; the object transform is cleared below
    o.matrix_world = Matrix.Identity(4)
    o.data.vertices.foreach_set('co', local.astype(np.float32).ravel())
    o.data.update()
    if pre_yaw:   # imported custom normals would still point the old way
        bpy.context.view_layer.objects.active = o
        try:
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
        except RuntimeError as e:
            print('WARN custom normals not cleared:', e)
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out / 'model.fbx'), use_selection=True, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, path_mode='COPY', embed_textures=False,
                         add_leaf_bones=False, bake_anim=False)
# verify by re-importing the written file
back = np.concatenate(unity_points(import_fbx(out / 'model.fbx')))
L = lambda v: [round(float(x), 5) for x in v]
rec = dict(source=str(src), module=dict(height=height, bury=bury, corner=corner, tol=tol, width=width, depth=depth, pre_yaw=pre_yaw), vertices=len(allp),
           box=dict(lo=L(info['box_lo']), hi=L(info['box_hi'])),
           bounds_before=dict(min=L(info['before']['min']), max=L(info['before']['max'])),
           bounds_after=dict(min=L(back.min(0)), max=L(back.max(0))),
           scale=L(info['scale']), scale_ratio=L(info['scale_ratio']), welded_axes=['xyz'[a] for a in info['welded_axes']],
           warnings=info['warnings'], welded_vertices=info['moved_vertices'], planes=info['planes'], blender=bpy.app.version_string)
(out / 'fit.json').write_text(json.dumps(rec, indent=1))
for w in info['warnings']:
    print('WARN', w)
if info['warnings'] and '--strict' in argv:
    raise SystemExit('FIT_STRICT_STOP: ' + ' '.join(info['warnings']))
print('FIT_OK', json.dumps(rec))

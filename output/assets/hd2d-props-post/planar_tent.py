"""A자 천막의 양옆 천 면을 평면으로 펴서 큰 쐐기형 꺾임을 없앤다(Blender, 크레딧 0).
인자: <in.glb> <out_dir> [--margin-y 0.18] [--margin-z 0.12]
post_prop.py 축(용마루 = Y, 옆면 = ±X, 위 Z, 바닥 z=0). 끝 장대·토글(|y| 끝 margin 안), 밑단(z<margin), 용마루 근처는 건드리지 않는다.
옆면마다 천 정점으로 x = a + b*z 직선을 맞추고(최소제곱) 그 정점의 x를 직선 값으로 옮긴다. 결과 model.fbx/glb, planar.json."""
import bpy, json, sys
from pathlib import Path
a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], Path(a[1]); out.mkdir(parents=True, exist_ok=True)
my = float(a[a.index('--margin-y') + 1]) if '--margin-y' in a else 0.18
mz = float(a[a.index('--margin-z') + 1]) if '--margin-z' in a else 0.12
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
o = [x for x in bpy.context.scene.objects if x.type == 'MESH'][0]
bpy.context.view_layer.objects.active = o; o.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = o.data
ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
y0, y1, H = min(ys), max(ys), max(zs)
rep = {}
for side in (1, -1):
    cl = [v for v in me.vertices if side * v.co.x > 0.05 and y0 + my < v.co.y < y1 - my and mz < v.co.z < H - mz]
    n = len(cl)
    sz = sum(v.co.z for v in cl); sx = sum(side * v.co.x for v in cl)
    szz = sum(v.co.z ** 2 for v in cl); szx = sum(v.co.z * side * v.co.x for v in cl)
    b = (n * szx - sz * sx) / (n * szz - sz * sz); c = (sx - b * sz) / n
    moved = [abs(side * v.co.x - (c + b * v.co.z)) for v in cl]
    for v in cl:
        v.co.x = side * (c + b * v.co.z)
    rep['pos' if side > 0 else 'neg'] = {'verts': n, 'line_x_at_z0': round(c, 3), 'slope': round(b, 3), 'max_move_m': round(max(moved), 3), 'mean_move_m': round(sum(moved) / n, 3)}
me.update()
for p in me.polygons: p.use_smooth = False
tris = sum(len(p.vertices) - 2 for p in me.polygons)
vs = [v.co for v in me.vertices]
size = [round(max(getattr(v, k) for v in vs) - min(getattr(v, k) for v in vs), 3) for k in 'xyz']
rep.update({'src': src, 'tris': tris, 'size_xyz_m': size, 'min_z': round(min(v.z for v in vs), 4)})
bpy.ops.export_scene.fbx(filepath=str(out / 'model.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
bpy.ops.export_scene.gltf(filepath=str(out / 'model.glb'), use_selection=True, export_format='GLB', export_yup=True,
                          export_materials='NONE', export_texcoords=False, export_normals=True)
(out / 'planar.json').write_text(json.dumps(rep, indent=1))
print('PLANAR_OK', json.dumps(rep))

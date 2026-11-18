"""좌우 대칭 소품의 나쁜 쪽 반을 버리고 좋은 쪽을 거울로 복사(Blender, 크레딧 0).
인자: <post model.glb> <out_dir> keep=neg|pos   (X=0 평면 기준, post_prop.py 축: 긴 축 Y, 앞 -Y)
결과: model.fbx, model.glb, mirror.json(삼각형, 크기). 피벗은 다시 바닥 가운데 z=0."""
import bpy, bmesh, json, sys
from pathlib import Path
from mathutils import Vector, Matrix
a = sys.argv[sys.argv.index('--') + 1:]
src, out, keep = a[0], Path(a[1]), a[2].split('=')[1]
out.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
o = [x for x in bpy.context.scene.objects if x.type == 'MESH'][0]
bpy.context.view_layer.objects.active = o; o.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bm = bmesh.new(); bm.from_mesh(o.data)
geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, 0), plane_no=(1, 0, 0),
                       clear_inner=(keep == 'pos'), clear_outer=(keep == 'neg'))
bm.to_mesh(o.data); bm.free()
m = o.modifiers.new('mir', 'MIRROR'); m.use_axis = (True, False, False); m.use_clip = True; m.use_mirror_merge = True; m.merge_threshold = 0.002
bpy.ops.object.modifier_apply(modifier='mir')
m = o.modifiers.new('tri', 'TRIANGULATE'); bpy.ops.object.modifier_apply(modifier='tri')
me = o.data
vs = [v.co for v in me.vertices]
mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
me.transform(Matrix.Translation(-Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z)))); me.update()
for p in me.polygons: p.use_smooth = False
vs = [v.co for v in me.vertices]
size = [round(max(getattr(v, k) for v in vs) - min(getattr(v, k) for v in vs), 3) for k in 'xyz']
tris = sum(len(p.vertices) - 2 for p in me.polygons)
bpy.ops.export_scene.fbx(filepath=str(out / 'model.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
bpy.ops.export_scene.gltf(filepath=str(out / 'model.glb'), use_selection=True, export_format='GLB', export_yup=True,
                          export_materials='NONE', export_texcoords=False, export_normals=True)
(out / 'mirror.json').write_text(json.dumps({'src': src, 'keep': keep, 'tris': tris, 'size_xyz_m': size}, indent=1))
print('MIRROR_OK', keep, tris, size)

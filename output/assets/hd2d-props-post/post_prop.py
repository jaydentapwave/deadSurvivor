"""HD-2D 3D 소품 후처리(Blender, 크레딧 0): Tripo GLB -> 질감 버림 -> 감면 -> 크기·바닥 피벗 맞춤 -> FBX/GLB + 미리보기.

실행(프로젝트 루트):
  Blender -b --factory-startup --python-exit-code 1 -P output/assets/hd2d-props-post/post_prop.py -- \
      <in.glb> <out_dir> --tris 600 --length 4.0 --width 1.8 [--height 0] [--yaw 0] [--uniform]
- 축: 결과는 Blender Z 위, 긴 축(length)은 Blender Y, 앞(+Unity Z)은 Blender -Y(키트 FBX 설정 axis_forward=-Z, axis_up=Y).
- --yaw: 감면 전 Z축 회전(도). 앞 방향을 맞출 때 쓴다.
- 크기: length·width는 축마다 맞춘다. --height 0이면 높이는 length·width 배율의 평균으로(모양 비율 유지), 주면 그 값으로.
- --uniform: 세 축을 --height / 높이 하나로(모양 비율 그대로, length·width는 긴 축 판별에만 쓴다).
- 피벗: 바닥 사각형 가운데, 가장 낮은 점 z=0.
- 결과: model.fbx, model.glb, post.json(삼각형 전후, 바운드 전후, 배율), preview_front/34/top.png
"""
import bpy, bmesh, json, math, sys
from pathlib import Path
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:]
src, out = Path(argv[0]), Path(argv[1])
opt = {'--tris': 600, '--length': 1.0, '--width': 1.0, '--height': 0.0, '--yaw': 0.0}
for k in list(opt):
    if k in argv:
        opt[k] = float(argv[argv.index(k) + 1])
out.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(src))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes:
    raise SystemExit('NO_MESH')
# 부모(빈 오브젝트) 변환을 굽고 하나로 합친다
for o in meshes:
    mw = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = mw
for o in list(bpy.context.scene.objects):
    if o.type != 'MESH':
        bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.name = src.parent.parent.name.replace('-', '_') if src.parent.name == 'tripo' else 'prop'
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = obj.data
# 질감 버림: 재질·UV 밖 속성 제거(UV는 남겨도 무해하나 질감을 다시 칠하므로 지운다)
me.materials.clear()
while me.uv_layers:
    me.uv_layers.remove(me.uv_layers[0])
for a in [a.name for a in me.color_attributes]:
    me.color_attributes.remove(me.color_attributes[a])


def tris(m):
    return sum(len(p.vertices) - 2 for p in m.polygons)


def bounds(o):
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return mn, mx


tri_before = tris(me)
mn, mx = bounds(obj)
bounds_before = [list(mn), list(mx)]
if opt['--yaw']:
    obj.data.transform(Matrix.Rotation(math.radians(opt['--yaw']), 4, 'Z'))
    me.update()

# 감면: 겹친 정점 합치기 -> 평면 녹이기(상자 모양을 지킨다) -> 남으면 접기
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0005)
bpy.ops.object.mode_set(mode='OBJECT')
target = int(opt['--tris'])
steps = []
if tris(me) > target:
    for ang in (2.0, 4.0, 6.0):
        m = obj.modifiers.new('planar', 'DECIMATE')
        m.decimate_type = 'DISSOLVE'
        m.angle_limit = math.radians(ang)
        bpy.ops.object.modifier_apply(modifier=m.name)
        m = obj.modifiers.new('tri', 'TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=m.name)
        steps.append(('dissolve', ang, tris(me)))
        if tris(me) <= target * 1.6:
            break
    tries = 0
    while tris(me) > target and tries < 6:
        m = obj.modifiers.new('collapse', 'DECIMATE')
        m.decimate_type = 'COLLAPSE'
        ratio = max(0.02, target / tris(me) * 0.98)
        m.ratio = ratio
        m.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=m.name)
        steps.append(('collapse', round(ratio, 4), tris(me)))
        tries += 1
m = obj.modifiers.new('tri', 'TRIANGULATE')
bpy.ops.object.modifier_apply(modifier=m.name)
tri_after = tris(me)

# 크기: 긴 수평 축을 Y로(필요하면 90도 돌림), 축마다 배율
mn, mx = bounds(obj)
d = mx - mn
rotated = False
if d.x > d.y and opt['--length'] >= opt['--width']:
    me.transform(Matrix.Rotation(math.radians(90), 4, 'Z'))
    me.update()
    rotated = True
    mn, mx = bounds(obj)
    d = mx - mn
sy = opt['--length'] / d.y
sx = opt['--width'] / d.x
sz = opt['--height'] / d.z if opt['--height'] > 0 else (sx + sy) / 2
if '--uniform' in argv:
    sx = sy = sz = opt['--height'] / d.z
me.transform(Matrix.Diagonal((sx, sy, sz, 1.0)))
me.update()
mn, mx = bounds(obj)
c = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
me.transform(Matrix.Translation(-c))
me.update()
for p in me.polygons:
    p.use_smooth = False
mn, mx = bounds(obj)
med = sorted((sx, sy, sz))[1]
distortion = {k: round(v / med, 3) for k, v in (('x', sx), ('y', sy), ('z', sz))}

bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out / 'model.fbx'), use_selection=True, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
                         add_leaf_bones=False, bake_anim=False)
bpy.ops.export_scene.gltf(filepath=str(out / 'model.glb'), use_selection=True, export_format='GLB', export_yup=True,
                          export_materials='NONE', export_texcoords=False, export_normals=True)

# 미리보기: 정사영, Workbench 단색 + 외곽선
scn = bpy.context.scene
scn.render.engine = 'BLENDER_WORKBENCH'
sh = scn.display.shading
sh.light = 'STUDIO'
sh.color_type = 'SINGLE'
sh.single_color = (0.75, 0.62, 0.48)
sh.show_object_outline = True
sh.show_cavity = True
scn.render.resolution_x = scn.render.resolution_y = 800
scn.render.film_transparent = False
scn.world = bpy.data.worlds.new('w')
scn.world.color = (0.8, 0.8, 0.8)
cam_data = bpy.data.cameras.new('cam')
cam_data.type = 'ORTHO'
cam = bpy.data.objects.new('cam', cam_data)
scn.collection.objects.link(cam)
scn.camera = cam
size = max(mx - mn) * 1.25
centre = Vector((0, 0, (mx.z) / 2))
# 앞 = Blender -Y(= Unity +Z). 3/4 = 앞-오른쪽 위 30도. 위 = 바로 위(앞이 화면 아래)
views = {'front': (0.0, 0.0), '34': (35.0, 30.0), 'top': (0.0, 89.9)}
for name, (yaw, pitch) in views.items():
    yw, pt = math.radians(yaw), math.radians(pitch)
    direction = Vector((math.sin(yw) * math.cos(pt), -math.cos(yw) * math.cos(pt), math.sin(pt)))
    cam.location = centre + direction * 20
    cam.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
    cam_data.ortho_scale = size
    scn.render.filepath = str(out / f'preview_{name}.png')
    bpy.ops.render.render(write_still=True)

report = {
    'src': str(src), 'tris_before': tri_before, 'tris_after': tri_after, 'target': target, 'steps': steps,
    'bounds_before': bounds_before, 'bounds_after': [list(mn), list(mx)], 'size_after_xyz_m': [round(v, 3) for v in (mx - mn)],
    'axes': 'Blender Z up, length along Y, front = Blender -Y (= Unity +Z with axis_forward=-Z)',
    'rotated_90_to_put_length_on_y': rotated, 'yaw_deg': opt['--yaw'],
    'scale_xyz': [round(sx, 4), round(sy, 4), round(sz, 4)], 'scale_distortion_vs_median': distortion,
    'pivot': 'bottom-face centre, lowest point z=0', 'materials': 'none (generated texture discarded)',
}
(out / 'post.json').write_text(json.dumps(report, indent=1, ensure_ascii=False))
print('POST_OK', json.dumps({k: report[k] for k in ('tris_before', 'tris_after', 'size_after_xyz_m', 'scale_distortion_vs_median')}))

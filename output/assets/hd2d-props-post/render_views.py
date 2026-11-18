"""post/model.glb를 여러 각도(yaw,pitch)로 정사영 렌더. 인자: <glb> <out_prefix> yaw:pitch ..."""
import bpy, math, sys
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a[0])
o = [x for x in bpy.context.scene.objects if x.type == 'MESH'][0]
vs = [o.matrix_world @ v.co for v in o.data.vertices]
mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
s = bpy.context.scene
s.render.engine = 'BLENDER_WORKBENCH'
s.display.shading.light = 'STUDIO'; s.display.shading.color_type = 'SINGLE'; s.display.shading.single_color = (0.75, 0.62, 0.48)
s.display.shading.show_object_outline = True; s.display.shading.show_cavity = True
s.render.resolution_x = s.render.resolution_y = 800
s.world = bpy.data.worlds.new('w'); s.world.color = (0.8, 0.8, 0.8)
cd = bpy.data.cameras.new('c'); cd.type = 'ORTHO'; cd.ortho_scale = max(mx - mn) * 1.25
cam = bpy.data.objects.new('c', cd); s.collection.objects.link(cam); s.camera = cam
c = (mx + mn) / 2
for spec in a[2:]:
    yw, pt = [math.radians(float(t)) for t in spec.split(':')]
    d = Vector((math.sin(yw) * math.cos(pt), -math.cos(yw) * math.cos(pt), math.sin(pt)))
    cam.location = c + d * 20
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    s.render.filepath = f'{a[1]}_{spec.replace(":", "_").replace("-", "m")}.png'
    bpy.ops.render.render(write_still=True)

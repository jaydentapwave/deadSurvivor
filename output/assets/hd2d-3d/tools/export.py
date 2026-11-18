# Blender -b --factory-startup --python-exit-code 1 -P export.py -- <dir>  (work.blend + albedo.png → model.glb / model.fbx)
# CPU fallback is wall-only; avoids GPU initialisation on restricted hosts.
import sys
if '--cpu-wall' in sys.argv:
    from wall_cpu import export
    args = sys.argv[sys.argv.index('--cpu-wall') + 1:]
    export(*args)
    raise SystemExit(0)

import bpy, sys
d = sys.argv[sys.argv.index('--') + 1:][0]
bpy.ops.wm.open_mainfile(filepath=d + '/work.blend')
T = bpy.data.objects['prop']
img = bpy.data.images.load(d + '/albedo.png'); img.name = 'albedo'
mat = bpy.data.materials.new('dot_albedo'); mat.use_nodes = True
nt = mat.node_tree; bsdf = nt.nodes.get('Principled BSDF')
tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Closest'
nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value = 1.0
T.data.materials.clear(); T.data.materials.append(mat)   # 부위 슬롯은 알베도 하나로 합침(재질 1)
bpy.ops.object.select_all(action='DESELECT'); T.select_set(True); bpy.context.view_layer.objects.active = T
bpy.ops.export_scene.gltf(filepath=d + '/model.glb', export_format='GLB', use_selection=True)
bpy.ops.export_scene.fbx(filepath=d + '/model.fbx', use_selection=True, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, path_mode='RELATIVE', embed_textures=False,
                         add_leaf_bones=False, bake_anim=False)
me = T.data; me.calc_loop_triangles()
print('EXPORTED tris', len(me.loop_triangles), 'verts', len(me.vertices))

"""Run only in a fresh Blender process; never save over the source FBX."""
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector


def bounds(obj):
    points = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
    lo = [min(p[i] for p in points) for i in range(3)]
    hi = [max(p[i] for p in points) for i in range(3)]
    return lo, hi


def tris(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def main():
    specfile, rawdir, output = map(Path, sys.argv[sys.argv.index('--') + 1:])
    spec = json.loads(specfile.read_text()); limit = spec['limits']
    def source(role):
        return rawdir / spec['source_files'][spec[role]]['destination']
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(source('model')))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if len(meshes) != 1:
        raise RuntimeError('Minimum processor requires one mesh')
    rock = meshes[0]
    for obj in list(bpy.context.scene.objects):
        if obj != rock:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.objects.active = rock
    rock.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    before_tris = tris(rock)
    lo, hi = bounds(rock)
    original_size = [hi[i] - lo[i] for i in range(3)]
    if min(original_size) <= 0 or not all(math.isfinite(x) for x in original_size):
        raise RuntimeError('Degenerate source')
    scale = limit['horizontal_size'] / max(original_size[0], original_size[1])
    rock.scale *= scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    lo, hi = bounds(rock)
    rock.location -= Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]))
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    material = bpy.data.materials.new('RockTransferMaterial'); material.use_nodes = True
    nodes = material.node_tree.nodes; links = material.node_tree.links
    shader = nodes.get('Principled BSDF'); shader.inputs['Roughness'].default_value = 0.88
    textures = {}
    for role in ('basecolor', 'normal'):
        im = bpy.data.images.load(str(source(role)), check_existing=False)
        if role == 'normal':
            im.colorspace_settings.name = 'Non-Color'
        factor = min(1.0, limit['texture_size'] / max(im.size))
        im.scale(max(1, round(im.size[0] * factor)), max(1, round(im.size[1] * factor)))
        im.filepath_raw = str(output / (role + '.png')); im.file_format = 'PNG'; im.save()
        textures[role] = list(im.size)
        node = nodes.new('ShaderNodeTexImage'); node.image = im
        if role == 'basecolor':
            links.new(node.outputs['Color'], shader.inputs['Base Color'])
        else:
            normal = nodes.new('ShaderNodeNormalMap'); normal.inputs['Strength'].default_value = 0.7
            links.new(node.outputs['Color'], normal.inputs['Color'])
            links.new(normal.outputs['Normal'], shader.inputs['Normal'])
    rock.data.materials.clear(); rock.data.materials.append(material)
    for face in rock.data.polygons:
        face.material_index = 0

    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'; scene.cycles.samples = 12
    scene.render.resolution_x = 512; scene.render.resolution_y = 512; scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.world.color = (0.18, 0.18, 0.18)
    scene.view_settings.view_transform = 'Standard'
    camdata = bpy.data.cameras.new('ComparisonCamera'); camera = bpy.data.objects.new('ComparisonCamera', camdata)
    scene.collection.objects.link(camera); scene.camera = camera
    camera.location = (4, -4, 3.6); target = Vector((0, 0, 0.7))
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camdata.type = 'ORTHO'; camdata.ortho_scale = 4.4
    lightdata = bpy.data.lights.new('Key', 'AREA'); lightdata.energy = 500; lightdata.size = 4
    light = bpy.data.objects.new('Key', lightdata); scene.collection.objects.link(light)
    light.location = (3, -4, 6); light.rotation_euler = (target - light.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str(output / 'before.png'); bpy.ops.render.render(write_still=True)

    bpy.context.view_layer.objects.active = rock
    if before_tris > limit['triangles']:
        mod = rock.modifiers.new('BudgetReduction', 'DECIMATE')
        mod.ratio = (limit['triangles'] * 0.95) / before_tris
        bpy.ops.object.modifier_apply(modifier=mod.name)
    tri = rock.modifiers.new('Triangles', 'TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=tri.name)
    # Decimation moves extremal vertices: normalize the processed geometry again.
    lo, hi = bounds(rock)
    rock.scale *= limit['horizontal_size'] / max(hi[0]-lo[0], hi[1]-lo[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    lo, hi = bounds(rock)
    rock.location -= Vector(((lo[0]+hi[0])/2, (lo[1]+hi[1])/2, lo[2]))
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    scene.render.filepath = str(output / 'after.png'); bpy.ops.render.render(write_still=True)
    bpy.ops.object.select_all(action='DESELECT'); rock.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(output / 'model.fbx'), use_selection=True,
        object_types={'MESH'}, axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
        bake_anim=False, path_mode='STRIP')
    lo, hi = bounds(rock)
    # Report in Y-up metric coordinates for the shared predicate.
    measurement = {'triangles': tris(rock), 'meshes': 1, 'materials': len(rock.data.materials),
        'size': [hi[0]-lo[0], hi[2]-lo[2], hi[1]-lo[1]],
        'center': [(hi[0]+lo[0])/2, (hi[2]+lo[2])/2, (hi[1]+lo[1])/2],
        'bottom': lo[2], 'textures': textures}
    receipt = {'blender_version': bpy.app.version_string, 'source_triangles': before_tris,
        'source_size_z_up': original_size, 'scale_initial': scale, 'measurement': measurement,
        'camera': {'position_z_up': list(camera.location), 'target_z_up': list(target), 'ortho_scale': 4.4, 'resolution': [512,512]},
        'material_policy': 'basecolor+normal; constant roughness .88; original metal/rough maps preserved',
        'scope': 'TRANSFER_TEST; no art or performance approval'}
    (output / 'receipt.json').write_text(json.dumps(receipt, indent=2, allow_nan=False))


main()

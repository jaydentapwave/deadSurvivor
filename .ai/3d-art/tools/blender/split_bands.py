"""Split a model into horizontal bands (bottom -> top) and cap the cut faces, for props that lose pieces part by part
(e.g. a tree losing its canopy layers from the top). Kit tool: no project names, values come from the arguments.

blender -b --factory-startup -P split_bands.py -- <in.fbx> <out_dir> --cuts F1,F2,... --names Base,Middle,Top
    [--tilts D1,D2,...] [--tilt-dirs A1,A2,...] [--texture basecolor.png] [--cap-percentile 20] [--render]

--cuts       cut heights as fractions of the model height from the bottom (0..1), ascending; bands = cuts + 1
--names      one object name per band, bottom first (Unity keeps them as child renderers under the model)
--tilts      per cut, degrees the cut plane leans from horizontal (default 0); the cut height is measured on the
             model's vertical centre line, so a leaning cut still passes through that height there
--tilt-dirs  per cut, the compass direction (degrees around +Z from +X, Blender axes) the plane leans down towards
--texture    base colour image used to pick the cap colour: the cap faces all get the UV of a green model face at the
             given luminance percentile (darker = more like the inside of foliage). Without it the caps use the UV of the
             model face closest to the cut centre.
--render     also write bands.png: the bands pulled apart vertically, seen from 45 degrees (a quick look, not a game capture)

Writes <out_dir>/model.fbx (same export settings as fit_module/convert_glb) and split.json (per band counts, cap faces,
cut boundary edges left open). Prints SPLIT_OK on success.
"""
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
src, out = Path(argv[0]), Path(argv[1])
opt = lambda n, d=None: argv[argv.index(n) + 1] if n in argv else d
cuts = [float(x) for x in opt('--cuts').split(',')]
names = opt('--names').split(',')
texture = opt('--texture')
percentile = float(opt('--cap-percentile', '20'))
tilts = [float(x) for x in opt('--tilts', ','.join('0' * len(cuts))).split(',')]
tilt_dirs = [float(x) for x in opt('--tilt-dirs', ','.join('0' * len(cuts))).split(',')]
if len(tilts) != len(cuts) or len(tilt_dirs) != len(cuts) or not all(0 <= a < 60 for a in tilts):
    raise SystemExit('BAD_ARGS: --tilts/--tilt-dirs need one value per cut, tilts in [0, 60)')
if len(names) != len(cuts) + 1 or cuts != sorted(cuts) or not all(0 < c < 1 for c in cuts):
    raise SystemExit('BAD_ARGS: need ascending --cuts in (0,1) and one more --names than cuts')
out.mkdir(parents=True, exist_ok=True)
for t in (out / 'model.fbx', out / 'split.json', out / 'bands.png'):
    if t.exists():
        t.unlink()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(src))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes:
    raise SystemExit('NO_MESH')
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
whole = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in [o for o in bpy.context.scene.objects if o.type != 'MESH']:
    bpy.data.objects.remove(o, do_unlink=True)
# generated meshes split vertices along UV seams, so a cut would leave open chains instead of closed loops; weld them
# (UVs live on face corners, so they survive the weld)
_bm = bmesh.new()
_bm.from_mesh(whole.data)
_before = len(_bm.verts)
bmesh.ops.remove_doubles(_bm, verts=_bm.verts, dist=1e-5)
_welded = _before - len(_bm.verts)
_bm.to_mesh(whole.data)
_bm.free()
whole.data.update()

zs = [v.co.z for v in whole.data.vertices]
zmin, zmax = min(zs), max(zs)
heights = [zmin + c * (zmax - zmin) for c in cuts]
uv_name = whole.data.uv_layers.active.name if whole.data.uv_layers else None
if not uv_name:
    raise SystemExit('NO_UV')


def pick_cap_uv():
    bm = bmesh.new()
    bm.from_mesh(whole.data)
    uv = bm.loops.layers.uv[uv_name]
    faces = [f for f in bm.faces if f.calc_center_median().z > heights[0]] or list(bm.faces)
    centre = lambda f: sum((l[uv].uv for l in f.loops), Vector((0, 0))) / len(f.loops)
    if texture:
        img = bpy.data.images.load(texture)
        w, h = img.size
        px = img.pixels[:]
        rows = []
        for f in faces:
            u = centre(f)
            x, y = min(w - 1, max(0, int(u.x % 1 * w))), min(h - 1, max(0, int(u.y % 1 * h)))
            r, g, b = px[(y * w + x) * 4:(y * w + x) * 4 + 3]
            if g > r and g > b:
                rows.append((0.2126 * r + 0.7152 * g + 0.0722 * b, tuple(u), (r, g, b)))
        if rows:
            rows.sort()
            lum, u, rgb = rows[min(len(rows) - 1, int(len(rows) * percentile / 100))]
            bm.free()
            return Vector(u), dict(source='texture', luminance=round(lum, 3), rgb=[round(c, 3) for c in rgb], green_faces=len(rows))
    mid = Vector((0, 0, heights[-1]))
    f = min(faces, key=lambda f: (f.calc_center_median() - mid).length)
    u = centre(f)
    bm.free()
    return u, dict(source='nearest_face')


cap_uv, cap_info = pick_cap_uv()


def plane_normal(tilt, direction):
    """unit normal of a plane leaning `tilt` degrees, its low side towards `direction` (degrees around +Z from +X)"""
    t, d = math.radians(tilt), math.radians(direction)
    return Vector((math.sin(t) * math.cos(d), math.sin(t) * math.sin(d), math.cos(t)))


def cut(bm, h, keep_below, n):
    """bisect at height h, drop the other side, fill the cut loops; returns (cap faces, open cut edges)"""
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    co = Vector((0, 0, h))
    bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-5, plane_co=co, plane_no=n,
                           clear_outer=keep_below, clear_inner=not keep_below)
    on = lambda e: all(abs((v.co - co).dot(n)) < 1e-4 for v in e.verts)
    edges = [e for e in bm.edges if e.is_boundary and on(e)]
    faces = bmesh.ops.holes_fill(bm, edges=edges, sides=100000)['faces'] if edges else []
    for f in faces:
        if (f.normal.dot(n) < 0) == keep_below:   # top cap faces along the plane normal, bottom cap against it
            f.normal_flip()
    left = [e for e in bm.edges if e.is_boundary and on(e)]
    return faces, len(left)


record = dict(source=str(src), welded_vertices=_welded, cuts=cuts, tilts=tilts, tilt_dirs=tilt_dirs, heights=[round(h, 5) for h in heights], z=[round(zmin, 5), round(zmax, 5)],
              cap_uv=[round(c, 5) for c in cap_uv], cap=cap_info, bands=[])
bounds = [None] + heights + [None]
objs = []
for i, name in enumerate(names):
    o = whole.copy()
    o.data = whole.data.copy()
    o.name = o.data.name = name
    bpy.context.scene.collection.objects.link(o)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    uv = bm.loops.layers.uv[uv_name]
    caps, open_edges = [], 0
    for k, keep_below in ((i, True), (i - 1, False)):
        h = bounds[k + 1] if keep_below else bounds[i]
        if h is None:
            continue
        f, n = cut(bm, h, keep_below, plane_normal(tilts[k], tilt_dirs[k]) if 0 <= k < len(cuts) else Vector((0, 0, 1)))
        caps += f
        open_edges += n
    if caps:   # big n-gons: triangulate here (beauty) rather than leave it to the importer
        caps = [f for f in bmesh.ops.triangulate(bm, faces=caps, quad_method='BEAUTY', ngon_method='BEAUTY')['faces']]
    for f in caps:
        f.smooth = False
        for l in f.loops:
            l[uv].uv = cap_uv
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    record['bands'].append(dict(name=name, vertices=len(o.data.vertices), triangles=tris, cap_faces=len(caps), open_cut_edges=open_edges))
    objs.append(o)
bpy.data.objects.remove(whole, do_unlink=True)

bpy.ops.object.select_all(action='DESELECT')
for o in objs:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out / 'model.fbx'), use_selection=True, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, path_mode='COPY', embed_textures=False,
                         add_leaf_bones=False, bake_anim=False)

if '--render' in argv:
    gap = (zmax - zmin) * 0.25
    for i, o in enumerate(objs):
        o.location.z += i * gap
    scene = bpy.context.scene
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    scene.collection.objects.link(cam)
    span = (zmax - zmin) * 1.8 + gap * len(objs)
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = span
    mid = (zmin + zmax + gap * (len(objs) - 1)) / 2
    cam.location = (0, -span * 2 * math.cos(math.radians(45)), mid + span * 2 * math.sin(math.radians(45)))
    cam.rotation_euler = (math.radians(45), 0, 0)
    scene.camera = cam
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.color_type = 'TEXTURE'
    scene.display.shading.light = 'STUDIO'
    scene.render.resolution_x, scene.render.resolution_y = 640, 640
    scene.render.filepath = str(out / 'bands.png')
    if texture:
        img = bpy.data.images.load(texture, check_existing=True)
        for o in objs:
            for m in o.data.materials:
                if m and m.use_nodes:
                    nodes = m.node_tree.nodes
                    if not any(n.type == 'TEX_IMAGE' and n.image for n in nodes):
                        t = nodes.new('ShaderNodeTexImage')
                        t.image = img
                        nodes.active = t
    bpy.ops.render.render(write_still=True)

(out / 'split.json').write_text(json.dumps(record, ensure_ascii=False, indent=1))
print('SPLIT_OK ' + json.dumps([(b['name'], b['triangles'], b['cap_faces'], b['open_cut_edges']) for b in record['bands']]))

"""Attach a hat to the head bone (K13) and a hand-held prop to a hand bone (K14) as SEPARATE meshes (Blender headless).
The props are bone children, not skinned: a straw hat does not bend with automatic weights, a shovel does not follow finger curls.
Each part is SIZED to the character before it is attached (a generated part comes in at its own arbitrary size).

Usage: Blender -b --factory-startup -P .ai/3d-art/tools/blender/attach_props.py -- <character.fbx> <out_dir>
          [--hat hat.fbx] [--hat-bone Head] [--hat-brim-ratio 1.31] [--hat-sink-frac 0.2 | --hat-sink m] [--hat-offset x,y,z]
          [--prop prop.fbx] [--prop-bone Right_Hand] [--prop-length-ratio 0.3846] [--prop-grip 0.72] [--prop-tilt 0] [--prop-offset x,y,z]
          [--prop-wide-end down|up] [--prop-roll degrees]
          [--forward x,y,z]
Placement is in the rest pose, Unity axes (x right, y up, z forward), metres (offsets):
- hat: scaled uniformly so its brim width (the larger horizontal extent of the hat) = --hat-brim-ratio x the head width (x extent of
  the vertices weighted to the head bone). The ratio is measured on the concept image (brim width / head width in pixels; give your
  project's value; 0 = keep the hat's own size). The hat's bottom-centre (bounds, not the file origin) goes to the head's horizontal
  centre at (top of the head vertices - sink), sink = --hat-sink-frac x the head height unless --hat-sink metres is given. + --hat-offset.
- prop: turned so its long axis is vertical with the wide (blade) end down and the broad face toward the way the character faces in the file
  (--forward, Unity axes, default 0,0,1; the S5 hero faces -x: -1,0,0), scaled so its length =
  --prop-length-ratio x the character height (body + hat; default 0.5 m / 1.3 m from the S5 spec), and placed so the point
  --prop-grip (0 = blade tip, 1 = handle end) of its shaft sits at the centre of the hand-bone vertices (inside the fist). + --prop-offset.
  --prop-tilt degrees turns it around the grip, tip toward the way the character faces (a long blade would go through the floor if it hung straight down).
  --prop-wide-end up holds a hammer with its head up (grip still counts from the wide end). --prop-roll degrees turns it around its long axis, so a hammer can strike with its end face.
Part meshes are renamed Hat / Prop (Hat_1.. if several; kitlib.modfit.part_names): the Unity build gives those renderers the part's own
material from the catalog 'parts' map (S7 r3: one body material on every renderer painted the hat with the body atlas = camouflage).
Writes <out_dir>/model.fbx (animations kept) and attach.json (sizes with their basis, placements, and the prop-origin-in-bone-space
drift over sampled frames of every action: 0 means it follows the bone exactly).
"""
import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from kitlib import modfit  # noqa: E402

argv = sys.argv[sys.argv.index('--') + 1:]
src, out = argv[0], Path(argv[1])


def opt(name, default=None):
    return argv[argv.index(name) + 1] if name in argv else default


def vec(s):
    x, y, z = (float(t) for t in s.split(','))
    return Vector((x, -z, y))          # Unity -> Blender world


out.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = next(o for o in bpy.data.objects if o.type == 'MESH')
if arm.animation_data is None:
    arm.animation_data_create()
actions = list(bpy.data.actions)
# the character's frame rate: importing a part FBX (static, 24 fps) resets the scene rate, and the export would then write the
# clips' frames at 24 fps = 25% slower (S7 r3: a 30 fps Tripo walk came into Unity at 24 fps). Put back before the export.
fps = (bpy.context.scene.render.fps, bpy.context.scene.render.fps_base)
arm.data.pose_position = 'REST'
bpy.context.view_layer.update()
rec = dict(source=src, sizes={}, placements={}, drift={}, fps=round(fps[0] / fps[1], 3), blender=bpy.app.version_string)


def world_pts(meshes):
    parts = []
    for o in meshes:
        n = len(o.data.vertices)
        a = np.empty(n * 3, np.float64)
        o.data.vertices.foreach_get('co', a)
        parts.append((np.array(o.matrix_world) @ np.c_[a.reshape(-1, 3), np.ones(n)].T).T[:, :3])
    return np.concatenate(parts)


def bone_pts_or_empty(bone):
    try:
        return bone_pts(bone)
    except SystemExit:
        return np.empty((0, 3))


def bone_pts(bone):
    """world vertices of the body whose strongest weight is `bone`"""
    names = {i: g.name for i, g in enumerate(body.vertex_groups)}
    idx = [v.index for v in body.data.vertices if v.groups and names[max(v.groups, key=lambda g: g.weight).group] == bone]
    if not idx:
        raise SystemExit('NO_VERTICES_ON_BONE:' + bone)
    return np.array([(body.matrix_world @ body.data.vertices[i].co)[:] for i in idx])


def rot_to_matrix(r):
    return Matrix([[*r[0], 0], [*r[1], 0], [*r[2], 0], [0, 0, 0, 1]])


def bring(path, label):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == 'MESH']
    if not meshes:
        raise SystemExit('NO_MESH:' + path)
    for o, n in zip(meshes, modfit.part_names(label, len(meshes))):
        o.name = n
    return new, meshes


def transform_roots(new, mat):
    """apply a world-space 4x4 to the imported object group"""
    roots = [o for o in new if o.parent is None]
    for o in roots:
        o.matrix_world = mat @ o.matrix_world
    bpy.context.view_layer.update()
    return roots


def bone_attach(roots, bone_name):
    """parent the (already sized and placed) objects to the bone, keeping the world transform"""
    bone = arm.data.bones[bone_name]
    inv = (arm.matrix_world @ bone.matrix_local).inverted()
    for o in roots:
        w = o.matrix_world.copy()
        o.parent, o.parent_type, o.parent_bone = arm, 'BONE', bone_name
        o.matrix_parent_inverse = inv
        o.matrix_world = w
    return roots


def to_unity(v):
    return [round(float(x), 4) for x in (v.x, v.z, -v.y)]


tracked = []
L = lambda v: [round(float(x), 4) for x in v]
body_pts = world_pts([body])
if opt('--hat'):
    bone = opt('--hat-bone', 'Head')
    pts = bone_pts(bone)
    lo, hi = pts.min(0), pts.max(0)
    head_w, head_h = float(hi[0] - lo[0]), float(hi[2] - lo[2])
    new, meshes = bring(opt('--hat'), 'Hat')
    hp = world_pts(meshes)
    hlo, hhi = hp.min(0), hp.max(0)
    brim = float(max(hhi[0] - hlo[0], hhi[1] - hlo[1]))
    ratio = float(opt('--hat-brim-ratio', 1.31))
    s = ratio * head_w / brim if ratio > 0 else 1.0
    sink = float(opt('--hat-sink')) if opt('--hat-sink') else float(opt('--hat-sink-frac', 0.2)) * head_h
    top = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, float(hi[2])))      # head vertices: top centre (Blender z up)
    origin = top - Vector((0, 0, sink)) + vec(opt('--hat-offset', '0,0,0'))
    c0 = Vector(((hlo[0] + hhi[0]) / 2, (hlo[1] + hhi[1]) / 2, float(hlo[2])))  # hat bottom centre
    mat = Matrix.Translation(origin) @ Matrix.Scale(s, 4) @ Matrix.Translation(-c0)
    roots = bone_attach(transform_roots(new, mat), bone)
    after = world_pts(meshes)
    rec['sizes']['hat'] = dict(head_width_m=round(head_w, 4), head_height_m=round(head_h, 4), brim_before=round(brim, 4), scale=round(s, 4),
                              brim_after=round(brim * s, 4), brim_to_head_ratio=ratio, sink=round(sink, 4), hat_height_after=round(float(after[:, 2].max() - after[:, 2].min()), 4),
                              basis='concept front_hat.png: brim width 764 px / head (hair) width 584 px = 1.31 (project value; flag --hat-brim-ratio)')
    rec['placements']['hat'] = dict(bone=bone, origin_unity=to_unity(origin), head_top_unity=to_unity(top), objects=[o.name for o in roots], meshes=[o.name for o in meshes])
    tracked.append(('hat', bone, roots[0]))
if opt('--prop'):
    bone = opt('--prop-bone', 'Right_Hand')
    # the fist is mostly weighted to the finger bones (S5 hero: 1 vertex on the hand bone, 79 on a finger bone), so take the hand bone and everything under it
    grip_bones = [bone] + [b.name for b in arm.data.bones[bone].children_recursive]
    weighted = {g.name for g in body.vertex_groups}
    fist = np.vstack([bone_pts_or_empty(n) for n in grip_bones if n in weighted]).mean(0)
    allz = world_pts([o for o in bpy.data.objects if o.type == 'MESH'])
    char_h = float(allz[:, 2].max() - body_pts[:, 2].min())          # body + hat, rest pose
    new, meshes = bring(opt('--prop'), 'Prop')
    pp = world_pts(meshes)
    # long axis = principal axis of the vertices (a generated model is often tilted in its file); the wide (blade) end goes down
    c = pp.mean(0)
    v1 = np.linalg.svd(pp - c, full_matrices=False)[2][0]
    def tail_spread(sign):
        t = (pp - c) @ (sign * v1)
        res = (pp - c) - np.outer(t, sign * v1)
        lo_, hi_ = t.min(), t.max()
        return float(res[t > hi_ - 0.2 * (hi_ - lo_)].std(0).sum()), float(res[t < lo_ + 0.2 * (hi_ - lo_)].std(0).sum())
    top_sp, bottom_sp = tail_spread(1.0)
    wide_up = opt('--prop-wide-end', 'down') == 'up'
    if (top_sp > bottom_sp) != wide_up:                                # the wide end goes down, or up when asked
        v1 = -v1
    R = np.array(Vector(v1).rotation_difference(Vector((0, 0, 1))).to_matrix())
    pr = pp @ R.T
    zl, zh = pr[:, 2].min(), pr[:, 2].max()
    length = float(zh - zl)
    blade = pr[:, 2] < zl + 0.4 * length
    xy = pr[blade, :2] - pr[blade, :2].mean(0)
    major = np.linalg.svd(xy, full_matrices=False)[2][0]               # broad face forward: blade's wide direction -> x
    fwd = vec(opt('--forward', '0,0,1'))                                # the way the character faces in the file (Unity axes)
    lat = np.array([-fwd.y, fwd.x])                                    # sideways: the blade's wide direction
    ang = float(np.arctan2(lat[1], lat[0]) - np.arctan2(major[1], major[0]))
    spin = np.array([[np.cos(ang), -np.sin(ang), 0.0], [np.sin(ang), np.cos(ang), 0.0], [0.0, 0.0, 1.0]])
    R, pr = spin @ R, pr @ spin.T
    tilt_deg = float(np.degrees(np.arccos(min(1.0, abs(float(v1[2]))))))
    ratio = float(opt('--prop-length-ratio', 0.3846))
    s = ratio * char_h / length
    grip = float(opt('--prop-grip', 0.72))
    zl, zh = pr[:, 2].min(), pr[:, 2].max()
    band = (pr[:, 2] >= zl + (grip - 0.08) * (zh - zl)) & (pr[:, 2] <= zl + (grip + 0.08) * (zh - zl))
    c0 = Vector((float(pr[band, 0].mean()), float(pr[band, 1].mean()), float(zl + grip * (zh - zl))))
    origin = Vector(fist) + vec(opt('--prop-offset', '0,0,0'))
    tilt = float(opt('--prop-tilt', 0.0))
    tilt_axis = Vector((0.0, 0.0, -1.0)).cross(Vector((-fwd.x, fwd.y, fwd.z))).normalized()   # FBX import mirrors x: Unity -X = Blender +X
    roll = float(opt('--prop-roll', 0.0))                              # around the prop's long axis, through the grip
    mat = Matrix.Translation(origin) @ Matrix.Rotation(np.radians(tilt), 4, tilt_axis) @ Matrix.Scale(s, 4) @ Matrix.Rotation(np.radians(roll), 4, 'Z') @ Matrix.Translation(-c0) @ rot_to_matrix(R)
    roots = bone_attach(transform_roots(new, mat), bone)
    after = world_pts(meshes)
    rec['sizes']['prop'] = dict(length_before=round(length, 4), scale=round(s, 4), length_after=round(length * s, 4), character_height=round(char_h, 4),
                               length_to_height_ratio=ratio, grip=grip, tilt_toward_forward_deg=tilt, forward_unity=opt('--forward', '0,0,1'), tilt_in_file_deg=round(tilt_deg, 1), extent_after=L(after.max(0) - after.min(0)),
                               basis='S5 spec 2.3: about 0.5 m on a 1.3 m character = 0.3846 of body+hat height (flag --prop-length-ratio)')
    rec['placements']['prop'] = dict(bone=bone, origin_unity=to_unity(origin), fist_centre_unity=to_unity(Vector(fist)), objects=[o.name for o in roots], meshes=[o.name for o in meshes])
    tracked.append(('prop', bone, roots[0]))
if not tracked:
    raise SystemExit('NOTHING_TO_ATTACH (give --hat and/or --prop)')

# drift: prop origin expressed in its bone's space, over sampled frames of every action (rest pose rule off)
arm.data.pose_position = 'POSE'
for a in actions:
    arm.animation_data.action = a
    if hasattr(arm.animation_data, 'action_slot') and a.slots:
        arm.animation_data.action_slot = a.slots[0]
    fr = sorted({int(a.frame_range[0]), int((a.frame_range[0] + a.frame_range[1]) // 2), int(a.frame_range[1])})
    for name, bone, o in tracked:
        local = []
        for f in fr:
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
            pbn = arm.pose.bones[bone]
            local.append(np.array(((arm.matrix_world @ pbn.matrix).inverted() @ o.matrix_world.translation)[:]))
        rec['drift'][f'{name}:{a.name}'] = round(float(max(np.linalg.norm(l - local[0]) for l in local)), 6)
bpy.context.scene.frame_set(1)

bpy.context.scene.render.fps, bpy.context.scene.render.fps_base = fps
arm.animation_data.action = None
for a in actions:
    t = arm.animation_data.nla_tracks.new()
    t.name = a.name
    t.strips.new(a.name, int(a.frame_range[0]), a)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(out / 'model.fbx'), add_leaf_bones=False, bake_anim=bool(actions), bake_anim_use_nla_strips=True,
                         bake_anim_use_all_actions=False, path_mode='COPY', embed_textures=True)
(out / 'attach.json').write_text(json.dumps(rec, indent=1))
print('ATTACH_OK', json.dumps(rec))

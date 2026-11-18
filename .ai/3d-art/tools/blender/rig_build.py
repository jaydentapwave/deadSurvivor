"""3dkit: rig + in-place motions built in Blender, no credits. One engine; what differs per creature is a shape.

Usage: Blender -b --factory-startup --python-exit-code 1 -P rig_build.py -- <shape.py | shape.json> <static.fbx> <albedo.png> <out_dir> --height H [--debug]
- A .json shape goes through rig_legged.py next to this file: bones, regions, gait and key poses as data, no code. A .py shape is
  for what data cannot say (rule-based weights of an odd creature). It is run with this file's names already in its globals:
    prepare()   optional, before the vertices are read: edit the mesh. Its return value lands in rig.json as "prepare"
    define()    fill bones[name] = (head, tail, parent, connected) and W8[name] = weights over all vertices.
                Returns dict(torso=[bone...], accessory=[bone...], mesh_name=...): torso for the limb-on-torso check, accessory left out of it;
                mesh_name keeps a name Unity already refers to (optional)
    motions()   make_action(...) per clip; returns the actions
  Names ready for define(): g x y z side ay col N_V bones W8 info SIDES sstep split seg_dist mirror.
  Added for motions(): arm REST VEC qaxis reset_pose set_world_delta set_world_offset upd solve_leg rest_pole wd make_action rest_head rest_tail TAU FPS.
- Mesh: the static convert FBX put at game scale: height H m, feet z = 0, centred on XY. The front stays where the file has it
  (Blender +X), so the catalog yaw 90 still turns it to Unity +Z.
- Stops: MULTIPLE_MESHES (input has more than one mesh, --mesh NAME picks the body), IK_REACH_ERROR (a foot misses its target by more than
  --max-reach-error, default 0.02 m).
- Weights: max 3 influences, normalised. An unweighted vertex stops the build.
- Actions (30 fps, in place, loop: frame N == frame 0) are keyed as plain bone rotations, no constraints left.
- Export: model.fbx with the kit convert/fit axis-scale settings (convert_glb.py: apply_unit_scale, FBX_SCALE_ALL, -Z forward, Y up,
  bake_space_transform) + baked NLA takes. Also rig.blend, rig.json, rest.npy/frames.npz (evaluated vertices for the FBX round-trip
  check), and with --debug weight-colour renders.
"""
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

argv = sys.argv[sys.argv.index('--') + 1:]
shape_path, src, albedo, out = Path(argv[0]), argv[1], argv[2], Path(argv[3])
species = shape_path.stem



def opt(name, default=None):
    return argv[argv.index(name) + 1] if name in argv else default


HEIGHT = float(opt('--height'))
DEBUG = '--debug' in argv
FPS = 30
out.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps, scene.render.fps_base = FPS, 1
bpy.context.preferences.edit.keyframe_new_interpolation_type = 'LINEAR'
bpy.ops.import_scene.fbx(filepath=src)
scene.render.fps, scene.render.fps_base = FPS, 1     # the FBX importer sets the scene rate from the file (24): set it after
meshes = [o for o in scene.objects if o.type == 'MESH']
if len(meshes) != 1 and not opt('--mesh'):         # B60: an adopted model with a hat would quietly rig the hat
    raise SystemExit(f"MULTIPLE_MESHES:{[o.name for o in meshes]} -> next: give the body with --mesh NAME, or an input with the body only")
body = next(o for o in meshes if o.name == opt('--mesh', meshes[0].name))
for o in list(scene.objects):
    if o is not body:
        bpy.data.objects.remove(o)


def sel_only(o):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def mesh_co(o):
    n = len(o.data.vertices)
    a = np.empty(n * 3)
    o.data.vertices.foreach_get('co', a)
    return a.reshape(-1, 3)


# ---------------- mesh to game scale ----------------
sel_only(body)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
co = mesh_co(body)
src_bounds = (co.min(0).round(4).tolist(), co.max(0).round(4).tolist())
s = HEIGHT / float(co[:, 2].max() - co[:, 2].min())
c = np.array([(co[:, 0].max() + co[:, 0].min()) / 2, (co[:, 1].max() + co[:, 1].min()) / 2, co[:, 2].min()])
body.scale = (s, s, s)
body.location = Vector((-c * s).tolist())
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


# ---------------- the shape ----------------
shape = {}
config = None
if shape_path.suffix == '.json':
    config = json.loads(shape_path.read_text())
    code_path = Path(__file__).resolve().parent / 'rig_legged.py'
else:
    code_path = shape_path
shape.update(body=body, albedo=albedo, bpy=bpy, np=np, math=math, Vector=Vector, Quaternion=Quaternion, Matrix=Matrix, config=config, HEIGHT=HEIGHT, __name__='shape')
exec(compile(code_path.read_text(), str(code_path), 'exec'), shape)
prepared = shape['prepare']() if 'prepare' in shape else None
g = mesh_co(body)
N_V = len(g)
x, y, z = g[:, 0], g[:, 1], g[:, 2]
side = np.where(y >= 0, 1.0, -1.0)
ay = np.abs(y)

# per-vertex albedo (average over the vertex's loops)
img = bpy.data.images.load(albedo)
W, Hh = img.size
pix = np.empty(W * Hh * 4, np.float32)
img.pixels.foreach_get(pix)
pix = pix.reshape(Hh, W, 4)
me = body.data
uv = np.empty(len(me.loops) * 2)
me.uv_layers.active.data.foreach_get('uv', uv)
uv = uv.reshape(-1, 2)
lv = np.empty(len(me.loops), int)
me.loops.foreach_get('vertex_index', lv)
px = np.clip((uv[:, 0] % 1.0) * W, 0, W - 1).astype(int)
py = np.clip((uv[:, 1] % 1.0) * Hh, 0, Hh - 1).astype(int)
lc = pix[py, px, :3]
col = np.zeros((N_V, 3))
np.add.at(col, lv, lc)
cnt = np.bincount(lv, minlength=N_V)[:, None]
col = col / np.maximum(cnt, 1)


def sstep(t, a, b):
    """0 at a, 1 at b (either order), smooth"""
    u = np.clip((t - a) / (b - a), 0.0, 1.0)
    return u * u * (3 - 2 * u)


def split(t, cuts, width):
    """partition of unity over len(cuts)+1 consecutive segments along t, blend `width` around each cut"""
    steps = [sstep(t, ct - width / 2, ct + width / 2) for ct in cuts]
    parts = []
    for k in range(len(cuts) + 1):
        lo = steps[k - 1] if k > 0 else np.ones_like(t)
        hi = steps[k] if k < len(cuts) else np.zeros_like(t)
        parts.append(lo - hi)
    return parts


def seg_dist(p, a, b):
    a, b = np.asarray(a, float), np.asarray(b, float)
    d = b - a
    t = np.clip(((p - a) @ d) / (d @ d), 0, 1)
    return np.linalg.norm(p - (a + t[:, None] * d), axis=1)


SIDES = (('L', 1.0), ('R', -1.0))
bones = {}      # name -> (head, tail, parent, connected)
W8 = {}         # name -> weights (N_V)
info = dict(prepare=prepared, shape=species, source=src, albedo=albedo, height=HEIGHT, scale_from_source=round(s, 6), source_bounds=src_bounds,
            game_bounds=(g.min(0).round(4).tolist(), g.max(0).round(4).tolist()), front='+X (Blender) = Unity -X in file; catalog yaw 90 -> +Z')


def mirror(p, sgn):
    return (p[0], p[1] * sgn, p[2])

shape.update(g=g, x=x, y=y, z=z, side=side, ay=ay, col=col, N_V=N_V, bones=bones, W8=W8, info=info, SIDES=SIDES, sstep=sstep, split=split, seg_dist=seg_dist, mirror=mirror)
defined = shape['define']()
TORSO = defined['torso']
NOT_LIMB = {'root', *defined.get('accessory', [])}

# ---------------- weights: prune to 3, normalise, groups ----------------
names_all = list(bones)
Wm = np.stack([W8.get(b, np.zeros(N_V)) for b in names_all], 1)
Wm[Wm < 1e-3] = 0
order = np.argsort(-Wm, 1)
for i in range(N_V):
    Wm[i, order[i, 3:]] = 0
tot = Wm.sum(1, keepdims=True)
if (tot[:, 0] <= 0).any():
    raise SystemExit(f'UNWEIGHTED_VERTICES:{int((tot[:, 0] <= 0).sum())}')
Wm /= tot
dom = np.array([names_all[k] for k in Wm.argmax(1)])
info['bones'] = len(names_all)
info['bone_names'] = names_all
info['dominant_vertices'] = {b: int((dom == b).sum()) for b in names_all}
info['max_influences'] = int((Wm > 0).sum(1).max())
info['blended_vertices'] = int(((Wm > 0).sum(1) > 1).sum())
torso_set = set(TORSO)
limb_bones = [b for b in names_all if b not in torso_set and b not in NOT_LIMB]
# "torso on limb" check: vertices inside the torso core (by the same rules' body test) with any limb weight > 0.5
info['limb_weight_max_on_torso_dominant'] = round(float(max(Wm[dom == t][:, [names_all.index(b) for b in limb_bones]].max(initial=0) for t in TORSO)), 4)

# ---------------- armature ----------------
arm_data = bpy.data.armatures.new('Armature')
arm = bpy.data.objects.new('Armature', arm_data)
scene.collection.objects.link(arm)
sel_only(arm)
bpy.ops.object.mode_set(mode='EDIT')
for b, (h, t, p, conn) in bones.items():
    eb = arm_data.edit_bones.new(b)
    eb.head, eb.tail, eb.roll = Vector(h), Vector(t), 0.0
for b, (h, t, p, conn) in bones.items():
    if p:
        eb = arm_data.edit_bones[b]
        eb.parent = arm_data.edit_bones[p]
        eb.use_connect = conn and (Vector(arm_data.edit_bones[p].tail) - Vector(h)).length < 1e-6
bpy.ops.object.mode_set(mode='OBJECT')
for b in names_all:
    vg = body.vertex_groups.new(name=b)
    k = names_all.index(b)
    idx = np.nonzero(Wm[:, k])[0]
    for i in idx:
        vg.add([int(i)], float(Wm[i, k]), 'REPLACE')
body.name = body.data.name = defined.get('mesh_name') or f'{species}_mesh'
mod = body.modifiers.new('Armature', 'ARMATURE')
mod.object = arm
# mesh stays a separate top-level object (two FBX roots -> Unity makes an identity file root; the kit prefab then only
# turns/scales that root)
for pb in arm.pose.bones:
    pb.rotation_mode = 'QUATERNION'

REST = {b: arm.data.bones[b].matrix_local.to_quaternion() for b in names_all}
VEC = {b: (arm.data.bones[b].tail_local - arm.data.bones[b].head_local) for b in names_all}


def qaxis(axis, deg):
    return Quaternion(Vector(axis), math.radians(deg))


def set_world_delta(b, D):
    """pose bone b so that its armature-space rotation = D @ rest (D = world delta), given the parent's current pose"""
    pb = arm.pose.bones[b]
    par = arm.data.bones[b].parent
    if par:
        Pp = arm.pose.bones[par.name].matrix.to_quaternion()
        base = Pp @ REST[par.name].inverted() @ REST[b]
    else:
        base = REST[b]
    q = base.inverted() @ D @ REST[b]
    q.normalize()
    pb.rotation_quaternion = q


def set_world_offset(b, d):
    """translate bone b by d (armature space) from its rest head (parents assumed unrotated: root-level bones only)"""
    pb = arm.pose.bones[b]
    pb.location = REST[b].inverted() @ Vector(d)


def upd():
    bpy.context.view_layer.update()


def two_bone(hip, target, L1, L2, pole):
    hip, target, pole = Vector(hip), Vector(target), Vector(pole)
    d_vec = target - hip
    d = d_vec.length
    dmax, dmin = (L1 + L2) * 0.999, abs(L1 - L2) * 1.001 + 1e-6
    reach_err = max(0.0, d - dmax)
    dd = min(max(d, dmin), dmax)
    u = d_vec.normalized()
    a = (L1 * L1 - L2 * L2 + dd * dd) / (2 * dd)
    hgt = math.sqrt(max(L1 * L1 - a * a, 0))
    pp = (pole - u * pole.dot(u))
    pp = pp.normalized() if pp.length > 1e-9 else Vector((1, 0, 0))
    knee = hip + u * a + pp * hgt
    ankle = hip + u * dd
    return knee, ankle, reach_err


def solve_leg(up, low, target, pole, end=None, end_delta=None):
    """2-bone IK for bones up->low (connected), then optional end bone world delta"""
    upd()
    hip = arm.pose.bones[up].head.copy()
    L1, L2 = VEC[up].length, VEC[low].length
    knee, ankle, err = two_bone(hip, target, L1, L2, pole)
    set_world_delta(up, VEC[up].rotation_difference(knee - hip))
    upd()
    set_world_delta(low, VEC[low].rotation_difference(ankle - knee))
    if end:
        upd()
        set_world_delta(end, end_delta or Quaternion())
    return err


def rest_pole(up, low, fallback=(1, 0, 0)):
    h = arm.data.bones[up].head_local
    k = arm.data.bones[up].tail_local
    a = arm.data.bones[low].tail_local
    u = (a - h).normalized()
    v = (k - h) - u * (k - h).dot(u)
    return v.normalized() if v.length > 2e-3 else Vector(fallback).normalized()


def wd(b):
    """current world delta of bone b (posed armature rotation @ rest^-1)"""
    return arm.pose.bones[b].matrix.to_quaternion() @ REST[b].inverted()


def key_all(f):
    for pb in arm.pose.bones:
        pb.keyframe_insert('rotation_quaternion', frame=f, group=pb.name)
        pb.keyframe_insert('location', frame=f, group=pb.name)
        pb.keyframe_insert('scale', frame=f, group=pb.name)


def reset_pose():
    for pb in arm.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
        pb.scale = Vector((1, 1, 1))


TAU = 2 * math.pi
clips = {}
ik_err = {}


def make_action(name, n_frames, pose_fn):
    act = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = act
    worst = 0.0
    prev = {}
    for f in range(n_frames + 1):
        t = (f % n_frames) / n_frames
        reset_pose()
        upd()
        worst = max(worst, pose_fn(t))
        upd()
        for pb in arm.pose.bones:      # keep quaternion signs continuous between keys
            q = pb.rotation_quaternion
            if pb.name in prev and prev[pb.name].dot(q) < 0:
                pb.rotation_quaternion = -q
            prev[pb.name] = pb.rotation_quaternion.copy()
        key_all(f)
    clips[name] = dict(frames=[0, n_frames], fps=FPS, seconds=round(n_frames / FPS, 4))
    ik_err[name] = round(worst, 5)
    arm.animation_data.action = None
    return act


def rest_head(b):
    return arm.data.bones[b].head_local.copy()


def rest_tail(b):
    return arm.data.bones[b].tail_local.copy()


shape.update(arm=arm, reset_pose=reset_pose, REST=REST, VEC=VEC, qaxis=qaxis, set_world_delta=set_world_delta, set_world_offset=set_world_offset, upd=upd, solve_leg=solve_leg,
             rest_pole=rest_pole, wd=wd, make_action=make_action, rest_head=rest_head, rest_tail=rest_tail, TAU=TAU, FPS=FPS)
acts = shape['motions']()

info['clips'] = clips
info['ik_reach_error_max_m'] = ik_err
too_far = {k: v for k, v in ik_err.items() if v > float(opt('--max-reach-error', 0.02))}
if too_far:                                         # B59: a foot that cannot reach also reads as a good slide number
    raise SystemExit(f'IK_REACH_ERROR:{too_far} metres -> next: keep the hips within leg length of the feet in those clips; --max-reach-error M only with a reason')

# ---------------- evaluated vertices for the FBX round-trip check ----------------
def evaluated():
    dg = bpy.context.evaluated_depsgraph_get()
    e = body.evaluated_get(dg)
    m = e.to_mesh()
    a = np.empty(len(m.vertices) * 3)
    m.vertices.foreach_get('co', a)
    e.to_mesh_clear()
    return a.reshape(-1, 3) @ np.array(body.matrix_world)[:3, :3].T + np.array(body.matrix_world)[:3, 3]


reset_pose()
upd()
np.save(out / 'rest.npy', evaluated())
samples = {}
for act in acts:
    arm.animation_data.action = act
    n = int(act.frame_range[1])
    for f in sorted({0, n // 4, n // 2, (3 * n) // 4, n}):
        scene.frame_set(f)
        samples[f'{act.name}_{f}'] = evaluated()
arm.animation_data.action = None
np.savez(out / 'frames.npz', **samples)
# the FBX bone nodes get the CURRENT pose (Unity's default pose of the prefab) - put it back to the bind pose, not the last sampled frame
reset_pose()
upd()

# ---------------- debug renders: dominant-bone colours ----------------
if DEBUG:
    import colorsys
    palette = {b: np.array(colorsys.hsv_to_rgb((k * 0.381966) % 1.0, 0.85, 1.0 if k % 2 else 0.6)) for k, b in enumerate(names_all)}
    ca = me.color_attributes.new('dom', 'FLOAT_COLOR', 'POINT')
    cols = np.array([[*palette[d], 1.0] for d in dom], np.float32)
    ca.data.foreach_set('color', cols.ravel())
    me.color_attributes.active_color = ca
    sc = scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'VERTEX'
    sc.render.resolution_x = sc.render.resolution_y = 420
    sc.world = bpy.data.worlds.new('w')
    sc.world.color = (0.25, 0.25, 0.25)
    sc.display.shading.background_type = 'WORLD'
    cd = bpy.data.cameras.new('c')
    cd.type, cd.ortho_scale = 'ORTHO', max(HEIGHT, float(np.ptp(y))) * 1.25
    cam = bpy.data.objects.new('c', cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    ctr = Vector((0, 0, HEIGHT / 2))
    for nm, d in (('front', (1, 0, 0.0)), ('side', (0, 1, 0.0)), ('back', (-1, 0, 0.0)), ('top', (0.3, 0.0, 1)), ('below', (0.3, 0.2, -1))):
        v = Vector(d).normalized()
        cam.location = ctr + v * 4
        cam.rotation_euler = (-v).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = str(out / f'weights_{nm}.png')
        bpy.ops.render.render(write_still=True)
    me.color_attributes.remove(me.color_attributes['dom'])
    (out / 'weights_legend.json').write_text(json.dumps({b: [round(float(c), 3) for c in palette[b]] for b in names_all}, indent=0))

# ---------------- export ----------------
for act in acts:
    tr = arm.animation_data.nla_tracks.new()
    tr.name = act.name
    st = tr.strips.new(act.name, int(act.frame_range[0]), act)
    st.name = act.name
scene.frame_start, scene.frame_end = 0, max(int(a.frame_range[1]) for a in acts)
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=str(out / 'model.fbx'), use_selection=True, object_types={'ARMATURE', 'MESH'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, path_mode='COPY', embed_textures=False, add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                         bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
info['export'] = dict(file=str(out / 'model.fbx'), settings='convert/fit: apply_unit_scale, FBX_SCALE_ALL, -Z fwd, Y up, bake_space_transform; '
                      'bake_anim NLA takes, step 1, simplify 0')
info['triangles'] = int(sum(len(p.vertices) - 2 for p in me.polygons))
info['blender'] = bpy.app.version_string
bpy.ops.wm.save_as_mainfile(filepath=str(out / 'rig.blend'))
(out / 'rig.json').write_text(json.dumps(info, indent=1, ensure_ascii=False))
print('RIG_OK', json.dumps({k: info[k] for k in ('bones', 'clips', 'ik_reach_error_max_m', 'max_influences', 'limb_weight_max_on_torso_dominant')}))

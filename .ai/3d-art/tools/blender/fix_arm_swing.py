"""Character motion post-process for Tripo retarget clips (Blender headless). Kept from the kit's development project S2 (2026-11-03), kit version 2026-11-04.

Usage: Blender -b --factory-startup -P .ai/3d-art/tools/blender/fix_arm_swing.py -- <in.fbx> <out_dir> [--target-degrees 15 | --add-degrees D] [--actions walk,idle]
                                                                                [--max-degrees 20] [--gate 0.05] [--no-gate] [--no-rest-fingers] [--no-rest-wrists] [--step N]
- --target-degrees (default 15 = art standard K15): rotate the upper arms so the clip's mean abduction (both sides) becomes this angle,
  i.e. only by the difference from the current mean. --add-degrees D instead adds D to the clip's pose (old behaviour, no gate).
- gate: after rotating, the worst-frame hand-in-body share must be <= --gate (default 0.05). Else the target goes up 1 deg at a time
  to --max-degrees (default 20 = K15 upper bound). Still over -> fix.json gate FAIL + reason (arms too short for the torso: concept
  problem), no model is written, exit non-zero. PASS records the angle used. --no-gate skips it.
- fingers: every finger bone is held at its bind pose on every frame of every action (default ON; K7 / S2 4.3: a fist
  without modelled fingers is torn apart by the preset's finger curls). --no-rest-fingers turns it off.
- wrists: Left_Hand / Right_Hand are held at their bind rotation relative to the forearm on every frame of every action (default ON;
  S7 r2 2026-11-04: the retarget turns the auto-rig's Left_Hand 112-135 deg from bind and only the finger counter-rotation hid it,
  so resting the fingers alone sinks the mitten into the hips). --no-rest-wrists turns it off. Bones from kitlib.modfit.rest_bones.
- measures, before and after, on every action (every --step frames, default <= 72 samples per action):
  * hand lateral distance from the body midline,
  * upper-arm abduction angle from straight down (frontal plane),
  * hand-in-body: share of hand vertices (dominant bone = that side's LowerArm/Hand/finger, bind position at/beyond the wrist) inside the convex hull of the torso
    vertices (dominant bone Hips/Spine/Chest/UpperChest/Neck) = the hand sinking into the trunk. Per side, mean + worst frame.
Writes <out_dir>/model.fbx (Unity), model.glb (viewer) and fix.json. Nothing else is changed.
"""
import json
import sys
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Quaternion

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from kitlib import modfit  # noqa: E402

argv = sys.argv[sys.argv.index('--') + 1:]
src, out = argv[0], Path(argv[1])


def opt(name, default):
    return argv[argv.index(name) + 1] if name in argv else default


add_deg = float(opt('--add-degrees', 0)) if '--add-degrees' in argv else None
target_deg = float(opt('--target-degrees', 15))
max_deg = float(opt('--max-degrees', 20))
gate_limit = float(opt('--gate', 0.05))
use_gate = '--no-gate' not in argv
targets = opt('--actions', 'walk').split(',')
rest_fingers = '--no-rest-fingers' not in argv
rest_wrists = '--no-rest-wrists' not in argv
step_opt = opt('--step', None)
out.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = next(o for o in bpy.data.objects if o.type == 'MESH')
pb = arm.pose.bones
scene = bpy.context.scene
SIDES = {'Left': 1.0, 'Right': -1.0}
FINGERS = modfit.FINGERS
TORSO = ('Hips', 'Spine', 'Chest', 'UpperChest', 'Neck', 'Neck_Twist_A')


def frames(action):
    a, b = action.frame_range
    return range(int(a), int(b) + 1)


def sampled(action):
    fr = list(frames(action))
    step = int(step_opt) if step_opt else max(1, len(fr) // 72)
    return fr[::step]


def use(action):
    arm.animation_data.action = action
    if hasattr(arm.animation_data, 'action_slot') and action.slots:
        arm.animation_data.action_slot = action.slots[0]


def body_axes():
    l, r = arm.matrix_world @ pb['Left_Shoulder'].head, arm.matrix_world @ pb['Right_Shoulder'].head
    lat = (l - r).normalized()
    up = (arm.matrix_world @ pb['Head'].head - arm.matrix_world @ pb['Hips'].head).normalized()
    return lat, up, (l + r) / 2


def lateral(side):
    """hand distance from the midline, positive = outside the body on that side"""
    lat, _, mid = body_axes()
    return (arm.matrix_world @ pb[f'{side}_Hand'].head - mid).dot(lat) * SIDES[side]


def abduction(side):
    """upper-arm angle from straight down, in the frontal plane, degrees (0 = hanging, 90 = horizontal)"""
    import math
    lat, up, _ = body_axes()
    d = arm.matrix_world @ pb[f'{side}_LowerArm'].head - arm.matrix_world @ pb[f'{side}_UpperArm'].head
    return math.degrees(math.atan2(d.dot(lat) * SIDES[side], -d.dot(up)))


# vertex classes by dominant bone (bind-pose weights)
names = {i: g.name for i, g in enumerate(body.vertex_groups)}
dom = []
for v in body.data.vertices:
    dom.append(names[max(v.groups, key=lambda g: g.weight).group] if v.groups else '')
dom = np.array(dom)
torso_idx = np.array([i for i, n in enumerate(dom) if n in TORSO])

def hand_vertices(side):
    """Tripo skins the mitten hand mostly to LowerArm, so 'dominant bone = hand' misses it. Hand vertices = dominant bone is this
    side's LowerArm / Hand / finger AND (bind pose) at or beyond the wrist along the forearm direction."""
    bones = arm.data.bones
    wrist = arm.matrix_world @ bones[f'{side}_Hand'].head_local
    d = (wrist - arm.matrix_world @ bones[f'{side}_LowerArm'].head_local).normalized()
    ok = []
    for i, n in enumerate(dom):
        if n.startswith(side + '_') and (n.endswith('LowerArm') or 'Hand' in n or any(k in n for k in FINGERS)):
            if (body.matrix_world @ body.data.vertices[i].co - wrist).dot(d) >= 0:
                ok.append(i)
    return np.array(ok, int)


hand_idx = {s: hand_vertices(s) for s in SIDES}


def posed_vertices():
    dg = bpy.context.evaluated_depsgraph_get()
    eo = body.evaluated_get(dg)
    me = eo.to_mesh()
    a = np.empty(len(me.vertices) * 3, np.float64)
    me.vertices.foreach_get('co', a)
    eo.to_mesh_clear()
    return a.reshape(-1, 3)


def torso_hull(p):
    bm = bmesh.new()
    verts = [bm.verts.new(x) for x in p[torso_idx]]
    res = bmesh.ops.convex_hull(bm, input=verts)
    faces = [f for f in res['geom'] if isinstance(f, bmesh.types.BMFace)]
    tri = []
    for f in faces:
        vi = [v.index for v in f.verts]
        bm.verts.index_update()
        tri += [(vi[0], vi[k], vi[k + 1]) for k in range(1, len(vi) - 1)]
    pts = np.array([v.co[:] for v in bm.verts])
    bm.free()
    return modfit.hull_planes(pts, tri)


def stats(action):
    use(action)
    lat = {s: [] for s in SIDES}
    abd = {s: [] for s in SIDES}
    inb = {s: [] for s in SIDES}
    for f in sampled(action):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        for s in SIDES:
            lat[s].append(lateral(s))
            abd[s].append(abduction(s))
        p = posed_vertices()
        n, q = torso_hull(p)
        for s in SIDES:
            inb[s].append(float(modfit.inside_convex(p[hand_idx[s]], n, q).mean()))
    r = lambda x: round(float(x), 4)
    return {s: dict(lateral_min=r(min(lat[s])), lateral_mean=r(np.mean(lat[s])), abduction_deg_mean=round(float(np.mean(abd[s])), 2),
                    hand_in_body_mean=r(np.mean(inb[s])), hand_in_body_worst=r(max(inb[s])), samples=len(inb[s])) for s in SIDES}


if arm.animation_data is None:
    arm.animation_data_create()
actions = list(bpy.data.actions)
before = {a.name: stats(a) for a in actions}

tgt = [a for a in actions if any(t in a.name for t in targets)]
if not tgt:
    raise SystemExit('NO_TARGET_ACTION:' + ','.join(a.name for a in actions))

# original upper-arm poses of the target actions (rotations are always applied from these, so retries do not compound)
orig = {}
for a in tgt:
    use(a)
    orig[a.name] = {}
    for f in frames(a):
        scene.frame_set(f)
        l, r = pb['Left_Shoulder'].head, pb['Right_Shoulder'].head
        lat = (l - r).normalized()
        up = (pb['Head'].head - pb['Hips'].head).normalized()
        fwd = lat.cross(up).normalized()
        orig[a.name][f] = {s_: (pb[f'{s_}_UpperArm'].matrix.copy(), pb[f'{s_}_UpperArm'].head.copy(), fwd.copy()) for s_ in SIDES}
signs = {}


def rotate_arms(delta):
    """rotate the upper arms of every target action by `delta` degrees outward (negative = inward), from the original pose"""
    rad = abs(delta) * 3.14159265 / 180
    for a in tgt:
        use(a)
        for f, d in orig[a.name].items():
            scene.frame_set(f)
            for s_, (m, head, fwd) in d.items():
                b = pb[f'{s_}_UpperArm']
                best = None
                for sg in (1, -1):    # the sign that moves the hand outward
                    R = Matrix.Translation(head) @ Matrix.Rotation(sg * rad, 4, fwd) @ Matrix.Translation(-head)
                    b.matrix = R @ m
                    bpy.context.view_layer.update()
                    v = lateral(s_)
                    if best is None or v > best[0]:
                        best = (v, sg)
                sg = best[1] if delta >= 0 else -best[1]
                R = Matrix.Translation(head) @ Matrix.Rotation(sg * rad, 4, fwd) @ Matrix.Translation(-head)
                b.matrix = R @ m
                bpy.context.view_layer.update()
                b.rotation_mode = 'QUATERNION'
                b.keyframe_insert('rotation_quaternion', frame=f)
                signs.setdefault(s_, set()).add(sg)


finger_names, wrist_names = modfit.rest_bones([b.name for b in pb], rest_fingers, rest_wrists)
fb, wb = [pb[n] for n in finger_names], [pb[n] for n in wrist_names]
if fb or wb:
    for a in actions:
        use(a)
        for f in frames(a):
            scene.frame_set(f)
            for b in fb:                      # fingers: bind rotation and position
                b.rotation_mode = 'QUATERNION'
                b.rotation_quaternion = Quaternion((1, 0, 0, 0))
                b.location = (0, 0, 0)
                b.keyframe_insert('rotation_quaternion', frame=f)
                b.keyframe_insert('location', frame=f)
            for b in wb:                      # wrists: bind rotation relative to the forearm (position follows the arm)
                b.rotation_mode = 'QUATERNION'
                b.rotation_quaternion = Quaternion((1, 0, 0, 0))
                b.keyframe_insert('rotation_quaternion', frame=f)
rested = finger_names

worst_of = lambda st: max(v['hand_in_body_worst'] for t in tgt for v in st[t.name].values())
gate = dict(mode='add' if add_deg is not None else 'target', threshold=None if (add_deg is not None or not use_gate) else gate_limit)
cur_mean = {a.name: round(float(np.mean([before[a.name][s]['abduction_deg_mean'] for s in SIDES])), 2) for a in tgt}
tries = []
if add_deg is not None:
    if add_deg:
        rotate_arms(add_deg)
    used = add_deg
    gate['result'] = 'SKIPPED'
else:
    used = None
    for t in range(int(target_deg), int(max_deg) + 1) if use_gate else [target_deg]:
        # per action delta = wanted mean abduction - the clip's current mean (rotate_arms uses one delta, so take the mean over actions)
        delta = t - float(np.mean(list(cur_mean.values())))
        rotate_arms(delta)
        st = {a.name: stats(a) for a in tgt}
        tries.append(dict(target_deg=t, rotated_deg=round(delta, 2), worst_hand_in_body=worst_of(st),
                          abduction_mean={n: round(float(np.mean([st[n][s]['abduction_deg_mean'] for s in SIDES])), 2) for n in st}))
        used = t
        if not use_gate or worst_of(st) <= gate_limit:
            break
    ok = (not use_gate) or tries[-1]['worst_hand_in_body'] <= gate_limit
    gate['result'] = 'PASS' if ok else 'FAIL'
    if use_gate and not ok:
        gate['reason'] = (f'hand-in-body worst {tries[-1]["worst_hand_in_body"]} > {gate_limit} even at {int(max_deg)} deg (K15 upper bound): '
                          'arms too short for the torso -> concept problem (regenerate the character), not a motion problem')
    if not use_gate:
        gate['result'] = 'SKIPPED'
gate.update(used_degrees=used, tries=tries)
after = {a.name: stats(a) for a in actions}

info = dict(source=src, mode=gate['mode'], degrees=used, current_mean_abduction=cur_mean, gate=gate, rest_fingers=bool(rested), rested_fingers=len(rested),
            rest_wrists=bool(wrist_names), rested_wrists=wrist_names,
            targets=[a.name for a in tgt], signs={s: sorted(v) for s, v in signs.items()}, before=before, after=after,
            hand_vertices={s: int(len(hand_idx[s])) for s in SIDES}, torso_vertices=int(len(torso_idx)), blender=bpy.app.version_string)
(out / 'fix.json').write_text(json.dumps(info, indent=1, ensure_ascii=False))
if gate['result'] == 'FAIL':
    raise SystemExit('FIX_GATE_FAIL: ' + gate['reason'])
arm.animation_data.action = None
for a in actions:
    t = arm.animation_data.nla_tracks.new()
    t.name = a.name
    t.strips.new(a.name, int(a.frame_range[0]), a)
bpy.ops.export_scene.gltf(filepath=str(out / 'model.glb'), export_format='GLB', export_animations=True, export_animation_mode='NLA_TRACKS')
bpy.ops.export_scene.fbx(filepath=str(out / 'model.fbx'), add_leaf_bones=False, bake_anim=True, bake_anim_use_nla_strips=True,
                         bake_anim_use_all_actions=False, path_mode='COPY', embed_textures=True)
print('FIX_OK', json.dumps({k: info[k] for k in ('mode', 'degrees', 'rest_fingers', 'rest_wrists', 'targets')}), gate['result'])

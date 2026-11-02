"""045 stage 3 v2: fix a Tripo auto-rig whose skeleton faces the mesh's BACK (miss S3-14). Blender headless, no provider:
  Blender -b --factory-startup -P Tools/Proto/fix_rig_facing_045.py -- <asset dir>
Reads <asset>/retarget/model.fbx and <asset>/unity-input/basecolor.png. Writes <asset>/fixed/model.fbx + fix-report.json.

Detection (same test as the miss record): at each eye bone, cast a ray from the bone-front side and from the opposite side;
the skeleton is reversed when the bone-front side hits non-dark texels and the opposite side hits the dark eye texels.
Fix: mirror the MESH (not the skeleton) across the plane through the head bone perpendicular to the bone-front axis, flip
the face winding, keep vertex groups / UVs / skeleton / actions. Lateral positions do not change, so every vertex keeps
the bone it was weighted to (the skeleton's 'Left' side is the mesh's left side after the mirror). The character is
left-right symmetric, so the mirror image looks the same.
Verification after export (fresh re-import of the written FBX): eye test now passes on the bone-front side, same vertex /
triangle / bone counts and names, same actions and frame ranges, and the run clip leans toward the face (Hips->Head
vector has a positive bone-front component at 25%/50%/75% of the clip), and every bone's local rotation at 6 frames of
each action matches the source within 0.5 deg (the exporter resamples; impl review v2 r001 #5).
"""
import bpy, bmesh, hashlib, json, math, sys
from pathlib import Path
from mathutils import Vector
from mathutils.geometry import barycentric_transform
import numpy as np

asset = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
SRC = asset / 'retarget/model.fbx'; OUT = asset / 'fixed'
if OUT.exists(): raise SystemExit('OUTPUT_EXISTS')
OUT.mkdir()
rep = dict(scope='045 stage 3 v2 rig facing fix (local mirror, no provider call)', src=str(SRC), src_sha256=hashlib.sha256(SRC.read_bytes()).hexdigest())
img_path = asset / 'unity-input/basecolor.png'


def finish(status, **kw):
    rep.update(status=status, **kw); (OUT / 'fix-report.json').write_text(json.dumps(rep, indent=2) + '\n')
    print('RESULT ' + json.dumps({k: v for k, v in rep.items() if k not in ('before', 'after')}))
    if status != 'PASS': raise SystemExit(status)


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=str(path))
    arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE'); mesh = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    return arm, mesh


def rest(arm, mesh, on=True):
    for o in bpy.context.scene.objects:
        if o.animation_data: o.animation_data.action = None
    arm.data.pose_position = 'REST'
    for md in mesh.modifiers:
        if md.type == 'ARMATURE': md.show_viewport = not on
    bpy.context.view_layer.update()


def bone(arm, n): return arm.matrix_world @ arm.data.bones[n].head_local


def bone_front(arm):
    r = bone(arm, 'Right_Hand') - bone(arm, 'Left_Hand'); r.z = 0; r.normalize(); fh = Vector((0, 0, 1)).cross(r)
    r = bone(arm, 'Right_Eye') - bone(arm, 'Left_Eye'); r.z = 0; r.normalize(); fe = Vector((0, 0, 1)).cross(r)
    return (fh + fe).normalized(), math.degrees(fh.angle(fe))


def eye_test(arm, mesh):
    """per eye bone: luminance of the texel hit from the bone-front side and from the opposite side"""
    img = bpy.data.images.load(str(img_path)); W, H = img.size; px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)
    me = mesh.data; me.calc_loop_triangles(); uvl = me.uv_layers.active.data; Mi = mesh.matrix_world.inverted()
    tp = {}
    for t in me.loop_triangles: tp.setdefault(t.polygon_index, []).append(t)
    dg = bpy.context.evaluated_depsgraph_get(); f, _ = bone_front(arm)
    def lum(o, d):
        ok, loc, n, idx, obj, _ = bpy.context.scene.ray_cast(dg, o, d)
        if not ok or obj != mesh: return None
        lp = Mi @ loc; best = None
        for t in tp[idx]:
            A, B, C = (me.vertices[i].co for i in t.vertices)
            w = barycentric_transform(lp, A, B, C, Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
            if best is None or min(w) > best[0]: best = (min(w), t)
        t = best[1]; A, B, C = (me.vertices[i].co for i in t.vertices)
        uv = barycentric_transform(lp, A, B, C, *(Vector((uvl[li].uv[0], uvl[li].uv[1], 0)) for li in t.loops))
        c = px[min(H - 1, max(0, int(uv.y * H))), min(W - 1, max(0, int(uv.x * W))), :3]
        return round(float(c[0] * .299 + c[1] * .587 + c[2] * .114), 3)
    res = {}
    for n in ('Left_Eye', 'Right_Eye'):
        p = bone(arm, n); res[n] = dict(bone_front=lum(p + f * 2, -f), opposite=lum(p - f * 2, f))
    return res


def counts(arm, mesh):
    me = mesh.data
    return dict(verts=len(me.vertices), polys=len(me.polygons), tris=sum(len(p.vertices) - 2 for p in me.polygons), bones=sorted(b.name for b in arm.data.bones),
                groups=sorted(g.name for g in mesh.vertex_groups), actions={a.name: [round(x, 2) for x in a.frame_range] for a in bpy.data.actions})


def poses(arm, n=6):
    """bone local rotations (pose_bone.matrix_basis quaternions) at n frames of every action (keyed by short name)"""
    out = {}; arm.data.pose_position = 'POSE'; arm.animation_data_create()
    for a in bpy.data.actions:
        arm.animation_data.action = a; f0, f1 = a.frame_range; key = a.name.split('|')[-1]; out[key] = []
        for q in range(n):
            bpy.context.scene.frame_set(int(round(f0 + (f1 - f0) * q / (n - 1))))
            out[key].append({b.name: b.matrix_basis.to_quaternion().copy() for b in arm.pose.bones})
    arm.animation_data.action = None; arm.data.pose_position = 'REST'; return out


def lean(arm, mesh):
    """bone-front component (degrees) of Hips->Head at 25/50/75% of the run action"""
    f, _ = bone_front(arm); run = next(a for a in bpy.data.actions if a.name.lower().endswith('run'))
    arm.data.pose_position = 'POSE'; arm.animation_data_create(); arm.animation_data.action = run; out = []
    for q in (.25, .5, .75):
        fr = run.frame_range[0] + q * (run.frame_range[1] - run.frame_range[0]); bpy.context.scene.frame_set(int(round(fr)))
        hp = arm.matrix_world @ arm.pose.bones['Hips'].head; hd = arm.matrix_world @ arm.pose.bones['Head'].head; v = hd - hp
        out.append(round(math.degrees(math.atan2(v.dot(f), v.z)), 2))
    arm.animation_data.action = None; arm.data.pose_position = 'REST'; return out


# --- before ---------------------------------------------------------------------------------------------------------
arm, mesh = load(SRC); rest(arm, mesh)
f, agree = bone_front(arm); rep['bone_front_agreement_deg'] = round(agree, 2)
if agree > 20: finish('BLOCKED', reason='EYES_HANDS_DISAGREE')
before = eye_test(arm, mesh); rep['eye_test_before'] = before; rep['before'] = counts(arm, mesh)
DARK = 0.35
rev = all(v['bone_front'] is not None and v['opposite'] is not None and v['bone_front'] > DARK and v['opposite'] < DARK for v in before.values())
ok_now = all(v['bone_front'] is not None and v['bone_front'] < DARK for v in before.values())
rep['reversed_before'] = rev
if ok_now: finish('NOTHING_TO_FIX')
if not rev: finish('BLOCKED', reason='EYE_TEST_INCONCLUSIVE')
rep['run_lean_before_deg'] = lean(arm, mesh)
pose_before = poses(arm)

# --- mirror the mesh across the plane through the head bone, normal = bone-front axis --------------------------------------
c = bone(arm, 'Head'); M = mesh.matrix_world.copy(); Mi = M.inverted()
for v in mesh.data.vertices:
    w = M @ v.co; w = w - 2 * (w - c).dot(f) * f; v.co = Mi @ w
bm = bmesh.new(); bm.from_mesh(mesh.data); bmesh.ops.reverse_faces(bm, faces=bm.faces[:]); bm.to_mesh(mesh.data); bm.free()
mesh.data.update()
rest(arm, mesh); after_same_session = eye_test(arm, mesh); rep['eye_test_after_in_session'] = after_same_session

# --- export ---------------------------------------------------------------------------------------------------------
for o in bpy.context.scene.objects: o.select_set(o.type in ('ARMATURE', 'MESH'))
for md in mesh.modifiers:
    if md.type == 'ARMATURE': md.show_viewport = True
# the bake samples the evaluated pose: with pose_position REST every action is exported as a static pose (first attempt)
arm.data.pose_position = 'POSE'
for a in bpy.data.actions:                                   # 'Armature|Armature|run' -> 'run' (the exporter prefixes the armature)
    a.name = a.name.split('|')[-1]
arm.animation_data_create(); arm.animation_data.action = next(a for a in bpy.data.actions if a.name == 'idle')
out_fbx = OUT / 'model.fbx'
bpy.ops.export_scene.fbx(filepath=str(out_fbx), use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=True,
                         bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, path_mode='COPY', embed_textures=True)
rep['fixed_sha256'] = hashlib.sha256(out_fbx.read_bytes()).hexdigest()

# --- verify by re-importing the written file ------------------------------------------------------------------------
arm2, mesh2 = load(out_fbx); rest(arm2, mesh2)
after = eye_test(arm2, mesh2); rep['eye_test_after'] = after; rep['after'] = counts(arm2, mesh2)
b, a = rep['before'], rep['after']
same = dict(verts=b['verts'] == a['verts'], tris=b['tris'] == a['tris'], bones=b['bones'] == a['bones'], groups=b['groups'] == a['groups'],
            action_names=sorted(x.split('|')[-1] for x in b['actions']) == sorted(x.split('|')[-1] for x in a['actions']))
# impl review v2 r001 #5: the exporter resamples the actions, so compare the bone local rotations at 6 frames per action
pose_after = poses(arm2); worst = 0.0
for k, frames in pose_before.items():
    for fb, fa in zip(frames, pose_after.get(k, [])):
        for bn, qb in fb.items():
            if bn in fa: worst = max(worst, math.degrees(qb.rotation_difference(fa[bn]).angle))
rep['pose_max_diff_deg'] = round(worst, 4); same['poses_within_0_5deg'] = bool(pose_after) and worst <= 0.5
rep['same'] = same; rep['run_lean_after_deg'] = lean(arm2, mesh2)
rep['run_lean_note'] = 'before: bone-front axis = mesh back, so the lean was toward the back of the mesh; after: toward the face'
eyes_ok = all(v['bone_front'] is not None and v['bone_front'] < DARK and (v['opposite'] is None or v['opposite'] > DARK) for v in after.values())
lean_ok = all(x > 0 for x in rep['run_lean_after_deg'])
rep['checks'] = dict(eyes_on_bone_front=eyes_ok, structure_same=all(same.values()), run_leans_toward_face=lean_ok)
finish('PASS' if all(rep['checks'].values()) else 'FAIL')

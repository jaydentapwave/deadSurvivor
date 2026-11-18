"""Read an exported character FBX back and report what the engine will get (rule 3d-animation.md 5.3). Measures only, writes nothing.

Usage: Blender -b --factory-startup -P .ai/3d-art/tools/blender/anim_fbxcheck.py -- <model.fbx> [--expect a,b] [--fps F]
Prints one line: FBXCHECK_OK {json}
- clips: every action with its raw name, the plain name (text after the last "|": the FBX round trip prefixes "Armature|"),
  frame range, length in seconds at the file's fps, loop seam = largest distance (m, world) of any deform bone between the first and
  the last frame, and the bone it happens on
- rig: bone count, deform bones, leaf bones ("_end"), root bones
- expect: --expect names missing from / not in the plain clip names, duplicate plain names; --fps mismatch
"""
import json
import sys

import bpy


def arg(name, default=None):
    a = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    return a[a.index(name) + 1] if name in a and a.index(name) + 1 < len(a) else default


def main():
    a = sys.argv[sys.argv.index('--') + 1:]
    path = a[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    scene = bpy.context.scene
    fps = scene.render.fps / scene.render.fps_base
    arms = [o for o in scene.objects if o.type == 'ARMATURE']
    if len(arms) != 1:
        print('FBXCHECK_OK ' + json.dumps(dict(file=path, error=f'ARMATURES={len(arms)}')))
        return
    arm = arms[0]
    bones = arm.data.bones
    deform = [b.name for b in bones if b.use_deform]
    rig = dict(armature=arm.name, bones=len(bones), deform_bones=len(deform), leaf_bones=[b.name for b in bones if b.name.endswith('_end')],
               roots=[b.name for b in bones if b.parent is None], world_scale=[round(v, 5) for v in arm.matrix_world.to_scale()])
    if arm.animation_data is None:
        arm.animation_data_create()
    clips = []
    for act in bpy.data.actions:
        s, e = (int(round(x)) for x in act.frame_range)
        arm.animation_data.action = act
        if hasattr(arm.animation_data, 'action_slot') and act.slots:   # Blender 4.4+ slotted actions
            arm.animation_data.action_slot = act.slots[0]

        def pose_at(f):
            scene.frame_set(f)
            mw = arm.matrix_world
            return {n: mw @ arm.pose.bones[n].head for n in deform}

        p0, p1 = pose_at(s), pose_at(e)
        gap, gb = max(((p0[n] - p1[n]).length, n) for n in deform) if deform else (0.0, None)
        clips.append(dict(raw=act.name, name=act.name.split('|')[-1], frames=[s, e], length_s=round((e - s) / fps, 4),
                          loop_seam_m=round(gap, 5), loop_seam_bone=gb))
    names = [c['name'] for c in clips]
    out = dict(file=path, fps=round(fps, 3), rig=rig, clips=clips,
               prefixed=[c['raw'] for c in clips if c['raw'] != c['name']],
               duplicate_names=sorted({n for n in names if names.count(n) > 1}))
    exp = arg('--expect')
    if exp:
        want = [x for x in exp.split(',') if x]
        out.update(missing=[n for n in want if n not in names], unexpected=[n for n in names if n not in want])
    want_fps = arg('--fps')
    if want_fps:
        out['fps_matches'] = abs(float(want_fps) - fps) < 1e-3
    print('FBXCHECK_OK ' + json.dumps(out, ensure_ascii=False))


main()

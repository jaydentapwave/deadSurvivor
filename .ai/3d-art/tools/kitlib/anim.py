"""3dkit animation tools (rule: .ai/rules/3d-animation.md). Project values come from the catalog and 3dkit.config.json, never
from this file: bone names (catalog asset anim.bones, or the Humanoid avatar), part names (catalog parts), character height
(catalog height), game camera (config assets.game_camera).

  anim probe <id> [--clips a,b] [--step S] [--out F]      Unity: sample clips of the asset's prefab -> probe JSON
  anim frames <id> --clip C [--view V] [--speed M/S] [--seconds N] [--fps F] [--size WxH] [--tag T] [--hide P,Q] [--heading DEG]
                                                          Unity: continuous frames (skin update forced) -> PNG folder
  anim metrics <probe.json> --clip C --type move|idle|oneshot|reaction [--speed M/S] [--rest-clip C] [--band M]
                                                          numbers only (no judging): slide, ground, loop seam, pose return
  anim sheet <out.png> --row "label=glob" [--row ...] [--cols 8] [--cell 240]
                                                          rows of evenly picked frames side by side (reference rows too)
  anim fbxcheck <model.fbx> [--expect a,b] [--fps F]      Blender: clip names, fps, frame ranges, loop seam, name prefixes
  anim recheck <probe.json> <id>                          a fresh probe against the catalog asset's approved values
Views: game (follows), game_fixed, side_r, side_l, front, feet (+ "_fixed" on side/front/feet keeps the camera still).
"""
import glob
import json
import math
from pathlib import Path

PROBE_SCHEMA = '3dkit-anim-probe/1'
ROLES = ('hips', 'head', 'foot_l', 'foot_r', 'hand_l', 'hand_r')
TYPES = ('move', 'idle', 'oneshot', 'reaction')


def opt(a, name, default=None):
    if name in a:
        i = a.index(name)
        if i + 1 >= len(a):
            raise SystemExit(f'{name} NEEDS_A_VALUE')
        return a[i + 1]
    return default


def opts(a, name):
    return [a[i + 1] for i, x in enumerate(a) if x == name and i + 1 < len(a)]


# ---------------- catalog / config ----------------
def asset_of(kit, aid):
    a = next((x for x in kit.cat['assets'] if x['id'] == aid), None)
    if a is None:
        raise SystemExit('UNKNOWN_ASSET:' + aid)
    if not (a.get('anim') or {}).get('clips'):
        raise SystemExit('NO_ANIM_CLIPS_IN_CATALOG:' + aid)
    return a


def prefab_of(kit, a):
    from kitlib import pipeline as P
    return f"{kit.apath(a['folder'])}/{P.naming(kit, a['kind'])['prefab'].format(name=P.name_of(a))}.prefab"


def bone_map(a):
    """catalog anim.bones {role: bone name}; empty = the prefab must have a Humanoid avatar (Unity maps the roles)"""
    m = (a.get('anim') or {}).get('bones') or {}
    bad = [r for r in m if r not in ROLES and not r.startswith(('foot_', 'hand_'))]
    if bad:
        raise SystemExit(f"BAD_BONE_ROLE:{bad} (roles: {', '.join(ROLES)}; more legs or arms: any foot_x, hand_x)")
    return m


def game_camera(kit):
    c = kit.data['assets'].get('game_camera')
    if not c:
        raise SystemExit('NO_GAME_CAMERA (set 3dkit.config.json assets.game_camera: pitch, yaw, fov, distance, target_height)')
    need = ('pitch', 'yaw', 'fov', 'distance', 'target_height')
    miss = [k for k in need if k not in c]
    if miss:
        raise SystemExit(f'GAME_CAMERA_MISSING:{miss}')
    return c


def base_request(kit, a, mode, out_path):
    roles = bone_map(a)
    return dict(mode=mode, prefab=prefab_of(kit, a), out_path=str(out_path), roles=list(roles), bones=list(roles.values()),
                parts=list((a.get('parts') or {}).keys()), char_height=float(a.get('height') or 1.0))


# ---------------- Unity commands ----------------
def probe(kit, rest):
    from kitlib import unity as U
    a = asset_of(kit, rest[0])
    clips = (opt(rest, '--clips') or ','.join(a['anim']['clips'])).split(',')
    out = Path(opt(rest, '--out') or kit.work / a['id'] / 'anim' / 'probe.json')
    out.parent.mkdir(parents=True, exist_ok=True)
    req = base_request(kit, a, 'probe', out.resolve())
    req.update(clips=clips, step=float(opt(rest, '--step', 1 / 120)))
    msg = U.run_call(kit, 'AnimProbe', req, 'Kit3D.AnimProbe.Run')
    print(msg.strip())
    print('PROBE', out)


def frames(kit, rest):
    from kitlib import unity as U
    a = asset_of(kit, rest[0])
    clip = opt(rest, '--clip') or a['anim'].get('default') or a['anim']['clips'][0]
    view = opt(rest, '--view', 'game')
    tag = opt(rest, '--tag', f'{clip}_{view}')
    out = Path(opt(rest, '--out') or kit.work / a['id'] / 'anim' / 'frames' / tag)
    out.mkdir(parents=True, exist_ok=True)
    w, h = (int(x) for x in opt(rest, '--size', '960x540').split('x'))
    cam = game_camera(kit)
    req = base_request(kit, a, 'frames', out.resolve())
    req.update(clip=clip, view=view, speed=float(opt(rest, '--speed', 0)), seconds=float(opt(rest, '--seconds', 2)),
               fps=float(opt(rest, '--fps', 30)), width=w, height=h, heading=float(opt(rest, '--heading', 90)),
               hide_parts=[x for x in (opt(rest, '--hide') or '').split(',') if x], prefix=tag,
               cam_pitch=float(cam['pitch']), cam_yaw=float(cam['yaw']), cam_fov=float(cam['fov']),
               cam_distance=float(cam['distance']), cam_target_height=float(cam['target_height']))
    msg = U.run_call(kit, 'AnimFrames', req, 'Kit3D.AnimProbe.Run')
    print(msg.strip())
    print('FRAMES', out)


# ---------------- metrics (pure) ----------------
def clip_of(probe_data, name):
    if probe_data.get('schema') != PROBE_SCHEMA:
        raise SystemExit('BAD_PROBE_SCHEMA:' + str(probe_data.get('schema')))
    c = next((c for c in probe_data['clips'] if c['name'] == name), None)
    if c is None:
        raise SystemExit(f"CLIP_NOT_IN_PROBE:{name} (have {[c['name'] for c in probe_data['clips']]})")
    if len(c['frames']) < 3:
        raise SystemExit('TOO_FEW_FRAMES:' + name)
    return c


def dist(p, q):
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(p, q)))


def ptp(v):
    return max(v) - min(v)


def pose_gap(f0, f1, roles=None):
    """largest distance between the same role in two frames (m) and which role"""
    rs = [r for r in (roles or f0['b']) if r in f0['b'] and r in f1['b']]
    if not rs:
        return None, None
    g = max(rs, key=lambda r: dist(f0['b'][r], f1['b'][r]))
    return dist(f0['b'][g], f1['b'][g]), g


def ground(c):
    """lowest body point over the clip, the frame-to-frame spread of the per-frame lowest point, and per-part lowest"""
    low = [f['low'] for f in c['frames']]
    i_min, i_max = low.index(min(low)), low.index(max(low))
    parts = {}
    for f in c['frames']:
        for k, v in (f.get('parts') or {}).items():
            parts[k] = min(parts.get(k, v), v)
    return dict(lowest_m=round(min(low), 4), lowest_t=round(c['frames'][i_min]['t'], 4),
                highest_lowest_m=round(max(low), 4), highest_lowest_t=round(c['frames'][i_max]['t'], 4),
                parts_lowest_m={k: round(v, 4) for k, v in sorted(parts.items())})


def contact_phases(frames, role, speed, band, length):
    """foot contact phases over two loops back to back (a phase across the loop seam is one phase).
    Contact = the foot bone within `band` m of its lowest height. Slide = range of the foot's ground position in the world while
    in contact, with the root moving forward (+Z) at `speed` m/s (in-place clip). Only phases fully inside the window count."""
    fs = frames[:-1] if len(frames) > 2 and abs(frames[-1]['t'] - length) < 1e-6 else frames
    ts = [f['t'] for f in fs] + [f['t'] + length for f in fs]
    ps = [f['b'][role] for f in fs] * 2
    ys = [p[1] for p in ps]
    lo = min(ys)
    contact = [y <= lo + band for y in ys]
    if all(contact):   # planted the whole clip: one phase over one loop (window-edge rule would drop it)
        k = len(fs)
        wz = [speed * ts[m] + ps[m][2] for m in range(k)]
        return [dict(t_start=0.0, duration_s=round(length, 4), slide_m=round(ptp(wz), 4),
                     side_drift_m=round(ptp([ps[m][0] for m in range(k)]), 4), whole_clip=True)]
    out, i, n = [], 0, len(ts)
    while i < n:
        if not contact[i]:
            i += 1
            continue
        j = i
        while j + 1 < n and contact[j + 1]:
            j += 1
        if i > 0 and j < n - 1:
            wz = [speed * ts[k] + ps[k][2] for k in range(i, j + 1)]
            wx = [ps[k][0] for k in range(i, j + 1)]
            out.append(dict(t_start=round(ts[i] % length, 4), duration_s=round(ts[j] - ts[i], 4),
                            slide_m=round(ptp(wz), 4), side_drift_m=round(ptp(wx), 4)))
        i = j + 1
    # the two loops repeat the same phases; keep the ones that start in the first loop
    seen, uniq = set(), []
    for p in out:
        k = round(p['t_start'], 3)
        if k not in seen:
            seen.add(k)
            uniq.append(p)
    return uniq


def metrics(probe_data, clip, kind, speed=None, rest_clip=None, band=None):
    if kind not in TYPES:
        raise SystemExit(f'BAD_TYPE:{kind} ({", ".join(TYPES)})')
    c = clip_of(probe_data, clip)
    fr, L = c['frames'], float(c['length'])
    h = float(probe_data.get('char_height') or 1.0)
    out = dict(clip=clip, type=kind, length_s=round(L, 4), fps=c.get('fps'), loop_flag=c.get('loop'), samples=len(fr),
               char_height_m=h, ground=ground(c))
    if kind in ('move', 'idle'):
        g, r = pose_gap(fr[0], fr[-1])
        out['loop_seam_m'] = None if g is None else round(g, 4)
        out['loop_seam_role'] = r
    if 'hips' in fr[0]['b']:
        out['hips_bob_m'] = round(ptp([f['b']['hips'][1] for f in fr]), 4)
    if kind == 'move':
        if speed is None:
            raise SystemExit('MOVE_NEEDS_--speed (m/s the game moves the root)')
        bnd = float(band) if band is not None else round(0.012 * h, 4)
        out.update(speed_mps=speed, distance_per_loop_m=round(speed * L, 4), contact_band_m=bnd, feet={})
        for side in sorted(r for r in fr[0]['b'] if r.startswith('foot_')):
            ph = contact_phases(fr, side, speed, bnd, L)
            ys = [f['b'][side][1] for f in fr]
            zs = [f['b'][side][2] for f in fr]
            out['feet'][side] = dict(lift_m=round(ptp(ys), 4), stride_local_z_m=round(ptp(zs), 4), contact_phases=ph,
                                     worst_slide_m=max((p['slide_m'] for p in ph), default=None))
    if kind in ('oneshot', 'reaction'):
        if rest_clip:
            rest = clip_of(probe_data, rest_clip)['frames'][0]
            s, sr = pose_gap(rest, fr[0])
            e, er = pose_gap(rest, fr[-1])
            out.update(rest_clip=rest_clip, start_gap_m=None if s is None else round(s, 4), start_gap_role=sr,
                       end_gap_m=None if e is None else round(e, 4), end_gap_role=er)
        speeds = {}
        for hand in sorted(r for r in fr[0]['b'] if r.startswith('hand_')):
            v = [dist(fr[i]['b'][hand], fr[i - 1]['b'][hand]) / max(fr[i]['t'] - fr[i - 1]['t'], 1e-9) for i in range(1, len(fr))]
            k = v.index(max(v)) + 1
            fps = float(c.get('fps') or 30)
            speeds[hand] = dict(peak_speed_mps=round(max(v), 3), peak_t=round(fr[k]['t'], 4), peak_frame=round(fr[k]['t'] * fps, 1))
        out['hand_peak'] = speeds
        if kind == 'reaction' and 'hips' in fr[0]['b']:
            out['hips_travel_m'] = round(dist(fr[0]['b']['hips'], fr[-1]['b']['hips']), 4)
    return out


def metrics_cmd(rest):
    if not rest or rest[0].startswith('--'):
        raise SystemExit('USAGE: assets.py anim metrics <probe.json> --clip C --type move|idle|oneshot|reaction [--speed S]')
    d = json.loads(Path(rest[0]).read_text())
    clip, kind = opt(rest, '--clip'), opt(rest, '--type')
    if not clip or not kind:
        raise SystemExit('NEEDS --clip and --type')
    sp = opt(rest, '--speed')
    print(json.dumps(metrics(d, clip, kind, float(sp) if sp is not None else None, opt(rest, '--rest-clip'), opt(rest, '--band')),
                     ensure_ascii=False, indent=1))


# ---------------- recheck against approved values (pure) ----------------
def recheck(probe_data, approved):
    """compare a fresh probe with the catalog asset's 'approved' block (values the user accepted, with their words):
    {"model_scale": {"value": v, "approval": text}, "clips": {name: {"length_s": v, "lowest_m": v, "approval": text}},
     "tol_m": 0.005, "tol_scale": 0.002}. Reports every value with the difference; `changed` = outside the tolerance."""
    tol_m, tol_s = float(approved.get('tol_m', .005)), float(approved.get('tol_scale', .002))
    rows = []
    ms = approved.get('model_scale')
    if ms:
        now = float(probe_data['model_scale'][0])
        rows.append(dict(what='model_scale', approved=ms['value'], now=round(now, 5), diff=round(now - ms['value'], 5),
                         changed=abs(now - ms['value']) > tol_s, approval=ms.get('approval')))
    have = {c['name']: c for c in probe_data['clips']}
    for name, want in (approved.get('clips') or {}).items():
        c = have.get(name)
        if c is None:
            rows.append(dict(what=name, missing_in_probe=True, changed=True, approval=want.get('approval')))
            continue
        now = dict(length_s=float(c['length']), lowest_m=min(f['low'] for f in c['frames']))
        for k in ('length_s', 'lowest_m'):
            if k in want:
                d = now[k] - float(want[k])
                rows.append(dict(what=f'{name}.{k}', approved=want[k], now=round(now[k], 5), diff=round(d, 5),
                                 changed=abs(d) > tol_m, approval=want.get('approval')))
    return dict(changed=[r['what'] for r in rows if r['changed']], rows=rows)


def recheck_cmd(kit, rest):
    if len(rest) < 2:
        raise SystemExit('USAGE: assets.py anim recheck <probe.json> <id>')
    a = next((x for x in kit.cat['assets'] if x['id'] == rest[1]), None)
    if a is None:
        raise SystemExit('UNKNOWN_ASSET:' + rest[1])
    if not a.get('approved'):
        raise SystemExit('NO_APPROVED_VALUES:' + rest[1] + ' (catalog asset "approved" block)')
    print(json.dumps(recheck(json.loads(Path(rest[0]).read_text()), a['approved']), ensure_ascii=False, indent=1))


# ---------------- sheet (pure, Pillow) ----------------
def pick(n_have, n_want):
    if n_have <= n_want:
        return list(range(n_have))
    return [round(i * (n_have - 1) / (n_want - 1)) for i in range(n_want)] if n_want > 1 else [0]


def sheet(rest):
    from PIL import Image, ImageDraw
    if not rest or rest[0].startswith('--'):
        raise SystemExit('USAGE: assets.py anim sheet <out.png> --row "label=glob" [--row ...] [--cols 8] [--cell 240]')
    out, cols, cell = Path(rest[0]), int(opt(rest, '--cols', 8)), int(opt(rest, '--cell', 240))
    rows = []
    for spec in opts(rest, '--row'):
        if '=' not in spec:
            raise SystemExit('BAD_ROW (label=glob):' + spec)
        label, pat = spec.split('=', 1)
        files = sorted(glob.glob(pat))
        if not files:
            raise SystemExit('NO_FILES:' + pat)
        rows.append((label, [files[i] for i in pick(len(files), cols)]))
    if not rows:
        raise SystemExit('NO_ROWS')
    lab = 160
    W = lab + cols * cell
    img = Image.new('RGB', (W, len(rows) * cell), 'white')
    d = ImageDraw.Draw(img)
    for r, (label, files) in enumerate(rows):
        d.text((8, r * cell + cell // 2 - 6), label, fill='black')
        for c, f in enumerate(files):
            im = Image.open(f).convert('RGB')
            im.thumbnail((cell, cell))
            img.paste(im, (lab + c * cell + (cell - im.width) // 2, r * cell + (cell - im.height) // 2))
    out.parent.mkdir(parents=True, exist_ok=True)
    img.save(out)
    print(json.dumps(dict(sheet=str(out), rows=[(l, len(f)) for l, f in rows], size=img.size)))


# ---------------- fbxcheck (Blender) ----------------
def fbxcheck(kit, rest):
    from kitlib import blender as B
    if not rest or rest[0].startswith('--'):
        raise SystemExit('USAGE: assets.py anim fbxcheck <model.fbx> [--expect a,b] [--fps F]')
    line = B.run(kit, 'anim_fbxcheck', [Path(rest[0]).resolve(), *rest[1:]])
    print(json.dumps(json.loads(line.split(' ', 1)[1]), ensure_ascii=False, indent=1))


def selftest():
    """synthetic clips with hand-computed answers (algorithm-tester cases 2026-11-04)"""
    def walk(contact_idx, z_of_t, speed=1.0):
        fr = [dict(t=i * .125, b=dict(foot_l=[0., 0. if i in contact_idx else .5, z_of_t(i * .125)], hips=[0., 1., 0.]), low=0.)
              for i in range(9)]
        return dict(schema=PROBE_SCHEMA, char_height=1.0, clips=[dict(name='w', length=1.0, fps=8, loop=True, frames=fr)])
    m = metrics(walk({2, 3, 4, 5}, lambda t: -.5 * t), 'w', 'move', speed=1.0, band=.01)['feet']['foot_l']['contact_phases']
    assert m == [dict(t_start=.25, duration_s=.375, slide_m=.1875, side_drift_m=0.)], m          # known slide
    m = metrics(walk({0, 1, 6, 7, 8}, lambda t: 0.), 'w', 'move', speed=1.0, band=.01)['feet']['foot_l']['contact_phases']
    assert len(m) == 1 and m[0]['t_start'] == .75 and m[0]['duration_s'] == .375, m              # across the loop seam = one phase
    m = metrics(walk(set(range(9)), lambda t: -.5 * t), 'w', 'move', speed=1.0, band=.01)['feet']['foot_l']['contact_phases']
    assert m[0].get('whole_clip') and m[0]['slide_m'] == .4375, m                                  # planted the whole clip
    try:
        metrics(walk({2}, lambda t: 0.), 'w', 'move')
        raise AssertionError('move without speed must stop')
    except SystemExit:
        pass
    four = [dict(t=i * .125, b={f: [0., 0., -.5 * i * .125] for f in ('foot_fl', 'foot_fr', 'foot_bl', 'foot_br')}, low=0.) for i in range(9)]
    assert sorted(metrics(dict(schema=PROBE_SCHEMA, clips=[dict(name='w', frames=four, length=1.0, fps=8)], char_height=1.0), 'w', 'move', speed=1.0, band=.01)['feet']) == ['foot_bl', 'foot_br', 'foot_fl', 'foot_fr'], 'every foot role is measured'
    assert bone_map(dict(anim=dict(bones=dict(foot_bl='a', hand_x='b')))) == dict(foot_bl='a', hand_x='b')
    try:
        bone_map(dict(anim=dict(bones=dict(tail='a'))))
        raise AssertionError('an unknown role must be refused')
    except SystemExit as e:
        assert 'BAD_BONE_ROLE' in str(e)
    rest = [dict(t=i * .1, b=dict(hips=[0, 1, 0], hand_r=[.3, 1, 0]), low=0.) for i in range(3)]
    act = [dict(t=i * .1, b=dict(hips=[0, 1, z], hand_r=[.3, 1, hz]), low=-.02 * i)
           for i, (z, hz) in enumerate([(.05, 0), (.05, .1), (.1, .5), (.2, .6)])]
    d = dict(schema=PROBE_SCHEMA, char_height=1.0, clips=[dict(name='idle', length=.2, fps=10, loop=True, frames=rest),
                                                          dict(name='hit', length=.3, fps=10, loop=False, frames=act)])
    r = metrics(d, 'hit', 'reaction', rest_clip='idle')
    assert (r['start_gap_m'], r['end_gap_m'], r['end_gap_role']) == (.05, .6, 'hand_r'), r
    assert r['hand_peak']['hand_r']['peak_frame'] == 2.0 and r['hips_travel_m'] == .15 and r['ground']['lowest_m'] == -.06, r
    ap = dict(model_scale=dict(value=1.0), clips=dict(hit=dict(length_s=.3, lowest_m=-.05), gone=dict(length_s=1.0)), tol_m=.005)
    rc = recheck(dict(d, model_scale=[1.001, 1.001, 1.001]), ap)
    assert rc['changed'] == ['hit.lowest_m', 'gone'], rc                                         # -.06 vs -.05; missing clip
    assert pick(5, 3) == [0, 2, 4] and pick(3, 5) == [0, 1, 2] and pick(10, 2) == [0, 9] and pick(5, 1) == [0]
    return 'anim OK (known slide, seam-crossing contact, whole-clip contact, pose return, hand peak, recheck, frame pick)'


def main(kit, rest):
    if not rest:
        raise SystemExit(__doc__)
    sub, r = rest[0], rest[1:]
    if sub == 'metrics':
        return metrics_cmd(r)
    if sub == 'sheet':
        return sheet(r)
    if sub == 'recheck':
        return recheck_cmd(kit, r)
    if sub == 'probe':
        return probe(kit, r)
    if sub == 'frames':
        return frames(kit, r)
    if sub == 'fbxcheck':
        return fbxcheck(kit, r)
    raise SystemExit(__doc__)

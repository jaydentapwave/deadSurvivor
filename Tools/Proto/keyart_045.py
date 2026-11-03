#!/usr/bin/env python3
"""045 stage 1: drive Proto045_Main in Play mode and record evidence (based on the 044 runner).

Checks: layout pure checks, 1- and 4-hero sorties, 10 waves with per-wave natural spawn stats, a load segment
(editor-only driver keeps >=300 enemies for 20 s) with view/enemy reconciliation and frame-order evidence,
occlusion fade by injection, touch joystick, three real Game View sizes, Mobile vs PC quality key-art captures,
victory/defeat/re-sortie with view cleanup. Every cleanup step is attempted independently.
KEYART045_STAGE=smoke runs only open -> 4-hero start -> start-screen captures -> cleanup (initial real connection).
Tiers (docs/workflows/core-rules.md): the default KEYART045_STAGE=short is the 1-2 min Play check (squads B and C sortie,
move, stop, one 1280x720 capture each, console, cleanup). KEYART045_STAGE=full|load|baseline044 is a whole/long run and
needs DS_FULL_REASON="<stage end / pre-commit reason>" (Tools/Harness/tier.py require_full; the Claude hook checks it too).
Any other KEYART045_STAGE value is rejected before anything runs.
Short and full results are recorded for `Tools/Harness/tier.py plan` with captured=False under KEYART045_NOCAPTURE=1
(such a run never counts as visual evidence).
Usage: /usr/bin/python3 Tools/Proto/keyart_045.py            (short)
       DS_FULL_REASON="stage 3 end" KEYART045_STAGE=full /usr/bin/python3 Tools/Proto/keyart_045.py
"""
import glob, hashlib, json, math, os, shutil, struct, subprocess, sys, time, uuid
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

ROOT = Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor')
sys.path.insert(0, str(ROOT / 'Tools/Harness'))
import tier as TIER   # noqa: E402  verification tiers / evidence records
from play_probe import ATTACK as ATTACK_Q   # noqa: E402  047 H6: hero attack-layer state per hero
import protect   # noqa: E402  047 H4: side-effect files (font cache) restored when they were clean before
PROBE = 'DeadSurvivor.Editor.DSKeyArt045Verification'
TOUCH = 'DeadSurvivor.Editor.DSProtoMain044Verification'
SCREEN = 'DeadSurvivor.Editor.DSScreenVerification'
SIZES = [(1280, 720), (1560, 720), (1024, 768)]
START, NEXT, RESULT, REFORM = '스쿼드 출전 →', '다음 웨이브 →', '결과 확인 →', '스쿼드 재편성 →'
STAGE = os.environ.get('KEYART045_STAGE', 'short')
STAGES_SHORT = ('short', 'smoke', 'purity')          # 1-2 min checks, no reason needed
STAGES_FULL = ('full', 'load', 'baseline044')        # 10 waves and/or the load segment: DS_FULL_REASON (tier-policy full_if)
if STAGE not in STAGES_SHORT + STAGES_FULL:          # a typo must not fall through to the full path
    sys.exit(f"keyart_045.py: unknown KEYART045_STAGE={STAGE!r}. Allowed: {', '.join(STAGES_SHORT)} "
             f"(short) / {', '.join(STAGES_FULL)} (full, needs DS_FULL_REASON). Nothing was run.")
SCENE = os.environ.get('KEYART045_SCENE', 'Assets/DeadSurvivor/Scenes/Proto045/Proto045_Main.unity')
NOCAPTURE = os.environ.get('KEYART045_NOCAPTURE') == '1'   # batch-mode editor: no Game view (captures/screen checks NOT_RUN)
S3 = SCENE.endswith('_s3.unity')            # stage-3 hero test scene: a copy of s2 with bruno swapped
S3B = SCENE.endswith('_s3b.unity')          # stage-3 v2: all six heroes = one v2 base hero + per-hero textures
S2 = SCENE.endswith('_s2.unity') or S3 or S3B
ART3B = 'Assets/DeadSurvivor/Scenes/Proto045/Art_s3b'
V2_MESH, V2_CTRL, V2_RUN = ART3B + '/hero045v2.fbx', 'Hero045v2', 'run_lean_k4'
HERO_IDS = ('bruno', 'hans', 'lena', 'yuki', 'bolt', 'mira')
BRUNO_MESH = 'Assets/DeadSurvivor/Scenes/Proto045/Art_s3/hero_bruno045.fbx'
MESH_042 = 'Assets/DeadSurvivor/Art/Characters/Test042_r001/hero_model.fbx'


def parse_models(txt):
    out = {}
    for part in txt.split(','):
        hid, rest = part.split('=', 1); f = rest.split('|')
        out[hid] = dict(mesh=f[0], ctrl=f[1] if len(f) > 1 else '', **{k: v for k, v in (x.split('=', 1) for x in f[2:] if '=' in x)})
    return out


def judge_s3(mv, ma, mb, batch):
    """045 stage 3 (impl review s3 r001 #2, #5): bruno is drawn by the s3 FBX mesh with the s3 controller, switches Run/Idle
    and its state time advances; every other hero is drawn by the 042 mesh and controller. Bone poses: NOT_RUN only in a
    batch editor when no hero's pose changes; otherwise bruno's pose must change."""
    br = 'bruno'; a = ma.get(br, {})
    others = {k: v for k, v in ma.items() if k != br}
    checks = dict(bruno_present=br in ma, bruno_mesh_s3=a.get('asset') == BRUNO_MESH, bruno_ctrl=a.get('ctrl') == 'Hero045s3',
                  bruno_human=a.get('human') == '1', bruno_run_moving=mv.get(br, {}).get('run') == 'True', bruno_idle_stopped=a.get('run') == 'False',
                  bruno_state_run_moving=mv.get(br, {}).get('state') == 'Run', bruno_state_idle_stopped=a.get('state') == 'Idle',
                  bruno_state_time_advances=float(mb.get(br, {}).get('t', -1)) > float(a.get('t', -1)) >= 0,
                  others_042=len(others) > 0 and all(v.get('ctrl') == 'Hero045' and v.get('asset') == MESH_042 for v in others.values()))
    any_pose = any(ma[k].get('pose') != mb.get(k, {}).get('pose') for k in ma)
    bruno_pose = a.get('pose') is not None and a.get('pose') != mb.get(br, {}).get('pose')
    pose = 'PASS' if bruno_pose else ('NOT_RUN' if (batch and not any_pose) else 'FAIL')
    return ('PASS' if all(checks.values()) and pose != 'FAIL' else 'FAIL'), checks, pose


def _s3_selftest():
    ok_b = 'bruno=m|Hero045s3|human=1|run={r}|yaw=90|pose=1|state={s}|t={t}|asset=' + BRUNO_MESH
    ok_h = 'hans=m|Hero045|human=1|run={r}|yaw=180|pose=2|state={s}|t={t}|asset=' + MESH_042
    def txt(b, h, r, s_, t): return b.format(r=r, s=s_, t=t) + ',' + h.format(r=r, s=s_, t=t)
    mv, ma, mb = (parse_models(txt(ok_b, ok_h, 'True', 'Run', '1.0')), parse_models(txt(ok_b, ok_h, 'False', 'Idle', '0.1')),
                  parse_models(txt(ok_b, ok_h, 'False', 'Idle', '0.2')))
    cases = {'good_batch': (mv, ma, mb, True, 'PASS')}
    bad_mesh = parse_models(txt(ok_b.replace(BRUNO_MESH, MESH_042), ok_h, 'False', 'Idle', '0.1'))
    cases['bruno_042_mesh_with_s3_controller'] = (mv, bad_mesh, mb, True, 'FAIL')
    no_mesh = parse_models(txt(ok_b.replace(BRUNO_MESH, 'none'), ok_h, 'False', 'Idle', '0.1'))
    cases['bruno_no_mesh'] = (mv, no_mesh, mb, True, 'FAIL')
    cases['static_poses_windowed_editor'] = (mv, ma, mb, False, 'FAIL')
    other_bad = parse_models(txt(ok_b, ok_h.replace(MESH_042, BRUNO_MESH), 'False', 'Idle', '0.1'))
    cases['other_hero_not_042'] = (mv, other_bad, mb, True, 'FAIL')
    res = {k: judge_s3(*v[:4])[0] == v[4] for k, v in cases.items()}
    return dict(status='PASS' if all(res.values()) else 'FAIL', cases=res)


SQUAD_B, SQUAD_C = ('bruno', 'hans', 'lena', 'yuki'), ('lena', 'yuki', 'bolt', 'mira')


def _ang(a, b):
    """absolute angle difference (deg); NaN or missing -> 999 (fails)"""
    if b is None or a != a: return 999.0
    return abs((a - b + 180) % 360 - 180)


def _circ_mean(degs):
    """circular mean (deg); any NaN -> NaN"""
    if not degs or any(d != d for d in degs): return float('nan')
    s = sum(math.sin(math.radians(d)) for d in degs); c = sum(math.cos(math.radians(d)) for d in degs)
    return math.degrees(math.atan2(s, c))


def judge_s3b(sm, expected, batch):
    """045 stage 3 v2 (design D4; impl review v2 r001 #1). sm = dict(move_a, move_b, idle_a, idle_b) parsed samples taken
    0.35 s apart while the squad moves and 0.5 s apart after it stops. The live heroes must be exactly the expected
    squad; each hero: v2 mesh, ITS OWN texture, v2 controller, humanoid; moving: Run state with the reduced clip in both
    samples, bone pose (local rotations) and state time change between them; stopped: Idle in both samples, pose and time
    change. Bone-pose checks may be NOT_RUN only in a batch editor when no hero's pose changes in either phase."""
    ma, mb, ia, ib = (sm.get(k, {}) for k in ('move_a', 'move_b', 'idle_a', 'idle_b'))
    series = sm.get('move_series') or []
    heroes = sorted(ia); per = {}
    REQ = ('pose', 't', 'asset', 'tex', 'human', 'run', 'state', 'clip', 'body')
    def linked(v, h): return (v.get('asset') == V2_MESH and v.get('tex') == f'{ART3B}/hero045v2_{h}.png' and v.get('ctrl') == V2_CTRL
                              and v.get('human') == '1')
    for h in heroes:
        a, b, x, y = ma.get(h, {}), mb.get(h, {}), ia.get(h, {}), ib.get(h, {})
        # impl review v2 r002 #1: every sample must carry every field and the same correct asset links (not only idle_a)
        per[h] = dict(fields_all_samples=all(all(k in v and v[k] not in ('', 'none', 'nan') for k in REQ) for v in (a, b, x, y)),
                      linked_all_samples=all(linked(v, h) for v in (a, b, x, y)),
                      mesh=x.get('asset') == V2_MESH, tex=x.get('tex') == f'{ART3B}/hero045v2_{h}.png', ctrl=x.get('ctrl') == V2_CTRL, human=x.get('human') == '1',
                      run_moving=all(v.get('run') == 'True' and v.get('state') == 'Run' and v.get('clip') == V2_RUN for v in (a, b)),
                      time_moving=float(b.get('t', -1)) > float(a.get('t', -1)) >= 0,
                      # S3-22: hand-bone body yaw swings ~±35° over a run cycle, so the facing check uses the circular mean of
                      # move_a + move_series (4) + move_b; the S3-21 defect was a constant 68° offset
                      faces_movement=_ang(_circ_mean([float(v.get('body', 'nan')) for v in [a] + [m.get(h, {}) for m in series] + [b]]), sm.get('move_yaw')) <= 35
                      and len(series) >= 4,
                      idle_stopped=all(v.get('run') == 'False' and v.get('state') == 'Idle' for v in (x, y)),
                      time_idle=float(y.get('t', -1)) > float(x.get('t', -1)) >= 0)
    checks = dict(squad_exact=all(sorted(v) == sorted(expected) for v in (ia, ib, ma, mb)), per_hero=all(all(v.values()) for v in per.values()),
                  distinct_textures=len({ia[h].get('tex') for h in heroes}) == len(heroes))
    def changed(u, v): return u is not None and v is not None and u != v
    moved = {h: (changed(ma.get(h, {}).get('pose'), mb.get(h, {}).get('pose')), changed(ia[h].get('pose'), ib.get(h, {}).get('pose'))) for h in heroes}
    any_moved = any(m or i for m, i in moved.values())
    pose = 'PASS' if heroes and all(m and i for m, i in moved.values()) else ('NOT_RUN' if (batch and not any_moved) else 'FAIL')
    return ('PASS' if all(checks.values()) and pose != 'FAIL' else 'FAIL'), dict(checks, per_hero_detail=per, pose_moved=moved), pose


def _s3b_selftest():
    def entry(h, run, state, t, pose, tex=None, mesh=V2_MESH, clip=None, body=90.0):
        return (f"{h}=m|{V2_CTRL}|human=1|run={run}|yaw=0|pose={pose}|state={state}|t={t}|asset={mesh}|body={body}"
                f"|tex={tex or f'{ART3B}/hero045v2_{h}.png'}|clip={clip or (V2_RUN if run == 'True' else 'Armature_idle')}")
    hs = ('bruno', 'lena')
    def txt(run, state, t, pose, only=hs, **kw): return ','.join(entry(h, run, state, t, pose + i, **kw.get(h, {})) for i, h in enumerate(only))
    def ser(**kw): return [parse_models(txt('True', 'Run', '1.1', 50 + i, **kw)) for i in range(4)]
    def good(): return dict(move_a=parse_models(txt('True', 'Run', '1.0', 10)), move_b=parse_models(txt('True', 'Run', '1.3', 40)),
                            idle_a=parse_models(txt('False', 'Idle', '0.1', 20)), idle_b=parse_models(txt('False', 'Idle', '0.2', 30)), move_yaw=90.0,
                            move_series=ser())
    def mod(k, v): g = good(); g[k] = v; return g
    cases = {
        'good_windowed': (good(), hs, False, 'PASS'),
        'squad_missing_hero': (mod('idle_a', parse_models(txt('False', 'Idle', '0.1', 20, only=('bruno',)))), hs, False, 'FAIL'),
        'textures_swapped': (mod('idle_a', parse_models(txt('False', 'Idle', '0.1', 20, lena=dict(tex=f'{ART3B}/hero045v2_bruno.png')))), hs, False, 'FAIL'),
        'mesh_042': (mod('idle_a', parse_models(txt('False', 'Idle', '0.1', 20, bruno=dict(mesh=MESH_042)))), hs, False, 'FAIL'),
        'mesh_missing': (mod('idle_a', parse_models(txt('False', 'Idle', '0.1', 20, bruno=dict(mesh='none')))), hs, False, 'FAIL'),
        'run_clip_not_reduced': (mod('move_b', parse_models(txt('True', 'Run', '1.3', 40, bruno=dict(clip='Armature_run')))), hs, False, 'FAIL'),
        'pose_frozen_while_moving': (mod('move_b', parse_models(txt('True', 'Run', '1.3', 10))), hs, False, 'FAIL'),
        'pose_frozen_idle': (mod('idle_b', parse_models(txt('False', 'Idle', '0.2', 20))), hs, False, 'FAIL'),
        'time_static_moving': (mod('move_b', parse_models(txt('True', 'Run', '1.0', 40))), hs, False, 'FAIL'),
        'not_idle_after_stop': (mod('idle_b', parse_models(txt('True', 'Run', '0.2', 30))), hs, False, 'FAIL'),
        # S3-22: a run-cycle swing (52..115, mean ~86) is facing; a constant 68° turn (S3-21) is not; no series -> FAIL
        'run_cycle_swing_ok': (dict(good(), move_a=parse_models(txt('True', 'Run', '1.0', 10, bruno=dict(body=52.0))),
                                    move_b=parse_models(txt('True', 'Run', '1.3', 40, bruno=dict(body=93.0))),
                                    move_series=[parse_models(txt('True', 'Run', '1.1', 50 + i, bruno=dict(body=b))) for i, b in enumerate((60.0, 85.0, 114.0, 112.0))]), hs, False, 'PASS'),
        'body_turned_68': (dict(good(), move_a=parse_models(txt('True', 'Run', '1.0', 10, bruno=dict(body=22.0))),
                                move_b=parse_models(txt('True', 'Run', '1.3', 40, bruno=dict(body=22.0))), move_series=ser(bruno=dict(body=22.0))), hs, False, 'FAIL'),
        'series_missing': (dict(good(), move_series=[]), hs, False, 'FAIL'),
        'pose_missing_move_b': (mod('move_b', parse_models(txt('True', 'Run', '1.3', 40).replace('|pose=40', ''))), hs, False, 'FAIL'),
        'mesh_042_while_moving': (mod('move_a', parse_models(txt('True', 'Run', '1.0', 10, bruno=dict(mesh=MESH_042)))), hs, False, 'FAIL'),
        'squad_missing_in_move': (mod('move_a', parse_models(txt('True', 'Run', '1.0', 10, only=('bruno',)))), hs, False, 'FAIL'),
        'batch_no_pose_change': (dict(move_a=parse_models(txt('True', 'Run', '1.0', 10)), move_b=parse_models(txt('True', 'Run', '1.3', 10)),
                                      idle_a=parse_models(txt('False', 'Idle', '0.1', 20)), idle_b=parse_models(txt('False', 'Idle', '0.2', 20)), move_yaw=90.0, move_series=ser()), hs, True, 'PASS'),
    }
    res = {k: judge_s3b(*v[:3])[0] == v[3] for k, v in cases.items()}
    return dict(status='PASS' if all(res.values()) else 'FAIL', cases=res)


def sample_no_hit(tries=6):
    txt = call('HeroViewModels')
    for _ in range(tries):
        if '|tex=HIT' not in txt: break
        time.sleep(0.25); txt = call('HeroViewModels')
    return txt


def move_sample():
    """S3B: move the live squad with the touch joystick (lower-left start so the joystick does not cover anyone).
    While moving: move_a, four quick body samples (move_series, S3-22), move_b; release; two stopped samples 0.5 s apart.
    The party must actually move before the samples count (S3-20) and must still be running at move_b. Start positions
    vary, so a wall may stop it (S3-22): then the next attempt goes left, where the party just came from. Up to three
    attempts; the last attempt's samples are kept so the judge fails with data when none succeeds."""
    out = {}; call('BeginInputScope', cls=TOUCH)
    try:
        rect = None
        for _ in range(6):
            try: rect = json.loads(call('MovementRect', cls=TOUCH).split('|')[0]); break
            except Exception: time.sleep(0.5)
        if rect is None: raise RuntimeError('S3B_NO_MOVEMENT_RECT')
        x = rect['x'] + rect['width'] * 0.15; y = rect['y'] + rect['height'] * 0.2
        diag = []; dx = 90
        for attempt_no in range(3):
            c0 = state()['center']
            call('Touch', 'began', float(x), float(y), cls=TOUCH); time.sleep(0.2)
            call('Touch', 'moved', float(x + dx), float(y), cls=TOUCH); time.sleep(0.6)
            s1 = state(); moved = abs(s1['center'][0] - c0[0]) + abs(s1['center'][1] - c0[1])
            entry = dict(attempt=attempt_no, dx=dx, joystickActive=s1['joystickActive'], joystick=s1['joystick'], moveInput=s1.get('moveInput'),
                         centre_moved=round(moved, 2))
            if not (s1['joystickActive'] and moved > 2):      # did not start (S3-20): same direction after 2 s
                entry['result'] = 'not_moving'; diag.append(entry)
                call('Touch', 'ended', float(x + dx), float(y), cls=TOUCH); time.sleep(2.0); continue
            c_a = state()['center']; ma = sample_no_hit(); series = []
            for _ in range(4):
                series.append(call('HeroViewModels')); time.sleep(0.1)
            mb = sample_no_hit(); c_b = state()['center']
            out.update(move_a=ma, move_b=mb, move_series=json.dumps(series), move_centres=json.dumps([c_a, c_b]))
            running = all(v.get('state') == 'Run' for v in parse_models(mb).values())
            entry.update(result='ok' if running else 'stopped', segment=round(abs(c_b[0] - c_a[0]) + abs(c_b[1] - c_a[1]), 1)); diag.append(entry)
            if running: break
            call('Touch', 'ended', float(x + dx), float(y), cls=TOUCH); time.sleep(1.0); dx = -dx   # blocked: go back the way it came
        out['move_diag'] = json.dumps(diag)
        if 'move_a' not in out:   # never started moving: keep samples so the judge fails with data
            c = state()['center']; out.update(move_a=sample_no_hit(), move_b=sample_no_hit(), move_series='[]', move_centres=json.dumps([c, c]))
        call('Touch', 'ended', float(x + dx), float(y), cls=TOUCH); time.sleep(1.0)
        out['idle_a'] = sample_no_hit(); time.sleep(0.5); out['idle_b'] = sample_no_hit()
    finally:
        try: call('RemoveTouch', cls=TOUCH)
        finally: call('EndInputScope', cls=TOUCH)
    return out


def s3b_step(name, expected):
    raw = move_sample(); sm = {k: parse_models(v) for k, v in raw.items() if k not in ('move_diag', 'move_centres', 'move_series')}
    sm['move_series'] = [parse_models(x) for x in json.loads(raw.get('move_series', '[]'))]
    (ax, ay), (bx, by) = json.loads(raw['move_centres'])
    sm['move_yaw'] = math.degrees(math.atan2(bx - ax, -(by - ay)))   # sim (x, y) -> world (x, -z): yaw = atan2(dx, dz)
    st, ck, pose = judge_s3b(sm, expected, call('BatchMode') == '1')
    step(name, status=st, expected=list(expected), checks=ck, bone_pose=pose, judge_selftest=S3_SELFTEST, **raw)
    return st


S3_SELFTEST = _s3_selftest() if S3 else (_s3b_selftest() if S3B else None)
if (S3 or S3B) and S3_SELFTEST['status'] != 'PASS':
    raise SystemExit('S3_JUDGE_SELFTEST_FAILED:' + json.dumps(S3_SELFTEST))
RT = 'Assets/DeadSurvivor/Runtime/'
SOURCES = ['Tools/Proto/keyart_045.py', 'Tools/Proto/build_keyart_045.cs', 'Tools/Proto/keyart_layout_045.py',
           'Assets/DeadSurvivor/Editor/DSKeyArt045Verification.cs', 'Assets/DeadSurvivor/Editor/DSProtoMain044Verification.cs',
           'Assets/DeadSurvivor/Editor/DSScreenVerification.cs', 'Assets/DeadSurvivor/Art/LowPoly043/Shaders/DSLowPolyPalette.shader',
           'Assets/DeadSurvivor/Scenes/Proto045/Proto045_Main.unity', 'Assets/DeadSurvivor/Scenes/Proto045/Layout045.json'] + [
    RT + f for f in ('DSWorld.Map.cs', 'DSMapVisibility.cs', 'DSBlockoutData.cs', 'DSMapActorViews.cs', 'DSMapActorSet.cs',
                     'DSMapPresentation.cs', 'DSLookGlobals.cs', 'DSKeyArt045LoadDriver.cs', 'DSUI.cs')]
if S2:
    # provider driver / Blender normaliser are not fingerprinted: their outputs (Art_s2 files) are, and stage-3 work keeps using them
    SOURCES += ['Tools/Proto/build_keyart_045_s2.cs', 'Tools/Proto/render_check_045_s2.cs', 'Tools/Proto/Water045.shader', SCENE] + (
        ['Tools/Proto/build_hero_045_s3.cs', 'Assets/DeadSurvivor/Scenes/Proto045/Proto045_Main_s2.unity'] + sorted(str(Path(p).relative_to(ROOT)) for p in glob.glob(
            str(ROOT / 'Assets/DeadSurvivor/Scenes/Proto045/Art_s3/**/*'), recursive=True) if Path(p).is_file()) if S3 else []) + (
        ['Tools/Proto/build_heroes_045_s3b.cs', 'Assets/DeadSurvivor/Scenes/Proto045/Proto045_Main_s2.unity'] + sorted(str(Path(p).relative_to(ROOT)) for p in glob.glob(
            str(ROOT / (ART3B + '/**/*')), recursive=True) if Path(p).is_file()) if S3B else []) + sorted(str(Path(p).relative_to(ROOT)) for p in glob.glob(str(ROOT / 'Assets/DeadSurvivor/Scenes/Proto045/Art_s2/**/*'), recursive=True) if Path(p).is_file())
PROTECTED = sorted(set(SOURCES + ['Assets/DeadSurvivor/Scenes/Proto045/Proto045_Main.unity'] + [str(Path(p).relative_to(ROOT)) for p in glob.glob(str(ROOT / 'Assets/DeadSurvivor/Scenes/Proto045/Art/**/*'), recursive=True) if Path(p).is_file()] + [str(Path(p).relative_to(ROOT)) for p in glob.glob(str(ROOT / 'ProjectSettings/*.asset'))] + [
    'ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json', 'Assets/DeadSurvivor/Resources/DeadSurvivor/UI/PrototypeFont.asset',
    'Assets/DeadSurvivor/Scenes/DeadSurvivor.unity', 'Assets/DeadSurvivor/Scenes/Map038_TripoPair_r003/Map038_TripoPair.unity',
    'Assets/DeadSurvivor/Scenes/Map043_LowPolyShader_r001/Map043_LowPolyShader.unity', 'Assets/DeadSurvivor/Scenes/Proto044/Proto044_Main.unity',
    'Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json', 'Assets/InputSystem_Actions.inputactions'] + [
    str(Path(p).relative_to(ROOT)) for p in glob.glob(str(ROOT / 'Assets/Settings/*.asset')) + glob.glob(str(ROOT / 'Assets/DeadSurvivor/Art/LowPoly043/Materials/*.mat'))
    + glob.glob(str(ROOT / 'Assets/DeadSurvivor/Scenes/Map037_ReferenceDetail_r002/**/Foliage*.mat'), recursive=True)]))

RUN = uuid.uuid4().hex[:12]
OUT = ROOT / 'output/keyart-target-045/r001' / ('run-' + RUN)
OUT.mkdir(parents=True)
T0 = time.time()
steps = []
report = dict(status='RUNNING', run=RUN, stage=STAGE, scene=SCENE, nocapture=NOCAPTURE, started=datetime.now(ZoneInfo('Asia/Seoul')).isoformat(), steps=steps)


def save():
    (OUT / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=1))


def digest(rel):
    p = ROOT / rel
    return hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else 'MISSING'


def fingerprints(paths):
    return {p: digest(p) for p in paths}


def cli(args, timeout=120):
    i = args.index('--') if '--' in args else len(args)
    r = subprocess.run(['unity', *args[:i], '--project-path', str(ROOT), '--format', 'json', *args[i:]], capture_output=True, text=True, timeout=timeout)
    try:
        d = json.loads(r.stdout)
    except json.JSONDecodeError:
        raise RuntimeError('CLI_NOT_JSON:' + (r.stdout + r.stderr)[:400])
    if not d.get('success'):
        raise RuntimeError('CLI_FAIL:' + json.dumps(d.get('errors'), ensure_ascii=False)[:400])
    return d['data']


def lit(a):
    return repr(a) + 'f' if isinstance(a, float) else json.dumps(a)


def evaluate(code, retries=0):
    for attempt in range(retries + 1):
        try:
            res = cli(['command', 'script', 'eval', '--', code], 60)['result']
            if not res.get('success', True):
                raise RuntimeError('EVAL_FAIL:' + json.dumps(res, ensure_ascii=False)[:500])
            return res['result']
        except Exception:
            if attempt == retries:
                raise
            time.sleep(1.0)


def call(method, *args, retries=0, cls=PROBE):
    report['last_call'] = f'{cls.split(".")[-1]}.{method}{args}'
    return evaluate(f'return {cls}.{method}({",".join(lit(a) for a in args)});', retries)


def state(retries=0):
    s = json.loads(call('State', retries=retries))
    if s.get('keysPressed'):
        report.setdefault('external_keys', []).append(dict(t=round(time.time() - T0, 1), keys=s['keysPressed'][:6]))
    return s


def step(name, **kw):
    steps.append(dict(name=name, t=round(time.time() - T0, 2), **kw))
    save()


def wait(pred, what, timeout=20.0, retries=3):
    end, last = time.time() + timeout, None
    while time.time() < end:
        try:
            last = state(retries=retries)
            if pred(last):
                return last
        except Exception as e:
            last = {'error': str(e)[:200]}
        time.sleep(0.3)
    raise RuntimeError('TIMEOUT:' + what + ':' + json.dumps(last, ensure_ascii=False)[:600])


def png_size(path):
    return struct.unpack('>II', Path(path).read_bytes()[16:24])


def edge_check(path):
    sys.path.append('/Users/hyeongwoo/Library/Python/3.9/lib/python/site-packages')
    from PIL import Image
    im = Image.open(path).convert('RGB'); w, h = im.size
    pts = [(x, y) for x in range(0, w, max(1, w // 64)) for y in (1, h - 2)] + [(x, y) for y in range(0, h, max(1, h // 36)) for x in (1, w - 2)]
    bad = [p for p in pts if max(abs(a - b) for a, b in zip(im.getpixel(p), (13, 19, 23))) > 8]
    return dict(samples=len(pts), mismatched=len(bad), first=bad[:3], status='PASS' if not bad else 'FAIL')


def capture(label, sizes=SIZES, edges=None):
    if NOCAPTURE:
        step('capture', label=label, shots=[], note='NOT_RUN: batch-mode editor has no Game view')
        return []
    shots = []
    for w, h in sizes:
        report['screen_resized'] = True
        call('Resize', w, h, cls=SCREEN)
        check = None
        for _ in range(20):
            time.sleep(0.25)
            try:
                check = call('Check', w, h, cls=SCREEN); break
            except Exception as e:
                check = 'PENDING:' + str(e)[:160]
        if not isinstance(check, str) or check.startswith('PENDING'):
            raise RuntimeError(f'SCREEN_CHECK_FAILED:{label}:{w}x{h}:{check}')
        rel = f'output/keyart045/{RUN}/{label}-{w}x{h}.png'
        if (ROOT / 'Assets' / rel).exists():
            raise RuntimeError('CAPTURE_EXISTS')
        cli(['command', 'capture', 'game_view', '--', '--source', 'screen', '--width', str(w), '--height', str(h), '--save_path', rel], 120)
        dst = OUT / f'{label}-{w}x{h}.png'
        shutil.copy2(ROOT / 'Assets' / rel, dst)
        shot = dict(file=dst.name, size=png_size(dst), sha256=hashlib.sha256(dst.read_bytes()).hexdigest(), screen_check=check, state=state())
        if edges if edges is not None else label.startswith(('lobby', 'prep', 'result')):
            shot['edges'] = edge_check(dst)
        shots.append(shot)
    step('capture', label=label, shots=shots)
    return shots


def console_errors():
    return cli(['command', 'console', 'get', '--', '--tail', '50', '--level', 'error'], 60)['result']


def attempt(name, fn):
    try:
        report.setdefault('cleanup', {})[name] = fn()
        return True
    except Exception as e:
        report.setdefault('cleanup_errors', {})[name] = str(e)[:300]
        return False


def views_ok(s):
    return s['views'] == s['enemies'] + s['heroesAlive']


def select_only(n):
    s = state()
    for i in list(s['selected']):
        call('Submit', 'ToggleSelection', i)
    for i in range(n):
        call('Submit', 'ToggleSelection', i)
    return wait(lambda s: s['selectedCount'] == n, f'select{n}')


def layout_r1_behind():
    d = json.loads((ROOT / 'Assets/DeadSurvivor/Scenes/Proto045/Layout045.json').read_text())
    o = next(x for x in d['objects'] if x['id'] == 'OB-001')
    pts = [((p['x'] - 565) * 4, (p['y'] - 580) * 4) for p in o['points_px']]
    cx = sum(p[0] for p in pts) / len(pts)
    return cx, min(p[1] for p in pts)


def load_segment(label='load'):
    report['pipeline_load'] = call('MobilePipeline', True)
    d, shot, stats = {}, None, []
    try:
        call('AttachDriver', 300, 20.0, 35.0, False)
        end = time.time() + 80
        while time.time() < end:
            time.sleep(1.0)
            d = json.loads(call('DriverReport'))
            if d.get('reached') and len(stats) < 6:
                try:
                    stats.append(json.loads(call('Stats')))
                except Exception as e:
                    stats.append({'error': str(e)[:160]})
            if d.get('reached') and shot is None:
                shot = capture(label, [(1280, 720)], edges=False)
            if d.get('ended'):
                break
        d = json.loads(call('DriverReport'))
        call('RemoveDriver')
    finally:
        report['pipeline_load_restored'] = call('MobilePipeline', False)
    order_ok = d.get('orderViolations', 1) == 0
    prev = None
    for line in d.get('order', []):
        kv = dict(x.split('=') for x in line.split())
        if prev is not None and int(kv['sim']) > prev and int(kv['rendered']) >= 0:
            order_ok &= kv['rendered'] == kv['frame']
        prev = int(kv['sim'])
    # not held although every injection succeeded -> the injector could not outpace kills (harness limit) -> BLOCKED.
    # not held with injection failures -> placement problem -> FAIL (below).
    rate_limited = not d.get('held') and d.get('endReason') == 'REACH_LIMIT' and d.get('injectFailures') == 0
    state_ended = not d.get('held') and str(d.get('endReason', '')).startswith('STATE_')   # 045-J5: wave ended under the segment
    if d.get('heroDied') or d.get('searchFieldMissing') or not order_ok or not d.get('attached') or rate_limited or state_ended:
        status = 'BLOCKED'
    elif d.get('held') and d.get('mismatches') == 0 and d.get('noPathMax') == 0 and d.get('unexplainedMaxRatio', 1) <= 0.02:
        status = 'PASS'
    else:
        status = 'FAIL'
    seg = dict(status=status, order_ok=order_ok, rate_limited=rate_limited, driver=d, stats=stats,
               note='PC quality level with Mobile_RPAsset swapped in (not the full Mobile quality level). still = >=10 s still & >60 from heroes; classified: holdRanged (163.8..231.84 + LOS) / phase / noPath (TryPath 16384) / waitRetry / pendingSearch / unexplained (onRoute or noRoute). Pass needs noPath == 0 and unexplained <= 2 % of alive.')
    step(label + '_segment', **seg)
    return seg


def natural_waves(lobby, s, captures):
    base_uid, base_fail = 0, lobby['spawnFailures']   # enemyUid restarts at 0 on START; failures only accumulate
    waves, spawn_ok = [], True
    for w in range(1, 11):
        if w > 1:
            call('Click', NEXT)
            s = wait(lambda s, w=w: s['state'] == 'battle' and s['wave'] == w, f'battle_n{w}')
        call('AttachDriver', 0, 0.0, 0.0, True)
        inside, views_bad, samples = 0, 0, 0
        end = time.time() + s['duration'] + 30
        while time.time() < end:
            time.sleep(1.0)
            cur = state()
            if cur['state'] != 'battle':
                break
            samples += 1; inside = max(inside, cur['enemiesInsideObstacles']); views_bad += 0 if (cur['views'] < 0 or views_ok(cur)) else 1
        d = json.loads(call('DriverReport')); call('RemoveDriver')
        b = wait(lambda s: s['state'] in ('prep', 'result'), f'wave_end_n{w}', 10)
        succ, fl = b['enemyUid'] - base_uid, b['spawnFailures'] - base_fail
        base_uid, base_fail = b['enemyUid'], b['spawnFailures']
        rate = fl / (succ + fl) if succ + fl else 0.0
        rec = dict(wave=w, spawned=succ, failures=fl, failure_rate=round(rate, 4), geo_failures=d.get('geoFailures'), search_failures=d.get('searchFailures'),
                   inside_max=inside, views_mismatch_samples=views_bad, samples=samples,
                   driver=dict((k, d.get(k)) for k in ('endReason', 'heroDied', 'mismatches', 'orderViolations', 'maxAlive', 'rawStillMax', 'holdRangedMax', 'phaseMax',
                                                       'noPathMax', 'waitingPathMax', 'pendingSearchMax', 'unexplainedMax', 'unexplainedMaxRatio', 'noPathSamples', 'unexplainedSamples')),
                   # measured cause split (DSKeyArt045LoadDriver.ClassifySpawnStep; diagnostic only, the PASS rule below is unchanged)
                   spawn_split=dict(geo=d.get('geoFailures'), budget=d.get('budgetFailures'), path=d.get('pathFailures'), burning_window=d.get('windowFailures'),
                                    saturated_steps=d.get('saturatedSteps'), multi_sim_frames=d.get('multiSimFrames'), ticks=d.get('ticks'),
                                    frame_ms_mean=round(d.get('frameMsMean') or -1, 1), slow33=d.get('slowFrames33'), slow50=d.get('slowFrames50'),
                                    fields_missing=d.get('spawnFieldsMissing'), fail_samples=(d.get('spawnFailSamples') or [])[:8]))
        ok = rate <= 0.20 and inside == 0 and views_bad == 0 and not d.get('heroDied') and d.get('mismatches') == 0 and d.get('orderViolations') == 0 and d.get('noPathMax') == 0 and d.get('unexplainedMaxRatio', 1) <= 0.02
        rec['status'] = 'PASS' if ok else 'FAIL'; spawn_ok &= ok
        waves.append(rec); step('wave_natural', **rec)
        if w == 1 and captures:
            capture('prep')
    return waves, spawn_ok


def spawn_cause(waves, selftest):
    """measured spawn-failure cause over the waves that failed the 20 % rule (or all waves when none failed)"""
    judged = [w for w in waves if w.get('failure_rate', 0) > 0.20] or waves
    tot = {k: sum((w.get('spawn_split') or {}).get(k) or 0 for w in judged) for k in ('geo', 'budget', 'path', 'burning_window', 'saturated_steps', 'multi_sim_frames')}
    n = tot['geo'] + tot['budget'] + tot['path']
    bad = selftest != 'PASS' or any((w.get('spawn_split') or {}).get('fields_missing') in (True, None) for w in judged) or tot['multi_sim_frames'] > 0
    if bad or n == 0:
        label = 'UNKNOWN' if bad else 'NO_FAILURES'
    else:
        top = max(('geo', 'budget', 'path'), key=lambda k: tot[k])
        label = f"{top.upper()}({round(100 * tot[top] / n)}%)"
    return dict(label=label, waves=[w['wave'] for w in judged], totals=tot, selftest=selftest,
                frame_ms_mean={w['wave']: (w.get('spawn_split') or {}).get('frame_ms_mean') for w in judged})


def _spawn_cause_selftest():
    sp = lambda g, b, p, missing=False, multi=0: dict(geo=g, budget=b, path=p, burning_window=0, saturated_steps=0, multi_sim_frames=multi, fields_missing=missing)
    w = lambda n, rate, split: dict(wave=n, failure_rate=rate, spawn_split=split)
    c = {}
    c['budget_dominant'] = spawn_cause([w(6, 0.05, sp(0, 5, 0)), w(7, 0.27, sp(0, 150, 18))], 'PASS')['label'] == 'BUDGET(89%)'
    c['only_failed_waves_judged'] = spawn_cause([w(6, 0.05, sp(0, 0, 50)), w(7, 0.27, sp(0, 150, 18))], 'PASS')['waves'] == [7]
    c['selftest_fail_unknown'] = spawn_cause([w(7, 0.27, sp(0, 150, 18))], 'FAIL:x')['label'] == 'UNKNOWN'
    c['missing_fields_unknown'] = spawn_cause([w(7, 0.27, sp(0, 150, 18, missing=True))], 'PASS')['label'] == 'UNKNOWN'
    c['old_driver_unknown'] = spawn_cause([dict(wave=7, failure_rate=0.27)], 'PASS')['label'] == 'UNKNOWN'
    c['merged_steps_unknown'] = spawn_cause([w(7, 0.27, sp(0, 150, 18, multi=3))], 'PASS')['label'] == 'UNKNOWN'
    c['no_failures'] = spawn_cause([w(1, 0.0, sp(0, 0, 0))], 'PASS')['label'] == 'NO_FAILURES'
    return dict(status='PASS' if all(c.values()) else 'FAIL', cases=c)


# 047 H6: scenes whose heroes carry the Attack047 upper-body layer; the short run checks that at least one attack plays
ATTACK_LAYER_SCENES = ('Assets/DeadSurvivor/Scenes/Map046/Map046_Full.unity',)


def attack_sample(max_samples=30):
    """sample every 0.1 s until the first Slash/Shoot state (or max_samples). PASS = the layer exists and an attack
    played: catches a lost layer/trigger after a controller or builder change, not attack timing or look."""
    counts = {}
    for _ in range(max_samples):
        for item in filter(None, evaluate(ATTACK_Q).split(',')):
            n, v = item.rsplit(':', 1); counts.setdefault(n, {'S': 0, 'H': 0, '-': 0})[v] += 1
        if not counts or any(c['S'] + c['H'] for c in counts.values()):
            break
        time.sleep(0.1)
    ok = bool(counts) and any(c['S'] + c['H'] for c in counts.values())
    return ('PASS' if ok else 'FAIL'), (counts or 'NO_ATTACK047_LAYER')


def skip_to_wave(n):
    for w in range(1, n):
        s = state()
        if s['wave'] != w or s['state'] != 'battle':
            raise RuntimeError(f'SKIP_EXPECTED_BATTLE_{w}:' + json.dumps(s)[:200])
        call('Submit', 'DebugFinish', 0)
        wait(lambda s: s['state'] in ('prep', 'result'), f'skip_finish_w{w}', 30)
        call('Click', NEXT)
        wait(lambda s, w=w: s['state'] == 'battle' and s['wave'] == w + 1, f'skip_battle_w{w + 1}')


def run_short():
    """short tier (core-rules): leftover cleanup -> open -> Play -> squads B (0-3) and C (2-5): sortie, move, stop
    (S3B scenes: hero judge; other scenes: the party really moves and stops), one 1280x720 capture per squad for the
    user, defeat -> reform -> lobby with no views left -> console / protected files / cleanup. About 1-2 minutes.
    Scenes in ATTACK_LAYER_SCENES also sample the hero attack layer once (squad B, 047 H6)."""
    subprocess.run(['unity', 'status', '--until-ready', '--project-path', str(ROOT)], capture_output=True, timeout=300)
    report['sources'] = fingerprints(SOURCES); protected_before = fingerprints(PROTECTED); results = {}; side_snap = protect.snapshot()
    try: report['console_errors_before'] = console_errors()
    except Exception as e: report['console_errors_before'] = {'error': str(e)[:200]}
    pre = report['pre_cleanup'] = {}
    for name, fn in (('touch', lambda: call('RemoveTouch', cls=TOUCH)), ('input_scope', lambda: call('EndInputScope', cls=TOUCH)),
                     ('screen', lambda: call('Restore', cls=SCREEN)), ('exit_play', lambda: call('ExitPlay')),
                     ('wait_edit', lambda: wait(lambda s: not s.get('playing'), 'pre_exit', 60, retries=5) and 'EDIT_MODE'),
                     ('quality', lambda: call('RestoreQuality')), ('scene_setup', lambda: call('Restore', retries=3))):
        try: pre[name] = str(fn())[:60]          # leftovers of an interrupted run; 'skip' is normal when nothing is left
        except Exception as e: pre[name] = 'skip: ' + str(e)[:60]
    try:
        report['open_attempted'] = True; call('OpenScene', SCENE)
        report['run_in_background_before'] = evaluate('return UnityEditor.PlayerSettings.runInBackground.ToString();')
        cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', 'true'], 60); report['run_in_background_override'] = True
        call('EnterPlay'); wait(lambda s: s.get('playing') and s.get('state') == 'lobby', 'enter_play', 60, retries=5)
        for name, picks, squad in (('short_b', (0, 1, 2, 3), SQUAD_B), ('short_c', (2, 3, 4, 5), SQUAD_C)):
            lobby = state()
            for i in list(lobby['selected']): call('Submit', 'ToggleSelection', i)
            for i in picks: call('Submit', 'ToggleSelection', i)
            wait(lambda s: sorted(s['selected']) == list(picks), 'select_' + name)
            call('Click', START); wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'battle_' + name); time.sleep(2.5)
            capture(name, sizes=[(1280, 720)])
            if name == 'short_b' and SCENE in ATTACK_LAYER_SCENES:
                results['attack_layer'], counts = attack_sample(); step('attack_layer', status=results['attack_layer'], counts=counts)
            if S3B:
                results[name] = s3b_step(name, squad)
            else:
                raw = move_sample(); d = json.loads(raw['move_diag'])
                results[name] = 'PASS' if d and d[-1].get('result') == 'ok' else 'FAIL'
                step(name, status=results[name], move_diag=raw['move_diag'])
            call('KillParty'); wait(lambda s: s['state'] == 'result', 'defeat_' + name)
            call('Click', REFORM); s = wait(lambda s: s['state'] == 'lobby', 'lobby_' + name)
            results[name + '_views_left'] = 'PASS' if s['views'] == 0 else 'FAIL'
        report['flow'] = 'PASS'
    except Exception as e:
        report['error'] = str(e)[:1500]; report['error_call'] = report.get('last_call')
        try:   # 048: game state at the failure, to tell a game defect from a checker defect (core-rules 0)
            s = state(); report['state_at_error'] = {k: s.get(k) for k in ('state', 'wave', 'win', 'elapsed', 'teamHp', 'heroesAlive', 'enemies', 'selected', 'center')}
        except Exception as e2:
            report['state_at_error'] = 'UNAVAILABLE:' + str(e2)[:200]
    finally:
        attempt('touch_device', lambda: call('RemoveTouch', cls=TOUCH))
        if report.get('screen_resized'): attempt('game_view_size', lambda: call('Restore', cls=SCREEN))
        attempt('exit_play', lambda: call('ExitPlay'))
        attempt('wait_edit_mode', lambda: wait(lambda s: not s.get('playing'), 'exit_play', 60, retries=5) and 'EDIT_MODE')
        attempt('quality', lambda: call('RestoreQuality'))
        attempt('side_effect_dirty', lambda: call('ClearSideEffectDirty'))
        if report.get('run_in_background_override'):
            attempt('run_in_background', lambda: cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', 'false'], 60))
        attempt('scene_setup', lambda: call('Restore', retries=3))
        report['side_effect_files'] = protect.restore(side_snap)   # before the protected-file comparison
        after = fingerprints(PROTECTED); report['protected_changed'] = [p for p in PROTECTED if protected_before[p] != after[p]]
        try:
            before_seq = max([e['seq'] for e in report['console_errors_before'].get('entries', [])] or [0])
            report['console_new_errors'] = [dict(seq=e['seq'], message=e['message'][:300]) for e in console_errors().get('entries', []) if e['seq'] > before_seq]
        except Exception as e:
            report['console_new_errors'] = 'UNAVAILABLE:' + str(e)[:200]
        report['results'] = results
        ok = (report.get('flow') == 'PASS' and results and all(v == 'PASS' for v in results.values()) and not report.get('cleanup_errors')
              and not report['protected_changed'] and report['console_new_errors'] == [])
        report['status'] = 'PASS' if ok else 'FAIL'
        report['finished'] = datetime.now(ZoneInfo('Asia/Seoul')).isoformat(); report['seconds'] = round(time.time() - T0, 1)
        save(); TIER.record(SCENE, 'short', report['status'], RUN, captured=not NOCAPTURE)
        print(json.dumps(dict(status=report['status'], tier='short', results=results, error=report.get('error'), state_at_error=report.get('state_at_error'), cleanup_errors=report.get('cleanup_errors'),
                              protected_changed=report['protected_changed'], side_effect_files=report.get('side_effect_files'), console_new=report['console_new_errors'], out=str(OUT.relative_to(ROOT)),
                              seconds=report['seconds']), ensure_ascii=False)[:2500])


def main():
    if STAGE in STAGES_FULL:
        report['full_reason'] = TIER.require_full('keyart_045.py', SCENE)
    report['spawn_cause_selftest'] = _spawn_cause_selftest()
    if STAGE == 'short':
        return run_short()
    subprocess.run(['unity', 'status', '--until-ready', '--project-path', str(ROOT)], capture_output=True, timeout=300)
    report['sources'] = fingerprints(SOURCES)
    protected_before = fingerprints(PROTECTED); side_snap = protect.snapshot()
    try:
        report['console_errors_before'] = console_errors()
    except Exception as e:
        report['console_errors_before'] = {'error': str(e)[:200]}
    touch, results = dict(status='NOT_RUN'), {}
    try:
        report['open_attempted'] = True
        if STAGE == 'baseline044':
            call('OpenScene', 'Assets/DeadSurvivor/Scenes/Proto044/Proto044_Main.unity')
        else:
            call('OpenScene', SCENE)
        step('open_main', state=state())
        if STAGE != 'baseline044':
            lc = json.loads(call('LayoutChecks')); results['layout'] = lc['status']; step('layout_checks', **lc)
        report['run_in_background_before'] = evaluate('return UnityEditor.PlayerSettings.runInBackground.ToString();')
        cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', 'true'], 60); report['run_in_background_override'] = True
        call('EnterPlay')
        s = wait(lambda s: s.get('playing') and s.get('state') == 'lobby', 'enter_play', 60, retries=5)
        step('playing_lobby', state=s)
        if STAGE == 'purity':
            # 045-K1 re-check: deep state comparison with a negative control, during a live wave-1 battle
            select_only(4); call('Click', START)
            wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'p_battle'); time.sleep(3.0)
            outs = [call('RenderPurity') for _ in range(3)]
            results['purity'] = 'PASS' if all(o.startswith('PURE:') for o in outs) else 'FAIL'
            step('purity', outputs=outs, state=state())
            call('KillParty'); wait(lambda s: s['state'] == 'result', 'p_defeat')
            call('Click', REFORM); wait(lambda s: s['state'] == 'lobby', 'p_lobby')
            report['flow'] = 'PASS'
            return
        if STAGE == 'load':
            # load segment only (045): heroes 0-3, skip to wave 10, mobile pipeline
            select_only(4); call('Click', START)
            wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'l_battle_w1')
            skip_to_wave(10)
            results['load'] = load_segment('load')['status']
            call('KillParty'); wait(lambda s: s['state'] == 'result', 'l_defeat')
            call('Click', REFORM); s = wait(lambda s: s['state'] == 'lobby', 'l_lobby'); assert s['views'] == 0
            report['flow'] = 'PASS'
            return
        if STAGE == 'baseline044':
            # same load segment on the old Proto044 layout (sprites, no 3D views) for comparison
            lobby = select_only(4)
            call('Click', START)
            s = wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'b_battle_w1')
            bw, _ = natural_waves(lobby, s, captures=False)
            step('baseline_waves', waves=bw)
            cur = state()
            if cur['state'] == 'prep':
                call('Click', RESULT)
            wait(lambda s: s['state'] == 'result', 'b_victory')
            call('Click', REFORM); lobby = wait(lambda s: s['state'] == 'lobby', 'b_lobby1')
            select_only(4); call('Click', START)
            wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'b_battle_d1')
            skip_to_wave(10)
            results['load'] = load_segment('baseline044-load')['status']
            call('KillParty'); wait(lambda s: s['state'] == 'result', 'b_defeat')
            call('Click', REFORM); wait(lambda s: s['state'] == 'lobby', 'b_lobby')
            report['flow'] = 'PASS'
            return
        if STAGE != 'smoke':
            # A) 1-hero sortie (start slots) -> defeat -> reform; views cleared in lobby
            select_only(1)
            call('Click', START)
            s = wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'battle_1hero'); time.sleep(1.5); s = state()
            assert s['heroesAlive'] == 1 and views_ok(s), 'ONE_HERO_SORTIE:' + json.dumps({k: s.get(k) for k in ('heroesAlive', 'views', 'enemies', 'renderedFrame', 'mapError', 'selected')})
            if not NOCAPTURE:
                # 045 stage 2 budget (design "성능 예산"): start screen, 1280x720 Game view, PC quality + Mobile_RPAsset.
                # Measured right after the first sortie (few enemies, recorded); reported, not part of the overall verdict.
                report['screen_resized'] = True; call('Resize', 1280, 720, cls=SCREEN); time.sleep(1.0); samples = []
                for _ in range(5):
                    st = json.loads(call('Stats')); st['enemies'] = state()['enemies']; samples.append(st); time.sleep(0.2)
                dc = max(x['drawCalls'] for x in samples); tri = max(x['triangles'] for x in samples)
                results['perf_start'] = 'PASS' if (0 < dc <= 250 and 0 < tri <= 150000) else ('NOT_RUN' if dc == 0 else 'FAIL')
                step('perf_start', status=results['perf_start'], budget=dict(drawCalls=250, triangles=150000), maxDrawCalls=dc, maxTriangles=tri, samples=samples,
                     note='editor UnityStats at 1280x720, PC quality level + Mobile_RPAsset; not a device measurement')
            step('sortie_1hero', state=s)
            call('KillParty'); s = wait(lambda s: s['state'] == 'result', 'defeat_1hero'); assert not s['win']
            call('Click', REFORM)
            s = wait(lambda s: s['state'] == 'lobby', 'lobby_after_1hero')
            assert s['views'] == 0, 'VIEWS_LEFT_IN_LOBBY:' + str(s['views'])
            step('lobby_views_cleared', state=s)
        # B) heroes 0-3: start-screen captures, occlusion, purity, self-test, touch
        select_only(4)
        if STAGE != 'smoke':
            capture('lobby')
        call('Click', START)
        s = wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'battle_w1')
        f0 = s['frame']; time.sleep(1.5); s = state()
        assert s['elapsed'] > 0 and s['frame'] > f0, 'NO_SIMULATION'
        step('wave1_start', state=s)
        report['pipeline_play'] = call('MobilePipeline', False)
        capture('start-pc', [(1280, 720)], edges=False)
        report['pipeline_mobile'] = call('MobilePipeline', True); time.sleep(0.5)
        capture('start-mobile', [(1280, 720)], edges=False)
        report['pipeline_restored'] = call('MobilePipeline', False); time.sleep(0.3)
        if STAGE == 'smoke':
            report['flow'] = 'PASS'
            return
        # wave 1 lasts 30 s: do the static checks paused (World.Render still runs every frame), resume for touch
        call('Submit', 'Pause', 0); wait(lambda s: s['paused'], 'pause_b')
        capture('battle')
        keys_b = call('HeroViewKeys'); results['hero_keys_b'] = keys_b
        results['purity'] = 'PASS' if call('RenderPurity').startswith('PURE') else 'FAIL'
        st = call('ActorViewsSelfTest'); results['selftest'] = 'PASS' if st == 'PASS' else 'FAIL'; step('selftest', result=st)
        home = s['center']
        def occlusion(name, target):
            before = state()['faded']
            call('Teleport', float(target[0]), float(target[1])); time.sleep(0.6); s_occ = state()
            capture(name, [(1280, 720)], edges=False)
            call('Teleport', float(home[0]), float(home[1])); time.sleep(0.6); s_back = state()
            ok = s_occ['faded'] > 0 and s_back['faded'] <= before
            step(name, status='PASS' if ok else 'FAIL', target=target, faded_before=before, faded_behind=s_occ['faded'], faded_back=s_back['faded'],
                 note='Party teleported by test injection (visual check only).')
            return 'PASS' if ok else 'FAIL'
        border = None
        for py in (380, 384, 376, 388, 372):
            try:
                call('Teleport', float((280 - 565) * 4), float((py - 580) * 4)); border = ((280 - 565) * 4, (py - 580) * 4); break
            except Exception:
                continue
        results['occlusion'] = occlusion('occluded', border) if border else 'FAIL'
        if S2:
            to = call('TallOccluders'); report['tall_occluders'] = to
            ok_static = '|missing=0|' in to and to.endswith('|control=1')
            spot = call('TreeOcclusionSpot'); tree_ok = False
            if spot != 'NONE':
                name, tx, ty = spot.split('|')
                call('Teleport', float(tx), float(ty)); time.sleep(0.8); faded_in = call('FadedNames')
                call('Teleport', float(home[0]), float(home[1])); time.sleep(0.8); faded_out = call('FadedNames')
                tree_ok = name in faded_in.split(',') and name not in faded_out.split(',')
                step('tree_occlusion', target=spot, faded_behind=faded_in, faded_back=faded_out, status='PASS' if tree_ok else 'FAIL')
            results['s2_occluders'] = 'PASS' if ok_static and tree_ok else 'FAIL'
            step('s2_occluders', static=to, tree_spot=spot, status=results['s2_occluders'])
        om = call('OccluderMaterials'); report['occluder_materials'] = om
        results['occlusion_legacy'] = 'PASS' if '|nonDither=0|' in om else 'FAIL'
        step('occluder_materials', result=om, note='Static check: all occluders use dither-fade materials. Legacy prisms (<=0.64 high) cannot hide the focus point at 55 deg (run-781db9bd7e21: teleport behind one gave faded=0).')
        # touch (044 probe)
        call('Submit', 'Resume', 0); wait(lambda s: not s['paused'] and s['state'] == 'battle', 'resume_b')
        report['input_scope_begin'] = call('BeginInputScope', cls=TOUCH)
        rect_raw, tries = None, 0
        while rect_raw is None and tries < 6:
            tries += 1
            try:
                rect_raw = call('MovementRect', cls=TOUCH)
            except Exception as e:
                report.setdefault('movement_rect_retries', []).append(dict(error=str(e)[-80:], state=state()))
                time.sleep(0.5)
        if rect_raw is None:
            raise RuntimeError('NO_MOVEMENT_INPUT_AFTER_RETRIES')
        rect = json.loads(rect_raw.split('|')[0])
        x = rect['x'] + rect['width'] * 0.35; y = rect['y'] + rect['height'] * 0.5
        c0 = state()['center']
        call('Touch', 'began', float(x), float(y), cls=TOUCH); time.sleep(0.4)
        call('Touch', 'moved', float(x + 90), float(y), cls=TOUCH); time.sleep(0.4)
        s1 = state()
        models_moving = call('HeroViewModels') if S3 else None
        call('Touch', 'moved', float(x + 95), float(y), cls=TOUCH); time.sleep(1.2)
        s2 = state()
        call('Touch', 'ended', float(x + 95), float(y), cls=TOUCH); time.sleep(0.5)
        s3 = state(); time.sleep(0.8); s4 = state()
        if S3:
            # 045 stage 3: bruno is drawn by the new rigged model and actually animates (pose changes over time),
            # switches to Run while the party moves and back to Idle when it stops; the others keep the 042 model.
            models_a = call('HeroViewModels'); time.sleep(0.5); models_b = call('HeroViewModels')
            mv, ma, mb = parse_models(models_moving), parse_models(models_a), parse_models(models_b)
            batch = call('BatchMode') == '1'
            status3, checks3, pose_check = judge_s3(mv, ma, mb, batch)
            results['s3_hero'] = status3
            step('s3_hero', status=status3, checks=checks3, bone_pose=pose_check, batch_editor=batch, judge_selftest=S3_SELFTEST,
                 note='bone_pose NOT_RUN only in a batch-mode editor when no hero pose changes (culled animators); state progress is checked instead',
                 moving=models_moving, idle_a=models_a, idle_b=models_b)
        touch = dict(dx=s2['center'][0] - c0[0], drift=[s4['center'][0] - s3['center'][0], s4['center'][1] - s3['center'][1]], during=s1, released=s3)
        touch['status'] = 'PASS' if (s1['joystickActive'] and s1['joystick'][0] > 0.5 and touch['dx'] > 20 and not s3['joystickActive']
                                     and abs(touch['drift'][0]) < 2 and abs(touch['drift'][1]) < 2) else 'FAIL'
        report['touch_removed'] = call('RemoveTouch', cls=TOUCH)
        report['input_scope_end'] = call('EndInputScope', cls=TOUCH)
        step('touch', **touch)
        if S3B:
            # S3-23: after the touch check only ~9 s of the 30 s wave 1 are left (elapsed 17.8 / 21.3 in run-465bd14aee0c /
            # run-3633136a76d5) and move_sample with its retries (S3-20/S3-22) can take longer -> the wave ended mid-sample
            # (NOT_IN_BATTLE). Judge squad B in a fresh sortie, like squad C and the short tier.
            call('KillParty'); wait(lambda s: s['state'] == 'result', 'defeat_b_pre')
            call('Click', REFORM); lobby_b = wait(lambda s: s['state'] == 'lobby', 'lobby_b_pre')
            for i in list(lobby_b['selected']): call('Submit', 'ToggleSelection', i)
            for i in (0, 1, 2, 3): call('Submit', 'ToggleSelection', i)
            wait(lambda s: sorted(s['selected']) == [0, 1, 2, 3], 'select_b_judge')
            call('Click', START); wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'battle_b_judge'); time.sleep(2.0)
            report['before_s3b_b'] = {k: v for k, v in state().items() if k in ('state', 'wave', 'elapsed', 'heroesAlive', 'enemies', 'frame', 'win')}
            results['s3b_heroes'] = s3b_step('s3b_heroes', SQUAD_B)
        call('KillParty'); s = wait(lambda s: s['state'] == 'result', 'defeat_b'); assert not s['win']
        capture('result-defeat')
        call('Click', REFORM); s = wait(lambda s: s['state'] == 'lobby', 'lobby_b'); assert s['views'] == 0
        # C) different heroes (2-5): natural 10 waves, heroes protected (heal only, no injection), full-wave spawn stats
        lobby = state()
        for i in list(lobby['selected']):
            call('Submit', 'ToggleSelection', i)
        for i in (2, 3, 4, 5):
            call('Submit', 'ToggleSelection', i)
        lobby = wait(lambda s: sorted(s['selected']) == [2, 3, 4, 5], 'select_2345')
        call('Click', START)
        s = wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'battle_c1')
        keys_c = call('HeroViewKeys'); results['hero_keys_c'] = keys_c
        if S3B:
            # the second squad (lena, yuki, bolt, mira) gets the same moving + stopped judge so all six heroes are covered
            time.sleep(2.0)   # let the first frames of the battle pass before driving the squad
            results['s3b_heroes_c'] = s3b_step('s3b_heroes_c', SQUAD_C)
        results['hero_keys'] = 'PASS' if all(k.split('=')[1] == 'H:' + k.split('=')[0] for k in (keys_b + ',' + keys_c).split(',')) and set(keys_b.split(',')) != set(keys_c.split(',')) else 'FAIL'
        split_selftest = call('DriverSelfTest'); report['spawn_split_selftest'] = split_selftest
        waves, spawn_ok = natural_waves(lobby, s, captures=True)
        results['spawn'] = 'PASS' if spawn_ok else 'FAIL'
        # 045-J2 follow-up: the driver now splits each simulation's failures into geo / budget (the one path search per
        # simulation was used by a success) / path (the search itself failed). Still a per-simulation split: a geo-rejected
        # call before the search counts as budget. UNKNOWN when the split self-test fails, fields are missing or simulations merged.
        report['spawn_cause_detail'] = cause = spawn_cause(waves, split_selftest)
        results['spawn_cause'] = cause['label']
        step('waves', waves=waves, spawn=results['spawn'], spawn_cause=cause, note='Natural waves (no DebugFinish), heroes healed by the editor-only driver, no injection. Counts = enemyUid/MapSpawnFailures deltas over each whole wave.')
        if s['state'] != 'result':
            cur = state()
            if cur['state'] == 'prep':
                call('Click', RESULT)
        s = wait(lambda s: s['state'] == 'result', 'victory_result')
        assert s['win'], 'NOT_VICTORY'
        step('victory', state=s)
        capture('result-victory')
        call('Click', REFORM)
        s = wait(lambda s: s['state'] == 'lobby', 'lobby_after_victory'); assert s['views'] == 0, 'VIEWS_LEFT_AFTER_VICTORY'
        # D) re-sortie heroes 0-3, skip to wave 10, load segment on the mobile pipeline
        lobby = state()
        for i in list(lobby['selected']):
            call('Submit', 'ToggleSelection', i)
        select_only(4)
        call('Click', START)
        s = wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'battle_d1'); time.sleep(1.0); s = state()
        assert views_ok(s), 'RESORTIE_VIEWS:' + json.dumps(s)[:300]
        step('resortie', state=s)
        skip_to_wave(10)
        results['load'] = load_segment('load')['status']
        call('KillParty'); s = wait(lambda s: s['state'] == 'result', 'defeat_d'); assert not s['win']
        call('Click', REFORM); s = wait(lambda s: s['state'] == 'lobby', 'lobby_after_d'); assert s['views'] == 0 and s['mapError'] == ''
        step('reform_after_d', state=s)
        edges_ok = all(sh.get('edges', {}).get('status', 'PASS') == 'PASS' for st in steps if st['name'] == 'capture' for sh in st['shots'])
        results['edges'] = 'NOT_RUN' if NOCAPTURE else ('PASS' if edges_ok else 'FAIL')
        report['flow'] = 'PASS'
    except Exception as e:
        report['error'] = str(e)[:1500]; report['error_call'] = report.get('last_call')
    finally:
        attempt('driver', lambda: call('RemoveDriver'))
        attempt('pipeline', lambda: call('MobilePipeline', False))
        if report.get('input_scope_begin') and not report.get('input_scope_end'):
            attempt('input_scope', lambda: call('EndInputScope', cls=TOUCH))
        attempt('touch_device', lambda: call('RemoveTouch', cls=TOUCH))
        if report.get('screen_resized'):
            attempt('game_view_size', lambda: call('Restore', cls=SCREEN))
        if report.get('open_attempted'):
            attempt('exit_play', lambda: call('ExitPlay'))
            attempt('wait_edit_mode', lambda: wait(lambda s: not s.get('playing'), 'exit_play', 60, retries=5) and 'EDIT_MODE')
            attempt('quality', lambda: call('RestoreQuality'))
            attempt('side_effect_dirty', lambda: call('ClearSideEffectDirty'))
        if report.get('run_in_background_override'):
            attempt('run_in_background', lambda: cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', 'false'], 60) and
                    evaluate('return UnityEditor.PlayerSettings.runInBackground.ToString();'))
            if report.get('cleanup', {}).get('run_in_background') != report.get('run_in_background_before'):
                report.setdefault('cleanup_errors', {})['run_in_background_value'] = 'NOT_RESTORED_TO_' + str(report.get('run_in_background_before'))
        if report.get('open_attempted'):
            attempt('scene_setup', lambda: call('Restore', retries=3))
        report['side_effect_files'] = protect.restore(side_snap)
        protected_after = fingerprints(PROTECTED)
        report['protected_changed'] = [p for p in PROTECTED if protected_before[p] != protected_after[p]]
        report['sources_after_match'] = fingerprints(SOURCES) == report['sources']
        try:
            before = report['console_errors_before']
            before_seq = max([e['seq'] for e in before.get('entries', [])] or [0])
            after = console_errors().get('entries', [])
            report['console_new_errors'] = [dict(seq=e['seq'], message=e['message'][:300]) for e in after if e['seq'] > before_seq]
            console_ok = 'error' not in before
        except Exception as e:
            report['console_new_errors'] = 'UNAVAILABLE:' + str(e)[:200]; console_ok = False
        report['results'] = results; report['touch'] = touch.get('status')
        base_ok = report.get('flow') == 'PASS' and not report.get('cleanup_errors') and not report['protected_changed'] and report['sources_after_match']
        if STAGE in ('smoke', 'load'):
            parts_ok = base_ok
        elif STAGE == 'purity':
            parts_ok = base_ok and results.get('purity') == 'PASS'
        else:
            need = ['layout', 'occlusion', 'occlusion_legacy', 'spawn', 'selftest', 'purity', 'hero_keys'] + ([] if NOCAPTURE else ['edges']) + (['s2_occluders'] if S2 else []) + (['s3_hero'] if S3 else []) + (['s3b_heroes', 's3b_heroes_c'] if S3B else [])
            parts_ok = base_ok and (STAGE == 'baseline044' or (touch.get('status') == 'PASS' and all(results.get(k) == 'PASS' for k in need)))
        if not parts_ok:
            report['status'] = 'FAIL'
        elif STAGE in ('full', 'load') and results.get('load') != 'PASS':
            report['status'] = results.get('load', 'FAIL')
        elif not console_ok or not isinstance(report.get('console_new_errors'), list):
            report['status'] = 'BLOCKED'
        else:
            report['status'] = 'PASS' if not report['console_new_errors'] else 'FAIL'
        report['finished'] = datetime.now(ZoneInfo('Asia/Seoul')).isoformat(); report['seconds'] = round(time.time() - T0, 1)
        save()
        if STAGE == 'full':
            TIER.record(SCENE, 'full', report['status'], RUN, note=report.get('full_reason', ''), captured=not NOCAPTURE)
        print(json.dumps(dict(status=report['status'], results=results, touch=touch.get('status'), error=report.get('error'), cleanup_errors=report.get('cleanup_errors'),
                              protected_changed=report['protected_changed'], console_new=report.get('console_new_errors'), out=str(OUT.relative_to(ROOT)), seconds=report['seconds']), ensure_ascii=False)[:3000])


if __name__ == '__main__':
    main()

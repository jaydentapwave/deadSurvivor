#!/usr/bin/env python3
"""044: drive the saved 3D-map main scene (Proto044_Main) in Play mode through the real UI and a
virtual Touchscreen, check three real Game View resolutions, and record evidence. Every cleanup step
(input scope, touch device, Game View size, Play mode, run-in-background, scene setup) is attempted
independently. PROTO044_FAIL_AFTER=<step> forces a failure after that step to prove cleanup.
Usage: /usr/bin/python3 Tools/Proto/proto_main_044.py
"""
import glob, hashlib, json, os, shutil, struct, subprocess, sys, time, uuid
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

ROOT = Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor')
PROBE = 'DeadSurvivor.Editor.DSProtoMain044Verification'
SCREEN = 'DeadSurvivor.Editor.DSScreenVerification'
SIZES = [(1280, 720), (1560, 720), (1024, 768)]
START, NEXT, RESULT, REFORM = '스쿼드 출전 →', '다음 웨이브 →', '결과 확인 →', '스쿼드 재편성 →'
SOURCES = ['Tools/Proto/proto_main_044.py', 'Assets/DeadSurvivor/Editor/DSProtoMain044Verification.cs',
           'Assets/DeadSurvivor/Editor/DSScreenVerification.cs', 'Assets/DeadSurvivor/Runtime/DSUI.cs',
           'Assets/DeadSurvivor/Scenes/Proto044/Proto044_Main.unity', 'Assets/DeadSurvivor/Scenes/Proto044/Proto044_Main.unity.meta',
           'ProjectSettings/EditorBuildSettings.asset']
PROTECTED = SOURCES + ['ProjectSettings/ProjectSettings.asset', 'Assets/DeadSurvivor/Resources/DeadSurvivor/UI/PrototypeFont.asset',
                       'Assets/DeadSurvivor/Scenes/DeadSurvivor.unity', 'Assets/DeadSurvivor/Scenes/Map038_TripoPair_r003/Map038_TripoPair.unity',
                       'Assets/InputSystem_Actions.inputactions', 'ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json'] + sorted(
    str(Path(p).relative_to(ROOT)) for p in glob.glob(str(ROOT / 'Assets/DeadSurvivor/Scenes/Map037_ReferenceDetail_r002/**/Foliage*.mat'), recursive=True))
FAIL_AFTER = os.environ.get('PROTO044_FAIL_AFTER', '')
SIMULATE = set(x for x in os.environ.get('PROTO044_SIMULATE', '').split(',') if x)  # resize_fail, console_before_fail
# 045 stage 2/3: a batch-mode editor has no Game view, so screen resize/check/capture cannot run. With PROTO044_NOCAPTURE=1
# captures are skipped and recorded NOT_RUN (edge coverage NOT_RUN); flow, touch, console and protected files are still judged.
NOCAPTURE = os.environ.get('PROTO044_NOCAPTURE') == '1'

RUN = uuid.uuid4().hex[:12]
OUT = ROOT / 'output/proto-main-044/r001' / ('run-' + RUN)
OUT.mkdir(parents=True)
T0 = time.time()
steps = []
report = dict(status='RUNNING', run=RUN, nocapture=NOCAPTURE, forced_failure=FAIL_AFTER or None, simulate=sorted(SIMULATE) or None, started=datetime.now(ZoneInfo('Asia/Seoul')).isoformat(), steps=steps)


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
                raise RuntimeError('EVAL_FAIL:' + json.dumps(res, ensure_ascii=False)[:400])
            return res['result']
        except Exception:
            if attempt == retries:
                raise
            time.sleep(1.0)


def call(method, *args, retries=0, cls=PROBE):
    return evaluate(f'return {cls}.{method}({",".join(lit(a) for a in args)});', retries)


def state(retries=0):
    return json.loads(call('State', retries=retries))


def step(name, **kw):
    steps.append(dict(name=name, t=round(time.time() - T0, 2), **kw))
    save()
    if FAIL_AFTER == name:
        raise RuntimeError('FORCED_FAILURE_AFTER:' + name)


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
    # Non-battle pages must cover the whole screen: outer border pixels must be #0d1317 (13,19,23).
    sys.path.append('/Users/hyeongwoo/Library/Python/3.9/lib/python/site-packages')
    from PIL import Image
    im = Image.open(path).convert('RGB'); w, h = im.size
    pts = [(x, y) for x in range(0, w, max(1, w // 64)) for y in (1, h - 2)] + [(x, y) for y in range(0, h, max(1, h // 36)) for x in (1, w - 2)]
    bad = [p for p in pts if max(abs(a - b) for a, b in zip(im.getpixel(p), (13, 19, 23))) > 8]
    return dict(samples=len(pts), mismatched=len(bad), first=bad[:3], status='PASS' if not bad else 'FAIL')


def capture(label):
    """Resize the real Game View, wait for the layout, check it (DSScreenVerification.Check), then capture 1:1."""
    if NOCAPTURE:
        step('capture', label=label, shots=[], note='NOT_RUN: batch-mode editor has no Game view (PROTO044_NOCAPTURE=1)')
        return
    shots = []
    for w, h in SIZES:
        report['screen_resized'] = True  # set before the call: a partial Resize still needs Restore
        call('Resize', w, h, cls=SCREEN)
        if 'resize_fail' in SIMULATE:
            raise RuntimeError('SIMULATED_RESIZE_FAILURE')
        check = None
        for _ in range(20):
            time.sleep(0.25)
            try:
                check = call('Check', w, h, cls=SCREEN); break
            except Exception as e:
                check = 'PENDING:' + str(e)[:160]
        if not isinstance(check, str) or check.startswith('PENDING'):
            raise RuntimeError(f'SCREEN_CHECK_FAILED:{label}:{w}x{h}:{check}')
        rel = f'output/proto044/{RUN}/{label}-{w}x{h}.png'
        if (ROOT / 'Assets' / rel).exists():
            raise RuntimeError('CAPTURE_EXISTS')
        cli(['command', 'capture', 'game_view', '--', '--source', 'screen', '--width', str(w), '--height', str(h), '--save_path', rel], 120)
        dst = OUT / f'{label}-{w}x{h}.png'
        shutil.copy2(ROOT / 'Assets' / rel, dst)
        shot = dict(file=dst.name, size=png_size(dst), sha256=hashlib.sha256(dst.read_bytes()).hexdigest(), screen_check=check)
        if label != 'battle':
            shot['edges'] = edge_check(dst)
        shots.append(shot)
    step('capture', label=label, shots=shots)


def console_errors():
    return cli(['command', 'console', 'get', '--', '--tail', '50', '--level', 'error'], 60)['result']


def verdict(report, touch_status, console_ok):
    """PASS only when everything passed AND the console comparison was possible; otherwise BLOCKED/FAIL."""
    rest_ok = (report.get('flow') == 'PASS' and touch_status == 'PASS' and report.get('edge_coverage') == ('NOT_RUN' if NOCAPTURE else 'PASS')
               and not report.get('cleanup_errors') and not report.get('protected_changed') and report.get('sources_after_match'))
    if not rest_ok:
        return 'FAIL'
    if not console_ok or not isinstance(report.get('console_new_errors'), list):
        return 'BLOCKED'
    return 'PASS' if not report['console_new_errors'] else 'FAIL'


def attempt(name, fn):
    try:
        report.setdefault('cleanup', {})[name] = fn()
        return True
    except Exception as e:
        report.setdefault('cleanup_errors', {})[name] = str(e)[:300]
        return False


def main():
    subprocess.run(['unity', 'status', '--until-ready', '--project-path', str(ROOT)], capture_output=True, timeout=300)
    report['sources'] = fingerprints(SOURCES)
    protected_before = fingerprints(PROTECTED)
    try:
        if 'console_before_fail' in SIMULATE:
            raise RuntimeError('SIMULATED_CONSOLE_QUERY_FAILURE')
        report['console_errors_before'] = console_errors()
    except Exception as e:
        report['console_errors_before'] = {'error': str(e)[:200]}
    touch = dict(status='NOT_RUN')
    try:
        report['open_attempted'] = True
        call('OpenMain')
        step('open_main', state=state())
        report['run_in_background_before'] = evaluate('return UnityEditor.PlayerSettings.runInBackground.ToString();')
        cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', 'true'], 60); report['run_in_background_override'] = True
        call('EnterPlay')
        s = wait(lambda s: s.get('playing') and s.get('state') == 'lobby', 'enter_play', 60, retries=5)
        step('playing_lobby', state=s)
        if s['selectedCount'] == 0:
            for i in range(4):
                call('Submit', 'ToggleSelection', i)
            s = wait(lambda s: s['selectedCount'] == 4, 'select4')
        step('selected', selected=s['selected'])
        capture('lobby')

        call('Click', START)
        s = wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'battle_w1')
        f0 = s['frame']; time.sleep(4.0); s = state()
        assert s['elapsed'] > 0 and s['frame'] > f0, 'NO_SIMULATION'
        step('wave1_live', state=s, frames=s['frame'] - f0)
        capture('battle')

        report['input_scope_begin'] = call('BeginInputScope')
        rect_raw = call('MovementRect'); rect = json.loads(rect_raw.split('|')[0])
        x = rect['x'] + rect['width'] * 0.35; y = rect['y'] + rect['height'] * 0.5
        c0 = state()['center']
        call('Touch', 'began', float(x), float(y)); time.sleep(0.4)
        call('Touch', 'moved', float(x + 90), float(y)); time.sleep(0.4)
        s1 = state()
        call('Touch', 'moved', float(x + 95), float(y)); time.sleep(1.2)
        s2 = state()
        call('Touch', 'ended', float(x + 95), float(y)); time.sleep(0.5)
        s3 = state(); time.sleep(0.8); s4 = state()
        if FAIL_AFTER == 'touch_inside':  # fail while the input scope and touch device are still active
            raise RuntimeError('FORCED_FAILURE_INSIDE_INPUT_SCOPE')
        touch = dict(rect=rect_raw, point=[x, y], center_before=c0, during=s1, held=s2, released=s3, after=s4,
                     dx=s2['center'][0] - c0[0], dy=s2['center'][1] - c0[1],
                     drift_after_release=[s4['center'][0] - s3['center'][0], s4['center'][1] - s3['center'][1]])
        touch['status'] = 'PASS' if (s1['joystickActive'] and s1['joystick'][0] > 0.5 and touch['dx'] > 20 and not s3['joystickActive']
                                     and abs(touch['drift_after_release'][0]) < 2 and abs(touch['drift_after_release'][1]) < 2) else 'FAIL'
        report['touch_removed'] = call('RemoveTouch')
        report['input_scope_end'] = call('EndInputScope')
        step('touch', **touch)

        waves = []
        for w in range(1, 11):
            if w > 1:
                call('Click', NEXT)
                s = wait(lambda s, w=w: s['state'] == 'battle' and s['wave'] == w, f'battle_w{w}')
                time.sleep(2.0); s = state()
            waves.append(dict(wave=w, elapsed=s['elapsed'], spawned=s['spawned'], enemies=s['enemies'], kills=s['kills'], hp=s['teamHp']))
            call('Submit', 'DebugFinish', 0)
            s = wait(lambda s: s['state'] in ('prep', 'result'), f'finish_w{w}')
            if w == 1:
                capture('prep')
        step('waves', waves=waves, note='Each wave simulated ~2-5 s of real frames; the rest skipped with the DebugFinish queue command.')
        if s['state'] == 'prep':
            call('Click', RESULT)
            s = wait(lambda s: s['state'] == 'result', 'victory_result')
        assert s['win'], 'NOT_VICTORY'
        step('victory', state=s)
        capture('result-victory')
        call('Click', REFORM)
        s = wait(lambda s: s['state'] == 'lobby', 'lobby_after_victory'); step('reform_after_victory', state=s)
        call('Click', START)
        s = wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'restart_battle'); time.sleep(1.5)
        step('restart', state=state())
        call('KillParty')
        s = wait(lambda s: s['state'] == 'result', 'defeat_result')
        assert not s['win'], 'NOT_DEFEAT'
        step('defeat', state=s, note='Party HP set to 0 by test injection; the real frames judged the wipe.')
        capture('result-defeat')
        call('Click', REFORM)
        s = wait(lambda s: s['state'] == 'lobby', 'lobby_after_defeat'); step('reform_after_defeat', state=s)
        assert s['mapError'] == '', 'MAP_ERROR:' + s['mapError']
        edges_ok = all(sh.get('edges', {}).get('status', 'PASS') == 'PASS' for st in steps if st['name'] == 'capture' for sh in st['shots'])
        report['edge_coverage'] = 'NOT_RUN' if NOCAPTURE else ('PASS' if edges_ok else 'FAIL')
        report['flow'] = 'PASS'
    except Exception as e:
        report['error'] = str(e)[:1500]
    finally:
        # Each cleanup is independent; a failure in one does not skip the others.
        if report.get('input_scope_begin') and not report.get('input_scope_end'):
            attempt('input_scope', lambda: call('EndInputScope'))
        attempt('touch_device', lambda: call('RemoveTouch'))
        if report.get('screen_resized'):
            attempt('game_view_size', lambda: call('Restore', cls=SCREEN))
        if report.get('open_attempted'):
            attempt('exit_play', lambda: call('ExitPlay'))
            attempt('wait_edit_mode', lambda: wait(lambda s: not s.get('playing'), 'exit_play', 60, retries=5) and 'EDIT_MODE')
        if report.get('run_in_background_override'):
            # '--enable false' ends the CLI override and returns the project's own value (it does not force false);
            # '--enable true' would leave the override recorded in ProjectSettings/Packages/com.unity.pipeline.
            attempt('run_in_background', lambda: cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', 'false'], 60) and
                    evaluate('return UnityEditor.PlayerSettings.runInBackground.ToString();'))
            if report.get('cleanup', {}).get('run_in_background') != report.get('run_in_background_before'):
                report.setdefault('cleanup_errors', {})['run_in_background_value'] = 'NOT_RESTORED_TO_' + str(report.get('run_in_background_before'))
        if report.get('open_attempted'):
            attempt('scene_setup', lambda: call('Restore', retries=3))
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
        report['status'] = verdict(report, touch.get('status'), console_ok)
        report['finished'] = datetime.now(ZoneInfo('Asia/Seoul')).isoformat(); report['seconds'] = round(time.time() - T0, 1)
        save()
        print(json.dumps(dict(status=report['status'], screens='NOT_RUN' if NOCAPTURE else 'RUN', error=report.get('error'), cleanup_errors=report.get('cleanup_errors'),
                              protected_changed=report['protected_changed'], out=str(OUT.relative_to(ROOT)), seconds=report['seconds']), ensure_ascii=False))


if __name__ == '__main__':
    main()

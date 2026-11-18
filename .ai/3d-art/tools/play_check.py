#!/usr/bin/env python3
"""3dkit H2: the one way to run the game for checking (rule 1). Project values: 3dkit.config.json harness.play
  scene, probe_file, probe_dir, short_pass, require_capture, full_entry, full_result, full_pass, rebuild_call,
  perf{probe_file, stand, swing, budget}
and harness.evidence{runtime, visual} (paths for the fingerprints).

  play_check.py short <entry> <result file> [--keep] [--probe-file F --probe-dir D] [--pass REGEX]
        Editor check -> lock -> Play -> probe entry -> wait for the result file -> console -> stop. Exit 0 only if frames advanced,
        the result is not empty and has no ERROR/PLAY_ENDED line, a line matches short_pass (when set), the probe saved at
        least one new image (unless require_capture is false; rule 1 keeps one capture) and the console has no error (B45).
        Writes short Play evidence (fingerprint, captures) on PASS. --pass replaces short_pass for another probe ('' = none).
  FULL_REASON="<why>" play_check.py full       one full run; refuses without a reason; appends full-log.txt
  play_check.py perf [out.json] [--size 1920x1080]    one game view at that size, 20 s medians standing and swinging. WHATIF="cascades,distance"
        changes the shadow settings in memory only. Refuses to report a run that did not render at 1920x1080 unless --size is given.
  play_check.py rebuild      refresh, wait for compile, rebuild the scene, console errors
  play_check.py selftest     offline, fake unity on PATH, temp project
Exit: 0 pass, 1 fail, 2 refused/usage, 4 editor not ready (nothing was touched), 5 editor locked by another run.
Nothing is deleted before the Editor is confirmed ready (W13, W22)."""
import json
import os
import re
import shutil
import statistics
import subprocess
import sys
import tempfile
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from kitlib import config as C, editor_lock as L, evidence as E  # noqa: E402

SCALE = float(os.environ.get('PLAY_CHECK_TIME_SCALE', '1'))
PLAY_WAIT, RESULT_WAIT, SECONDS, RELOAD_WAIT = 90, 900, 20, 120


def nap(s):
    time.sleep(s * SCALE)


class Fail(Exception):
    def __init__(self, code, msg):
        super().__init__(msg)
        self.code = code


class Editor:
    def __init__(self, root):
        self.root = str(root)
        self.patient = False   # during a run the Editor drops off for a moment at every domain reload (entering Play): wait for it

    def _call(self, args, timeout):
        waited = 0
        while True:
            p = subprocess.run(['unity', 'command', *args], capture_output=True, text=True, timeout=timeout + 30, cwd=self.root)
            if not (self.patient and ('EDITOR_NOT_RUNNING' in p.stdout or not p.stdout.strip())) or waited >= RELOAD_WAIT:
                return p
            nap(1)
            waited += 1

    def ev(self, code, timeout=120):
        p = self._call(['script', 'eval', '--project-path', self.root, '--timeout', str(timeout), '--result-only', '--', code], timeout)
        try:
            d = json.loads(p.stdout)
        except ValueError:
            raise Fail(1, f'EVAL_NO_OUTPUT {code[:60]}: {(p.stdout + p.stderr)[-200:]}')
        if not d.get('success'):
            raise Fail(1, f'EVAL_FAILED {code[:60]}: {json.dumps(d, ensure_ascii=False)[:300]}')
        return d.get('result')

    def cmd(self, *a, timeout=60):
        return self._call(['--project-path', self.root, '--result-only', *a], timeout).stdout

    def run(self, file, entry, timeout=60):
        p = self._call(['script', 'run', '--project-path', self.root, '--timeout', str(timeout), '--result-only', '--file', str(file), '--entry', entry], timeout)
        try:
            d = json.loads(p.stdout)
        except ValueError:
            raise Fail(1, f'PROBE_NO_OUTPUT {entry}: {(p.stdout + p.stderr)[-200:]}')
        if not d.get('success'):
            raise Fail(1, f'PROBE_FAILED {entry}: {json.dumps(d, ensure_ascii=False)[:400]}')
        return d.get('result')

    def status(self):
        try:
            return json.loads(self.cmd('editor', 'status'))
        except (ValueError, subprocess.TimeoutExpired):
            return None

    def playing(self):
        return self.ev('return UnityEditor.EditorApplication.isPlaying.ToString();') == 'True'

    def console(self):
        try:
            return json.loads(self.cmd('console', 'status'))
        except ValueError:
            raise Fail(1, 'CONSOLE_STATUS_UNREADABLE')

    def first_error(self):
        """the newest console error, so a CONSOLE_ERRORS failure says what it was (game or Editor environment)"""
        try:
            e = (json.loads(self.cmd('console', 'get', '--level', 'error', '--tail', '1')).get('entries') or [{}])[-1]
            return (e.get('message') or '').strip().splitlines()[0][:220] if e else ''
        except (ValueError, IndexError, AttributeError):
            return ''


def console_fail(ed, n):
    msg = ed.first_error()
    raise Fail(1, f'CONSOLE_ERRORS {n}' + (f': {msg}' if msg else '') +
               ' -> next: a game error -> fix it; an Editor environment error (audio, licence, another window) -> report it, clear the console and run once more')


def config():
    cfg_path = C.find_config()
    root = Path(cfg_path).resolve().parent
    data = json.loads(Path(cfg_path).read_text())
    h = data.get('harness') or {}
    return root, h.get('play') or {}, h.get('evidence') or {}


def ready(ed, scene=None):
    """Exit 4 before anything is changed if this Editor cannot run a check."""
    st = ed.status()
    if not st or st.get('status') != 'ready' or st.get('compiling') or st.get('domainReloadInProgress') or st.get('playMode') != 'stopped':
        raise Fail(4, f'EDITOR_NOT_READY {json.dumps(st, ensure_ascii=False)[:200] if st else "no answer: open the Editor first"}')
    if scene:
        cur = ed.ev('var s=UnityEngine.SceneManagement.SceneManager.GetActiveScene();return s.path+"|"+s.isDirty;')
        if cur != f'{scene}|False':
            if cur.endswith('|True'):
                raise Fail(4, f'DIRTY_SCENE_OPEN {cur}')
            ed.ev(f'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("{scene}");return "ok";')


class background:
    """runInBackground on for the run, back to its old value whatever happens (W5)"""

    def __init__(self, ed):
        self.ed = ed

    def __enter__(self):
        self.old = self.ed.ev('return UnityEditor.PlayerSettings.runInBackground.ToString();')
        self.ed.ev('UnityEditor.PlayerSettings.runInBackground=true;return "ok";')

    def __exit__(self, exc_type, *exc):
        try:
            got = self.ed.ev(f'UnityEditor.PlayerSettings.runInBackground={self.old.lower()};return UnityEditor.PlayerSettings.runInBackground.ToString();')
            if got != self.old:
                raise Fail(1, f'RUN_IN_BACKGROUND_NOT_RESTORED {got} (was {self.old})')
        except Fail as e:
            if exc_type is None:
                raise
            print(f'PLAY_CHECK cleanup: {e}', file=sys.stderr)   # keep the first error, it is the cause


def leave_play(ed, failing):
    """always try to stop Play. A failure here is reported, but does not hide the error that brought us here"""
    try:
        exit_play(ed)
    except Fail as e:
        if not failing:
            raise
        print(f'PLAY_CHECK cleanup: {e}', file=sys.stderr)


def enter_play(ed):
    ed.patient = True
    ed.cmd('console', 'clear')
    ed.cmd('editor', 'focus')
    ed.ev('UnityEditor.EditorApplication.EnterPlaymode();return "ok";')
    for _ in range(PLAY_WAIT):
        if ed.playing():
            nap(3)
            return
        nap(1)
    raise Fail(1, 'PLAY_DID_NOT_START')


def exit_play(ed):
    ed.ev('UnityEditor.EditorApplication.ExitPlaymode();return "ok";')
    for _ in range(PLAY_WAIT):
        if not ed.playing():
            return
        nap(1)
    raise Fail(1, 'PLAY_DID_NOT_STOP')


def frame(ed):
    return int(ed.ev('return UnityEngine.Time.frameCount.ToString();'))


def one_run(ed, play, entry, result, keep, probe_file, probe_dir):
    """Returns (result text, console counts). Raises Fail on a broken run."""
    out = Path(probe_dir) / 'probe'
    with background(ed):
        if not keep:
            shutil.rmtree(out, ignore_errors=True)   # only now: the Editor answered and the lock is ours (W13, W22)
        out.mkdir(parents=True, exist_ok=True)
        (out / result).unlink(missing_ok=True)
        failing = True
        try:
            enter_play(ed)
            start = frame(ed)
            print('start:', ed.run(probe_file, entry))
            text = None
            for _ in range(RESULT_WAIT):
                if (out / result).exists():
                    text = (out / result).read_text()
                    break
                if not ed.playing():
                    raise Fail(1, 'PLAY_ENDED_BEFORE_RESULT')
                nap(1)
            if text is None:
                raise Fail(1, f'NO_RESULT_FILE {result} after {RESULT_WAIT}s')
            if frame(ed) <= start:     # B28: a stopped game still answers every query
                raise Fail(1, f'FRAMES_DID_NOT_ADVANCE at {start}')
            ed.run(probe_file, entry.split('.')[0] + '.Stop')   # every probe class has Stop (it clears the capture time step)
            counts = ed.console()['counts']
            failing = False
        finally:
            leave_play(ed, failing)
    return text, counts


def bad_lines(text):
    return [l for l in text.splitlines() if l.startswith(('ERROR', 'PLAY_ENDED', 'TIMEOUT'))]


def short(args):
    root, play, ev_cfg = config()
    keep = '--keep' in args
    args = [a for a in args if a != '--keep']
    probe_file, probe_dir = play.get('probe_file'), play.get('probe_dir')
    pat = play.get('short_pass')
    for flag in ('--probe-file', '--probe-dir', '--pass'):
        if flag in args:
            i = args.index(flag)
            if i + 1 >= len(args):
                raise Fail(2, f'USAGE: {flag} needs a value')
            if flag == '--probe-file':
                probe_file = args[i + 1]
            elif flag == '--probe-dir':
                probe_dir = args[i + 1]
            else:
                pat = args[i + 1]
            del args[i:i + 2]
    if len(args) != 2 or not probe_file or not probe_dir:
        raise Fail(2, 'USAGE: short <entry> <result file> [--keep] [--probe-file F --probe-dir D], or set harness.play')
    if subprocess.run(['git', 'check-ignore', '-q', probe_file], cwd=root, capture_output=True).returncode == 0:
        print(f'PLAY_CHECK note: PROBE_NOT_TRACKED {probe_file} is ignored by Git and can vanish; keep the probe in a tracked folder', file=sys.stderr)
    ed = Editor(root)
    ready(ed, play.get('scene'))
    t0 = time.time()
    with L.EditorLock(root, 'play_check short ' + args[0]):
        text, counts = one_run(ed, play, args[0], args[1], keep, root / probe_file, root / probe_dir)
    print(text.rstrip())
    print('console', counts)
    if not text.strip():
        raise Fail(1, 'RESULT_EMPTY: the probe wrote an empty result (no sign that it ran) -> next: make the probe entry write its lines')
    bad = bad_lines(text)
    if bad:
        raise Fail(1, 'RESULT_BAD: ' + bad[0][:200])
    if counts.get('error'):
        console_fail(ed, counts['error'])
    if pat and not re.search(pat, text, re.M):
        raise Fail(1, f'SHORT_PASS_NOT_MET: no result line matches harness.play.short_pass {pat!r}')
    shots = captures(root / probe_dir / 'probe', t0)
    if play.get('require_capture', True) and not shots:
        raise Fail(1, f'NO_CAPTURE: the probe saved no new image in {probe_dir}/probe (rule 1: a short Play keeps one capture) '
                      '-> next: make the probe entry save a game-camera image, or set harness.play.require_capture false for a probe that cannot')
    fp = E.fingerprint(root, ev_cfg.get('runtime', []) + ev_cfg.get('visual', [])) if ev_cfg else None
    E.write(root, 'short', fingerprint=fp, entry=args[0], result=str(Path(probe_dir) / 'probe' / args[1]),
            lines=len(text.splitlines()), captures=[str(x.relative_to(root)) for x in shots][:20])
    print('SHORT PASS', fp, f'{len(shots)} capture(s)')


def captures(folder, since):
    """images the probe saved during this run (older files, e.g. kept with --keep, do not count)"""
    if not Path(folder).is_dir():
        return []
    return sorted(p for p in Path(folder).rglob('*') if p.suffix.lower() in ('.png', '.jpg', '.jpeg') and p.stat().st_mtime >= since)


def full(_args):
    root, play, ev_cfg = config()
    reason = os.environ.get('FULL_REASON', '').strip()
    if not reason:
        raise Fail(2, 'FULL_REASON_REQUIRED')
    if not play.get('full_entry'):
        raise Fail(2, 'NO_FULL_ENTRY in harness.play')
    ed = Editor(root)
    ready(ed, play.get('scene'))
    with L.EditorLock(root, 'play_check full'):
        text, counts = one_run(ed, play, play['full_entry'], play['full_result'], False, root / play['probe_file'], root / play['probe_dir'])
    print(text.rstrip())
    print('console', counts)
    run = E.fingerprint(root, ev_cfg.get('runtime', [])) if ev_cfg else None
    vis = E.fingerprint(root, ev_cfg.get('visual', [])) if ev_cfg else None
    ok = bool(re.search(play.get('full_pass', r'^fails=none'), text, re.M)) and not counts.get('error') and not bad_lines(text)
    head = subprocess.run(['git', 'rev-parse', '--short', 'HEAD'], cwd=root, capture_output=True, text=True).stdout.strip()
    line = f"{time.strftime('%F %T')} {'PASS' if ok else 'FAIL'} head={head} runtime={run} visual={vis} reason={reason}"
    with (root / play['probe_dir'] / 'full-log.txt').open('a') as f:
        f.write(line + '\n')
    print(line)
    if not ok:
        raise Fail(1, 'FULL_FAIL')
    all_fp = E.fingerprint(root, ev_cfg.get('runtime', []) + ev_cfg.get('visual', [])) if ev_cfg else None
    E.write(root, 'short', fingerprint=all_fp, entry=play['full_entry'], result='full run')
    E.write(root, 'full', runtime=run, visual=vis, reason=reason)


def rebuild(_args):
    root, play, _ = config()
    if not play.get('rebuild_call'):
        raise Fail(2, 'NO_REBUILD_CALL in harness.play')
    ed = Editor(root)
    st = ed.status()
    if not st or st.get('playMode') != 'stopped':
        raise Fail(4, f'EDITOR_NOT_READY {st}')
    with L.EditorLock(root, 'play_check rebuild'):
        ed.ev('UnityEditor.AssetDatabase.Refresh();return "ok";')
        for _ in range(120):
            nap(2)
            st = ed.status()
            if st and st.get('status') == 'ready' and not st.get('compiling') and not st.get('domainReloadInProgress'):
                break
        else:
            raise Fail(1, f'COMPILE_DID_NOT_FINISH {st}')
        ed.cmd('console', 'clear')
        print('rebuild:', ed.ev(play['rebuild_call'], timeout=300))
        c = ed.console()
    print('console', c['counts'])
    if (c.get('groundTruth') or {}).get('compilationFailed'):
        raise Fail(1, 'COMPILE_FAILED')
    if c['counts'].get('error'):
        raise Fail(1, f"CONSOLE_ERRORS {c['counts']['error']}")


# ---------------- perf (3d-performance.md 2절) ----------------
STATS = ('var st=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.UnityStats");'
         'string V(string n){var pr=st.GetProperty(n,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);return System.Convert.ToString(pr.GetValue(null),System.Globalization.CultureInfo.InvariantCulture);}'
         'return "{"+string.Join(",",new[]{"drawCalls","setPassCalls","triangles","shadowCasters","frameTime","renderTime"}.Select(n=>"\\""+n+"\\":"+V(n)))+"}";')
KEYS = ('drawCalls', 'setPassCalls', 'triangles', 'shadowCasters', 'renderTime', 'frameTime')
GV = ('var t=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");'
      'var f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;'
      'var all=UnityEngine.Resources.FindObjectsOfTypeAll(t).Cast<UnityEditor.EditorWindow>().OrderBy(w=>w.GetInstanceID()).ToArray();'
      'object Host(UnityEditor.EditorWindow w)=>typeof(UnityEditor.EditorWindow).GetField("m_Parent",f).GetValue(w);')


def view_on(ed, size):
    """first Game view at that size; the other Game views hidden behind a tab (rule: one view draws), not closed"""
    return ed.ev(GV + 'var keep=all[0];var old=(int)t.GetProperty("selectedSizeIndex",f).GetValue(keep);'
                 'var gs=t.Assembly.GetType("UnityEditor.GameViewSizes");var si=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(gs).GetProperty("instance").GetValue(null);'
                 f'var grp=gs.GetProperty("currentGroup").GetValue(si);var names=(string[])grp.GetType().GetMethod("GetDisplayTexts").Invoke(grp,null);var idx=System.Array.FindIndex(names,n=>n.Contains("{size}"));'
                 'if(idx<0){throw new System.Exception("NO_SIZE");}'
                 't.GetMethod("SizeSelectionCallback",f,null,new[]{typeof(int),typeof(object)},null).Invoke(keep,new object[]{idx,null});'
                 'var hidden=new System.Collections.Generic.List<string>();foreach(var w in all.Skip(1)){var h=Host(w);var ht=h.GetType();var panes=(System.Collections.IList)ht.GetField("m_Panes",f).GetValue(h);var cur=(int)ht.GetProperty("selected",f).GetValue(h);var me=panes.IndexOf(w);if(panes.Count<2){throw new System.Exception("ONLY_TAB");}'
                 'var other=Enumerable.Range(0,panes.Count).First(i=>i!=me);ht.GetProperty("selected",f).SetValue(h,other);hidden.Add(w.GetInstanceID()+":"+cur);}'
                 'return old+"|"+string.Join(",",hidden);')


def view_off(ed, state):
    old, hidden = state.split('|')
    tabs = ''.join(f'{{var w=all.First(x=>x.GetInstanceID()=={h.split(":")[0]});var ho=Host(w);ho.GetType().GetProperty("selected",f).SetValue(ho,{h.split(":")[1]});}}' for h in hidden.split(',') if h)
    ed.ev(GV + tabs + f't.GetMethod("SizeSelectionCallback",f,null,new[]{{typeof(int),typeof(object)}},null).Invoke(all[0],new object[]{{{old},null}});return "restored";')


def shadow_settings(ed, cascades, distance):
    return ed.ev('var a=(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;'
                 'var old=a.shadowCascadeCount+","+a.shadowDistance.ToString(System.Globalization.CultureInfo.InvariantCulture);'
                 f'a.shadowCascadeCount={cascades};a.shadowDistance={distance}f;return old;')


def perf_window(ed, label):
    samples = []
    end = time.time() + SECONDS * SCALE
    f0 = ed.ev('return UnityEngine.Time.frameCount+"|"+UnityEngine.Time.realtimeSinceStartup.ToString(System.Globalization.CultureInfo.InvariantCulture);')
    while True:
        samples.append(json.loads(ed.ev(STATS)))
        if time.time() >= end:
            break
        time.sleep(max(0.0, min(SCALE, end - time.time())))
    f1 = ed.ev('return UnityEngine.Time.frameCount+"|"+UnityEngine.Time.realtimeSinceStartup.ToString(System.Globalization.CultureInfo.InvariantCulture);')
    (fa, ta), (fb, tb) = (map(float, f0.split('|')), map(float, f1.split('|')))
    return dict(label=label, samples=len(samples), fps=round((fb - fa) / max(1e-6, tb - ta), 1),
                median={k: statistics.median(s[k] for s in samples) for k in KEYS}, max={k: max(s[k] for s in samples) for k in KEYS})


def perf(args):
    root, play, _ = config()
    pf = play.get('perf') or {}
    if not pf.get('probe_file'):
        raise Fail(2, 'NO_PERF_PROBE in harness.play.perf')
    size = '1920x1080'
    if '--size' in args:
        i = args.index('--size')
        size = args[i + 1]
        del args[i:i + 2]
        forced = True
    else:
        forced = False
    out = Path(args[0]) if args else root / play.get('probe_dir', 'output') / 'perf.json'
    whatif = os.environ['WHATIF'].split(',') if os.environ.get('WHATIF') else None
    ed = Editor(root)
    ready(ed, play.get('scene'))
    rep = dict(scene=play.get('scene'), seconds=SECONDS, started=time.strftime('%F %T'), size=size)
    with L.EditorLock(root, 'play_check perf'):
        state = view_on(ed, size)
        shadow_old = shadow_settings(ed, *whatif) if whatif else None
        try:
            rep['pipeline'] = ed.ev('return UnityEditor.AssetDatabase.GetAssetPath(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline);')
            if whatif:
                rep['whatif'] = whatif
            with background(ed):
                failing = True
                try:
                    enter_play(ed)
                    ed.cmd('editor', 'focus')
                    nap(1)
                    rep['render_size'] = ed.ev('var c=UnityEngine.Camera.main;return c.pixelWidth+"x"+c.pixelHeight;')
                    # the numbers scale with the view: a tall window sees half the field (W6)
                    if not forced and rep['render_size'] != '1920x1080':
                        raise Fail(1, 'RENDER_SIZE_NOT_1080P ' + rep['render_size'])
                    ed.run(root / pf['probe_file'], pf['stand'])
                    nap(2)
                    rep['windows'] = [perf_window(ed, 'stand')]
                    ed.run(root / pf['probe_file'], pf['swing'])
                    nap(1)
                    rep['windows'].append(perf_window(ed, 'swing'))
                    rep['console'] = ed.console()['counts']
                    failing = False
                finally:
                    leave_play(ed, failing)
        finally:
            if shadow_old:
                shadow_settings(ed, *shadow_old.split(','))
            view_off(ed, state)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(rep, ensure_ascii=False, indent=1))
    budget = pf.get('budget') or {}
    for w in rep['windows']:
        m, x = w['median'], w['max']
        over = [k for k, v in budget.items() if x.get(k, 0) > v]
        print(f"{w['label']}: fps {w['fps']} | median draw {m['drawCalls']:.0f} setpass {m['setPassCalls']:.0f} tris {m['triangles'] / 1e4:.1f}만 shadow {m['shadowCasters']:.0f} render {m['renderTime'] * 1000:.2f}ms frame {m['frameTime'] * 1000:.2f}ms"
              f" | max draw {x['drawCalls']:.0f} tris {x['triangles'] / 1e4:.1f}만 shadow {x['shadowCasters']:.0f} | over budget: {over or 'none'}")
    print('size', rep['render_size'], 'console', rep['console'], '->', out)
    if rep['console'].get('error'):
        raise Fail(1, f"CONSOLE_ERRORS {rep['console']['error']}")


# ---------------- selftest ----------------
FAKE = r'''#!/usr/bin/python3
import json, os, sys, re
S = os.environ['FAKE_DIR']
def st(k, d=None):
    p = f'{S}/{k}'
    return open(p).read() if os.path.exists(p) else d
def put(k, v): open(f'{S}/{k}', 'w').write(str(v))
s = ' '.join(sys.argv[1:])
if st('dead'): sys.exit(1)
if st('reloading'):
    n = int(st('reloading'))
    if n > 0:
        put('reloading', n - 1); print(json.dumps(dict(success=False, errors=[dict(code='EDITOR_NOT_RUNNING')]))); sys.exit(0)
def ok(r): print(json.dumps(dict(success=True, result=r)))
if 'editor status' in s:
    print(json.dumps(dict(status='ready', compiling=False, domainReloadInProgress=False, playMode='playing' if st('playing') else 'stopped')))
elif 'console status' in s: print(json.dumps(dict(counts=dict(error=int(st('errors', '0')), warn=0, log=0), groundTruth=dict(compilationFailed=False))))
elif 'console clear' in s or 'editor focus' in s: print('{}')
elif 'script run' in s:
    e = s.split('--entry ')[1].split()[0]
    if e.endswith('Stop'): ok('stopped')
    elif st('play_dies'): os.remove(f'{S}/playing'); ok('run')
    else:
        if not st('no_result'):
            d = os.environ['FAKE_RESULT_DIR']; os.makedirs(d, exist_ok=True); open(f"{d}/{os.environ['FAKE_RESULT']}", 'w').write(st('result_text', 'ok\n'))
            if not st('no_capture'): open(f"{d}/shot.jpg", 'w').write('jpg')
        if not st('frames_stuck'): put('frame', int(st('frame', '5')) + 10)
        ok('run')
else:
    code = sys.argv[-1]
    if 'GetActiveScene' in code: ok('Assets/G.unity|False')
    elif 'EnterPlaymode' in code:
        put('playing', 1); ok('ok')
        if st('reload_after_enter'): put('reloading', st('reload_after_enter'))
        if st('never_back'): put('reloading', 99999)
    elif 'ExitPlaymode' in code:
        if os.path.exists(f'{S}/playing'): os.remove(f'{S}/playing')
        ok('ok')
    elif 'isPlaying' in code: ok('True' if st('playing') else 'False')
    elif 'frameCount+' in code: ok(st('frame', '5') + '|1.0')
    elif 'frameCount' in code: ok(st('frame', '5'))
    elif 'runInBackground=' in code:
        v = 'True' if 'runInBackground=true' in code else 'False'; put('bg', v); ok(v)
    elif 'runInBackground' in code: ok(st('bg', 'False'))
    elif 'pixelWidth' in code: ok(st('size', '1920x1080'))
    elif 'UnityStats' in code: ok(json.dumps(dict(drawCalls=10, setPassCalls=2, triangles=100, shadowCasters=1, frameTime=.01, renderTime=.001)))
    elif 'selectedSizeIndex' in code: ok('0|')
    elif 'currentRenderPipeline' in code and 'shadowCascadeCount' in code: ok('4,50')
    elif 'GetAssetPath' in code: ok('Assets/P.asset')
    else: ok('ok')
'''


def selftest():
    t = Path(tempfile.mkdtemp(prefix='play-check-selftest-'))
    (t / 'bin').mkdir(); (t / 'fake').mkdir(); (t / 'pd/probe').mkdir(parents=True)
    fake = t / 'bin/unity'; fake.write_text(FAKE); fake.chmod(0o755)
    (t / '3dkit.config.json').write_text(json.dumps(dict(schema=C.CONFIG_SCHEMA, harness=dict(
        play=dict(scene='Assets/G.unity', probe_file='P.cs', probe_dir='pd', short_pass='^ok', full_entry='P.Full', full_result='r.txt', rebuild_call='B.Run();',
                  perf=dict(probe_file='P.cs', stand='P.Stand', swing='P.Swing', budget=dict(drawCalls=5))),
        evidence=dict(runtime=['src'], visual=['art'])))))
    (t / 'P.cs').write_text('')
    env0 = {k: v for k, v in os.environ.items() if not k.startswith('GIT_') and k != 'KIT_EDITOR_LOCK'}
    subprocess.run(['git', 'init', '-q'], cwd=t, env=env0, check=True)
    (t / 'src').mkdir(); (t / 'src/a.cs').write_text('a')
    git = lambda *x: subprocess.run(['git', '-c', 'user.name=t', '-c', 'user.email=t@t', *x], cwd=t, env=env0, check=True, capture_output=True)
    git('add', 'src'); git('commit', '-qm', 'base')
    os.environ.pop('GIT_DIR', None); os.environ.pop('GIT_INDEX_FILE', None); os.environ.pop('GIT_WORK_TREE', None)
    fp0 = E.fingerprint(t, ['src']); (t / 'src/a.cs').write_text('b'); fp1 = E.fingerprint(t, ['src']); git('add', 'src'); git('commit', '-qm', 'b'); fp2 = E.fingerprint(t, ['src'])
    (t / 'src/n.cs').write_text('n'); fp3 = E.fingerprint(t, ['src']); (t / 'src/n.cs').unlink()
    (t / 'src/Editor').mkdir(); (t / 'src/Editor/e.cs').write_text('e'); fp4 = E.fingerprint(t, ['src'])
    env = dict(env0, PATH=f"{t}/bin:{env0['PATH']}", FAKE_DIR=str(t / 'fake'), FAKE_RESULT_DIR=str(t / 'pd/probe'), FAKE_RESULT='r.txt', PLAY_CHECK_TIME_SCALE='0.0005')
    env.pop('FULL_REASON', None)
    f = t / 'fake'
    def reset(**kw):
        for p in f.iterdir(): p.unlink()
        (t / 'pd/probe').mkdir(parents=True, exist_ok=True)
        (t / 'pd/probe/old.jpg').write_text('old')
        for k, v in kw.items(): (f / k).write_text(str(v))
    def run(*a, **e):
        return subprocess.run([sys.executable, str(HERE / 'play_check.py'), *a], cwd=t, capture_output=True, text=True, env=dict(env, **e), timeout=120)
    cases = []
    def case(name, got, want): cases.append((name, got == want, got))
    case('fingerprint: edit changes it', fp0 != fp1, True); case('fingerprint: committing does not', fp1 == fp2, True)
    case('fingerprint: new file changes it', fp2 != fp3, True); case('fingerprint: Editor folder ignored', fp4 == fp2, True)
    reset(dead=1); r = run('short', 'P.Run', 'r.txt')
    case('no editor: exit 4', r.returncode, 4); case('no editor: old capture kept', (t / 'pd/probe/old.jpg').exists(), True)
    reset(); r = run('short', 'P.Run', 'r.txt')
    if r.returncode: print(r.stdout[-300:], r.stderr[-600:])
    case('clean run: exit 0', r.returncode, 0); case('clean run: evidence written', bool(E.read(t).get('short', {}).get('fingerprint')), True)
    case('clean run: old capture cleared', (t / 'pd/probe/old.jpg').exists(), False); case('clean run: background restored', (f / 'bg').read_text(), 'False')
    reset(); r = run('short', 'P.Run', 'r.txt', '--keep'); case('keep: old capture kept', (t / 'pd/probe/old.jpg').exists(), True)
    reset(errors=3); E.path(t).unlink(missing_ok=True); r = run('short', 'P.Run', 'r.txt')
    case('console errors: exit 1', r.returncode, 1); case('console errors: no evidence', E.path(t).exists(), False)
    reset(errors=3); r = run('short', 'P.Run', 'r.txt', '--keep'); case('console errors with keep: exit 1', r.returncode, 1)
    reset(result_text='ERROR boom\n'); r = run('short', 'P.Run', 'r.txt'); case('error in result: exit 1', r.returncode, 1)
    reset(frames_stuck=1); r = run('short', 'P.Run', 'r.txt'); case('frames stuck: exit 1', (r.returncode, 'FRAMES_DID_NOT_ADVANCE' in r.stderr), (1, True))
    reset(play_dies=1); r = run('short', 'P.Run', 'r.txt'); case('play dies: exit 1', (r.returncode, 'PLAY_ENDED_BEFORE_RESULT' in r.stderr), (1, True))
    case('play dies: background restored', (f / 'bg').read_text(), 'False')
    reset(reload_after_enter=4); r = run('short', 'P.Run', 'r.txt')
    case('reload while entering Play: waits, passes', r.returncode, 0); case('reload: Play stopped', (f / 'playing').exists(), False); case('reload: background restored', (f / 'bg').read_text(), 'False')
    reset(reload_after_enter=3); (f / 'errors').write_text('1'); r = run('short', 'P.Run', 'r.txt'); case('reload then console error', (r.returncode, 'CONSOLE_ERRORS' in r.stderr), (1, True))
    reset(never_back=1); r = run('short', 'P.Run', 'r.txt'); case('Editor gone mid-run: exit 1, no hang', r.returncode, 1)
    reset(); r = run('full'); case('full without reason: exit 2', (r.returncode, 'FULL_REASON_REQUIRED' in r.stderr), (2, True))
    reset(result_text='fails=none\n'); r = run('full', FULL_REASON='selftest'); case('full pass', (r.returncode, 'PASS' in (t / 'pd/full-log.txt').read_text()), (0, True))
    reset(result_text='fails=STUCK\n'); r = run('full', FULL_REASON='selftest'); case('full fail', r.returncode, 1)
    # B45: the result contract
    reset(result_text=' \n'); r = run('short', 'P.Run', 'r.txt'); case('empty result: exit 1', (r.returncode, 'RESULT_EMPTY' in r.stderr), (1, True))
    reset(no_capture=1); r = run('short', 'P.Run', 'r.txt'); case('no capture: exit 1', (r.returncode, 'NO_CAPTURE' in r.stderr), (1, True))
    reset(no_capture=1); r = run('short', 'P.Run', 'r.txt', '--keep'); case('kept old capture does not count', (r.returncode, 'NO_CAPTURE' in r.stderr), (1, True))
    reset(result_text='nope\n'); r = run('short', 'P.Run', 'r.txt'); case('short_pass not met: exit 1', (r.returncode, 'SHORT_PASS_NOT_MET' in r.stderr), (1, True))
    reset(result_text='nope\n'); r = run('short', 'P.Run', 'r.txt', '--pass', '^nope'); case('--pass replaces short_pass', r.returncode, 0)
    reset(result_text='nope\n'); r = run('short', 'P.Run', 'r.txt', '--pass', ''); case("--pass '' turns it off", r.returncode, 0)
    reset(); E.path(t).unlink(missing_ok=True); r = run('short', 'P.Run', 'r.txt'); case('evidence lists the capture', E.read(t).get('short', {}).get('captures'), ['pd/probe/shot.jpg'])
    # B46: an OS lock held by another run (here: this process) refuses with the owner; a dead owner line is ignored
    held = L.EditorLock(t, 'other session').__enter__()
    reset(); r = run('short', 'P.Run', 'r.txt')
    case('editor locked: exit 5', (r.returncode, 'other session' in r.stderr), (5, True)); case('editor locked: old capture kept', (t / 'pd/probe/old.jpg').exists(), True)
    held.__exit__(None, None, None)
    L.lock_path(t).write_text(json.dumps(dict(pid=999999, what='dead', at='then')))
    reset(); r = run('short', 'P.Run', 'r.txt'); case('dead owner line: runs', r.returncode, 0)
    free = L.EditorLock(t, 'after'); free.__enter__(); free.__exit__(None, None, None); case('lock released after the run', True, True)
    case('editor_lock selftest', L.selftest().startswith('editor_lock OK'), True)
    reset(size='776x991'); r = run('perf', str(t / 'p.json')); case('perf tall window refused', (r.returncode, 'RENDER_SIZE_NOT_1080P' in r.stderr), (1, True))
    reset(); r = run('perf', str(t / 'p.json')); case('perf pass and over budget shown', (r.returncode, "over budget: ['drawCalls']" in r.stdout), (0, True))
    reset(); r = run('rebuild'); case('rebuild pass', r.returncode, 0)
    reset(errors=1); r = run('rebuild'); case('rebuild console error', r.returncode, 1)
    shutil.rmtree(t, ignore_errors=True)
    bad = [c for c in cases if not c[1]]
    for c in bad:
        print('FAIL', c[0], c[2])
    print(f'play_check selftest {"PASS" if not bad else "FAIL"} ({len(cases)} cases)')
    return not bad


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ('short', 'full', 'perf', 'rebuild', 'selftest'):
        print(__doc__); return 2
    cmd, args = sys.argv[1], sys.argv[2:]
    if cmd == 'selftest':
        return 0 if selftest() else 1
    try:
        dict(short=short, full=full, perf=perf, rebuild=rebuild)[cmd](args)
        return 0
    except Fail as e:
        print(f'PLAY_CHECK {cmd}: {e}', file=sys.stderr)
        return e.code
    except L.Locked as e:
        print(f'PLAY_CHECK {cmd}: {e}', file=sys.stderr)
        return 5


if __name__ == '__main__':
    sys.exit(main())

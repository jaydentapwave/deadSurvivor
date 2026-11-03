#!/usr/bin/env python3
"""One Play probe for looking and measuring (047 H5). Replaces the throw-away scripts of 046/047 (spawn, UI, effects,
attack layer, movement): lobby -> squad -> sortie -> skip to a wave -> optional HP lock / night -> captures, counts,
attack-layer samples, performance line -> exit Play -> cleanup -> side-effect files restored.

This is a measuring tool, not a verification tier: it records no tier evidence and judges nothing except its own run
(flow finished, cleanup done, no new console errors). The short Play (core-rules 1) is still Tools/Proto/keyart_045.py.

  /usr/bin/python3 Tools/Harness/play_probe.py [options]
    --scene PATH        default Assets/DeadSurvivor/Scenes/Map046/Map046_Full.unity
    --squad 0,1,2,3     lobby slots to select (default 0,1,2,3)
    --wave N            skip to wave N with DebugFinish (default 1)
    --hp-lock           heroes get 1e6 HP after the skip (long looks without defeat)
    --settle S          seconds to wait in battle before measuring (default 3)
    --captures N        game-camera renders 1280x720 (default 1; 0 = none)
    --interval S        seconds between captures (default 0.3; effects last 0.2-0.5 s -> use bursts, 047 H7)
    --night             switch DSDayNight to night (2.2 s transition) before the captures
    --attack N          sample the hero attack layer N times every 0.1 s (047 H6)
    --perf S            measure for S seconds: fps, and the median (and max) of draw calls, set-pass, triangles,
                        shadow casters sampled about once a second (047 H12, 049)
    --mobile            render with Mobile_RPAsset while measuring (the budget basis, docs/workflows/performance.md)
    --count-scale X     monster count multiplier for the run (game setting 0.1-3; load measurements)
    --assets            no Play: print the active scene's actor set (renderers, sub-meshes, materials, triangles,
                        shadows, instancing) — look at the assets before proposing an optimisation (049-1)
    --label NAME        output folder suffix
    --dry-run           print the plan only (no Unity)
    --selftest          argument/plan checks without Unity
Output: output/play-probe/<time>-<id>-<label>/report.json and PNGs. Prints one compact JSON line."""
import argparse, json, subprocess, sys, time, uuid
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/Harness'))
import protect  # noqa: E402

PROBE = 'DeadSurvivor.Editor.DSKeyArt045Verification'
START, NEXT = '스쿼드 출전 →', '다음 웨이브 →'
OPEN_REFUSED = ('ALREADY_PLAYING', 'UNSAVED_SCENE', 'UNFINISHED_PREVIOUS_RUN', 'WRONG_PROJECT')   # DSKeyArt045Verification.OpenScene checks
GAME = 'var g=UnityEngine.Object.FindAnyObjectByType<DeadSurvivor.DSGame>();'
CAPTURE = (GAME + 'var cam=g.MapSession.ViewCamera;var rt=new UnityEngine.RenderTexture(1280,720,24){antiAliasing=4};cam.targetTexture=rt;cam.Render();'
           'var prev=UnityEngine.RenderTexture.active;UnityEngine.RenderTexture.active=rt;var tex=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);'
           'tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0);tex.Apply();UnityEngine.RenderTexture.active=prev;cam.targetTexture=null;'
           'System.IO.File.WriteAllBytes("PATH",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);'
           'var fx=g.MapSession.GetComponent<DeadSurvivor.DSMapFx>();'
           'return (fx?fx.ActiveShots:-1)+"|"+(fx?fx.ActiveEffects:-1)+"|"+g.Effects.Count+"|"+g.Enemies.Count+"|"+string.Join(",",g.Enemies.Select(e=>e.type).Distinct());')
ATTACK = ('var all=UnityEngine.Object.FindObjectsByType<UnityEngine.Animator>(UnityEngine.FindObjectsInactive.Exclude);'
          'return string.Join(",",all.Select(a=>new{a,i=a.GetLayerIndex("Attack047")}).Where(x=>x.i>=0).Select(x=>{var s=x.a.GetCurrentAnimatorStateInfo(x.i);'
          'var n=x.a.transform.parent?x.a.transform.parent.name:x.a.name;if(x.a.transform.parent&&x.a.transform.parent.parent)n=x.a.transform.parent.parent.name;'
          'return n+":"+(s.IsName("Slash")?"S":s.IsName("Shoot")?"H":"-");}));')
STATS = ('var st=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.UnityStats");'
         'string V(string n){var pr=st.GetProperty(n,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);return pr==null?"null":pr.GetValue(null).ToString();}'
         'return "{"+string.Join(",",new[]{"drawCalls","setPassCalls","triangles","shadowCasters","frameTime","renderTime"}.Select(n=>"\\""+n+"\\":"+V(n)))+"}";')


def parse(argv):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('--scene', default='Assets/DeadSurvivor/Scenes/Map046/Map046_Full.unity')
    p.add_argument('--squad', default='0,1,2,3')
    p.add_argument('--wave', type=int, default=1)
    p.add_argument('--hp-lock', action='store_true')
    p.add_argument('--settle', type=float, default=3.0)
    p.add_argument('--captures', type=int, default=1)
    p.add_argument('--interval', type=float, default=0.3)
    p.add_argument('--night', action='store_true')
    p.add_argument('--attack', type=int, default=0)
    p.add_argument('--perf', type=float, default=0.0)
    p.add_argument('--mobile', action='store_true')
    p.add_argument('--count-scale', type=float, default=0.0)
    p.add_argument('--assets', action='store_true')
    p.add_argument('--label', default='probe')
    p.add_argument('--dry-run', action='store_true')
    p.add_argument('--selftest', action='store_true')
    a = p.parse_args(argv)
    squad = [int(x) for x in a.squad.split(',') if x.strip() != '']
    errs = []
    if not a.scene.startswith('Assets/') or not a.scene.endswith('.unity') or not (ROOT / a.scene).is_file():
        errs.append('scene not found: ' + a.scene)
    if not 1 <= len(squad) <= 4 or len(set(squad)) != len(squad) or any(not 0 <= i <= 5 for i in squad):
        errs.append('squad: 1-4 distinct lobby slots 0-5')
    if not 1 <= a.wave <= 10: errs.append('wave: 1-10')
    if not 0 <= a.captures <= 20: errs.append('captures: 0-20')
    if not 0 <= a.attack <= 100: errs.append('attack: 0-100 samples')
    if not 0 <= a.perf <= 120: errs.append('perf: 0-120 s')
    if not 0 <= a.settle <= 120 or not 0 <= a.interval <= 10: errs.append('settle/interval out of range')
    if not all(ch.isalnum() or ch in '-_' for ch in a.label): errs.append('label: letters, digits, - and _ only')
    if a.count_scale and not 0.1 <= a.count_scale <= 3: errs.append('count-scale: 0.1-3 (game setting range)')
    return a, squad, errs


def plan(a, squad):
    steps = [f'open {a.scene}', 'enter Play'] + (['mobile pipeline'] if a.mobile else []) + ([f'count scale {a.count_scale}'] if a.count_scale else []) + [f'select {squad}', 'sortie']
    if a.wave > 1: steps.append(f'skip to wave {a.wave}')
    if a.hp_lock: steps.append('hp lock')
    steps.append(f'wait {a.settle}s')
    if a.attack: steps.append(f'attack samples x{a.attack}')
    if a.night: steps.append('night')
    if a.captures: steps.append(f'captures x{a.captures} every {a.interval}s')
    if a.perf: steps.append(f'perf {a.perf}s')
    return steps + (['pc pipeline back'] if a.mobile else []) + ['exit Play', 'cleanup (quality, side-effect dirty flags, scene setup, runInBackground)', 'restore side-effect files']


class Unity:
    def __init__(self, report):
        self.report = report

    def ready(self):
        subprocess.run(['unity', 'status', '--until-ready', '--project-path', str(ROOT)], capture_output=True, timeout=300)

    def cli(self, args, timeout=120):
        i = args.index('--') if '--' in args else len(args)
        r = subprocess.run(['unity', *args[:i], '--project-path', str(ROOT), '--format', 'json', *args[i:]], capture_output=True, text=True, timeout=timeout)
        try:
            d = json.loads(r.stdout)
        except json.JSONDecodeError:
            raise RuntimeError('CLI_NOT_JSON:' + (r.stdout + r.stderr)[:400])
        if not d.get('success'):
            raise RuntimeError('CLI_FAIL:' + json.dumps(d.get('errors'), ensure_ascii=False)[:400])
        return d['data']

    def eval(self, code, timeout=60):
        res = self.cli(['command', 'script', 'eval', '--', code], timeout)['result']
        if not res.get('success', True):
            raise RuntimeError('EVAL_FAIL:' + json.dumps(res, ensure_ascii=False)[:500])
        return res['result']

    def call(self, method, *args):
        self.report['last_call'] = f'{method}{args}'
        return self.eval(f'return {PROBE}.{method}({",".join(json.dumps(x, ensure_ascii=False) for x in args)});')

    def state(self):
        return json.loads(self.call('State'))

    def wait(self, pred, what, timeout=30.0):
        end, last = time.time() + timeout, None
        while time.time() < end:
            try:
                last = self.state()
                if pred(last):
                    return last
            except Exception as e:
                last = {'error': str(e)[:200]}
            time.sleep(0.3)
        raise RuntimeError('TIMEOUT:' + what + ':' + json.dumps(last, ensure_ascii=False)[:400])

    def console_seq(self):
        entries = self.cli(['command', 'console', 'get', '--', '--tail', '50', '--level', 'error'], 60)['result'].get('entries', [])
        return entries


def run(a, squad, unity_cls=None):
    t0 = time.time(); stamp = datetime.now(ZoneInfo('Asia/Seoul'))
    out = ROOT / 'output/play-probe' / f"{stamp.strftime('%Y%m%d-%H%M%S')}-{uuid.uuid4().hex[:4]}-{a.label}"; out.mkdir(parents=True)
    report = dict(status='RUNNING', started=stamp.isoformat(), args=vars(a), plan=plan(a, squad), results={})
    R = report['results']; u = (unity_cls or Unity)(report)
    save = lambda: (out / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=1))
    u.ready()
    snap = protect.snapshot(); bg_before = None; opened = False; mobile_on = False
    try:
        s0 = u.state()
        if s0.get('playing'):
            raise RuntimeError('EDITOR_IN_PLAY_MODE: stop Play first (it may be the user playing)')
        try: before = u.console_seq()
        except Exception: before = []
        report['console_seq_before'] = max([e.get('seq', 0) for e in before] or [0])
        bg_before = u.eval('return UnityEditor.PlayerSettings.runInBackground.ToString();').lower()
        u.cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', 'true'], 60)   # 046-3: else 3 fps
        # review 048 r001 #3: a timeout after Unity already opened the scene must still restore it. Only the refusals
        # below happen before the probe saves any state; then the previous run's / the user's state is left alone.
        try:
            u.call('OpenScene', a.scene); opened = True
        except Exception as e:
            opened = not any(k in str(e) for k in OPEN_REFUSED)
            raise
        u.call('EnterPlay'); s = u.wait(lambda s: s.get('playing') and s.get('state') == 'lobby', 'lobby', 90)
        if a.mobile:
            report['pipeline'] = u.call('MobilePipeline', True); mobile_on = True; time.sleep(0.5)
        if a.count_scale:
            report['count_scale'] = u.eval(GAME + f'g.Config.countScale={a.count_scale}f;return g.Config.countScale.ToString();')
        for i in list(s['selected']): u.call('Submit', 'ToggleSelection', i)
        for i in squad: u.call('Submit', 'ToggleSelection', i)
        u.wait(lambda s: sorted(s['selected']) == sorted(squad), 'select')
        u.call('Click', START); u.wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'battle')
        for w in range(1, a.wave):
            u.call('Submit', 'DebugFinish', 0); u.wait(lambda s: s['state'] in ('prep', 'result'), f'finish_w{w}')
            u.call('Click', NEXT); u.wait(lambda s, w=w: s['state'] == 'battle' and s['wave'] == w + 1, f'battle_w{w + 1}')
        if a.hp_lock:
            u.eval(GAME + 'foreach(var c in g.Team){c.maxhp=1e6f;c.hp=1e6f;}return "HP";')
        time.sleep(a.settle)
        if a.attack:
            counts = {}
            for _ in range(a.attack):
                for item in filter(None, u.eval(ATTACK).split(',')):
                    n, v = item.rsplit(':', 1); counts.setdefault(n, {'S': 0, 'H': 0, '-': 0})[v] += 1
                time.sleep(0.1)
            R['attack_layer'] = counts or 'NO_ATTACK047_LAYER'
        if a.night:
            u.eval('UnityEngine.Object.FindAnyObjectByType<DeadSurvivor.DSDayNight>().SetNight(true);return "N";'); time.sleep(2.2)
        shots = []
        for k in range(a.captures):
            rel = str((out / f'{a.label}_{k}.png').relative_to(ROOT))
            sh, fx, eff, en, types = u.eval(CAPTURE.replace('PATH', rel)).split('|')
            shots.append(dict(png=rel, projectiles=int(sh), timed_fx=int(fx), game_effects=int(eff), enemies=int(en), types=types))
            if k + 1 < a.captures: time.sleep(a.interval)
        if shots: R['captures'] = shots
        if a.perf:
            s1 = u.state(); f0 = u.eval('return UnityEngine.Time.frameCount+"|"+UnityEngine.Time.realtimeSinceStartup;')
            # 049: UnityStats is one frame; sample it about once a second over the window and keep median and max
            samples, end = [], time.time() + a.perf
            while time.time() < end:
                try: samples.append(json.loads(u.eval(STATS)))
                except Exception: pass
                time.sleep(max(0.0, min(1.0, end - time.time())))
            f1 = u.eval('return UnityEngine.Time.frameCount+"|"+UnityEngine.Time.realtimeSinceStartup;'); s2 = u.state()
            med = lambda k: sorted(x[k] for x in samples)[len(samples) // 2] if samples else None
            stats = {k: med(k) for k in ('drawCalls', 'setPassCalls', 'triangles', 'shadowCasters', 'frameTime', 'renderTime')}
            stats.update(samples=len(samples), max={k: max(x[k] for x in samples) for k in ('drawCalls', 'setPassCalls', 'triangles')} if samples else {})
            (fa, ta), (fb, tb) = (map(float, f0.split('|')), map(float, f1.split('|')))
            R['perf'] = dict(seconds=a.perf, fps=round((fb - fa) / max(1e-6, tb - ta), 1), enemies=s2['enemies'], spawned=s2['spawned'] - s1['spawned'],
                             spawn_failures=s2['spawnFailures'] - s1['spawnFailures'], **stats,
                             note='editor numbers; only one view may render the game (046-13), runInBackground on (046-3)')
        fin = u.state()
        R['state'] = {k: fin.get(k) for k in ('state', 'wave', 'enemies', 'spawned', 'spawnFailures', 'heroesAlive', 'kills')}
        report['flow'] = 'PASS'
    except Exception as e:
        report['flow'] = 'FAIL'; report['error'] = str(e)[:800]
    finally:
        clean = report['cleanup'] = {}
        def attempt(name, fn):
            try: clean[name] = str(fn())[:120]
            except Exception as e: report.setdefault('cleanup_errors', {})[name] = str(e)[:200]
        if mobile_on:
            attempt('pc_pipeline', lambda: u.call('MobilePipeline', False))
        if opened:
            attempt('exit_play', lambda: u.call('ExitPlay'))
            attempt('wait_edit', lambda: u.wait(lambda s: not s.get('playing'), 'edit', 60) and 'EDIT_MODE')
            attempt('quality', lambda: u.call('RestoreQuality'))
            attempt('side_effect_dirty', lambda: u.call('ClearSideEffectDirty'))
            attempt('scene_setup', lambda: u.call('Restore'))
        if bg_before is not None:
            attempt('run_in_background', lambda: u.cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', bg_before], 60) and bg_before)
        report['side_effect_files'] = protect.restore(snap)
        try:
            report['console_new_errors'] = [dict(seq=e['seq'], message=e.get('message', '')[:300]) for e in u.console_seq() if e.get('seq', 0) > report.get('console_seq_before', 1 << 62)]
        except Exception as e:
            report['console_new_errors'] = 'UNAVAILABLE:' + str(e)[:200]
        ok = report.get('flow') == 'PASS' and not report.get('cleanup_errors') and report['console_new_errors'] == [] and not report['side_effect_files']['failed']
        report['status'] = 'PASS' if ok else 'FAIL'; report['seconds'] = round(time.time() - t0, 1)
        save()
    brief = dict(status=report['status'], out=str(out.relative_to(ROOT)), seconds=report['seconds'], error=report.get('error'),
                 cleanup_errors=report.get('cleanup_errors'), console_new=report['console_new_errors'], side_effect_files=report['side_effect_files'],
                 state=R.get('state'), perf=R.get('perf'), attack_layer=R.get('attack_layer'),
                 captures=[f"{c['png'].rsplit('/', 1)[-1]} shots={c['projectiles']} fx={c['timed_fx']}" for c in R.get('captures', [])])
    print(json.dumps({k: v for k, v in brief.items() if v not in (None, [], {})}, ensure_ascii=False)[:3000])
    return report['status'] == 'PASS'


ASSETS = ('var pres=UnityEngine.Object.FindAnyObjectByType<DeadSurvivor.DSMapPresentation>();if(!pres||!pres.Actors)return "NO_ACTOR_SET";var set=pres.Actors;'
          'var rows=new System.Collections.Generic.List<string>();'
          'foreach(var grp in new[]{("hero",set.Heroes),("monster",set.Monsters)})foreach(var e in grp.Item2){if(!e.prefab)continue;'
          'var rs=e.prefab.GetComponentsInChildren<UnityEngine.Renderer>(true);int sub=0,tri=0;var mats=new System.Collections.Generic.HashSet<UnityEngine.Material>();'
          'foreach(var r in rs){UnityEngine.Mesh m=r is UnityEngine.SkinnedMeshRenderer s?s.sharedMesh:(r.GetComponent<UnityEngine.MeshFilter>()?r.GetComponent<UnityEngine.MeshFilter>().sharedMesh:null);'
          'if(m){sub+=m.subMeshCount;tri+=m.triangles.Length/3;}foreach(var x in r.sharedMaterials)if(x)mats.Add(x);}'
          'var mat=e.material?e.material:System.Linq.Enumerable.FirstOrDefault(mats);'
          'rows.Add("{\\"group\\":\\""+grp.Item1+"\\",\\"id\\":\\""+e.id+"\\",\\"renderers\\":"+rs.Length+",\\"submeshes\\":"+sub+",\\"materials\\":"+(e.material?1:mats.Count)+",\\"triangles\\":"+tri'
          '+",\\"skinned\\":"+(rs.Length>0&&rs[0] is UnityEngine.SkinnedMeshRenderer?"true":"false")+",\\"shadow\\":\\""+(rs.Length>0?rs[0].shadowCastingMode.ToString():"-")+"\\",\\"instancing\\":"+(mat&&mat.enableInstancing?"true":"false")+"}");}'
          'return "["+string.Join(",",rows)+"]";')


def assets():
    u = Unity({}); u.ready()
    rows = json.loads(u.eval(ASSETS))
    print(json.dumps({'actor_set': rows}, ensure_ascii=False))
    return True


def selftest():
    c = {}
    a, sq, e = parse([]); c['defaults_ok'] = e == [] and sq == [0, 1, 2, 3] and a.wave == 1 and a.captures == 1
    c['bad_squad'] = bool(parse(['--squad', '0,0,1'])[2]) and bool(parse(['--squad', '0,1,2,3,4'])[2]) and bool(parse(['--squad', '6'])[2])
    c['bad_wave'] = bool(parse(['--wave', '11'])[2]) and bool(parse(['--wave', '0'])[2])
    c['bad_scene'] = bool(parse(['--scene', 'Assets/none.unity'])[2])
    c['bad_label'] = bool(parse(['--label', '../x'])[2])
    a, sq, e = parse(['--wave', '7', '--hp-lock', '--perf', '20', '--captures', '4', '--attack', '30', '--night'])
    p = plan(a, sq); c['plan_order'] = e == [] and p.index('skip to wave 7') < p.index('hp lock') < p.index('wait 3.0s') < p.index('night') < p.index('captures x4 every 0.3s') < p.index('perf 20.0s')
    c['plan_cleanup_last'] = p[-1] == 'restore side-effect files'
    a2, sq2, e2 = parse(['--mobile', '--count-scale', '3']); p2 = plan(a2, sq2)
    c['mobile_plan'] = e2 == [] and p2.index('mobile pipeline') < p2.index('sortie') and p2.index('pc pipeline back') < p2.index('exit Play')
    c['bad_count_scale'] = bool(parse(['--count-scale', '5'])[2])
    c['capture_path_placeholder'] = CAPTURE.count('PATH') == 1
    # review 048 r001 #3: cleanup after OpenScene fails (fake Unity, no editor)
    def fake_run(open_error):
        calls = []
        class Fake(Unity):
            def ready(self): pass
            def cli(self, args, timeout=120):
                calls.append(' '.join(args[:2]))
                return {'result': {'success': True, 'result': 'false', 'entries': []}}
            def eval(self, code, timeout=60):
                if 'OpenScene' in code: calls.append('OpenScene'); raise RuntimeError(open_error)
                for m in ('ExitPlay', 'RestoreQuality', 'ClearSideEffectDirty', 'Restore('):
                    if m in code: calls.append(m.rstrip('(')); return 'OK'
                if 'State' in code: return json.dumps({'playing': False})
                return 'false'
        with contextlib.redirect_stdout(io.StringIO()):
            run(*parse(['--captures', '0', '--label', 'selftest'])[:2], unity_cls=Fake)
        return calls
    import contextlib, io, shutil
    before = set((ROOT / 'output/play-probe').glob('*-selftest')) if (ROOT / 'output/play-probe').exists() else set()
    timeout_calls = fake_run('CLI_NOT_JSON: timeout')
    refused_calls = fake_run('Runtime Error UNFINISHED_PREVIOUS_RUN')
    for d in set((ROOT / 'output/play-probe').glob('*-selftest')) - before:
        shutil.rmtree(d, ignore_errors=True)
    c['open_timeout_restores'] = 'Restore' in timeout_calls and 'ExitPlay' in timeout_calls
    c['open_refused_leaves_state'] = 'OpenScene' in refused_calls and not any(x in refused_calls for x in ('Restore', 'ExitPlay', 'RestoreQuality'))
    ok = all(c.values())
    print(json.dumps({'status': 'PASS' if ok else 'FAIL', 'cases': c}))
    return ok


def main():
    a, squad, errs = parse(sys.argv[1:])
    if a.selftest:
        sys.exit(0 if selftest() else 1)
    if errs:
        sys.exit('play_probe: ' + '; '.join(errs) + '. Nothing was run.')
    if a.assets:
        sys.exit(0 if assets() else 1)
    if a.dry_run:
        print(json.dumps({'plan': plan(a, squad)}, ensure_ascii=False)); return
    sys.exit(0 if run(a, squad) else 1)


if __name__ == '__main__':
    main()

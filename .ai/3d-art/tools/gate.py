#!/usr/bin/env python3
"""3dkit H4: pre-commit gate. Looks only at what this commit touches, so a document commit takes a moment. Policy: 3dkit.config.json harness.gate
  never_commit      path patterns that must not be committed
  game_paths        paths whose change needs short Play evidence (harness.evidence fingerprints it)
  art_paths         paths whose change needs `assets.py check`
  accepted_check_fails   substrings of check failures the user decided to keep (with the decision in the config note)
  stage_records     path patterns of stage records (Markdown): in a table row this commit adds with a status cell 통과/PASS, a
                    project path in backticks must exist (fail); a row naming no evidence in a table with an evidence column is a note
  harness.python    interpreter for the tools (numpy + Pillow); default: the one running the gate

The assets check counts only a report that this gate run asked for (run id) or one made for exactly this tree (fingerprint of
art, catalog, config and kit; the gate then reuses it without the Editor). A check that died never passes on an old report (B44).

  gate.py run         what the Git hook calls
  gate.py install     write .git/hooks/pre-commit (not tracked by Git, so each clone runs this once)
  gate.py selftest    offline, temp repo, fake unity
Skipping: GATE_SKIP="<reason>" git commit ...  is logged to output/hook-state/gate-skips.log. `--no-verify` is denied by guard.py.
Exit 0 pass, 1 fail. Checks: forbidden files, runInBackground left on, catalog file format and consistency, links in changed documents, game scene in
the build list, missing GUIDs in changed scenes/prefabs, references to deleted assets, compile, short Play evidence, assets check, evidence of
the pass rows a stage record gains, kit selftests."""
import fnmatch
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import uuid
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from kitlib import config as C, evidence as E  # noqa: E402

YAML_EXT = ('.unity', '.prefab', '.mat', '.asset', '.controller', '.overrideController', '.anim', '.mask', '.physicMaterial')
BUILTIN_GUID = re.compile(r'^0{16}[def]0{15}$')
PY = sys.executable          # replaced by harness.python in run()


def clean_env():
    """a pre-commit hook runs with GIT_DIR and GIT_INDEX_FILE set: never let a tool or a selftest touch this index through them"""
    return {k: v for k, v in os.environ.items() if not k.startswith('GIT_')}


def commit_env():
    """the queries about what this commit writes keep the index git hands the hook: `commit --only <paths>` and `commit -a`
    commit a temporary one (GIT_INDEX_FILE), and the plain index does not have their changes yet (B54). Tools and selftests
    the gate starts still get clean_env()."""
    keep = ('GIT_INDEX_FILE', 'GIT_DIR', 'GIT_WORK_TREE', 'GIT_COMMON_DIR', 'GIT_OBJECT_DIRECTORY')
    return {k: v for k, v in os.environ.items() if not k.startswith('GIT_') or k in keep}


def git(root, *a, check=True, env=None):
    return subprocess.run(['git', *a], cwd=root, capture_output=True, text=True, check=check, env=env or clean_env()).stdout


def staged(root):
    out = []
    for line in git(root, 'diff', '--cached', '--name-status', '--no-renames', env=commit_env()).splitlines():
        st, p = line.split('\t', 1)
        out.append((st, p))
    return out


def show(root, p):
    return subprocess.run(['git', 'show', ':' + p], cwd=root, capture_output=True, env=commit_env()).stdout


def under(p, prefixes):
    return any(p == x or p.startswith(x.rstrip('/') + '/') for x in prefixes)


def check_never(root, files, g):
    return [f'NEVER_COMMIT {p}' for st, p in files if st != 'D' and any(fnmatch.fnmatch(p, pat) for pat in g.get('never_commit', []))]


def check_run_in_background(root, files):
    if any(p == 'ProjectSettings/ProjectSettings.asset' and st != 'D' for st, p in files):
        if re.search(rb'^\s*runInBackground: 1\s*$', show(root, 'ProjectSettings/ProjectSettings.asset'), re.M):
            return ['RUN_IN_BACKGROUND_LEFT_ON ProjectSettings/ProjectSettings.asset (a measuring run did not restore it, rule 1)']
    return []


def check_catalog(root, files, catalog):
    if not any(p == catalog and st != 'D' for st, p in files):
        return []
    raw = show(root, catalog).decode()
    try:
        c = json.loads(raw)
    except ValueError as e:
        return [f'CATALOG_NOT_JSON {e}']
    bad = []
    if raw.rstrip('\n') != json.dumps(c, ensure_ascii=False, indent=1):
        bad.append('CATALOG_FORMAT_CHANGED: write it with indent=1, ensure_ascii=False (B1)')
    r = subprocess.run([PY, str(HERE / 'gate.py'), '_project_check'], cwd=root, capture_output=True, text=True, env=clean_env())
    if r.returncode:
        bad.append('CATALOG_INCONSISTENT ' + (r.stderr.strip().splitlines() or ['?'])[-1][:200])
    return bad


def check_links(root, files):
    bad = []
    for st, p in files:
        if st == 'D' or not p.endswith('.md'):
            continue
        text = re.sub(r'```.*?```', '', show(root, p).decode(errors='ignore'), flags=re.S)
        text = re.sub(r'`[^`\n]*`', '', text)
        for m in re.findall(r'\]\(([^)\s#]+)(?:#[^)]*)?\)', text):
            if m.startswith(('http:', 'https:', 'mailto:')):
                continue
            if not (root / Path(p).parent / m).exists():
                bad.append(f'BROKEN_LINK {p} -> {m}')
    return bad


def _pass_row(row):
    """a Markdown table row with a status cell (not the first) that starts with 통과 or PASS"""
    t = row.strip()
    if not t.startswith('|'):
        return False
    return any(re.match(r'\**\s*(?:통과|PASS)\b', c.strip()) for c in t.strip('|').split('|')[1:])


def _evidence(row, root):
    """(paths to check, named): a path in backticks is checked when its first folder is a folder of the project root
    (output/..., docs/...); a bone name like Ctrl_Foot_L/R or a fragment of another folder is not. Named evidence: any such
    path, a file name with an extension, a link, or quoted user words."""
    check, named = [], False
    for x in re.findall(r'`([^`\n]+)`', row):
        x = re.sub(r'(:\d+(-\d+)?|#.*)$', '', x.strip())
        if not x or re.search(r'[\s<>*{}]', x) or x.startswith(('http:', 'https:')):
            continue
        if '/' in x and (root / x.split('/')[0]).is_dir():
            check.append(x)
        named = named or '/' in x or bool(re.search(r'\.\w{1,5}$', x))
    named = named or bool(check) or any(not m.startswith(('http:', 'https:', 'mailto:')) for m in re.findall(r'\]\(([^)\s#]+)', row))
    named = named or bool(re.search(r'"[^"]{2,}"|\u201c[^\u201d]{2,}\u201d', row))
    return check, named


def _added_lines(root, p):
    """(line number in the staged file, text) of each line this commit adds to p"""
    out, n = [], 0
    for line in git(root, 'diff', '--cached', '-U0', '--no-color', '--', p, env=commit_env()).splitlines():
        m = re.match(r'@@ -\S+ \+(\d+)', line)
        if m:
            n = int(m[1])
        elif line.startswith('+') and not line.startswith('+++'):
            out.append((n, line[1:]))
            n += 1
    return out


def check_stage_records(root, files, g):
    """a stage record's pass row keeps its proof (005 bundle E, Claude evaluation; AGENTS: a result keeps its evidence path).
    Only the rows this commit adds: an older row may name output/ files cleaned up since. A named project path that does not
    exist fails; a pass row in a table with an evidence column (근거/증거/evidence) that names nothing is a note, since the
    proof of a measurement table is often named once above it."""
    pats = g.get('stage_records') or []
    bad = []
    for st, p in files:
        if st == 'D' or not p.endswith('.md') or not any(fnmatch.fnmatch(p, x) for x in pats):
            continue
        lines = show(root, p).decode(errors='ignore').splitlines()
        for n, row in _added_lines(root, p):
            if not _pass_row(row):
                continue
            check, named = _evidence(row, root)
            missing = [x for x in check if not (root / x).exists()]
            if missing:
                bad.append(f'STAGE_EVIDENCE_MISSING {p}:{n} {missing[0]} -> next: the row says it passed; keep that file or name the one that exists')
                continue
            j = n - 1
            while 0 < j <= len(lines) and lines[j - 1].lstrip().startswith('|'):
                j -= 1
            head = lines[j] if j < len(lines) else ''
            if not named and re.search(r'근거|증거|[Ee]vidence', head):
                print(f'gate note STAGE_PASS_NO_EVIDENCE {p}:{n} {row.strip()[:60]} -> name the capture, report or log, or quote the user')
    return bad


def check_build_scene(root, play):
    scene = play.get('scene')
    if not scene:
        return []
    f = root / 'ProjectSettings/EditorBuildSettings.asset'
    txt = f.read_text() if f.exists() else ''
    on = [m.group(2) for m in re.finditer(r'enabled: (\d)\s+path: (\S+)', txt) if m.group(1) == '1']
    if scene not in on:
        return [f'GAME_SCENE_NOT_IN_BUILD {scene} (build list: {on})']
    return [] if (root / scene).exists() else [f'GAME_SCENE_MISSING {scene}']


def guid_map(root):
    """every GUID Unity can resolve here. None if the package cache is absent (a fresh clone): then missing GUIDs cannot be told apart"""
    if not (root / 'Library/PackageCache').is_dir():
        return None
    known = set()
    for base in ('Assets', 'Packages', 'Library/PackageCache', *E.local_packages(root)):
        for dp, _, names in os.walk(root / base):
            for n in names:
                if n.endswith('.meta'):
                    try:
                        with open(os.path.join(dp, n), errors='ignore') as fh:
                            for line in fh:
                                if line.startswith('guid:'):
                                    known.add(line.split()[1])
                                    break
                    except OSError:
                        pass
    return known


def check_guids(root, files):
    text_files = [p for st, p in files if st != 'D' and p.endswith(YAML_EXT)]
    deleted = [p for st, p in files if st == 'D' and p.endswith('.meta')]
    bad = []
    if text_files:
        known = guid_map(root)
        if known is None:
            print('gate: note: no Library/PackageCache, GUID check NOT_RUN')
        else:
            for p in text_files:
                data = show(root, p)
                if not data.startswith(b'%YAML'):
                    continue
                missing = {g for g in re.findall(r'guid: ([0-9a-f]{32})', data.decode(errors='ignore')) if g not in known and not BUILTIN_GUID.match(g)}
                if missing:
                    bad.append(f'MISSING_GUID {p}: {len(missing)} ({sorted(missing)[:2]})')
    if deleted:
        gone = {}
        for p in deleted:
            m = re.search(rb'^guid: ([0-9a-f]{32})', subprocess.run(['git', 'show', 'HEAD:' + p], cwd=root, capture_output=True, env=clean_env()).stdout, re.M)
            if m:
                gone[m.group(1).decode()] = p
        # a moved asset keeps its GUID: its .meta is staged at the new path, so nothing was deleted
        for st, p in files:
            if st != 'D' and p.endswith('.meta'):
                m = re.search(rb'^guid: ([0-9a-f]{32})', show(root, p), re.M)
                if m:
                    gone.pop(m.group(1).decode(), None)
        if gone:
            args = ['grep', '--cached', '-l', '-F']
            for g in gone:
                args += ['-e', 'guid: ' + g]
            users = git(root, *args, '--', 'Assets', check=False, env=commit_env()).splitlines()
            users = [u for u in users if not u.endswith('.meta')]
            if users:
                bad.append(f'DELETED_ASSET_STILL_USED {users[:3]}')
    return bad


def unity_status(root):
    try:
        p = subprocess.run(['unity', 'command', '--project-path', str(root), '--result-only', 'editor', 'status'], capture_output=True, text=True, timeout=30)
        return json.loads(p.stdout)
    except (OSError, ValueError, subprocess.TimeoutExpired):
        return None


def check_compile(root, files):
    if not any(p.endswith('.cs') and p.startswith('Assets/') for st, p in files):
        return []
    st = unity_status(root)
    if not st or st.get('playMode') != 'stopped':
        return ['COMPILE_NOT_CHECKED: changed C# needs the Editor open and not in Play']
    def ev(code):
        return subprocess.run(['unity', 'command', 'script', 'eval', '--project-path', str(root), '--timeout', '120', '--result-only', '--', code], capture_output=True, text=True, timeout=150)
    ev('UnityEditor.AssetDatabase.Refresh();return "ok";')
    for _ in range(100):
        st = unity_status(root)
        if st and st.get('status') == 'ready' and not st.get('compiling') and not st.get('domainReloadInProgress'):
            break
        time.sleep(float(os.environ.get('GATE_POLL', '3')))
    else:
        return [f'COMPILE_DID_NOT_FINISH {st}']
    try:
        c = json.loads(subprocess.run(['unity', 'command', '--project-path', str(root), '--result-only', 'console', 'status'], capture_output=True, text=True, timeout=30).stdout)
    except ValueError:
        return ['COMPILE_NOT_CHECKED: console status unreadable']
    return ['COMPILE_FAILED'] if (c.get('groundTruth') or {}).get('compilationFailed') else []


def check_evidence(root, files, g, ev_cfg):
    paths = g.get('game_paths') or []
    touched = [p for st, p in files if under(p, paths) and '/Editor/' not in p]
    if not touched:
        return []
    want = E.fingerprint(root, ev_cfg.get('runtime', []) + ev_cfg.get('visual', []))
    got = (E.read(root).get('short') or {})
    if got.get('fingerprint') != want:
        return [f'NO_SHORT_PLAY_FOR_THIS_TREE: {len(touched)} game file(s) changed ({touched[0]}). Run play_check.py short, look at the capture, then commit (rule 1, U13). Evidence {got.get("fingerprint")} at {got.get("at")}, tree {want}']
    return []


def check_assets(root, files, g, data):
    if not any(under(p, g.get('art_paths') or []) for st, p in files):
        return []
    accepted = g.get('accepted_check_fails') or []
    rep = root / ((data.get('assets') or {}).get('work_root') or 'output/assets') / 'check-report.json'
    want = E.check_fingerprint(root, data)
    old = json.loads(rep.read_text()) if rep.exists() else {}
    if old.get('run_id') and old.get('fingerprint') == want and old.get('status') in ('PASS', 'FAIL'):
        print(f"gate: assets check reused (run {old['run_id'][:8]} at {old.get('finished')}, same tree {want})")
        fails = old.get('fails') or []
    else:
        st = unity_status(root)
        if not st or st.get('status') != 'ready' or st.get('playMode') != 'stopped':
            return ['ASSETS_NOT_CHECKED: changed art needs the Editor open and not in Play (no report for this tree '
                    f'{want}) -> next: open the Editor, or run assets.py check once and commit again']
        run_id = uuid.uuid4().hex
        tool = os.environ.get('GATE_ASSETS_TOOL') or str(HERE / 'assets.py')
        r = subprocess.run([PY, tool, 'check'], cwd=root, capture_output=True, text=True, env=dict(clean_env(), KIT_CHECK_RUN=run_id), timeout=900)
        got = json.loads(rep.read_text()) if rep.exists() else {}
        tail = ((r.stderr or '') + (r.stdout or '')).strip()[-200:]
        if got.get('run_id') != run_id:
            return [f'ASSETS_NOT_CHECKED: assets.py check wrote no report for this run (exit {r.returncode}): {tail} -> next: fix the cause and commit again']
        if r.returncode not in (0, 1) or (got.get('status') == 'PASS') != (r.returncode == 0):
            return [f'ASSETS_NOT_CHECKED: report and exit code disagree (status {got.get("status")}, exit {r.returncode}): {tail}']
        fails = got.get('fails') or []
    return [f'ASSET_CHECK {f}' for f in fails if not any(a in f for a in accepted)]


KIT_SELFTESTS = (['assets.py', 'selftest'], ['guard.py', 'selftest'], ['protect.py', 'selftest'], ['session_time.py', '--selftest'],
                 ['play_check.py', 'selftest'], ['gate.py', 'selftest'], ['replay.py', 'run'])


def kit_dirs(root, data):
    """project-relative folders of the kit itself (tools, profiles): a change there runs every kit selftest"""
    a = data.get('assets') or {}
    out = []
    for d in (HERE, root / a['profiles_dir'] if a.get('profiles_dir') else HERE.parent / 'profiles'):
        d = Path(d).resolve()
        if root.resolve() in d.parents:
            out.append(str(d.relative_to(root.resolve())))
    return out


def check_kit(root, files, data):
    dirs = kit_dirs(root, data)
    if not any(under(p, dirs) and st != 'D' for st, p in files):
        return []
    bad = []
    for cmd in KIT_SELFTESTS:
        if cmd[0] == 'gate.py' and os.environ.get('GATE_IN_SELFTEST'):
            continue
        if not (HERE / cmd[0]).exists():
            continue
        r = subprocess.run([PY, str(HERE / cmd[0]), *cmd[1:]], cwd=root, capture_output=True, text=True, env=dict(clean_env(), GATE_IN_SELFTEST='1'), timeout=900)
        if r.returncode:
            bad.append(f'KIT_SELFTEST_FAILED {cmd[0]}: ' + ((r.stdout + r.stderr).strip().splitlines() or ['?'])[-1][:200])
    return bad


def run():
    t0 = time.time()
    root = Path(git(Path.cwd(), 'rev-parse', '--show-toplevel').strip())
    cfg = root / '3dkit.config.json'
    if not cfg.exists():
        return 0
    data = json.loads(cfg.read_text())
    h = data.get('harness') or {}
    global PY
    PY = h.get('python') or sys.executable
    g, play, ev_cfg = h.get('gate') or {}, h.get('play') or {}, h.get('evidence') or {}
    files = staged(root)
    if not files:
        return 0
    skip = os.environ.get('GATE_SKIP', '').strip()
    if skip:
        log = root / 'output/hook-state/gate-skips.log'
        log.parent.mkdir(parents=True, exist_ok=True)
        with log.open('a') as f:
            f.write(f"{time.strftime('%F %T')} {len(files)} files, reason: {skip}\n")
        print(f'gate: SKIPPED ({skip}), logged')
        return 0
    fails = []
    fails += check_never(root, files, g)
    fails += check_run_in_background(root, files)
    fails += check_catalog(root, files, (data.get('assets') or {}).get('catalog', ''))
    fails += check_links(root, files)
    fails += check_stage_records(root, files, g)
    fails += check_build_scene(root, play)
    fails += check_guids(root, files)
    fails += check_compile(root, files)
    fails += check_evidence(root, files, g, ev_cfg) if ev_cfg else []
    fails += check_assets(root, files, g, data)
    fails += check_kit(root, files, data)
    for f in fails:
        print('gate FAIL', f)
    print(f"gate {'FAIL' if fails else 'PASS'}: {len(files)} files, {len(fails)} problem(s), {time.time() - t0:.0f}s")
    return 1 if fails else 0


def project_check():
    import importlib.util
    spec = importlib.util.spec_from_file_location('assets_tool', HERE / 'assets.py')
    a = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(a)
    print(a.check_project(C.Kit()))


def install():
    root = Path(git(Path.cwd(), 'rev-parse', '--show-toplevel').strip())
    d = Path(git(root, 'rev-parse', '--git-common-dir').strip())
    d = d if d.is_absolute() else root / d
    hook = d / 'hooks/pre-commit'
    hook.parent.mkdir(exist_ok=True)
    if hook.exists() and '3dkit gate' not in hook.read_text():
        print(f'gate: {hook} exists and is not ours, not touched')
        return 1
    cfg = root / '3dkit.config.json'
    py = ((json.loads(cfg.read_text()).get('harness') or {}).get('python') if cfg.exists() else None) or 'python3'
    me = HERE.resolve()
    rel = str(me.relative_to(root.resolve())) if root.resolve() in me.parents else str(me)
    hook.write_text('#!/bin/sh\n# 3dkit gate (gate.py install; interpreter = harness.python)\nroot="$(git rev-parse --show-toplevel)"\n'
                    f'[ -f "$root/{rel}/gate.py" ] || {{ echo "3dkit gate: $root/{rel}/gate.py not found, commit NOT checked -> next: run gate.py install from where the kit is now" >&2; exit 0; }}\n'
                    f'cd "$root" && exec "{py}" "{rel}/gate.py" run\n')
    hook.chmod(0o755)
    print('gate: installed', hook, f'({py} {rel}/gate.py)')
    return 0


FAKE = '''#!/usr/bin/python3
import json, os, sys
s = ' '.join(sys.argv[1:]); F = os.environ['FAKE_DIR']
st = lambda k: os.path.exists(f'{F}/{k}')
if st('dead'): sys.exit(1)
if 'editor status' in s: print(json.dumps(dict(status='ready', compiling=False, domainReloadInProgress=False, playMode='stopped')))
elif 'console status' in s: print(json.dumps(dict(counts=dict(error=0), groundTruth=dict(compilationFailed=st('compile_bad')))))
else: print(json.dumps(dict(success=True, result='ok')))
'''


FAKE_ASSETS = '''#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
sys.path.insert(0, os.environ['KIT_TOOLS'])
from kitlib import evidence as E
F = os.environ['FAKE_DIR']; root = Path.cwd()
open(F + '/check_called', 'a').write('x')
mode = open(F + '/check_mode').read() if os.path.exists(F + '/check_mode') else 'ok'
if mode == 'crash':
    sys.stderr.write('Traceback (most recent call last): SystemExit: EDITOR_LOCKED by pid 1'); sys.exit(1)
fails = json.loads(open(F + '/check_fails').read()) if os.path.exists(F + '/check_fails') else []
data = json.loads((root / '3dkit.config.json').read_text())
rep = dict(status='FAIL' if fails else 'PASS', run_id=os.environ.get('KIT_CHECK_RUN'), fingerprint=E.check_fingerprint(root, data), finished='now', fails=fails, measured={})
(root / 'output/assets').mkdir(parents=True, exist_ok=True)
(root / 'output/assets/check-report.json').write_text(json.dumps(rep))
sys.exit(1 if fails else 0)
'''


def selftest():
    env0 = clean_env()
    t = Path(tempfile.mkdtemp(prefix='gate-selftest-'))
    (t / 'bin').mkdir(); (t / 'fake').mkdir()
    fake = t / 'bin/unity'; fake.write_text(FAKE); fake.chmod(0o755)
    fa = t / 'bin/fake_assets.py'; fa.write_text(FAKE_ASSETS)
    r_ = t / 'p'
    for d in ('docs', 'Assets/Game/Scr/Editor', 'Assets/Game/Art/x', 'ProjectSettings', 'Library/PackageCache', 'output/assets'):
        (r_ / d).mkdir(parents=True)
    (r_ / '.gitignore').write_text('Library/\noutput/\n')
    cfg = dict(schema=C.CONFIG_SCHEMA, assets=dict(catalog='docs/cat.json'), harness=dict(
        play=dict(scene='Assets/G.unity'), evidence=dict(runtime=['Assets/Game/Scr', 'Assets/G.unity'], visual=['Assets/Game/Art']),
        gate=dict(never_commit=['*.lock', '*/__pycache__/*'], game_paths=['Assets/Game/Scr', 'Assets/G.unity'], art_paths=['Assets/Game/Art'], accepted_check_fails=['hero: tris'], stage_records=['docs/stage/*.md'])))
    (r_ / '3dkit.config.json').write_text(json.dumps(cfg))
    (r_ / 'Assets/G.unity').write_text('%YAML 1.1\n'); (r_ / 'Assets/G.unity.meta').write_text('guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n')
    (r_ / 'ProjectSettings/EditorBuildSettings.asset').write_text('  - enabled: 1\n    path: Assets/G.unity\n')
    (r_ / 'ProjectSettings/ProjectSettings.asset').write_text('  runInBackground: 0\n')
    (r_ / 'Assets/Game/Scr/a.cs').write_text('a'); (r_ / 'Assets/Game/Art/x/m.mat').write_text('%YAML 1.1\nm: {guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa}\n')
    (r_ / 'Assets/Game/Art/x/t.png').write_text('png'); (r_ / 'Assets/Game/Art/x/t.png.meta').write_text('guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n')
    (r_ / 'docs/a.md').write_text('# a\n')
    (r_ / 'docs/stage').mkdir(); (r_ / 'docs/stage/s1.md').write_text('| 단계 | 결과 | 근거 |\n|---|---|---|\n| 0 옛 행 | 통과 | 근거 없음 |\n')
    (r_ / 'output/play').mkdir(parents=True); (r_ / 'output/play/shot.jpg').write_text('jpg')
    env = dict(env0, PATH=f"{t}/bin:{env0['PATH']}", FAKE_DIR=str(t / 'fake'), GATE_POLL='0', GATE_IN_SELFTEST='1', GATE_ASSETS_TOOL=str(fa), KIT_TOOLS=str(HERE))
    def g(*a):
        return subprocess.run(['git', '-c', 'user.name=t', '-c', 'user.email=t@t', *a], cwd=r_, env=env0, capture_output=True, text=True, check=True)
    g('init', '-q'); g('add', '-A'); g('commit', '-qm', 'base')
    def gate(**e):
        return subprocess.run([sys.executable, str(HERE / 'gate.py'), 'run'], cwd=r_, capture_output=True, text=True, env=dict(env, **e), timeout=120)
    cases = []
    def case(name, r, fails, token=''):
        got = r.returncode != 0
        cases.append((name, got == fails and (not token or token in r.stdout), r.stdout[-300:] + r.stderr[-200:]))
    def reset():
        g('reset', '-q', '--hard'); g('clean', '-fdq')
        (r_ / 'Assets/Game/Scr/Editor').mkdir(exist_ok=True)
        for f in (t / 'fake').iterdir(): f.unlink()
    def proof():
        ev = cfg['harness']['evidence']
        E.write(r_, 'short', fingerprint=E.fingerprint(r_, ev['runtime'] + ev['visual']))
    # documents only: quick pass
    (r_ / 'docs/a.md').write_text('# a\n[ok](a.md)\n'); g('add', 'docs/a.md'); case('document: pass', gate(), False)
    reset(); (r_ / 'docs/a.md').write_text('[bad](nope.md)\n'); g('add', 'docs/a.md'); case('broken link', gate(), True, 'BROKEN_LINK')
    reset(); (r_ / 'docs/a.md').write_text('`[code](nope.md)`\n'); g('add', 'docs/a.md'); case('link inside code ignored', gate(), False)
    reset(); (r_ / 'x.lock').write_text(''); g('add', 'x.lock'); case('never commit file', gate(), True, 'NEVER_COMMIT')
    reset(); (r_ / 'ProjectSettings/ProjectSettings.asset').write_text('  runInBackground: 1\n'); g('add', 'ProjectSettings/ProjectSettings.asset'); case('runInBackground left on', gate(), True, 'RUN_IN_BACKGROUND')
    reset(); (r_ / 'ProjectSettings/EditorBuildSettings.asset').write_text('  - enabled: 1\n    path: Assets/Sample.unity\n'); g('add', 'ProjectSettings/EditorBuildSettings.asset'); case('game scene not in build', gate(), True, 'GAME_SCENE_NOT_IN_BUILD')
    reset(); (r_ / 'docs/cat.json').write_text(json.dumps(dict(a=1), indent=2)); g('add', 'docs/cat.json'); case('catalog format', gate(), True, 'CATALOG_FORMAT_CHANGED')
    # stage records: a pass row this commit adds names evidence that exists; old rows and prose are not judged
    def stage(row):
        reset(); f = r_ / 'docs/stage/s1.md'; f.write_text(f.read_text() + row + '\n'); g('add', 'docs/stage/s1.md'); return gate()
    case('stage pass row with its capture', stage('| 1 화면 | 통과 | `output/play/shot.jpg` |'), False)
    case('stage pass row, capture gone', stage('| 1 화면 | 통과 | `output/play/gone.jpg` |'), True, 'STAGE_EVIDENCE_MISSING')
    case('stage pass row naming nothing: a note, not a failure', stage('| 1 화면 | **PASS** | 잘 나옴 |'), False, 'STAGE_PASS_NO_EVIDENCE')
    case("stage pass row with the user's words", stage('| 2 판정 | 통과 | "채택" |'), False)
    case('stage: prose and a FAIL row are not pass rows', stage('짧은 Play를 통과했다.\n| 3 화면 | FAIL | 없음 |'), False)
    case('stage: a :line suffix, a command, a bone name with a slash', stage('| 4 명령 | 통과 (2회) | `play_check.py short P.Run r.txt`, `Ctrl_Foot_L/R`, `output/play/shot.jpg:12` |'), False)
    r = stage('| 5 화면 | 통과 | `shot_small.jpg` |'); case('stage: a bare file name counts as named, old evidence-less row not judged', r, False)
    cases.append(('stage: no note for named evidence or old rows', 'STAGE_PASS_NO_EVIDENCE' not in r.stdout, r.stdout[-200:]))
    # game code
    reset(); (r_ / 'Assets/Game/Scr/a.cs').write_text('b'); g('add', 'Assets/Game/Scr/a.cs'); E.path(r_).unlink(missing_ok=True); case('game code without short Play', gate(), True, 'NO_SHORT_PLAY')
    reset(); (r_ / 'Assets/Game/Scr/a.cs').write_text('b'); g('add', 'Assets/Game/Scr/a.cs'); proof(); case('game code with short Play', gate(), False)
    reset(); (r_ / 'Assets/Game/Scr/a.cs').write_text('b'); g('add', 'Assets/Game/Scr/a.cs'); proof(); (r_ / 'Assets/Game/Scr/a.cs').write_text('c'); g('add', 'Assets/Game/Scr/a.cs'); case('edit after short Play', gate(), True, 'NO_SHORT_PLAY')
    reset(); (r_ / 'Assets/Game/Scr/Editor/e.cs').write_text('b'); g('add', 'Assets/Game/Scr/Editor/e.cs'); case('Editor code needs no short Play', gate(), False)
    reset(); (r_ / 'Assets/Game/Scr/Editor/e.cs').write_text('b'); g('add', 'Assets/Game/Scr/Editor/e.cs'); (t / 'fake/compile_bad').write_text(''); case('compile error', gate(), True, 'COMPILE_FAILED')
    reset(); (r_ / 'Assets/Game/Scr/Editor/e.cs').write_text('b'); g('add', 'Assets/Game/Scr/Editor/e.cs'); (t / 'fake/dead').write_text(''); case('C# without an Editor', gate(), True, 'COMPILE_NOT_CHECKED')
    # references: a material pointing at nothing, an asset deleted while still used
    reset(); (r_ / 'Assets/G.prefab').write_text('%YAML 1.1\nm: {guid: cccccccccccccccccccccccccccccccc}\n'); g('add', 'Assets/G.prefab'); case('missing GUID', gate(), True, 'MISSING_GUID')
    reset(); (r_ / 'Assets/G.prefab').write_text('%YAML 1.1\nm: {guid: 0000000000000000e000000000000000}\n'); g('add', 'Assets/G.prefab'); case('built-in GUID is fine', gate(), False)
    reset(); (r_ / 'Assets/G.prefab').write_text('%YAML 1.1\nm: {guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb}\n'); g('add', 'Assets/G.prefab'); g('commit', '-qm', 'uses t'); g('rm', '-q', 'Assets/Game/Art/x/t.png', 'Assets/Game/Art/x/t.png.meta')
    rep = r_ / 'output/assets/check-report.json'; rep.write_text(json.dumps(dict(fails=[]))); proof(); case('deleted asset still used', gate(), True, 'DELETED_ASSET_STILL_USED')
    g('reset', '-q'); g('checkout', '-q', '--', '.'); (r_ / 'Assets/Game/Art/y').mkdir(parents=True, exist_ok=True); g('mv', 'Assets/Game/Art/x/t.png', 'Assets/Game/Art/y/t.png'); g('mv', 'Assets/Game/Art/x/t.png.meta', 'Assets/Game/Art/y/t.png.meta'); rep.write_text(json.dumps(dict(fails=[]))); proof(); r = gate(); cases.append(('moved asset is not deleted', 'DELETED_ASSET_STILL_USED' not in r.stdout, r.stdout[-200:]))
    g('reset', '-q', '--hard', 'HEAD~1')
    # art check (B44): only a report this run wrote, or one made for exactly this tree, counts
    fk = t / 'fake'
    def art(content):
        (r_ / 'Assets/Game/Art/x/t2.png').write_text(content); g('add', 'Assets/Game/Art/x/t2.png')
    reset(); rep.unlink(missing_ok=True); art('p'); (fk / 'check_fails').write_text(json.dumps(['hero: tris 9 > 8']))
    case('accepted check failure passes', gate(), False)
    (fk / 'check_called').unlink(missing_ok=True); (fk / 'check_mode').write_text('crash')
    case('same tree: report reused', gate(), False, 'reused'); cases.append(('reuse runs no check', not (fk / 'check_called').exists(), ''))
    (fk / 'dead').write_text(''); case('same tree, Editor closed: report reused', gate(), False, 'reused'); (fk / 'dead').unlink()
    art('p2'); case('B44: check died, old report not trusted', gate(), True, 'ASSETS_NOT_CHECKED')
    (fk / 'dead').write_text(''); case('changed art, Editor closed', gate(), True, 'ASSETS_NOT_CHECKED'); (fk / 'dead').unlink()
    (fk / 'check_mode').unlink(); (fk / 'check_fails').write_text(json.dumps(['well: shader X'])); case('new check failure', gate(), True, 'ASSET_CHECK')
    (fk / 'check_fails').write_text('[]'); art('p3'); case('check passes (new tree, new run)', gate(), False)
    case('GATE_SKIP passes and is logged', gate(GATE_SKIP='selftest'), False, 'SKIPPED'); cases.append(('skip logged', 'selftest' in (r_ / 'output/hook-state/gate-skips.log').read_text(), ''))
    # B54: `commit --only <paths>` and `commit -a` hand the hook a temporary index (GIT_INDEX_FILE): the gate judges that one
    hook = r_ / '.git/hooks/pre-commit'
    gp = HERE / 'gate.py'
    hook.write_text(f'#!/bin/sh\nexec "{sys.executable}" "{gp}" run\n'); hook.chmod(0o755)
    def commit(*a):
        r = subprocess.run(['git', '-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '-m', 'x', *a], cwd=r_, env=env,
                           capture_output=True, text=True, timeout=120)
        r.stdout, r.stderr = r.stdout + r.stderr, ''
        return r
    for how, extra in (('--only <path>', ['--only', '--', 'Assets/Game/Scr/a.cs']), ('-a', ['-a'])):
        reset(); E.path(r_).unlink(missing_ok=True); (r_ / 'Assets/Game/Scr/a.cs').write_text(f'd {how}')
        case(f'B54: commit {how} judges what it commits', commit(*extra), True, 'NO_SHORT_PLAY')
    hook.unlink()
    shutil.rmtree(t, ignore_errors=True)
    bad = [c for c in cases if not c[1]]
    for c in bad:
        print('FAIL', c[0], c[2])
    print(f'gate selftest {"PASS" if not bad else "FAIL"} ({len(cases)} cases)')
    return not bad


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else ''
    if cmd == 'run':
        return run()
    if cmd == 'install':
        return install()
    if cmd == 'selftest':
        return 0 if selftest() else 1
    if cmd == '_project_check':
        project_check(); return 0
    print(__doc__)
    return 2


if __name__ == '__main__':
    sys.exit(main())

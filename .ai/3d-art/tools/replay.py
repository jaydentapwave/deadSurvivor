#!/usr/bin/env python3
"""3dkit: replay past defects. Put a known fault into a copy of a project and see that today's tools still stop on it
(005 plan bundle E; the pack evaluations asked for a fixed fixture, four outcomes, and project cases kept apart from the kit's).

    replay.py run [--only ID,..] [--keep]              the kit's sample cases (tools/replay-sample.json) on a sample project
                                                       built in a temp folder. The commit gate runs this; it writes nothing
                                                       in the project.
    replay.py project [--cases FILE] [--only ID,..]    the sample cases, then the project's own cases (harness.replay_cases,
                                                       real catalog and measured report). Result: output/replay/result.json
    replay.py selftest                                 = run

A case (shape: tools/schemas/replay-cases.schema.json) names a fault, how to put it in, what the tool says when it stops on
it ('code', a regular expression) and the outcome expected, written before running. The run fails on any difference.

    detected  the tool stopped and named this fault: 'code' is in what it said. An exit code alone is not enough (r1: a
              case "caught" by a later, unrelated stop)
    crashed   the tool died: a Python traceback, an unexpected exception, a timeout. Never counted as a detection
    missed    the tool ran to its end, or stopped for another reason, without naming this fault
    not_run   nothing was executed: a person judges it (kind manual), the case is broken, its control failed, or the
              commit gate already runs the selftest the case points at (kind selftest under the gate: DELEGATED)

Kinds: judge (the assets.py check verdict on a measured report: new FAIL lines against the unchanged report), project (the
catalog check of assets.py selftest), cli (a command run in the case folder), hook (a hook command fed a JSON payload:
deny, ask or exit 2), selftest (a line of a tool's own selftest: where the kit already covers the fault; 'covered_by' on
other kinds marks the same overlap as a candidate to merge), manual (no tool can tell; a person does).

Controls: the sample must pass every tool before a fault means anything (otherwise every case would look detected).
Project judge cases compare against the unchanged real report; project catalog cases need the real catalog to pass.

Safety: no credits, no network, no real Editor. Sample cases run in a temp copy with a fake `unity` first on PATH. Project
cases read the real project, may write only under {tmp}, and see a fake Editor that is not running. Placeholders in
run/payload/env/mutate strings: {py} {tools} {root} (the folder the case runs in) {tmp} (a temp folder of the case)."""
import collections
import contextlib
import copy
import hashlib
import importlib.util
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from kitlib import config as C, editor_lock as L, pipeline as P, schema as S  # noqa: E402
from kitlib.errors import code_of, fail  # noqa: E402

SAMPLE_CASES = HERE / 'replay-sample.json'
CASES_SCHEMA = '3dkit-replay-cases/1'
LABEL = dict(detected='탐지', crashed='예외', missed='미탐', not_run='미실행')
# what would let a case reach past its sandbox (the real config, a held lock) or wave a check through
STRIP = ('KIT_EDITOR_LOCK', 'KIT3D_CONFIG', 'KIT_CHECK_RUN', 'FULL_REASON', 'REVIEW_OK', 'INPUT_OK', 'REGEN_OK', 'GATE_SKIP',
         'CLAUDE_PROJECT_DIR', 'TRIPO_API_KEY')
TRACEBACK = 'Traceback (most recent call last)'
LIT = C.DEFAULT_SHADERS['lit']
SAMPLE_SPEC = '# 아트 기준서 (견본)\n\n- 상태: **r1 승인**\n- 승인(r1): 2026-01-01 — "진행"\n\n| 번호 | 무엇 | 값 |\n|---|---|---|\n| K1 | 소품 높이 | 0.7m |\n'


def _module(name, file):
    spec = importlib.util.spec_from_file_location(name, HERE / file)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


AS = _module('kit_assets', 'assets.py')
FAKE_UNITY = _module('kit_play_check', 'play_check.py').FAKE


class Broken(Exception):
    """the case itself is wrong (a path that names nothing, a write outside {tmp}): not_run, never a tool outcome"""


# ---------------- small helpers ----------------
def child_env(root, extra=None):
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_') and k not in STRIP}
    env['CLAUDE_PROJECT_DIR'] = str(root)
    env.update(extra or {})
    return env


def fill(v, sub):
    if isinstance(v, str):
        for k, x in sub.items():
            v = v.replace('{' + k + '}', x)
        return v
    if isinstance(v, list):
        return [fill(x, sub) for x in v]
    if isinstance(v, dict):
        return {k: fill(x, sub) for k, x in v.items()}
    return v


def tail(out, n=160):
    lines = [x for x in out.strip().splitlines() if x.strip()]
    return lines[-1][:n] if lines else '(no output)'


def line_of(out, m):
    s = out.rfind('\n', 0, m.start()) + 1
    e = out.find('\n', m.end())
    return out[s:len(out) if e < 0 else e].strip()[:200]


# ---------------- changes: JSON paths, text, files ----------------
def _parts(path):
    parts = re.findall(r'\[[^\]]*\]|[^.\[\]]+', path)
    if not parts:
        raise Broken(f'empty path {path!r}')
    return parts


def _get(d, dotted):
    for k in dotted.split('.'):
        if not isinstance(d, dict) or k not in d:
            return None
        d = d[k]
    return d


def _keys(node, part, create):
    if part == '*':
        if isinstance(node, dict):
            return list(node)
        return list(range(len(node))) if isinstance(node, list) else []
    if part.startswith('['):
        sel = part[1:-1]
        if not isinstance(node, list):
            return []
        if sel.isdigit():
            return [int(sel)] if int(sel) < len(node) else []
        k, _, want = sel.partition('=')
        return [i for i, x in enumerate(node) if isinstance(x, dict) and str(_get(x, k)) == want]
    return [part] if isinstance(node, dict) and (part in node or create) else []


def slots(doc, path, create=False):
    """(container, key) pairs a path names: 'items[id=prop].prefab.height', 'budgets.*.cap', 'assets[tripo.asset=zz]',
    'scenes[0]'. set may create the last key; every other change needs it to exist."""
    parts = _parts(path)
    nodes = [doc]
    for i, part in enumerate(parts):
        last = i == len(parts) - 1
        found = [(n, k) for n in nodes for k in _keys(n, part, create and last)]
        if not found:
            raise Broken(f'{path}: nothing at {part}')
        if last:
            return found
        nodes = [n[k] for n, k in found]
    return []


def edit_json(doc, op):
    if 'set' in op:
        for n, k in slots(doc, op['set'], create=True):
            n[k] = copy.deepcopy(op['value'])
    elif 'del' in op:
        for n, k in sorted(slots(doc, op['del']), key=lambda s: s[1] if isinstance(s[1], int) else 0, reverse=True):
            del n[k]
    elif 'scale' in op or 'add' in op:
        for n, k in slots(doc, op['scale'] if 'scale' in op else op['add']):
            if isinstance(n[k], bool) or not isinstance(n[k], (int, float)):
                raise Broken(f'{op}: not a number ({type(n[k]).__name__})')
            n[k] = n[k] * op['by'] if 'scale' in op else n[k] + op['by']
    elif 'append' in op:
        for n, k in slots(doc, op['append']):
            if not isinstance(n[k], list):
                raise Broken(f'{op}: not a list')
            n[k].append(copy.deepcopy(op['value']))
    else:
        raise Broken(f'unknown change {op}')


def edit_text(text, op):
    if 'append_text' in op:
        return text + op['append_text']
    new, n = re.subn(op['replace'], op.get('with', ''), text, flags=re.M)
    if not n:
        raise Broken(f"replace {op['replace']!r}: no match")
    return new


def change_docs(case, docs):
    """judge/project cases change in-memory documents: config, catalog, measured"""
    for op in case.get('mutate') or []:
        if op.get('file') not in docs:
            raise Broken(f"a {case['kind']} case changes {', '.join(docs)}, not {op.get('file')!r}")
        edit_json(docs[op['file']], op)


def change_files(case, work, tmp, sub, writable):
    """cli/hook cases change files in the folder they run in; a project case writes only under {tmp}"""
    def where(p):
        p = Path(fill(p, sub))
        p = p if p.is_absolute() else work / p
        if not writable and Path(tmp).resolve() not in p.resolve().parents:
            raise Broken(f'{p}: a project case writes only under {{tmp}}')
        return p
    for op in case.get('mutate') or []:
        if 'write' in op:
            p = where(op['write'])
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(fill(op.get('text', ''), sub))
            if op.get('mode'):
                p.chmod(int(op['mode'], 8))
        elif 'rm' in op:
            p = where(op['rm'])
            if p.is_dir():
                shutil.rmtree(p)
            elif p.exists():
                p.unlink()
            else:
                raise Broken(f"rm {op['rm']}: no such file")
        elif 'copy' in op:
            src = Path(fill(op['copy'], sub))
            src = src if src.is_absolute() else work / src
            p = where(op['to'])
            p.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(src, p)
        elif 'file' in op:
            p = where(op['file'])
            if not p.is_file():
                raise Broken(f"{op['file']}: no such file")
            if 'append_text' in op or 'replace' in op:
                p.write_text(edit_text(p.read_text(), op))
            else:
                doc = json.loads(p.read_text())
                edit_json(doc, op)
                p.write_text(json.dumps(doc, ensure_ascii=False, indent=1) + '\n')
        else:
            raise Broken(f'unknown change {op}')


# ---------------- the projects cases run against ----------------
def build_sample(d, py):
    """a small project every sample case starts from: config, catalog (adopted props and a hero, a superseded pair, a budget
    bound to one asset, an asset ready to freeze), measured report, approved spec, concepts, one frozen request with spend,
    the journal of an asset removed from the catalog, a probe for the fake Editor. It passes every tool (sample_health)."""
    d.mkdir(parents=True, exist_ok=True)
    prof = C.load_profile('midpoly')
    tex_import = prof['import']['texture']

    def w(rel, data):
        p = d / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        if isinstance(data, bytes):
            p.write_bytes(data)
        elif isinstance(data, str):
            p.write_text(data)
        else:
            p.write_text(json.dumps(data, ensure_ascii=False, indent=1) + '\n')
        return p

    cfg = dict(schema=C.CONFIG_SCHEMA,
               assets=dict(profile='midpoly', catalog='docs/art/asset-catalog.json', content_root='Assets/Game', art_root='Assets/Game/Art',
                           work_root='output/assets', spec='docs/art/spec.md'),
               tripo=dict(new_work_root='output/assets'),
               harness=dict(python=py, full_runners=[dict(match='play_check.py', full_if=r"play_check\.py['\"]?\s+full\b")],
                            full_override_env='FULL_REASON', review=dict(max_rounds_per_topic=2, override_env='REVIEW_OK'),
                            play=dict(scene='Assets/G.unity', probe_file='P.cs', probe_dir='pd', short_pass='^ok', full_entry='P.Full',
                                      full_result='r.txt', perf=dict(probe_file='P.cs', stand='P.Stand', swing='P.Swing')),
                            evidence=dict(runtime=['src'], visual=['Assets/Game/Art'])))

    def built(i, kind, folder, **kw):
        return dict(id=i, kind=kind, status='adopted', method='build', folder=folder, source=dict(texture=f'output/assets/{i}/src/basecolor.png'), **kw)

    assets = [built('prop', 'prop_small', 'Art/Field/prop', height=0.7,
                    tripo=dict(asset='prop', budget='b', seed=1, face_limit=1000, concept='output/assets/prop/concept/front.png')),
              built('well', 'prop_large', 'Art/Field/well', height=1.4),
              built('hero', 'hero', 'Art/Hero/hero', height=1.2, anim=dict(clips=['idle', 'walk'])),
              dict(built('pot', 'prop_small', 'Art/Field/pot', height=0.7), status='trial'),
              built('pot_r2', 'prop_small', 'Art/Field/pot_r2', height=0.7),
              dict(id='zz', kind='prop_small', status='trial', method='build', folder='Art/Field/zz',
                   tripo=dict(asset='zz', budget='z', seed=7, face_limit=500, concept='output/assets/zz/concept/front.png'))]
    budgets = dict(b=dict(cap=100, approval='견본 사용자 원문: 소품 하나 진행', assets=['prop']),
                   z=dict(cap=100, approval='견본 사용자 원문: 진행'))
    w(C.CONFIG_NAME, cfg)
    w('docs/art/asset-catalog.json', dict(schema=C.CATALOG_SCHEMA, project=dict(trial_roots=['Art/Tests']), budgets=budgets, assets=assets))
    for a in assets:
        if a.get('source'):
            w(a['source']['texture'], b'tex')
        if a.get('tripo'):
            w(a['tripo']['concept'], f"png {a['id']}".encode())     # each concept its own bytes: a swap changes the sha
    spec = w('docs/art/spec.md', SAMPLE_SPEC)
    w('docs/art/spec.approval.json', dict(schema='art-spec-approval/1', revision='r1', approved_on='2026-01-01', user_quote='진행',
                                          sha256=hashlib.sha256(spec.read_bytes()).hexdigest()))
    std = prof['standards']

    def item(a, tris, bones=0):
        t = std[a['kind']]['texture']
        tex = dict(width=t, height=t, mip=True, alpha=tex_import.get('alpha', 'None'),
                   android=f"{tex_import['platforms']['Android']}@{t}", ios=f"{tex_import['platforms']['iPhone']}@{t}")
        return dict(id=a['id'], model_child=True, folder_exists=True, material=dict(shader=LIT, props={}), textures=dict(albedo=tex),
                    prefab=dict(tris=tris, bones=bones, materials=1, height=a.get('height', 1.0), feet=0.0, centre=0.0, shadows=['On'],
                                materialPaths=[f"Assets/Game/{a['folder']}/{a['id']}.mat"]))
    by = {a['id']: a for a in assets}
    shown = ('prop', 'well', 'hero', 'pot_r2')
    scene = dict(scene='Assets/G.unity', deps=[f"Assets/Game/{by[i]['folder']}/{i}.prefab" for i in shown],
                 materials=[dict(path=f"Assets/Game/{by[i]['folder']}/{i}.mat", shader=LIT) for i in shown], textures=[])
    w('output/assets/check-report.json', dict(status='PASS', measured=dict(
        items=[item(by['prop'], 600), item(by['well'], 2000), item(by['hero'], 6000, 60), item(by['pot_r2'], 600)],
        scenes=[scene], project_problems=[])))
    w('.env.local', 'TRIPO_API_KEY=replay-sample-not-a-key\n')     # freeze redacts with the key; the sample has no Tripo CLI to send it to
    w('P.cs', '')
    w('src/a.cs', 'a')
    w('pd/probe/old_capture.jpg', 'old')
    env = child_env(d)
    r = subprocess.run([py, str(HERE / 'assets.py'), 'gen', 'freeze', 'prop'], cwd=d, env=env, capture_output=True, text=True, timeout=120)
    if r.returncode:
        fail('SAMPLE_BROKEN', 'gen freeze prop: ' + tail(r.stdout + r.stderr), 'the sample no longer passes gen freeze: fix the tool or build_sample')
    jf = d / 'output/assets/prop/tripo/journal.json'
    j = json.loads(jf.read_text())
    j.update(state='DOWNLOADED', credits_task=40)
    jf.write_text(json.dumps(j, ensure_ascii=False, indent=1))
    w('output/assets/gone/tripo/journal.json', dict(state='DOWNLOADED', credits_task=30))   # removed from the catalog (B39)
    w('output/assets/gone/tripo/request-frozen.json', dict(scope='BUDGET_b'))
    for args in (('init', '-q'), ('add', '-A'), ('commit', '-qm', 'sample')):
        subprocess.run(['git', '-c', 'user.name=replay', '-c', 'user.email=replay@kit', '-c', 'core.hooksPath=/dev/null', *args],
                       cwd=d, env=env, check=True, capture_output=True)
    return d


class Base:
    """a project the cases run against: the sample (a temp folder this run built) or the real project (read only)"""

    def __init__(self, name, root, py, sample):
        self.name, self.root, self.py, self.sample = name, Path(root), py, sample
        self.cfg_path = self.root / C.CONFIG_NAME
        self.cfg = json.loads(self.cfg_path.read_text())
        a = self.cfg['assets']
        self.cat = json.loads((self.root / a['catalog']).read_text())
        rep = self.root / a['work_root'] / 'check-report.json'
        self.measured = json.loads(rep.read_text()).get('measured') if rep.is_file() else None
        self.controls = {}
        self.judge0 = self._control('judge', lambda: None if self.measured is None else set(P.judge(self.kit(), self.measured)[1]))
        self._control('catalog', lambda: AS.check_project(self.kit()))

    def _control(self, name, fn):
        try:
            v = fn()
            self.controls[name] = ('ok', v)
            return v
        except SystemExit as e:
            self.controls[name] = ('fail', code_of(e))
        except AssertionError as e:
            self.controls[name] = ('fail', repr(e.args[0]) if e.args else 'AssertionError')
        except Exception as e:
            self.controls[name] = ('fail', f'{type(e).__name__}: {e}')
        return None

    def kit(self, cfg=None, cat=None):
        return C.Kit(self.cfg_path, catalog=copy.deepcopy(self.cat if cat is None else cat), data=copy.deepcopy(self.cfg if cfg is None else cfg))

    def folder(self, tmp):
        """where a cli/hook case runs: a fresh copy of the sample, or the real project itself"""
        if not self.sample:
            return self.root
        d = Path(tmp) / 'p'
        shutil.copytree(self.root, d, symlinks=True)
        return d


# ---------------- one case ----------------
def stopped_for(case, said):
    """a tool refused: the fault is detected only if the refusal names it"""
    if re.search(case['code'], said):
        return 'detected', said
    return 'missed', f'stopped for another reason: {said}'


def run_judge(case, base, tmp, cache):
    if base.measured is None:
        return 'not_run', 'NO_CHECK_REPORT: run assets.py check once (its report is the measured base)'
    if base.judge0 is None:
        return 'not_run', f"CONTROL_FAILED judge: {base.controls['judge'][1]}"
    docs = dict(config=copy.deepcopy(base.cfg), catalog=copy.deepcopy(base.cat), measured=copy.deepcopy(base.measured))
    change_docs(case, docs)
    try:
        fails = set(P.judge(base.kit(docs['config'], docs['catalog']), docs['measured'])[1])
    except SystemExit as e:
        return stopped_for(case, code_of(e))
    except Exception as e:
        return 'crashed', f'{type(e).__name__}: {e}'
    new = sorted(fails - base.judge0)
    hit = [f for f in new if re.search(case['code'], f)]
    if hit:
        return 'detected', '; '.join(hit[:2])
    return 'missed', '; '.join(new[:2]) or 'no new FAIL'


def run_catalog(case, base, tmp, cache):
    if base.controls['catalog'][0] != 'ok':
        return 'not_run', f"CONTROL_FAILED catalog: {base.controls['catalog'][1]}"
    docs = dict(config=copy.deepcopy(base.cfg), catalog=copy.deepcopy(base.cat))
    change_docs(case, docs)
    try:
        AS.check_project(base.kit(docs['config'], docs['catalog']))
    except AssertionError as e:
        return stopped_for(case, repr(e.args[0]) if e.args else 'AssertionError')
    except SystemExit as e:
        return stopped_for(case, code_of(e))
    except Exception as e:
        return 'crashed', f'{type(e).__name__}: {e}'
    return 'missed', 'the catalog check passed'


def fake_editor(tmp, state, work, result):
    b = Path(tmp) / 'bin'
    b.mkdir(exist_ok=True)
    (b / 'unity').write_text(FAKE_UNITY)
    (b / 'unity').chmod(0o755)
    s = Path(tmp) / 'fake'
    s.mkdir(exist_ok=True)
    for k, v in state.items():
        (s / k).write_text(str(v))
    play = (json.loads((work / C.CONFIG_NAME).read_text()).get('harness') or {}).get('play') or {}
    return dict(PATH=f"{b}{os.pathsep}{os.environ.get('PATH', '')}", FAKE_DIR=str(s), FAKE_RESULT=result,
                FAKE_RESULT_DIR=str(work / play.get('probe_dir', 'output/probe') / 'probe'), PLAY_CHECK_TIME_SCALE='0.002')


def run_command(case, base, tmp, cache):
    work = base.folder(tmp)
    sub = dict(py=base.py, tools=str(HERE), root=str(work), tmp=str(tmp))
    change_files(case, work, tmp, sub, base.sample)
    state = dict(case.get('fake_unity') or {}) if base.sample else dict(dead=1)     # the real project never meets a live Editor
    env = child_env(work, fake_editor(tmp, state, work, case.get('fake_result', 'r.txt')))
    env.update(fill(case.get('env') or {}, sub))
    cmd = fill(case['run'], sub)
    stdin = json.dumps(fill(case['payload'], sub), ensure_ascii=False) if 'payload' in case else None
    hold = L.EditorLock(work, 'replay: another session') if case.get('hold_editor_lock') else contextlib.nullcontext()
    try:
        with hold:
            r = subprocess.run(cmd, cwd=work, input=stdin, capture_output=True, text=True, env=env, timeout=case.get('timeout', 120))
    except subprocess.TimeoutExpired:
        return 'crashed', f"TIMEOUT after {case.get('timeout', 120)}s"
    except OSError as e:
        raise Broken(f'cannot start {cmd[0]}: {e}')
    out = r.stdout + r.stderr
    if TRACEBACK in out:
        return 'crashed', tail(out)
    if case['kind'] == 'hook':
        stopped = r.returncode == 2 or bool(re.search(r'"permissionDecision"\s*:\s*"(deny|ask)"', r.stdout))
    else:
        want = case.get('exit', 'nonzero')
        stopped = r.returncode != 0 if want == 'nonzero' else r.returncode == want
    gone = [p for p in case.get('keeps') or [] if not (work / p).exists()]
    m = re.search(case['code'], out, re.M)
    if stopped and m and not gone:
        return 'detected', line_of(out, m)
    return 'missed', f'exit {r.returncode}' + (f', removed {gone}' if gone else '') + f': {tail(out)}'


def run_selftest(case, base, tmp, cache):
    if os.environ.get('GATE_IN_SELFTEST'):
        return 'not_run', 'DELEGATED: the commit gate runs this selftest itself'
    cmd = fill(case['run'], dict(py=base.py, tools=str(HERE), root=str(base.root), tmp=str(tmp)))
    key = (str(base.root), tuple(cmd))
    if key not in cache:
        try:
            r = subprocess.run(cmd, cwd=base.root, capture_output=True, text=True, env=child_env(base.root), timeout=case.get('timeout', 900))
            cache[key] = (r.returncode, r.stdout + r.stderr)
        except subprocess.TimeoutExpired:
            cache[key] = (None, 'TIMEOUT')
    rc, out = cache[key]
    m = re.search(case['code'], out, re.M)
    if m:
        return 'detected', line_of(out, m)
    if rc is None or TRACEBACK in out:
        return 'crashed', tail(out)
    return 'missed', f'exit {rc}: no line matches; {tail(out)}'


def run_manual(case, base, tmp, cache):
    return 'not_run', case.get('why', '')


RUN = dict(judge=run_judge, project=run_catalog, cli=run_command, hook=run_command, selftest=run_selftest, manual=run_manual)


# ---------------- case files ----------------
def load_cases(path, where):
    data = json.loads(Path(path).read_text())
    bad = S.check(data, 'replay-cases')
    ids = set()
    for c in ([] if bad else data['cases']):
        k, i = c['kind'], c['id']
        if i in ids:
            bad.append(f'duplicate id {i}')
        ids.add(i)
        if k != 'manual':
            if not c.get('code'):
                bad.append(f"{i}: a {k} case needs 'code' (what the tool says when it stops on this fault)")
            else:
                try:
                    re.compile(c['code'])
                except re.error as e:
                    bad.append(f'{i}: code is not a regular expression ({e})')
        if k in ('cli', 'hook', 'selftest') and not c.get('run'):
            bad.append(f"{i}: a {k} case needs 'run'")
        if k == 'hook' and 'payload' not in c:
            bad.append(f"{i}: a hook case needs 'payload'")
        if k == 'manual' and c['expect'] != 'not_run':
            bad.append(f'{i}: a manual case runs nothing; expect not_run')
        if where == 'sample' and c.get('base') == 'project':
            bad.append(f'{i}: a kit sample case cannot read a project')
    item = S.load('replay-cases')['properties']['cases']['items']
    controls = [dict(c, expect='detected') for c in data.get('controls') or []] if not bad else []
    for c in controls:
        bad += [f"controls {c.get('id')}: {e}" for e in S.errors(c, item)]
        if c.get('kind') in ('manual', 'selftest') or not c.get('code') or not c.get('run'):
            bad.append(f"controls {c.get('id')}: a control is a cli/hook/judge/project run with 'code' (the line that says it passed)")
    if bad:
        fail('REPLAY_CASES_INVALID', f'{path}: ' + '; '.join(bad[:4]), 'fix the case file (shape: tools/schemas/replay-cases.schema.json)')
    return data['cases'], controls


def run_cases(cases, bases, default, only, tmp_root, label):
    rows, cache = [], {}
    for c in cases:
        if only and c['id'] not in only:
            continue
        t0 = time.time()
        tmp = Path(tempfile.mkdtemp(prefix=f"{c['id']}-", dir=tmp_root))
        try:
            got, ev = RUN[c['kind']](c, bases[c.get('base', default)], tmp, cache)
        except Broken as e:
            got, ev = 'not_run', f'CASE_BROKEN {e}'
        rows.append(dict(set=label, id=c['id'], kind=c['kind'], src=c.get('src', ''), fault=c['fault'], expect=c['expect'], result=got,
                         evidence=str(ev)[:300], seconds=round(time.time() - t0, 2), covered_by=c.get('covered_by')))
    return rows


def as_expected(r):
    return r['result'] == r['expect'] or r['evidence'].startswith('DELEGATED')


def show(rows, title):
    print(f'== {title} ==')
    for r in rows:
        mark = '' if as_expected(r) else f"  <<예상 {LABEL[r['expect']]}"
        print(f"{r['id']:<5}{LABEL[r['result']]:<4} {r['kind']:<8} {r['src'][:14]:<14} {r['fault'][:40]} | {r['evidence'][:110]}{mark}")
    n = collections.Counter(r['result'] for r in rows)
    off = [r for r in rows if not as_expected(r)]
    print(f"{title} {len(rows)}건: 탐지 {n['detected']}, 예외 {n['crashed']}, 미탐 {n['missed']}, 미실행 {n['not_run']}, 예상과 다름 {len(off)}")
    dup = [f"{r['id']}~{r['covered_by']}" for r in rows if r.get('covered_by')]
    if dup:
        print(f'자체 시험과 겹침(합치기 후보) {len(dup)}건: ' + ', '.join(dup))
    return off


# ---------------- sample controls and commands ----------------
HEALTH = [dict(id='freeze', kind='cli', run=['{py}', '{tools}/assets.py', 'gen', 'freeze', 'zz'], exit=0, code='^FROZEN'),
          dict(id='short-play', kind='cli', run=['{py}', '{tools}/play_check.py', 'short', 'P.Run', 'r.txt'], exit=0, code='^SHORT PASS'),
          dict(id='guard', kind='cli', run=['{py}', '-B', '{tools}/guard.py', 'pretool'], exit=0, code=r'\A\s*\Z',
               payload=dict(tool_name='Bash', tool_input=dict(command='echo replay'), cwd='{root}'))]


def sample_health(base, tmp):
    """the sample passes every tool with no fault in it; otherwise a fault proves nothing"""
    bad = [f'{k}: {v}' for k, (st, v) in base.controls.items() if st != 'ok']
    if base.judge0:
        bad.append(f'judge FAIL {sorted(base.judge0)[:2]}')
    for h in HEALTH:
        got, ev = run_command(h, base, Path(tempfile.mkdtemp(prefix='health-', dir=tmp)), {})
        if got != 'detected':
            bad.append(f"{h['id']}: {ev}")
    return bad


_GUARD = ['{py}', '-B', '{tools}/guard.py', 'pretool']
# the runner's own counterexamples: each case would be counted wrongly by a runner that trusts exit codes or printed words
RUNNER_CHECKS = [
    ('traceback that prints the code', 'crashed', dict(kind='cli', run=['{py}', '-c', "raise KeyError('NO_APPROVAL')"], code='NO_APPROVAL')),
    ('stopped for another reason', 'missed', dict(kind='cli', run=['{py}', '-c', "import sys; sys.exit('MISSING_KEY:x')"], code='NO_APPROVAL')),
    ('named the fault but did not stop', 'missed', dict(kind='cli', run=['{py}', '-c', "print('NO_APPROVAL')"], code='NO_APPROVAL')),
    ('stopped with another exit code', 'missed', dict(kind='cli', run=['{py}', '-c', "import sys; print('EDITOR_NOT_READY'); sys.exit(1)"],
                                                      code='EDITOR_NOT_READY', exit=4)),
    ('stopped but removed what it must keep', 'missed', dict(kind='cli', code='EDITOR_NOT_READY', keeps=['pd/probe/old_capture.jpg'],
                                                             run=['{py}', '-c', "import os, sys; os.remove('pd/probe/old_capture.jpg'); sys.exit('EDITOR_NOT_READY')"])),
    ('hung', 'crashed', dict(kind='cli', run=['{py}', '-c', 'import time; time.sleep(30)'], code='x', timeout=1)),
    ('hook allowed it', 'missed', dict(kind='hook', run=_GUARD, code='.', payload=dict(tool_name='Bash', tool_input=dict(command='echo x'), cwd='{root}'))),
    ('judge: no change, no new FAIL', 'missed', dict(kind='judge', code='.')),
    ('a path that names nothing', 'not_run', dict(kind='judge', code='.', mutate=[dict(file='measured', set='items[id=nope].prefab.height', value=1)])),
    ('catalog check passes', 'missed', dict(kind='project', code='.')),
]


def runner_checks(base, tmp):
    """the outcome rules on cases whose right answer is known; also: a project case cannot write outside {tmp}, a selftest
    case under the commit gate is delegated"""
    bad = []
    for name, want, case in RUNNER_CHECKS:
        c = dict(case, id='runner', fault=name, expect='detected')
        rows = run_cases([c], dict(sample=base), 'sample', set(), tmp, 'runner')
        if rows[0]['result'] != want:
            bad.append(f"{name}: {rows[0]['result']} (want {want}) {rows[0]['evidence'][:80]}")
    t = Path(tempfile.mkdtemp(dir=tmp))
    try:
        change_files(dict(mutate=[dict(write='x.txt', text='x')]), base.root, t, {}, writable=False)
        bad.append('a project case wrote outside {tmp}')
    except Broken:
        pass
    os.environ['GATE_IN_SELFTEST'] = '1'
    try:
        if not run_selftest(dict(run=['{py}', '-c', 'print(1)'], code='1'), base, t, {})[1].startswith('DELEGATED'):
            bad.append('a selftest case ran under the commit gate')
    finally:
        if not GATE:
            os.environ.pop('GATE_IN_SELFTEST', None)
    return bad, len(RUNNER_CHECKS) + 2


GATE = os.environ.get('GATE_IN_SELFTEST')


def sample_base(tmp, py):
    base = Base('sample', build_sample(tmp / 'sample', py), py, True)
    bad = sample_health(base, tmp)
    if bad:
        print('SAMPLE_BROKEN ' + '; '.join(bad))
        return None
    bad, n = runner_checks(base, tmp)
    if bad:
        print('RUNNER_BROKEN ' + '; '.join(bad))
        return None
    print(f'견본 대조 PASS(결함 없는 견본이 도구를 모두 통과), 실행기 반례 {n}건 PASS')
    return base


def finish(off, title):
    if off:
        print(f"REPLAY FAIL ({title}): {len(off)} case(s) not as expected: " + ', '.join(f"{r['id']} {LABEL[r['result']]}" for r in off[:6]))
        return 1
    print(f'REPLAY PASS ({title})')
    return 0


def cmd_run(only, keep):
    tmp = Path(tempfile.mkdtemp(prefix='kit-replay-'))
    try:
        base = sample_base(tmp, sys.executable)
        if base is None:
            print('REPLAY FAIL (sample): the sample project or the runner itself is broken (line above)')
            return 1
        rows = run_cases(load_cases(SAMPLE_CASES, 'sample')[0], dict(sample=base), 'sample', only, tmp, 'sample')
        return finish(show(rows, '견본'), 'sample')
    finally:
        if keep:
            print('kept', tmp)
        else:
            shutil.rmtree(tmp, ignore_errors=True)


def cmd_project(only, keep, cases_file):
    cfg_path = C.find_config()
    root = cfg_path.parent
    h = json.loads(cfg_path.read_text()).get('harness') or {}
    py = h.get('python') or sys.executable
    rel = cases_file or h.get('replay_cases')
    tmp = Path(tempfile.mkdtemp(prefix='kit-replay-'))
    try:
        sample = sample_base(tmp, py)
        if sample is None:
            print('REPLAY FAIL (sample): the sample project or the runner itself is broken (line above)')
            return 1
        proj = Base('project', root, py, False)
        bases = dict(sample=sample, project=proj)
        rows = run_cases(load_cases(SAMPLE_CASES, 'sample')[0], bases, 'sample', only, tmp, 'sample')
        pcases, pcontrols = load_cases(root / rel, 'project') if rel else ([], [])
        crows = run_cases(pcontrols, bases, 'project', set(), tmp, 'control')
        prows = run_cases(pcases, bases, 'project', only, tmp, 'project')
        off = show(rows, '견본')
        for r in crows:
            print(f"프로젝트 대조 {r['id']} {'PASS' if r['result'] == 'detected' else 'FAIL'}: {r['fault']} | {r['evidence'][:110]}")
        off += [r for r in crows if r['result'] != 'detected']
        j0 = proj.controls['judge']
        accepted = (h.get('gate') or {}).get('accepted_check_fails') or []
        if j0[0] == 'ok' and j0[1] is not None:
            left = [f for f in j0[1] if not any(a in f for a in accepted)]
            scenes = [s['scene'] for s in proj.measured['scenes']]
            print(f"프로젝트 대조: check 보고서 FAIL {len(j0[1])}건(받아들인 것 밖 {len(left)}건{': ' + left[0][:80] if left else ''}), 장면 {scenes}")
        else:
            print(f'프로젝트 대조: check 보고서 {j0[1] or "없음"}')
        print(f"프로젝트 대조: 카탈로그 검사 {proj.controls['catalog'][0]} {str(proj.controls['catalog'][1])[:120]}")
        if rel:
            off += show(prows, '프로젝트')
        else:
            print('프로젝트 사례 없음: 3dkit.config.json harness.replay_cases에 사례 파일을 적는다')
        out = root / 'output/replay/result.json'
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(dict(schema='3dkit-replay-result/1', at=time.strftime('%F %T'), cases_file=rel,
                                       controls=dict(sample={k: v[0] for k, v in sample.controls.items()},
                                                     project={k: [v[0], str(v[1])[:300]] for k, v in proj.controls.items()}),
                                       cases=rows + crows + prows), ensure_ascii=False, indent=1) + '\n')
        print('result:', out.relative_to(root))
        return finish(off, 'sample + project')
    finally:
        if keep:
            print('kept', tmp)
        else:
            shutil.rmtree(tmp, ignore_errors=True)


def main(argv=None):
    a = list(sys.argv[1:] if argv is None else argv)
    only, cases_file = set(), None
    for flag in ('--only', '--cases'):
        if flag in a:
            i = a.index(flag)
            if i + 1 >= len(a):
                fail('USAGE', f'{flag} needs a value', 'replay.py run --only J1,F2 | replay.py project --cases docs/art/replay-cases.json')
            if flag == '--only':
                only = set(a[i + 1].split(','))
            else:
                cases_file = a[i + 1]
            del a[i:i + 2]
    keep = '--keep' in a
    a = [x for x in a if x != '--keep']
    cmd = a[0] if a else ''
    if cmd in ('run', 'selftest'):
        return cmd_run(only, keep)
    if cmd == 'project':
        return cmd_project(only, keep, cases_file)
    raise SystemExit(__doc__)


if __name__ == '__main__':
    sys.exit(main())

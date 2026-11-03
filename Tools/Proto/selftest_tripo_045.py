#!/usr/bin/env python3
"""Offline counterexamples for tripo_045.py (impl review r002 #3, #4). No network: balance/cli are stubbed, fake assets
live in a temp sandbox, the real journals are never touched. Usage: /usr/bin/python3 Tools/Proto/selftest_tripo_045.py"""
import contextlib, importlib.util, io, shutil, sys, tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
T = Path(tempfile.mkdtemp(prefix='tripo045-selftest-')); (T / 'output/keyart-target-045').mkdir(parents=True)
spec = importlib.util.spec_from_file_location('t45', HERE / 'tripo_045.py'); m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
m._SECRET.append('FAKE_TEST_KEY')
m.SPECS = {'fa': dict(stage=9, seed=1, face_limit=500, concept=str(T / 'c.png')), 'fb': dict(stage=9, seed=2, face_limit=500, concept=str(T / 'c.png'))}
m.CAPS = {9: 100}; m.adir = lambda a: T / a; m.ROOT = T; m.AUTH3 = m.AUTH = 'selftest'
(T / 'c.png').write_bytes(b'png'); m.balance = lambda: {'balance': 500, 'frozen': 0}
calls = []
def fake_cli(args, timeout=120):
    calls.append('dry' if '--dry-run' in args else args[0])
    return (0, {'valid': True, 'model': 'x'}, '') if '--dry-run' in args else (7, {}, 'network')
m.cli = fake_cli
def run(cmd, a):
    d = m.adir(a); d.mkdir(exist_ok=True)
    try:
        with contextlib.redirect_stdout(io.StringIO()):
            with m.asset_lock(a): m.gen_command(cmd, a, d, T / 'c.png', 100)
        return 'OK'
    except SystemExit as e: return str(e)
def anim_case(exit_code, http, api):
    a = 'fa'; d = m.adir(a); j = m.journal(a); j['state'] = 'DOWNLOADED'
    j['anim'] = {'rig': dict(state='SUBMISSION_UNKNOWN', exit=exit_code, estimate=25)}; m.save(d / 'journal.json', j)
    m.save(d / 'rig-submit.json', dict(exit=exit_code, out=dict(http_status=http, api_code=api) if http else {}, stderr=''))
    m.save(d / 'rig-balance-before.json', dict(balance=500, frozen=0))
    for p in d.glob('rig-rejected-*'): shutil.rmtree(p)
    try:
        with contextlib.redirect_stdout(io.StringIO()): m.reset_rejected(a, d, 'rig')
        return 'RESET_TO_NEW'
    except SystemExit as e: return str(e)
cases = []
for a in ('fa', 'fb'): run('freeze', a)
import json as _j
cases.append(('frozen request keeps the approval text', _j.loads((m.adir('fa') / 'request-frozen.json').read_text()).get('user_approval'), 'selftest'))
cases.append(('dry run without estimate', run('dryrun', 'fa'), 'DRY_RUN_NO_ESTIMATE'))
for a in ('fa', 'fb'):
    m.set_state(a, state='DRY_RUN_OK', estimate=60, dry_run_request_sha256=m.journal(a)['request_sha256'])
cases.append(('submit fa, network exit 7', run('submit', 'fa'), 'SUBMISSION_UNKNOWN:exit=7'))
cases.append(('fa reservation kept', f"{m.journal('fa')['state']}/{m.journal('fa').get('estimate')}/{m.spent(9)}", 'SUBMISSION_UNKNOWN/60/60'))
cases.append(('submit fb while fa reserved', run('submit', 'fb'), 'CAP_EXCEEDED:60+60>100'))
m.set_state('fb', state='DRY_RUN_OK', estimate=None)
cases.append(('submit with estimate None', run('submit', 'fb'), 'NO_ESTIMATE'))
cases.append(('reset: exit 7 + HTTP 408', anim_case(7, 408, 1000), 'NETWORK_EXIT_NEVER_RESENT'))
cases.append(('reset: exit 2 + HTTP 408', anim_case(2, 408, 1000), 'NOT_A_DEFINITE_REJECTION'))
cases.append(('reset: exit 2 + HTTP 500', anim_case(2, 500, 1), 'NOT_A_DEFINITE_REJECTION'))
cases.append(('reset: exit 2 + no status', anim_case(2, None, None), 'NOT_A_DEFINITE_REJECTION'))
cases.append(('reset: exit 2 + HTTP 400 api 2006', anim_case(2, 400, 2006), 'RESET_TO_NEW'))
cases.append(('provider calls', ','.join(calls), 'dry,make'))
# 047 H8: a paid anim step writes its approval to the journal before submit; no approval -> refused, nothing sent
def anim_submit(a, name, auth3):
    m.SPECS[a] = dict(stage=9, seed=3, face_limit=500, concept=str(T / 'c.png')); d = m.adir(a); d.mkdir(exist_ok=True)
    m.save(d / 'journal.json', dict(state='DOWNLOADED', task_id='g1', credits_task=0, anim={'check': dict(state='DONE', riggable=True, credits_task=0), **({'rig': dict(state='DONE', task_id='r1', credits_task=0)} if name != 'rig' else {})}))
    m.AUTH3, n0 = auth3, len(calls)
    try:
        with contextlib.redirect_stdout(io.StringIO()): m.anim_step(a, d, name, 1000)
        got = 'OK'
    except SystemExit as e: got = str(e).split(':')[0]
    finally: m.AUTH3 = 'selftest'
    rec = (m.journal(a).get('anim') or {}).get(name) or {}
    return f"{got}/{rec.get('user_approval')}/{len(calls) - n0}"
cases.append(('anim rig: stage approval recorded', anim_submit('fc', 'rig', 'stage-3 ok'), 'SUBMISSION_UNKNOWN/stage-3 ok/1'))
cases.append(('anim rig: no approval refused', anim_submit('fd', 'rig', ''), 'NO_AUTHORIZATION/None/0'))
cases.append(('anim attack: own approval', anim_submit('fe', 'attack', '').split('/')[1].startswith('User approved the hero attack'), True))
shutil.rmtree(T)
ok = all(got == want for _, got, want in cases)
for name, got, want in cases: print(('PASS' if got == want else 'FAIL'), name, '->', got)
print('SELFTEST', 'PASS' if ok else 'FAIL'); sys.exit(0 if ok else 1)

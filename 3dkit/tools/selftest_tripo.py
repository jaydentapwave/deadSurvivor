#!/usr/bin/env python3
"""3dkit: offline counterexamples for kitlib/tripo.py (ported from Deadsurvivor Tools/Art/selftest_asset_tripo.py, 050;
045 impl review r002 #3, #4, 047 H8). No network: balance/cli are stubbed, fake assets and budgets live in a temp
sandbox, no project config/catalog/journal is read or touched. Usage: /usr/bin/python3 3dkit/tools/selftest_tripo.py"""
import contextlib, importlib.util, io, shutil, sys, tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
T = Path(tempfile.mkdtemp(prefix='asset-tripo-selftest-')); (T / 'output/keyart-target-045').mkdir(parents=True)
spec = importlib.util.spec_from_file_location('at', HERE / 'kitlib/tripo.py'); m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
m.GEN = dict(model='tripo-v3.1', params=['face_limit={face_limit}']); m.TRIPO = dict(new_work_root='output/assets')
m._SECRET.append('FAKE_TEST_KEY')
m.SPECS = {'fa': dict(budget='t9', seed=1, face_limit=500, concept=str(T / 'c.png')), 'fb': dict(budget='t9', seed=2, face_limit=500, concept=str(T / 'c.png'))}
m.BUDGETS = {'t9': dict(cap=100, approval='selftest', lock=str(T / '.tripo-budget-t9.lock'))}; m.adir = lambda a: T / a; m.ROOT = T
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
m.SPECS['fz'] = dict(budget='t0', seed=9, face_limit=500, concept=str(T / 'c.png')); m.BUDGETS['t0'] = dict(cap=10, approval='')
cases.append(('freeze without approval', run('freeze', 'fz'), 'NO_APPROVAL:t0'))
for a in ('fa', 'fb'): run('freeze', a)
import json as _j
cases.append(('frozen request keeps the approval text', _j.loads((m.adir('fa') / 'request-frozen.json').read_text()).get('user_approval'), 'selftest'))
cases.append(('dry run without estimate', run('dryrun', 'fa'), 'DRY_RUN_NO_ESTIMATE'))
for a in ('fa', 'fb'):
    m.set_state(a, state='DRY_RUN_OK', estimate=60, dry_run_request_sha256=m.journal(a)['request_sha256'])
cases.append(('submit fa, network exit 7', run('submit', 'fa'), 'SUBMISSION_UNKNOWN:exit=7'))
cases.append(('fa reservation kept', f"{m.journal('fa')['state']}/{m.journal('fa').get('estimate')}/{m.spent('t9')}", 'SUBMISSION_UNKNOWN/60/60'))
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
    m.SPECS[a] = dict(budget='t9', seed=3, face_limit=500, concept=str(T / 'c.png')); d = m.adir(a); d.mkdir(exist_ok=True)
    m.save(d / 'journal.json', dict(state='DOWNLOADED', task_id='g1', credits_task=0, anim={'check': dict(state='DONE', riggable=True, credits_task=0), **({'rig': dict(state='DONE', task_id='r1', credits_task=0)} if name != 'rig' else {})}))
    m.BUDGETS['t9']['approval'], n0 = auth3, len(calls)
    try:
        with contextlib.redirect_stdout(io.StringIO()): m.anim_step(a, d, name, 1000)
        got = 'OK'
    except SystemExit as e: got = str(e).split(':')[0]
    finally: m.BUDGETS['t9']['approval'] = 'selftest'
    rec = (m.journal(a).get('anim') or {}).get(name) or {}
    return f"{got}/{rec.get('user_approval')}/{len(calls) - n0}"
cases.append(('anim rig: budget approval recorded', anim_submit('fc', 'rig', 'stage-3 ok'), 'SUBMISSION_UNKNOWN/stage-3 ok/1'))
cases.append(('anim rig: no approval refused', anim_submit('fd', 'rig', ''), 'NO_AUTHORIZATION/None/0'))
# a project step (catalog anim_steps) with its own approval in the budget's step_approvals; without it -> refused
m.ANIM['attack'] = dict(type='retarget', prev='rig', src='rig', est=20, presets=['slash', 'shoot'], own_approval=True)
m.BUDGETS['t9']['step_approvals'] = {'attack': 'User approved the hero attack (selftest)'}
cases.append(('anim attack: step approval recorded', anim_submit('fe', 'attack', ''), 'SUBMISSION_UNKNOWN/User approved the hero attack (selftest)/1'))
m.BUDGETS['t9']['step_approvals'] = {}
cases.append(('anim attack: budget approval alone refused (051 M2)', anim_submit('ff', 'attack', 'budget ok'), 'NO_AUTHORIZATION/None/0'))
cases.append(('attack args from data', ' '.join(m.anim_args(m.ANIM['attack'], 'r1')), 'anim retarget r1 --animation preset:slash preset:shoot --animate-in-place --out-format fbx --yes'))
# a budget that lists its assets does not cover another asset's anim step
m.SPECS['fg'] = dict(budget='t8', seed=4, face_limit=500, concept=str(T / 'c.png')); m.BUDGETS['t8'] = dict(cap=100, approval='ok', assets=['other'], step_approvals={'rig': 'ok'})
cases.append(('step approval limited to listed assets', m.step_auth('fg', 'rig'), None))
shutil.rmtree(T)
ok = all(got == want for _, got, want in cases)
for name, got, want in cases: print(('PASS' if got == want else 'FAIL'), name, '->', got)
print('SELFTEST', 'PASS' if ok else 'FAIL'); sys.exit(0 if ok else 1)

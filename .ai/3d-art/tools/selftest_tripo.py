#!/usr/bin/env python3
"""3dkit: offline counterexamples for kitlib/tripo.py (ported from an earlier project's selftest_asset_tripo.py, 050;
045 impl review r002 #3, #4, 047 H8; 005: B42, B48, regen limit, estimate, quality, fetch --wait). No network: balance/cli are
stubbed, fake assets and budgets live in a temp sandbox, no project config/catalog/journal is read or touched.
Usage: python3 <tools>/selftest_tripo.py   (refusals are compared by their code, without the "-> next:" hint)"""
import contextlib, io, json, os, shutil, struct, subprocess, sys, tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from kitlib import tripo as m  # noqa: E402
from kitlib.errors import code_of  # noqa: E402

T = Path(tempfile.mkdtemp(prefix='asset-tripo-selftest-')); (T / 'output/keyart-target-045').mkdir(parents=True)
m.GEN = dict(model='tripo-v3.1', params=['face_limit={face_limit}']); m.TRIPO = dict(new_work_root='output/assets'); m.REGEN_LIMIT = 2
m._SECRET.append('FAKE_TEST_KEY')
m.SPECS = {'fa': dict(budget='t9', seed=1, face_limit=500, concept=str(T / 'c.png')), 'fb': dict(budget='t9', seed=2, face_limit=500, concept=str(T / 'c.png'))}
m.BUDGETS = {'t9': dict(cap=100, approval='selftest', lock=str(T / '.tripo-budget-t9.lock'))}; m.adir = lambda a: T / a; m.ROOT = T
SPEC_OK = '# 기준서\n- 상태: **r2 승인** (2026-11-05)\n- 승인(r2): 2026-11-05 — "진행"\n'
m.SPEC_DOC = T / 'spec.md'; m.SPEC_APPROVAL = T / 'spec.approval.json'


def approve(text, rev=None):
    """write the spec and the approval record for exactly this text (what approve.py record does)"""
    m.SPEC_DOC.write_text(text)
    if rev is None:
        import re
        r = re.search(r'상태\s*:\s*\**\s*(r\d+)?', text)
        rev = r[1] if r and r[1] else ''
    m.SPEC_APPROVAL.write_text(json.dumps(dict(schema='art-spec-approval/1', revision=rev, approved_on='2026-11-05', user_quote='진행', sha256=m.sha(m.SPEC_DOC))))


approve(SPEC_OK)
(T / 'c.png').write_bytes(b'png'); m.balance = lambda: {'balance': 500, 'frozen': 0}
calls = []
def fake_cli(args, timeout=120):
    calls.append('dry' if '--dry-run' in args else args[0])
    return (0, {'valid': True, 'model': 'x'}, '') if '--dry-run' in args else (7, {}, 'network')
m.cli = fake_cli
def run(cmd, a, img=None, **kw):
    d = m.adir(a); d.mkdir(exist_ok=True)
    try:
        with contextlib.redirect_stdout(io.StringIO()) as out:
            with m.asset_lock(a): m.gen_command(cmd, a, d, img or T / 'c.png', 100, **kw)
        run.out = out.getvalue()
        return 'OK'
    except SystemExit as e: return code_of(e)
def anim_case(exit_code, http, api):
    a = 'fa'; d = m.adir(a); j = m.journal(a); j['state'] = 'DOWNLOADED'
    j['anim'] = {'rig': dict(state='SUBMISSION_UNKNOWN', exit=exit_code, estimate=25)}; m.save(d / 'journal.json', j)
    m.save(d / 'rig-submit.json', dict(exit=exit_code, out=dict(http_status=http, api_code=api) if http else {}, stderr=''))
    m.save(d / 'rig-balance-before.json', dict(balance=500, frozen=0))
    for p in d.glob('rig-rejected-*'): shutil.rmtree(p)
    try:
        with contextlib.redirect_stdout(io.StringIO()): m.reset_rejected(a, d, 'rig')
        return 'RESET_TO_NEW'
    except SystemExit as e: return code_of(e)
cases = []
m.SPECS['fz'] = dict(budget='t0', seed=9, face_limit=500, concept=str(T / 'c.png')); m.BUDGETS['t0'] = dict(cap=10, approval='')
cases.append(('freeze without approval', run('freeze', 'fz'), 'NO_APPROVAL:t0'))
# B39: removing an asset from the catalog keeps its cost in the budget; a retired asset of another budget does not count.
_gone = T / 'output/assets/gone/tripo'; _gone.mkdir(parents=True)
m.save(_gone / 'journal.json', dict(state='DOWNLOADED', credits_task=7)); m.save(_gone / 'request-frozen.json', dict(scope='BUDGET_t0'))
_other = T / 'output/assets/other/tripo'; _other.mkdir(parents=True)
m.save(_other / 'journal.json', dict(state='DOWNLOADED', credits_task=5)); m.save(_other / 'request-frozen.json', dict(scope='BUDGET_zz'))
cases.append(('spent keeps a removed asset', m.spent('t0'), 7))
# B34: the same frozen request from another worktree compares equal; a changed seed or image count does not.
_base = ['make', '/a/urp/c.png', '--model', 'm', '--seed', '1', '--yes', '--no-wait']
cases.append(('args: other worktree path equal', m.args_without_images(['make', '/b/urp-system/c.png', *_base[2:]]) == m.args_without_images(_base), True))
cases.append(('args: changed seed differs', m.args_without_images(['make', '/a/urp/c.png', '--model', 'm', '--seed', '2', '--yes', '--no-wait']) == m.args_without_images(_base), False))
cases.append(('args: image count differs', m.args_without_images(['make', '/a/1.png', '/a/2.png', *_base[2:]]) == m.args_without_images(_base), False))
# F3: no paid call before the art spec is approved (freeze, submit, paid anim step); approval is checked first
def spec_case(text, cmd='freeze', a='fs', record=False):
    m.SPECS[a] = dict(budget='t9', seed=7, face_limit=500, concept=str(T / 'c.png'))
    if text is None: m.SPEC_DOC.unlink()
    elif record: approve(text)
    else: m.SPEC_DOC.write_text(text)
    try: return run(cmd, a).split(':')[0]
    finally:
        approve(SPEC_OK); shutil.rmtree(m.adir(a), ignore_errors=True)
cases.append(('spec: draft refused', spec_case('# 기준서\n- 상태: 초안\n'), 'SPEC_NOT_APPROVED'))
cases.append(('spec: "승인 전" refused', spec_case('- 상태: r3 승인 전\n- 승인(r2): 2026-11-04\n'), 'SPEC_NOT_APPROVED'))
cases.append(('spec: r3 without its approval line refused', spec_case('- 상태: **r3 승인**\n- 승인(r2): 2026-11-04 — "ok"\n'), 'SPEC_NOT_APPROVED'))
cases.append(('spec: approved without a date refused', spec_case('- 상태: 승인\n- 승인: 사용자 ok\n'), 'SPEC_NOT_APPROVED'))
cases.append(('spec: file missing refused', spec_case(None), 'SPEC_NOT_APPROVED'))
cases.append(('spec: unnumbered approval passes', spec_case('- 상태: 승인 (2026-11-05)\n- 승인: 2026-11-05 — "좋아"\n', record=True), 'OK'))
cases.append(('spec: r2 approval passes', spec_case(SPEC_OK, record=True), 'OK'))
# B48: the approval is bound to the approved text
cases.append(('B48: edited after approval refused', spec_case(SPEC_OK + '\n고친 줄\n'), 'SPEC_CHANGED_AFTER_APPROVAL'))
m.SPEC_APPROVAL.unlink(); cases.append(('B48: no approval record refused', spec_case(SPEC_OK), 'SPEC_NOT_APPROVED')); approve(SPEC_OK)
approve(SPEC_OK, rev='r1'); cases.append(('B48: record for another revision refused', spec_case(SPEC_OK), 'SPEC_NOT_APPROVED')); approve(SPEC_OK)
_doc, m.SPEC_DOC = m.SPEC_DOC, None; m.SPECS['fs2'] = dict(budget='t9', seed=6, face_limit=500, concept=str(T / 'c.png'))
cases.append(('spec: no assets.spec refused', run('freeze', 'fs2').split(':')[0], 'SPEC_NOT_APPROVED')); m.SPEC_DOC = _doc
m.SPECS['fs3'] = dict(budget='t0', seed=8, face_limit=500, concept=str(T / 'c.png')); m.SPEC_DOC.write_text('- 상태: 초안\n')
cases.append(('approval checked before the spec', run('freeze', 'fs3'), 'NO_APPROVAL:t0')); approve(SPEC_OK)
# B42: a missing concept image is named, not a FileNotFoundError
m.SPECS['fn'] = dict(budget='t9', seed=5, face_limit=500, concept=str(T / 'nope.png'))
cases.append(('B42: missing concept file named', run('freeze', 'fn', img=T / 'nope.png').split(':')[0], 'NO_CONCEPT_FILE'))
for a in ('fa', 'fb'): run('freeze', a)
cases.append(('frozen request keeps the approval text', json.loads((m.adir('fa') / 'request-frozen.json').read_text()).get('user_approval'), 'selftest'))
cases.append(('frozen request keeps the approved spec revision', json.loads((m.adir('fa') / 'request-frozen.json').read_text()).get('spec_doc', {}).get('revision'), 'r2'))
cases.append(('dry run without estimate', run('dryrun', 'fa'), 'DRY_RUN_NO_ESTIMATE:fa'))
for a in ('fa', 'fb'):
    m.set_state(a, state='DRY_RUN_OK', estimate=60, dry_run_request_sha256=m.journal(a)['request_sha256'])
m.SPEC_DOC.write_text('- 상태: r3 개정 중\n'); n0 = len(calls)
cases.append(('submit after the spec went back to a draft', f"{run('submit', 'fa').split(':')[0]}/{len(calls) - n0}/{m.journal('fa')['state']}", 'SPEC_NOT_APPROVED/0/DRY_RUN_OK'))
m.SPEC_DOC.write_text(SPEC_OK + '추가\n'); n0 = len(calls)
cases.append(('B48: submit after an edit since freeze', f"{run('submit', 'fa').split(':')[0]}/{len(calls) - n0}", 'SPEC_CHANGED_AFTER_APPROVAL/0'))
approve(SPEC_OK)
cases.append(('submit fa, network exit 7', run('submit', 'fa'), 'SUBMISSION_UNKNOWN:exit=7'))
cases.append(('fa reservation kept', f"{m.journal('fa')['state']}/{m.journal('fa').get('estimate')}/{m.spent('t9')}", 'SUBMISSION_UNKNOWN/60/60'))
cases.append(('submit fb while fa reserved', run('submit', 'fb'), 'CAP_EXCEEDED:60+60>100'))
m.set_state('fb', state='DRY_RUN_OK', estimate=None)
cases.append(('submit with estimate None', run('submit', 'fb'), 'NO_ESTIMATE:fb'))
cases.append(('reset: exit 7 + HTTP 408', anim_case(7, 408, 1000), 'NETWORK_EXIT_NEVER_RESENT:rig'))
cases.append(('reset: exit 2 + HTTP 408', anim_case(2, 408, 1000), 'NOT_A_DEFINITE_REJECTION:rig'))
cases.append(('reset: exit 2 + HTTP 500', anim_case(2, 500, 1), 'NOT_A_DEFINITE_REJECTION:rig'))
cases.append(('reset: exit 2 + no status', anim_case(2, None, None), 'NOT_A_DEFINITE_REJECTION:rig'))
cases.append(('reset: exit 2 + HTTP 400 api 2006', anim_case(2, 400, 2006), 'RESET_TO_NEW'))
cases.append(('provider calls', ','.join(calls), 'dry,make'))
# every refusal carries a next action
try:
    m.fail('X', 'y', 'do z')
except SystemExit as e:
    cases.append(('refusal shape', str(e), 'X:y -> next: do z'))
# 047 H8: a paid anim step writes its approval to the journal before submit; no approval -> refused, nothing sent
def anim_submit(a, name, auth3, quality=None, env=None):
    m.SPECS[a] = dict(budget='t9', seed=3, face_limit=500, concept=str(T / 'c.png')); d = m.adir(a); d.mkdir(exist_ok=True)
    m.save(d / 'journal.json', dict(state='DOWNLOADED', task_id='g1', credits_task=0, **({'quality': quality} if quality else {}), anim={'check': dict(state='DONE', riggable=True, credits_task=0), **({'rig': dict(state='DONE', task_id='r1', credits_task=0)} if name != 'rig' else {})}))
    m.BUDGETS['t9']['approval'], n0 = auth3, len(calls)
    old = {k: os.environ.get(k) for k in (env or {})}; os.environ.update(env or {})
    try:
        with contextlib.redirect_stdout(io.StringIO()): m.anim_step(a, d, name, 1000)
        got = 'OK'
    except SystemExit as e: got = str(e).split(':')[0]
    finally:
        m.BUDGETS['t9']['approval'] = 'selftest'
        for k, v in old.items():
            if v is None: os.environ.pop(k, None)
            else: os.environ[k] = v
    rec = (m.journal(a).get('anim') or {}).get(name) or {}
    return f"{got}/{rec.get('user_approval')}/{len(calls) - n0}" + (f"/{rec.get('quality_ok')}" if quality else '')
cases.append(('anim rig: budget approval recorded', anim_submit('fc', 'rig', 'stage-3 ok'), 'SUBMISSION_UNKNOWN/stage-3 ok/1'))
cases.append(('anim rig: no approval refused', anim_submit('fd', 'rig', ''), 'NO_AUTHORIZATION/None/0'))
m.SPEC_DOC.write_text('- 상태: 초안\n')
cases.append(('anim rig: spec draft refused', anim_submit('fd2', 'rig', 'ok'), 'SPEC_NOT_APPROVED/None/0')); approve(SPEC_OK)
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
# quality warnings stop paid rigging until the user's words are given
qw = dict(warnings=['COLOR_LOST concept 0.87 -> result 0.006'], summary='x')
cases.append(('quality warning: paid rig refused', anim_submit('fq', 'rig', 'ok', quality=qw), 'QUALITY_WARN_UNRESOLVED/None/0/None'))
cases.append(('quality warning: QUALITY_OK records and goes on', anim_submit('fq2', 'rig', 'ok', quality=qw, env={'QUALITY_OK': '사용자: 그대로 진행'}), 'SUBMISSION_UNKNOWN/ok/1/사용자: 그대로 진행'))
# regen limit: X, X_r2, X_r3 paid -> X_r4 refused; REGEN_OK goes on and is recorded
for name in ('rg', 'rg_r2', 'rg_r3'):
    (T / 'output/assets' / name / 'tripo').mkdir(parents=True); m.save(T / 'output/assets' / name / 'tripo/journal.json', dict(state='DOWNLOADED'))
m.SPECS['rg_r4'] = dict(budget='t9', seed=10, face_limit=500, concept=str(T / 'c.png'))
cases.append(('regen limit: 4th paid attempt refused', run('freeze', 'rg_r4').split(':')[0], 'REGEN_LIMIT'))
os.environ['REGEN_OK'] = '사용자: 한 번 더'
cases.append(('regen limit: REGEN_OK recorded', (run('freeze', 'rg_r4'), json.loads((m.adir('rg_r4') / 'request-frozen.json').read_text()).get('regen_ok')), ('OK', '사용자: 한 번 더')))
os.environ.pop('REGEN_OK')
m.SPECS['rg_r3b'] = dict(budget='t9', seed=11, face_limit=500, concept=str(T / 'c.png'))
cases.append(('regen limit: other base unaffected', run('freeze', 'rg_r3b'), 'OK'))
# estimate: dry run without approval or journal
m.SPECS['fx'] = dict(budget='t0', seed=12, face_limit=500, concept=str(T / 'c.png')); n0 = len(calls)
cases.append(('estimate without approval, no journal', (run('estimate', 'fx'), len(calls) - n0, m.journal('fx')), ('OK', 1, None)))
# fetch --wait: a watch that runs out of time leaves the state alone
def slow_cli(args, timeout=120):
    if args[:2] == ['task', 'watch']: raise subprocess.TimeoutExpired(args, timeout)
    return fake_cli(args, timeout)
m.cli = slow_cli; m.SPECS['fw'] = dict(budget='t9', seed=13, face_limit=500, concept=str(T / 'c.png')); run('freeze', 'fw')
m.set_state('fw', state='SUBMITTED', task_id='t1')
r = run('fetch', 'fw', wait=5); cases.append(('fetch --wait: pending, state kept', (r, 'FETCH_PENDING' in run.out, m.journal('fw')['state']), ('OK', True, 'SUBMITTED')))
m.cli = fake_cli
# quality(): measured on a tiny GLB (one material, a 16x16 texture) against the concept
import numpy as np  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402
from kitlib import genqc  # noqa: E402
FOUR = [(-.25, -.25), (.25, -.25), (-.25, .25), (.25, .25)]
def glb(path, rgb, tris, legs=FOUR, lift=None):
    """a real mesh (body + legs, padded with zero-area triangles up to tris) and one 16x16 texture"""
    b = io.BytesIO(); Image.new('RGB', (16, 16), rgb).save(b, 'PNG'); png = b.getvalue(); png += b'\0' * (-len(png) % 4)
    P, I = genqc.test_creature(legs, lift)
    I = np.concatenate([I, np.zeros((max(0, tris - len(I)), 3), int)])
    pos, idx = P.astype(np.float32).tobytes(), I.astype(np.uint32).tobytes()
    views = [dict(buffer=0, byteOffset=o, byteLength=n) for o, n in ((0, len(png)), (len(png), len(pos)), (len(png) + len(pos), len(idx)))]
    j = dict(asset=dict(version='2.0'), meshes=[dict(primitives=[dict(attributes=dict(POSITION=0), indices=1)])],
             accessors=[dict(bufferView=1, componentType=5126, count=len(P), type='VEC3'), dict(bufferView=2, componentType=5125, count=I.size, type='SCALAR')],
             images=[dict(bufferView=0, mimeType='image/png')], textures=[dict(source=0)], materials=[dict(pbrMetallicRoughness=dict(baseColorTexture=dict(index=0)))],
             bufferViews=views, buffers=[dict(byteLength=len(png) + len(pos) + len(idx))])
    js = json.dumps(j).encode(); js += b' ' * (-len(js) % 4)
    bin_ = png + pos + idx
    body = struct.pack('<I4s', len(js), b'JSON') + js + struct.pack('<I4s', len(bin_), b'BIN\0') + bin_
    Path(path).parent.mkdir(parents=True, exist_ok=True); Path(path).write_bytes(struct.pack('<4sII', b'glTF', 2, 12 + len(body)) + body)
con = T / 'green.png'; Image.new('RGB', (64, 64), (40, 200, 60)).save(con)
m.SPECS['qa'] = dict(budget='t9', seed=14, face_limit=500, concept=str(con))
glb(T / 'q1/raw/a.glb', (170, 170, 170), 480); w1 = m.quality('qa', T / 'q1/raw')['warnings']
glb(T / 'q2/raw/a.glb', (40, 190, 70), 480); w2 = m.quality('qa', T / 'q2/raw')['warnings']
glb(T / 'q3/raw/a.glb', (40, 190, 70), 700); w3 = m.quality('qa', T / 'q3/raw')['warnings']
cases.append(('quality: grey result of a green concept', [x.split(' ')[0] for x in w1], ['COLOR_LOST']))
cases.append(('quality: coloured result passes', w2, []))
cases.append(('quality: over face_limit', [x.split(' ')[0] for x in w3], ['OVER_FACE_LIMIT']))
m.SPECS['qg'] = dict(budget='t9', seed=15, face_limit=500, concept=str(T / 'grey.png')); Image.new('RGB', (64, 64), (128, 128, 128)).save(T / 'grey.png')
cases.append(('quality: grey result of a grey concept (rocks) passes', m.quality('qg', T / 'q1/raw')['warnings'], []))
# quality(): genqc on the generated shape, only what the catalog declares
codes = lambda q: [x.split(' ')[0] for x in q['warnings']]
GREEN = (40, 190, 70)
m.SPECS['qe'] = dict(budget='t9', seed=16, face_limit=500, concept=str(con), expect=dict(feet=4))
glb(T / 'q4/raw/a.glb', GREEN, 480); q4 = m.quality('qe', T / 'q4/raw')
cases.append(('quality: 4 legs as declared pass, numbers kept', (codes(q4), len(q4['qc']['feet'])), ([], 4)))
glb(T / 'q5/raw/a.glb', GREEN, 480, legs=FOUR + [(0, .36)])
cases.append(('quality: an extra leg warns', codes(m.quality('qe', T / 'q5/raw')), ['RESULT_FEET_COUNT']))
cases.append(('quality: an extra leg passes when no count is declared', codes(m.quality('qa', T / 'q5/raw')), []))
glb(T / 'q6/raw/a.glb', GREEN, 480, lift={0: .05})
cases.append(('quality: a raised leg warns', codes(m.quality('qe', T / 'q6/raw')), ['RESULT_FEET_NOT_LEVEL']))
m.SPECS['qr'] = dict(budget='t9', seed=17, face_limit=500, concept=str(con), expect=dict(top_round=True))
cases.append(('quality: a square top warns', codes(m.quality('qr', T / 'q4/raw')), ['RESULT_TOP_NOT_ROUND']))
# silhouette against the views: the concept is a wide blob, the model is tall
wide = [T / f'wide{i}.png' for i in range(2)]
for x in wide:
    im = Image.new('RGB', (256, 256), (255, 255, 255)); ImageDraw.Draw(im).ellipse((8, 90, 248, 170), fill=(40, 200, 60)); im.save(x)
m.SPECS['qs'] = dict(budget='t9', seed=18, face_limit=500, concept=[str(x) for x in wide])
cases.append(('quality: another silhouette than the views warns', codes(m.quality('qs', T / 'q4/raw')), ['RESULT_SILHOUETTE_DIFFERS']))
# a mesh genqc cannot read is reported, the paid download is not lost
bad = T / 'q7/raw/a.glb'; bad.parent.mkdir(parents=True); data = bytearray((T / 'q4/raw/a.glb').read_bytes())
n_js = struct.unpack('<I', data[12:16])[0]; broken = json.loads(data[20:20 + n_js]); broken['accessors'][0]['componentType'] = 5120
js = json.dumps(broken).encode(); js += b' ' * (-len(js) % 4); rest = bytes(data[20 + n_js:])
bad.write_bytes(struct.pack('<4sII', b'glTF', 2, 12 + 8 + len(js) + len(rest)) + struct.pack('<I4s', len(js), b'JSON') + js + rest)
q7 = m.quality('qa', T / 'q7/raw')
cases.append(('quality: unreadable mesh is QC_NOT_MEASURED, not a pass', (codes(q7), q7['qc']), (['QC_NOT_MEASURED'], None)))
# freeze: input views. Same figure in each view; the bad set draws one foot higher (a perspective view, B-frog r002)
def view(path, feet):
    im = Image.new('RGB', (256, 256), (255, 255, 255)); d = ImageDraw.Draw(im); d.rectangle((70, 40, 186, 150), fill=(40, 200, 60))
    for x0, x1, y1 in feet:
        d.rectangle((x0, 150, x1, y1), fill=(40, 200, 60))
    im.save(path)
LEVEL = [(80, 110, 220), (146, 176, 220)]
good = [T / f'good{i}.png' for i in range(3)]
for x in good: view(x, LEVEL)
badv = [T / f'badv{i}.png' for i in range(3)]
view(badv[0], LEVEL); view(badv[1], [(80, 110, 220), (146, 176, 205)]); view(badv[2], LEVEL)
blank = [T / 'blank.png', good[1], good[2]]; Image.new('RGB', (64, 64), (255, 255, 255)).save(blank[0])
fake = [T / 'c.png', good[1], good[2]]
def freeze_views(a, imgs, **env):
    m.SPECS[a] = dict(budget='t9', seed=19, face_limit=500, concept=[str(x) for x in imgs])
    for k, v in env.items(): os.environ[k] = v
    try: return run('freeze', a, img=list(imgs))
    finally:
        for k in env: os.environ.pop(k, None)
cases.append(('freeze: level views pass', freeze_views('iv1', good), 'OK'))
cases.append(('freeze: measures kept in the request', json.loads((m.adir('iv1') / 'request-frozen.json').read_text())['input_check']['fails'], []))
r = freeze_views('iv2', badv); cases.append(('freeze: uneven feet refused, nothing frozen', (r.split(':')[0], 'INPUT_FEET_NOT_LEVEL' in r, m.journal('iv2')), ('INPUT_CHECK_FAILED', True, None)))
cases.append(('freeze: INPUT_OK records the words and goes on', (freeze_views('iv3', badv, INPUT_OK='사용자: 그대로 진행'), json.loads((m.adir('iv3') / 'request-frozen.json').read_text())['input_ok']), ('OK', '사용자: 그대로 진행')))
r = freeze_views('iv4', blank); cases.append(('freeze: an empty view is refused', (r.split(':')[0], 'INPUT_EMPTY_VIEW' in r), ('INPUT_CHECK_FAILED', True)))
cases.append(('freeze: a file that is no image is refused', freeze_views('iv5', fake).split(':')[0], 'INPUT_UNREADABLE'))
m.cli = lambda a, t=120: (0, dict(valid=True, model='x', credits_estimate=30), '')
m.SPECS['iv6'] = dict(budget='t9', seed=20, face_limit=500, concept=[str(x) for x in badv])
cases.append(('estimate: bad views reported, not refused', (run('estimate', 'iv6', img=list(badv)), json.loads(run.out)['input_fails'][0].split(' ')[0]), ('OK', 'INPUT_FEET_NOT_LEVEL')))
m.cli = fake_cli
# an unknown asset name says which names exist
sys.argv = ['assets.py gen', 'status', 'nope']
try:
    m.main(); cases.append(('unknown asset named', 'no error', 'UNKNOWN_TRIPO_ASSET'))
except SystemExit as e:
    cases.append(('unknown asset named', code_of(e).split(':')[0], 'UNKNOWN_TRIPO_ASSET'))
shutil.rmtree(T)
ok = all(got == want for _, got, want in cases)
for name, got, want in cases: print(('PASS' if got == want else 'FAIL'), name, '->', got)
print('SELFTEST', 'PASS' if ok else 'FAIL', f'({len(cases)} cases)'); sys.exit(0 if ok else 1)

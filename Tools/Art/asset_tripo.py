#!/usr/bin/env python3
"""050: paid Tripo generation for the official asset pipeline (promoted from Tools/Proto/tripo_045.py, rules unchanged).

Assets, budgets (user approval text + credit cap) and work folders come from docs/art/asset-catalog.json: every catalog
asset with a "tripo" block is an asset here, keyed by its tripo name. Old 045/046 journals are read from their own
work_dir. A new asset needs a budget entry whose "approval" quotes the user's approval; freeze refuses without it.

States (journal.json, atomic writes): FROZEN -> DRY_RUN_OK -> SUBMITTING -> SUBMITTED(task_id) -> DOWNLOADED.
- SUBMITTING without a task id after a crash/network error = SUBMISSION_UNKNOWN: stop; a human reconciles with
  `tripo balance` / `tripo usage`. Exit code 7 (network) is never resent (CLI doc rule).
- A task id is always resumed with `tripo task watch/get --download`, never re-made.
- Cumulative spend + estimate must stay <= the budget cap before any submit. Spend = provider-reported credits_consumed
  per task (watch output); balance deltas are kept as evidence only.
- Locks: every mutating command holds the asset lock and re-reads the journal inside it; the cap check and the SUBMITTING
  reservation happen under the budget lock. Submit uses the frozen planned_args only, and only after a dry run of the same
  frozen request.
Usage (through Tools/Art/assets.py gen ...): freeze|dryrun|submit|fetch|status|reconcile <tripo asset> ;
       anim <tripo asset> check|rig|retarget|attack|reset-<step>
Never prints the API key; saved JSON is redacted (key-like field names, the key value itself, bearer headers, URL queries).
"""
import hashlib, json, os, re, subprocess, sys, time
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

ROOT = Path(__file__).resolve().parents[2]
CATALOG = json.loads((ROOT / 'docs/art/asset-catalog.json').read_text())
TRIPO = CATALOG['tripo']
CLI = ROOT / TRIPO['cli']
BUDGETS = CATALOG['budgets']
SPECS = {a['tripo']['asset']: a['tripo'] for a in CATALOG['assets'] if a.get('tripo')}


def budget(asset): return SPECS[asset]['budget']
def adir(asset): return ROOT / (SPECS[asset].get('work_dir') or f"{TRIPO['new_work_root']}/{asset}/tripo")


def approval(asset):
    """the user's approval text for this asset; a budget that lists its assets covers only those (050 impl review: a new
    asset must not inherit an old budget's approval)"""
    b = BUDGETS[budget(asset)]
    if 'assets' in b and asset not in b['assets']:
        return None
    return (b.get('approval_by_asset') or {}).get(asset) or b.get('approval')


def now():
    return datetime.now(ZoneInfo('Asia/Seoul')).isoformat()


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def env():
    e = dict(os.environ, TRIPO_HOME=str(ROOT / 'output/tripo-state'), TRIPO_ANALYTICS_ENABLED='0', TRIPO_NO_UPDATE_CHECK='1')
    for line in (ROOT / '.env.local').read_text().splitlines():
        m = re.match(r'^(?:export\s+)?TRIPO_API_KEY\s*=\s*(.*?)\s*$', line.strip())
        if m:
            e['TRIPO_API_KEY'] = m.group(1).strip('"\'')
    if not e.get('TRIPO_API_KEY'):
        raise SystemExit('MISSING_KEY')
    return e


_SECRET = []
def _secret():
    if not _SECRET:
        _SECRET.append(env()['TRIPO_API_KEY'])
    return _SECRET[0]


# 047 H8: approval text is stored as 'user_approval'. A key named 'authorization' is treated as an HTTP credential and
# redacted, which is why the request-frozen.json files written before 2026-11-03 show '[REDACTED]' instead of the approval.
def redact(v):
    if isinstance(v, dict):
        return {k: ('[REDACTED]' if any(s in k.lower() for s in ('api_key', 'apikey', 'authorization', 'token', 'secret')) else redact(x)) for k, x in v.items()}
    if isinstance(v, list):
        return [redact(x) for x in v]
    if isinstance(v, str):
        k = _secret()
        if k and k in v:
            v = v.replace(k, '[REDACTED]')
        v = re.sub(r'(?i)(bearer\s+)[^\s"\',]+', r'\1[REDACTED]', v)
        v = re.sub(r'(?i)((?:api[_-]?key|authorization|token)\s*[:=]\s*)[^\s"\',]+', r'\1[REDACTED]', v)
        v = re.sub(r'https?://[^\s"\'?]+\?[^\s"\']*', lambda m: m.group(0).split('?')[0] + '?[REDACTED]', v)
    return v


def save(path, data):
    tmp = Path(str(path) + '.tmp')
    tmp.write_text(json.dumps(redact(data), ensure_ascii=False, indent=2) + '\n')
    os.replace(tmp, path)


def cli(args, timeout=120):
    p = subprocess.run([str(CLI), *args, '--json', '--no-open'], cwd=ROOT, env=env(), capture_output=True, text=True, timeout=timeout)
    try:
        d = json.loads(p.stdout.strip().splitlines()[-1]) if p.stdout.strip() else {}
    except Exception:
        d = {'non_json_stdout': p.stdout[-400:]}
    return p.returncode, d, p.stderr[-800:]


def journal(asset):
    j = adir(asset) / 'journal.json'
    return json.loads(j.read_text()) if j.exists() else None


def set_state(asset, **kw):
    d = journal(asset) or {}
    d.update(kw); d.setdefault('history', []).append(dict(at=now(), state=kw.get('state'), note=kw.get('note')))
    save(adir(asset) / 'journal.json', d)
    return d


INFLIGHT = ('SUBMITTING', 'SUBMITTED', 'SUBMISSION_UNKNOWN')


def cost(rec):
    """one task's credits for the cap: provider-reported credits_task; balance delta only if no task figure exists;
    the estimate while the task is in flight or unknown (reservation)."""
    if rec.get('credits_task') is not None: return int(rec['credits_task'])
    if rec.get('state') in INFLIGHT: return int(rec.get('estimate') or 0)
    return int(rec.get('credits_spent') or 0)


def spent(bud):
    total = 0
    for a in SPECS:
        if budget(a) != bud: continue
        j = journal(a) or {}
        total += cost(j)
        for v in (j.get('anim') or {}).values():
            total += cost(v)
    return total


def task_credits(watch_out):
    v = (watch_out or {}).get('credits_consumed')
    return int(v) if isinstance(v, (int, float)) else None


class Lock:
    """O_EXCL lock file; a leftover lock after a crash blocks (a human removes it after checking the journal)."""
    def __init__(self, path): self.path = path
    def __enter__(self):
        try:
            self.fd = os.open(self.path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
        except FileExistsError:
            raise SystemExit('LOCKED:' + self.path.name)
        os.write(self.fd, f'{os.getpid()} {now()}\n'.encode()); return self
    def __exit__(self, *exc):
        os.close(self.fd); self.path.unlink(missing_ok=True)


def asset_lock(asset): return Lock(adir(asset) / '.asset.lock')
def stage_lock(asset):
    b = budget(asset)
    return Lock(ROOT / (BUDGETS[b].get('lock') or f'output/assets/.tripo-budget-{b}.lock'))


def balance():
    code, d, err = cli(['balance'], 60)
    if code != 0 or 'balance' not in d:
        raise SystemExit('BALANCE_FAILED:' + str(code))
    return d


def concepts(asset):
    """frozen input images: one path (single image) or a list (multiview, front first)"""
    c = SPECS[asset].get('concept')
    if not c: raise SystemExit('NO_CONCEPT:' + asset)
    return [ROOT / x for x in (c if isinstance(c, list) else [c])]


def args_for(asset, img, dry):
    s = SPECS[asset]; imgs = img if isinstance(img, list) else [img]
    a = ['make', *[str(x) for x in imgs], '--model', TRIPO['model'], '--seed', str(s['seed']), '--candidates', '1']
    for p in TRIPO['params']:
        a += ['-p', p.format(face_limit=s['face_limit'])]
    a += ['--yes']
    return a + (['--dry-run'] if dry else ['--no-wait'])


def main():
    if len(sys.argv) not in (3, 4) or sys.argv[2] not in SPECS:
        raise SystemExit(__doc__)
    cmd, asset = sys.argv[1], sys.argv[2]
    d = adir(asset); d.mkdir(parents=True, exist_ok=True)
    imgs = concepts(asset); img = imgs[0] if len(imgs) == 1 else imgs
    cap = int(BUDGETS[budget(asset)]['cap'])
    if cmd == 'status':
        print(json.dumps(dict(journal=journal(asset), spent=spent(budget(asset))), ensure_ascii=False)); return
    with asset_lock(asset):          # every mutating command; the journal is (re)read inside the lock
        if cmd == 'anim':
            return anim_step(asset, d, sys.argv[3], cap)
        return gen_command(cmd, asset, d, img, cap)


def reconcile(asset, d):
    """set credits_task from the saved provider watch output (no network). Balance deltas stay as evidence."""
    j = journal(asset); changed = {}
    if j.get('task_id') and j.get('credits_task') is None and (d / 'watch.json').exists():
        c = task_credits(json.loads((d / 'watch.json').read_text()).get('out'))
        if c is not None: changed['gen'] = c; j['credits_task'] = c
    for k, v in (j.get('anim') or {}).items():
        w = d / f'{k}-watch.json'
        if v.get('task_id') and v.get('credits_task') is None and w.exists():
            c = task_credits(json.loads(w.read_text()).get('out'))
            if c is not None: changed[k] = c; v['credits_task'] = c
    if changed:
        set_state(asset, **{k: j[k] for k in ('credits_task', 'anim') if k in j}, note=f'reconcile credits_task from watch output: {changed}')
    print(json.dumps(dict(asset=asset, reconciled=changed, stage_spent=spent(budget(asset)))))


def gen_command(cmd, asset, d, img, cap):
    j = journal(asset)
    if cmd == 'reconcile':
        return reconcile(asset, d)
    if cmd == 'freeze':
        if j: raise SystemExit('ALREADY_FROZEN')
        many = isinstance(img, list)
        auth = approval(asset)
        if not auth: raise SystemExit('NO_APPROVAL:' + budget(asset))
        req = dict(scope=f'BUDGET_{budget(asset)}', asset=asset, created_at=now(), user_approval=auth, cap_credits=cap,
                   concept=[str(x.relative_to(ROOT)) for x in img] if many else str(img.relative_to(ROOT)),
                   concept_sha256=[sha(x) for x in img] if many else sha(img), spec=SPECS[asset], planned_args=args_for(asset, img, False),
                   upload='concept PNG only', visibility='UNVERIFIED: CLI 0.8.1 docs expose no public/private parameter; no publish/share command is used')
        save(d / 'request-frozen.json', req)
        set_state(asset, state='FROZEN', request_sha256=sha(d / 'request-frozen.json')); print('FROZEN'); return
    if not j: raise SystemExit('NOT_FROZEN')
    req = json.loads((d / 'request-frozen.json').read_text())
    cur = [sha(x) for x in img] if isinstance(img, list) else sha(img)
    if sha(d / 'request-frozen.json') != j['request_sha256'] or cur != req['concept_sha256']:
        raise SystemExit('FROZEN_INPUT_CHANGED')
    # the provider call is made from the frozen planned_args only; the current SPECS must still produce exactly them
    planned = list(req['planned_args'])
    if args_for(asset, img, False) != planned or planned[-1] != '--no-wait':
        raise SystemExit('FROZEN_ARGS_MISMATCH')
    if cmd == 'dryrun':
        if j['state'] not in ('FROZEN', 'DRY_RUN_OK'): raise SystemExit('STATE:' + j['state'])
        code, out, err = cli(planned[:-1] + ['--dry-run'], 60)
        save(d / 'dry-run.json', dict(exit=code, out=out, stderr=err, request_sha256=j['request_sha256']))
        est = out.get('generation_credits_estimate') or out.get('credits_estimate')
        if code != 0 or not out.get('valid', False): raise SystemExit('DRY_RUN_INVALID:' + str(code))
        if not isinstance(est, int) or est <= 0: raise SystemExit('DRY_RUN_NO_ESTIMATE')   # impl review r002 #3: no default cost
        if journal(asset)['state'] not in ('FROZEN', 'DRY_RUN_OK'): raise SystemExit('STATE_CHANGED_DURING_DRY_RUN')
        set_state(asset, state='DRY_RUN_OK', estimate=est, dry_run_request_sha256=j['request_sha256'], note=f'estimate={est}')
        print(json.dumps(dict(state='DRY_RUN_OK', estimate=est, model=out.get('model')))); return
    if cmd == 'submit':
        if j['state'] != 'DRY_RUN_OK' or j.get('task_id'): raise SystemExit('STATE:' + j['state'])
        if j.get('dry_run_request_sha256') != j['request_sha256']: raise SystemExit('DRY_RUN_NOT_FOR_THIS_REQUEST')
        est = j.get('estimate')
        if not isinstance(est, int) or est <= 0: raise SystemExit('NO_ESTIMATE')
        with stage_lock(asset):     # cap check + reservation (SUBMITTING counts the same estimate) are atomic per stage
            if spent(budget(asset)) + est > cap: raise SystemExit(f'CAP_EXCEEDED:{spent(budget(asset))}+{est}>{cap}')
            b0 = balance(); save(d / 'balance-before.json', dict(at=now(), **b0))
            set_state(asset, state='SUBMITTING', estimate=est, note='about to submit (no task id yet)')
        code, out, err = cli(planned, 180)
        save(d / 'submit.json', dict(exit=code, out=out, stderr=err, args=planned))
        tid = out.get('task_id')
        if code != 0 or not tid:
            set_state(asset, state='SUBMISSION_UNKNOWN', note=f'exit={code}; never resend; reconcile with balance/usage')
            raise SystemExit(f'SUBMISSION_UNKNOWN:exit={code}')
        set_state(asset, state='SUBMITTED', task_id=tid, submitted_at=now()); print(json.dumps(dict(state='SUBMITTED', task_id=tid)))
        return
    if cmd == 'fetch':
        if j['state'] not in ('SUBMITTED', 'DOWNLOADED') or not j.get('task_id'): raise SystemExit('STATE:' + j['state'])
        code, out, err = cli(['task', 'watch', j['task_id']], 900)
        save(d / 'watch.json', dict(exit=code, out=out, stderr=err))
        if code != 0: raise SystemExit(f'WATCH_FAILED:exit={code} (do not re-make; refunded if failed)')
        ctask = task_credits(out)
        code, out, err = cli(['task', 'get', j['task_id'], '--download', '-o', str(d / 'raw')], 300)
        save(d / 'result.json', dict(exit=code, out=out, stderr=err))
        if code != 0: raise SystemExit(f'DOWNLOAD_FAILED:exit={code} (retry fetch only)')
        files = [dict(path=str(p.relative_to(ROOT)), size=p.stat().st_size, sha256=sha(p)) for p in sorted((d / 'raw').rglob('*')) if p.is_file()]
        b1 = balance(); save(d / 'balance-after.json', dict(at=now(), **b1))
        b0 = json.loads((d / 'balance-before.json').read_text())
        set_state(asset, state='DOWNLOADED', raw_files=files, credits_spent=int(b0['balance']) - int(b1['balance']), credits_task=ctask, note='downloaded')
        print(json.dumps(dict(state='DOWNLOADED', files=len(files), credits_spent=int(b0['balance']) - int(b1['balance'])))); return
    raise SystemExit('UNKNOWN_COMMAND')


ANIM = {  # paid anim steps run strictly in order (prev must be DONE); src = whose task id the step is applied to
    # 042 record: rig was applied to the generation task, retarget to the rig task (a check task id is rejected: api 2006)
    'check': dict(prev='gen', src='gen', est=0, args=lambda tid: ['anim', 'check', tid]),
    'rig': dict(prev='check', src='gen', est=25, args=lambda tid: ['anim', 'rig', tid, '--rig-type', 'biped', '--spec', 'unity', '--out-format', 'fbx', '--yes']),
    'retarget': dict(prev='rig', src='rig', est=20, args=lambda tid: ['anim', 'retarget', tid, '--animation', 'preset:idle', 'preset:run', '--animate-in-place', '--out-format', 'fbx', '--yes']),
    # 047: melee heroes use slash, ranged/caster/healer heroes use shoot. Applied to the same rig task.
    'attack': dict(prev='rig', src='rig', est=20, args=lambda tid: ['anim', 'retarget', tid, '--animation', 'preset:slash', 'preset:shoot', '--animate-in-place', '--out-format', 'fbx', '--yes'],
                   auth=("User approved the hero attack motions on 2026-11-03: '1번부터해서 4번까지 모두 작업을 진행해줘' answering the listed "
                         "item '영웅 공격 동작: Tripo 동작 추가, 크레딧 필요(동작 1개당 약 10)' (047).")),
}


def step_auth(asset, name):
    """047 H8: the user approval behind a paid anim step, written to the journal before submit. Steps without their own
    'auth' fall back to the approval of the budget the generation belongs to (rig/retarget were part of those approvals)."""
    return ANIM[name].get('auth') or approval(asset)


REJECT_HTTP = (400, 404, 422)


def reset_rejected(asset, d, name):
    """SUBMISSION_UNKNOWN -> NEW only when the provider definitely rejected the request (impl review r002 #4): the CLI
    exit is not 7 (network: never resent), the response is HTTP 400/404/422 with an api code (validation-type errors;
    408/429/5xx and anything without a status are refused), no task id, and the balance is unchanged since the step's
    balance-before. The rejected record is kept."""
    j = journal(asset); a = j.get('anim') or {}; st = a.get(name) or {}
    if st.get('state') != 'SUBMISSION_UNKNOWN': raise SystemExit('NOT_SUBMISSION_UNKNOWN')
    sub = json.loads((d / f'{name}-submit.json').read_text()); out = sub.get('out') or {}
    if int(sub.get('exit', 7)) == 7 or int(st.get('exit', 7)) == 7: raise SystemExit('NETWORK_EXIT_NEVER_RESENT')
    if out.get('task_id') or int(out.get('http_status') or 0) not in REJECT_HTTP or not out.get('api_code'):
        raise SystemExit('NOT_A_DEFINITE_REJECTION')
    b0 = json.loads((d / f'{name}-balance-before.json').read_text()); b1 = balance()
    if int(b1['balance']) != int(b0['balance']) or int(b1.get('frozen') or 0) != int(b0.get('frozen') or 0):
        raise SystemExit(f'BALANCE_CHANGED:{b0["balance"]}->{b1["balance"]}')
    n = len(list(d.glob(f'{name}-rejected-*'))) + 1
    keep = d / f'{name}-rejected-{n}'; keep.mkdir()
    for f in (f'{name}-submit.json', f'{name}-balance-before.json'):
        (d / f).rename(keep / f)
    save(keep / 'balance-after.json', dict(at=now(), **b1))
    hist = st.get('rejected', []) + [dict(at=now(), source_task=st.get('source_task'), api_code=out.get('api_code'),
                                          http_status=out.get('http_status'), request_id=out.get('request_id'), credits_spent=0, record=str(keep.relative_to(ROOT)))]
    a[name] = dict(state='NEW', rejected=hist); set_state(asset, anim=a, note=f'anim {name}: rejected by provider (0 credits), reset to NEW')
    print(json.dumps(dict(step=name, state='NEW', rejected=len(hist), balance=b1['balance'])))


def anim_step(asset, d, name, cap):
    if name.startswith('reset-'):
        return reset_rejected(asset, d, name[len('reset-'):])
    if name not in ANIM: raise SystemExit('UNKNOWN_ANIM_STEP')
    j = journal(asset)
    if not j or j.get('state') != 'DOWNLOADED': raise SystemExit('GENERATION_NOT_DOWNLOADED')
    a = j.get('anim') or {}
    st = a.get(name) or {}
    prev = ANIM[name]['prev']
    src = j['task_id'] if ANIM[name]['src'] == 'gen' else (a.get(ANIM[name]['src']) or {}).get('task_id')
    if prev != 'gen' and (a.get(prev) or {}).get('state') != 'DONE': raise SystemExit('PREVIOUS_STEP_NOT_DONE:' + prev)
    if name == 'rig' and not (a.get('check') or {}).get('riggable', False): raise SystemExit('NOT_RIGGABLE')
    def put(**kw):
        jj = journal(asset); aa = jj.get('anim') or {}; cur = aa.get(name) or {}; cur.update(kw); aa[name] = cur
        set_state(asset, anim=aa, note=f'anim {name}: {kw.get("state")}')
    if st.get('state') in (None, 'NEW'):
        est = ANIM[name]['est']; args = ANIM[name]['args'](src) + ['--no-wait']
        auth = step_auth(asset, name)
        if est and not auth: raise SystemExit('NO_AUTHORIZATION:' + name)
        with stage_lock(asset):
            if est and spent(budget(asset)) + est > cap: raise SystemExit(f'CAP_EXCEEDED:{spent(budget(asset))}+{est}>{cap}')
            b0 = balance(); save(d / f'{name}-balance-before.json', dict(at=now(), **b0))
            put(state='SUBMITTING', estimate=est, source_task=src, args=args, user_approval=auth)
        code, out, err = cli(args, 180)
        save(d / f'{name}-submit.json', dict(exit=code, out=out, stderr=err))
        tid = out.get('task_id')
        if code != 0 or not tid:
            put(state='SUBMISSION_UNKNOWN', exit=code); raise SystemExit(f'SUBMISSION_UNKNOWN:{name}:exit={code}')
        put(state='SUBMITTED', task_id=tid)
        st = journal(asset)['anim'][name]
    if st.get('state') == 'SUBMITTED':
        code, out, err = cli(['task', 'watch', st['task_id']], 900)
        save(d / f'{name}-watch.json', dict(exit=code, out=out, stderr=err))
        if code != 0: raise SystemExit(f'WATCH_FAILED:{name}:exit={code}')
        ctask = task_credits(out)
        code, out, err = cli(['task', 'get', st['task_id'], '--download', '-o', str(d / name)], 300)
        save(d / f'{name}-result.json', dict(exit=code, out=out, stderr=err))
        if code != 0: raise SystemExit(f'DOWNLOAD_FAILED:{name}:exit={code}')
        b1 = balance(); b0 = json.loads((d / f'{name}-balance-before.json').read_text())
        files = [dict(path=str(p.relative_to(ROOT)), size=p.stat().st_size, sha256=sha(p)) for p in sorted((d / name).rglob('*')) if p.is_file()]
        extra = {}
        if name == 'check':
            txt = json.dumps(out).lower(); extra['riggable'] = ('"riggable": true' in txt) or ('riggable": true' in txt) or ('"rig_type"' in txt)
            extra['check_output'] = out.get('output') or out
        put(state='DONE', files=files, credits_spent=int(b0['balance']) - int(b1['balance']), credits_task=ctask, **extra)
    print(json.dumps(dict(step=name, **{k: v for k, v in journal(asset)['anim'][name].items() if k in ('state', 'task_id', 'credits_spent', 'credits_task', 'riggable')})))



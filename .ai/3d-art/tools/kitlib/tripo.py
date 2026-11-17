#!/usr/bin/env python3
"""3dkit generate stage: paid Tripo generation (ported from an earlier project, Tools/Art/asset_tripo.py, 050; rules unchanged).

Assets, budgets (user approval text + credit cap), project anim steps and work folders come from the project's catalog:
every catalog asset with a "tripo" block is an asset here, keyed by its tripo name. Old journals are read from their own
work_dir. A new asset needs a budget whose "approval" quotes the user's approval; freeze refuses without it.
Model and params come from the profile (generate.tripo), an asset's tripo block may override them. CLI path, TRIPO_HOME,
key file and key variable come from 3dkit.config.json "tripo". Call setup(kit) first (assets.py does).

States (journal.json, atomic writes): FROZEN -> DRY_RUN_OK -> SUBMITTING -> SUBMITTED(task_id) -> DOWNLOADED.
- SUBMITTING without a task id after a crash/network error = SUBMISSION_UNKNOWN: stop; a human reconciles with
  `tripo balance` / `tripo usage`. Exit code 7 (network) is never resent (CLI doc rule).
- A task id is always resumed with `tripo task watch/get --download`, never re-made.
- Cumulative spend + estimate must stay <= the budget cap before any submit. Spend = provider-reported credits_consumed
  per task (watch output); balance deltas are kept as evidence only.
- Locks: every mutating command holds the asset lock and re-reads the journal inside it; the cap check and the SUBMITTING
  reservation happen under the budget lock. Submit uses the frozen planned_args only, and only after a dry run of the same
  frozen request.
- Paid anim steps write the user's approval text to the journal before submit: the budget's step_approvals[step], else the
  budget approval. No approval -> refused.
- No paid call before the art spec is approved AND unchanged since (B48): the spec's status line, its dated approval line, and
  the approval record (art-spec scripts/approve.py: revision + sha256 of the approved text) must agree with the file now.
- A new version of an asset (X, X_r2, X_r3 ...) may be generated tripo.regen_limit times (default 2) after the first paid
  attempt; more needs the user's words in REGEN_OK="..." (recorded in the frozen request).
- fetch measures the result (triangles vs face_limit, colour vs the concept) and keeps the warnings in the journal; a paid anim
  step refuses while they are unresolved unless QUALITY_OK="<user words>" is given (recorded).
Usage (through assets.py gen ...): estimate|freeze|dryrun|submit|fetch [--wait S]|status|reconcile <tripo asset> ;
       anim <tripo asset> <step>|reset-<step>   (steps: check, rig, retarget + the catalog's anim_steps)
  estimate  dry-run price without approval or journal (ask the user with this number); fetch --wait S stops waiting after S
  seconds with FETCH_PENDING (nothing changes; fetch again later).
Every refusal reads CODE:detail -> next: <action> (kitlib/errors.py).
Never prints the API key; saved JSON is redacted (key-like field names, the key value itself, bearer headers, URL queries).
"""
import hashlib, json, os, re, subprocess, sys, time
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

from .errors import fail

ROOT = None          # set by setup(kit)
TRIPO = {}           # 3dkit.config.json "tripo": cli, home, key_env_file, key_env, new_work_root
GEN = {}             # profile generate.tripo: model, params
CLI = None
BUDGETS = {}
SPECS = {}
TZ = 'Asia/Seoul'
SPEC_DOC = None     # 3dkit.config.json assets.spec: the art spec; no paid call before it is approved (F3)
SPEC_APPROVAL = None   # its approval record (B48); assets.spec_approval or <spec>.approval.json
REGEN_LIMIT = 2
APPROVE_CMD = 'python3 <art-spec skill>/scripts/approve.py record --quote "<user words>"'


def setup(kit):
    global ROOT, TRIPO, GEN, CLI, BUDGETS, SPECS, ANIM, TZ, SPEC_DOC, SPEC_APPROVAL, REGEN_LIMIT
    ROOT = kit.root
    a = kit.data['assets']
    SPEC_DOC = ROOT / a['spec'] if a.get('spec') else None
    SPEC_APPROVAL = ROOT / a['spec_approval'] if a.get('spec_approval') else (SPEC_DOC.with_suffix('.approval.json') if SPEC_DOC else None)
    TRIPO = dict(kit.tripo())
    REGEN_LIMIT = int(TRIPO.get('regen_limit', 2))
    GEN = kit.profile['generate']['tripo']
    CLI = ROOT / TRIPO['cli'] if TRIPO.get('cli') else None
    BUDGETS = kit.cat['budgets']
    SPECS = {a['tripo']['asset']: a['tripo'] for a in kit.cat['assets'] if a.get('tripo')}
    extra = kit.cat.get('anim_steps') or {}
    clash = sorted(set(extra) & set(DEFAULT_ANIM))
    if clash:
        fail('ANIM_STEP_NAME_CLASH', ','.join(clash), 'rename the catalog anim_steps so they do not reuse check/rig/retarget')
    ANIM = {**DEFAULT_ANIM, **{k: dict(v, own_approval=True) for k, v in extra.items()}}
    TZ = TRIPO.get('timezone') or TZ


def spec_gate():
    """refuse a paid call unless the art spec is approved (art-spec skill: no paid generation before approval; replay F3) and
    still the approved text (B48). Approved = the status line reads '상태: [rN] 승인' (not 승인 전/대기/요청/필요/중), an approval
    line with a date exists ('승인(rN): YYYY-MM-DD' for a numbered revision, '승인: YYYY-MM-DD' otherwise), and the approval
    record (approve.py) has the same revision and the sha256 of the file as it is now. Returns what the frozen request keeps."""
    nxt = f'show the spec to the user and get the approval (/art-spec step 8), then record it: {APPROVE_CMD}'
    if SPEC_DOC is None: fail('SPEC_NOT_APPROVED', 'no assets.spec in 3dkit.config.json', 'set assets.spec to the art spec path')
    if not SPEC_DOC.is_file(): fail('SPEC_NOT_APPROVED', f'missing {SPEC_DOC.name}', 'write and approve the art spec first (/art-spec)')
    text = SPEC_DOC.read_text()
    st = re.search(r'^\s*-?\s*상태\s*:\s*(.*)$', text, re.M)
    val = st[1].replace('*', '').strip() if st else ''
    ok = re.match(r'(?:r(\d+)\s*)?승인(?!\s*(?:전|대기|요청|필요|중))', val)
    if not ok: fail('SPEC_NOT_APPROVED', f'status "{val[:40]}"', nxt)
    tag = rf'\(r{ok[1]}\)' if ok[1] else ''
    if not re.search(rf'^\s*-?\s*승인{tag}\s*:\s*\**\s*\d\d\d\d-\d\d-\d\d', text, re.M):
        fail('SPEC_NOT_APPROVED', f'no dated approval line for {"r" + ok[1] if ok[1] else "the spec"}', nxt)
    if SPEC_APPROVAL is None or not SPEC_APPROVAL.is_file():
        fail('SPEC_NOT_APPROVED', f'no approval record {SPEC_APPROVAL.name if SPEC_APPROVAL else "(none)"}', nxt)
    rec = json.loads(SPEC_APPROVAL.read_text())
    rev = f'r{ok[1]}' if ok[1] else ''
    if rev and rec.get('revision') != rev:
        fail('SPEC_NOT_APPROVED', f'the approval record is for {rec.get("revision")}, the status line says {rev}', nxt)
    if rec.get('sha256') != sha(SPEC_DOC):
        fail('SPEC_CHANGED_AFTER_APPROVAL', f'{SPEC_DOC.name} is not the approved {rec.get("revision") or "text"} any more',
             'show the change to the user; after their approval record it again (raise the revision for a real change)')
    rel = str(SPEC_DOC.relative_to(ROOT)) if ROOT in SPEC_DOC.parents else str(SPEC_DOC)
    return dict(path=rel, revision=rec.get('revision'), sha256=rec.get('sha256'), approved_on=rec.get('approved_on'))


def budget(asset): return SPECS[asset]['budget']
def adir(asset): return ROOT / (SPECS[asset].get('work_dir') or f"{TRIPO.get('new_work_root', 'output/assets')}/{asset}/tripo")


def approval(asset):
    """the user's approval text for this asset; a budget that lists its assets covers only those (050 impl review: a new
    asset must not inherit an old budget's approval)"""
    b = BUDGETS[budget(asset)]
    if 'assets' in b and asset not in b['assets']:
        return None
    return (b.get('approval_by_asset') or {}).get(asset) or b.get('approval')


def now():
    return datetime.now(ZoneInfo(TZ)).isoformat()


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def env():
    var = TRIPO.get('key_env', 'TRIPO_API_KEY')
    e = dict(os.environ, TRIPO_HOME=str(ROOT / TRIPO.get('home', 'output/tripo-state')), TRIPO_ANALYTICS_ENABLED='0', TRIPO_NO_UPDATE_CHECK='1')
    kf = ROOT / TRIPO.get('key_env_file', '.env.local')
    for line in (kf.read_text().splitlines() if kf.exists() else []):
        m = re.match(r'^(?:export\s+)?' + re.escape(var) + r'\s*=\s*(.*?)\s*$', line.strip())
        if m:
            e[var] = m.group(1).strip('"\'')
    if not e.get(var):
        fail('MISSING_KEY', var, f'put {var}=... in {TRIPO.get("key_env_file", ".env.local")} (never print it)')
    if var != 'TRIPO_API_KEY':
        e['TRIPO_API_KEY'] = e[var]
    return e


_SECRET = []
def _secret():
    if not _SECRET:
        _SECRET.append(env()[TRIPO.get('key_env', 'TRIPO_API_KEY')])
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
    if not CLI:
        fail('NO_TRIPO_CLI', '', 'set tripo.cli in 3dkit.config.json to the Tripo CLI path')
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
    # an asset removed from the catalog keeps its cost (B39): its journal stays on disk, the frozen request names the budget
    live = {adir(a).resolve() for a in SPECS}
    scopes = {f'BUDGET_{bud}', BUDGETS[bud].get('legacy_scope')}
    for f in (ROOT / TRIPO.get('new_work_root', 'output/assets')).glob('*/tripo/journal.json'):
        q = f.with_name('request-frozen.json')
        if f.parent.resolve() in live or not q.exists() or json.loads(q.read_text()).get('scope') not in scopes:
            continue
        j = json.loads(f.read_text())
        total += cost(j) + sum(cost(v) for v in (j.get('anim') or {}).values())
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
            fail('LOCKED', str(self.path), 'another gen command for this asset/budget is running, or one crashed. If no assets.py gen runs '
                 '(ps), read journal.json (a SUBMITTING state needs reconcile with balance/usage), then delete this lock file')
        os.write(self.fd, f'{os.getpid()} {now()}\n'.encode()); return self
    def __exit__(self, *exc):
        os.close(self.fd); self.path.unlink(missing_ok=True)


def asset_lock(asset): return Lock(adir(asset) / '.asset.lock')
def stage_lock(asset):
    b = budget(asset)
    return Lock(ROOT / (BUDGETS[b].get('lock') or f"{TRIPO.get('new_work_root', 'output/assets')}/.tripo-budget-{b}.lock"))


def balance():
    code, d, err = cli(['balance'], 60)
    if code != 0 or 'balance' not in d:
        fail('BALANCE_FAILED', str(code), 'check the network and the key; nothing was submitted')
    return d


def concepts(asset):
    """frozen input images: one path (single image) or a list (multiview, front first)"""
    c = SPECS[asset].get('concept')
    if not c: fail('NO_CONCEPT', asset, 'set tripo.concept in the catalog to the adopted concept image (produce step 4)')
    return [ROOT / x for x in (c if isinstance(c, list) else [c])]


def need_files(asset, img):
    for x in (img if isinstance(img, list) else [img]):
        if not Path(x).is_file():
            fail('NO_CONCEPT_FILE', f'{asset}:{x}', 'make or copy the concept image there first (produce step 2), or fix tripo.concept in the catalog')


def input_measure(asset, img):
    """genqc numbers of the input images (no network, no credits). An image that cannot be read is a refusal, not a pass."""
    from . import genqc
    try:
        return genqc.check_input(img if isinstance(img, list) else [img])
    except OSError as e:
        fail('INPUT_UNREADABLE', f'{asset}: {e}', 'replace the file with a real image, or fix tripo.concept in the catalog')


def input_check(asset, img):
    """freeze: images that are known to make Tripo add legs or collapse the shape stop here, before any credit"""
    r = input_measure(asset, img)
    ok = os.environ.get('INPUT_OK', '').strip()
    if r['fails'] and not ok:
        fail('INPUT_CHECK_FAILED', f"{asset}: " + '; '.join(r['fails']),
             'fix the concept images and show them to the user; to go on with these images, run again with INPUT_OK="<user words>"')
    return dict(input_check=r, input_ok=ok or None)


PAID = ('SUBMITTING', 'SUBMITTED', 'SUBMISSION_UNKNOWN', 'DOWNLOADED')


def base_name(asset):
    return re.sub(r'_r\d+$', '', asset)


def attempts(asset):
    """paid generation attempts of the other versions of this asset (X, X_r2 ...): journals of catalog assets and of work
    folders left on disk"""
    b = base_name(asset)
    dirs = {adir(a).resolve() for a in SPECS if a != asset and base_name(a) == b}
    dirs |= {f.parent.resolve() for f in (ROOT / TRIPO.get('new_work_root', 'output/assets')).glob('*/tripo/journal.json')
             if f.parents[1].name != asset and base_name(f.parents[1].name) == b}
    n = 0
    for d in dirs:
        jf = d / 'journal.json'
        if jf.is_file() and json.loads(jf.read_text()).get('state') in PAID:
            n += 1
    return n


def regen_check(asset):
    n = attempts(asset)
    ok = os.environ.get('REGEN_OK', '').strip()
    if n > REGEN_LIMIT and not ok:
        fail('REGEN_LIMIT', f'{base_name(asset)} already has {n} paid attempts (limit: first + {REGEN_LIMIT})',
             'stop and show the attempts to the user (maybe the input or a 0-credit Blender route is the fix); to go on, run again with REGEN_OK="<user words>"')
    return dict(attempts_before=n, regen_ok=ok or None)


def quality(asset, raw):
    """measure a downloaded result (no network): triangles vs face_limit, colour of the base texture vs the concept, and genqc:
    leg ends and top against tripo.expect, silhouette against the input views"""
    from . import genqc, texture as T
    glbs = sorted(Path(raw).rglob('*.glb'))
    if not glbs:
        return dict(warnings=['NO_GLB'], summary='no GLB in the download')
    g = T.glb_stats(glbs[0])
    limit = int(SPECS[asset]['face_limit'])
    imgs = []
    try:
        imgs = concepts(asset)
        con = round(T.image_saturation(imgs[0]), 3)
    except (OSError, SystemExit):
        con = None
    warn = []
    qc = None
    try:
        qc = genqc.check_result(glbs[0], imgs, SPECS[asset].get('expect'))
        warn += qc['fails']
    except Exception as e:     # the credits are spent: keep the download. Not measured is a warning, never a pass
        warn.append(f'QC_NOT_MEASURED {type(e).__name__}: {e}')
    if g['tris'] > 1.1 * limit:
        warn.append(f"OVER_FACE_LIMIT {g['tris']}>{limit}")
    if g['sat_p90'] is None:
        warn.append('NO_BASE_COLOR_TEXTURE')
    elif con is not None and con >= 0.3 and g['sat_p90'] < 0.15:
        warn.append(f"COLOR_LOST concept {con} -> result {g['sat_p90']}")
    return dict(glb=str(glbs[0].relative_to(ROOT)) if ROOT in glbs[0].parents else str(glbs[0]), tris=g['tris'], face_limit=limit,
                sat_p90=g['sat_p90'], concept_sat_p90=con, qc=qc and qc['measures'], warnings=warn,
                summary=f"tris {g['tris']}/{limit}, colour p90 {g['sat_p90']} (concept {con})")


def args_for(asset, img, dry):
    s = SPECS[asset]; imgs = img if isinstance(img, list) else [img]
    a = ['make', *[str(x) for x in imgs], '--model', s.get('model') or GEN['model'], '--seed', str(s['seed']), '--candidates', '1']
    for p in s.get('params') or GEN['params']:
        a += ['-p', p.format(face_limit=s['face_limit'])]
    a += ['--yes']
    return a + (['--dry-run'] if dry else ['--no-wait'])


def args_without_images(args):
    """planned args minus the image paths: 'make', <images...>, '--model', ...  An image count change still differs (length)."""
    model = args.index('--model')
    return [args[0], model - 1, *args[model:]]


def main():
    argv = list(sys.argv)
    wait = None
    if '--wait' in argv:
        i = argv.index('--wait')
        if i + 1 >= len(argv) or not argv[i + 1].isdigit():
            fail('USAGE', '--wait needs seconds', 'gen fetch <asset> --wait 30')
        wait = int(argv[i + 1]); del argv[i:i + 2]
    if len(argv) not in (3, 4):
        fail('USAGE', 'gen estimate|freeze|dryrun|submit|fetch [--wait S]|status|reconcile <asset> | gen anim <asset> <step>|reset-<step>',
             'see the docstring of kitlib/tripo.py')
    cmd, asset = argv[1], argv[2]
    if asset not in SPECS:
        fail('UNKNOWN_TRIPO_ASSET', asset, f'use a catalog tripo.asset name ({", ".join(sorted(SPECS)[:8])}{" ..." if len(SPECS) > 8 else ""})')
    d = adir(asset); d.mkdir(parents=True, exist_ok=True)
    imgs = concepts(asset); img = imgs[0] if len(imgs) == 1 else imgs
    cap = int(BUDGETS[budget(asset)]['cap'])
    if cmd == 'status':
        print(json.dumps(dict(journal=journal(asset), spent=spent(budget(asset))), ensure_ascii=False)); return
    with asset_lock(asset):          # every mutating command; the journal is (re)read inside the lock
        if cmd == 'anim':
            return anim_step(asset, d, argv[3], cap)
        return gen_command(cmd, asset, d, img, cap, wait)


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


def state_fail(j, want):
    fail('STATE', j['state'], f'this command needs {want}; run `gen status` and continue from the state shown there')


def gen_command(cmd, asset, d, img, cap, wait=None):
    j = journal(asset)
    if cmd == 'reconcile':
        return reconcile(asset, d)
    if cmd == 'estimate':
        need_files(asset, img)
        code, out, err = cli(args_for(asset, img, True), 60)
        save(d / 'estimate.json', dict(at=now(), exit=code, out=out, stderr=err))
        est = out.get('generation_credits_estimate') or out.get('credits_estimate')
        if code != 0 or not out.get('valid', False):
            fail('DRY_RUN_INVALID', str(code), f'read {d.name}/estimate.json; fix the concept image or the catalog tripo block')
        print(json.dumps(dict(state='ESTIMATE', asset=asset, estimate=est, model=out.get('model'), input_fails=input_measure(asset, img)['fails'],
                              note='no credits used; ask the user for the approval and a cap with this number. freeze refuses while input_fails is not empty')))
        return
    if cmd == 'freeze':
        if j: fail('ALREADY_FROZEN', asset, 'continue with dryrun/submit/fetch; a new attempt is a new version <asset>_rN in the catalog')
        need_files(asset, img)
        inp = input_check(asset, img)
        many = isinstance(img, list)
        auth = approval(asset)
        if not auth: fail('NO_APPROVAL', budget(asset), "ask the user; put the user's words and a cap in the catalog budget (approval / approval_by_asset). Old approvals do not carry over")
        doc = spec_gate()
        regen = regen_check(asset)
        req = dict(scope=f'BUDGET_{budget(asset)}', asset=asset, created_at=now(), user_approval=auth, cap_credits=cap,
                   concept=[str(x.relative_to(ROOT)) for x in img] if many else str(img.relative_to(ROOT)),
                   concept_sha256=[sha(x) for x in img] if many else sha(img), spec=SPECS[asset], planned_args=args_for(asset, img, False),
                   spec_doc=doc, **regen, **inp,
                   upload='concept PNG only', visibility='UNVERIFIED: CLI 0.8.1 docs expose no public/private parameter; no publish/share command is used')
        save(d / 'request-frozen.json', req)
        set_state(asset, state='FROZEN', request_sha256=sha(d / 'request-frozen.json')); print('FROZEN'); return
    if not j: fail('NOT_FROZEN', asset, 'run gen freeze first (needs the approval in the catalog budget)')
    req = json.loads((d / 'request-frozen.json').read_text())
    need_files(asset, img)
    cur = [sha(x) for x in img] if isinstance(img, list) else sha(img)
    if sha(d / 'request-frozen.json') != j['request_sha256'] or cur != req['concept_sha256']:
        fail('FROZEN_INPUT_CHANGED', asset, 'the concept image or request-frozen.json changed after freeze: restore them, or make a new version <asset>_rN')
    # the provider call is made from the frozen planned_args only; the current SPECS must still produce exactly them
    planned = list(req['planned_args'])
    if args_for(asset, img, False) != planned or planned[-1] != '--no-wait':
        fail('FROZEN_ARGS_MISMATCH', asset, 'the catalog tripo block (seed, face_limit, model, params) changed after freeze: restore it, or make a new version')
    if cmd == 'dryrun':
        if j['state'] not in ('FROZEN', 'DRY_RUN_OK'): state_fail(j, 'FROZEN or DRY_RUN_OK')
        code, out, err = cli(planned[:-1] + ['--dry-run'], 60)
        save(d / 'dry-run.json', dict(exit=code, out=out, stderr=err, request_sha256=j['request_sha256']))
        est = out.get('generation_credits_estimate') or out.get('credits_estimate')
        if code != 0 or not out.get('valid', False): fail('DRY_RUN_INVALID', str(code), f'read {d.name}/dry-run.json')
        if not isinstance(est, int) or est <= 0: fail('DRY_RUN_NO_ESTIMATE', asset, 'the provider gave no price; do not submit without one')   # impl review r002 #3: no default cost
        if journal(asset)['state'] not in ('FROZEN', 'DRY_RUN_OK'): fail('STATE_CHANGED_DURING_DRY_RUN', asset, 'another command ran at the same time; run gen status')
        set_state(asset, state='DRY_RUN_OK', estimate=est, dry_run_request_sha256=j['request_sha256'], note=f'estimate={est}')
        print(json.dumps(dict(state='DRY_RUN_OK', estimate=est, model=out.get('model')))); return
    if cmd == 'submit':
        if j['state'] != 'DRY_RUN_OK' or j.get('task_id'): state_fail(j, 'DRY_RUN_OK without a task id')
        if j.get('dry_run_request_sha256') != j['request_sha256']: fail('DRY_RUN_NOT_FOR_THIS_REQUEST', asset, 'run gen dryrun again')
        est = j.get('estimate')
        if not isinstance(est, int) or est <= 0: fail('NO_ESTIMATE', asset, 'run gen dryrun again')
        spec_gate()     # the spec may have gone back to a draft, or been edited, since freeze
        with stage_lock(asset):     # cap check + reservation (SUBMITTING counts the same estimate) are atomic per stage
            if spent(budget(asset)) + est > cap: fail('CAP_EXCEEDED', f'{spent(budget(asset))}+{est}>{cap}', "stop; ask the user for a higher cap or a new budget (their words go in the catalog)")
            b0 = balance(); save(d / 'balance-before.json', dict(at=now(), **b0))
            set_state(asset, state='SUBMITTING', estimate=est, note='about to submit (no task id yet)')
        code, out, err = cli(planned, 180)
        save(d / 'submit.json', dict(exit=code, out=out, stderr=err, args=planned))
        tid = out.get('task_id')
        if code != 0 or not tid:
            set_state(asset, state='SUBMISSION_UNKNOWN', note=f'exit={code}; never resend; reconcile with balance/usage')
            fail('SUBMISSION_UNKNOWN', f'exit={code}', 'never resend: compare `tripo balance`/`tripo usage` with balance-before.json and record what happened')
        set_state(asset, state='SUBMITTED', task_id=tid, submitted_at=now()); print(json.dumps(dict(state='SUBMITTED', task_id=tid)))
        return
    if cmd == 'fetch':
        if j['state'] not in ('SUBMITTED', 'DOWNLOADED') or not j.get('task_id'): state_fail(j, 'SUBMITTED (a task id)')
        try:
            code, out, err = cli(['task', 'watch', j['task_id']], wait or 900)
        except subprocess.TimeoutExpired:
            print(json.dumps(dict(state='FETCH_PENDING', task_id=j['task_id'], waited=wait or 900,
                                  note='still running at the provider; nothing changed. Run fetch again later (no new charge)'))); return
        save(d / 'watch.json', dict(exit=code, out=out, stderr=err))
        if code != 0: fail('WATCH_FAILED', f'exit={code}', 'do not re-make (a failed task is refunded); read watch.json and ask the user')
        ctask = task_credits(out)
        code, out, err = cli(['task', 'get', j['task_id'], '--download', '-o', str(d / 'raw')], 300)
        save(d / 'result.json', dict(exit=code, out=out, stderr=err))
        if code != 0: fail('DOWNLOAD_FAILED', f'exit={code}', 'run fetch again (download only, no new charge)')
        files = [dict(path=str(p.relative_to(ROOT)), size=p.stat().st_size, sha256=sha(p)) for p in sorted((d / 'raw').rglob('*')) if p.is_file()]
        b1 = balance(); save(d / 'balance-after.json', dict(at=now(), **b1))
        b0 = json.loads((d / 'balance-before.json').read_text())
        q = quality(asset, d / 'raw')
        set_state(asset, state='DOWNLOADED', raw_files=files, credits_spent=int(b0['balance']) - int(b1['balance']), credits_task=ctask, quality=q, note='downloaded')
        print(json.dumps(dict(state='DOWNLOADED', files=len(files), credits_spent=int(b0['balance']) - int(b1['balance']), quality=q['summary'], warnings=q['warnings'])))
        if q['warnings']:
            print('QUALITY_WARN:' + '; '.join(q['warnings']) + ' -> next: look at the provider preview with the user before rigging or import; '
                  'paid anim steps need QUALITY_OK="<user words>"', file=sys.stderr)
        return
    fail('UNKNOWN_COMMAND', cmd, 'use estimate|freeze|dryrun|submit|fetch|status|reconcile|anim')


# Paid anim steps run strictly in order (prev must be DONE); src = whose task id the step is applied to. 042 record: rig
# was applied to the generation task, retarget to the rig task (a check task id is rejected: api 2006). A project adds
# steps (or overrides these) in the catalog's anim_steps, e.g. an earlier project's 047 'attack' = retarget slash/shoot.
DEFAULT_ANIM = {
    'check': dict(type='check', prev='gen', src='gen', est=0),
    'rig': dict(type='rig', prev='check', src='gen', est=25),
    'retarget': dict(type='retarget', prev='rig', src='rig', est=20, presets=['idle', 'run']),
}
ANIM = dict(DEFAULT_ANIM)


def anim_args(step, tid):
    t = step['type']
    if t == 'check':
        return ['anim', 'check', tid]
    if t == 'rig':
        return ['anim', 'rig', tid, '--rig-type', step.get('rig_type', 'biped'), '--spec', 'unity', '--out-format', 'fbx', '--yes']
    if t == 'retarget':
        return ['anim', 'retarget', tid, '--animation', *[f'preset:{p}' for p in step['presets']], '--animate-in-place', '--out-format', 'fbx', '--yes']
    fail('UNKNOWN_ANIM_TYPE', str(t), 'catalog anim_steps type must be check, rig or retarget')


def step_auth(asset, name):
    """047 H8: the user approval behind a paid anim step, written to the journal before submit: the budget's
    step_approvals[name] (e.g. the 047 attack motions), else the approval of the budget the generation belongs to
    (rig/retarget were part of those approvals). Project steps need their own step_approvals text. A budget that lists
    its assets covers only those."""
    b = BUDGETS[budget(asset)]
    if 'assets' in b and asset not in b['assets']:
        return None
    own = (b.get('step_approvals') or {}).get(name)
    if ANIM[name].get('own_approval'):      # a project step (catalog anim_steps) never inherits the budget approval (051 M2)
        return own
    return own or approval(asset)


REJECT_HTTP = (400, 404, 422)


def reset_rejected(asset, d, name):
    """SUBMISSION_UNKNOWN -> NEW only when the provider definitely rejected the request (impl review r002 #4): the CLI
    exit is not 7 (network: never resent), the response is HTTP 400/404/422 with an api code (validation-type errors;
    408/429/5xx and anything without a status are refused), no task id, and the balance is unchanged since the step's
    balance-before. The rejected record is kept."""
    j = journal(asset); a = j.get('anim') or {}; st = a.get(name) or {}
    if st.get('state') != 'SUBMISSION_UNKNOWN': fail('NOT_SUBMISSION_UNKNOWN', name, 'reset is only for a step whose submission is unknown')
    sub = json.loads((d / f'{name}-submit.json').read_text()); out = sub.get('out') or {}
    if int(sub.get('exit', 7)) == 7 or int(st.get('exit', 7)) == 7: fail('NETWORK_EXIT_NEVER_RESENT', name, 'a network failure may have been charged: compare balance/usage by hand and ask the user')
    if out.get('task_id') or int(out.get('http_status') or 0) not in REJECT_HTTP or not out.get('api_code'):
        fail('NOT_A_DEFINITE_REJECTION', name, 'only HTTP 400/404/422 with an api code and an unchanged balance can be reset; reconcile by hand')
    b0 = json.loads((d / f'{name}-balance-before.json').read_text()); b1 = balance()
    if int(b1['balance']) != int(b0['balance']) or int(b1.get('frozen') or 0) != int(b0.get('frozen') or 0):
        fail('BALANCE_CHANGED', f'{b0["balance"]}->{b1["balance"]}', 'something was charged: reconcile with tripo usage before any reset')
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
    if name not in ANIM: fail('UNKNOWN_ANIM_STEP', name, f'use one of {", ".join(ANIM)}')
    j = journal(asset)
    if not j or j.get('state') != 'DOWNLOADED': fail('GENERATION_NOT_DOWNLOADED', asset, 'run gen fetch first')
    a = j.get('anim') or {}
    st = a.get(name) or {}
    prev = ANIM[name]['prev']
    src = j['task_id'] if ANIM[name]['src'] == 'gen' else (a.get(ANIM[name]['src']) or {}).get('task_id')
    if prev != 'gen' and (a.get(prev) or {}).get('state') != 'DONE': fail('PREVIOUS_STEP_NOT_DONE', prev, f'run gen anim {asset} {prev} first')
    if ANIM[name]['type'] == 'rig' and not (a.get(prev) or {}).get('riggable', False): fail('NOT_RIGGABLE', asset, 'the provider check says it cannot be rigged: rig it in Blender (0 credits) instead')
    def put(**kw):
        jj = journal(asset); aa = jj.get('anim') or {}; cur = aa.get(name) or {}; cur.update(kw); aa[name] = cur
        set_state(asset, anim=aa, note=f'anim {name}: {kw.get("state")}')
    if st.get('state') in (None, 'NEW'):
        est = ANIM[name]['est']; args = anim_args(ANIM[name], src) + ['--no-wait']
        auth = step_auth(asset, name)
        if est and not auth: fail('NO_AUTHORIZATION', name, "ask the user; put their words in the budget's step_approvals")
        if est: spec_gate()
        qwarn = (j.get('quality') or {}).get('warnings') or []
        qok = os.environ.get('QUALITY_OK', '').strip()
        if est and qwarn and not qok:
            fail('QUALITY_WARN_UNRESOLVED', '; '.join(qwarn), 'show the generated result to the user first; to go on with it, run again with QUALITY_OK="<user words>"')
        with stage_lock(asset):
            if est and spent(budget(asset)) + est > cap: fail('CAP_EXCEEDED', f'{spent(budget(asset))}+{est}>{cap}', 'stop; ask the user for a higher cap or a new budget')
            b0 = balance(); save(d / f'{name}-balance-before.json', dict(at=now(), **b0))
            put(state='SUBMITTING', estimate=est, source_task=src, args=args, user_approval=auth, **({'quality_ok': qok} if qwarn and qok else {}))
        code, out, err = cli(args, 180)
        save(d / f'{name}-submit.json', dict(exit=code, out=out, stderr=err))
        tid = out.get('task_id')
        if code != 0 or not tid:
            put(state='SUBMISSION_UNKNOWN', exit=code); fail('SUBMISSION_UNKNOWN', f'{name}:exit={code}', 'never resend; reconcile with balance/usage, then reset-<step> only for a definite rejection')
        put(state='SUBMITTED', task_id=tid)
        st = journal(asset)['anim'][name]
    if st.get('state') == 'SUBMITTED':
        code, out, err = cli(['task', 'watch', st['task_id']], 900)
        save(d / f'{name}-watch.json', dict(exit=code, out=out, stderr=err))
        if code != 0: fail('WATCH_FAILED', f'{name}:exit={code}', 'do not re-make; read the watch file and ask the user')
        ctask = task_credits(out)
        code, out, err = cli(['task', 'get', st['task_id'], '--download', '-o', str(d / name)], 300)
        save(d / f'{name}-result.json', dict(exit=code, out=out, stderr=err))
        if code != 0: fail('DOWNLOAD_FAILED', f'{name}:exit={code}', 'run the same step again (download only)')
        b1 = balance(); b0 = json.loads((d / f'{name}-balance-before.json').read_text())
        files = [dict(path=str(p.relative_to(ROOT)), size=p.stat().st_size, sha256=sha(p)) for p in sorted((d / name).rglob('*')) if p.is_file()]
        extra = {}
        if ANIM[name]['type'] == 'check':
            txt = json.dumps(out).lower(); extra['riggable'] = ('"riggable": true' in txt) or ('riggable": true' in txt) or ('"rig_type"' in txt)
            extra['check_output'] = out.get('output') or out
        put(state='DONE', files=files, credits_spent=int(b0['balance']) - int(b1['balance']), credits_task=ctask, **extra)
    print(json.dumps(dict(step=name, **{k: v for k, v in journal(asset)['anim'][name].items() if k in ('state', 'task_id', 'credits_spent', 'credits_task', 'riggable')})))



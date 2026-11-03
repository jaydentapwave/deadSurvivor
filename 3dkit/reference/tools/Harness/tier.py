#!/usr/bin/env python3
"""Verification tiers (docs/workflows/core-rules.md): pick the smallest Play check that a change needs and reuse
gameplay evidence when only visual files changed.

  quick : compile / static checks / tool self-tests. No Play.
  short : 1-2 min Play (enter, sortie, move, stop, console, cleanup) + capture for the user. Default during work.
  full  : the whole runner (10 waves, load, aspect ratios). Once at a stage end or before a commit, only when gameplay
          files changed since the last full PASS. Needs DS_FULL_REASON (require_full below; the Claude hook checks too).
The pre-commit gate (Tools/Verify/gate.py, docs/workflows/verification-gates.md) keeps its own Unity scope and does
not read this evidence.

Evidence is split by kind. A full PASS records per-file hashes of the gameplay set and the visual set
(output/verification/tier-evidence.json). `plan` compares the current files with that record:
  gameplay unchanged -> gameplay evidence REUSE (visual changes need only the short tier + capture),
  gameplay changed   -> short now, full at the stage end.

Usage:
  /usr/bin/python3 Tools/Harness/tier.py plan --scene <scene> [--stage-end]
  /usr/bin/python3 Tools/Harness/tier.py selftest
Runners call record(...) after a PASS and require_full(...) before a full run."""
import fnmatch, hashlib, json, os, sys, time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
POLICY = json.loads((ROOT / 'Tools/Harness/tier-policy.json').read_text())
EVIDENCE = ROOT / 'output/verification/tier-evidence.json'
FULL_LEDGER = ROOT / 'output/verification/full-runs.jsonl'


def _match(path, patterns):
    return any(fnmatch.fnmatchcase(path, p) or (p.endswith('/**') and path.startswith(p[:-3] + '/')) for p in patterns)


def classify(path):
    """docs / tools / visual / gameplay; first match in category_order wins, unknown -> gameplay (conservative)"""
    for cat in POLICY['category_order']:
        if _match(path, POLICY['categories'][cat]):
            return cat
    return POLICY['unknown_category']


def evidence_files(root=ROOT):
    out = {}
    for base in POLICY['evidence_roots']:
        for dp, dns, fns in os.walk(root / base):
            dns[:] = [d for d in dns if not d.startswith('.')]
            for fn in fns:
                rel = str((Path(dp) / fn).relative_to(root))
                if fn.startswith('.') or _match(rel, POLICY['evidence_ignore']):
                    continue
                cat = classify(rel)
                if cat in ('gameplay', 'visual'):
                    out[rel] = cat
    return out


def snapshot(root=ROOT):
    snap = {'gameplay': {}, 'visual': {}}
    for rel, cat in evidence_files(root).items():
        snap[cat][rel] = hashlib.sha256((root / rel).read_bytes()).hexdigest()
    return snap


def diff(old, new):
    keys = set(old) | set(new)
    return sorted(k for k in keys if old.get(k) != new.get(k))


def _load():
    return json.loads(EVIDENCE.read_text()) if EVIDENCE.exists() else {'records': []}


KEEP_RECORDS = 40


def _is_full_pass(r):
    return r['tier'] == 'full' and r['status'] == 'PASS'


def trim(records, keep=KEEP_RECORDS):
    """keep the newest `keep` records plus each scene's last full PASS (gameplay evidence must not age out)"""
    last_full = {r['scene']: i for i, r in enumerate(records) if _is_full_pass(r)}
    start = max(0, len(records) - keep)
    return [r for i, r in enumerate(records) if i >= start or i in last_full.values()]


def record(scene, tier, status, run, note='', captured=False):
    """called by a runner after it finishes; only PASS records count as evidence.
    captured=True only when the run really took the Game view captures; otherwise the record is gameplay evidence
    (full) but never visual evidence (KEYART045_NOCAPTURE / batch-mode editor)."""
    data = _load()
    data['records'].append(dict(scene=scene, tier=tier, status=status, run=run, note=note, captured=bool(captured),
                                time=time.strftime('%Y-%m-%dT%H:%M:%S%z'), snapshot=snapshot()))
    data['records'] = trim(data['records'])
    EVIDENCE.parent.mkdir(parents=True, exist_ok=True); EVIDENCE.write_text(json.dumps(data, ensure_ascii=False))


def plan(scene, stage_end=False, snap=None, data=None):
    """gameplay evidence = the last full PASS; visual evidence = the last PASS (short or full) that really captured.
    Records without captured=True (older records, NOCAPTURE runs) are not visual evidence."""
    snap = snap if snap is not None else snapshot()
    data = data if data is not None else _load()
    mine = [r for r in data['records'] if r['scene'] == scene and r['status'] == 'PASS']
    last_full = next((r for r in reversed(mine) if r['tier'] == 'full'), None)
    last_seen = next((r for r in reversed(mine) if r['tier'] in ('short', 'full') and r.get('captured') is True), None)
    res = dict(scene=scene, last_full=last_full and last_full['run'], last_captured=last_seen and last_seen['run'])
    if last_full is None:
        res.update(gameplay_changed=None, visual_changed=None, gameplay_evidence='NONE',
                   tier='full' if stage_end else 'short', why='no full PASS recorded for this scene')
        return res
    g = diff(last_full['snapshot']['gameplay'], snap['gameplay'])
    v = diff(last_full['snapshot']['visual'], snap['visual'])
    since_seen = None if last_seen is None else \
        diff(last_seen['snapshot']['gameplay'], snap['gameplay']) + diff(last_seen['snapshot']['visual'], snap['visual'])
    res.update(gameplay_changed=g[:20], gameplay_changed_count=len(g), visual_changed=v[:20], visual_changed_count=len(v),
               gameplay_evidence='REUSE' if not g else 'STALE')
    if g:
        res.update(tier='full' if stage_end else 'short', why='gameplay files changed since the last full PASS')
    elif last_seen is not None and not since_seen:
        res.update(tier='none', why='nothing changed since the last captured PASS')
    elif last_seen is None:
        res.update(tier='short', why='gameplay evidence reused, but no captured PASS: short Play + capture for the user')
    else:
        res.update(tier='short', why='visual only: gameplay evidence reused; short Play + capture for the user')
    return res


def require_full(runner, scene=''):
    """full runs need a written reason (stage end / pre-commit); the reason goes to output/verification/full-runs.jsonl"""
    reason = os.environ.get(POLICY['full_override_env'], '').strip()
    if not reason:
        sys.stderr.write(f"{runner}: full run blocked. Default is the short tier. For a stage end or pre-commit run set "
                         f"{POLICY['full_override_env']}=\"<reason>\" (docs/workflows/core-rules.md).\n")
        sys.exit(3)
    FULL_LEDGER.parent.mkdir(parents=True, exist_ok=True)
    with FULL_LEDGER.open('a') as f:
        f.write(json.dumps(dict(time=time.strftime('%Y-%m-%dT%H:%M:%S%z'), runner=runner, scene=scene, reason=reason), ensure_ascii=False) + '\n')
    return reason


def selftest():
    cases = {}
    cases['classify'] = (classify('docs/a.md'), classify('Tools/Proto/x.py'), classify('Assets/DeadSurvivor/Editor/A.cs'),
                         classify('Assets/DeadSurvivor/Scenes/P/Art/h.png'), classify('Assets/DeadSurvivor/Runtime/DSUI.cs'),
                         classify('Assets/DeadSurvivor/Scenes/P/Main.unity'), classify('ProjectSettings/QualitySettings.asset'),
                         classify('Assets/DeadSurvivor/Resources/DeadSurvivor/UI/PrototypeFont.asset'), classify('Assets/Other/x.bin')) == \
        ('docs', 'tools', 'tools', 'visual', 'gameplay', 'gameplay', 'gameplay', 'visual', 'gameplay')
    base = {'gameplay': {'g.cs': '1'}, 'visual': {'h.png': 'a'}}
    rec = lambda tier, snap, run='r1', status='PASS', captured=True: dict(scene='S', tier=tier, status=status, run=run, snapshot=snap, captured=captured)
    data = {'records': [rec('full', base)]}
    cases['none'] = plan('S', snap=base, data=data)['tier'] == 'none'
    vis = {'gameplay': {'g.cs': '1'}, 'visual': {'h.png': 'b'}}
    p = plan('S', snap=vis, data=data)
    cases['visual_only_reuse'] = p['tier'] == 'short' and p['gameplay_evidence'] == 'REUSE'
    cases['visual_only_after_short'] = plan('S', snap=vis, data={'records': [rec('full', base), rec('short', vis, 'r2')]})['tier'] == 'none'
    gp = {'gameplay': {'g.cs': '2'}, 'visual': {'h.png': 'a'}}
    cases['gameplay_short'] = plan('S', snap=gp, data=data)['tier'] == 'short'
    cases['gameplay_stage_end_full'] = plan('S', stage_end=True, snap=gp, data=data)['tier'] == 'full'
    cases['failed_full_not_evidence'] = plan('S', stage_end=True, snap=base, data={'records': [rec('full', base, status='FAIL')]})['tier'] == 'full'
    cases['other_scene_not_evidence'] = plan('T', stage_end=True, snap=base, data=data)['tier'] == 'full'
    cases['added_file_is_change'] = plan('S', stage_end=True, snap={'gameplay': {'g.cs': '1', 'n.cs': '1'}, 'visual': {'h.png': 'a'}}, data=data)['tier'] == 'full'
    # Codex harness r001 #3: many short records after a full PASS must not drop the full PASS
    many = trim([rec('full', base)] + [rec('short', vis, f's{i}') for i in range(KEEP_RECORDS + 5)])
    p = plan('S', stage_end=True, snap=vis, data={'records': many})
    cases['full_pass_survives_trim'] = len(many) == KEEP_RECORDS + 1 and p['gameplay_evidence'] == 'REUSE' and p['tier'] == 'none'
    cases['trim_drops_old_non_full'] = len(trim([rec('short', base, f's{i}') for i in range(KEEP_RECORDS + 5)])) == KEEP_RECORDS
    # Codex harness r001 #4: a run without captures is never visual evidence
    p = plan('S', snap=vis, data={'records': [rec('full', base), rec('short', vis, 'r2', captured=False)]})
    cases['nocapture_short_not_visual'] = p['tier'] == 'short' and p['last_captured'] == 'r1'
    p = plan('S', snap=base, data={'records': [rec('full', base, captured=False)]})
    cases['nocapture_full_gameplay_only'] = p['gameplay_evidence'] == 'REUSE' and p['tier'] == 'short'
    legacy = dict(scene='S', tier='full', status='PASS', run='old', snapshot=base)   # written before the captured field
    cases['legacy_record_not_visual'] = plan('S', snap=base, data={'records': [legacy]})['tier'] == 'short'
    old = os.environ.pop(POLICY['full_override_env'], None)
    try:
        try: require_full('selftest'); cases['full_without_reason_blocked'] = False
        except SystemExit as e: cases['full_without_reason_blocked'] = e.code == 3
    finally:
        if old is not None: os.environ[POLICY['full_override_env']] = old
    ok = all(cases.values())
    print(json.dumps(dict(status='PASS' if ok else 'FAIL', cases=cases)))
    return ok


if __name__ == '__main__':
    a = sys.argv[1:]
    if a[:1] == ['selftest']:
        sys.exit(0 if selftest() else 1)
    if a[:1] == ['plan'] and '--scene' in a:
        print(json.dumps(plan(a[a.index('--scene') + 1], stage_end='--stage-end' in a), ensure_ascii=False, indent=1))
        sys.exit(0)
    print(__doc__); sys.exit(2)

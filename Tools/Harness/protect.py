#!/usr/bin/env python3
"""Put back known editor side effects after Play, scene builds and captures (047 H4, 046-2, 047-E9).

Unity rewrites some files as a side effect (the dynamic TMP font cache PrototypeFont.asset grows after Play / captures /
scene saves). The list is `side_effect_files` in Tools/Harness/tier-policy.json.

Rule: a listed file that was clean (work tree = index = HEAD, checked separately) before the command and differs
afterwards is restored with `git checkout -- <file>`. Before overwriting, the current content is copied to
output/hook-state/protect-backup/<time>/<file> (review 048 r001 #2: someone may have edited it during the run).
A file that already had local changes before, or whose HEAD/index changed during the run, is never touched, only
reported.

  /usr/bin/python3 Tools/Harness/protect.py run -- <command ...>   run a command, then restore; prints one JSON line
                                                                     to stderr; exit code = the command's
  /usr/bin/python3 Tools/Harness/protect.py check                  list side-effect files that differ from HEAD now
  /usr/bin/python3 Tools/Harness/protect.py selftest

Python use (play_probe.py): snap = protect.snapshot(); ...; result = protect.restore(snap)
After a restore, Unity reimports the file at its next asset refresh; the in-memory copy is replaced then."""
import hashlib, json, shutil, subprocess, sys, time, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def files(root=ROOT):
    return json.loads((root / 'Tools/Harness/tier-policy.json').read_text()).get('side_effect_files', [])


def _git(root, *args):
    return subprocess.run(['git', '-C', str(root), *args], capture_output=True, text=True)


def _sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None


def _blob(root, spec):
    r = _git(root, 'rev-parse', '--verify', '-q', spec)
    return r.stdout.strip() if r.returncode == 0 else None


def _state(root, rel):
    tracked = _git(root, 'ls-files', '--error-unmatch', '--', rel).returncode == 0
    head, index = _blob(root, 'HEAD:' + rel), _blob(root, ':' + rel)
    work_eq_index = tracked and _git(root, 'diff', '--quiet', '--', rel).returncode == 0
    return dict(tracked=tracked, head=head, index=index, clean=bool(tracked and head and head == index and work_eq_index), sha=_sha(root / rel))


def snapshot(root=ROOT, paths=None):
    return {rel: _state(root, rel) for rel in (paths if paths is not None else files(root))}


def restore(snap, root=ROOT):
    """-> {'restored': [...], 'kept_dirty': [...], 'failed': [...]} (kept_dirty: changed during the run but already
    modified before it — possibly the user's work — so left alone)"""
    res = dict(restored=[], kept_dirty=[], failed=[], backups=[])
    stamp = time.strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8]   # per call (review 048 r002: same-second calls)
    for rel, before in snap.items():
        now = _state(root, rel)
        if now['sha'] == before['sha']:
            continue
        if not before['clean'] or (now['head'], now['index']) != (before['head'], before['index']):
            res['kept_dirty'].append(rel); continue          # not ours to reset: dirty before, or committed/staged meanwhile
        p = root / rel
        if p.exists():
            bak = root / 'output/hook-state/protect-backup' / stamp / rel
            try:
                bak.parent.mkdir(parents=True, exist_ok=True)
                with p.open('rb') as src, bak.open('xb') as dst:   # 'x': never replace an existing copy
                    shutil.copyfileobj(src, dst)
            except OSError:
                res['failed'].append(rel); continue          # no recoverable copy -> do not overwrite
            res['backups'].append(str(bak.relative_to(root)))
        r = _git(root, 'checkout', '--', rel)
        (res['restored'] if r.returncode == 0 and _state(root, rel)['clean'] else res['failed']).append(rel)
    return res


def check(root=ROOT):
    return [rel for rel in files(root) if _git(root, 'diff', '--quiet', 'HEAD', '--', rel).returncode != 0]


def selftest():
    import tempfile
    c = {}
    with tempfile.TemporaryDirectory() as td:
        r = Path(td); _git(r, 'init', '-q'); _git(r, 'config', 'user.email', 't@t'); _git(r, 'config', 'user.name', 't')
        for n in ('font.asset', 'mine.asset'):
            (r / n).write_text('v1')
        _git(r, 'add', '.'); _git(r, 'commit', '-qm', 'x')
        (r / 'mine.asset').write_text('user edit')                       # already dirty before the run
        snap = snapshot(r, ['font.asset', 'mine.asset', 'new.asset'])
        (r / 'font.asset').write_text('cache grew'); (r / 'mine.asset').write_text('user edit + side effect'); (r / 'new.asset').write_text('x')
        res = restore(snap, r)
        c['clean_file_restored'] = res['restored'] == ['font.asset'] and (r / 'font.asset').read_text() == 'v1'
        c['overwritten_content_backed_up'] = len(res['backups']) == 1 and (r / res['backups'][0]).read_text() == 'cache grew'
        c['dirty_file_kept'] = 'mine.asset' in res['kept_dirty'] and (r / 'mine.asset').read_text() == 'user edit + side effect'
        c['untracked_not_deleted'] = 'new.asset' in res['kept_dirty'] and (r / 'new.asset').exists()
        c['unchanged_silent'] = restore(snapshot(r, ['font.asset']), r) == dict(restored=[], kept_dirty=[], failed=[], backups=[])
        # staged-only change before the run is not clean (index != HEAD)
        (r / 'font.asset').write_text('staged'); _git(r, 'add', 'font.asset'); (r / 'font.asset').write_text('v1')
        c['staged_is_not_clean'] = snapshot(r, ['font.asset'])['font.asset']['clean'] is False
        _git(r, 'reset', '-q', '--', 'font.asset'); (r / 'font.asset').write_text('v1')
        # committed during the run (another session): HEAD changed -> conflict, not reset
        snap2 = snapshot(r, ['font.asset']); (r / 'font.asset').write_text('v2'); _git(r, 'commit', '-qam', 'y'); (r / 'font.asset').write_text('v3')
        res2 = restore(snap2, r)
        c['head_changed_kept'] = res2['kept_dirty'] == ['font.asset'] and (r / 'font.asset').read_text() == 'v3'
        # review 048 r002: two restores in the same second keep both overwritten contents
        _git(r, 'checkout', '-q', '--', 'font.asset'); got = []
        for edit in ('edit-A', 'edit-B'):
            sn = snapshot(r, ['font.asset']); (r / 'font.asset').write_text(edit); got += restore(sn, r)['backups']
        c['same_second_backups_distinct'] = len(set(got)) == 2 and sorted((r / b).read_text() for b in got) == ['edit-A', 'edit-B']
    c['policy_lists_font'] = 'Assets/DeadSurvivor/Resources/DeadSurvivor/UI/PrototypeFont.asset' in files()
    ok = all(c.values())
    print(json.dumps({'status': 'PASS' if ok else 'FAIL', 'cases': c}))
    return ok


def main():
    a = sys.argv[1:]
    if a[:1] == ['selftest']:
        sys.exit(0 if selftest() else 1)
    if a[:1] == ['check']:
        print(json.dumps({'side_effect_files_changed': check()}, ensure_ascii=False)); return
    if a[:1] == ['run'] and '--' in a:
        cmd = a[a.index('--') + 1:]
        snap = snapshot()
        try:
            code = subprocess.run(cmd).returncode
        finally:
            print(json.dumps({'protect': restore(snap)}, ensure_ascii=False), file=sys.stderr)
        sys.exit(code)
    sys.exit(__doc__)


if __name__ == '__main__':
    main()

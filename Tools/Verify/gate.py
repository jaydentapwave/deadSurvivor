#!/usr/bin/env python3
"""Local quality gate shared by Codex Stop and Git pre-commit. No Editor launch."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import uuid
from gate_support import runtime_state, evidence_context, validate_unity_report, digest

ROOT = Path(__file__).resolve().parents[2]
PYTHON = '/usr/bin/python3'
REQUIRED = ('AGENTS.md', 'Tools/Verify/run.py', 'Tools/Verify/gate.py',
            'Tools/Verify/gate_support.py', 'Tools/Verify/gate-policy.json',
            '.githooks/pre-commit', '.codex/hooks.json')


def git(root, *args):
    return subprocess.run(['git', '-C', str(root), *args], check=True, capture_output=True).stdout


def source_snapshot(root):
    paths = set(os.fsdecode(x) for x in git(root, 'ls-files', '--cached', '--others', '--exclude-standard', '-z').split(b'\0') if x)
    paths.update(REQUIRED)
    result = {}
    for rel in sorted(paths):
        p = root / rel
        if p.is_symlink():
            raise ValueError('Symlink source rejected: ' + rel)
        if not p.resolve().is_relative_to(root.resolve()):
            raise ValueError('Source outside project: ' + rel)
        if not p.exists():
            result[rel] = {'missing': True}
        elif p.is_file():
            result[rel] = {'sha256': digest(p), 'mode': '100755' if p.stat().st_mode & stat.S_IXUSR else '100644'}
        else:
            raise ValueError('Non-file source: ' + rel)
    for rel in REQUIRED:
        if result[rel].get('missing'):
            raise ValueError('Required gate/input file absent: ' + rel)
    return result


def index_snapshot(root):
    result = {}
    for row in git(root, 'ls-files', '--stage', '-z').split(b'\0'):
        if not row:
            continue
        info, name = row.split(b'\t', 1)
        mode, oid, stage = info.decode().split()
        if stage != '0' or mode not in ('100644', '100755'):
            raise ValueError('Unmerged/link/submodule index entry: ' + os.fsdecode(name))
        result[os.fsdecode(name)] = {'mode': mode, 'sha256': hashlib.sha256(git(root, 'cat-file', 'blob', oid)).hexdigest()}
    return result


def require_index_match(source, index):
    # All non-ignored project inputs must be included; no automatic staging.
    current = {p: v for p, v in source.items() if not v.get('missing')}
    if current != index:
        names = sorted(p for p in set(current) | set(index) if current.get(p) != index.get(p))
        raise ValueError('Index differs from checked files; stage intended inputs/deletions/modes first: ' + ', '.join(names[:12]))


def run_check(command, report_kind, root, timeout=30):
    existing_reports = {p.resolve() for p in (root / 'output/verification').glob('*/*.json')}
    proc = subprocess.run(command, cwd=root, capture_output=True, text=True, timeout=timeout)
    if proc.returncode:
        raise ValueError('Check command failed: ' + ' '.join(command[-2:]) + '\n' + proc.stderr[-1500:] + proc.stdout[-1500:])
    try:
        summary = json.loads(proc.stdout)
        path = Path(summary['report']).resolve()
        if not path.is_relative_to((root / 'output/verification').resolve()):
            raise ValueError('Report outside project output')
        if path in existing_reports:
            raise ValueError('Old report returned instead of a fresh check')
        report = json.loads(path.read_text())
    except (ValueError, KeyError, OSError, TypeError) as error:
        raise ValueError('Missing/malformed check report: ' + str(error))
    if summary.get('status') != 'PASS' or report.get('status') != 'PASS':
        raise ValueError('Check result is not PASS: ' + str(path))
    if report_kind == 'local':
        results = report.get('results', {})
        if any(results.get(k, {}).get('status') != 'PASS' for k in ('docs', 'data', 'source_stability')) or results.get('docs', {}).get('files', 0) < 1:
            raise ValueError('Local result is empty/incomplete')
    elif report_kind == 'map':
        checks = report.get('checks', [])
        if len(checks) < 151 or any(x.get('status') != 'PASS' for x in checks):
            raise ValueError('Map preparation checks missing/failed')
    return {'status': 'PASS', 'report': str(path.relative_to(root)), 'sha256': digest(path)}


def active_map_check(root, policy):
    # Historical MAP007 inputs.json is deliberately not compared to today's code.
    spec = policy['active_map']
    layout_path = root / spec['layout']
    layout = json.loads(layout_path.read_text())
    image = root / spec['reference']
    if digest(image) != layout['source_sha256']:
        raise ValueError('Active map reference image fingerprint changed')
    import csv
    cases = list(csv.DictReader((root / spec['cases']).open(encoding='utf-8-sig')))
    if len(cases) != 56 or any(r.get('execution') != 'NOT_RUN' for r in cases):
        raise ValueError('Preparation case list changed; update and review gate scope')
    for sheet in spec['sheets']:
        svg = root / sheet
        png = svg.with_suffix('.png')
        if not svg.is_file() or not png.is_file() or png.stat().st_size < 8 or png.read_bytes()[:8] != b'\x89PNG\r\n\x1a\n':
            raise ValueError('Missing diagram pair: ' + sheet)
    manifest = json.loads((root / spec['render_manifest']).read_text())
    expected = set(spec['sheets']) | {str(Path(x).with_suffix('.png')) for x in spec['sheets']}
    if manifest.get('layout_sha256') != digest(layout_path) or set(manifest.get('files', {})) != expected:
        raise ValueError('Stale or incomplete diagram export manifest')
    for rel, value in manifest['files'].items():
        if digest(root / rel) != value:
            raise ValueError('Diagram changed after export: ' + rel)
    return {'status': 'PASS', 'scope': 'active map source, candidate plans and artifact presence; not gameplay'}


def validate_policy_paths(root, policy):
    spec = policy['active_map']
    paths = [spec[k] for k in ('layout', 'reference', 'cases', 'audit', 'render_manifest')] + spec['sheets']
    for rel in paths:
        p = root / rel
        if Path(rel).is_absolute() or p.is_symlink() or not p.resolve().is_relative_to(root.resolve()) or not p.is_file():
            raise ValueError('Invalid active map dependency path: ' + rel)


def runtime_requirement(root, policy):
    now = runtime_state(root)
    baseline = policy['runtime_baseline']
    if now == baseline:
        return {'status': 'PASS', 'scope': 'unchanged installation baseline; NOT a new runtime validation'}
    if now['art'] != baseline['art']:
        raise ValueError('BLOCKED: UnityArtExport changed; verified source-to-target art adapter is not implemented. Do not rebaseline to bypass.')
    context = evidence_context(root)
    for p in sorted((root / 'output/verification').glob('*/summary.json'), reverse=True):
        try:
            report = json.loads(p.read_text())
            stamp = datetime.datetime.fromisoformat(report['time'])
            if stamp.tzinfo is None or stamp < datetime.datetime.fromisoformat(policy['installed_at']) or stamp > datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(seconds=5):
                continue
            valid, why = validate_unity_report(report, p.parent, context)
            if valid:
                return {'status': 'PASS', 'scope': why, 'report': str(p.relative_to(root)), 'sha256': digest(p)}
        except (OSError, ValueError, KeyError, TypeError):
            continue
    raise ValueError('BLOCKED: runtime/target changed without current complete all --screens evidence. Follow Unity verification procedure; the hook will not start or sync Unity.')


def execute(root, mode):
    start = datetime.datetime.now().astimezone()
    out = root / 'output/verification' / ('gate-' + start.strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8])
    out.mkdir(parents=True)
    result = {'time': start.isoformat(), 'mode': mode, 'status': 'FAIL', 'checks': {}, 'runtime_scope': 'not automatically executed'}
    try:
        before = source_snapshot(root)
        runtime_before = runtime_state(root)
        index_before = index_snapshot(root) if mode == 'pre-commit' else None
        if index_before is not None:
            require_index_match(before, index_before)
        policy = json.loads((root / 'Tools/Verify/gate-policy.json').read_text())
        if policy.get('version') != 1:
            raise ValueError('Unsupported gate policy')
        validate_policy_paths(root, policy)
        result['checks']['local'] = run_check([PYTHON, str(root / 'Tools/Verify/run.py'), 'local'], 'local', root)
        result['checks']['map_preparation'] = run_check([PYTHON, str(root / policy['active_map']['audit'])], 'map', root)
        result['checks']['map_dependencies'] = active_map_check(root, policy)
        result['checks']['runtime_evidence'] = runtime_requirement(root, policy)
        if source_snapshot(root) != before:
            raise ValueError('Source changed during checks; re-run against final files')
        if index_before is not None and index_snapshot(root) != index_before:
            raise ValueError('Git index changed during checks')
        if runtime_state(root) != runtime_before:
            raise ValueError('Unity source/art/target changed during checks; re-run')
        result['source_sha256'] = before
        result['status'] = 'PASS'
    except (Exception,) as error:
        result['error'] = str(error)
        result['status'] = 'BLOCKED' if isinstance(error, subprocess.TimeoutExpired) or 'BLOCKED:' in str(error) else 'FAIL'
    report = out / 'gate.json'
    report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    return result, report


def stop_response(result, report):
    if result['status'] == 'PASS':
        return {'systemMessage': 'Project verification gate PASS: ' + str(report)}
    return {'decision': 'block', 'reason': '프로젝트 필수 검사가 통과하지 않았습니다. 완료로 보고하지 말고 원인을 해결한 뒤 다시 검사하세요. 검사 기준이나 hook을 끄지 마세요. 실행 불가이면 근거와 BLOCKED 상태를 사용자에게 설명하세요. ' + result.get('error', '') + ' 기록: ' + str(report)}


def main():
    mode = sys.argv[1] if len(sys.argv) == 2 else ''
    if mode not in ('check', 'stop', 'pre-commit'):
        print('Usage: gate.py check|stop|pre-commit', file=sys.stderr)
        return 2
    if mode == 'stop':
        try:
            payload = json.load(sys.stdin)
            cwd = Path(payload['cwd']).resolve()
            if payload.get('hook_event_name') != 'Stop' or not cwd.is_relative_to(ROOT):
                raise ValueError('Wrong hook event or cwd; no checks executed')
        except Exception as error:
            print(json.dumps({'decision': 'block', 'reason': str(error)}, ensure_ascii=False))
            return 0
    elif not Path.cwd().resolve().is_relative_to(ROOT):
        print('Wrong project cwd', file=sys.stderr)
        return 2
    try:
        result, report = execute(ROOT, mode)
    except Exception as error:
        # Even output-directory/permission errors must not become a silent Stop success.
        if mode == 'stop':
            print(json.dumps({'decision': 'block', 'reason': '검사 실행/기록 불가: ' + str(error)}, ensure_ascii=False))
            return 0
        print('Gate could not execute: ' + str(error), file=sys.stderr)
        return 1
    if mode == 'stop':
        print(json.dumps(stop_response(result, report), ensure_ascii=False))
        return 0
    print(json.dumps({'status': result['status'], 'report': str(report), 'error': result.get('error')}, ensure_ascii=False))
    return 0 if result['status'] == 'PASS' else 1


if __name__ == '__main__':
    sys.exit(main())

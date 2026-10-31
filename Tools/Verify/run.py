#!/usr/bin/env python3
"""Local, read-only source checks. Writes only fresh reports under output/."""
import argparse
import hashlib
import json
from pathlib import Path
import re
from datetime import datetime
from zoneinfo import ZoneInfo
import uuid
from gate_support import evidence_context, artifact_hashes
from report import brief

ROOT = Path(__file__).resolve().parents[2]

def documents(root):
    errors = []
    paths = [root / n for n in ('README.md', 'AGENTS.md', 'CLAUDE.md', '할일.md')]
    for directory in ('docs', '.agents/skills', '.claude/agents'):
        paths += sorted((root / directory).rglob('*.md'))
    for path in paths:
        if not path.is_file():
            errors.append(f'Missing {path.relative_to(root)}')
            continue
        for target in re.findall(r'\]\(([^)]+)\)', path.read_text()):
            target = target.split('#')[0].strip('<>')
            if not target or '://' in target or target.startswith('mailto:'):
                continue
            resolved = (path.parent / target).resolve()
            if not resolved.is_relative_to(root.resolve()):
                errors.append(f'External file link: {path.relative_to(root)} -> {target}')
            elif not resolved.exists():
                errors.append(f'Broken link: {path.relative_to(root)} -> {target}')
    return {'status': 'FAIL' if errors else 'PASS', 'files': len(paths), 'errors': errors}

def data_check(root):
    errors = []
    data = json.loads((root / 'UnityMigration/Data/PrototypeData.json').read_text())
    ids = {}
    for section in ('characters', 'items', 'monsters'):
        rows = data[section]
        values = [row['id'] for row in rows]
        if len(values) != len(set(values)) or any(not v for v in values):
            errors.append(f'{section}: empty/duplicate id')
        ids[section] = set(values)
    for character in data['characters']:
        if character['weapon'] not in ids['items']:
            errors.append(f'Unknown starting weapon: {character["id"]}')
    for weight in data['settings']['weights']:
        if weight['id'] not in ids['items'] or weight['weight'] < 0:
            errors.append(f'Invalid drop weight: {weight["id"]}')
    for index, wave in enumerate(data['settings']['waves']):
        if any(t not in ids['monsters'] for t in wave['types']):
            errors.append(f'Unknown monster: wave {index + 1}')
    return {'status': 'FAIL' if errors else 'PASS', 'errors': errors,
            'scope': 'IDs and references only; not gameplay or balance verification'}

def map_oracle_foundation(root):
    from map_check import foundation_check
    result = foundation_check(root / 'docs/design/map-harness-fixtures.json')
    return {'status': result['status'], 'scope': result['scope'],
            'unity_status': result['unity_status'], 'checks_run': len(result['checks']),
            'failed_checks': [c for c in result['checks'] if c['check_status'] != 'PASS'],
            'errors': result['errors']}

def fingerprint():
    hashes = {}
    for directory in ('UnityMigration', 'Tools/Verify', 'docs', '.agents', '.claude'):
        for path in sorted((ROOT / directory).rglob('*')):
            if path.is_file() and path.suffix in ('.py', '.cs', '.json', '.md', '.svg'):
                hashes[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
    for name in ('AGENTS.md', 'CLAUDE.md', 'README.md', '할일.md', '.editorconfig'):
        if (ROOT / name).is_file():
            hashes[name] = hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
        else:
            hashes[name] = None
    return hashes

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', choices=['docs', 'data', 'local', 'unity', 'all'])
    parser.add_argument("--loadouts", action="store_true")
    parser.add_argument("--experiments", action="store_true")
    parser.add_argument("--screens", action="store_true")
    parser.add_argument('--verbose', action='store_true', help='Print full results; default is a compact summary')
    parser.add_argument("--project", default="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor")
    args = parser.parse_args()
    now = datetime.now(ZoneInfo("Asia/Seoul"))
    run_id = now.strftime("%Y%m%d-%H%M%S") + "-" + uuid.uuid4().hex[:8]
    out = ROOT / "output/verification" / run_id
    out.mkdir(parents=True)
    hashes = fingerprint()
    results = {}
    gate_before = None
    if args.stage in ('unity', 'all'):
        gate_before = evidence_context(ROOT, Path(args.project))
    for name, fn in [('docs', documents), ('data', data_check),
                     ('map_oracle_foundation', map_oracle_foundation)]:
        if args.stage in (name, 'local', 'all'):
            try:
                results[name] = fn(ROOT)
            except Exception as error:
                results[name] = {'status': 'FAIL', 'errors': [str(error)]}
    if args.stage in ('unity','all'):
        from unity_check import run_unity
        results['unity'] = run_unity(ROOT, args.project, out/'unity', screens=args.screens, experiments=args.experiments, loadouts=args.loadouts)
    results['source_stability'] = {'status': 'PASS' if hashes == fingerprint() else 'FAIL'}
    statuses = [r['status'] for r in results.values()]
    code = 1 if 'FAIL' in statuses else 2 if any(s != 'PASS' for s in statuses) else 0
    report = {'id': run_id, 'time': now.isoformat(), 'stage': args.stage,
              'status': 'PASS' if code == 0 else 'FAIL' if code == 1 else 'BLOCKED',
              'results': results, 'sha256': hashes}
    if args.stage in ('unity', 'all'):
        report['options'] = {'project': str(Path(args.project).resolve()),
                             'screens': args.screens, 'experiments': args.experiments, 'loadouts': args.loadouts}
        report['gate_before'] = gate_before
        report['gate_after'] = evidence_context(ROOT, Path(args.project))
        report['unity_artifact_sha256'] = artifact_hashes(out / 'unity')
        if report['gate_before'] != report['gate_after']:
            results['gate_source_stability'] = {'status': 'FAIL'}
            report['status'] = 'FAIL'
            code = 1
    out = ROOT / 'output/verification' / run_id
    (out / 'summary.json').write_text(json.dumps(report, ensure_ascii=False, indent=2))
    displayed = ({'status': report['status'], 'results': results, 'report': str(out/'summary.json')}
                 if args.verbose else brief(report, out/'summary.json'))
    print(json.dumps(displayed, ensure_ascii=False))
    return code

if __name__ == '__main__':
    raise SystemExit(main())

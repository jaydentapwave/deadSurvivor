"""Bounded views of stored evidence. This reader never reruns or approves checks."""
import argparse
from collections import Counter
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STATES = {'PASS', 'FAIL', 'BLOCKED', 'NOT_RUN'}


def entries(report, prefix=''):
    for name, value in report.items():
        if name.endswith('_status') and isinstance(value,str) and value in STATES:
            yield prefix+name, {'status':value,'reason':'Explicit scope marker; see full report'}
    children = report.get('results', report.get('checks', {}))
    items = children.items() if isinstance(children,dict) else enumerate(children) if isinstance(children,list) else []
    for name, value in items:
        if not isinstance(value, dict):
            continue
        path = prefix + str(name)
        if 'status' in value:
            yield path, value
        yield from entries(value, path + '.')


def brief(report, path):
    """Only presentation is bounded; saved evidence and its verdict stay intact."""
    rows = list(entries(report))
    problems = [(name, value) for name, value in rows if value.get('status') != 'PASS']
    def reason(value):
        text = value.get('error', value.get('errors', value.get('reason', 'See full report')))
        if not isinstance(text, str):
            text = json.dumps(text, ensure_ascii=False)
        return text[:240]
    result = {
        'status': report.get('status', 'BLOCKED'), 'report': str(path),
        'checks': {name[:100]: value['status'] for name, value in rows[:12]},
        'status_counts': dict(Counter(value['status'] for _, value in rows)),
        'checks_omitted': max(0, len(rows)-12),
        'problems': [{'check': name[:100], 'status': value['status'], 'reason': reason(value)}
                     for name, value in problems[:3]],
        'problems_omitted': max(0, len(problems)-3),
    }
    if report.get('error'):
        result['error'] = reason(report)
    return result


def select(report, check):
    for name, value in entries(report):
        if name == check:
            return value
    raise ValueError('Unknown check: ' + check)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('report')
    parser.add_argument('--check', help='Exact dotted check name from the summary')
    parser.add_argument('--verbose', action='store_true', help='Print full stored JSON')
    args = parser.parse_args()
    try:
        path = Path(args.report).resolve()
        if not path.is_relative_to((ROOT/'output').resolve()):
            raise ValueError('Report must be in project output')
        data = json.loads(path.read_text())
        if not isinstance(data, dict) or data.get('status') not in STATES:
            raise ValueError('Missing or invalid report status')
        if args.check:
            node = select(data, args.check)
            text = json.dumps(node, ensure_ascii=False, indent=2)
            result = {'status': node['status'], 'report': str(path), 'check': args.check,
                      'detail': text if args.verbose else text[:4000],
                      'characters_omitted': 0 if args.verbose else max(0, len(text)-4000)}
        else:
            result = data if args.verbose else brief(data, path)
        print(json.dumps(result, ensure_ascii=False))
        state = result.get('status', 'BLOCKED')
        return 0 if state == 'PASS' else 1 if state == 'FAIL' else 2
    except (ValueError, OSError, TypeError, AttributeError) as error:
        print(json.dumps({'status': 'BLOCKED', 'error': str(error)}, ensure_ascii=False))
        return 2


if __name__ == '__main__':
    raise SystemExit(main())

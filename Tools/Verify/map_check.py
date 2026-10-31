"""H1 foundation analysis only. Does not run or judge a Unity map session."""
import argparse
from datetime import datetime
import hashlib
import json
from pathlib import Path
import uuid
from zoneinfo import ZoneInfo

from map_io import MapInputError, load_json, reject_symlinks, read_input_bytes
from map_oracle import Geometry, assess_sweep

ROOT = Path(__file__).resolve().parents[2]
FIXTURES = ROOT / 'docs/design/map-harness-fixtures.json'
SCOPE = 'H1 independent oracle analysis only; not Unity/G2 acceptance'


def foundation_check(fixture_path=FIXTURES):
    checks = []
    try:
        data = load_json(fixture_path)
        if (not isinstance(data, dict) or type(data.get('schema_version')) is not int or data['schema_version'] != 1
                or data.get('document_role') != 'DESIGN_EXAMPLES_NOT_EXECUTABLE_REQUEST'):
            raise MapInputError('INVALID_SCHEMA', 'Expected version 1 design examples')
        cases, vectors = data['cases'], data['oracle_vectors']
        required_cases = {'NORMAL-EMPTY', 'NORMAL-WALL', 'FI03-THIN-WALL'}
        required_vectors = {'BODY_OVERLAP_OUTSIDE_CENTER', 'EPSILON_BAND',
                            'CENTER_INSIDE_SOLID', 'BODY_OUTSIDE_BOUNDARY',
                            'CENTER_OUTSIDE_BOUNDARY'}
        for rows, expected in [(cases, required_cases), (vectors, required_vectors)]:
            ids = [row['id'] for row in rows]
            if len(ids) != len(set(ids)) or set(ids) != expected:
                raise MapInputError('CASE_SET_MISMATCH', 'Missing, unexpected or duplicate IDs')
        by_id = {c['id']: c for c in cases}
        wall = Geometry.from_polygons(by_id['NORMAL-WALL']['boundary'],
                                      by_id['NORMAL-WALL']['obstacles'])
        empty = Geometry.from_polygons(by_id['NORMAL-EMPTY']['boundary'], [])

        def check(case_id, geometry, start, end, radius, epsilon, expected, gap=None):
            if expected not in ('PASS', 'FAIL', 'BLOCKED'):
                raise MapInputError('INVALID_EXPECTATION', case_id)
            observed = assess_sweep(geometry, start, end, radius, epsilon)
            correct = observed['status'] == expected
            if gap is not None:
                # Numeric assertion is independent of status; witness gaps are local only.
                correct = correct and abs(observed['body_clearance'] - gap) <= 1e-10
            checks.append({'id': case_id, 'expected_status': expected,
                           'expected_gap': gap, 'observed': observed,
                           'check_status': 'PASS' if correct else 'FAIL'})

        for row in vectors:
            geometry = wall if row['surface'] == 'wall_x_3.4_to_3.6' else empty
            if row['surface'] not in ('wall_x_3.4_to_3.6', 'boundary_x_500'):
                raise MapInputError('UNKNOWN_SURFACE', row['surface'])
            check('ANALYTIC-' + row['id'], geometry, row['center'], row['center'],
                  row['radius'], row['epsilon'], row['expected'], row['body_gap'])
        check('ANALYTIC-EMPTY-SWEEP', empty, [0, 0], [4.2, 0], .25, .0001, 'PASS', 495.55)
        check('ANALYTIC-THIN-WALL-CROSSING', wall, [0, 0], [4.2, 0], .25, .0001, 'FAIL')
        check('ANALYTIC-BEFORE-WALL', wall, [0, 0], [3.1, 0], .25, .0001, 'PASS', .05)
        for source in ('NORMAL-EMPTY', 'NORMAL-WALL', 'FI03-THIN-WALL'):
            if by_id[source]['runtime_status'] != 'NOT_RUN':
                raise MapInputError('RUNTIME_CLAIM_CHANGED', 'Design example runtime marker changed')
    except (MapInputError, KeyError, TypeError, ValueError) as error:
        status = 'FAIL' if any(c['check_status'] == 'FAIL' for c in checks) else getattr(error, 'status', 'FAIL')
        return {'status': status, 'scope': SCOPE,
                'unity_status': 'NOT_RUN', 'checks': checks,
                'errors': [{'code': getattr(error, 'code', 'INVALID_FIXTURE'), 'message': str(error)}]}
    return {'status': 'PASS' if checks and all(c['check_status'] == 'PASS' for c in checks)
            else 'FAIL', 'scope': SCOPE, 'unity_status': 'NOT_RUN', 'checks': checks, 'errors': []}


def fingerprints(paths, root):
    return {str(p.relative_to(root)): hashlib.sha256(read_input_bytes(p)).hexdigest() for p in paths}


def failure_result(error, previous):
    failed = previous.get('status') == 'FAIL' or any(
        c['check_status'] == 'FAIL' for c in previous.get('checks', []))
    status = 'FAIL' if failed else getattr(error, 'status', 'BLOCKED' if isinstance(error, OSError) else 'FAIL')
    return dict(previous, status=status, errors=previous.get('errors', []) + [
        {'code': getattr(error, 'code', 'IO_ERROR' if isinstance(error, OSError) else 'INTERNAL_ERROR'),
         'message': str(error)}])


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['oracle-selftest'])
    parser.parse_args(argv)
    now = datetime.now(ZoneInfo('Asia/Seoul'))
    run_id = 'map-oracle-' + now.strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex
    result = {'status': 'NOT_RUN', 'scope': SCOPE, 'unity_status': 'NOT_RUN', 'checks': [], 'errors': []}
    hashes, report = {}, None
    try:
        output_root = reject_symlinks(ROOT / 'output/verification')
        out = output_root / run_id / 'map'
        out.mkdir(parents=True, exist_ok=False)
        paths = [FIXTURES] + [Path(__file__).with_name(n) for n in
                             ('map_check.py', 'map_io.py', 'map_oracle.py', 'test_map_foundation.py')]
        try:
            hashes = fingerprints(paths, ROOT)
            result = foundation_check()
            if hashes != fingerprints(paths, ROOT):
                result = failure_result(MapInputError('SOURCE_CHANGED', 'Inputs changed during analysis'), result)
        except (OSError, MapInputError, ValueError, TypeError) as error:
            result = failure_result(error, result)
        result.update(run_id=run_id, time=now.isoformat(), sha256=hashes)
        temp = out / 'summary.tmp'
        temp.write_text(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + '\n')
        temp.replace(out / 'summary.json')
        report = str(out / 'summary.json')
    except (OSError, MapInputError) as error:
        result = failure_result(error, result)
    print(json.dumps({'status': result['status'], 'report': report, 'run_id': run_id,
                      'scope': SCOPE, 'unity_status': 'NOT_RUN',
                      'errors': result['errors']}, ensure_ascii=False))
    return {'PASS': 0, 'FAIL': 1, 'BLOCKED': 2, 'NOT_RUN': 2}[result['status']]


if __name__ == '__main__':
    raise SystemExit(main())

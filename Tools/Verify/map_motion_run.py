#!/usr/bin/env python3
"""Owned Unity movement fixtures. Separate from full map v1/G2 acceptance."""
import argparse
from contextlib import contextmanager
from copy import deepcopy
from datetime import datetime
import fcntl
import hashlib
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid
from zoneinfo import ZoneInfo

from map_io import load_json
from map_oracle import Geometry, assess_sweep

ROOT = Path(__file__).resolve().parents[2]
PROJECT = Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor')
BASE = ROOT / 'output/map-i2-017'


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write(path, value):
    path = Path(path)
    temp = path.with_suffix(path.suffix + '.pending')
    with temp.open('x') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2, allow_nan=False)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temp, path)


@contextmanager
def editor_lock(base=BASE):
    base.mkdir(parents=True, exist_ok=True)
    with (base / '.editor.lock').open('a') as stream:
        try:
            fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise RuntimeError('EDITOR_LOCKED') from error
        try:
            yield
        finally:
            fcntl.flock(stream, fcntl.LOCK_UN)


def vec(x, y):
    return {'x': x, 'y': y}


def box(x0, y0, x1, y1):
    return [vec(x0, y0), vec(x1, y0), vec(x1, y1), vec(x0, y1)]


def fixtures():
    rows = []

    def case(name, *, n=1, f=0, center=(0, 0), actual=None, boundary=None,
             obstacles=(), frames=36, direction=(1, 0), dt=1/60, speed=1,
             goal=None, bypass=-1, error='', alive=None, elapsed=0, mode='FIXED_REPLAY'):
        spec = dict(id=name, mode=mode, count=n, formation=f, center=vec(*center),
                    actual=[] if actual is None else [vec(*p) for p in actual],
                    boundary=boundary or box(-400, -400, 400, 400),
                    obstacles=[{'points': p} for p in obstacles],
                    radii=[8]*n, center_radius=8, epsilon=.001, speed=speed,
                    alive=alive or [True]*n, bypass_step=bypass, expected_error=error,
                    elapsed=elapsed, inputs=[dict(input=vec(*direction), frame_dt=dt) for _ in range(frames)])
        rows.append(dict(spec=spec, goal=goal or {}))
        return spec

    for n in range(1, 5):
        for f in range(3):
            for repeat in range(2):
                case(f'OPEN-N{n}-F{f}-R{repeat}', n=n, f=f,
                     goal={'center_x': [83.99, 84.01]})
    case('THIN', center=(-12, 0), obstacles=[box(0, -100, .01, 100)],
         frames=1, dt=.05, speed=4, goal={'center_x': [-8.063, -8.062]})
    case('THIN-BYPASS', center=(-12, 0), obstacles=[box(0, -100, .01, 100)],
         frames=1, dt=.05, speed=4, bypass=0, error='SWEPT_PENETRATION')
    case('CENTER-BYPASS', n=2, f=2, center=(0, -20), obstacles=[box(-1, -1, 1, 1)],
         direction=(0, 1), frames=1, dt=.05, speed=6, bypass=0, error='SWEPT_PENETRATION',
         goal={'failure_surface': 'center'})
    case('WALL-PARTY', n=4, f=2, obstacles=[box(120, -300, 120.05, 300)],
         frames=70, goal={'center_x': [39.9, 40]})
    u = [vec(-50, -50), vec(50, -50), vec(50, 50), vec(20, 50),
         vec(20, -20), vec(-20, -20), vec(-20, 50), vec(-50, 50)]
    case('CONCAVE-NOTCH', obstacles=[u], direction=(0, 1), frames=1, dt=.05,
         goal={'center_y': [6.999, 7.001]})
    case('BAND', obstacles=[box(-400, -200, 400, -50), box(-400, 50, 400, 200)],
         goal={'center_x': [83.99, 84.01]})
    case('BOUNDARY', frames=180, goal={'center_x': [391.9, 392]})
    case('ZERO-FOLLOW', center=(0, 0), actual=[(-90, 0)], direction=(0, 0),
         goal={'actual0_x': [-.04, .04]})
    case('ZERO-FOLLOW-WALL', actual=[(-90, 0)], direction=(0, 0),
         obstacles=[box(-50, -80, -49.95, 80)], goal={'actual0_x': [-72.001, -71.999]})
    wedge = [(0, -48), (-48, 0), (48, 0), (-48, 48)]
    case('NONMONOTONIC', n=4, f=1, actual=wedge, boundary=box(-200, -200, 200, 60),
         obstacles=[box(-31, 39.5, -29, 40.5)], frames=1, direction=(0, 1), dt=.05,
         goal={'center_y': [3.992, 3.993]})
    case('ALL-HELD', n=4, f=1, actual=wedge, boundary=box(-200, -200, 200, 60),
         obstacles=[box(-31, 39.5, -29, 40.5)], frames=1, direction=(0, 0), dt=.05,
         goal={'actual3_x': [-48.001, -47.999], 'actual3_y': [47.999, 48.001]})
    case('ALPHA-ZERO-FOLLOW', n=4, f=1, actual=wedge[:3]+[(0, 44)], boundary=box(-200, -200, 200, 56.01),
         frames=1, direction=(0, 1), dt=.05, goal={'center_y': [-.0001, .0001], 'actual3_y': [46.399, 46.401]})
    case('DEAD-RESERVED', n=4, f=1, actual=wedge[:3]+[(-500, -500)], boundary=box(-200, -200, 200, 60),
         alive=[True, True, True, False], frames=1, direction=(0, 1), dt=.05,
         goal={'center_y': [3.992, 3.993], 'actual3_y': [-500.001, -499.999]})
    s = case('REVERSE', n=4, f=1, frames=72, goal={'center_x': [-.01, .01], 'step35_center_x': [83.99, 84.01]})
    for row in s['inputs'][36:]:
        row['input'] = vec(-1, 0)
    case('INVALID-INITIAL', obstacles=[box(-10, -10, 10, 10)], frames=1,
         error='INVALID_INITIAL_OCCUPANCY')
    case('DT-CLAMP', frames=1, dt=.051, goal={'center_x': [6.999, 7.001]})
    case('TIME-CLIP', frames=1, dt=.05, elapsed=29.98, goal={'center_x': [2.79, 2.81]})
    case('KEYBOARD', frames=36, mode='DEVICE_SMOKE', goal={'center_x': [1, 300]})
    assert len({c['spec']['id'] for c in rows}) == len(rows)
    return rows


def point(p):
    if type(p) is not dict or not {'x', 'y'} <= p.keys():
        raise ValueError('INVALID_POINT')
    v = [p['x'], p['y']]
    if any(type(x) not in (float, int) or not math.isfinite(x) or abs(x) > 1024 for x in v):
        raise ValueError('NONFINITE_OBSERVATION')
    return v


def near(a, b, tolerance=3e-5):
    return abs(a-b) <= tolerance


def bind_plan(plan, catalog):
    table = {(row['count'], row['formation']): row['offsets'] for row in catalog}
    if len(table) != 12:
        raise ValueError('INCOMPLETE_FORMATION_CATALOG')
    for row in plan:
        s = row['spec']
        offsets = deepcopy(table[s['count'], s['formation']])
        if len(offsets) != s['count']:
            raise ValueError('INVALID_FORMATION_CATALOG')
        row['body_offsets'] = offsets
        if not s['actual']:
            c = point(s['center'])
            s['actual'] = [vec(c[0]+point(p)[0], c[1]+point(p)[1]) for p in offsets]
    return plan


def judge_case(request, raw, request_sha):
    """Use source polygons and observed endpoints; never trust game alpha/success."""
    spec = request['spec']
    if any(raw.get(k) != request[k] for k in ('run_id', 'nonce')) or raw.get('case_id') != spec['id'] or raw.get('request_sha256') != request_sha:
        raise ValueError('EVIDENCE_MISMATCH')
    if raw.get('scope') != 'MOVEMENT_FIXTURE_ONLY' or type(raw.get('instance_id')) is not int or raw['instance_id'] == 0:
        raise ValueError('INVALID_INSTANCE')
    observed = raw['spec']
    # The Unity initializer resolves an empty actual array from the real formation formula.
    for key in ('id', 'mode', 'count', 'formation', 'alive', 'bypass_step', 'expected_error'):
        if observed[key] != spec[key]:
            raise ValueError('SPEC_MISMATCH:' + key)
    for key in ('speed', 'epsilon', 'center_radius', 'elapsed'):
        if not near(observed[key], spec[key], 1e-6):
            raise ValueError('SPEC_MISMATCH:' + key)
    geom = Geometry.from_polygons([point(p) for p in spec['boundary']],
                                  [[point(p) for p in o['points']] for o in spec['obstacles']])
    checks, violations = 0, []

    def sweep(start, end, radius, surface):
        nonlocal checks
        result = assess_sweep(geom, point(start), point(end), radius, spec['epsilon'])
        checks += 1
        if result['status'] != 'PASS':
            violations.append(dict(subject=surface, **result))

    if spec['expected_error'] == 'INVALID_INITIAL_OCCUPANCY':
        sweep(spec['center'], spec['center'], spec['center_radius'], 'center')
        for i, p in enumerate(observed['actual']):
            if spec['alive'][i]:
                sweep(p, p, spec['radii'][i], f'actual:{i}')
        if raw.get('error') != 'INVALID_INITIAL_OCCUPANCY' or raw.get('complete') or raw['steps'] or not any(v['status'] == 'FAIL' for v in violations):
            raise ValueError('INITIAL_CONTROL_NOT_DETECTED')
        return {'status': 'PASS', 'scope': 'expected invalid initialization detected', 'reason': 'INVALID_INITIAL_OCCUPANCY', 'checks': checks}
    if raw.get('error') or not raw.get('complete') or len(raw['steps']) != len(spec['inputs']):
        raise ValueError('INCOMPLETE_RUN:' + str(raw.get('error')))
    previous, frame = None, -1
    initial = raw['steps'][0]['before']
    offsets = [point(p) for p in request['body_offsets']]
    if len(offsets) != spec['count'] or len(initial['actual']) != spec['count'] or len(initial['reserved']) != spec['count']:
        raise ValueError('INITIAL_BODY_COUNT')
    if not all(near(a, b) for a, b in zip(point(initial['center']), point(spec['center']))) or not near(initial['elapsed'], spec['elapsed']):
        raise ValueError('INITIAL_STATE_MISMATCH')
    expected_actual = spec['actual'] or [vec(point(spec['center'])[0]+p[0], point(spec['center'])[1]+p[1]) for p in offsets]
    if len(observed['actual']) != spec['count'] or any(not all(near(a, b) for a, b in zip(point(p), point(q)))
           for collection in (initial['actual'], observed['actual']) for p, q in zip(collection, expected_actual)):
        raise ValueError('INITIAL_ACTUAL_MISMATCH')
    for n, step in enumerate(raw['steps']):
        if step['step_id'] != n or step['frame_id'] <= frame or step['instance_id'] != raw['instance_id'] or step['simulation_calls'] != 1 or step['combat_calls'] != 0:
            raise ValueError('OBSERVATION_GAP')
        frame = step['frame_id']
        before, after = step['before'], step['after']
        if previous is not None and previous != before:
            raise ValueError('STATE_CONTAMINATION')
        previous = after
        for state in (before, after):
            if len(state['actual']) != spec['count'] or len(state['reserved']) != spec['count']:
                raise ValueError('BODY_COUNT_MISMATCH')
            c = point(state['center'])
            for i, p in enumerate(state['reserved']):
                if not all(near(a, b) for a, b in zip(point(p), [c[j]+offsets[i][j] for j in (0, 1)])):
                    raise ValueError('RESERVATION_MISMATCH')
        if point(step['input']) != point(spec['inputs'][n]['input']):
            raise ValueError('INPUT_MISMATCH')
        supplied = step['supplied_frame_dt']
        if not math.isfinite(supplied) or supplied < 0 or not math.isfinite(step['actual_frame_dt']) or step['actual_frame_dt'] < 0:
            raise ValueError('INVALID_FRAME_TIME')
        if spec['mode'] == 'FIXED_REPLAY' and not near(supplied, spec['inputs'][n]['frame_dt'], 1e-6):
            raise ValueError('FIXED_DT_MISMATCH')
        requested = min(supplied, .05)*spec['speed']
        applied = min(requested, max(0, 30-before['elapsed']))
        if not near(step['requested_dt'], requested) or not near(step['applied_dt'], applied) or not near(after['elapsed'], before['elapsed']+applied) or not near(after['total'], before['total']+applied):
            raise ValueError('TIME_MISMATCH')
        if step['bypass'] != (n == spec['bypass_step']):
            raise ValueError('UNDECLARED_MUTATION')
        delta = [b-a for a, b in zip(point(before['center']), point(after['center']))]
        full = [v*140*applied for v in point(step['input'])]
        if step['held']:
            if any(not near(d, 0) for d in delta) or before['actual'] != after['actual']:
                raise ValueError('HELD_STATE_MOVED')
        else:
            length = sum(v*v for v in full)
            fraction = sum(a*b for a, b in zip(delta, full))/length if length else 0
            if not -.0001 <= fraction <= 1.0001 or abs(fraction*256-round(fraction*256)) > .005 or any(not near(d, v*fraction) for d, v in zip(delta, full)):
                raise ValueError('CENTER_MOTION_MISMATCH')
            k = min(1, 12*applied)
            for i in range(spec['count']):
                if spec['alive'][i]:
                    a, target = point(before['actual'][i]), point(after['reserved'][i])
                    if any(not near(b, x+(y-x)*k) for b, x, y in zip(point(after['actual'][i]), a, target)):
                        raise ValueError('FOLLOW_MISMATCH')
        sweep(before['center'], after['center'], spec['center_radius'], 'center')
        for i in range(spec['count']):
            sweep(before['reserved'][i], after['reserved'][i], spec['radii'][i], f'reserved:{i}')
            if spec['alive'][i]:
                sweep(before['actual'][i], after['actual'][i], spec['radii'][i], f'actual:{i}')
            elif before['actual'][i] != after['actual'][i]:
                raise ValueError('CORPSE_MOVED')
    if spec['mode'] == 'DEVICE_SMOKE' and raw.get('device_id', 0) <= 0:
        raise ValueError('MISSING_DEVICE')
    if spec['expected_error']:
        failed = [v for v in violations if v['status'] == 'FAIL' and 'SWEPT_PENETRATION' in v['reason_codes']]
        if spec['expected_error'] != 'SWEPT_PENETRATION' or not failed or any(v['status'] == 'BLOCKED' for v in violations):
            raise ValueError('EXPECTED_PENETRATION_NOT_DETECTED')
        subject = request['goal'].get('failure_surface')
        if subject and not any(v['subject'] == subject for v in failed):
            raise ValueError('EXPECTED_SUBJECT_NOT_DETECTED')
        return {'status': 'PASS', 'scope': 'declared unsafe mutation detected', 'reason': 'SWEPT_PENETRATION', 'checks': checks, 'violations': violations}
    if violations:
        raise ValueError('UNSAFE_OBSERVATION:' + json.dumps(violations[0]))
    end = raw['steps'][-1]['after']
    for field, limits in request['goal'].items():
        state = end
        metric = field
        if field.startswith('step'):
            index, metric = field[4:].split('_', 1)
            state = raw['steps'][int(index)]['after']
        if metric.startswith('actual'):
            value = point(state['actual'][int(metric[6])])[0 if metric.endswith('_x') else 1]
        else:
            value = point(state['center'])[0 if metric == 'center_x' else 1]
        if not limits[0] <= value <= limits[1]:
            raise ValueError('PROGRESS_MISMATCH:' + field + ':' + str(value))
    return {'status': 'PASS', 'scope': 'movement fixture safety and progress', 'checks': checks, 'frames': len(raw['steps']), 'last': end}


def tree_hashes(base):
    return {str(p.relative_to(base)): digest(p) for p in sorted(base.rglob('*'))
            if p.is_file() and p.name != '.DS_Store'}


def source_hashes():
    return {str(p.relative_to(ROOT)): digest(p) for directory in ('UnityMigration/Runtime', 'UnityMigration/Editor', 'Tools/Verify')
            for p in sorted((ROOT/directory).glob('*')) if p.suffix in ('.py', '.cs')}


def finish_owned_files(project, captures, target_before, sources_unchanged, pending, delete_asset):
    """No pending removal until every owned resource and dependency is verified."""
    for item in captures:
        native = project/item['path']
        if not native.resolve().is_relative_to((project/'Assets/MigrationVerification').resolve()) or native.is_symlink():
            raise RuntimeError('CAPTURE_PATH_CONFLICT')
        meta = Path(str(native)+'.meta')
        if item['phase'] == 'PLANNED':
            if native.exists() or meta.exists():
                raise RuntimeError('CAPTURE_UNCONFIRMED: preserve intent, files and pending marker')
            continue
        if str(native.relative_to(project/'Assets')) in target_before['Assets'] or digest(native) != item['sha256'] or digest(meta) != item['meta_sha256']:
            raise RuntimeError('CAPTURE_OWNERSHIP_CONFLICT')
        if delete_asset(item['path']) is not True or native.exists() or meta.exists():
            raise RuntimeError('CAPTURE_DELETE_FAILED')
    after = {name: tree_hashes(project/name) for name in target_before}
    if target_before != after:
        raise RuntimeError('TARGET_FILES_CHANGED')
    if not sources_unchanged():
        raise RuntimeError('SOURCE_CHANGED_DURING_RUN')
    if pending is not None:
        pending.unlink()
    return after


def cleanup_owned(method, ready, run_id, nonce, snapshot_path, snapshot, editor_pid):
    # StartOwned can have entered domain reload before its response is received.
    ready()
    owner = method('Ownership')
    if not owner['owner']:
        if owner['playing'] or method('Snapshot') != snapshot:
            raise RuntimeError('UNOWNED_EDITOR_CHANGED')
        return 'NOT_RUN'
    if owner['owner'] != run_id+':'+nonce or owner['pid'] != editor_pid or owner.get('marker') != 'DSMapMotionOwner_'+run_id+'_'+nonce:
        raise RuntimeError('OWNERSHIP_CONFLICT')
    if owner['phase'] in ('ENTERING', 'EXITING'):
        ready()
        owner = method('Ownership')
    if owner['phase'] == 'PLAY' and owner['playing']:
        method('StopOwned', run_id, nonce)
        ready()
        owner = method('Ownership')
    if owner['phase'] not in ('PREPARED', 'STOPPED') or owner['playing']:
        raise RuntimeError('UNSETTLED_OWNED_PHASE:' + owner['phase'])
    method('Restore', run_id, nonce, snapshot_path)
    restored, final = method('Snapshot'), method('Ownership')
    if restored != snapshot or final['owner'] or final['playing'] or final['devices']:
        raise RuntimeError('RESTORE_MISMATCH')
    return 'PASS'


def run(cleanup_probe=None):
    BASE.mkdir(parents=True, exist_ok=True)
    with editor_lock():
        pending = BASE/'unity-pending.json'
        if pending.exists():
            raise RuntimeError('UNFINISHED_UNITY_RUN: preserve marker; inspect ownership and restore first')
        run_id, nonce = uuid.uuid4().hex, uuid.uuid4().hex
        out = BASE/run_id
        out.mkdir()
        started = datetime.now(ZoneInfo('Asia/Seoul')).isoformat()
        plan = fixtures()
        sources = source_hashes()
        manifest = dict(scope='MOVEMENT_FIXTURE_ONLY', run_id=run_id, nonce=nonce, time=started,
                        expected_case_ids=[c['spec']['id'] for c in plan], cases=plan, sources=sources,
                        full_map_v1='NOT_RUN', g2_acceptance='NOT_RUN')
        report = dict(status='BLOCKED', scope='MOVEMENT_FIXTURE_ONLY', run_id=run_id, time=started,
                      results={}, full_map_v1='NOT_RUN', g2_acceptance='NOT_RUN', physical_device='NOT_RUN')
        env = os.environ.copy()
        env.update(UNITY_NO_CLI_INVOKED_TELEMETRY='1', UNITY_NO_CRASH_REPORT='1', UNITY_NO_CONSENT_PROMPT='1')
        sequence = 0
        owned_captures = []

        def cli(args, timeout=90):
            nonlocal sequence
            sequence += 1
            fence = args.index('--') if '--' in args else len(args)
            command = ['unity', *args[:fence], '--project-path', str(PROJECT), '--format', 'json', *args[fence:]]
            p = subprocess.run(command,
                               capture_output=True, text=True, env=env, timeout=timeout)
            write(out/f'{sequence:03d}-command.json', dict(args=args, exit_code=p.returncode, stdout=p.stdout, stderr=p.stderr))
            if not p.stdout.strip():
                raise RuntimeError('UNITY_EMPTY_RESPONSE: ' + p.stderr)
            blob = json.loads(p.stdout)
            if p.returncode or blob.get('success') is not True:
                raise RuntimeError('UNITY_COMMAND_FAILED:' + str(blob.get('errors')))
            return blob

        def evaluate(code):
            blob = cli(['command', 'script', 'eval', '--caller', 'plugin', '--skill', 'ds-workflow', '--', '--code', code])
            value = blob['data']['result']
            if not value.get('success'):
                raise RuntimeError('UNITY_SCRIPT_FAILED:' + str(value))
            return value['result']

        def method(name, *args):
            return evaluate('return DeadSurvivor.Editor.DSMapMotionVerification.'+name+'('+','.join(json.dumps(a) for a in args)+');')

        def capture(label, case, raw):
            relative = f'Assets/MigrationVerification/Map017-{run_id}-{label}.png'
            expected = PROJECT/relative
            if not expected.parent.is_dir() or expected.exists() or expected.with_suffix('.png.meta').exists():
                raise RuntimeError('CAPTURE_DESTINATION_NOT_FRESH')
            intent = dict(path=relative, phase='PLANNED', run_id=run_id, nonce=nonce, absent_before=True)
            owned_captures.append(intent)
            write(out/'owned-captures.json', owned_captures)
            cap = cli(['command', 'capture', 'game_view', '--caller', 'plugin', '--skill', 'ds-workflow', '--', '--source', 'screen',
                       '--save_path', f'MigrationVerification/Map017-{run_id}-{label}.png'])['data']['result']
            saved = (PROJECT/cap['savedPath']).resolve()
            if saved != expected.resolve():
                raise RuntimeError('INVALID_CAPTURE_PATH')
            shutil.copy2(saved, out/(label+'.png'))
            evaluate('UnityEditor.AssetDatabase.ImportAsset('+json.dumps(relative)+',UnityEditor.ImportAssetOptions.ForceSynchronousImport); return true;')
            intent.update(phase='CONFIRMED', sha256=digest(saved), meta_sha256=digest(str(saved)+'.meta'))
            write(out/'owned-captures.json', owned_captures)
            report.setdefault('captures', []).append(dict(run_id=run_id, nonce=nonce, case_id=case, image=label+'.png',
                last_observed_step=raw['steps'][-1]['step_id'], last_observed_frame=raw['steps'][-1]['frame_id'],
                capture_followup_frame=evaluate('return UnityEngine.Time.frameCount;'), instance_id=raw['instance_id']))

        snapshot, target_before, editor_pid = None, None, None
        try:
            cli(['status', '--until-ready'])
            for directory in ('Runtime', 'Editor'):
                local = ROOT/'UnityMigration'/directory
                target = PROJECT/'Assets/DeadSurvivor'/directory
                if tree_hashes(local) != {p: h for p, h in tree_hashes(target).items() if p.endswith('.cs')}:
                    # Source directories contain only .cs and local generated caches are excluded.
                    a = {p: h for p, h in tree_hashes(local).items() if p.endswith('.cs')}
                    b = {p: h for p, h in tree_hashes(target).items() if p.endswith('.cs')}
                    if a != b:
                        raise RuntimeError('SOURCE_TARGET_MISMATCH:' + directory)
            if digest(ROOT/'UnityMigration/Data/PrototypeData.json') != digest(PROJECT/'Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json'):
                raise RuntimeError('CONTENT_MISMATCH')
            if evaluate('return UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;'):
                raise RuntimeError('USER_PLAY_ACTIVE')
            cli(['recompile'], timeout=120)
            cli(['status', '--until-ready'])
            snapshot = method('Snapshot')
            editor_pid = method('Ownership')['pid']
            bind_plan(plan, json.loads(method('FormationCatalog'))['entries'])
            write(out/'manifest.json', manifest)
            write(out/'snapshot.json', snapshot)
            target_before = {name: tree_hashes(PROJECT/name) for name in ('Assets', 'Packages', 'ProjectSettings')}
            write(out/'target-before.json', target_before)
            write(pending, dict(run_id=run_id, nonce=nonce, snapshot=str(out/'snapshot.json'), pid=os.getpid()))
            method('Prepare', run_id, nonce)
            if cleanup_probe == 'prepare':
                raise TimeoutError('INJECTED_PREPARE_RESPONSE_LOSS')
            marker_tests = method('MarkerGuardTests', run_id, nonce)
            if marker_tests.get('status') != 'PASS':
                raise RuntimeError('MARKER_GUARDS_FAILED')
            report['results']['marker_guards'] = marker_tests
            # Wrong-owner cleanup must reject without mutating this owned stopped session.
            denied = evaluate('try { DeadSurvivor.Editor.DSMapMotionVerification.Restore("wrong","wrong",'+json.dumps(str(out/'snapshot.json'))+'); return false; } catch(System.Exception e) { return e.Message=="OWNERSHIP_CONFLICT"; }')
            if denied is not True:
                raise RuntimeError('WRONG_OWNER_NOT_REJECTED')
            report['results']['wrong_owner_rejected'] = {'status': 'PASS'}
            method('StartOwned', run_id, nonce)
            if cleanup_probe == 'start':
                raise TimeoutError('INJECTED_START_RESPONSE_LOSS')
            cli(['status', '--until-ready'])
            first = evaluate('return UnityEngine.Time.frameCount;')
            units = method('UnitTests')
            write(out/'unity-unit-tests.json', units)
            if units.get('status') != 'PASS':
                raise RuntimeError('UNIT_FAILED')
            report['results']['unity_units'] = {'status': 'PASS', 'count': units['count']}
            cases, raw_cases = {}, {}
            for row in plan:
                name = row['spec']['id']
                request = dict(run_id=run_id, nonce=nonce, **row)
                request_path, result_path = out/(name+'.request.json'), out/(name+'.raw.json')
                write(request_path, request)
                method('BeginCase', str(request_path), str(result_path))
                deadline = time.monotonic()+30
                while not result_path.exists() and time.monotonic() < deadline:
                    time.sleep(.15)
                if not result_path.exists():
                    raise RuntimeError('CASE_TIMEOUT:' + name)
                raw = load_json(result_path)
                raw_cases[name] = raw
                cases[name] = judge_case(request, raw, digest(request_path))
                write(out/'judgments.json', cases)
                if name == 'WALL-PARTY':
                    capture('wall-party', name, raw)
            for n in range(1, 5):
                for f in range(3):
                    a, b = [raw_cases[f'OPEN-N{n}-F{f}-R{r}']['steps'] for r in (0, 1)]
                    if [(x['before'], x['after'], x['input'], x['applied_dt']) for x in a] != [(x['before'], x['after'], x['input'], x['applied_dt']) for x in b]:
                        raise RuntimeError('REPLAY_MISMATCH')
            after = evaluate('return UnityEngine.Time.frameCount;')
            if after <= first:
                raise RuntimeError('FROZEN_EDITOR')
            report['results']['fixtures'] = {'status': 'PASS', 'count': len(cases), 'replay_pairs': 12,
                'oracle_sweeps': sum(c['checks'] for c in cases.values()), 'frame_before': first, 'frame_after': after}
            capture('movement', 'KEYBOARD', raw_cases['KEYBOARD'])
            console = cli(['command', 'console', 'get', '--caller', 'plugin', '--skill', 'ds-workflow', '--', '--tail', '100', '--level', 'error'])['data']['result']
            if console.get('entries') or console.get('groundTruth', {}).get('compilationFailed') or console.get('groundTruth', {}).get('consoleErrors', 0):
                raise RuntimeError('CONSOLE_ERRORS')
            report['results']['console'] = {'status': 'PASS'}
            report['status'] = 'PASS'
        except (Exception, KeyboardInterrupt) as error:
            expected_loss = cleanup_probe and isinstance(error, TimeoutError) and str(error).startswith('INJECTED_')
            report['status'] = 'PASS' if expected_loss else 'FAIL' if isinstance(error, (ValueError, AssertionError)) else 'BLOCKED'
            if expected_loss:
                report['scope'] = 'MOVEMENT_LIFECYCLE_PROBE'
                report['results']['injected_response_loss'] = {'status': 'PASS', 'stage': cleanup_probe}
            report['error'] = str(error) or 'CANCELLED'
        finally:
            try:
                cleanup_status = 'NOT_RUN'
                if pending.exists():
                    marker = load_json(pending)
                    if marker['run_id'] != run_id or marker['nonce'] != nonce:
                        raise RuntimeError('PENDING_MARKER_CHANGED')
                    cleanup_status = cleanup_owned(method, lambda: cli(['status', '--until-ready']), run_id, nonce, str(out/'snapshot.json'), snapshot, editor_pid)
                    write(out/'restored.json', dict(snapshot=method('Snapshot'), ownership=method('Ownership')))
                if target_before is not None:
                    target_after = finish_owned_files(PROJECT, owned_captures, target_before,
                        lambda: sources == source_hashes(), pending if pending.exists() else None,
                        lambda rel: evaluate('return UnityEditor.AssetDatabase.DeleteAsset('+json.dumps(rel)+');'))
                    write(out/'target-after.json', target_after)
                if sources != source_hashes():
                    raise RuntimeError('SOURCE_CHANGED_DURING_RUN')
                report['results']['cleanup'] = {'status': cleanup_status, 'scope': 'owned parent-finally cleanup only; external watchdog NOT_RUN'}
            except (Exception, KeyboardInterrupt) as error:
                report['results']['cleanup'] = {'status': 'BLOCKED', 'error': str(error)}
                if report['status'] != 'FAIL':
                    report['status'] = 'BLOCKED'
            report['finished_at'] = datetime.now(ZoneInfo('Asia/Seoul')).isoformat()
            report['artifacts'] = {str(p.relative_to(out)): digest(p) for p in out.rglob('*') if p.is_file() and p.name != 'summary.json'}
            write(out/'summary.json', report)
        print(json.dumps({'status': report['status'], 'scope': report['scope'], 'report': str(out/'summary.json'),
                          'error': report.get('error'), 'g2_acceptance': 'NOT_RUN'}, ensure_ascii=False))
        return 0 if report['status'] == 'PASS' else 1 if report['status'] == 'FAIL' else 2


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cleanup-probe', choices=('prepare', 'start'))
    args = parser.parse_args()
    try:
        raise SystemExit(run(args.cleanup_probe))
    except RuntimeError as error:
        print(json.dumps({'status': 'BLOCKED', 'error': str(error)}, ensure_ascii=False))
        raise SystemExit(2)

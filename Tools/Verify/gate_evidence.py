"""Gate evidence judgment, separate from unchanged Unity collection helpers."""
from pathlib import Path
from gate_support import digest, PERSONAL_PROJECT

# Only files that produce Unity execution/collection semantics. Other diagnostic
# scripts and the current judge are not dependencies of an old collected run.
UNITY_PRODUCERS = ('Tools/Verify/run.py', 'Tools/Verify/unity_check.py',
                   'Tools/Verify/gate_support.py', 'Tools/project_paths.py')


def same_producers(a, b):
    try:
        av, bv = a['verifiers'], b['verifiers']
        return all(isinstance(av[k], str) and len(av[k]) == 64 and av[k] == bv[k]
                   for k in UNITY_PRODUCERS)
    except (KeyError, TypeError):
        return False


def same_execution_context(a, b):
    return (isinstance(a, dict) and isinstance(b, dict)
            and a.get('project') == b.get('project')
            and isinstance(a.get('runtime'), dict) and a.get('runtime') == b.get('runtime')
            and same_producers(a, b))

def validate_unity_report(report, folder, context, require_screens=True):
    """Reject incomplete, stale, or unverifiable evidence; never invoke the Editor."""
    if report.get('status') != 'PASS' or report.get('stage') != 'all':
        return False, 'all stage PASS required'
    if require_screens and report.get('options', {}).get('screens') is not True:
        return False, 'screens option required'
    if report.get('options', {}).get('project') != str(PERSONAL_PROJECT):
        return False, 'wrong target project'
    if (report.get('gate_before') != report.get('gate_after')
            or not same_execution_context(report.get('gate_before'), context)):
        return False, 'source, verifier or target fingerprint mismatch'
    results = report.get('results', {})
    if any(results.get(k, {}).get('status') != 'PASS' for k in ('docs', 'data', 'source_stability')):
        return False, 'local prerequisites incomplete'
    unity = results.get('unity', {})
    steps = unity.get('results', {})
    required = ('compile', 'rules', 'frames', 'interaction', 'capture', 'console', 'cleanup')
    screens_requested = require_screens or report.get('options', {}).get('screens') is True
    if screens_requested:
        required += ('screens',)
    if unity.get('status') != 'PASS' or any(steps.get(k, {}).get('status') != 'PASS' for k in required):
        return False, 'required Unity results missing or failed'
    if screens_requested:
        screens = steps['screens'].get('checks')
        if not isinstance(screens, list) or not screens or not all(isinstance(x, str) and x.startswith('PASS') for x in screens):
            return False, 'empty or invalid screen evidence'
    frames = steps['frames']
    if not isinstance(frames.get('before'), int) or not isinstance(frames.get('after'), int) or frames['after'] <= frames['before']:
        return False, 'frames did not advance'
    hashes = report.get('unity_artifact_sha256')
    if not isinstance(hashes, dict) or not hashes:
        return False, 'artifact hashes absent'
    if not {'compile.json', 'rules.txt'}.issubset(hashes) or not any(n.endswith('.png') for n in hashes):
        return False, 'compile/rules/capture originals missing'
    if screens_requested and not any(n.startswith('interaction-') and n.endswith('.txt') for n in hashes):
        return False, 'screen interaction originals missing'
    sizes = ('1280x720', '1560x720', '1024x768')
    states = ('prep', 'description', 'comparison', 'safearea', 'settings', 'result', 'battle-items', 'battle-full')
    expected = {'inventory.png', 'revive.png', 'comparison.png', 'interaction.txt'}
    if screens_requested:
        expected.update('interaction-' + size + '.txt' for size in sizes)
        expected.update(state + '-' + size + '.png' for state in states for size in sizes)
    if not expected.issubset(hashes):
        return False, 'screen/capture matrix incomplete'
    for rel, value in hashes.items():
        p = folder / 'unity' / rel
        if (Path(rel).is_absolute() or '..' in Path(rel).parts
                or not p.resolve().is_relative_to((folder / 'unity').resolve())
                or any(x.is_symlink() for x in (p, *p.parents))):
            return False, 'artifact path outside run'
        if not p.is_file() or p.stat().st_size == 0 or digest(p) != value:
            return False, 'missing or changed artifact: ' + rel
    return True, 'current all --screens evidence' if screens_requested else 'current all basic evidence'

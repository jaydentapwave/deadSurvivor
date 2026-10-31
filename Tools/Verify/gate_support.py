"""Fingerprint/evidence helpers. No Unity commands, network, or external writes."""
import hashlib
import json
from pathlib import Path

PERSONAL_PROJECT = Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def tree_hashes(folder):
    if not folder.is_dir():
        return {'__MISSING__': True}
    result = {}
    for p in sorted(folder.rglob('*')):
        if '__pycache__' in p.parts or p.name == '.DS_Store' or p.suffix == '.pyc':
            continue
        if p.is_symlink():
            raise ValueError('Symlink cannot be fingerprinted as regular source: ' + str(p))
        if p.is_file():
            result[str(p.relative_to(folder))] = digest(p)
    return result


def runtime_state(root, project=PERSONAL_PROJECT):
    if project.resolve() != PERSONAL_PROJECT:
        raise ValueError('Only the approved personal Unity project is supported')
    return {'source': tree_hashes(root / 'UnityMigration'),
            'art': tree_hashes(root / 'UnityArtExport'),
            'target': {n: tree_hashes(project / n) for n in ('Assets/DeadSurvivor', 'ProjectSettings', 'Packages')}}


def verifier_hashes(root):
    return {str(p.relative_to(root)): digest(p) for p in sorted((root / 'Tools/Verify').rglob('*.py'))}


def evidence_context(root, project=PERSONAL_PROJECT):
    return {'project': str(project.resolve()), 'runtime': runtime_state(root, project), 'verifiers': verifier_hashes(root)}


def artifact_hashes(folder):
    return {str(p.relative_to(folder)): digest(p) for p in sorted(folder.rglob('*'))
            if p.is_file() and not p.is_symlink()}


def validate_unity_report(report, folder, context):
    """Reject incomplete, stale, or unverifiable evidence; never invoke the Editor."""
    if report.get('status') != 'PASS' or report.get('stage') != 'all':
        return False, 'all stage PASS required'
    if report.get('options', {}).get('screens') is not True:
        return False, 'screens option required'
    if report.get('options', {}).get('project') != str(PERSONAL_PROJECT):
        return False, 'wrong target project'
    if report.get('gate_before') != context or report.get('gate_after') != context:
        return False, 'source, verifier or target fingerprint mismatch'
    results = report.get('results', {})
    if any(results.get(k, {}).get('status') != 'PASS' for k in ('docs', 'data', 'source_stability')):
        return False, 'local prerequisites incomplete'
    unity = results.get('unity', {})
    steps = unity.get('results', {})
    required = ('compile', 'rules', 'frames', 'interaction', 'capture', 'screens', 'console', 'cleanup')
    if unity.get('status') != 'PASS' or any(steps.get(k, {}).get('status') != 'PASS' for k in required):
        return False, 'required Unity results missing or failed'
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
    if not any(n.startswith('interaction-') and n.endswith('.txt') for n in hashes):
        return False, 'screen interaction originals missing'
    sizes = ('1280x720', '1560x720', '1024x768')
    states = ('prep', 'description', 'comparison', 'safearea', 'settings', 'result', 'battle-items', 'battle-full')
    expected = {'inventory.png', 'revive.png', 'comparison.png', 'interaction.txt'}
    expected.update('interaction-' + size + '.txt' for size in sizes)
    expected.update(state + '-' + size + '.png' for state in states for size in sizes)
    if not expected.issubset(hashes):
        return False, 'screen/capture matrix incomplete'
    for rel, value in hashes.items():
        p = folder / 'unity' / rel
        if p.is_symlink() or not p.resolve().is_relative_to((folder / 'unity').resolve()):
            return False, 'artifact path outside run'
        if not p.is_file() or p.stat().st_size == 0 or digest(p) != value:
            return False, 'missing or changed artifact: ' + rel
    return True, 'current all --screens evidence'

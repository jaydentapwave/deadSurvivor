"""Small, explicit gate dependencies and checked static-result reuse."""
import datetime
import json
from pathlib import Path
import re
import sys
from gate_support import digest

SCHEMA = 2
COMMON = ('Tools/Verify/gate.py', 'Tools/Verify/gate_scope.py',
          'Tools/Verify/gate_support.py', 'Tools/Verify/gate_evidence.py', 'Tools/Verify/run.py',
          'Tools/project_paths.py', 'Tools/Verify/gate-policy.json')
CORE_LOGIC = frozenset('target/Assets/DeadSurvivor/Runtime/' + n + '.cs' for n in (
    'DSGame.Combat', 'DSGame.EquipmentCombat', 'DSGame.Stats', 'EquipmentRules',
    'DSMapGeometry', 'DSMapMotion', 'DSMapNavigation'))


def file_state(root, rel):
    p = root / rel
    if Path(rel).is_absolute() or '..' in Path(rel).parts or not p.resolve().is_relative_to(root.resolve()):
        raise ValueError('Dependency outside project: ' + rel)
    for ancestor in (p, *p.parents):
        if ancestor == root:
            break
        if ancestor.is_symlink():
            raise ValueError('Symlink dependency: ' + rel)
    if not p.is_file():
        return {'missing': True}
    return {'sha256': digest(p), 'mode': '100755' if p.stat().st_mode & 0o100 else '100644'}


def dependencies(root, source, policy):
    common = {p: file_state(root, p) for p in COMMON}
    common['@python'] = {'version': sys.version, 'executable': sys.executable}
    docs = {'README.md', 'AGENTS.md', 'CLAUDE.md', '할일.md'}
    for directory in ('docs', '.agents/skills', '.claude/agents'):
        docs.update(str(p.relative_to(root)) for p in (root / directory).rglob('*.md'))
    doc_inputs = {p: file_state(root, p) for p in sorted(docs)}
    targets = {}
    for name in sorted(docs):
        p = root / name
        if not p.is_file():
            continue
        for raw in re.findall(r'\]\(([^)]+)\)', p.read_text()):
            rel = raw.split('#')[0].strip('<>')
            if not rel or '://' in rel or rel.startswith('mailto:'):
                continue
            target = (p.parent / rel).resolve()
            key = str(target)
            targets[key] = (target.exists() if target.is_relative_to(root.resolve()) else 'EXTERNAL')
    # Existence of ignored output links is an input to the document checker too.
    doc_inputs['@link_targets'] = targets
    data = {p: file_state(root, p) for p in (
        'Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json',)}
    oracle = {p: file_state(root, p) for p in (
        'Tools/Verify/map_check.py', 'Tools/Verify/map_io.py', 'Tools/Verify/map_oracle.py',
        'docs/design/map-harness-fixtures.json')}
    spec = policy['active_map']
    map_paths = {spec[k] for k in ('layout', 'reference', 'cases', 'audit', 'render_manifest')}
    map_paths.update(spec['sheets'])
    map_paths.update(str(Path(p).with_suffix('.png')) for p in spec['sheets'])
    folder = str(Path(spec['audit']).parent) + '/'
    map_paths.update(p for p in source if p.startswith(folder))
    # render_sheets.py is called by the audit and reads the 005 register.
    map_paths.add('docs/design/map-meadow-005-r001-objects.csv')
    map_paths.add('docs/art/maps/map-meadow-007-r001/render_sheets.py')
    mapped = {p: file_state(root, p) for p in sorted(map_paths)}
    return {name: {'common': common, 'inputs': inputs} for name, inputs in (
        ('docs', doc_inputs), ('data', data), ('map_oracle_foundation', oracle),
        ('map_preparation', mapped))}


def output_path(root, rel):
    p = root / rel
    base = root / 'output/verification'
    if Path(rel).is_absolute() or '..' in Path(rel).parts or not p.resolve().is_relative_to(base.resolve()):
        raise ValueError('Evidence outside verification output')
    for ancestor in (p, *p.parents):
        if ancestor == root:
            break
        if ancestor.is_symlink():
            raise ValueError('Symlink evidence')
    return p


def history(root):
    rows = []
    now = datetime.datetime.now(datetime.timezone.utc)
    for p in (root / 'output/verification').glob('gate-*/gate.json'):
        try:
            checked = output_path(root, str(p.relative_to(root)))
            value = json.loads(checked.read_text())
            stamp = datetime.datetime.fromisoformat(value['time'])
            if value.get('schema') != SCHEMA or stamp.tzinfo is None or stamp > now + datetime.timedelta(seconds=5):
                continue
            rows.append((stamp, value))
        except (ValueError, OSError, TypeError, KeyError):
            continue
    return [row for _, row in sorted(rows, key=lambda pair: pair[0], reverse=True)]


def valid_result(name, value):
    if not isinstance(value, dict) or value.get('status') != 'PASS':
        return False
    if name == 'docs':
        return type(value.get('files')) is int and value['files'] > 0 and value.get('errors') == []
    if name == 'data':
        return value.get('errors') == []
    if name == 'map_oracle_foundation':
        return (value.get('checks_run') == 8 and value.get('failed_checks') == []
                and value.get('errors') == [] and value.get('unity_status') == 'NOT_RUN')
    if name == 'map_preparation':
        return (value.get('audit', {}).get('status') == 'PASS'
                and value.get('map_dependencies', {}).get('status') == 'PASS')
    return False


def reusable(root, records, name, deps):
    for gate in records:
        record = gate.get('checks', {}).get(name, {})
        if record.get('dependencies') != deps:
            continue
        # A later failed check invalidates an earlier success for this same input.
        if record.get('status') != 'PASS':
            return None, 'latest matching check did not pass'
        if gate.get('status') != 'PASS' or gate.get('source_stable') is not True:
            continue
        try:
            p = output_path(root, record['report'])
            if digest(p) != record['sha256']:
                return None, 'cached evidence changed'
            raw = json.loads(p.read_text())
            if (raw.get('schema') != 1 or raw.get('name') != name or raw.get('dependencies') != deps
                    or not valid_result(name, raw.get('result'))):
                return None, 'cached evidence incomplete'
            evidence = raw['result'].get('audit')
            if evidence:
                audit_path = output_path(root, evidence['report'])
                if digest(audit_path) != evidence['sha256']:
                    return None, 'cached map audit changed'
                audit = json.loads(audit_path.read_text())
                checks = audit.get('checks', [])
                if audit.get('status') != 'PASS' or len(checks) < 151 or any(c.get('status') != 'PASS' for c in checks):
                    return None, 'cached map audit incomplete'
            return {**record, 'action': 'REUSE', 'reason': 'same dependencies; original evidence revalidated'}, None
        except (KeyError, ValueError, OSError, TypeError, AttributeError):
            return None, 'cached evidence missing or invalid'
    return None, 'no valid result for these dependencies'


def runtime_files(state):
    if not isinstance(state, dict) or set(state) != {'source', 'art', 'target'}:
        raise ValueError('Invalid runtime manifest')
    result = {}
    for section in ('source', 'art'):
        for name, value in state[section].items():
            result[section + '/' + name] = value
    for section, files in state['target'].items():
        for name, value in files.items():
            result['target/' + section + '/' + name] = value
    return result


def same_visual_inputs(old, current):
    try:
        a, b = runtime_files(old), runtime_files(current)
        # Added/deleted files, including allowlisted files, require full screens.
        return set(a) == set(b) and all(p in CORE_LOGIC for p in a if a[p] != b[p])
    except (TypeError, AttributeError, ValueError):
        return False

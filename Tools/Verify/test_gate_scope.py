"""Scope/cache and Unity-evidence boundary tests; isolated files, no Editor."""
import copy
import datetime
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import gate
import gate_scope as scope
import gate_support as support
import gate_evidence as evidence


class ScopeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.count = 0

    def put(self, rel, value):
        p = self.root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(json.dumps(value) if isinstance(value, (dict, list)) else value)
        return p

    def fixture(self):
        for p in scope.COMMON:
            self.put(p, 'checker')
        for p in ('README.md', 'AGENTS.md', 'CLAUDE.md', '할일.md'):
            self.put(p, 'document')
        self.put('Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json',
                 {'characters': [], 'items': [], 'monsters': [],
                  'settings': {'weights': [], 'waves': []}})
        for p in ('Tools/Verify/map_check.py', 'Tools/Verify/map_io.py',
                  'Tools/Verify/map_oracle.py', 'docs/design/map-harness-fixtures.json'):
            self.put(p, (gate.ROOT / p).read_text())
        self.policy = {'active_map': {'layout': 'docs/map/layout.json',
            'reference': 'docs/map/reference.png', 'cases': 'docs/map/cases.csv',
            'audit': 'docs/map/audit.py', 'render_manifest': 'docs/map/render.json',
            'sheets': ['docs/map/sheet.svg']}}
        for p in list(self.policy['active_map'].values())[:-1] + ['docs/map/sheet.svg', 'docs/map/sheet.png',
                'docs/design/map-meadow-005-r001-objects.csv',
                'docs/art/maps/map-meadow-007-r001/render_sheets.py']:
            self.put(p, 'input')

    def source(self):
        return {str(p.relative_to(self.root)): {} for p in self.root.rglob('*')
                if p.is_file() and 'output' not in p.relative_to(self.root).parts}

    def deps(self):
        return scope.dependencies(self.root, self.source(), self.policy)

    def fake_audit(self, *args):
        self.count += 1
        p = self.put('output/verification/audit-' + str(self.count) + '/summary.json',
                     {'status': 'PASS', 'checks': [{'status': 'PASS'}] * 151})
        return {'status': 'PASS', 'report': str(p.relative_to(self.root)), 'sha256': support.digest(p)}

    def static(self):
        self.count += 1
        out = self.root / ('output/verification/gate-' + str(self.count))
        out.mkdir(parents=True)
        with patch.object(gate, 'run_check', side_effect=self.fake_audit), \
                patch.object(gate, 'active_map_check', return_value={'status': 'PASS'}):
            checks = gate.static_requirements(self.root, self.policy, self.source(), out)
        result = {'schema': 2, 'time': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  'status': 'PASS' if all(c['status'] == 'PASS' for c in checks.values()) else 'FAIL',
                  'source_stable': True, 'checks': checks}
        (out / 'gate.json').write_text(json.dumps(result))
        return result

    def test_dependency_selection_and_missing_link_target(self):
        self.fixture()
        base = self.deps()
        cases = [('README.md', {'docs'}),
                 ('Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json', {'data'}),
                 ('Tools/Verify/map_oracle.py', {'map_oracle_foundation'}),
                 ('docs/design/map-harness-fixtures.json', {'map_oracle_foundation'}),
                 ('docs/map/sheet.png', {'map_preparation'}),
                 ('docs/map/layout.json', {'map_preparation'}),
                 ('docs/design/map-meadow-005-r001-objects.csv', {'map_preparation'}),
                 ('Tools/Verify/gate.py', set(base)),
                 ('Tools/Verify/unrelated_test.py', set()),
                 ('Assets/DeadSurvivor/Runtime/DSUI.cs', set())]
        for rel, expected in cases:
            with self.subTest(rel=rel):
                p = self.root / rel
                before = p.read_bytes() if p.exists() else None
                self.put(rel, 'changed')
                self.assertEqual({n for n in base if base[n] != self.deps()[n]}, expected)
                if before is None:
                    p.unlink()
                else:
                    p.write_bytes(before)
        p = self.root / 'docs/map/sheet.png'
        p.unlink()
        self.assertNotEqual(self.deps()['map_preparation'], base['map_preparation'])
        self.put('README.md', '[evidence](output/verification/external/original.json)')
        absent = self.deps()['docs']
        self.put('output/verification/external/original.json', '{}')
        self.assertNotEqual(absent, self.deps()['docs'])
        # documents() also reads Markdown ignored by Git; dependency collection must agree.
        self.put('docs/ignored.md', '[bad](missing)')
        self.assertIn('docs/ignored.md', scope.dependencies(self.root, {}, self.policy)['docs']['inputs'])

    def test_cold_warm_and_docs_only_run_with_real_static_checks(self):
        self.fixture()
        first = self.static()
        self.assertEqual(first['status'], 'PASS', first)
        self.assertTrue(all(c['action'] == 'RUN' for c in first['checks'].values()))
        warm = self.static()
        self.assertTrue(all(c['action'] == 'REUSE' for c in warm['checks'].values()), warm)
        self.put('README.md', 'edited document')
        changed = self.static()
        self.assertEqual([n for n, c in changed['checks'].items() if c['action'] == 'RUN'], ['docs'])
        self.assertEqual(changed['checks']['data']['report'], first['checks']['data']['report'])

    def test_missing_tampered_and_newer_failed_evidence_reruns(self):
        self.fixture()
        first = self.static()
        (self.root / first['checks']['data']['report']).write_text('{}')
        second = self.static()
        self.assertEqual([n for n,c in second['checks'].items() if c['action'] == 'RUN'], ['data'])
        (self.root / second['checks']['data']['report']).unlink()
        self.assertEqual(self.static()['checks']['data']['action'], 'RUN')
        current = self.static()
        failed = copy.deepcopy(current)
        failed['status'] = 'FAIL'
        failed['checks']['docs']['status'] = 'FAIL'
        self.assertIsNone(scope.reusable(self.root, [failed, current], 'docs', self.deps()['docs'])[0])
        unstable = copy.deepcopy(current)
        unstable['source_stable'] = False
        self.assertIsNone(scope.reusable(self.root, [unstable], 'data', self.deps()['data'])[0])

    def test_map_original_tamper_reruns_only_map(self):
        self.fixture()
        first = self.static()
        receipt = json.loads((self.root / first['checks']['map_preparation']['report']).read_text())
        (self.root / receipt['result']['audit']['report']).write_text('{}')
        result = self.static()
        self.assertEqual([n for n,c in result['checks'].items() if c['action'] == 'RUN'], ['map_preparation'])

    def test_evidence_and_dependency_symlinks_rejected(self):
        self.fixture()
        original = self.put('output/verification/original/file.json', '{}')
        link = self.root / 'output/verification/link'
        link.symlink_to(original.parent, target_is_directory=True)
        with self.assertRaises(ValueError):
            scope.output_path(self.root, 'output/verification/link/file.json')
        (self.root / 'README.md').unlink()
        (self.root / 'README.md').symlink_to(original)
        with self.assertRaises(ValueError):
            self.deps()

    def state(self):
        return {'source': {}, 'art': {}, 'target': {'Assets/DeadSurvivor': {
            'Runtime/DSGame.Combat.cs': 'a', 'Runtime/DSUI.cs': 'a'},
            'ProjectSettings': {}, 'Packages': {}}}

    def context(self, state=None):
        return {'project': str(support.PERSONAL_PROJECT), 'runtime': state or self.state(),
                'verifiers': {p: 'a' * 64 for p in evidence.UNITY_PRODUCERS}}

    def report(self, name, context, screens, age=10):
        files = ['compile.json', 'rules.txt', 'inventory.png', 'revive.png', 'comparison.png', 'interaction.txt']
        if screens:
            for size in ('1280x720', '1560x720', '1024x768'):
                files += ['interaction-' + size + '.txt'] + [s + '-' + size + '.png' for s in
                    ('prep', 'description', 'comparison', 'safearea', 'settings', 'result', 'battle-items', 'battle-full')]
        folder = self.root / ('output/verification/' + name)
        for f in files:
            self.put(str((folder / 'unity' / f).relative_to(self.root)), 'fixture original')
        steps = {k: {'status': 'PASS'} for k in
                 ('compile', 'rules', 'frames', 'interaction', 'capture', 'console', 'cleanup')}
        steps['frames'].update(before=1, after=2)
        if screens:
            steps['screens'] = {'status': 'PASS', 'checks': ['PASS fixture']}
        value = {'time': (datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(seconds=age)).isoformat(),
                 'stage': 'all', 'status': 'PASS',
                 'options': {'screens': screens, 'project': str(support.PERSONAL_PROJECT)},
                 'gate_before': context, 'gate_after': copy.deepcopy(context),
                 'results': {**{k: {'status': 'PASS'} for k in ('docs', 'data', 'source_stability')},
                             'unity': {'status': 'PASS', 'results': steps}},
                 'unity_artifact_sha256': support.artifact_hashes(folder / 'unity')}
        path = self.put(str((folder / 'summary.json').relative_to(self.root)), value)
        return path, value

    def test_basic_still_requires_frames_input_capture_cleanup_and_originals(self):
        ctx = self.context()
        path, value = self.report('basic', ctx, False)
        self.assertTrue(evidence.validate_unity_report(value, path.parent, ctx, require_screens=False)[0])
        self.assertFalse(evidence.validate_unity_report(value, path.parent, ctx)[0])
        for key in ('frames', 'interaction', 'capture', 'cleanup'):
            broken = copy.deepcopy(value)
            del broken['results']['unity']['results'][key]
            self.assertFalse(evidence.validate_unity_report(broken, path.parent, ctx, False)[0])
        (path.parent / 'unity/interaction.txt').write_text('tampered')
        self.assertFalse(evidence.validate_unity_report(value, path.parent, ctx, False)[0])

    def test_collector_projection_does_not_ignore_collector_or_self_stability(self):
        ctx = self.context()
        current = copy.deepcopy(ctx)
        current['verifiers']['Tools/Verify/unrelated_test.py'] = 'b' * 64
        self.assertTrue(evidence.same_execution_context(ctx, current))
        for key in evidence.UNITY_PRODUCERS:
            changed = copy.deepcopy(current)
            changed['verifiers'][key] = 'b' * 64
            self.assertFalse(evidence.same_execution_context(ctx, changed))
        path, value = self.report('basic', ctx, False)
        value['gate_after'] = current
        self.assertFalse(evidence.validate_unity_report(value, path.parent, current, False)[0])

    def test_visual_allowlist_content_only_and_unknown_fallback(self):
        old = self.state()
        new = copy.deepcopy(old)
        new['target']['Assets/DeadSurvivor']['Runtime/DSGame.Combat.cs'] = 'b'
        self.assertTrue(scope.same_visual_inputs(old, new))
        for rel in ('Runtime/DSUI.cs', 'Runtime/New.cs', 'Runtime/DSGame.Combat.cs.meta'):
            changed = copy.deepcopy(new)
            changed['target']['Assets/DeadSurvivor'][rel] = 'b'
            self.assertFalse(scope.same_visual_inputs(old, changed), rel)
        del new['target']['Assets/DeadSurvivor']['Runtime/DSGame.Combat.cs']
        self.assertFalse(scope.same_visual_inputs(old, new))

    def runtime(self, current):
        policy = {'runtime_baseline': {'source': {'old': 'x'}, 'art': {}, 'target': {}},
                  'installed_at': '2026-10-01T00:00:00+00:00'}
        with patch.object(gate, 'runtime_state', return_value=current['runtime']), \
                patch.object(gate, 'evidence_context', return_value=current):
            return gate.runtime_requirement(self.root, policy)

    def test_runtime_basic_needs_valid_full_screen_basis_and_current_fingerprint(self):
        old = self.context()
        basis, _ = self.report('old-screens', old, True, age=40)
        current = copy.deepcopy(old)
        current['runtime']['target']['Assets/DeadSurvivor']['Runtime/DSGame.Combat.cs'] = 'b'
        basic, _ = self.report('current-basic', current, False)
        result = self.runtime(current)
        self.assertEqual(result['required_profile'], 'basic')
        self.assertEqual(result['screen_basis']['report'], str(basis.relative_to(self.root)))
        changed = copy.deepcopy(current)
        changed['runtime']['target']['Assets/DeadSurvivor']['Runtime/DSUI.cs'] = 'b'
        self.report('current-ui-basic', changed, False, age=5)
        with self.assertRaises(ValueError):
            self.runtime(changed)
        (basis.parent / 'unity/prep-1280x720.png').unlink()
        with self.assertRaises(ValueError):
            self.runtime(current)

    def test_latest_current_or_visual_failure_cannot_hide_behind_old_pass(self):
        old = self.context()
        self.report('old-screens', old, True, age=50)
        current = copy.deepcopy(old)
        current['runtime']['target']['Assets/DeadSurvivor']['Runtime/DSGame.Combat.cs'] = 'b'
        self.report('current-basic', current, False, age=20)
        failed_path, failed = self.report('failed-current', current, False, age=5)
        failed['status'] = 'FAIL'
        failed_path.write_text(json.dumps(failed))
        with self.assertRaisesRegex(ValueError, 'latest current'):
            self.runtime(current)
        failed_path.unlink()
        failed_path, failed = self.report('failed-screens', old, True, age=30)
        failed['status'] = 'FAIL'
        failed_path.write_text(json.dumps(failed))
        with self.assertRaises(ValueError):
            self.runtime(current)

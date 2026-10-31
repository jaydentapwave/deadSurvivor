from copy import deepcopy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from map_motion_run import fixtures, judge_case, editor_lock, vec, cleanup_owned, finish_owned_files, tree_hashes, digest


class MotionRecordingTests(unittest.TestCase):
    def sample(self):
        row = deepcopy(next(c for c in fixtures() if c['spec']['id'] == 'DT-CLAMP'))
        spec = row['spec']
        request = dict(run_id='a'*32, nonce='b'*32, body_offsets=[vec(0, 0)], **row)
        observed = deepcopy(spec)
        observed['actual'] = [vec(0, 0)]
        before = dict(center=vec(0, 0), actual=[vec(0, 0)], reserved=[vec(0, 0)], elapsed=0, total=0)
        after = dict(center=vec(7, 0), actual=[vec(4.2, 0)], reserved=[vec(7, 0)], elapsed=.05, total=.05)
        step = dict(step_id=0, frame_id=17, instance_id=23, simulation_calls=1, combat_calls=0,
                    actual_frame_dt=.016, supplied_frame_dt=.051, requested_dt=.05, applied_dt=.05,
                    input=vec(1, 0), alpha=1, held=False, bypass=False, before=before, after=after)
        raw = dict(scope='MOVEMENT_FIXTURE_ONLY', run_id=request['run_id'], nonce=request['nonce'],
                   case_id=spec['id'], request_sha256='c'*64, instance_id=23, spec=observed,
                   error='', complete=True, steps=[step], device_id=0)
        return request, raw

    def test_valid_safety_and_progress(self):
        a, b = self.sample()
        self.assertEqual(judge_case(a, b, 'c'*64)['status'], 'PASS')

    def test_old_nonce(self):
        a, b = self.sample();b['nonce'] = 'old'
        with self.assertRaisesRegex(ValueError, 'EVIDENCE_MISMATCH'): judge_case(a, b, 'c'*64)

    def test_changed_request(self):
        a, b = self.sample()
        with self.assertRaisesRegex(ValueError, 'EVIDENCE_MISMATCH'): judge_case(a, b, 'd'*64)

    def test_missing_step(self):
        a, b = self.sample();b['steps'] = []
        with self.assertRaisesRegex(ValueError, 'INCOMPLETE_RUN'): judge_case(a, b, 'c'*64)

    def test_silent_non_simulation(self):
        a, b = self.sample();b['steps'][0]['simulation_calls'] = 0
        with self.assertRaisesRegex(ValueError, 'OBSERVATION_GAP'): judge_case(a, b, 'c'*64)

    def test_combat_leak(self):
        a, b = self.sample();b['steps'][0]['combat_calls'] = 1
        with self.assertRaisesRegex(ValueError, 'OBSERVATION_GAP'): judge_case(a, b, 'c'*64)

    def test_wrong_applied_dt(self):
        a, b = self.sample();b['steps'][0]['applied_dt'] = .051
        with self.assertRaisesRegex(ValueError, 'TIME_MISMATCH'): judge_case(a, b, 'c'*64)

    def test_stationary_false_success(self):
        a, b = self.sample();b['steps'][0]['after']['center'] = vec(0, 0);b['steps'][0]['after']['reserved'] = [vec(0, 0)]
        b['steps'][0]['after']['actual'] = [vec(0, 0)]
        with self.assertRaisesRegex(ValueError, 'PROGRESS_MISMATCH'): judge_case(a, b, 'c'*64)

    def test_reservation_contamination(self):
        a, b = self.sample();b['steps'][0]['after']['reserved'] = [vec(8, 0)]
        with self.assertRaisesRegex(ValueError, 'RESERVATION_MISMATCH'): judge_case(a, b, 'c'*64)

    def test_nonfinite(self):
        a, b = self.sample();b['steps'][0]['after']['actual'][0]['x'] = float('nan')
        with self.assertRaisesRegex(ValueError, 'NONFINITE_OBSERVATION'): judge_case(a, b, 'c'*64)

    def test_boundary_violation(self):
        a, b = self.sample();b['steps'][0]['after']['actual'][0] = vec(410, 0)
        with self.assertRaisesRegex(ValueError, 'FOLLOW_MISMATCH'): judge_case(a, b, 'c'*64)

    def test_center_only_penetration_independent_of_flag(self):
        row = deepcopy(next(c for c in fixtures() if c['spec']['id'] == 'CENTER-BYPASS'))
        a = dict(run_id='a'*32, nonce='b'*32, body_offsets=[vec(-24, 0), vec(24, 0)], **row)
        _, b = self.sample();b.update(case_id=row['spec']['id'], spec=deepcopy(row['spec']))
        b['spec']['actual'] = [vec(-24, -20), vec(24, -20)]
        s = b['steps'][0]
        s.update(supplied_frame_dt=.05, requested_dt=.3, applied_dt=.3, input=vec(0, 1), bypass=True)
        s['before'] = dict(center=vec(0, -20), actual=deepcopy(b['spec']['actual']), reserved=deepcopy(b['spec']['actual']), elapsed=0, total=0)
        s['after'] = dict(center=vec(0, 22), actual=[vec(-24, 22), vec(24, 22)], reserved=[vec(-24, 22), vec(24, 22)], elapsed=.3, total=.3)
        result = judge_case(a, b, 'c'*64)
        self.assertEqual(result['reason'], 'SWEPT_PENETRATION')
        self.assertEqual({v['subject'] for v in result['violations']}, {'center'})

    def test_initial_actual_must_match_declaration(self):
        a, b = self.sample();b['steps'][0]['before']['actual'] = [vec(200, 0)];b['steps'][0]['after']['actual'] = [vec(-200, 0)]
        with self.assertRaisesRegex(ValueError, 'INITIAL_ACTUAL_MISMATCH'): judge_case(a, b, 'c'*64)

    def test_safe_stationary_survivor_is_not_following(self):
        a, b = self.sample();b['steps'][0]['after']['actual'] = [vec(0, 0)]
        with self.assertRaisesRegex(ValueError, 'FOLLOW_MISMATCH'): judge_case(a, b, 'c'*64)

    def test_initial_reserved_must_match_frozen_catalog(self):
        a, b = self.sample();b['steps'][0]['before']['reserved'] = [vec(24, 0)]
        with self.assertRaisesRegex(ValueError, 'RESERVATION_MISMATCH'): judge_case(a, b, 'c'*64)

    def test_held_must_preserve_whole_pose(self):
        a, b = self.sample();b['steps'][0]['held'] = True
        with self.assertRaisesRegex(ValueError, 'HELD_STATE_MOVED'): judge_case(a, b, 'c'*64)

    def test_intermediate_progress_is_required(self):
        a, b = self.sample();a['goal']['step0_center_x'] = [8, 9]
        with self.assertRaisesRegex(ValueError, 'PROGRESS_MISMATCH:step0_center_x'): judge_case(a, b, 'c'*64)

    def fake_cleanup(self, phase):
        state = dict(owner='r:n', pid=7, scene='DSMapMotion_n', marker='DSMapMotionOwner_r_n', phase=phase, playing=phase=='PLAY', devices=0)
        calls = []
        def method(name, *args):
            calls.append(name)
            if name == 'Ownership': return dict(state)
            if name == 'StopOwned': state.update(phase='STOPPED', playing=False)
            if name == 'Restore': state.update(owner='', phase='')
            if name == 'Snapshot': return {'saved': True}
        return state, calls, method

    def test_prepare_response_loss_uses_actual_owner(self):
        _, calls, method = self.fake_cleanup('PREPARED')
        self.assertEqual(cleanup_owned(method, lambda: None, 'r', 'n', 'snapshot', {'saved': True}, 7), 'PASS')
        self.assertIn('Restore', calls);self.assertNotIn('StopOwned', calls)

    def test_start_response_loss_stops_owned_play(self):
        _, calls, method = self.fake_cleanup('PLAY')
        self.assertEqual(cleanup_owned(method, lambda: None, 'r', 'n', 'snapshot', {'saved': True}, 7), 'PASS')
        self.assertLess(calls.index('StopOwned'), calls.index('Restore'))

    def test_wrong_owner_cleanup_does_not_mutate(self):
        state, calls, method = self.fake_cleanup('PLAY');state['owner'] = 'other'
        with self.assertRaisesRegex(RuntimeError, 'OWNERSHIP_CONFLICT'):
            cleanup_owned(method, lambda: None, 'r', 'n', 'snapshot', {'saved': True}, 7)
        self.assertEqual(calls, ['Ownership'])

    def test_entering_response_loss_waits_then_stops(self):
        state, calls, method = self.fake_cleanup('ENTERING')
        def ready(): state.update(phase='PLAY' if state['phase']=='ENTERING' else state['phase'], playing=state['phase']=='ENTERING')
        # The second ready occurs after the mock stop, which is already STOPPED.
        self.assertEqual(cleanup_owned(method, ready, 'r', 'n', 'snapshot', {'saved': True}, 7), 'PASS')

    def file_cleanup(self, state, tamper=False, source_ok=True):
        with tempfile.TemporaryDirectory() as tmp:
            project=Path(tmp)
            for name in ('Assets/MigrationVerification', 'Packages', 'ProjectSettings'):
                (project/name).mkdir(parents=True)
            before={name:tree_hashes(project/name) for name in ('Assets','Packages','ProjectSettings')}
            marker=project/'pending.json';marker.write_text('owned')
            rel='Assets/MigrationVerification/Map017-test.png'
            image=project/rel;meta=Path(str(image)+'.meta');image.write_bytes(b'PNG');meta.write_bytes(b'GUID')
            item=dict(path=rel,phase=state,sha256=digest(image),meta_sha256=digest(meta))
            if tamper:image.write_bytes(b'changed')
            calls=[]
            def delete(path):
                calls.append(path);image.unlink();meta.unlink();return True
            try:
                finish_owned_files(project,[item],before,lambda:source_ok,marker,delete)
            except RuntimeError as e:
                return str(e),marker.exists(),image.exists(),calls
            return 'PASS',marker.exists(),image.exists(),calls

    def test_capture_response_loss_preserves_pending_and_unconfirmed_file(self):
        error,marker,image,calls=self.file_cleanup('PLANNED')
        self.assertIn('CAPTURE_UNCONFIRMED',error);self.assertTrue(marker and image);self.assertFalse(calls)

    def test_capture_hash_conflict_preserves_pending_and_file(self):
        error,marker,image,calls=self.file_cleanup('CONFIRMED',tamper=True)
        self.assertEqual(error,'CAPTURE_OWNERSHIP_CONFLICT');self.assertTrue(marker and image);self.assertFalse(calls)

    def test_capture_cleanup_clears_pending_last(self):
        status,marker,image,calls=self.file_cleanup('CONFIRMED')
        self.assertEqual(status,'PASS');self.assertFalse(marker or image);self.assertEqual(len(calls),1)

    def test_final_source_change_preserves_pending_after_file_cleanup(self):
        error,marker,image,calls=self.file_cleanup('CONFIRMED',source_ok=False)
        self.assertEqual(error,'SOURCE_CHANGED_DURING_RUN');self.assertTrue(marker);self.assertFalse(image)

    def test_manifest_unique_and_fixed_expected_set(self):
        rows = fixtures();ids = [c['spec']['id'] for c in rows]
        self.assertEqual(len(ids), len(set(ids)))
        self.assertEqual(sum(x.startswith('OPEN-') for x in ids), 24)
        self.assertIn('KEYBOARD', ids)

    def test_cross_process_lock(self):
        with tempfile.TemporaryDirectory() as tmp:
            script = 'from pathlib import Path\nfrom map_motion_run import editor_lock\nwith editor_lock(Path('+repr(tmp)+')): print("BAD")'
            with editor_lock(Path(tmp)):
                proc = subprocess.run([sys.executable, '-c', script], cwd=Path(__file__).parent, capture_output=True, text=True)
            self.assertNotEqual(proc.returncode, 0)
            self.assertIn('EDITOR_LOCKED', proc.stderr)
            with editor_lock(Path(tmp)):
                pass


if __name__ == '__main__':
    unittest.main()

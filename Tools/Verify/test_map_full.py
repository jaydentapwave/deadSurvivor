"""Independent malformed-request / stale-evidence counterexamples for 026."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import map_full as h
from map_io import MapInputError, canonical_hash


def request(root):
    (root/'input.json').write_text('{"frozen":1}')
    geom={'boundary':[[-100,-100],[100,-100],[100,100],[-100,100]],'obstacles':[]}
    case=dict(case_id='EMPTY',parent_ids=[],requirement_ids=['MC-MOVE-01'],
              geometry=dict(geometry_id='EMPTY',revision='r001',coordinate_space='logical_xy',**geom,sha256=canonical_hash(geom)),
              bodies=[dict(slot=0,shape='circle',radius=8)],
              initial=dict(center=[0,0],formation=0,positions=[[0,0]],alive=[True],seed=17,loot_seed=29),
              input_steps=[dict(index=0,frame_dt_supplied=1/30,move=[1,0],commands=[],continue_on_rejection=False)],
              thresholds=dict(geometry_epsilon=.001,position_epsilon=.002,time_epsilon=.001,center_radius=8),
              expected=dict(status='PASS',reason='',assertions=[dict(id='SAFE',type='safety',target=None)]),
              mutation=dict(fi_id='none',variant='none'),display=dict(status='NOT_RUN',reason='adapter pending'),wall_timeout_s=30)
    return dict(schema_version=1,document_role=h.ROLE,suite_id='MAP-026',revision='r001',scope='G2_FIXTURE',mode='FIXED_REPLAY',map_mode='MapRequired',
                command_queue_limit=32,wall_timeout_s=300,max_evidence_bytes=1048576,
                inputs=[dict(path='input.json',sha256=hashlib.sha256((root/'input.json').read_bytes()).hexdigest())],
                expected_case_ids=['EMPTY'],cases=[case],required_fi_variants=[],controls={},required_requirements=[])


class ContractTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name).resolve();self.req=request(self.root)
    def tearDown(self):self.tmp.cleanup()
    def rejected(self,mutate,code=None):
        d=copy.deepcopy(self.req);mutate(d)
        with self.assertRaises(MapInputError) as e:h.validate_request(d,self.root)
        if code:self.assertEqual(e.exception.code,code)
    def test_valid_preflight_cannot_claim_runtime_acceptance(self):
        r=h.validate_request(self.req,self.root);self.assertEqual(r['status'],'PASS');self.assertEqual(r['execution_status'],'NOT_RUN');self.assertEqual(r['acceptance_status'],'BLOCKED')
    def test_empty_cases(self):self.rejected(lambda d:d.update(cases=[]),'EMPTY_OR_EXCESS_CASES')
    def test_duplicate_case(self):self.rejected(lambda d:d['cases'].append(copy.deepcopy(d['cases'][0])),'CASE_SET_MISMATCH')
    def test_unexpected_case(self):self.rejected(lambda d:d.update(expected_case_ids=['OTHER']),'CASE_SET_MISMATCH')
    def test_unknown_field(self):self.rejected(lambda d:d.update(silent_override=True),'FIELDS')
    def test_boolean_schema(self):self.rejected(lambda d:d.update(schema_version=True),'SCHEMA')
    def test_nan_dt(self):self.rejected(lambda d:d['cases'][0]['input_steps'][0].update(frame_dt_supplied=float('nan')),'INVALID_NUMBER')
    def test_negative_dt(self):self.rejected(lambda d:d['cases'][0]['input_steps'][0].update(frame_dt_supplied=-1),'NUMBER_RANGE')
    def test_bad_input_vector(self):self.rejected(lambda d:d['cases'][0]['input_steps'][0].update(move=[1,1]),'INPUT_MAGNITUDE')
    def test_sparse_step(self):self.rejected(lambda d:d['cases'][0]['input_steps'][0].update(index=1),'STEP_SEQUENCE')
    def test_unknown_command(self):
        self.rejected(lambda d:d['cases'][0]['input_steps'][0].update(commands=[dict(command_id='x',kind='Teleport',value=0)]),'UNSUPPORTED_COMMAND')
    def test_duplicate_command(self):
        def f(d):
            c=dict(command_id='x',kind='Pause',value=0);d['cases'][0]['input_steps'][0]['commands']=[c,c]
        self.rejected(f,'DUPLICATE_COMMAND')
    def test_slot_missing(self):self.rejected(lambda d:d['cases'][0]['bodies'][0].update(slot=1),'BODY_SLOT')
    def test_dead_slot_still_required(self):self.rejected(lambda d:d['cases'][0]['initial'].update(alive=[]),'INITIAL_BODIES')
    def test_geometry_hash_changed(self):self.rejected(lambda d:d['cases'][0]['geometry']['boundary'][0].__setitem__(0,-99),'GEOMETRY_HASH')
    def test_empty_assertions(self):self.rejected(lambda d:d['cases'][0]['expected'].update(assertions=[]),'EMPTY_ASSERTIONS')
    def test_input_changed(self):
        (self.root/'input.json').write_text('{}')
        self.rejected(lambda d:None,'INPUT_CHANGED')
    def test_path_escape(self):self.rejected(lambda d:d['inputs'][0].update(path='../input.json'),'INVALID_PATH')
    def test_symlink(self):
        (self.root/'link.json').symlink_to(self.root/'input.json')
        self.rejected(lambda d:d['inputs'][0].update(path='link.json'),'SYMLINK')
    def test_g3_cannot_mutate(self):
        def f(d):d['scope']='G3_MAP';d['cases'][0]['mutation']['fi_id']='FI-03'
        self.rejected(f,'G3_MUTATION')
    def test_coordinate_scaling_exact(self):
        g,s=h.normalized_geometry(self.req['cases'][0]['geometry']);self.assertEqual(s,16)
        from map_oracle import assess_sweep
        r=assess_sweep(g,[0,0],[1/16,0],8/16,.001/16)
        self.assertEqual(r['status'],'PASS')
    def test_evidence_cannot_self_seal(self):
        run=self.root/'run';run.mkdir();(run/'evidence-manifest.json').write_text(json.dumps(dict(run_id='a'*32,nonce='b'*32,files={'evidence-manifest.json':'x'})))
        with self.assertRaisesRegex(MapInputError,'SELF_HASH'):h.seal_check(run)
    def test_extra_file_rejected(self):
        run=self.root/'run';run.mkdir();(run/'a').write_text('x');(run/'evidence-manifest.json').write_text(json.dumps(dict(run_id='a'*32,nonce='b'*32,files={'a':hashlib.sha256(b'x').hexdigest()})))
        h.seal_check(run);(run/'injected').write_text('x')
        with self.assertRaisesRegex(MapInputError,'EVIDENCE_FILE_SET'):h.seal_check(run)
    def test_missing_file_rejected(self):
        run=self.root/'run';run.mkdir();(run/'evidence-manifest.json').write_text(json.dumps(dict(run_id='a'*32,nonce='b'*32,files={'missing':'x'})))
        with self.assertRaisesRegex(MapInputError,'EVIDENCE_FILE_SET'):h.seal_check(run)
    def test_tampered_file_rejected(self):
        run=self.root/'run';run.mkdir();(run/'a').write_text('tampered');(run/'evidence-manifest.json').write_text(json.dumps(dict(run_id='a'*32,nonce='b'*32,files={'a':hashlib.sha256(b'x').hexdigest()})))
        with self.assertRaisesRegex(MapInputError,'a'):h.seal_check(run)
    def test_nested_manifest_not_ignored(self):
        run=self.root/'run';run.mkdir();(run/'x').mkdir();(run/'x/evidence-manifest.json').write_text('{}')
        (run/'evidence-manifest.json').write_text(json.dumps(dict(run_id='a'*32,nonce='b'*32,files={'not-present':'0'*64})))
        with self.assertRaisesRegex(MapInputError,'EVIDENCE_FILE_SET'):h.seal_check(run)
    def test_directory_and_dangling_symlinks(self):
        for target in (self.root,self.root/'absent'):
            run=self.root/('run'+str(len(str(target))));run.mkdir();(run/'link').symlink_to(target)
            (run/'evidence-manifest.json').write_text(json.dumps(dict(run_id='a'*32,nonce='b'*32,files={'fake':'0'*64})))
            with self.assertRaises(MapInputError) as e:h.seal_check(run)
            self.assertEqual(e.exception.code,'SYMLINK')
    def test_size_limit(self):
        run=self.root/'large';run.mkdir();(run/'a').write_bytes(b'x'*2000);(run/'b').write_bytes(b'y'*2000)
        files={n:hashlib.sha256((run/n).read_bytes()).hexdigest() for n in ('a','b')}
        (run/'evidence-manifest.json').write_text(json.dumps(dict(run_id='a'*32,nonce='b'*32,files=files)))
        for limit in (1000,3500):
            with self.assertRaises(MapInputError) as e:h.seal_check(run,limit)
            self.assertEqual(e.exception.code,'EVIDENCE_LIMIT')
    def test_unknown_required_requirement(self):self.rejected(lambda d:d.update(required_requirements=['MC-BOGUS']),'UNKNOWN_REQUIREMENT')
    def test_unrepresented_requirement(self):self.rejected(lambda d:d.update(required_requirements=['MC-LIFE-01']),'MISSING_REQUIREMENT')
    def test_missing_normal_control(self):self.rejected(lambda d:d.update(required_fi_variants=['FI-03']),'CONTROLS')
    def test_huge_integer(self):self.rejected(lambda d:d.update(wall_timeout_s=10**400),'NUMBER_RANGE')
    def make_sealed(self,mutate=None):
        run=self.root/'sealed';run.mkdir()
        request_text=json.dumps(self.req);(run/'request.json').write_text(request_text)
        raw=dict(run_id='a'*32,nonce='b'*32,request_sha256=canonical_hash(self.req),cases=[dict(case_id='EMPTY')],execution_status='PASS')
        if mutate:mutate(raw)
        (run/'observations.json').write_text(json.dumps(raw))
        files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in run.iterdir()}
        (run/'evidence-manifest.json').write_text(json.dumps(dict(run_id='a'*32,nonce='b'*32,files=files)))
        return run
    def test_valid_seal_still_cannot_accept_runtime(self):
        run=self.make_sealed();self.assertEqual(h.judge_run(run,self.root)['status'],'BLOCKED')
    def test_wrong_nonce(self):
        run=self.make_sealed(lambda r:r.update(nonce='c'*32))
        with self.assertRaisesRegex(MapInputError,'STALE_EVIDENCE'):h.judge_run(run,self.root)
    def test_wrong_run(self):
        run=self.make_sealed(lambda r:r.update(run_id='c'*32))
        with self.assertRaisesRegex(MapInputError,'STALE_EVIDENCE'):h.judge_run(run,self.root)
    def test_wrong_request_hash(self):
        run=self.make_sealed(lambda r:r.update(request_sha256='c'*64))
        with self.assertRaisesRegex(MapInputError,'STALE_EVIDENCE'):h.judge_run(run,self.root)
    def test_missing_observation(self):
        run=self.make_sealed(lambda r:r.update(cases=[]))
        with self.assertRaisesRegex(MapInputError,'OBSERVATION_CASE_SET'):h.judge_run(run,self.root)
    def test_duplicate_observation(self):
        run=self.make_sealed(lambda r:r['cases'].append(dict(case_id='EMPTY')))
        with self.assertRaisesRegex(MapInputError,'OBSERVATION_CASE_SET'):h.judge_run(run,self.root)
    def test_invalid_execution_status(self):
        run=self.make_sealed(lambda r:r.update(execution_status='SUCCESS'))
        with self.assertRaisesRegex(MapInputError,'EXECUTION_STATUS'):h.judge_run(run,self.root)
    def test_verified_buffers_not_reopened(self):
        run=self.make_sealed();original=h.seal_check
        def replace_after_hash(path):
            answer=original(path);(run/'observations.json').write_text('invalid replacement');return answer
        with patch.object(h,'seal_check',replace_after_hash):
            self.assertEqual(h.judge_run(run,self.root)['status'],'BLOCKED')
    def test_aggregate_failure_wins(self):
        self.assertEqual(h.combine(['PASS','BLOCKED','FAIL']),'FAIL');self.assertEqual(h.combine([]),'NOT_RUN')

if __name__=='__main__':unittest.main()

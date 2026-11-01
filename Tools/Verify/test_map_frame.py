"""Counterexamples for the independent frame observer, not producer flags."""
from copy import deepcopy
import hashlib
import json
import unittest
from pathlib import Path

import map_frame_run as h

class FrameVerdictTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data=json.loads((h.b.PROJECT/'Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json').read_text())
    def setUp(self):
        self.spec=dict(run='a'*32,nonce='b'*32,id='MOVE-N1-F0',count=1,formation=0,mode='FIXED_REPLAY',initialMode='BATTLE',center={'x':0,'y':0},durations=[30]*10,inputs=[dict(dt=.05,move={'x':1,'y':0},commands=[],continueOnRejection=False)])
        empty=hashlib.sha256(b'').hexdigest()
        before=dict(center={'x':0,'y':0},actual=[{'x':0,'y':0}],reserved=[{'x':0,'y':0}],alive=[True],formation=0,pending=0,wave=1,team=[dict(hp=100,maxhp=100)],timers=[dict(slot=0)],config={'speed':1},enemies=[],shots=[],effects=[],state='battle',paused=False,elapsed=0,total=0,rngCalls=0,lootCalls=0,rngHash=empty,lootHash=empty)
        after=dict(before,center={'x':7,'y':0},actual=[{'x':4.2,'y':0}],reserved=[{'x':7,'y':0}],elapsed=.05,total=.05)
        step=dict(run=self.spec['run'],nonce=self.spec['nonce'],caseId=self.spec['id'],step=0,frame=11,before=json.dumps(before),after=json.dumps(after),actualDt=.016,suppliedDt=.05,requestedDt=.05,appliedDt=.05,simulationCalls=1,combatCalls=0,input={'x':1,'y':0},commands=[])
        self.raw=dict(run=self.spec['run'],nonce=self.spec['nonce'],id=self.spec['id'],scope='OWNED_FRAME_ADAPTER_ONLY',requestSha='hash',error='',complete=True,startFrame=10,endFrame=11,instanceId=1,initializationAfter=step['before'],steps=[step],rng=[],lootRng=[])
    def test_analytic_motion(self):self.assertEqual(h.judge(self.spec,self.raw,'hash',self.data)['status'],'PASS')
    def test_truncated_reservations(self):
        step=self.raw['steps'][0];after=json.loads(step['after']);after['reserved']=[];step['after']=json.dumps(after)
        with self.assertRaisesRegex(ValueError,'BODY_COUNT:reserved'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_applied_time_lie(self):
        step=self.raw['steps'][0];step['appliedDt']=.025
        with self.assertRaisesRegex(ValueError,'APPLIED_DT'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_unnecessary_slow_movement(self):
        step=self.raw['steps'][0];after=json.loads(step['after']);after['center']['x']=3.5;after['reserved'][0]['x']=3.5;after['actual'][0]['x']=2.1;step['after']=json.dumps(after)
        with self.assertRaisesRegex(ValueError,'NON_MAXIMAL_MOTION'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_wrong_initial_formation(self):
        self.spec['formation']=1
        with self.assertRaisesRegex(ValueError,'INITIAL_STATE'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_zero_input_valid_follow(self):
        self.spec['id']='HOLD';self.spec['inputs'][0]['move']={'x':0,'y':0};self.raw['id']='HOLD';step=self.raw['steps'][0];step['caseId']='HOLD';step['input']={'x':0,'y':0}
        before=json.loads(step['before']);before['actual'][0]['x']=-10
        after=dict(before,actual=[{'x':-4,'y':0}],elapsed=.05,total=.05)
        step['before']=json.dumps(before);self.raw['initializationAfter']=step['before'];step['after']=json.dumps(after)
        self.assertEqual(h.judge(self.spec,self.raw,'hash',self.data)['status'],'PASS')
    def test_false_hold_when_zero_candidate_recovers_lag(self):
        self.spec['id']='HOLD';self.spec['inputs'][0]['move']={'x':0,'y':0};self.raw['id']='HOLD';step=self.raw['steps'][0];step['caseId']='HOLD';step['input']={'x':0,'y':0}
        before=json.loads(step['before']);before['actual'][0]['x']=-10
        after=dict(before,elapsed=.05,total=.05)
        step['before']=json.dumps(before);self.raw['initializationAfter']=step['before'];step['after']=json.dumps(after)
        with self.assertRaisesRegex(ValueError,'UNJUSTIFIED_HOLD'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_wrong_nonce(self):
        self.raw['nonce']='c'*32
        with self.assertRaisesRegex(ValueError,'RAW_IDENTITY'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_missing_step(self):
        self.raw['steps']=[]
        with self.assertRaisesRegex(ValueError,'MISSING_STEPS'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_double_simulation(self):
        self.raw['steps'][0]['simulationCalls']=2
        with self.assertRaisesRegex(ValueError,'SIMULATION_COUNT'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_frame_frozen(self):
        self.raw['steps'][0]['frame']=10
        with self.assertRaisesRegex(ValueError,'OBSERVATION_GAP'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_follow_false_progress(self):
        step=self.raw['steps'][0];after=json.loads(step['after']);after['actual'][0]['x']=6;step['after']=json.dumps(after)
        with self.assertRaisesRegex(ValueError,'FOLLOW_MOTION'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_reserved_wrong(self):
        step=self.raw['steps'][0];after=json.loads(step['after']);after['reserved'][0]['x']=4;step['after']=json.dumps(after)
        with self.assertRaisesRegex(ValueError,'RESERVATION'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_rng_hash_mismatch(self):
        step=self.raw['steps'][0];after=json.loads(step['after']);after['rngHash']='x';step['after']=json.dumps(after)
        with self.assertRaisesRegex(ValueError,'RNG_TRACE_HASH'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_input_mismatch(self):
        self.raw['steps'][0]['input']={'x':0,'y':1}
        with self.assertRaisesRegex(ValueError,'INPUT_MISMATCH'):h.judge(self.spec,self.raw,'hash',self.data)
    def test_unobserved_state_change(self):
        state=json.loads(self.raw['initializationAfter']);state['elapsed']=.01;self.raw['initializationAfter']=json.dumps(state)
        with self.assertRaisesRegex(ValueError,'STATE_CONTAMINATION'):h.judge(self.spec,self.raw,'hash',self.data)

if __name__=='__main__':unittest.main()

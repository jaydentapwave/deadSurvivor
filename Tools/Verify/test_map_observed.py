import copy,json,unittest
import map_observed_run as o
class RecordTimeTests(unittest.TestCase):
    def make(self,state='battle',paused=False):
        settings=dict(speed=1,duration=30,drops=3,countScale=.49)
        value=dict(state=state,paused=paused,config=settings,history=[dict(prepTime=2)],scalars=[dict(name=k,value='0') for k in o.CLOCKS]+[dict(name='TestControlsUsed',value='False')])
        return value,settings
    def apply(self,a,names,dt):
        z=copy.deepcopy(a)
        for row in z['scalars']:
            if row['name'] in names:row['value']=str(float(row['value'])+dt)
        return z
    def check(self,a,z,dt,settings):o.ledger(json.dumps(a),json.dumps(z),dt,settings)
    def test_time_is_attributed_to_pre_command_state(self):
        a,settings=self.make();z=self.apply(a,('RealBattle','WaveReal'),.02);self.check(a,z,.02,settings)
        z['paused']=True
        with self.assertRaisesRegex(ValueError,'GAMEPLAY_MUTATION'):self.check(a,z,.02,settings)
    def test_pause_ledger(self):
        a,settings=self.make(paused=True);z=self.apply(a,('RealPaused','WavePaused'),.02);self.check(a,z,.02,settings)
    def test_prep_history(self):
        a,settings=self.make(state='prep');z=self.apply(a,('RealPrep',),.02);z['history'][0]['prepTime']+=.02;self.check(a,z,.02,settings)
        z['history'][0]['prepTime']=2
        with self.assertRaisesRegex(ValueError,'PREP_HISTORY'):self.check(a,z,.02,settings)
    def test_missing_record_time_rejected(self):
        a,settings=self.make()
        with self.assertRaisesRegex(ValueError,'RECORD_TIME'):self.check(a,a,.02,settings)
    def test_incorrect_counter_and_gameplay_change_rejected(self):
        a,settings=self.make();z=self.apply(a,('RealBattle','WaveReal'),.02);z['config']['speed']=2
        with self.assertRaisesRegex(ValueError,'GAMEPLAY_MUTATION'):self.check(a,z,.02,settings)
    def test_control_flag(self):
        a,settings=self.make();a['config']=dict(settings,speed=2);z=self.apply(a,('RealBattle','WaveReal'),.02)
        with self.assertRaisesRegex(ValueError,'CONTROLS'):self.check(a,z,.02,settings)
        z['scalars'][-1]['value']='True';self.check(a,z,.02,settings)
    def test_post_ledger_clock_mutation_rejected(self):
        a,_=self.make();z=self.apply(a,('RealPrep',),100)
        with self.assertRaisesRegex(ValueError,'PHASE_CLOCK_MUTATION'):o.phase_clocks(json.dumps(a),json.dumps(z))
    def test_next_wave_only_resets_wave_clocks(self):
        a,_=self.make();a=self.apply(a,('WaveReal','WavePaused','RealPrep'),2);z=copy.deepcopy(a)
        for row in z['scalars']:
            if row['name'] in ('WaveReal','WavePaused'):row['value']='0'
        o.phase_clocks(json.dumps(a),json.dumps(z),True)
        z['scalars'][0]['value']='100'
        with self.assertRaisesRegex(ValueError,'PHASE_CLOCK_MUTATION'):o.phase_clocks(json.dumps(a),json.dumps(z),True)
    def test_identity_before_normalization(self):
        spec=dict(id='REJECT-REVIVE-LIVE',run='a',nonce='b')
        with self.assertRaisesRegex(ValueError,'RAW_IDENTITY'):o.judge(spec,dict(id='wrong',run='a',nonce='b',requestSha='sha'),'sha',{})
    def test_csharp_float_controls_compare_at_float32_precision(self):
        a,settings=self.make();a['config']=dict(settings,countScale=.49000000953674316);z=self.apply(a,('RealBattle','WaveReal'),.02);self.check(a,z,.02,settings)
    def test_input_stage_only_allows_move_input_text(self):
        a,_=self.make();a['scalars'].append(dict(name='MoveInput',value='(0.00, 0.00)'));z=copy.deepcopy(a);z['scalars'][-1]['value']='(1.00, 0.00)'
        o.input_stage(json.dumps(a),json.dumps(z))
        z['hp']=1
        with self.assertRaisesRegex(ValueError,'STATE_MUTATION'):o.input_stage(json.dumps(a),json.dumps(z))
    def test_input_stage_requires_unique_move_input(self):
        a,_=self.make()
        with self.assertRaisesRegex(ValueError,'INPUT_SCALAR_SHAPE'):o.input_stage(json.dumps(a),json.dumps(a))
        a['scalars']+=[dict(name='MoveInput',value='x')]*2
        with self.assertRaisesRegex(ValueError,'INPUT_SCALAR_SHAPE'):o.input_stage(json.dumps(a),json.dumps(a))
    def test_fixed_plan_unique(self):
        rows=o.planned_cases();self.assertEqual(len({r['id'] for r in rows}),len(rows));self.assertEqual(len([r for r in rows if r['id'].startswith('MOVE-')]),12)
class NormalPipelineTests(unittest.TestCase):
    def setUp(self):
        from test_map_frame import FrameVerdictTests
        fixture=FrameVerdictTests('test_analytic_motion');fixture.setUpClass();fixture.setUp()
        self.spec=fixture.spec;self.raw=fixture.raw;self.data=fixture.data
        settings=o.old.load_json(o.b.PROJECT/'Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json')['settings']
        step=self.raw['steps'][0];before=json.loads(step['before']);after=json.loads(step['after'])
        for snap in (before,after):snap.update(config=settings,history=[],scalars=[dict(name=k,value='0') for k in o.CLOCKS]+[dict(name='TestControlsUsed',value='False')])
        for snap in (before,after):snap['scalars'].append(dict(name='MoveInput',value='informational'))
        timed=copy.deepcopy(before)
        for snap in (timed,after):
            for row in snap['scalars']:
                if row['name'] in ('RealBattle','WaveReal'):row['value']=str(step['actualDt'])
        step.update(before=json.dumps(before),afterTime=json.dumps(timed),beforeSimulation=json.dumps(timed),afterSimulation=json.dumps(after),after=json.dumps(after),eventOffset=0,gameFrame=1,gameSimulation=1)
        self.raw.update(scope=o.ROLE,initializationAfter=step['before'],initialQueueNonce='a'*32,initialEventSequence=0,initialEventOffset=0,initialGameFrames=0,initialGameSimulations=0,hasFailedFrame=False)
    def check(self):return o.judge(self.spec,self.raw,'hash',self.data)
    def test_complete_normalized_pipeline(self):self.assertEqual(self.check()['status'],'PASS')
    def test_late_clock_corruption(self):
        step=self.raw['steps'][0];after=json.loads(step['after'])
        after['scalars'][1]['value']='100';step['after']=step['afterSimulation']=json.dumps(after)
        with self.assertRaisesRegex(ValueError,'PHASE_CLOCK_MUTATION'):self.check()
    def test_unanchored_first_simulation_rejected(self):
        self.raw['steps'][0]['gameSimulation']=9
        with self.assertRaisesRegex(ValueError,'NATIVE_SIMULATION_GAP'):self.check()
    def test_wrong_event_offset(self):
        self.raw['steps'][0]['eventOffset']=9
        with self.assertRaisesRegex(ValueError,'EVENT_OFFSET'):self.check()
    def test_null_first_anchor_rejected(self):
        self.raw['initialGameSimulations']=None
        with self.assertRaisesRegex(ValueError,'INITIAL_ANCHOR'):self.check()
    def test_wave_boundary_blocked_explicitly(self):
        step=self.raw['steps'][0];after=json.loads(step['after']);after['state']='prep';step['afterSimulation']=step['after']=json.dumps(after)
        with self.assertRaisesRegex(RuntimeError,'BLOCKED_UNSUPPORTED_SIMULATION_BOUNDARY'):self.check()
    def cancellation_fixture(self):
        self.spec['id']=self.raw['id']='REJECT-TAIL-CANCEL'
        step=self.raw['steps'][0];step['caseId']=self.spec['id'];step.update(simulationCalls=0,gameSimulation=0,beforeSimulation='',afterSimulation='',after=step['afterTime'],requestedDt=0)
        commands=next(r for r in o.planned_cases() if r['id']=='REJECT-TAIL-CANCEL')['inputs'][0]['commands'];self.spec['inputs'][0]['commands']=commands
        step['commands']=[dict(id=c['id'],kind=c['kind'],value=c['value'],source='TEST',nonce='a'*32,enqueueFrame=11,consumeFrame=11,sequence=i,result=c['expectedResult'],before=step['afterTime'],after=step['afterTime']) for i,c in enumerate(commands)]
    def test_rejection_tail_cancellation_pipeline(self):
        self.cancellation_fixture();self.assertEqual(self.check()['status'],'PASS')
    def test_cancelled_command_cannot_execute(self):
        self.cancellation_fixture();self.raw['steps'][0]['commands'][1]['result']='ACCEPTED'
        with self.assertRaisesRegex(ValueError,'COMMAND_RESULT'):self.check()
    def test_wrong_original_identity(self):
        self.raw['id']='foreign'
        with self.assertRaisesRegex(ValueError,'RAW_IDENTITY'):self.check()
class FaultPipelineTests(unittest.TestCase):
    def setUp(self):
        fixture=NormalPipelineTests();fixture.setUp();self.raw=fixture.raw;self.spec=fixture.spec;self.data=fixture.data
        step=self.raw['steps'][0];initial=json.loads(step['before']);timed=json.loads(step['afterTime'])
        for snap in (initial,timed):snap['scalars'].append(dict(name='Paused',value='False'))
        partial=copy.deepcopy(timed);partial['paused']=True;partial['scalars'][-1]['value']='True'
        before=json.dumps(initial);after_time=json.dumps(timed);after=json.dumps(partial)
        self.spec.update(id='NEGATIVE-EXTERNAL_QUEUE_DURING_COMMAND',fault='EXTERNAL_QUEUE_DURING_COMMAND')
        self.spec['inputs'][0]['commands']=[dict(id='first-pause',kind='Pause',value=0),dict(id='tail-resume',kind='Resume',value=0)]
        event=dict(id='first-pause',kind='Pause',value=0,source='TEST',nonce='a'*32,sequence=0,enqueueFrame=11,consumeFrame=11,result='EXCEPTION:EXTERNAL_QUEUED_COMMAND:Resume',before=after_time,after=after)
        step.update(before=before,afterTime=after_time,after=after,simulationCalls=0,gameSimulation=0,commands=[event])
        self.raw.update(id=self.spec['id'],initializationAfter=before,partialState=after,failedFrame=step,hasFailedFrame=True,steps=[],complete=False,error='System.InvalidOperationException: EXTERNAL_QUEUED_COMMAND:Resume')
    def check(self):return o.judge(self.spec,self.raw,'hash',self.data)
    def test_expected_native_failure_is_detected(self):self.assertEqual(self.check()['status'],'PASS')
    def test_extra_mutation_rejected(self):
        partial=json.loads(self.raw['partialState']);partial['hp']=1;text=json.dumps(partial)
        self.raw['partialState']=self.raw['failedFrame']['after']=self.raw['failedFrame']['commands'][0]['after']=text
        with self.assertRaisesRegex(ValueError,'EXTRA_MUTATION'):self.check()
    def test_corrupt_exception_envelopes(self):
        original=copy.deepcopy(self.raw)
        for key,value in [('nonce','b'*32),('source','UI'),('sequence',7),('consumeFrame',9),('kind','Resume'),('value',3)]:
            self.raw=copy.deepcopy(original);self.raw['failedFrame']['commands'][0][key]=value
            with self.assertRaisesRegex(ValueError,'ENVELOPE'):self.check()
    def test_empty_error_is_not_expected_failure(self):
        self.raw['error']=''
        with self.assertRaisesRegex(ValueError,'EXPECTED_NATIVE_FAILURE'):self.check()
if __name__=='__main__':unittest.main()

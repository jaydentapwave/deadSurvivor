#!/usr/bin/env python3
"""Normal production Update/queue observations, combat-free body8 fixture only."""
import copy,json,math,re,struct
import hashlib,os,shutil,time,uuid
from pathlib import Path
from datetime import datetime
from zoneinfo import ZoneInfo
from map_io import load_json
from map_input_scope import verify_then_release
import map_frame_run as old
from map_frame_run import b,m,BASE,near,snapshot_shape,command_effect
CLOCKS=('RealBattle','RealPrep','RealPaused','WaveReal','WavePaused','LobbyTime','ResultTime')
ROLE='NORMAL_MAP_FRAME_OBSERVER_030'

def semantic(text):
    value=json.loads(text)
    value['scalars']=[r for r in value['scalars'] if r['name'] not in CLOCKS+('TestControlsUsed',)]
    for row in value['history']:row.pop('prepTime',None)
    return json.dumps(value,sort_keys=True,separators=(',',':'))

def ledger(before,after,dt,settings):
    if not math.isfinite(dt) or dt<0:raise ValueError('ACTUAL_TIME')
    a=json.loads(before);z=json.loads(after)
    sa={r['name']:r['value'] for r in a['scalars']};sz={r['name']:r['value'] for r in z['scalars']}
    active={'lobby':('LobbyTime',),'result':('ResultTime',),'prep':('RealPrep',),'battle':('RealPaused','WavePaused') if a['paused'] else ('RealBattle','WaveReal')}[a['state']]
    for name in CLOCKS:
        if not near(float(sz[name]),float(sa[name])+(dt if name in active else 0),max(.00001,abs(float(sz[name]))*1e-6)):raise ValueError('RECORD_TIME:'+name)
    f32=lambda v:struct.unpack('f',struct.pack('f',v))[0]
    changed=any(f32(a['config'][k])!=f32(settings[k]) for k in ('speed','duration','countScale')) or a['config']['drops']!=settings['drops']
    expected=sa['TestControlsUsed']=='True' or a['state'] in ('prep','battle') and changed
    if (sz['TestControlsUsed']=='True')!=expected:raise ValueError('TEST_CONTROLS_LEDGER')
    for i,(prior,end) in enumerate(zip(a['history'],z['history'])):
        expected=prior['prepTime']+(dt if a['state']=='prep' and i==len(a['history'])-1 else 0)
        if not near(end['prepTime'],expected,max(.00001,abs(expected)*1e-6)):raise ValueError('PREP_HISTORY_TIME')
    if semantic(before)!=semantic(after):raise ValueError('RECORD_TIME_GAMEPLAY_MUTATION')

def input_stage(before,after):
    a=json.loads(before);z=json.loads(after)
    # MoveInput is assigned by the shared input collection between these phases.
    # The exact collected vector is independently checked against the frozen tape
    # (or native device) and the full movement oracle, not its rounded scalar text.
    for value in (a,z):
        rows=[r for r in value['scalars'] if r['name']=='MoveInput']
        if len(rows)!=1 or type(rows[0].get('value')) is not str:raise ValueError('INPUT_SCALAR_SHAPE')
        value['scalars']=[r for r in value['scalars'] if r['name']!='MoveInput']
    if a!=z:raise ValueError('INPUT_COLLECTION_STATE_MUTATION')

def phase_clocks(before,after,reset_wave=False):
    a=json.loads(before);z=json.loads(after)
    sa={r['name']:r['value'] for r in a['scalars']};sz={r['name']:r['value'] for r in z['scalars']}
    for name in CLOCKS+('TestControlsUsed',):
        expected='0' if reset_wave and name in ('WaveReal','WavePaused') else sa[name]
        if name=='TestControlsUsed':ok=sz[name]==expected
        else:ok=float(sz[name])==float(expected)
        if not ok:raise ValueError('PHASE_CLOCK_MUTATION:'+name)
    if [r['prepTime'] for r in a['history']]!=[r['prepTime'] for r in z['history']]:raise ValueError('PHASE_HISTORY_TIME_MUTATION')

def planned_cases():
    rows=old.planned_cases()
    for row in rows:
        if row['id']=='REJECT-REVIVE-LIVE':row['inputs'][0]['commands'][0]['expectedResult']='NOT_DEAD'
    rows.extend([
        dict(id='REJECT-TAIL-CANCEL',count=4,formation=0,mode='FIXED_REPLAY',initialMode='BATTLE',inputs=[dict(dt=.03,move=m.vec(1,0),continueOnRejection=False,commands=[dict(id='bad-prep',kind='NextWave',value=0,expectedResult='STATE_REJECTED'),dict(id='cancel-pause',kind='Pause',value=0,expectedResult='CANCELLED:REJECTION')]),dict(dt=.03,move=m.vec(1,0),continueOnRejection=False,commands=[])]),
        dict(id='DT-LIMITS',count=4,formation=0,mode='FIXED_REPLAY',initialMode='BATTLE',inputs=[dict(dt=dt,move=m.vec(1,0),continueOnRejection=True,commands=[]) for dt in (0,.000001,1,.05)])])
    for fault in ('EXTERNAL_QUEUE_DURING_COMMAND','UNOWNED_DOMAIN_AFTER_BIND','UNOWNED_SIMULATE_AFTER_BIND'):
        commands=[dict(id='first-pause',kind='Pause',value=0),dict(id='tail-resume',kind='Resume',value=0)] if fault=='EXTERNAL_QUEUE_DURING_COMMAND' else []
        rows.append(dict(id='NEGATIVE-'+fault,count=4,formation=0,mode='FIXED_REPLAY',initialMode='BATTLE',fault=fault,inputs=[dict(dt=.03,move=m.vec(1,0),continueOnRejection=False,commands=commands)]))
    return rows

def judge_failure(spec,raw):
    fault=spec['fault'];active=fault=='EXTERNAL_QUEUE_DURING_COMMAND'
    expected='System.InvalidOperationException: EXTERNAL_QUEUED_COMMAND:Resume' if active else 'UNOWNED_DOMAIN_CALL:Pause' if fault=='UNOWNED_DOMAIN_AFTER_BIND' else 'UNOWNED_MAP_SIMULATE'
    if raw.get('complete') or raw.get('recordingError') or raw.get('steps') or raw.get('error','').splitlines()[:1]!=[expected]:raise ValueError('EXPECTED_NATIVE_FAILURE_MISSING')
    if raw.get('hasFailedFrame') is not active:raise ValueError('FAILED_FRAME_CLASSIFICATION')
    initial=raw['initializationAfter'];partial=raw['partialState']
    if active:
        frame=raw['failedFrame'];events=frame['commands']
        settings=old.load_json(b.PROJECT/'Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json')['settings']
        ledger(frame['before'],frame['afterTime'],frame['actualDt'],settings)
        if frame['before']!=initial or frame['after']!=partial or frame['simulationCalls']!=0 or frame['gameSimulation']!=raw['initialGameSimulations'] or frame['gameFrame']!=raw['initialGameFrames']+1:raise ValueError('FAILED_FRAME_COUNTERS')
        if len(events)!=1 or events[0]['id']!='first-pause' or events[0]['result']!='EXCEPTION:EXTERNAL_QUEUED_COMMAND:Resume' or events[0]['before']!=frame['afterTime'] or events[0]['after']!=partial:raise ValueError('FAILED_COMMAND_PARTIAL_STATE')
        event=events[0];wanted=spec['inputs'][0]['commands'][0]
        if any(event[k]!=wanted[k] for k in ('id','kind','value')) or event['source']!='TEST' or event['nonce']!=raw['initialQueueNonce'] or event['sequence']!=raw['initialEventSequence'] or event['enqueueFrame']!=frame['frame'] or event['consumeFrame']!=frame['frame'] or frame['eventOffset']!=raw['initialEventOffset'] or frame['step']!=0 or frame['frame']<=raw['startFrame']:raise ValueError('FAILED_COMMAND_ENVELOPE')
        a=json.loads(frame['afterTime']);a['paused']=True
        paused_rows=[r for r in a['scalars'] if r['name']=='Paused']
        if len(paused_rows)!=1:raise ValueError('FAILED_PAUSE_SCALAR')
        paused_rows[0]['value']='True'
        if a!=json.loads(partial):raise ValueError('FAILED_COMMAND_EXTRA_MUTATION')
    elif initial!=partial:raise ValueError('DIRECT_REJECTION_MUTATED_STATE')
    return dict(status='PASS',scope=ROLE,expected_failure=fault,steps=0,independent_sweeps=0,instance=raw['instanceId'])

def judge(spec,raw,request_sha,data,body_assess=None):
    if raw.get('requestSha')!=request_sha or any(raw.get(k)!=spec[k] for k in ('run','nonce','id')):raise ValueError('NORMAL_RAW_IDENTITY')
    for key in ('initialGameFrames','initialGameSimulations','initialEventOffset','initialEventSequence'):
        if type(raw.get(key)) is not int or raw[key]<0:raise ValueError('NORMAL_INITIAL_ANCHOR')
    if type(raw.get('initialQueueNonce')) is not str or not re.fullmatch('[0-9a-f]{32}',raw['initialQueueNonce']):raise ValueError('NORMAL_INITIAL_NONCE')
    if spec.get('fault'):
        if raw.get('scope')!=ROLE:raise ValueError('NORMAL_SCOPE')
        return judge_failure(spec,raw)
    if raw.get('scope')!=ROLE or raw.get('recordingError') or raw.get('hasFailedFrame') or raw.get('partialState'):raise ValueError('NORMAL_SCOPE_OR_FAILURE')
    if raw.get('error') or not raw.get('complete') or len(raw.get('steps',[]))!=len(spec['inputs']):raise ValueError('NORMAL_INCOMPLETE')
    settings=old.load_json(b.PROJECT/'Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json')['settings']
    transformed=copy.deepcopy(raw);transformed['scope']='OWNED_FRAME_ADAPTER_ONLY';converted_spec=copy.deepcopy(spec)
    previous=raw['initializationAfter'];native_sim=raw['initialGameSimulations'];event_sequence=raw['initialEventSequence']-1;game_frame=raw['initialGameFrames'];queue_nonce=raw['initialQueueNonce'];event_offset=raw['initialEventOffset']
    converted_sequence=0
    for i,step in enumerate(raw['steps']):
        if step['step']!=i or step['before']!=previous:raise ValueError('NORMAL_FRAME_CONTAMINATION')
        previous=step['after'];ledger(step['before'],step['afterTime'],step['actualDt'],settings)
        if game_frame is not None and step['gameFrame']!=game_frame+1:raise ValueError('NORMAL_NATIVE_FRAME_GAP')
        game_frame=step['gameFrame']
        if native_sim is not None and step['gameSimulation']-native_sim!=step['simulationCalls']:raise ValueError('NORMAL_NATIVE_SIMULATION_GAP')
        native_sim=step['gameSimulation']
        if step['eventOffset']!=event_offset:raise ValueError('NORMAL_EVENT_OFFSET')
        event_offset+=len(step['commands'])
        commands=step['commands'];expected=spec['inputs'][i]['commands']
        if len(commands)!=len(expected):raise ValueError('NORMAL_COMMAND_CUT')
        cursor=step['afterTime'];kept=[];kept_expected=[];cancelled=False
        for wanted,event in zip(expected,commands):
            if any(event[k]!=wanted[k] for k in ('id','kind','value')) or event['source']!='TEST' or event['enqueueFrame']!=step['frame'] or event['consumeFrame']!=step['frame']:raise ValueError('NORMAL_COMMAND_IDENTITY')
            if event_sequence is not None and event['sequence']!=event_sequence+1:raise ValueError('NORMAL_EVENT_SEQUENCE')
            event_sequence=event['sequence']
            if queue_nonce is not None and event['nonce']!=queue_nonce:raise ValueError('NORMAL_NONCE_CHANGED')
            queue_nonce=event['nonce']
            if event['before']!=cursor or event['result']!=wanted.get('expectedResult','ACCEPTED'):raise ValueError('NORMAL_COMMAND_RESULT')
            if cancelled:
                if event['result']!='CANCELLED:REJECTION' or event['before']!=event['after']:raise ValueError('NORMAL_CANCEL_TAIL')
            else:
                kept.append(dict(event,sequence=converted_sequence));kept_expected.append(wanted);converted_sequence+=1
                if event['result']!='ACCEPTED':
                    if event['before']!=event['after']:raise ValueError('NORMAL_REJECTION_MUTATION')
                    cancelled=not spec['inputs'][i]['continueOnRejection']
            phase_clocks(event['before'],event['after'],event['kind']=='NextWave' and event['result']=='ACCEPTED')
            cursor=event['after']
        if step['simulationCalls']:
            if step['simulationCalls']!=1 or step['afterSimulation']!=step['after']:raise ValueError('NORMAL_SIMULATION_PHASE')
            input_stage(cursor,step['beforeSimulation'])
            a=json.loads(cursor);z=json.loads(step['afterSimulation'])
            if a['state']!=z['state'] or a['wave']!=z['wave']:raise RuntimeError('BLOCKED_UNSUPPORTED_SIMULATION_BOUNDARY_030')
            applied=z['elapsed']-a['elapsed']
        else:
            if cursor!=step['after'] or step['beforeSimulation'] or step['afterSimulation']:raise ValueError('NORMAL_STOPPED_PHASE')
            applied=0
        phase_clocks(cursor,step['after'])
        dst=transformed['steps'][i]
        dst.update(run=spec['run'],nonce=spec['nonce'],caseId=spec['id'],before=semantic(step['afterTime']),after=semantic(step['after']),combatCalls=0,appliedDt=applied,commands=kept)
        for event in kept:
            event['before']=semantic(event['before']);event['after']=semantic(event['after'])
        converted_spec['inputs'][i]['commands']=kept_expected
    transformed['initializationAfter']=semantic(raw['initializationAfter'])
    # The shared mathematical checker receives normalized clock-free snapshots;
    # actual RecordTime and every production queue event are checked above.
    if spec['id']=='REJECT-REVIVE-LIVE':
        converted_spec['id']=transformed['id']='NORMAL-REJECT-REVIVE-LIVE'
        for step in transformed['steps']:step['caseId']=converted_spec['id']
    result=old.judge(converted_spec,transformed,request_sha,data,body_assess)
    result.update(scope=ROLE,actual_record_time='PASS',normal_queue='PASS')
    return result


def run():
    BASE.mkdir(parents=True,exist_ok=True);uid,nonce=uuid.uuid4().hex,uuid.uuid4().hex
    out=BASE/('normal-030-'+uid);out.mkdir();u=b.Unity(out);sources=m.source_hashes();pending=m.BASE/'unity-pending.json'
    report=dict(status='BLOCKED',scope=ROLE,run_id=uid,nonce=nonce,time=datetime.now(ZoneInfo('Asia/Seoul')).isoformat(),g2='NOT_RUN',g3='NOT_RUN',g4='NOT_RUN',cases={})
    snapshot=before=pid=None;captures=[]
    settings=load_json(b.PROJECT/'Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json')['settings']
    durations=[w['duration']*settings['duration']/30 for w in settings['waves']]
    plans=[dict(c,run=uid,nonce=nonce,center=m.vec(0,0),seed=17,lootSeed=29,durations=durations) for c in planned_cases()]
    m.write(out/'manifest.json',dict(run_id=uid,nonce=nonce,scope=report['scope'],expected_case_ids=[c['id'] for c in plans],cases=plans,sources=sources))
    with m.editor_lock():
        if pending.exists():raise RuntimeError('UNFINISHED_OWNED_SESSION')
        try:
            u.ready();console=u.cli(['command','console','get','--caller','plugin','--skill','ds-workflow','--','--tail','1','--level','error'])['data']['result'];m.write(out/'console-before.json',console)
            snapshot=u.session('Snapshot');pid=u.session('Ownership')['pid'];m.write(out/'snapshot.json',snapshot)
            before={n:m.tree_hashes(b.PROJECT/n) for n in ('Assets','Packages','ProjectSettings')};m.write(out/'target-before.json',before)
            m.write(pending,dict(run_id=uid,nonce=nonce,snapshot=str(out/'snapshot.json'),pid=os.getpid(),scope=report['scope']))
            u.session('Prepare',uid,nonce);u.session('StartOwned',uid,nonce);u.ready()
            data=load_json(b.PROJECT/'Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json')
            for spec in plans:
                request=out/(spec['id']+'.request.json');raw_path=out/(spec['id']+'.raw.json');m.write(request,spec)
                u.evaluate('return DeadSurvivor.Editor.DSNormalMapVerification.Begin('+','.join(json.dumps(str(x)) for x in (request,raw_path,m.digest(request)))+');')
                deadline=time.monotonic()+45
                while not raw_path.exists() and time.monotonic()<deadline:time.sleep(.15)
                if not raw_path.exists():
                    progress=u.evaluate('return DeadSurvivor.Editor.DSNormalMapVerification.Progress();')
                    m.write(out/(spec['id']+'.progress.json'),progress)
                    raise RuntimeError('NORMAL_OUTPUT_ABSENT:'+spec['id']+':'+str(progress))
                raw=load_json(raw_path,64*1024*1024)
                report['cases'][spec['id']]=judge(spec,raw,m.digest(request),data)
                if spec.get('fault'):
                    time.sleep(.15)
                    terminal=json.loads(u.evaluate('return DeadSurvivor.Editor.DSNormalMapVerification.TerminalEvidence('+json.dumps(uid)+','+json.dumps(nonce)+');'))
                    m.write(out/(spec['id']+'.terminal.json'),terminal)
                    expected_sim=raw['initialGameSimulations'];expected_frame=raw['initialGameFrames']+int(spec['fault']=='EXTERNAL_QUEUE_DURING_COMMAND')
                    if terminal['state']!=raw['partialState'] or terminal['gameFrames']!=expected_frame or terminal['gameSimulations']!=expected_sim:raise ValueError('TERMINAL_GAME_CONTINUED')
                    if spec['fault']=='EXTERNAL_QUEUE_DURING_COMMAND' and terminal['events']!=raw['failedFrame']['commands']:raise ValueError('TERMINAL_COMMAND_CONTINUED')
                    if spec['fault']=='UNOWNED_DOMAIN_AFTER_BIND':
                        events=terminal['events'][raw['initialEventOffset']:]
                        if len(events)!=1 or events[0]['kind']!='Pause' or events[0]['source']!='DIRECT' or events[0]['result']!='UNOWNED_DOMAIN_CALL':raise ValueError('DIRECT_DOMAIN_REJECTION_EVIDENCE')
                    if spec['fault']=='UNOWNED_SIMULATE_AFTER_BIND' and len(terminal['events'])!=raw['initialEventOffset']:raise ValueError('DIRECT_SIMULATION_EXTRA_EVENT')
                m.write(out/'judgments.json',report['cases'])
                if spec['id']=='DEVICE-MOVE':
                    rel=f'Assets/MigrationVerification/Map030-{uid}.png';native=b.PROJECT/rel
                    if native.exists() or Path(str(native)+'.meta').exists():raise RuntimeError('CAPTURE_EXISTS')
                    captures.append(dict(path=rel,phase='PLANNED',run_id=uid,nonce=nonce));m.write(out/'owned-captures.json',captures)
                    cap=u.cli(['command','capture','game_view','--caller','plugin','--skill','ds-workflow','--','--source','screen','--save_path',rel.removeprefix('Assets/')])['data']['result']
                    if (b.PROJECT/cap['savedPath']).resolve()!=native.resolve():raise RuntimeError('CAPTURE_PATH')
                    u.evaluate('UnityEditor.AssetDatabase.ImportAsset('+json.dumps(rel)+',UnityEditor.ImportAssetOptions.ForceSynchronousImport);return true;')
                    captures[-1].update(phase='CONFIRMED',sha256=m.digest(native),meta_sha256=m.digest(str(native)+'.meta'));m.write(out/'owned-captures.json',captures);shutil.copy2(native,out/'device.png')
            a=load_json(out/'MOVE-N1-F0.raw.json',64*1024*1024);z=load_json(out/'REPLAY-N1-F0.raw.json',64*1024*1024)
            def replay(raw):return [dict(before=semantic(s['before']),after=semantic(s['after']),input=s['input'],requested=s['requestedDt']) for s in raw['steps']]
            if replay(a)!=replay(z) or a['rng']!=z['rng'] or a['lootRng']!=z['lootRng']:raise ValueError('REPLAY_MISMATCH')
            report['replay']='PASS'
            logs=u.cli(['command','console','get','--caller','plugin','--skill','ds-workflow','--','--tail','100','--level','error'])['data']['result'];m.write(out/'console-after.json',logs)
            new_errors=[e for e in logs.get('entries',[]) if e['seq']>console['cursor']]
            expected_errors=['Map command session stopped: EXTERNAL_QUEUED_COMMAND:Resume']
            if logs.get('session')!=console.get('session') or logs.get('dropped') or [e.get('message') for e in new_errors]!=expected_errors or logs.get('groundTruth',{}).get('consoleErrors',0)!=console.get('groundTruth',{}).get('consoleErrors',0)+len(expected_errors):raise ValueError('CONSOLE_ERRORS')
            if set(report['cases'])!={s['id'] for s in plans}:raise ValueError('MISSING_CASES')
            report['status']='PASS'
        except Exception as e:report.update(status='FAIL' if isinstance(e,(ValueError,AssertionError)) else 'BLOCKED',error=str(e))
        finally:
            try:
                if pending.exists():m.cleanup_owned(u.session,u.ready,uid,nonce,str(out/'snapshot.json'),snapshot,pid)
                if before is not None:
                    report['input_scopes'],after=verify_then_release(b.PROJECT,uid,nonce,lambda:m.finish_owned_files(b.PROJECT,captures,before,lambda:sources==m.source_hashes(),pending if pending.exists() else None,lambda rel:u.evaluate('return UnityEditor.AssetDatabase.DeleteAsset('+json.dumps(rel)+');')),required=report['status']=='PASS',expected_count=len(plans) if report['status']=='PASS' else None)
                    m.write(out/'target-after.json',after)
                report['cleanup']='PASS'
            except Exception as e:report.update(status='BLOCKED',cleanup='BLOCKED',cleanup_error=str(e))
            report.update(sources=sources,finished_at=datetime.now(ZoneInfo('Asia/Seoul')).isoformat());report['artifacts']={str(p.relative_to(out)):m.digest(p) for p in out.rglob('*') if p.is_file() and p.name!='summary.json'};m.write(out/'summary.json',report)
    print(json.dumps(dict(status=report['status'],report=str(out/'summary.json'),error=report.get('error'),cleanup=report.get('cleanup'))));return 0 if report['status']=='PASS' else 1

if __name__=='__main__':raise SystemExit(run())

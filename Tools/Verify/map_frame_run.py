#!/usr/bin/env python3
"""Actual Update adapter checks; intentionally not full G2/G3 acceptance."""
import json
import hashlib
import math
import os
from pathlib import Path
import shutil
import time
import uuid
from datetime import datetime
from zoneinfo import ZoneInfo

from map_input_scope import verified_scopes, verify_then_release
import map_blockout as b
from map_io import load_json
m=b.motion
BASE=m.ROOT/'output/map-full-026'


def planned_cases():
    def steps(n=12,move=(1,0),commands=None):
        return [dict(dt=1/30,move=m.vec(*move),commands=(commands or []) if i==0 else [],continueOnRejection=False) for i in range(n)]
    def command(id,kind,value=0,expected='ACCEPTED'):return dict(id=id,kind=kind,value=value,expectedResult=expected)
    cases=[]
    for n in range(1,5):
        for f in range(3):
            cases.append(dict(id=f'MOVE-N{n}-F{f}',count=n,formation=f,mode='FIXED_REPLAY',initialMode='BATTLE',inputs=steps()))
    cases.extend([
        dict(id='PAUSE-HOLD',count=4,formation=0,mode='FIXED_REPLAY',initialMode='BATTLE',inputs=steps(commands=[command('pause','Pause')])),
        dict(id='PREP-HOLD',count=4,formation=0,mode='FIXED_REPLAY',initialMode='PREP',inputs=steps()),
        dict(id='PREP-COMMAND-FIFO',count=4,formation=0,mode='FIXED_REPLAY',initialMode='PREP',inputs=steps(commands=[command('formation','SelectFormation',1),command('next','NextWave')])),
        dict(id='REJECT-REVIVE-LIVE',count=4,formation=0,mode='FIXED_REPLAY',initialMode='PREP',inputs=steps(commands=[command('revive','Revive',0,expected='STATE_REJECTED')])),
        dict(id='REVIVE-DEAD',count=4,formation=0,mode='FIXED_REPLAY',initialMode='DEAD_PREP',inputs=steps(commands=[command('revive','Revive',0)])),
        dict(id='DEVICE-MOVE',count=4,formation=0,mode='DEVICE_SMOKE',initialMode='BATTLE',inputs=steps(16))])
    # Two separately initialized runs with exactly the same seeds/inputs.
    cases.append(dict(cases[0],id='REPLAY-N1-F0'))
    return cases


def offsets(n,f):
    catalog=([[-24,-24],[24,-24],[-24,24],[24,24]],[[0,-48],[-48,0],[48,0],[0,48]],[[-72,0],[-24,0],[24,0],[72,0]])[f][:n]
    mean=[sum(p[j] for p in catalog)/n for j in (0,1)]
    return [[p[j]-mean[j] for j in (0,1)] for p in catalog]


def near(a,b,eps=.003):return abs(a-b)<=eps


def snapshot_shape(snap,n):
    for key in ('actual','reserved','alive','team','timers'):
        if type(snap.get(key)) is not list or len(snap[key])!=n:raise ValueError('BODY_COUNT:'+key)
    if any(type(v) is not bool for v in snap['alive']):raise ValueError('ALIVE_TYPE')
    for i,(alive,body,timer) in enumerate(zip(snap['alive'],snap['team'],snap['timers'])):
        if alive!=(body['hp']>0) or timer['slot']!=i:raise ValueError('LIFECYCLE_MISMATCH')
    if snap['state'] not in ('battle','prep','result','lobby') or type(snap['paused']) is not bool:raise ValueError('SNAPSHOT_STATE')
    if snap.get('enemies') or snap.get('shots'):raise ValueError('COMBAT_LEAK')


def command_effect(kind,value,before,after,result):
    if result!='ACCEPTED':return
    if kind in ('Pause','Resume') and (after['paused']!=(kind=='Pause') or after['state']!='battle'):raise ValueError('PAUSE_POSTCONDITION')
    if kind in ('SelectFormation','CancelFormation') and after['pending']!=(value if kind=='SelectFormation' else before['formation']):raise ValueError('FORMATION_POSTCONDITION')
    if kind=='Revive':
        if not after['alive'][value] or not near(after['team'][value]['hp'],after['team'][value]['maxhp']):raise ValueError('REVIVE_POSTCONDITION')
    if kind=='NextWave':
        if after['wave']!=before['wave']+1 or after['state']!='battle' or after['paused'] or after['elapsed']!=0 or after['formation']!=before['pending']:raise ValueError('NEXT_WAVE_POSTCONDITION')


def judge(spec,raw,request_sha,data,body_assess=None):
    if raw.get('scope')!='OWNED_FRAME_ADAPTER_ONLY' or raw.get('requestSha')!=request_sha or any(raw.get(k)!=spec[k] for k in ('run','nonce','id')):raise ValueError('RAW_IDENTITY')
    if raw.get('error') or not raw.get('complete'):raise ValueError('INCOMPLETE:'+str(raw.get('error')))
    if len(raw['steps'])!=len(spec['inputs']) or raw['endFrame']<=raw['startFrame']:raise ValueError('MISSING_STEPS')
    if spec['mode']=='DEVICE_SMOKE' and raw.get('deviceId',0)<=0:raise ValueError('MISSING_DEVICE')
    initial=json.loads(raw['initializationAfter']);snapshot_shape(initial,spec['count'])
    expected_state='battle' if spec['initialMode']=='BATTLE' else 'prep'
    expected_formation=spec['formation'] if spec['initialMode']=='BATTLE' else 0
    if initial['state']!=expected_state or initial['formation']!=expected_formation or initial['pending']!=spec['formation'] or initial['paused']:raise ValueError('INITIAL_STATE')
    if not near(initial['config']['speed'],1,1e-7):raise ValueError('SPEED_NOT_FROZEN')
    if initial['alive']!=[not(spec['initialMode']=='DEAD_PREP' and i==0) for i in range(spec['count'])]:raise ValueError('INITIAL_LIFECYCLE')
    if any(not near(initial['center'][k],spec['center'][k]) for k in ('x','y')):raise ValueError('INITIAL_CENTER')
    previous=raw['initializationAfter'];frame=raw['startFrame'];checks=0;sequence=0
    for index,step in enumerate(raw['steps']):
        if any(step[k]!=spec[t] for k,t in [('run','run'),('nonce','nonce'),('caseId','id')]) or step['step']!=index or step['frame']<=frame:raise ValueError('OBSERVATION_GAP')
        frame=step['frame']
        if step['before']!=previous:raise ValueError('STATE_CONTAMINATION')
        before=json.loads(step['before']);after=json.loads(step['after']);previous=step['after']
        snapshot_shape(before,spec['count']);snapshot_shape(after,spec['count'])
        for snap in (before,after):
            for off,p in zip(offsets(spec['count'],snap['formation']),snap['reserved']):
                if any(not near(p[k],snap['center'][k]+off[j]) for j,k in enumerate(('x','y'))):raise ValueError('RESERVATION')
        commands=spec['inputs'][index]['commands']
        if len(step['commands'])>len(commands):raise ValueError('COMMAND_COUNT')
        cursor=step['before']
        rejected=False
        checked=0
        for expected,observed in zip(commands,step['commands']):
            checked+=1
            if observed['id']!=expected['id'] or observed['kind']!=expected['kind'] or observed['sequence']!=sequence or observed['enqueueFrame']!=step['frame'] or observed['before']!=cursor:raise ValueError('COMMAND_ORDER')
            pre_command=json.loads(observed['before']);post_command=json.loads(observed['after']);snapshot_shape(pre_command,spec['count']);snapshot_shape(post_command,spec['count'])
            if observed['result']!=expected.get('expectedResult','ACCEPTED'):raise ValueError('COMMAND_EXPECTATION')
            command_effect(expected['kind'],expected['value'],pre_command,post_command,observed['result'])
            cursor=observed['after'];sequence+=1
            if observed['result']!='ACCEPTED':
                if observed['before']!=observed['after']:raise ValueError('REJECT_NOT_ATOMIC')
                rejected=True
                if not spec['inputs'][index]['continueOnRejection']:break
        consumed=len(step['commands'])
        if consumed!=checked:raise ValueError('COMMAND_TRAILING_OBSERVATIONS')
        stopped=rejected and not spec['inputs'][index]['continueOnRejection']
        if consumed!=len(commands) and not (stopped and consumed>0):raise ValueError('COMMAND_COUNT')
        pre_sim=json.loads(cursor)
        should_sim=pre_sim['state']=='battle' and not pre_sim['paused'] and not stopped
        if step.get('combatCalls')!=0:raise ValueError('COMBAT_LEAK')
        if step['simulationCalls']!=int(should_sim):raise ValueError('SIMULATION_COUNT')
        if not math.isfinite(step['actualDt']) or step['actualDt']<0:raise ValueError('FRAME_TIME')
        if spec['mode']=='FIXED_REPLAY' and not near(step['suppliedDt'],spec['inputs'][index]['dt'],1e-6):raise ValueError('SUPPLIED_DT')
        requested=min(step['suppliedDt'],.05) if should_sim else 0
        duration=spec['durations'][pre_sim['wave']-1]
        expected_applied=min(requested,max(0,duration-pre_sim['elapsed'])) if should_sim else 0
        if not near(step['appliedDt'],expected_applied,1e-5):raise ValueError('APPLIED_DT')
        if not near(step['requestedDt'],requested,1e-6):raise ValueError('REQUESTED_DT')
        if not should_sim:
            if cursor!=step['after'] or step['appliedDt']!=0:raise ValueError('STOPPED_STATE_CHANGED')
        else:
            if not near(after['elapsed'],pre_sim['elapsed']+step['appliedDt']) or not near(after['total'],pre_sim['total']+step['appliedDt']):raise ValueError('APPLIED_TIME')
            move=spec['inputs'][index]['move']
            if spec['mode']=='FIXED_REPLAY' and step['input']!=move:raise ValueError('INPUT_MISMATCH')
            if spec['mode']=='DEVICE_SMOKE' and step['input']!={'x':1,'y':0}:raise ValueError('DEVICE_INPUT_MISSING')
            delta=[after['center'][k]-pre_sim['center'][k] for k in ('x','y')]
            full=[step['input'][k]*140*step['appliedDt'] for k in ('x','y')]
            length=sum(v*v for v in full)
            alpha=sum(a*z for a,z in zip(delta,full))/length if length else 0
            if not -.0001<=alpha<=1.0001 or length and abs(alpha*256-round(alpha*256))>.01 or any(not near(d,v*alpha) for d,v in zip(delta,full)):raise ValueError('CENTER_MOTION')
            factor=min(1,12*step['appliedDt'])
            hold_override=False
            for i,(start,end,target) in enumerate(zip(pre_sim['actual'],after['actual'],after['reserved'])):
                if pre_sim['alive'][i]:
                    if any(not near(end[k],start[k]+(target[k]-start[k])*factor) for k in ('x','y')):
                        # Complete hold may preserve every body rather than follow.
                        if pre_sim['center']!=after['center'] or pre_sim['actual']!=after['actual']:raise ValueError('FOLLOW_MOTION')
                        hold_override=True
                elif start!=end:raise ValueError('CORPSE_MOVED')
            if pre_sim['alive']!=after['alive']:raise ValueError('UNDECLARED_LIFECYCLE')
            # Refute under-movement with the independent geometry oracle. Do not
            # infer feasibility monotonically: every larger discrete candidate matters.
            candidates=range(256,-1,-1) if hold_override and length else [0] if hold_override else range(256,max(0,round(alpha*256)),-1) if length else []
            for j in candidates:
                candidate=[pre_sim['center'][k]+step['input'][k]*140*step['appliedDt']*j/256 for k in ('x','y')]
                segments=[(b.xy(pre_sim['center']),candidate,-1)]
                for i,off in enumerate(offsets(spec['count'],pre_sim['formation'])):
                    target=[candidate[k]+off[k] for k in (0,1)]
                    segments.append((b.xy(pre_sim['reserved'][i]),target,i))
                    if pre_sim['alive'][i]:
                        a=b.xy(pre_sim['actual'][i]);segments.append((a,[a[k]+(target[k]-a[k])*factor for k in (0,1)],i))
                if all((body_assess(a,z,slot,True) if body_assess else b.assess(data,a,z,4,8))['status']=='PASS' for a,z,slot in segments):raise ValueError('UNJUSTIFIED_HOLD' if hold_override else 'NON_MAXIMAL_MOTION')
            # Independent geometry receives recorded positions, never solver flags.
            for label in ('center','actual','reserved'):
                starts=[pre_sim[label]] if label=='center' else pre_sim[label]
                ends=[after[label]] if label=='center' else after[label]
                for i,(a,z) in enumerate(zip(starts,ends)):
                    if label=='actual' and not pre_sim['alive'][i]:continue
                    result=body_assess(b.xy(a),b.xy(z),-1 if label=='center' else i,False) if body_assess else b.assess(data,b.xy(a),b.xy(z),4,8);checks+=1
                    if result['status']!='PASS':raise ValueError('UNSAFE_SWEEP:'+label+':'+str(i)+':'+str(result))
    for label,key in [('rng','rng'),('lootRng','loot')]:
        calls=raw[label]
        if any(c['sequence']!=i or c['method'] not in ('Next','NextDouble') for i,c in enumerate(calls)):raise ValueError('RNG_TRACE_SEQUENCE')
        for step in raw['steps']:
            snapshots=[json.loads(step[location]) for location in ('before','after')] + [json.loads(c[location]) for c in step['commands'] for location in ('before','after')]
            for snap in snapshots:
                count=snap[key+'Calls']
                if not 0<=count<=len(calls):raise ValueError('RNG_COUNT')
                text=';'.join(str(c['sequence'])+':'+c['method']+':'+c['args']+':'+c['response'] for c in calls[:count])
                if hashlib.sha256(text.encode()).hexdigest()!=snap[key+'Hash']:raise ValueError('RNG_TRACE_HASH')
            pre_sim=json.loads(step['commands'][-1]['after'] if step['commands'] else step['before']);end_sim=json.loads(step['after'])
            if pre_sim['state']==end_sim['state'] and pre_sim[key+'Calls']!=end_sim[key+'Calls']:raise ValueError('UNEXPECTED_SIMULATION_RANDOM')
        end_snapshot=json.loads(raw['steps'][-1]['after'])
        if end_snapshot[key+'Calls']!=len(calls):raise ValueError('RNG_FINAL_COUNT')
    end=json.loads(raw['steps'][-1]['after'])
    if spec['id'].startswith(('MOVE-','REPLAY-','DEVICE-')) and end['center']['x']<=spec['center']['x']+1:raise ValueError('NO_PROGRESS')
    if spec['id']=='REJECT-REVIVE-LIVE' and raw['steps'][0]['commands'][0]['result']!='STATE_REJECTED':raise ValueError('EXPECTED_REJECTION_MISSING')
    if spec['id']=='REVIVE-DEAD' and not end['alive'][0]:raise ValueError('REVIVE_FAILED')
    return dict(status='PASS',steps=len(raw['steps']),independent_sweeps=checks,instance=raw['instanceId'])


def run():
    BASE.mkdir(parents=True,exist_ok=True);uid,nonce=uuid.uuid4().hex,uuid.uuid4().hex
    out=BASE/('frames-'+uid);out.mkdir();u=b.Unity(out);sources=m.source_hashes();pending=m.BASE/'unity-pending.json'
    report=dict(status='BLOCKED',scope='OWNED_FRAME_ADAPTER_ONLY',run_id=uid,nonce=nonce,time=datetime.now(ZoneInfo('Asia/Seoul')).isoformat(),g2='NOT_RUN',g3='NOT_RUN',g4='NOT_RUN',cases={})
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
                u.evaluate('return DeadSurvivor.Editor.DSMapFullVerification.Begin('+','.join(json.dumps(str(x)) for x in (request,raw_path,m.digest(request)))+');')
                deadline=time.monotonic()+45
                while not raw_path.exists() and time.monotonic()<deadline:time.sleep(.15)
                if not raw_path.exists():raise RuntimeError('FRAME_CASE_TIMEOUT:'+spec['id'])
                raw=load_json(raw_path,64*1024*1024)
                report['cases'][spec['id']]=judge(spec,raw,m.digest(request),data);m.write(out/'judgments.json',report['cases'])
                if spec['id']=='DEVICE-MOVE':
                    rel=f'Assets/MigrationVerification/Map026-{uid}.png';native=b.PROJECT/rel
                    if native.exists() or Path(str(native)+'.meta').exists():raise RuntimeError('CAPTURE_EXISTS')
                    captures.append(dict(path=rel,phase='PLANNED',run_id=uid,nonce=nonce));m.write(out/'owned-captures.json',captures)
                    cap=u.cli(['command','capture','game_view','--caller','plugin','--skill','ds-workflow','--','--source','screen','--save_path',rel.removeprefix('Assets/')])['data']['result']
                    if (b.PROJECT/cap['savedPath']).resolve()!=native.resolve():raise RuntimeError('CAPTURE_PATH')
                    u.evaluate('UnityEditor.AssetDatabase.ImportAsset('+json.dumps(rel)+',UnityEditor.ImportAssetOptions.ForceSynchronousImport);return true;')
                    captures[-1].update(phase='CONFIRMED',sha256=m.digest(native),meta_sha256=m.digest(str(native)+'.meta'));m.write(out/'owned-captures.json',captures);shutil.copy2(native,out/'device.png')
            a=load_json(out/'MOVE-N1-F0.raw.json',64*1024*1024);z=load_json(out/'REPLAY-N1-F0.raw.json',64*1024*1024)
            def replay(raw):return [dict(before=s['before'],after=s['after'],input=s['input'],applied=s['appliedDt']) for s in raw['steps']]
            if replay(a)!=replay(z) or a['rng']!=z['rng'] or a['lootRng']!=z['lootRng']:raise ValueError('REPLAY_MISMATCH')
            report['replay']='PASS'
            logs=u.cli(['command','console','get','--caller','plugin','--skill','ds-workflow','--','--tail','100','--level','error'])['data']['result'];m.write(out/'console-after.json',logs)
            if logs.get('session')!=console.get('session') or logs.get('dropped') or any(e['seq']>console['cursor'] for e in logs.get('entries',[])) or logs.get('groundTruth',{}).get('consoleErrors',0):raise ValueError('CONSOLE_ERRORS')
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

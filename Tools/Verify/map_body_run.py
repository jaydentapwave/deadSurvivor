#!/usr/bin/env python3
"""032 bounded profile binding diagnostic. This is not full G2/G3 acceptance."""
import copy,json,struct
import map_observed_run as normal
from map_observed_run import b,m,BASE,old,near
from map_oracle import Geometry,assess_sweep
from map_io import load_json
from map_input_scope import verify_then_release
from map_snapshot_run import safe_output,write_owned
import os,shutil,time,uuid
from pathlib import Path
from datetime import datetime
from zoneinfo import ZoneInfo
ROLE='BODY_PROFILE_BINDING_DIAGNOSTIC_032'

def planned_cases():
    def polygon(x0,x1,y0=-500,y1=500):return [m.vec(x0,y0),m.vec(x1,y0),m.vec(x1,y1),m.vec(x0,y1)]
    def step(commands=None):return dict(dt=.05,move=m.vec(1,0),continueOnRejection=False,commands=commands or [])
    def command(kind,value=0,result='ACCEPTED'):return dict(id=kind,kind=kind,value=value,expectedResult=result)
    def case(id,radius,count=1,obstacles=None,width=500,initial='BATTLE',commands=None,device=False):
        return dict(id=id,count=count,formation=0,useFixture=True,mode='DEVICE_SMOKE' if device else 'FIXED_REPLAY',initialMode=initial,fixture=dict(boundary=polygon(-width,width),obstacles=[dict(points=p) for p in obstacles or []],radii=[radius]*count,epsilon=.0001),inputs=[step(commands)]+[step() for _ in range(3 if device else 1)])
    rows=[dict(id='DEFAULT8',count=4,formation=0,mode='FIXED_REPLAY',initialMode='BATTLE',bindingChecks=True,inputs=[step(),step()])]
    for radius in (.25,1):
        rows.append(case('EMPTY-'+str(radius),radius))
        rows.append(case('DIVIDER-'+str(radius),radius,obstacles=[polygon(3.4,3.6)]))
    rows[1]['bindingChecks']=True
    rows.extend([case('NARROW-WEDGE',1,4,width=60,initial='PREP',commands=[command('SelectFormation',1),command('NextWave')]),case('NARROW-LINE-REJECT',1,4,width=60,initial='PREP',commands=[command('SelectFormation',2,'MAP_PARTY_POSE_BLOCKED'),command('NextWave',result='CANCELLED:REJECTION')]),case('REVIVE',1,4,width=60,initial='DEAD_PREP',commands=[command('Revive')]),case('DEVICE-MOVE',1,device=True)])
    return rows

def judge(spec,raw,request_sha,data):
    binding=raw.get('bodyBinding')
    if not isinstance(binding,dict):raise ValueError('MISSING_ACTUAL_BINDING')
    f32=lambda v:struct.unpack('f',struct.pack('f',v))[0]
    if spec.get('fixture'):
        requested=spec['fixture']
        def normalized(poly):return [{k:f32(p[k]) for k in ('x','y')} for p in poly]
        if binding['boundary']!=normalized(requested['boundary']) or binding['obstacles']!=[dict(points=normalized(p['points'])) for p in requested['obstacles']] or binding['radii']!=[f32(r) for r in requested['radii']] or not binding['fixture'] or binding['centerRadius']!=max(binding['radii']) or not near(binding['worldScale'],.02,1e-8) or binding['elevation']!=55 or not near(binding['epsilon'],.0001,1e-10):raise ValueError('ACTUAL_BINDING_MISMATCH')
        geometry=Geometry.from_polygons([b.xy(p) for p in binding['boundary']],[[b.xy(p) for p in o['points']] for o in binding['obstacles']])
        def assess(a,z,slot,maximal):return assess_sweep(geometry,a,z,binding['centerRadius'] if slot==-1 else binding['radii'][slot],binding['epsilon']*2)
        result=normal.judge(spec,raw,request_sha,data,assess)
        initial=json.loads(raw['initializationAfter'])
        for key in ('center','reserved','actual'):
            for i,p in enumerate([initial[key]] if key=='center' else initial[key]):
                if assess(b.xy(p),b.xy(p),-1 if key=='center' else i,False)['status']!='PASS':raise ValueError('UNSAFE_INITIAL:'+key)
        first=json.loads(raw['steps'][0]['after'])
        if spec['id'].startswith('EMPTY-') and (not near(first['center']['x'],7,.00001) or not near(first['actual'][0]['x'],4.2,.00001)):raise ValueError('EMPTY_ANALYTIC_MOTION')
        if spec['id'].startswith('DIVIDER-') and not 0<first['center']['x']<3.4-binding['centerRadius']:raise ValueError('DIVIDER_CLEARANCE')
    else:
        if binding['fixture'] or binding['radii']!=[8]*spec['count'] or binding['centerRadius']!=8:raise ValueError('DEFAULT_PROFILE_CHANGED')
        result=normal.judge(spec,raw,request_sha,data)
    if spec.get('bindingChecks'):
        checks=json.loads(raw['bindingChecks'])
        expected={'default8-count-'+str(i) for i in range(1,5)}|{'radii-owned-copy','nonfinite-radius','zero-radius','oversize-radius','slot-count','bind-once','setup-after-bind','bound-values-not-authoring-data'}
        if spec.get('fixture'):expected|={'count-rejected-before-start-mutation','snapshot031-blocks-fixture'}
        else:expected.add('snapshot031-default-compatible')
        if checks['status']!='PASS' or checks['count']!=len(expected) or set(checks['checks'])!=expected:raise ValueError('BINDING_UNITS')
        result['binding_checks']=checks
    result['scope']=ROLE
    return result


def run():
    safe_output(BASE);BASE.mkdir(parents=True,exist_ok=True);uid,nonce=uuid.uuid4().hex,uuid.uuid4().hex
    out=BASE/('body-032-'+uid);safe_output(out);out.mkdir();u=b.Unity(out);sources=m.source_hashes();pending=m.BASE/'unity-pending.json'
    report=dict(status='BLOCKED',scope=ROLE,run_id=uid,nonce=nonce,time=datetime.now(ZoneInfo('Asia/Seoul')).isoformat(),g2='NOT_RUN',g3='NOT_RUN',g4='NOT_RUN',cases={})
    snapshot=before=pid=None;captures=[]
    settings=load_json(b.PROJECT/'Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json')['settings']
    durations=[w['duration']*settings['duration']/30 for w in settings['waves']]
    plans=[dict(c,run=uid,nonce=nonce,center=m.vec(0,0),seed=17,lootSeed=29,durations=durations) for c in planned_cases()]
    write_owned(out/'manifest.json',dict(run_id=uid,nonce=nonce,scope=report['scope'],expected_case_ids=[c['id'] for c in plans],cases=plans,sources=sources))
    with m.editor_lock():
        if pending.exists():raise RuntimeError('UNFINISHED_OWNED_SESSION')
        try:
            u.ready();console=u.cli(['command','console','get','--caller','plugin','--skill','ds-workflow','--','--tail','1','--level','error'])['data']['result'];write_owned(out/'console-before.json',console)
            snapshot=u.session('Snapshot');pid=u.session('Ownership')['pid'];write_owned(out/'snapshot.json',snapshot)
            before={n:m.tree_hashes(b.PROJECT/n) for n in ('Assets','Packages','ProjectSettings')};write_owned(out/'target-before.json',before)
            write_owned(pending,dict(run_id=uid,nonce=nonce,snapshot=str(out/'snapshot.json'),pid=os.getpid(),scope=report['scope']))
            u.session('Prepare',uid,nonce);u.session('StartOwned',uid,nonce);u.ready()
            data=load_json(b.PROJECT/'Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json')
            for spec in plans:
                request=out/(spec['id']+'.request.json');raw_path=out/(spec['id']+'.raw.json');write_owned(request,spec)
                u.evaluate('return DeadSurvivor.Editor.DSNormalMapVerification.Begin('+','.join(json.dumps(str(x)) for x in (request,raw_path,m.digest(request)))+');')
                deadline=time.monotonic()+45
                while not raw_path.exists() and time.monotonic()<deadline:time.sleep(.15)
                if not raw_path.exists():
                    progress=u.evaluate('return DeadSurvivor.Editor.DSNormalMapVerification.Progress();')
                    write_owned(out/(spec['id']+'.progress.json'),progress)
                    raise RuntimeError('NORMAL_OUTPUT_ABSENT:'+spec['id']+':'+str(progress))
                raw=load_json(raw_path,64*1024*1024)
                report['cases'][spec['id']]=judge(spec,raw,m.digest(request),data)
                write_owned(out/'judgments.json',report['cases'])
                if spec['id']=='DEVICE-MOVE':
                    rel=f'Assets/MigrationVerification/Map032-{uid}.png';native=b.PROJECT/rel
                    if native.exists() or Path(str(native)+'.meta').exists():raise RuntimeError('CAPTURE_EXISTS')
                    captures.append(dict(path=rel,phase='PLANNED',run_id=uid,nonce=nonce));write_owned(out/'owned-captures.json',captures)
                    cap=u.cli(['command','capture','game_view','--caller','plugin','--skill','ds-workflow','--','--source','screen','--save_path',rel.removeprefix('Assets/')])['data']['result']
                    if (b.PROJECT/cap['savedPath']).resolve()!=native.resolve():raise RuntimeError('CAPTURE_PATH')
                    u.evaluate('UnityEditor.AssetDatabase.ImportAsset('+json.dumps(rel)+',UnityEditor.ImportAssetOptions.ForceSynchronousImport);return true;')
                    captures[-1].update(phase='CONFIRMED',sha256=m.digest(native),meta_sha256=m.digest(str(native)+'.meta'));write_owned(out/'owned-captures.json',captures);shutil.copy2(native,out/'device.png')
            cold=json.loads(u.evaluate('return DeadSurvivor.Editor.DSMapBodyVerification.ColdRequired('+json.dumps(uid)+','+json.dumps(nonce)+');'))
            write_owned(out/'cold-required.json',cold)
            if cold['error']!='MAP_REQUIRED_BINDING_MISSING' or not cold['disabled'] or not cold['noLegacy']:raise ValueError('COLD_REQUIRED_LEGACY_FALLBACK')
            report['cold_required']='PASS'
            logs=u.cli(['command','console','get','--caller','plugin','--skill','ds-workflow','--','--tail','100','--level','error'])['data']['result'];write_owned(out/'console-after.json',logs)
            new_errors=[e for e in logs.get('entries',[]) if e['seq']>console['cursor']]
            expected_errors=[]
            if logs.get('session')!=console.get('session') or logs.get('dropped') or [e.get('message') for e in new_errors]!=expected_errors or logs.get('groundTruth',{}).get('consoleErrors',0)!=console.get('groundTruth',{}).get('consoleErrors',0)+len(expected_errors):raise ValueError('CONSOLE_ERRORS')
            if set(report['cases'])!={s['id'] for s in plans}:raise ValueError('MISSING_CASES')
            report['status']='PASS'
        except Exception as e:report.update(status='FAIL' if isinstance(e,(ValueError,AssertionError)) else 'BLOCKED',error=str(e))
        finally:
            try:
                if pending.exists():m.cleanup_owned(u.session,u.ready,uid,nonce,str(out/'snapshot.json'),snapshot,pid)
                if before is not None:
                    report['input_scopes'],after=verify_then_release(b.PROJECT,uid,nonce,lambda:m.finish_owned_files(b.PROJECT,captures,before,lambda:sources==m.source_hashes(),pending if pending.exists() else None,lambda rel:u.evaluate('return UnityEditor.AssetDatabase.DeleteAsset('+json.dumps(rel)+');')),required=report['status']=='PASS',expected_count=len(plans) if report['status']=='PASS' else None)
                    write_owned(out/'target-after.json',after)
                report['cleanup']='PASS'
            except Exception as e:report.update(status='BLOCKED',cleanup='BLOCKED',cleanup_error=str(e))
            report.update(sources=sources,finished_at=datetime.now(ZoneInfo('Asia/Seoul')).isoformat());report['artifacts']={str(p.relative_to(out)):m.digest(p) for p in out.rglob('*') if p.is_file() and p.name!='summary.json'};write_owned(out/'summary.json',report)
    print(json.dumps(dict(status=report['status'],report=str(out/'summary.json'),error=report.get('error'),cleanup=report.get('cleanup'))));return 0 if report['status']=='PASS' else 1

if __name__=='__main__':raise SystemExit(run())

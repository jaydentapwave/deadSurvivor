#!/usr/bin/env python3
"""Owned Unity CLI checks for isolated 021–023 map experiments."""
from pathlib import Path
import argparse,json,shutil,sys,time,uuid
from datetime import datetime
from zoneinfo import ZoneInfo
from map_input_scope import verified_scopes, verify_then_release
import map_blockout as b
m=b.motion

def invoke(u,name,*args):
    return u.evaluate('return DeadSurvivor.Editor.DSMapPlayableVerification.'+name+'('+','.join(json.dumps(x) for x in args)+');')

def run(stage, queue_tests=False):
    uid,nonce=uuid.uuid4().hex,uuid.uuid4().hex
    out=b.BASE/('playable-'+str(stage)+'-'+uid);out.mkdir(parents=True)
    u=b.Unity(out);report=dict(status='BLOCKED',stage=stage,scope='ISOLATED_DEVELOPMENT_EXPERIMENT',queue_tests=queue_tests,time=datetime.now(ZoneInfo('Asia/Seoul')).isoformat(),g2='NOT_RUN',g3='NOT_RUN',g4='NOT_RUN')
    captures=[];snapshot=None;before=None;pid=None;sources=m.source_hashes();pending=m.BASE/'unity-pending.json'
    def capture(label,camera):
        rel=f'Assets/MigrationVerification/MapPlay-{uid}-{label}.png';native=b.PROJECT/rel
        if native.exists() or Path(str(native)+'.meta').exists():raise RuntimeError('CAPTURE_EXISTS')
        item=dict(path=rel,phase='PLANNED',run_id=uid,nonce=nonce);captures.append(item);m.write(out/'owned-captures.json',captures)
        result=u.cli(['command','capture','game_view','--caller','plugin','--skill','ds-workflow','--','--source','screen','--width',str(camera['width']),'--height',str(camera['height']),'--save_path',rel.removeprefix('Assets/')])['data']['result']
        if (b.PROJECT/result['savedPath']).resolve()!=native.resolve():raise RuntimeError('CAPTURE_PATH')
        u.evaluate('UnityEditor.AssetDatabase.ImportAsset('+json.dumps(rel)+',UnityEditor.ImportAssetOptions.ForceSynchronousImport);return true;')
        item.update(phase='CONFIRMED',sha256=m.digest(native),meta_sha256=m.digest(str(native)+'.meta'));m.write(out/'owned-captures.json',captures)
        shutil.copy2(native,out/(label+'.png'));sizes=b.capture_dimensions(camera,result,native.read_bytes())
        after=json.loads(invoke(u,'InspectCalibration' if stage==21 else 'GameEvidence'))['camera']
        if after!=camera:raise RuntimeError('CAMERA_CHANGED_DURING_CAPTURE')
        m.write(out/(label+'.capture.json'),dict(**sizes,camera_before=camera,camera_after=after,png_sha256=item['sha256']))
    with m.editor_lock():
        if pending.exists():raise RuntimeError('UNFINISHED_OWNED_SESSION')
        try:
            u.ready();console_before=u.cli(['command','console','get','--caller','plugin','--skill','ds-workflow','--','--tail','1','--level','error'])['data']['result'];m.write(out/'console-before.json',console_before);snapshot=u.session('Snapshot');pid=u.session('Ownership')['pid'];m.write(out/'snapshot.json',snapshot)
            before={n:m.tree_hashes(b.PROJECT/n) for n in ('Assets','Packages','ProjectSettings')};m.write(out/'target-before.json',before)
            m.write(pending,dict(run_id=uid,nonce=nonce,snapshot=str(out/'snapshot.json'),pid=__import__('os').getpid(),scope=report['scope']))
            u.session('Prepare',uid,nonce);u.session('StartOwned',uid,nonce);u.ready()
            rows=[]
            if stage==21:
                report["visibility"]=invoke(u,"VisibilityTests",uid,nonce)
                for scale in (4,6,8):
                    for angle in (45,55):
                        raw=json.loads(invoke(u,'Calibrate',uid,nonce,scale,angle));time.sleep(.15)
                        row=json.loads(invoke(u,'InspectCalibration'));cam=row['camera'];label=f's{scale}-a{angle}'
                        assert row['frame']>raw['frame'],'NO_FRAME_PROGRESS'
                        assert row['scale']==scale and cam['elevation']==angle and row['blockers']>=64
                        row['body_height_px']=[abs(a['y']-z['y']) for a,z in zip(cam['heads'],cam['feet'])]
                        assert min(row['body_height_px'])>=40,'ACTOR_TOO_SMALL'
                        assert all(0<p['x']<cam['width'] and 0<p['y']<cam['height'] for p in cam['heads']+cam['feet']),'PARTY_OUTSIDE'
                        capture(label,cam);rows.append(row)
                report.update(status='PASS',views=rows)
            else:
                raw=json.loads(invoke(u,'BeginGame',uid,nonce,stage));m.write(out/'started.json',raw)
                tests=json.loads(invoke(u,'QueueTests' if queue_tests else 'PartyTests' if stage==22 else 'ArtChecks' if stage in (28,29) else 'CombatTests'));m.write(out/'units.json',tests)
                assert tests['status']=='PASS'
                if queue_tests:
                    failure_proofs=[]
                    for exhaustion in (False,True):
                        failure_proofs.append(invoke(u,'QueueFailureTests',exhaustion))
                        invoke(u,'BeginGame',uid,nonce,stage)
                    m.write(out/'queue-failure-proofs.json',failure_proofs)
                    if failure_proofs!=['PASS_PARTIAL_EXCEPTION_EVIDENCE_AND_TERMINAL','PASS_CAPACITY_BEFORE_MUTATION_AND_TERMINAL']:raise ValueError('QUEUE_FAILURE_PROBES')
                if stage==23:
                    from fractions import Fraction
                    from map_oracle import Geometry,assess_sweep
                    fixture=Geometry.from_polygons([[-500,-500],[500,-500],[500,500],[-500,500]],[[[36,-100],[44,-100],[44,100],[36,100]]])
                    proofs=[]
                    for index,path in enumerate(tests['paths']):
                        previous=b.xy(path['start']);count=0
                        for point in path['points']:
                            result=assess_sweep(fixture,previous,b.xy(point),path['radius'],.001)
                            if result['status']!='PASS' or Fraction(result['exact_gap_interval'][0])<=Fraction(.002):raise AssertionError('INDEPENDENT_PATH_UNSAFE:'+str(index)+':'+str(count))
                            previous=b.xy(point);count+=1
                        proofs.append(dict(path=index,radius=path['radius'],segments=count,status='PASS'))
                    m.write(out/'independent-path-oracle.json',dict(status='PASS',paths=proofs,segments=sum(p['segments'] for p in proofs)))
                if stage in (28,29):
                    initial=json.loads(invoke(u,'ArtInitialState'));m.write(out/'initial-state.json',initial)
                    assert initial['elapsed']==0 and initial['selected']==[0,1,2,3] and initial['state']=='battle','ART_INITIAL_STATE'
                    draft=json.loads(invoke(u,'GameEvidence'));capture('warmup-discard',draft['camera']);capture('fixed-start-draft',draft['camera']);m.write(out/'fixed-start-draft.json',draft)
                    invoke(u,'DriveArt',True)
                first=json.loads(invoke(u,'GameEvidence'));time.sleep(.35);last=json.loads(invoke(u,'GameEvidence'))
                m.write(out/'frame-progress.json',dict(before=first,after=last))
                if first.get('inputScopeError') or last.get('inputScopeError'):raise RuntimeError('EXTERNAL_INPUT_CANCELLED:'+str(last.get('inputScopeError')))
                assert last['frame']>first['frame'] and last['elapsed']>first['elapsed'],'GAME_FROZEN'
                if (stage==22 and not queue_tests) or stage in (28,29):assert last['center']['x']>first['center']['x'],'MAP_INPUT_NO_PROGRESS'
                if stage in (28,29):
                    invoke(u,'DriveArt',False);time.sleep(.15);stopped=json.loads(invoke(u,'GameEvidence'));time.sleep(.20);held=json.loads(invoke(u,'GameEvidence'))
                    m.write(out/'stop-progress.json',dict(before=stopped,after=held))
                    assert held['frame']>stopped['frame'] and held['elapsed']>stopped['elapsed'] and held['center']==stopped['center'] and not held['updateInputD'] and held['updateInput']==dict(x=0.0,y=0.0),'ART_STOP_FAILED'
                    combat=[];deadline=time.monotonic()+55
                    while time.monotonic()<deadline:
                        e=json.loads(invoke(u,'GameEvidence'));combat.append(e);m.write(out/'representative-combat.json',combat)
                        if e['elapsed']>=5 and max(r['enemies'] for r in combat)>0 and any(r['shots']>0 or r['effects']>0 or r['kills']>0 for r in combat):break
                        if e['state']!='battle' or e['elapsed']>=12:break
                        time.sleep(.25)
                    m.write(out/'representative-combat.json',combat)
                    assert combat[-1]['elapsed']>=5 and combat[-1]['elapsed']>held['elapsed'] and max(r['enemies'] for r in combat)>0 and any(r['shots']>0 or r['effects']>0 or r['kills']>0 for r in combat),'ART_COMBAT_NOT_OBSERVED'

                invoke(u,'FreezeGame');e=json.loads(invoke(u,'GameEvidence'));capture('game',e['camera']);m.write(out/'game.json',e)
                report.update(status='PASS',units=tests,game=e)
            logs=u.cli(['command','console','get','--caller','plugin','--skill','ds-workflow','--','--tail','100','--level','error'])['data']['result'];m.write(out/'console.json',logs)
            if logs.get('session')!=console_before.get('session') or logs.get('dropped'):raise RuntimeError('CONSOLE_CURSOR_LOST')
            if any(x['seq']>console_before['cursor'] for x in logs.get('entries',[])) or logs.get('groundTruth',{}).get('consoleErrors',0):raise AssertionError('CONSOLE_ERRORS')
        except Exception as e:report.update(status='FAIL' if isinstance(e,(AssertionError,ValueError)) or 'PARTY_TEST:' in str(e) or 'COMBAT_TEST:' in str(e) or 'ART_TEST:' in str(e) else 'BLOCKED',error=str(e))
        finally:
            try:
                if pending.exists():m.cleanup_owned(u.session,u.ready,uid,nonce,str(out/'snapshot.json'),snapshot,pid)
                if before is not None:
                    release=lambda:m.finish_owned_files(b.PROJECT,captures,before,lambda:sources==m.source_hashes(),pending if pending.exists() else None,lambda rel:u.evaluate('return UnityEditor.AssetDatabase.DeleteAsset('+json.dumps(rel)+');'))
                    if stage!=21:
                        report['input_scopes'],after=verify_then_release(b.PROJECT,uid,nonce,release,required=report['status']=='PASS',expected_count=(3 if queue_tests else 1) if report['status']=='PASS' else None)
                    else:after=release()
                    m.write(out/'target-after.json',after)
                report['cleanup']='PASS'
            except Exception as e:report.update(status='BLOCKED',cleanup='BLOCKED',cleanup_error=str(e))
            report['sources']=sources;report['finished_at']=datetime.now(ZoneInfo('Asia/Seoul')).isoformat();report['artifacts']={str(p.relative_to(out)):m.digest(p) for p in out.rglob('*') if p.is_file() and p.name!='summary.json'};m.write(out/'summary.json',report)
    print(json.dumps(dict(status=report['status'],report=str(out/'summary.json'),error=report.get('error'),cleanup=report.get('cleanup'))));return 0 if report['status']=='PASS' else 1
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('stage',type=int,choices=(21,22,23,28,29));p.add_argument('--queue-tests',action='store_true');args=p.parse_args();raise SystemExit(run(args.stage,args.queue_tests))

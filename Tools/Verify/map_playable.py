#!/usr/bin/env python3
"""Owned Unity CLI checks for isolated 021–023 map experiments."""
from pathlib import Path
import argparse,json,shutil,sys,time,uuid
from datetime import datetime
from zoneinfo import ZoneInfo
import map_blockout as b
m=b.motion

def invoke(u,name,*args):
    return u.evaluate('return DeadSurvivor.Editor.DSMapPlayableVerification.'+name+'('+','.join(json.dumps(x) for x in args)+');')

def run(stage):
    uid,nonce=uuid.uuid4().hex,uuid.uuid4().hex
    out=b.BASE/('playable-'+str(stage)+'-'+uid);out.mkdir(parents=True)
    u=b.Unity(out);report=dict(status='BLOCKED',stage=stage,scope='ISOLATED_DEVELOPMENT_EXPERIMENT',time=datetime.now(ZoneInfo('Asia/Seoul')).isoformat(),g2='NOT_RUN',g3='NOT_RUN',g4='NOT_RUN')
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
                tests=json.loads(invoke(u,'PartyTests' if stage==22 else 'CombatTests'));m.write(out/'units.json',tests)
                assert tests['status']=='PASS'
                first=json.loads(invoke(u,'GameEvidence'));time.sleep(.35);last=json.loads(invoke(u,'GameEvidence'))
                assert last['frame']>first['frame'] and last['elapsed']>first['elapsed'],'GAME_FROZEN'
                if stage==22:assert last['center']['x']>first['center']['x'],'MAP_INPUT_NO_PROGRESS'
                m.write(out/'frame-progress.json',dict(before=first,after=last))
                invoke(u,'FreezeGame');e=json.loads(invoke(u,'GameEvidence'));capture('game',e['camera']);m.write(out/'game.json',e)
                report.update(status='PASS',units=tests,game=e)
            logs=u.cli(['command','console','get','--caller','plugin','--skill','ds-workflow','--','--tail','100','--level','error'])['data']['result'];m.write(out/'console.json',logs)
            if logs.get('session')!=console_before.get('session') or logs.get('dropped'):raise RuntimeError('CONSOLE_CURSOR_LOST')
            if any(x['seq']>console_before['cursor'] for x in logs.get('entries',[])) or logs.get('groundTruth',{}).get('consoleErrors',0):raise AssertionError('CONSOLE_ERRORS')
        except Exception as e:report.update(status='FAIL' if isinstance(e,(AssertionError,ValueError)) else 'BLOCKED',error=str(e))
        finally:
            try:
                if pending.exists():m.cleanup_owned(u.session,u.ready,uid,nonce,str(out/'snapshot.json'),snapshot,pid)
                if before is not None:
                    after=m.finish_owned_files(b.PROJECT,captures,before,lambda:sources==m.source_hashes(),pending if pending.exists() else None,lambda rel:u.evaluate('return UnityEditor.AssetDatabase.DeleteAsset('+json.dumps(rel)+');'))
                    m.write(out/'target-after.json',after)
                report['cleanup']='PASS'
            except Exception as e:report.update(status='BLOCKED',cleanup='BLOCKED',cleanup_error=str(e))
            report['sources']=sources;report['finished_at']=datetime.now(ZoneInfo('Asia/Seoul')).isoformat();report['artifacts']={str(p.relative_to(out)):m.digest(p) for p in out.rglob('*') if p.is_file() and p.name!='summary.json'};m.write(out/'summary.json',report)
    print(json.dumps(dict(status=report['status'],report=str(out/'summary.json'),error=report.get('error'),cleanup=report.get('cleanup'))));return 0 if report['status']=='PASS' else 1
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('stage',type=int,choices=(21,22,23));raise SystemExit(run(p.parse_args().stage))

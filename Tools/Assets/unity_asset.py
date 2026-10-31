"""Import and inspect one processed rock in the pinned personal Unity Editor."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
import pipeline as p

PROJECT = Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor')


def check_dependencies(hashes, root=PROJECT):
    if not isinstance(hashes,dict) or not hashes:
        raise p.AssetFailure('MISSING_DEPENDENCY_HASHES')
    for rel,value in hashes.items():
        item=p.child(root,rel)
        if not item.is_file() or p.digest(item)!=value:
            raise p.AssetFailure('MEASURED_ASSET_CHANGED: '+rel)


def snapshot_tree(folder):
    result = {}
    for item in sorted(folder.rglob('*')):
        if item.name == '.DS_Store':
            continue
        if item.is_symlink():
            raise p.Blocked('Symlink in Unity target')
        if item.is_file():
            result[str(item.relative_to(folder))] = p.digest(item)
    return result


def finish_report(report, out, checks):
    """Preserve each final audit failure even after Editor state was restored."""
    for name, check in checks:
        try:
            check()
            report['results'].setdefault(name, {'status':'PASS'})
        except (Exception, KeyboardInterrupt) as error:
            status='BLOCKED' if isinstance(error,(p.Blocked,OSError,subprocess.TimeoutExpired,KeyboardInterrupt)) else 'FAIL'
            report['results'][name]={'status':status,'error':str(error)}
            if report['status']!='FAIL': report['status']=status
    report['finished_at']=p.now()
    try:
        report['artifacts']={x.name:p.digest(x) for x in out.iterdir() if x.is_file() and x.name!='summary.json'}
    except OSError as error:
        report['results']['artifact_hashes']={'status':'BLOCKED','error':str(error)}
        if report['status']!='FAIL': report['status']='BLOCKED'
    p.atomic(out/'summary.json',report)


def run(job):
    folder = p.job_dir(job)
    with p.lock(p.BASE), p.lock(folder):
        marker = p.child(p.BASE, 'unity-pending.json')
        if marker.exists():
            raise p.Blocked('UNFINISHED_UNITY_RUN: inspect ownership and restore before retry')
        state, spec = p.verify_job(folder)
        unity_tools = p.tools_hashes()
        if state['phase'] != 'PROCESSED':
            raise p.Blocked('Process the asset first')
        uid = uuid.uuid4().hex
        out = p.child(folder, 'unity-' + uid); out.mkdir()
        env = os.environ.copy()
        env.update(UNITY_NO_CLI_INVOKED_TELEMETRY='1', UNITY_NO_CRASH_REPORT='1', UNITY_NO_CONSENT_PROMPT='1')
        counter = 0
        def call(name, **kwargs):
            nonlocal counter
            counter += 1
            args = ['unity', 'command', *name.split(), '--project-path', str(PROJECT),
                    '--caller', 'plugin', '--skill', 'ds-workflow', '--format', 'json']
            if kwargs:
                args += ['--']
                for k,v in kwargs.items():
                    args += ['--'+k, v if isinstance(v,str) else json.dumps(v)]
            proc = subprocess.run(args, env=env, capture_output=True, text=True, timeout=90)
            label = f'{counter:02d}-' + name.replace(' ', '-')
            (out/(label+'.json')).write_text(proc.stdout)
            (out/(label+'.stderr')).write_text(proc.stderr)
            result = json.loads(proc.stdout)
            if proc.returncode or result.get('success') is not True:
                raise p.Blocked('UNITY_COMMAND_FAILED: '+name+'; see '+label)
            value = result['data']['result']
            if isinstance(value,dict) and value.get('success') is False:
                raise p.Blocked('UNITY_SCRIPT_FAILED: '+label)
            return value
        def script(method, *args):
            return call('script run', file=str(p.TOOLS/'UnityAsset.cs'), entry='UnityAsset.'+method, args=list(args))['result']
        def evaluate(code):
            return call('script eval', code=code)['result']
        def ready():
            result = subprocess.run(['unity','status','--until-ready','--project-path',str(PROJECT),'--format','json'], env=env,capture_output=True,text=True,timeout=60)
            if result.returncode:
                raise p.Blocked('Unity did not become ready')
        ready()
        before = script('Snapshot')
        if before['playing'] or before['dirty'] or before['disabledReload'] or before['dirtyAssets']:
            raise p.Blocked('User Play, unsaved scene/asset, or disabled reload')
        config=p.load(PROJECT/'ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json')
        if config.get('m_RunInBackgroundCommandOverride') or config.get('m_RunInBackgroundInPlayMode'):
            raise p.Blocked('Existing background override; preserve user settings')
        if any(not s['path'] for s in before['scenes']):
            raise p.Blocked('Unsaved scene path')
        p.atomic(out/'before.json', before)
        target_before = {k:snapshot_tree(PROJECT/k) for k in ('Assets/DeadSurvivor','ProjectSettings','Packages')}
        p.atomic(out/'target-before.json', target_before)
        for section, files in target_before.items():
            for relative in files:
                backup=p.child(out,'target-backup/'+section+'/'+relative)
                backup.parent.mkdir(parents=True,exist_ok=True)
                shutil.copyfile(p.child(PROJECT,section+'/'+relative),backup)
                if p.digest(backup)!=files[relative]:
                    raise p.Blocked('Target changed while taking backup')
        destination = 'Assets/DeadSurvivor/Art/Tests/Asset012_' + uid
        scene = destination + '/Preview.unity'
        request = {'source':str(p.child(folder,state['processed'])), 'destination':destination,
                   'run_id':uid,'texture_size':spec['limits']['texture_size']}
        p.atomic(out/'request.json', request)
        journal = {'run_id':uid,'project':str(PROJECT),'pid':before['pid'],'before':before,
                   'output':str(out.relative_to(p.ROOT)),'scene':scene,'complete':False,'phase':'BEFORE_MUTATION'}
        p.atomic(marker,journal)
        attempted_play = False
        candidate_files = None
        dependency_hashes = None
        report = {'status':'BLOCKED','scope':'TRANSFER_TEST; not production acceptance',
                  'run_id':uid,'started_at':p.now(),'source_job':state['run_id'],
                  'unity_tools':unity_tools,'results':{}}
        try:
            owner = evaluate('return UnityEditor.SessionState.GetString("DS.Asset012.Owner", "");')
            if owner:
                raise p.Blocked('Unity session already owned: '+owner)
            evaluate('UnityEditor.SessionState.SetString("DS.Asset012.Owner", '+json.dumps(uid)+');')
            before_console = call('console get',tail=50,level='error'); p.atomic(out/'console-before.json',before_console)
            original = script('Measure','Assets/DeadSurvivor/Art/Tests/TripoRock21300150/TripoRock_TestOnly.prefab')
            p.atomic(out/'original-measurement.json',original)
            report['results']['original_contract'] = p.judge(original,spec['limits'])
            measurement = script('Build',str(out/'request.json'))
            dependency_hashes=measurement['dependency_hashes']
            check_dependencies(dependency_hashes)
            candidate_files=snapshot_tree(p.child(PROJECT,destination))
            p.atomic(out/'candidate-hashes.json',candidate_files)
            p.atomic(out/'measurement.json',measurement)
            contract = p.judge(measurement,spec['limits']);report['results']['contract']=contract
            # Validation does not silently repair a failing imported model.
            if contract['status'] != 'PASS':
                report['status']='FAIL'; raise ValueError('IMPORTED_CONTRACT_FAILED')
            journal['phase']='IMPORTED';p.atomic(marker,journal)
            call('editor set_auto_tick',enable=True,interval_ms=16,persist=False)
            call('editor.playmode set_run_in_background',enable=True)
            attempted_play=True;call('editor.playmode start');ready()
            frames=call('job wait',condition={'member':'UnityEngine.Time.frameCount','op':'changed'},timeout=10,return_history=True)
            if frames.get('met') is not True or frames.get('framesObserved',0)<=0:
                raise p.Blocked('PLAYER_NOT_ADVANCING')
            report['results']['frames']={'status':'PASS','observation':frames}
            capture=call('capture game_view',source='camera',camera='Asset012Camera',width=1024,height=768,
                         save_path='MigrationVerification/asset012-'+uid+'/preview.png')
            saved=p.child(PROJECT,capture['savedPath'])
            shutil.copyfile(saved,out/'unity-preview.png')
            report['results']['capture']={'status':'PASS','sha256':p.digest(out/'unity-preview.png'),
                'note':'Direct visual review is separate; actual camera after advancing frames'}
            console=call('console get',tail=50,level='error');p.atomic(out/'console-after.json',console)
            ground=console.get('groundTruth',{})
            if ground.get('compilationFailed') is not False or console.get('dropped') is not False or ground.get('consoleErrors') != 0:
                raise p.Blocked('CONSOLE_NOT_CLEAN; existing entries preserved')
            report['results']['console']={'status':'PASS','observation':console}
            report['visual_review_status']='NOT_RUN'
            report['status']='PASS'
        except (Exception,KeyboardInterrupt) as e:
            report['error']=str(e)
            if not isinstance(e,(p.Blocked,subprocess.TimeoutExpired,KeyboardInterrupt)):
                report['status']='FAIL'
        finally:
            errors=[]
            try:
                current=script('Ownership')
                if current['pid']!=before['pid'] or current['owner']!=uid:
                    raise p.Blocked('OWNERSHIP_LOST; manual recovery required')
                if current['playing']:
                    if not attempted_play or current['scene']!=scene:
                        raise p.Blocked('Unknown Play owner; not stopping')
                    call('editor.playmode stop');ready()
                call('editor.playmode set_run_in_background',enable=False)
                script('Restore',str(out/'before.json'),scene)
                call('editor set_auto_tick',enable=before['tickEnabled'],interval_ms=before['tickInterval'],persist=False)
                after=script('Snapshot');p.atomic(out/'after.json',after)
                if after!=before:
                    raise p.Blocked('RESTORE_MISMATCH')
                evaluate('UnityEditor.SessionState.EraseString("DS.Asset012.Owner");')
                journal['complete']=True;journal['phase']='RESTORED';p.atomic(marker,journal)
                os.replace(marker,out/'unity-journal.json')
            except Exception as e:
                errors.append(str(e))
            report['results']['cleanup']={'status':'FAIL' if errors else 'PASS','errors':errors}
            if errors:
                report['status']='FAIL'
            def measurement_stability():
                if candidate_files is None or snapshot_tree(p.child(PROJECT,destination)) != candidate_files:
                    raise p.AssetFailure('CANDIDATE_CHANGED_OR_INCOMPLETE')
                check_dependencies(dependency_hashes)
            def existing_files():
                after_files={k:snapshot_tree(PROJECT/k) for k in target_before}
                p.atomic(out/'target-after.json',after_files)
                changed=[]
                allowed=destination.removeprefix('Assets/DeadSurvivor/')
                for section, files in target_before.items():
                    for name in set(files)|set(after_files[section]):
                        if files.get(name)!=after_files[section].get(name):
                            if section!='Assets/DeadSurvivor' or not (name.startswith(allowed+'/') or name==allowed+'.meta'):
                                changed.append(section+'/'+name)
                report['results']['existing_files']={'status':'FAIL' if changed else 'PASS','unexpected_changes':changed}
                if changed: report['status']='FAIL'
            def unity_tool_stability():
                if p.tools_hashes()!=unity_tools:
                    raise p.Blocked('UNITY_TOOLS_CHANGED_DURING_RUN')
            finish_report(report,out,[('measurement_stability',measurement_stability),
                                      ('existing_files',existing_files),('source_stability',lambda:p.verify_job(folder)),
                                      ('unity_tool_stability',unity_tool_stability)])
        return out,report


if __name__=='__main__':
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--job',required=True);a=ap.parse_args()
    try:
        out,r=run(a.job);print(json.dumps({'status':r['status'],'report':str(out/'summary.json')},ensure_ascii=False))
        sys.exit(0 if r['status']=='PASS' else 1)
    except (ValueError,OSError,subprocess.TimeoutExpired) as e:
        print(json.dumps({'status':'BLOCKED','error':str(e)},ensure_ascii=False));sys.exit(2)

"""Pinned local Unity session checks. No network services or source syncing."""
import json
import shutil
from pathlib import Path
import subprocess
import time
import math

class UnityFailure(RuntimeError):
    pass

def run_unity(root, project, out, screens=False, experiments=False, loadouts=False):
    project=Path(project).resolve()
    expected=Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor')
    if project != expected:
        return {'status':'BLOCKED','error':'Project is not the approved personal Unity project'}
    out.mkdir(parents=True,exist_ok=True)
    results={}; counter=0; attempted=False; background=None; screen_cleanup_errors=[]
    def command(name, **kwargs):
        nonlocal counter
        args=['unity','command',*name.split(),'--project-path',str(project),'--caller','plugin','--skill','ds-workflow','--format','json']
        if kwargs:
            args+=['--']
            for k,v in kwargs.items():
                args+=['--'+k,json.dumps(v) if not isinstance(v,str) else v]
        counter+=1
        proc=subprocess.run(args,capture_output=True,text=True,timeout=60)
        (out/f'{counter:02d}-{name.replace(" ","-")}.json').write_text(proc.stdout+'\n'+proc.stderr)
        try: blob=json.loads(proc.stdout)
        except ValueError: raise UnityFailure(f'{name}: invalid response (exit {proc.returncode})')
        if proc.returncode or not blob.get('success'): raise UnityFailure(f'{name}: {blob.get("errors")}')
        value=blob['data']['result']
        if isinstance(value,dict) and value.get('success') is False: raise UnityFailure(f'{name}: {value}')
        return value
    def evaluate(code):
        return command('script eval',code=code)['result']
    def ready():
        p=subprocess.run(['unity','status','--until-ready','--project-path',str(project),'--format','json'],capture_output=True,text=True,timeout=60)
        if p.returncode: raise UnityFailure('Editor did not become ready: '+p.stdout)
    try:
        ready()
        status=command('editor status')
        if status['playMode']!='stopped': return {'status':'BLOCKED','error':'User play session is active'}
        check=evaluate('bool dirty=false;for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)dirty|=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty;return dirty || (UnityEditor.EditorSettings.enterPlayModeOptionsEnabled && UnityEditor.EditorSettings.enterPlayModeOptions != UnityEditor.EnterPlayModeOptions.None);')
        if check: return {'status':'BLOCKED','error':'Unsaved scene or disabled reload; no settings changed'}
        scene=evaluate('return UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;')
        if scene!='Assets/DeadSurvivor/Scenes/DeadSurvivor.unity':return {'status':'BLOCKED','error':'Expected gameplay scene is not active'}
        for folder in ['Runtime','Editor']:
            source_names={p.relative_to(root/'UnityMigration'/folder) for p in (root/'UnityMigration'/folder).rglob('*.cs')}
            target_names={p.relative_to(project/'Assets/DeadSurvivor'/folder) for p in (project/'Assets/DeadSurvivor'/folder).rglob('*.cs')}
            if source_names!=target_names:raise UnityFailure('Source file sets differ: '+folder)
            for relative in sorted(source_names):
                p=root/'UnityMigration'/folder/relative
                q=project/'Assets/DeadSurvivor'/folder/relative
                if not q.exists() or p.read_bytes()!=q.read_bytes():raise UnityFailure('Unsynced source: '+str(p))
        data=root/'UnityMigration/Data/PrototypeData.json'
        if data.read_bytes()!=(project/'Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json').read_bytes():raise UnityFailure('Unsynced data')
        compile_run=subprocess.run(['unity','recompile','--project-path',str(project),'--format','json'],capture_output=True,text=True,timeout=120)
        (out/'compile.json').write_text(compile_run.stdout+'\n'+compile_run.stderr)
        if compile_run.returncode:raise UnityFailure('Compile failed; see compile.json')
        ready();results['compile']={'status':'PASS'}
        command('console get',tail=100,level='error')
        command('console clear')
        background=evaluate('return UnityEngine.Application.runInBackground;')
        attempted=True
        command('editor.playmode start');ready()
        if command('editor status')['playMode']!='playing':raise UnityFailure('Play mode did not start')
        evaluate('UnityEngine.Application.runInBackground=true;')
        before=evaluate('return UnityEngine.Time.frameCount;')
        time.sleep(.3)
        after=evaluate('return UnityEngine.Time.frameCount;')
        if after<=before:raise UnityFailure('Player frame count did not advance')
        results['frames']={'status':'PASS','before':before,'after':after}
        load_time=evaluate('return UnityEngine.Object.FindAnyObjectByType<DeadSurvivor.DSGame>().InitialLoadTime;')
        results['initial_load']={'status':'PASS' if isinstance(load_time,(float,int)) and math.isfinite(load_time) and load_time>=0 else 'FAIL','seconds':load_time,'scope':'Awake to first completed UI frame, Editor only'}
        rules=evaluate('return DeadSurvivor.Editor.DSVerification.Run();')
        if 'checks passed' not in rules or 'FAILED' in rules:raise UnityFailure('Rules did not pass')
        (out/'rules.txt').write_text(rules);results['rules']={'status':'PASS','summary':rules.splitlines()[0]}
        if experiments:
            experiment_results=[]
            for seed in [17,29,43]:
                for formation in range(3):
                    value=json.loads(evaluate(f'return DeadSurvivor.Editor.DSExperiments.Compare({seed},{formation});'))
                    if not value.get('repeatMatches'):raise UnityFailure('Experiment repeat failed')
                    experiment_results.append(value)
            (out/'experiments.json').write_text(json.dumps(experiment_results,ensure_ascii=False,indent=2))
            results['experiments']={'status':'PASS','runs':len(experiment_results)*2,'note':'Fixed automatic policy, not human balance validation'}
        if loadouts:
            loadout_results=[]
            items=json.loads(data.read_text())['items']
            for seed in [17,29,43]:
                for item in items:
                    code=f'return DeadSurvivor.Editor.DSExperiments.CompareLoadout({seed},{json.dumps(item["id"])});'
                    loadout_results.append(json.loads(evaluate(code)))
                    (out/'loadouts-progress.json').write_text(json.dumps({'completed_pairs':len(loadout_results),'total_pairs':len(items)*3}))
            (out/'loadouts.json').write_text(json.dumps(loadout_results,ensure_ascii=False,indent=2))
            results['loadouts']={'status':'PASS','pairs':len(loadout_results),'runs':len(loadout_results)*2,'note':'Paired initial loadout pilots, no reward equip or human decisions'}
        interaction=out/'interaction.txt'
        if interaction.exists():raise UnityFailure('Interaction output must be new')
        command('script run',file=str(root/'UnityMigration/InteractionCheck.cs'),entry='InteractionCheck.QueueTo',args=[str(interaction)])
        deadline=time.monotonic()+20
        while not interaction.exists() and time.monotonic()<deadline:time.sleep(.2)
        if not interaction.exists():raise UnityFailure('Interaction completion timed out')
        result=interaction.read_text()
        if not result.startswith('PASS ') or 'FAILED' in result:raise UnityFailure(result)
        results['interaction']={'status':'PASS','summary':result}
        evaluate('var g=UnityEngine.Object.FindAnyObjectByType<DeadSurvivor.DSGame>();g.Selected=new System.Collections.Generic.List<int>{0,2,4,5};g.StartRun();g.FinishWave(true);return g.State;')
        capture_path=Path('MigrationVerification')/out.parent.name/'inventory.png'
        capture=command('capture game_view',source='screen',save_path=str(capture_path))
        saved=(project/capture['savedPath']).resolve()
        if not saved.is_relative_to(project):raise UnityFailure('Capture outside personal project')
        shutil.copy2(saved,out/'inventory.png')
        results['capture']={'status':'PASS','note':'Capture generated; visual review is separate'}
        evaluate('var g=UnityEngine.Object.FindAnyObjectByType<DeadSurvivor.DSGame>();g.Team[0].hp=0;g.Team[3].hp=0;g.UI.Prep();return g.State;')
        time.sleep(.2)
        death=command('capture game_view',source='screen',save_path=str(Path('MigrationVerification')/out.parent.name/'revive.png'))
        death_path=(project/death['savedPath']).resolve()
        if not death_path.is_relative_to(project):raise UnityFailure('Capture outside personal project')
        shutil.copy2(death_path,out/'revive.png')
        evaluate('var g=UnityEngine.Object.FindAnyObjectByType<DeadSurvivor.DSGame>();g.Revive(0);g.Revive(3);return g.State;')

        evaluate('var g=UnityEngine.Object.FindAnyObjectByType<DeadSurvivor.DSGame>();g.Stash.Clear();var i=g.MakeItem("amulet");g.Stash.Add(i);g.UI.Prep();UnityEngine.Canvas.ForceUpdateCanvases();DeadSurvivor.DSPointer source=null,target=null;foreach(var p in UnityEngine.Object.FindObjectsByType<DeadSurvivor.DSPointer>()){if(!p.gameObject.activeInHierarchy)continue;if(p.item==i.id)source=p;if(p.character==2&&p.x==4&&p.y==4)target=p;}g.UI.ClickTarget(source);g.UI.HoverTarget(target);return g.UI.Held;')
        comparison=command('capture game_view',source='screen',save_path=str(Path('MigrationVerification')/out.parent.name/'comparison.png'))
        saved_comparison=(project/comparison['savedPath']).resolve()
        if not saved_comparison.is_relative_to(project):raise UnityFailure('Comparison capture outside personal project')
        shutil.copy2(saved_comparison,out/'comparison.png')
        if screens:
            screen_results=[]
            try:
                for width,height in [(1280,720),(1560,720),(1024,768)]:
                    evaluate(f'return DeadSurvivor.Editor.DSScreenVerification.Resize({width},{height});')
                    time.sleep(.3)
                    matrix_input=out/f'interaction-{width}x{height}.txt'
                    command('script run',file=str(root/'UnityMigration/InteractionCheck.cs'),entry='InteractionCheck.QueueTo',args=[str(matrix_input)])
                    deadline=time.monotonic()+20
                    while not matrix_input.exists() and time.monotonic()<deadline:time.sleep(.2)
                    if not matrix_input.exists() or not matrix_input.read_text().startswith('PASS ') or 'FAILED' in matrix_input.read_text():raise UnityFailure('Resolution input failed '+str(matrix_input))
                    screen_results.append(f'PASS input {width}x{height}')
                    evaluate('var g=UnityEngine.Object.FindAnyObjectByType<DeadSurvivor.DSGame>();g.Selected=new System.Collections.Generic.List<int>{0,2,4,5};g.StartRun();g.FinishWave(true);return g.State;')
                    for state,code in [
                        ('prep','g.UI.Prep();'),
                        ('description','g.UI.Prep();var gear=g.MakeItem("censer");g.Stash.Add(gear);g.UI.Prep();foreach(var p in UnityEngine.Object.FindObjectsByType<DeadSurvivor.DSPointer>())if(p.item==gear.id&&p.gameObject.activeInHierarchy){g.UI.ClickTarget(p);break;}'),
                        ('comparison','g.UI.Prep();g.Stash.Clear();var gear=g.MakeItem("amulet");g.Stash.Add(gear);g.UI.Prep();DeadSurvivor.DSPointer source=null,target=null;foreach(var p in UnityEngine.Object.FindObjectsByType<DeadSurvivor.DSPointer>()){if(!p.gameObject.activeInHierarchy)continue;if(p.item==gear.id)source=p;if(p.character==2&&p.x==4&&p.y==4)target=p;}g.UI.ClickTarget(source);g.UI.HoverTarget(target);foreach(var b in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>())if(b.gameObject.activeInHierarchy&&b.name=="상세 보기"){b.onClick.Invoke();break;}'),
                        ('safearea',f'g.UI.SafeAreaPreview=new UnityEngine.Rect(60,24,{width}-120,{height}-48);g.UI.Prep();'),
                        ('settings','g.UI.SafeAreaPreview=null;g.UI.Settings();'),
                        ('result','g.ReturnLobby();g.Result(false);'),
                        ('battle-items','DeadSurvivor.Editor.DSBattleDisplayVerification.PrepareCapture();'),
                        ('battle-full','DeadSurvivor.Editor.DSBattleDisplayVerification.PrepareCapture(true);')]:
                        evaluate('var g=UnityEngine.Object.FindAnyObjectByType<DeadSurvivor.DSGame>();'+code+'return g.State;')
                        time.sleep(.15)
                        if state.startswith("battle-"):
                            screen_results.append(evaluate("return DeadSurvivor.Editor.DSBattleDisplayVerification.CheckBottomInput();"))
                        screen_results.append(evaluate(f'return DeadSurvivor.Editor.DSScreenVerification.Check({width},{height});'))
                        name=f'{state}-{width}x{height}.png'
                        cap=command('capture game_view',source='screen',width=width,height=height,save_path=str(Path('MigrationVerification')/out.parent.name/name))
                        saved=(project/cap['savedPath']).resolve()
                        if not saved.is_relative_to(project):raise UnityFailure('Screen capture outside personal project')
                        shutil.copy2(saved,out/name)
                    evaluate('var g=UnityEngine.Object.FindAnyObjectByType<DeadSurvivor.DSGame>();g.Selected=new System.Collections.Generic.List<int>{0,2,4,5};g.StartRun();g.FinishWave(true);return g.State;')
                results['screens']={'status':'PASS','checks':screen_results,'note':'Actual GameView sizes; visual review separate, physical device NOT_RUN'}
            finally:
                try:evaluate('return DeadSurvivor.Editor.DSScreenVerification.Restore();')
                except Exception as e:screen_cleanup_errors.append('screen restore: '+str(e))
        logs=command('console get',tail=100,level='error')
        results['console']={'status':'FAIL' if logs.get('entries') or logs.get('groundTruth',{}).get('compilationFailed') or logs.get('groundTruth',{}).get('consoleErrors',0)>0 else 'PASS','response':logs}
    except (UnityFailure,subprocess.TimeoutExpired,OSError,KeyError,ValueError) as e:
        results['execution']={'status':'FAIL','error':str(e)}
    finally:
        cleanup_errors=list(screen_cleanup_errors)
        if attempted:
            try:
                ready()
                state=command('editor status')['playMode']
                if state=='playing':
                    if background is not None:evaluate('UnityEngine.Application.runInBackground='+str(background).lower()+';')
                    command('editor.playmode stop')
                elif state!='stopped':raise UnityFailure('Unsettled Play mode during cleanup: '+str(state))
                ready()
                if command('editor status')['playMode']!='stopped':raise UnityFailure('Play mode did not stop')
            except Exception as e:cleanup_errors.append('stop: '+str(e))
        if background is not None:
            try:
                if evaluate('return UnityEngine.Application.runInBackground;')!=background:raise UnityFailure('Background setting was not restored')
            except Exception as e:cleanup_errors.append('background: '+str(e))
        results['cleanup']={'status':'FAIL' if cleanup_errors else 'PASS','errors':cleanup_errors}
    status='FAIL' if any(x['status']=='FAIL' for x in results.values()) else 'BLOCKED' if any(x['status']!='PASS' for x in results.values()) else 'PASS'
    return {'status':status,'results':results}

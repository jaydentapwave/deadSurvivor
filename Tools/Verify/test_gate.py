"""Gate regression tests. Fixtures only; no real Unity, user commits, or trust writes."""
import copy
import datetime
import importlib
import io
import json
from pathlib import Path
import shutil
import re
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import gate
import gate_support as support


class GateTests(unittest.TestCase):
    def test_stop_failure_stays_blocked(self):
        for status in ('FAIL', 'BLOCKED', 'NOT_RUN'):
            self.assertEqual(gate.stop_response({'status':status, 'error':'test'},Path('report'))['decision'],'block')
        self.assertNotIn('decision',gate.stop_response({'status':'PASS'},Path('report')))

    def test_bad_stop_input_blocks(self):
        for payload in ('invalid', json.dumps({'hook_event_name':'Stop','cwd':'/private/tmp'}), json.dumps({'hook_event_name':'Other','cwd':str(gate.ROOT)})):
            output=io.StringIO()
            with patch.object(sys,'argv',['gate.py','stop']),patch.object(sys,'stdin',io.StringIO(payload)),patch('sys.stdout',output):
                self.assertEqual(gate.main(),0)
            self.assertEqual(json.loads(output.getvalue())['decision'],'block')

    def test_stop_output_error_blocks(self):
        output=io.StringIO();payload={'hook_event_name':'Stop','cwd':str(gate.ROOT),'stop_hook_active':True}
        with patch.object(sys,'argv',['gate.py','stop']),patch.object(sys,'stdin',io.StringIO(json.dumps(payload))),patch('sys.stdout',output),patch.object(gate,'execute',side_effect=PermissionError('cannot record')):
            gate.main()
        self.assertEqual(json.loads(output.getvalue())['decision'],'block')

    def test_index_add_delete_content_mode(self):
        base={'a':{'sha256':'a','mode':'100644'}}
        gate.require_index_match(base,copy.deepcopy(base))
        for changed in ({}, {'a':{'sha256':'b','mode':'100644'}}, {'a':{'sha256':'a','mode':'100755'}}, dict(base,b={'sha256':'b','mode':'100644'})):
            with self.assertRaises(ValueError):gate.require_index_match(base,changed)
        gate.require_index_match({'removed':{'missing':True}}, {})
        with self.assertRaises(ValueError):gate.require_index_match({'removed':{'missing':True}},base)

    def test_command_failure_timeout_empty(self):
        for process in (subprocess.CompletedProcess([],1,'','bad'),subprocess.CompletedProcess([],0,'{}',''),subprocess.CompletedProcess([],0,'not json','')):
            with patch.object(subprocess,'run',return_value=process),self.assertRaises(ValueError):
                gate.run_check(['fake'],'local',gate.ROOT)
        with patch.object(subprocess,'run',side_effect=subprocess.TimeoutExpired(['fake'],1)),self.assertRaises(subprocess.TimeoutExpired):
            gate.run_check(['fake'],'local',gate.ROOT)

    def test_empty_pass_report_rejected(self):
        with tempfile.TemporaryDirectory() as t:
            root=Path(t);out=root/'output/verification/test';out.mkdir(parents=True);f=out/'summary.json'
            for report in ({'status':'PASS','results':{}},{'status':'PASS','checks':[]}):
                if f.exists():f.unlink()
                def respond(*args,**kwargs):
                    f.write_text(json.dumps(report))
                    return subprocess.CompletedProcess([],0,json.dumps({'report':str(f),'status':'PASS'}),'')
                with patch.object(subprocess,'run',side_effect=respond),self.assertRaises(ValueError):
                    gate.run_check(['fake'],'local' if 'results' in report else 'map',root)

    def test_old_report_cannot_replace_new_check(self):
        with tempfile.TemporaryDirectory() as t:
            root=Path(t);out=root/'output/verification/old';out.mkdir(parents=True);f=out/'summary.json';f.write_text(json.dumps({'status':'PASS'}))
            proc=subprocess.CompletedProcess([],0,json.dumps({'report':str(f),'status':'PASS'}),'')
            with patch.object(subprocess,'run',return_value=proc),self.assertRaisesRegex(ValueError,'Old report'):
                gate.run_check(['fake'],'local',root)

    def test_execute_detects_midrun_unity_target_change(self):
        with tempfile.TemporaryDirectory() as t:
            root=Path(t);(root/'Tools/Verify').mkdir(parents=True);(root/'Tools/Verify/gate-policy.json').write_text(json.dumps({'version':1,'active_map':{'audit':'audit.py'}}))
            with patch.object(gate,'runtime_state',side_effect=[{'target':'before'},{'target':'after'}]),patch.object(gate,'validate_policy_paths'),patch.object(gate,'source_snapshot',return_value={}),patch.object(gate,'run_check',return_value={'status':'PASS'}),patch.object(gate,'active_map_check',return_value={'status':'PASS'}),patch.object(gate,'runtime_requirement',return_value={'status':'PASS'}):
                result,_=gate.execute(root,'check')
            self.assertEqual(result['status'],'FAIL');self.assertIn('target changed',result['error'])

    def test_execute_detects_midrun_source_change(self):
        with tempfile.TemporaryDirectory() as t:
            root=Path(t);(root/'Tools/Verify').mkdir(parents=True);(root/'Tools/Verify/gate-policy.json').write_text(json.dumps({'version':1,'active_map':{'audit':'audit.py'}}))
            with patch.object(gate,'validate_policy_paths'),patch.object(gate,'source_snapshot',side_effect=[{'x':1},{'x':2}]),patch.object(gate,'run_check',return_value={'status':'PASS'}),patch.object(gate,'active_map_check',return_value={'status':'PASS'}),patch.object(gate,'runtime_requirement',return_value={'status':'PASS'}):
                result,_=gate.execute(root,'check')
            self.assertEqual(result['status'],'FAIL');self.assertIn('Source changed',result['error'])

    def test_execute_detects_midrun_index_change(self):
        with tempfile.TemporaryDirectory() as t:
            root=Path(t);(root/'Tools/Verify').mkdir(parents=True);(root/'Tools/Verify/gate-policy.json').write_text(json.dumps({'version':1,'active_map':{'audit':'audit.py'}}))
            with patch.object(gate,'validate_policy_paths'),patch.object(gate,'source_snapshot',return_value={}),patch.object(gate,'index_snapshot',side_effect=[{}, {'new':1}]),patch.object(gate,'run_check',return_value={'status':'PASS'}),patch.object(gate,'active_map_check',return_value={'status':'PASS'}),patch.object(gate,'runtime_requirement',return_value={'status':'PASS'}):
                result,_=gate.execute(root,'pre-commit')
            self.assertEqual(result['status'],'FAIL');self.assertIn('index changed',result['error'])

    def test_runtime_no_old_or_art_bypass(self):
        old={'source':{},'art':{},'target':{}};new=copy.deepcopy(old);new['source']['changed']='hash'
        with tempfile.TemporaryDirectory() as t:
            root=Path(t);policy={'runtime_baseline':old}
            with patch.object(gate,'runtime_state',return_value=old):self.assertEqual(gate.runtime_requirement(root,policy)['status'],'PASS')
            with patch.object(gate,'runtime_state',return_value=new),patch.object(gate,'evidence_context',return_value={}),self.assertRaises(ValueError):gate.runtime_requirement(root,policy)
            new['art']['new']='hash'
            with patch.object(gate,'runtime_state',return_value=new),self.assertRaisesRegex(ValueError,'art adapter'):gate.runtime_requirement(root,policy)

    def test_unity_evidence_missing_stale_tampered(self):
        with tempfile.TemporaryDirectory() as t:
            root=Path(t);u=root/'unity';u.mkdir();context={'runtime':'fixture','verifiers':'fixture','project':str(support.PERSONAL_PROJECT)}
            files=['compile.json','rules.txt','inventory.png','revive.png','comparison.png','interaction.txt']
            for size in ('1280x720','1560x720','1024x768'):
                files += ['interaction-'+size+'.txt']+[state+'-'+size+'.png' for state in ('prep','description','comparison','safearea','settings','result','battle-items','battle-full')]
            for name in files:(u/name).write_bytes(b'fixture')
            steps={k:{'status':'PASS'} for k in ('compile','rules','frames','interaction','capture','screens','console','cleanup')};steps['screens']['checks']=['PASS fixture'];steps['frames'].update(before=1,after=2)
            report={'status':'PASS','stage':'all','options':{'screens':True,'project':str(support.PERSONAL_PROJECT)},'gate_before':context,'gate_after':context,'results':{**{k:{'status':'PASS'} for k in ('docs','data','source_stability')},'unity':{'status':'PASS','results':steps}},'unity_artifact_sha256':support.artifact_hashes(u)}
            self.assertTrue(support.validate_unity_report(report,root,context)[0])
            changes=[('gate_after',{}),('options',{}),('unity_artifact_sha256',{})]
            for key,value in changes:
                broken=copy.deepcopy(report);broken[key]=value;self.assertFalse(support.validate_unity_report(broken,root,context)[0])
            for key in steps:
                broken=copy.deepcopy(report);del broken['results']['unity']['results'][key];self.assertFalse(support.validate_unity_report(broken,root,context)[0])
            broken=copy.deepcopy(report);broken['results']['unity']['results']['screens']['checks']=[];self.assertFalse(support.validate_unity_report(broken,root,context)[0])
            broken=copy.deepcopy(report);broken['results']['unity']['results']['frames']['after']=1;self.assertFalse(support.validate_unity_report(broken,root,context)[0])
            (u/'inventory.png').unlink();self.assertFalse(support.validate_unity_report(report,root,context)[0])

    def test_real_git_and_hooks_in_isolated_copy(self):
        # Full fixture uses actual validators, actual pre-commit dispatch and actual Stop entrypoint.
        with tempfile.TemporaryDirectory(prefix='ds-hook-fixture-') as t:
            root=Path(t).resolve()
            names=gate.git(gate.ROOT,'ls-files','--cached','--others','--exclude-standard','-z').split(b'\0')
            for name in names:
                if not name:continue
                rel=name.decode();source=gate.ROOT/rel
                if source.is_file() and not source.is_symlink():
                    dest=root/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,dest)
            # Historical review links point at ignored local evidence. Copy only referenced
            # originals for this document-check fixture, never use them as fresh gate proof.
            for md in root.rglob('*.md'):
                for target in re.findall(r'\]\(([^)]+)\)', md.read_text()):
                    target=target.split('#')[0].strip('<>')
                    if '://' in target:
                        continue
                    dest=(md.parent/target).resolve()
                    if dest.is_relative_to(root/'output'):
                        original=gate.ROOT/dest.relative_to(root)
                        if original.is_file():
                            dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(original,dest)
                        elif original.is_dir():
                            dest.mkdir(parents=True,exist_ok=True)
            subprocess.run(['git','init','-q',str(root)],check=True,capture_output=True)
            gate.git(root,'config','--local','core.hooksPath','.githooks')
            # Model a fresh installation in this disposable fixture only. The real
            # project's policy/evidence must never be changed by a hook unit test.
            # Runtime evidence rejection is covered by the separate tests above.
            policy_path=root/'Tools/Verify/gate-policy.json'
            fixture_policy=json.loads(policy_path.read_text())
            fixture_policy['runtime_baseline']=gate.runtime_state(root)
            policy_path.write_text(json.dumps(fixture_policy,ensure_ascii=False,indent=2)+'\n')
            # untracked required files must not pass
            before=gate.source_snapshot(root)
            with self.assertRaises(ValueError):gate.require_index_match(before,gate.index_snapshot(root))
            gate.git(root,'add','--all')
            good=subprocess.run(['git','-C',str(root),'-c','user.name=Gate Fixture','-c','user.email=fixture@invalid','commit','-qm','fixture only'],capture_output=True,text=True)
            self.assertEqual(good.returncode,0,good.stdout+good.stderr)
            saved=(root/'README.md').read_text();(root/'README.md').write_text(saved+'\n[broken](missing-file.md)\n');gate.git(root,'add','README.md')
            bad=subprocess.run(['git','-C',str(root),'-c','user.name=Gate Fixture','-c','user.email=fixture@invalid','commit','-qm','must reject'],capture_output=True,text=True)
            self.assertNotEqual(bad.returncode,0)
            payload={'hook_event_name':'Stop','cwd':str(root),'stop_hook_active':True}
            stop=subprocess.run([gate.PYTHON,str(root/'Tools/Verify/gate.py'),'stop'],input=json.dumps(payload),capture_output=True,text=True)
            self.assertEqual(json.loads(stop.stdout)['decision'],'block')
            (root/'README.md').write_text(saved)
            stop=subprocess.run([gate.PYTHON,str(root/'Tools/Verify/gate.py'),'stop'],input=json.dumps(payload),capture_output=True,text=True)
            self.assertNotIn('decision',json.loads(stop.stdout))
            # A stale PNG must be rejected even though SVG/layout hashes still match.
            policy=json.loads((root/'Tools/Verify/gate-policy.json').read_text());png=(root/policy['active_map']['sheets'][0]).with_suffix('.png');png.write_bytes(png.read_bytes()+b'tampered')
            with self.assertRaisesRegex(ValueError,'Diagram changed'):gate.active_map_check(root,policy)
            # Unstaged content and mode, then a symlink, are rejected.
            with self.assertRaises(ValueError):gate.require_index_match(gate.source_snapshot(root),gate.index_snapshot(root))
            (root/'untracked-link').symlink_to('/private/tmp')
            with self.assertRaisesRegex(ValueError,'Symlink'):gate.source_snapshot(root)


if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(GateTests)
    stream=io.StringIO();result=unittest.TextTestRunner(stream=stream,verbosity=2).run(suite)
    stamp=datetime.datetime.now().astimezone();folder=gate.ROOT/'output/verification'/('hook-tests-'+stamp.strftime('%Y%m%d-%H%M%S'));folder.mkdir(parents=True)
    (folder/'tests.txt').write_text(stream.getvalue())
    report={'time':stamp.isoformat(),'status':'PASS' if result.wasSuccessful() else 'FAIL','tests':result.testsRun,'failures':len(result.failures),'errors':len(result.errors),'scope':'fixture/unit hook tests; not native Codex trust/activation or Unity tests','sha256':support.verifier_hashes(gate.ROOT)}
    (folder/'summary.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(stream.getvalue());print(json.dumps({'status':report['status'],'report':str(folder/'summary.json')}));sys.exit(0 if result.wasSuccessful() else 1)

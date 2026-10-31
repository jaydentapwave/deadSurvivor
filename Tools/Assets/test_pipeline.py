import copy
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import pipeline as p
import unity_asset


class ContractTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name).resolve()
        self.tools=self.root/'tools';self.tools.mkdir()
        for name in ('pipeline.py','process_blender.py','UnityAsset.cs','unity_asset.py'):
            (self.tools/name).write_text(name)
        (self.tools.parent/'project_paths.py').write_text('fixture shared path policy')
        self.blender=self.root/'blender';self.blender.write_bytes(b'fixture executable')
        self.patch=patch.multiple(p,ROOT=self.root,BASE=self.root/'output/asset-pipeline',
                                 TOOLS=self.tools,BLENDER=str(self.blender));self.patch.start()
        sources={}
        for name in ('rock.fbx','base.png','normal.png'):
            file=self.root/name;file.write_bytes(name.encode())
            sources[name]={'sha256':p.digest(file),'destination':name}
        self.spec={'schema':1,'asset_id':'rock','revision':'r1','scope':'TRANSFER_TEST',
            'provider':'tripo-studio','task_id':'21300150-1f34-4e8d-ae0b-a0ca509bf443',
            'account_profile':'existing','source_files':sources,'model':'rock.fbx','basecolor':'base.png','normal':'normal.png',
            'limits':{'triangles':10000,'materials':1,'meshes':1,'texture_size':2048,'horizontal_size':2,'tolerance':.01},'purpose':'test'}
        self.specfile=self.root/'contract.json';p.atomic(self.specfile,self.spec)
        self.m={'triangles':9000,'materials':1,'meshes':1,'size':[2,1,1.5],
                'center':[0,.5,0],'bottom':0,'textures':{'basecolor':[2048,2048],'normal':[2048,2048]},
                'shader':'Universal Render Pipeline/Lit','shader_supported':True,'forbidden_components':[]}

    def tearDown(self):
        self.patch.stop();self.tmp.cleanup()

    def init(self, provider='tripo-studio'):
        self.spec['provider']=provider;p.atomic(self.specfile,self.spec)
        return p.init(self.specfile)

    def processed(self):
        job=self.init()
        def processor(cmd,**kwargs):
            out=Path(cmd[-1])
            for name in ('model.fbx','basecolor.png','normal.png','before.png','after.png'):
                (out/name).write_bytes(name.encode())
            p.atomic(out/'receipt.json',{'measurement':self.m})
            return subprocess.CompletedProcess(cmd,0)
        with patch.object(p.subprocess,'run',side_effect=processor): p.resume(job)
        return job

    def test_shared_path_policy_is_fingerprinted(self):
        before=p.tools_hashes()
        (self.tools.parent/'project_paths.py').write_text('changed fixture policy')
        after=p.tools_hashes()
        self.assertNotEqual(before['Tools/project_paths.py'],after['Tools/project_paths.py'])

    def test_contract_valid(self):
        self.assertEqual(p.validate_spec(self.spec),self.spec)

    def test_missing_and_nonfinite_limits(self):
        for value in (0,-1,float('nan'),True):
            with self.subTest(value=value):
                s=copy.deepcopy(self.spec);s['limits']['triangles']=value
                with self.assertRaises(ValueError):p.validate_spec(s)
        s=copy.deepcopy(self.spec);del s['limits']['texture_size']
        with self.assertRaises(ValueError):p.validate_spec(s)

    def test_unknown_schema_and_scope(self):
        for field,value in [('schema',2),('scope','PRODUCTION')]:
            s=copy.deepcopy(self.spec);s[field]=value
            with self.assertRaises(ValueError):p.validate_spec(s)

    def test_escape_and_symlink(self):
        for name in ('../outside','/tmp/outside','a/../../out'):
            with self.assertRaises(ValueError):p.child(self.root,name)
        (self.root/'link').symlink_to(self.root/'base.png')
        with self.assertRaises(ValueError):p.child(self.root,'link')

    def test_duplicate_destinations(self):
        self.spec['source_files']['base.png']['destination']='rock.fbx'
        with self.assertRaises(ValueError):p.validate_spec(self.spec)

    def test_strict_json(self):
        for text in ('{"a":1,"a":2}','{"a":NaN}'):
            self.specfile.write_text(text)
            with self.assertRaises(ValueError):p.load(self.specfile)

    def test_source_drift_rejected(self):
        job=self.init();(self.root/'base.png').write_bytes(b'changed')
        with self.assertRaises(p.Blocked):p.resume(job)

    def test_copy_resume_preserves_id_and_bytes(self):
        job=self.init();a=p.resume(job,True);b=p.resume(job,True)
        self.assertEqual(a['run_id'],b['run_id']);self.assertEqual(a['events'],b['events'])
        self.assertEqual(b['generation_requests'],0)
        self.assertEqual(p.digest(job/'raw/rock.fbx'),self.spec['source_files']['rock.fbx']['sha256'])

    def test_corrupt_partial_copy_rejected(self):
        job=self.init();(job/'raw').mkdir();(job/'raw/rock.fbx').write_bytes(b'partial')
        with self.assertRaises(p.Blocked):p.resume(job,True)

    def test_completed_copy_drift_rejected(self):
        job=self.init();p.resume(job,True);(job/'raw/rock.fbx').write_bytes(b'changed')
        with self.assertRaises(p.Blocked):p.resume(job,True)

    def test_interrupted_processor_retains_source_checkpoint(self):
        job=self.init();p.resume(job,True)
        with patch.object(p.subprocess,'run',side_effect=subprocess.TimeoutExpired('blender',1)):
            with self.assertRaises(subprocess.TimeoutExpired):p.resume(job)
        j,_=p.verify_job(job);self.assertEqual(j['phase'],'SOURCE_READY')
        self.assertEqual(p.resume(job,True)['task_id'],self.spec['task_id'])

    def test_tools_drift_rejected(self):
        job=self.init()
        (self.tools/'pipeline.py').write_text('changed common contract logic')
        with self.assertRaises(p.Blocked):p.verify_job(job)

    def test_incomplete_success_claim_rejected(self):
        job=self.init();j=p.load(job/'job.json')
        j.update(phase='PROCESSED',events=[],artifacts={});p.atomic(job/'job.json',j)
        with self.assertRaises(p.Blocked):p.verify_job(job)

    def test_missing_raw_manifest_rejected(self):
        job=self.init();p.resume(job,True);j=p.load(job/'job.json');j['artifacts']={};p.atomic(job/'job.json',j)
        with self.assertRaises(p.Blocked):p.verify_job(job)

    def test_observed_process_failure_is_fail(self):
        job=self.init();p.resume(job,True)
        def bad_processor(cmd,**kwargs):
            out=Path(cmd[-1]);m=copy.deepcopy(self.m);m['triangles']=10001
            p.atomic(out/'receipt.json',{'measurement':m})
            return subprocess.CompletedProcess(cmd,0)
        with patch.object(p.subprocess,'run',side_effect=bad_processor):
            with self.assertRaises(p.AssetFailure):p.resume(job)
        j,_=p.verify_job(job)
        self.assertEqual(j['events'][-1]['action'],'process-failed')

    def test_concurrent_writer_blocked(self):
        job=self.init()
        with p.lock(job):
            with self.assertRaises(p.Blocked):
                with p.lock(job):pass

    def test_measure_valid(self):
        self.assertEqual(p.judge(self.m,self.spec['limits'])['status'],'PASS')

    def test_triangle_lower_and_upper(self):
        for value in (0,-1,10001,True,9.5):
            self.m['triangles']=value
            self.assertIn('TRIANGLE_BUDGET',p.judge(self.m,self.spec['limits'])['errors'])

    def test_bounds_invalid(self):
        for size in ([0,1,2],[.02,1,.01],[float('nan'),1,2],[-1,1,2]):
            self.m['size']=size
            self.assertEqual(p.judge(self.m,self.spec['limits'])['status'],'FAIL')

    def test_pivot_both_signs(self):
        for offset in (-.1,.1):
            self.m['bottom']=offset
            self.assertIn('GROUND_PIVOT',p.judge(self.m,self.spec['limits'])['errors'])

    def test_missing_material_texture_and_forbidden_component(self):
        for field,value in [('materials',0),('textures',{}),('shader_supported',False),('forbidden_components',['Collider'])]:
            m=copy.deepcopy(self.m);m[field]=value
            self.assertEqual(p.judge(m,self.spec['limits'])['status'],'FAIL')

    def test_measured_dependency_change_rejected(self):
        file=self.root/'candidate.prefab';file.write_bytes(b'original')
        hashes={'candidate.prefab':p.digest(file)}
        unity_asset.check_dependencies(hashes,self.root)
        file.write_bytes(b'changed after measuring')
        with self.assertRaises(p.AssetFailure):unity_asset.check_dependencies(hashes,self.root)
        with self.assertRaises(p.AssetFailure):unity_asset.check_dependencies({},self.root)

    def test_studio_cannot_be_queried_as_api(self):
        job=self.init()
        with patch.object(p.subprocess,'run') as run:
            with self.assertRaises(p.Blocked):p.query_task(job,runner=run)
            run.assert_not_called()

    def test_final_source_change_keeps_failed_summary_after_cleanup(self):
        job=self.init();p.resume(job,True)
        out=job/'unity-test';out.mkdir()
        report={'status':'PASS','results':{'cleanup':{'status':'PASS'}}}
        (job/'raw/rock.fbx').write_bytes(b'changed at final audit')
        unity_asset.finish_report(report,out,[('source_stability',lambda:p.verify_job(job))])
        saved=p.load(out/'summary.json')
        self.assertEqual(saved['status'],'BLOCKED')
        self.assertEqual(saved['results']['source_stability']['status'],'BLOCKED')
        self.assertEqual(saved['results']['cleanup']['status'],'PASS')

    def test_final_target_symlink_preserves_other_audits_and_summary(self):
        out=self.root/'unity-test';out.mkdir()
        target=self.root/'target';target.mkdir();(target/'unexpected').symlink_to(self.root/'rock.fbx')
        report={'status':'PASS','results':{}}
        unity_asset.finish_report(report,out,[('existing_files',lambda:unity_asset.snapshot_tree(target)),
                                             ('independent_check',lambda:None)])
        saved=p.load(out/'summary.json')
        self.assertEqual(saved['status'],'BLOCKED')
        self.assertEqual(saved['results']['existing_files']['status'],'BLOCKED')
        self.assertEqual(saved['results']['independent_check']['status'],'PASS')

    def test_query_resume_no_create_and_no_urls(self):
        job=self.init('tripo-api');calls=[]
        def cli(cmd,**kw):
            calls.append(cmd)
            return subprocess.CompletedProcess(cmd,0,json.dumps({'task_id':self.spec['task_id'],'status':'success',
                'output':{'url':'https://example.invalid/?secret=not-for-log'}}),'')
        a=p.query_task(job,cli);b=p.query_task(job,cli)
        self.assertEqual(a,b);self.assertEqual(len(calls),2)
        self.assertTrue(all(c[1:3]==['task','get'] for c in calls))
        self.assertNotIn('secret',(job/'job.json').read_text())

    def test_query_timeout_and_id_mismatch_no_resubmit(self):
        job=self.init('tripo-api')
        def timed(*a,**k):raise subprocess.TimeoutExpired('get',30)
        with self.assertRaises(p.Blocked):p.query_task(job,timed)
        def mismatch(cmd,**kw):return subprocess.CompletedProcess(cmd,0,json.dumps({'task_id':'other','status':'success'}),'')
        with self.assertRaises(p.Blocked):p.query_task(job,mismatch)
        self.assertEqual(p.load(job/'job.json')['generation_requests'],0)

    def test_unity_or_display_change_reuses_processed_without_blender(self):
        job=self.processed();original=(job/'job.json').read_bytes()
        (self.tools/'UnityAsset.cs').write_text('new Unity measurement code')
        (self.tools/'unity_asset.py').write_text('new Unity reporter')
        (self.root/'report.py').write_text('new display only code')
        p.verify_job(job)
        with patch.object(p.subprocess,'run') as run:
            copied=p.init(self.specfile,reuse_from=job);p.resume(copied)
            run.assert_not_called()
        j,_=p.verify_job(copied)
        self.assertEqual(j['reuse']['scope'],'RAW_AND_PROCESSED')
        self.assertEqual(j['phase'],'PROCESSED')
        self.assertEqual((job/'job.json').read_bytes(),original)
        self.assertEqual(j['artifacts'],p.load(job/'job.json')['artifacts'])

    def test_processor_or_binary_change_reuses_only_raw(self):
        job=self.processed()
        for file in (self.tools/'process_blender.py',self.blender):
            with self.subTest(file=file.name):
                original=file.read_bytes();file.write_bytes(b'new version')
                with self.assertRaises(p.Blocked):p.verify_job(job)
                new=p.init(self.specfile,reuse_from=job);j,_=p.verify_job(new)
                self.assertEqual(j['phase'],'SOURCE_READY')
                self.assertEqual(j['reuse']['scope'],'RAW_ONLY')
                self.assertTrue(all(name.startswith('raw/') for name in j['artifacts']))
                file.write_bytes(original)

    def test_contract_change_cannot_reuse_processed(self):
        job=self.processed();self.spec['limits']['triangles']=9001;p.atomic(self.specfile,self.spec)
        new=p.init(self.specfile,reuse_from=job);j,_=p.verify_job(new)
        self.assertEqual(j['phase'],'SOURCE_READY')

    def test_corrupt_cache_is_blocked_not_silently_rebuilt(self):
        job=self.processed();j=p.load(job/'job.json')
        (job/j['processed']/'model.fbx').write_bytes(b'corruption')
        with patch.object(p.subprocess,'run') as run:
            with self.assertRaises(p.Blocked):p.init(self.specfile,reuse_from=job)
            run.assert_not_called()

    def test_legacy_job_is_unchanged_and_only_raw_can_be_reused(self):
        job=self.processed();j=p.load(job/'job.json');j['schema']=1;del j['process_dependencies'];p.atomic(job/'job.json',j)
        p.verify_job(job)
        (self.tools/'UnityAsset.cs').write_text('new importer')
        with self.assertRaises(p.Blocked):p.verify_job(job)
        original=(job/'job.json').read_bytes()
        new=p.init(self.specfile,reuse_from=job)
        self.assertEqual(p.load(new/'job.json')['reuse']['scope'],'RAW_ONLY')
        self.assertEqual((job/'job.json').read_bytes(),original)

    def test_reuse_source_lock_is_enforced(self):
        job=self.processed()
        with p.lock(job):
            with self.assertRaises(p.Blocked):p.init(self.specfile,reuse_from=job)

    def test_reuse_copy_race_is_rejected(self):
        job=self.processed();copyfileobj=p.shutil.copyfileobj
        def raced(src,dst):
            copyfileobj(src,dst)
            (job/'raw/rock.fbx').write_bytes(b'changed during copy')
        with patch.object(p.shutil,'copyfileobj',side_effect=raced):
            with self.assertRaises(p.Blocked):p.init(self.specfile,reuse_from=job)

    def test_reuse_identity_change_is_rejected(self):
        job=self.processed();self.spec['account_profile']='different';p.atomic(self.specfile,self.spec)
        with self.assertRaises(p.Blocked):p.init(self.specfile,reuse_from=job)

    def test_missing_stage_dependencies_cannot_be_reused(self):
        job=self.processed();j=p.load(job/'job.json');del j['process_dependencies'];p.atomic(job/'job.json',j)
        with self.assertRaises(p.Blocked):p.init(self.specfile,reuse_from=job)

    def test_malformed_dependency_values_are_blocked_not_raw_reused(self):
        job=self.processed();original=p.load(job/'job.json')
        changes=[('tools',None),('blender',None),('tools',{'pipeline.py':None,'process_blender.py':None}),
                 ('blender',{'path':None,'sha256':None}),('blender',{'path':'relative','sha256':'f'*64}),
                 ('blender',{'path':'/blender','sha256':'not a hash'})]
        for key,value in changes:
            with self.subTest(key=key,value=value):
                j=copy.deepcopy(original);j['process_dependencies'][key]=value;p.atomic(job/'job.json',j)
                with self.assertRaises(p.Blocked):p.init(self.specfile,reuse_from=job)


if __name__=='__main__':unittest.main()

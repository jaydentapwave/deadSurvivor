"""I1 protocol fixtures; these are never Unity motion observations."""
import copy
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from unittest.mock import patch

import map_session as s
from map_io import MapInputError, load_json


class SessionTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name).resolve()
        self.root_patch=patch.object(s,'ROOT',self.root);self.root_patch.start()
        self.boot=patch.object(s,'boot_identity',return_value='fixture-boot');self.boot.start()
        self.clock=patch.object(s.time,'monotonic',return_value=100.0);self.clock.start()
        self.input=self.root/'input.json';self.input.write_text('{"scope":"protocol fixture"}')
        self.plan={'schema_version':1,'document_role':'I1_COLLECTION_PLAN','scope':s.SCOPE,
                   'suite_id':'protocol','revision':'r001','expected_case_ids':['A','B'],
                   'cases':[{'case_id':x,'step_count':2,'event_count':1} for x in ['A','B']],
                   'inputs':[{'path':'input.json','sha256':s.digest(self.input)}],
                   'wall_timeout_s':60,'max_payload_bytes':1024*1024}
        self.planfile=self.root/'plan.json';self.serial=0

    def tearDown(self):
        self.clock.stop();self.boot.stop();self.root_patch.stop();self.temp.cleanup()

    def create(self, plan=None):
        self.planfile.write_text(json.dumps(self.plan if plan is None else plan))
        return s.create(self.planfile)

    def packet(self, folder, cid='A'):
        m=load_json(folder/'manifest.json');identity={k:m[k] for k in ['run_id','nonce']};identity['case_id']=cid
        return {'schema_version':1,'scope':s.SCOPE,**identity,
                'start_marker':{**identity,'frame_id':10},'end_marker':{**identity,'frame_id':13},
                'steps':[{**identity,'step_id':i,'frame_id':11+i,'observer_sequence':i+1,
                          'payload':{'fixture_value':i}} for i in range(2)],
                'events':[{**identity,'event_sequence':1,'step_id':1,'payload':{'fixture':'event'}}]}

    def collect(self, folder, packet=None, cid='A'):
        self.serial+=1;path=self.root/('packet-'+str(self.serial)+'.json')
        path.write_text(json.dumps(self.packet(folder,cid) if packet is None else packet))
        return s.collect(folder.name,path)

    def complete(self):
        folder=self.create()
        self.collect(folder,cid='A');self.collect(folder,cid='B')
        result=s.finish(folder.name)
        return folder,result

    def tree(self, folder):
        return {str(p.relative_to(folder)):s.digest(p) for p in folder.rglob('*') if p.is_file()}

    def test_complete_collection_never_claims_runtime_or_cleanup_pass(self):
        folder,r=self.complete()
        self.assertEqual(r['collector_status'],'PASS');self.assertEqual(r['status'],'NOT_RUN')
        self.assertEqual(r['unity_status'],'NOT_RUN');self.assertEqual(r['cleanup_status'],'NOT_RUN')
        self.assertTrue(all(v=='NOT_RUN' for v in r['axis_status'].values()))
        self.assertEqual(r['collected_case_ids'],['A','B'])

    def test_new_runs_have_distinct_identity(self):
        a,b=self.create(),self.create()
        self.assertNotEqual(a,b);self.assertNotEqual(load_json(a/'manifest.json')['nonce'],load_json(b/'manifest.json')['nonce'])

    def test_empty_duplicate_mismatched_and_design_plans_rejected(self):
        variants=[]
        for key,value in [('expected_case_ids',[]),('expected_case_ids',['A','A']),
                          ('expected_case_ids',['B','A']),('schema_version',True),('schema_version',2),
                          ('document_role','DESIGN_EXAMPLES_NOT_EXECUTABLE_REQUEST'),('wall_timeout_s',float('nan')),
                          ('wall_timeout_s',0),('wall_timeout_s',True),('max_payload_bytes',True),('inputs',[])]:
            value_plan=copy.deepcopy(self.plan);value_plan[key]=value;variants.append(value_plan)
        unknown=copy.deepcopy(self.plan);unknown['unknown']=True;variants.append(unknown)
        for variant in variants:
            with self.subTest(variant=variant),self.assertRaises(MapInputError):self.create(variant)

    def test_unsafe_input_paths_and_symlinks_are_rejected(self):
        (self.root/'link').symlink_to(self.input)
        for path in ['../outside','/tmp/outside','./input.json','link','a//b']:
            plan=copy.deepcopy(self.plan);plan['inputs'][0]['path']=path
            with self.subTest(path=path),self.assertRaises(MapInputError):self.create(plan)

    def test_empty_missing_and_wrong_sequence_observations_taint_run(self):
        variants=[lambda p:p.update(steps=[]),lambda p:p.update(events=[]),
                  lambda p:p['steps'][1].update(observer_sequence=3),
                  lambda p:p['steps'][1].update(frame_id=11),
                  lambda p:p['start_marker'].update(frame_id=100),
                  lambda p:p['events'][0].update(step_id=2)]
        for mutate in variants:
            folder=self.create();packet=self.packet(folder);mutate(packet)
            with self.assertRaises(MapInputError):self.collect(folder,packet)
            self.assertEqual(s.finish(folder.name,'cancelled')['status'],'FAIL')

    def test_old_nonce_and_mixed_row_identity_are_rejected(self):
        old=self.create();new=self.create()
        for packet in [self.packet(old),self.packet(new)]:
            if packet['run_id']==new.name:packet['steps'][0]['nonce']='0'*32
            with self.assertRaises(MapInputError):self.collect(new,packet)
        self.assertEqual(s.finish(new.name,'interrupted')['status'],'FAIL')

    def test_duplicate_collection_does_not_overwrite_packet(self):
        folder=self.create();self.collect(folder);before=s.digest(folder/'cases/A/packet.json')
        with self.assertRaises(MapInputError):self.collect(folder)
        self.assertEqual(s.digest(folder/'cases/A/packet.json'),before)
        self.assertEqual(s.finish(folder.name,'cancelled')['status'],'FAIL')

    def test_missing_cases_are_not_success(self):
        folder=self.create();self.collect(folder)
        r=s.finish(folder.name);self.assertEqual(r['status'],'BLOCKED');self.assertEqual(r['missing_case_ids'],['B'])

    def test_closed_result_is_immutable_and_deadline_no_longer_applies(self):
        folder,r=self.complete();before=self.tree(folder)
        with patch.object(s,'boot_identity',return_value='later-boot'),patch.object(s.time,'monotonic',return_value=99999):
            self.assertEqual(s.inspect(folder.name),r);self.assertEqual(s.finish(folder.name),r)
            with self.assertRaises(MapInputError):self.collect(folder,cid='A')
            with self.assertRaises(MapInputError):s.finish(folder.name,'cancelled')
        self.assertEqual(self.tree(folder),before)

    def test_aborted_result_cannot_be_rewritten_as_completed(self):
        folder=self.create();r=s.finish(folder.name,'cancelled');before=self.tree(folder)
        self.assertEqual(s.finish(folder.name),r);self.assertEqual(r['close_reason'],'cancelled')
        with self.assertRaises(MapInputError):s.finish(folder.name,'interrupted')
        self.assertEqual(self.tree(folder),before)

    def test_boot_change_with_increasing_monotonic_is_blocked(self):
        folder=self.create()
        with patch.object(s,'boot_identity',return_value='different-boot'),patch.object(s.time,'monotonic',return_value=120):
            with self.assertRaises(MapInputError) as error:s.inspect(folder.name)
            self.assertEqual(error.exception.code,'BOOT_CHANGED')

    def test_clock_regression_is_blocked(self):
        folder=self.create()
        with patch.object(s.time,'monotonic',return_value=99):
            with self.assertRaises(MapInputError):s.inspect(folder.name)

    def test_deadline_is_enforced_after_a_gap_between_commands(self):
        folder=self.create()
        with patch.object(s.time,'monotonic',return_value=200):
            self.assertEqual(s.inspect(folder.name)['status'],'BLOCKED')
            r=s.finish(folder.name);self.assertEqual(r['close_reason'],'timeout');self.assertEqual(r['status'],'BLOCKED')

    def test_deadline_crossed_at_close_marker_cannot_be_collector_pass(self):
        folder=self.create();self.collect(folder,cid='A');self.collect(folder,cid='B')
        with patch.object(s.time,'monotonic',side_effect=[159,159,161,161]):r=s.finish(folder.name)
        self.assertEqual(r['close_reason'],'timeout');self.assertEqual(r['collector_status'],'BLOCKED')

    def test_prior_failure_survives_later_timeout(self):
        folder=self.create();packet=self.packet(folder);packet['steps']=[]
        with self.assertRaises(MapInputError):self.collect(folder,packet)
        with patch.object(s.time,'monotonic',return_value=200):self.assertEqual(s.finish(folder.name)['status'],'FAIL')

    def test_payload_limit_is_a_persistent_blocker(self):
        plan=copy.deepcopy(self.plan);plan['max_payload_bytes']=100
        folder=self.create(plan)
        with self.assertRaises(MapInputError):self.collect(folder)
        self.assertEqual(s.inspect(folder.name)['status'],'BLOCKED')
        self.assertEqual(s.finish(folder.name)['status'],'BLOCKED')

    def test_changed_input_or_producer_invalidates_evidence(self):
        folder=self.create();original=self.input.read_bytes();self.input.write_bytes(b'changed')
        with self.assertRaises(MapInputError):s.inspect(folder.name)
        self.input.write_bytes(original)
        with patch.object(s,'producers',return_value={'different':'producer'}):
            with self.assertRaises(MapInputError):s.inspect(folder.name)

    def test_packet_tampering_is_detected(self):
        folder=self.create();self.collect(folder);(folder/'cases/A/packet.json').write_text('{}')
        with self.assertRaises(MapInputError):s.inspect(folder.name)

    def test_manifest_and_journal_mutation_are_detected(self):
        folder=self.create();manifest=load_json(folder/'manifest.json');manifest['nonce']='0'*32;s.save(folder/'manifest.json',manifest)
        with self.assertRaises(MapInputError):s.inspect(folder.name)
        folder=self.create();rows=(folder/'journal.jsonl').read_text().replace('"seq": 1','"seq": 2');(folder/'journal.jsonl').write_text(rows)
        with self.assertRaises(MapInputError):s.inspect(folder.name)

    def test_forged_final_pass_is_rejected_even_if_hash_is_updated(self):
        folder,_=self.complete();r=load_json(folder/'summary.json');r['status']='PASS';s.save(folder/'summary.json',r)
        rows=[json.loads(x) for x in (folder/'journal.jsonl').read_text().splitlines()]
        rows[-1]['details']['summary_sha256']=s.digest(folder/'summary.json')
        (folder/'journal.jsonl').write_text(''.join(json.dumps(x)+'\n' for x in rows))
        with self.assertRaises(MapInputError):s.inspect(folder.name)

    def test_unexpected_cleanup_is_not_accepted_as_proof(self):
        folder=self.create();s.save(folder/'cleanup.json',{'status':'PASS'})
        with self.assertRaises(MapInputError):s.finish(folder.name)

    def test_lock_is_enforced_and_released_after_exception(self):
        folder=self.create()
        with s.lock(folder):
            with self.assertRaises(MapInputError):s.inspect(folder.name)
        self.assertEqual(s.inspect(folder.name)['status'],'NOT_RUN')

    def test_worker_exit_leaves_pending_and_os_releases_lock(self):
        folder=self.create();packetfile=self.root/'worker-packet.json';packetfile.write_text(json.dumps(self.packet(folder)))
        code=f'''import sys,os
from pathlib import Path
sys.path.insert(0,{str(s.TOOLS)!r})
import map_session as s
s.ROOT=Path({str(self.root)!r});s.boot_identity=lambda:'fixture-boot';s.time.monotonic=lambda:100.0
original=s.atomic
def die(path,raw):
 if path.name=='packet.json':os._exit(17)
 return original(path,raw)
s.atomic=die
s.collect({folder.name!r},{str(packetfile)!r})
'''
        proc=subprocess.run([sys.executable,'-c',code],capture_output=True,text=True,timeout=10)
        self.assertEqual(proc.returncode,17,proc.stderr)
        self.assertIn('UNCOMMITTED_COLLECTION',s.inspect(folder.name)['reason_codes'])
        with self.assertRaises(MapInputError):self.collect(folder)
        with self.assertRaises(MapInputError):s.finish(folder.name)
        self.assertEqual(s.finish(folder.name,'interrupted')['status'],'BLOCKED')

    def test_incomplete_finalization_cannot_be_automatically_completed(self):
        folder=self.create();original=s.save
        def fail(path,value):
            if path.name=='summary.json':raise OSError('simulated storage failure')
            return original(path,value)
        with patch.object(s,'save',side_effect=fail):
            with self.assertRaises(OSError):s.finish(folder.name)
        self.assertIn('INCOMPLETE_FINALIZATION',s.inspect(folder.name)['reason_codes'])
        with self.assertRaises(MapInputError):s.finish(folder.name)
        with self.assertRaises(MapInputError):s.finish(folder.name,'interrupted')

    def test_os_exit_after_packet_write_preserves_verifiable_raw(self):
        folder=self.create();path=self.root/'exit-after-write.json';path.write_text(json.dumps(self.packet(folder)))
        code=f'''import sys,os
from pathlib import Path
sys.path.insert(0,{str(s.TOOLS)!r})
import map_session as s
s.ROOT=Path({str(self.root)!r});s.boot_identity=lambda:'fixture-boot';s.time.monotonic=lambda:100.0
original=s.append
def die(folder,manifest,rows,phase,**details):
 if phase=='ACCEPTED':os._exit(18)
 return original(folder,manifest,rows,phase,**details)
s.append=die
s.collect({folder.name!r},{str(path)!r})
'''
        proc=subprocess.run([sys.executable,'-c',code],capture_output=True,text=True,timeout=10)
        self.assertEqual(proc.returncode,18,proc.stderr)
        raw=folder/'cases/A/packet.json';self.assertTrue(raw.is_file())
        result=s.finish(folder.name,'interrupted')
        self.assertEqual(result['pending_evidence']['sha256'],s.digest(raw))
        raw.unlink()
        with self.assertRaises(MapInputError):s.inspect(folder.name)

    def pending_after_packet_write(self):
        folder=self.create();original=s.append
        def fail(folder,manifest,rows,phase,**details):
            if phase=='ACCEPTED':raise OSError('worker stopped before acceptance')
            return original(folder,manifest,rows,phase,**details)
        with patch.object(s,'append',side_effect=fail):
            with self.assertRaises(OSError):self.collect(folder)
        return folder

    def test_pending_packet_change_before_abort_is_detected(self):
        folder=self.pending_after_packet_write();(folder/'cases/A/packet.json').write_text('{}')
        with self.assertRaises(MapInputError):s.finish(folder.name,'interrupted')

    def test_closed_pending_packet_cannot_be_changed_or_deleted(self):
        for action in ('change','delete'):
            with self.subTest(action=action):
                folder=self.pending_after_packet_write();r=s.finish(folder.name,'interrupted')
                self.assertTrue(r['pending_evidence']['exists'])
                path=folder/'cases/A/packet.json'
                if action=='change':path.write_text('{}')
                else:path.unlink()
                with self.assertRaises(MapInputError):s.inspect(folder.name)
                with self.assertRaises(MapInputError):s.finish(folder.name)

    def test_absent_pending_file_cannot_be_added_after_close(self):
        folder=self.create();m=load_json(folder/'manifest.json');packet=self.packet(folder)
        raw=json.dumps(packet).encode();rows=[json.loads(x) for x in (folder/'journal.jsonl').read_text().splitlines()]
        with s.lock(folder):s.append(folder,m,rows,'COLLECT_PLANNED',case_id='A',sha256=s.sha(raw),bytes=len(raw))
        r=s.finish(folder.name,'interrupted');self.assertFalse(r['pending_evidence']['exists'])
        path=folder/'cases/A/packet.json';path.parent.mkdir(parents=True);path.write_bytes(raw)
        with self.assertRaises(MapInputError):s.inspect(folder.name)

    def test_uncommitted_temp_files_are_hash_frozen(self):
        for action in ('change','delete'):
            with self.subTest(action=action):
                folder=self.create();path=folder/('.pending-'+'f'*32);path.write_bytes(b'partial')
                r=s.finish(folder.name,'interrupted');self.assertEqual(r['uncommitted_files'][path.name],s.digest(path))
                if action=='change':path.write_bytes(b'changed')
                else:path.unlink()
                with self.assertRaises(MapInputError):s.inspect(folder.name)

    def test_cli_final_normal_protocol_returns_not_run_exit_two(self):
        folder,_=self.complete();stream=io.StringIO()
        with patch('sys.argv',['map_session.py','status','--run',folder.name]),redirect_stdout(stream):
            self.assertEqual(s.main(),2)
        response=json.loads(stream.getvalue());self.assertEqual(response['unity_status'],'NOT_RUN')

    def test_cli_failure_preserves_unique_error_report(self):
        stream=io.StringIO()
        with patch('sys.argv',['map_session.py','status','--run','not-a-run']),redirect_stdout(stream):self.assertEqual(s.main(),1)
        result=json.loads(stream.getvalue());self.assertTrue(Path(result['report']).is_file())


if __name__=='__main__':unittest.main()

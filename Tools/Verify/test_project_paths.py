from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch
import sys
import json
import subprocess
import unittest
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import project_paths as p
from map_io import load_json, MapInputError

class ProjectPathTests(unittest.TestCase):
    def setup_paths(self, base, portal=False):
        root=base/'real';old=base/'former';root.mkdir();(root/'Tools').mkdir();(root/'output').mkdir()
        if portal:
            old.mkdir();(old/'.git').write_text('gitdir: '+str(root/'.git')+'\n')
            for name in ('Tools','output'):(old/name).symlink_to(root/name,target_is_directory=True)
        else:old.symlink_to(root,target_is_directory=True)
        return root,old
    def scope(self,root,old):return patch.multiple(p,ROOT=root,APPROVED_PROJECT=root,LEGACY_ROOT=old)
    def test_approved_legacy_link_reads_same_json(self):
        with TemporaryDirectory() as t:
            root,old=self.setup_paths(Path(t).resolve());(root/'output/e.json').write_text('{"v":4}')
            with self.scope(root,old):self.assertEqual(load_json(old/'output/e.json'),{'v':4})
    def test_portal_accepts_only_exact_git_pointer(self):
        with TemporaryDirectory() as t:
            root,old=self.setup_paths(Path(t).resolve(),True);(root/'output/e.json').write_text('{}')
            with self.scope(root,old):
                self.assertEqual(load_json(old/'output/e.json'),{})
                (old/'.git').write_text('gitdir: /wrong/.git\n')
                with self.assertRaisesRegex(ValueError,'LEGACY_ROOT_CONFLICT'):load_json(old/'output/e.json')
    def test_unknown_alias_still_rejected(self):
        with TemporaryDirectory() as t:
            base=Path(t).resolve();root,old=self.setup_paths(base);alias=base/'unknown';alias.symlink_to(root,target_is_directory=True);(root/'output/e.json').write_text('{}')
            with self.scope(root,old):
                with self.assertRaises(MapInputError):load_json(alias/'output/e.json')
    def test_internal_symlink_not_hidden_by_translation(self):
        with TemporaryDirectory() as t:
            base=Path(t).resolve();root,old=self.setup_paths(base);(base/'outside.json').write_text('{}');(root/'output/e.json').symlink_to(base/'outside.json')
            with self.scope(root,old):
                self.assertEqual(p.canonical_evidence_path(old/'output/e.json'),root/'output/e.json')
                with self.assertRaises(MapInputError):load_json(old/'output/e.json')
    def test_escape_and_wrong_alias_target_rejected(self):
        with TemporaryDirectory() as t:
            base=Path(t).resolve();root,old=self.setup_paths(base)
            with self.scope(root,old):
                with self.assertRaisesRegex(ValueError,'LEGACY_PATH_ESCAPE'):p.canonical_evidence_path(old/'../escape.json')
                old.unlink();old.symlink_to(base,target_is_directory=True)
                with self.assertRaisesRegex(ValueError,'LEGACY_ROOT_CONFLICT'):p.canonical_evidence_path(old/'output/e.json')
    def test_real_018_receipt_relocation_preserves_report_hash(self):
        import map_blockout as b
        receipt=b.build_receipt();old=Path(receipt['build_report']);self.assertTrue(old.is_relative_to(p.LEGACY_ROOT))
        self.assertEqual(b.motion.digest(old),b.motion.digest(p.canonical_evidence_path(old)))
    def test_existing_asset_job_former_root_maps_to_same_job(self):
        sys.path.insert(0,str(p.ROOT/'Tools/Assets'))
        import pipeline
        relative='output/asset-pipeline/rock-21300150'
        self.assertEqual(pipeline.job_dir(p.LEGACY_ROOT/relative),p.ROOT/relative)
        with TemporaryDirectory() as t:
            alias=Path(t).resolve()/'unknown';alias.symlink_to(p.ROOT,target_is_directory=True)
            with self.assertRaisesRegex(ValueError,'Job outside'):pipeline.job_dir(alias/relative)
    def test_data_check_reads_active_assets(self):
        import run
        with TemporaryDirectory() as t:
            root=Path(t).resolve();target=root/p.DATA_RELATIVE;target.parent.mkdir(parents=True)
            data={'characters':[{'id':'c','weapon':'missing'}],'items':[],'monsters':[],'settings':{'weights':[],'waves':[]}}
            target.write_text(json.dumps(data))
            legacy=root/'UnityMigration/Data/PrototypeData.json';legacy.parent.mkdir(parents=True);legacy.write_text('{}')
            result=run.data_check(root);self.assertEqual(result['status'],'FAIL');self.assertIn('Unknown starting weapon: c',result['errors'])
    def test_project_guard_requires_live_assets_and_no_mirror(self):
        with TemporaryDirectory() as t:
            root=Path(t).resolve()
            for rel in [p.DATA_RELATIVE,'ProjectSettings/ProjectVersion.txt','Packages/manifest.json','Assets/DeadSurvivor/Runtime/DSGame.cs','Assets/DeadSurvivor/Editor/DSVerification.cs']:
                q=root/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_text('{}')
            with patch.object(p,'APPROVED_PROJECT',root):
                self.assertEqual(p.require_project(root,root),root)
                (root/'Assets/DeadSurvivor/Runtime/DSGame.cs').unlink()
                with self.assertRaisesRegex(ValueError,'PROJECT_SOURCE_MISSING'):p.require_project(root,root)
    def test_legacy_apply_cannot_overwrite_current_source(self):
        code=p.ROOT/'UnityMigration/sync.py';before=(p.ROOT/p.DATA_RELATIVE).read_bytes()
        r=subprocess.run([sys.executable,str(code),'--apply'],capture_output=True,text=True)
        self.assertNotEqual(r.returncode,0);self.assertIn('retired',r.stderr);self.assertEqual((p.ROOT/p.DATA_RELATIVE).read_bytes(),before)
if __name__=='__main__':unittest.main()

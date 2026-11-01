import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from map_input_scope import verified_scopes, verify_then_release

class ScopeEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name).resolve();self.base=self.root/'output/map-full-026/input-scopes';self.base.mkdir(parents=True)
        self.run='a'*32;self.nonce='b'*32;self.path=self.base/(self.run+'-'+self.nonce+'-c.json')
        self.data=dict(scope='OWNED_VIRTUAL_INPUT_CONFIGURATION',owner=self.run+':'+self.nonce,phase='RESTORED',error='',devicesBefore=[dict(id=1,enabled=False)],devicesAfter=[dict(id=1,enabled=False)],originalId=1,cloneId=2)
        self.write()
    def write(self):self.path.write_text(json.dumps(self.data))
    def tearDown(self):self.tmp.cleanup()
    def test_valid(self):self.assertEqual(verified_scopes(self.root,self.run,self.nonce)[0]['sha256'],hashlib.sha256(self.path.read_bytes()).hexdigest())
    def test_unfinished(self):
        self.data['phase']='INSTALLED';self.write()
        with self.assertRaises(RuntimeError):verified_scopes(self.root,self.run,self.nonce)
    def test_external_input_cancelled(self):
        self.data['error']='EXTERNAL_DEVICE_INPUT';self.write()
        with self.assertRaises(RuntimeError):verified_scopes(self.root,self.run,self.nonce)
    def test_device_changed(self):
        self.data['devicesAfter'][0]['enabled']=True;self.write()
        with self.assertRaises(RuntimeError):verified_scopes(self.root,self.run,self.nonce)
    def test_stale_owner(self):
        self.data['owner']='wrong';self.write()
        with self.assertRaises(RuntimeError):verified_scopes(self.root,self.run,self.nonce)
    def test_missing_device_fields(self):
        self.data.pop('devicesBefore');self.data.pop('devicesAfter');self.write()
        with self.assertRaises(RuntimeError):verified_scopes(self.root,self.run,self.nonce)
    def test_malformed_device_fields(self):
        self.data['devicesBefore']=self.data['devicesAfter']='bad';self.write()
        with self.assertRaises(RuntimeError):verified_scopes(self.root,self.run,self.nonce)
    def test_failure_preserves_recovery_marker(self):
        marker=self.root/'pending.json';marker.write_text('owned recovery')
        self.data['phase']='CONFLICT';self.write()
        with self.assertRaises(RuntimeError):verify_then_release(self.root,self.run,self.nonce,lambda:marker.unlink())
        self.assertTrue(marker.exists())
    def test_count_failure_preserves_recovery_marker(self):
        marker=self.root/'pending.json';marker.write_text('owned recovery')
        with self.assertRaises(RuntimeError):verify_then_release(self.root,self.run,self.nonce,lambda:marker.unlink(),expected_count=2)
        self.assertTrue(marker.exists())
    def test_missing(self):
        self.path.unlink()
        with self.assertRaises(RuntimeError):verified_scopes(self.root,self.run,self.nonce)
if __name__=='__main__':unittest.main()

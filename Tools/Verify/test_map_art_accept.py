import hashlib,tempfile,unittest
from pathlib import Path
from map_art_accept import evidence,four_party,REQUIRED
class AcceptanceTests(unittest.TestCase):
    def test_missing_consumed_artifact(self):
        with self.assertRaisesRegex(ValueError,'OMISSION'):evidence(Path('.'),{})
    def test_replaced_consumed_bytes(self):
        from map_art_accept import m
        with tempfile.TemporaryDirectory(dir=m.ROOT/'output/map-art-028') as d:
            p=Path(d);rows={}
            for name in REQUIRED:
                (p/name).write_bytes(b'{}');rows[name]=hashlib.sha256(b'{}').hexdigest()
            (p/'initial-state.json').write_bytes(b'{"modified":1}')
            with self.assertRaisesRegex(ValueError,'CHANGED'):evidence(p,rows)
    def test_evidence_uses_verified_bytes(self):
        from map_art_accept import m
        with tempfile.TemporaryDirectory(dir=m.ROOT/'output/map-art-028') as d:
            p=Path(d);rows={}
            for name in REQUIRED:
                (p/name).write_bytes(b'{}');rows[name]=hashlib.sha256(b'{}').hexdigest()
            checked=evidence(p,rows);(p/'initial-state.json').write_bytes(b'changed')
            self.assertEqual(checked['initial-state.json'],b'{}')
    def test_truncated_party(self):
        for a,z in [([],[]),([{}]*4,[{}]*3),([{}]*5,[{}]*4)]:
            with self.assertRaisesRegex(ValueError,'FOUR_PARTY'):four_party({'party':a},{'party':z})
    def test_four_party(self):four_party({'party':[{}]*4},{'party':[{}]*4})
if __name__=='__main__':unittest.main()

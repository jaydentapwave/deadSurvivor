import hashlib,tempfile,unittest
from copy import deepcopy
from datetime import datetime
from fractions import Fraction as F
from pathlib import Path
import map_topology as h
import map_topology_suite as s

def box():return tuple((F(x),F(y)) for x,y in [(-2,-2),(2,-2),(2,2),(-2,2)])
class FinalizationTests(unittest.TestCase):
    def test_late_failure_clears_accepted_values(self):
        result=dict(status='BLOCKED',bounded_complement_components=1,p01_safe=True,geometric_connectivity='PASS',completion_marker='SEALED')
        h.invalidate_acceptance(result)
        self.assertIsNone(result['bounded_complement_components']);self.assertIsNone(result['p01_safe']);self.assertEqual(result['geometric_connectivity'],'BLOCKED');self.assertEqual(result['unaccepted']['bounded_complement_components'],1);self.assertEqual(result['completion_marker'],'INCOMPLETE')
        h.invalidate_acceptance(result);self.assertEqual(result['unaccepted']['bounded_complement_components'],1)
    def test_certified_disconnection_retained(self):
        result=dict(status='PASS',bounded_complement_components=2,p01_safe=True,geometric_connectivity='FAIL')
        h.invalidate_acceptance(result);self.assertEqual(result['bounded_complement_components'],2);self.assertEqual(result['geometric_connectivity'],'FAIL')
class MatrixTests(unittest.TestCase):
    def rows(self):return [dict(case_id=id,certification_status='PASS',connectivity_status='PASS') for id in s.EXPECTED]
    def test_empty_and_missing_never_pass(self):
        for rows in ([],self.rows()[:-1]):self.assertEqual(s.aggregate(rows)['status'],'NOT_RUN')
    def test_duplicate_rejected(self):
        with self.assertRaisesRegex(ValueError,'DUPLICATE'):s.aggregate(self.rows()+self.rows()[:1])
    def test_unknown_status(self):
        rows=self.rows();rows[0]['connectivity_status']='PESS'
        with self.assertRaisesRegex(ValueError,'UNKNOWN'):s.aggregate(rows)
    def test_certified_not_connected_is_failure(self):
        rows=self.rows();rows[0]['connectivity_status']='FAIL';result=s.aggregate(rows)
        self.assertEqual(result['certification_status'],'PASS');self.assertEqual(result['status'],'FAIL')
    def test_explicit_not_run_is_not_pass(self):
        rows=self.rows();rows[0].update(certification_status='NOT_RUN',connectivity_status='NOT_RUN');self.assertEqual(s.aggregate(rows)['status'],'NOT_RUN')
    def test_complete(self):self.assertEqual(s.aggregate(self.rows())['status'],'PASS')
class StructuralTests(unittest.TestCase):
    def setUp(self):
        self.cells=[box() for _ in range(4)];self.raw=h.nerve(self.cells,F(1),h.Budget());self.certs=self.raw.pop('certificates')
    def check(self,raw=None,certs=None):return s.structural_check(self.cells,F(1),raw or self.raw,certs if certs is not None else self.certs,h.Budget(seconds=10))
    def test_independent_rank_and_graph(self):self.assertEqual(self.check()['bounded_complement_components'],0)
    def test_missing_pair_with_compensated_totals(self):
        certs=self.certs[1:];raw=deepcopy(self.raw);raw['pair_candidates']-=1;raw['pair_excluded']+=1
        with self.assertRaisesRegex(ValueError,'PAIR_COMPLETENESS'):self.check(raw,certs)
    def test_missing_triangle(self):
        certs=[r for r in self.certs if r['cells']!=[0,1,2]]
        with self.assertRaisesRegex(ValueError,'CLIQUE_COMPLETENESS'):self.check(certs=certs)
    def test_duplicate_incidence(self):
        with self.assertRaisesRegex(ValueError,'DUPLICATE_INCIDENCE'):self.check(certs=self.certs+self.certs[:1])
    def test_malformed_incidence_is_structured_failure(self):
        for bad in (None,{},[None],[3],['row']):
            with self.assertRaisesRegex(ValueError,'INCIDENCE'):self.check(certs=bad if bad is not None else {})
    def test_deadline_checked_during_pair_enumeration(self):
        budget=h.Budget(seconds=-1)
        with self.assertRaises(h.TopologyBlocked):s.structural_check(self.cells,F(1),self.raw,self.certs,budget)
    def test_false_rank(self):
        raw=deepcopy(self.raw);raw['boundary2_rank']-=1
        with self.assertRaisesRegex(ValueError,'HOMOLOGY_MISMATCH'):self.check(raw=raw)
    def test_unresolved_cannot_be_sealed(self):
        certs=deepcopy(self.certs);certs[0]['nonempty']=None
        with self.assertRaisesRegex(ValueError,'UNRESOLVED'):self.check(certs=certs)
class EvidenceTests(unittest.TestCase):
    def test_wrong_launch_nonce(self):
        row=dict(case_id='N1-F0',launch_nonce='a'*32);manifest=dict(suite_id='b'*32,created_at=datetime.now().astimezone().isoformat())
        result=dict(invocation={},worker_pid=1,started_at=manifest['created_at'])
        with self.assertRaisesRegex(ValueError,'INVOCATION'):s.validate_invocation(result,row,manifest,'c'*64,1)
    def test_missing_and_modified_artifact(self):
        with tempfile.TemporaryDirectory(dir=s.ROOT/'output/map-full-026') as directory:
            root=Path(directory);rows={}
            for name in s.ARTIFACTS:
                (root/name).write_bytes(b'[]');rows[name]=dict(bytes=2,sha256=hashlib.sha256(b'[]').hexdigest())
            self.assertEqual(len(s.artifact_bytes(root,rows)),3)
            with self.assertRaisesRegex(ValueError,'ARTIFACT_SET'):s.artifact_bytes(root,{})
            (root/'incidences.json').write_bytes(b'{}')
            with self.assertRaisesRegex(ValueError,'ARTIFACT_HASH'):s.artifact_bytes(root,rows)
    def test_final_manifest_and_launch_stability(self):
        with tempfile.TemporaryDirectory(dir=s.ROOT/'output/map-full-026') as directory:
            root=Path(directory);manifest=root/'manifest.json';launch=root/'launch.json'
            manifest.write_bytes(b'{}');launch.write_bytes(b'{}');sha=s.digest(manifest)
            launches={str(launch.relative_to(s.ROOT)):s.digest(launch)}
            s.final_evidence_check(manifest,sha,launches,[])
            launch.write_bytes(b'[]')
            with self.assertRaisesRegex(ValueError,'LAUNCH_CHANGED'):s.final_evidence_check(manifest,sha,launches,[])
            manifest.write_bytes(b'[]')
            with self.assertRaisesRegex(ValueError,'MANIFEST_CHANGED'):s.final_evidence_check(manifest,sha,{},[])
    def test_final_child_stability(self):
        with tempfile.TemporaryDirectory(dir=s.ROOT/'output/map-full-026') as directory:
            root=Path(directory);manifest=root/'manifest.json';manifest.write_bytes(b'{}');report=root/'summary.json';report.write_bytes(b'{}')
            row=dict(certification_status='PASS',report=str(report.relative_to(s.ROOT)),summary_sha256=s.digest(report),artifacts={})
            report.write_bytes(b'[]')
            with self.assertRaisesRegex(ValueError,'CHILD_EVIDENCE_CHANGED'):s.final_evidence_check(manifest,s.digest(manifest),{},[row])
    def test_output_no_overwrite(self):
        with tempfile.TemporaryDirectory(dir=s.ROOT/'output/map-full-026') as directory:
            p=Path(directory)/'result.json';s.atomic(p,{'state':1})
            with self.assertRaisesRegex(ValueError,'OUTPUT_EXISTS'):s.atomic(p,{'state':2})
            self.assertEqual(s.load_json(p),{'state':1})
if __name__=='__main__':unittest.main()

"""Analytic topology and exact-contact counterexamples for 027."""
from fractions import Fraction as F
import unittest
import json
from pathlib import Path
import tempfile
from copy import deepcopy
from map_io import MapInputError

import map_topology as h


def poly(rows):return tuple(tuple(F(v) for v in p) for p in rows)
def box(a,b,c,d):return poly([(a,b),(c,b),(c,d),(a,d)])

class ExactCoverTests(unittest.TestCase):
    def test_concave_inside_outside_cut_lines(self):
        p=poly([(-5,-5),(5,-5),(5,5),(0,5),(0,0),(-5,0)]);frame=(F(-10),F(-10),F(10),F(10))
        for outside in (False,True):
            budget=h.Budget(seconds=30);cells=h.decompose(p,frame,budget,outside)
            h.verify_cover(p,frame,cells,budget,outside)
            self.assertEqual(any(h.convex_contains(c,(F(-2),F(2))) for c in cells),outside)
    def test_omitted_cell_fails(self):
        p=box(-5,-5,5,5);frame=(F(-10),F(-10),F(10),F(10));b=h.Budget()
        with self.assertRaisesRegex(ValueError,'EXACT_COVER_MISMATCH'):h.verify_cover(p,frame,[],b)
    def test_unowned_diagonal_fails(self):
        p=box(-5,-5,5,5);frame=(F(-10),F(-10),F(10),F(10));b=h.Budget()
        with self.assertRaisesRegex(ValueError,'CELL_EDGE_NOT_SOURCE'):h.verify_cover(p,frame,[poly([(-5,-5),(5,-4),(5,5),(-5,5)])],b)
    def test_closed_point_segment_clipping(self):
        b=h.Budget();a=box(-1,-1,0,1);z=box(0,-1,1,1)
        self.assertEqual(set(h.clip(a,z,b)),{(F(0),F(-1)),(F(0),F(1))})
        result=h.clip(h.clip(a,z,b),box(0,1,2,2),b)
        self.assertEqual(result,((F(0),F(1)),))
    def test_thin_feature_not_erased(self):
        p=box(F(0),F(0),F(1,10000),F(2));frame=(F(-1),F(-1),F(1),F(3));b=h.Budget()
        cells=h.decompose(p,frame,b);self.assertTrue(any(h.convex_contains(c,(F(1,20000),F(1))) for c in cells))
    def test_actual_epsilon_representation(self):
        self.assertGreater(F(h.f32(.001)),F(1,1000))
    def test_nested_disk_bounds(self):
        small_in,small_out=h.disk_bounds(8);large_in,large_out=h.disk_bounds(16)
        self.assertTrue(all(h.convex_contains(large_in,p) for p in small_in))
        self.assertTrue(all(h.convex_contains(small_out,p) for p in large_out))
        self.assertTrue(all(x*x+y*y==1 for x,y in large_in))

class IntakeAndResourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.layout=json.loads((h.ROOT/'docs/design/map-meadow-007-r001-layout.json').read_text())
    def test_layout_identity(self):
        h.validate_layout(self.layout)
        for field,value in [('revision','r999'),('status','ACCEPTED'),('coordinate','world')]:
            data=deepcopy(self.layout);data[field]=value
            with self.assertRaises(ValueError):h.validate_layout(data)
    def test_missing_boundary_and_origin(self):
        for field in ('boundary','origin_px'):
            data=deepcopy(self.layout);data.pop(field)
            with self.assertRaises(ValueError):h.validate_layout(data)
    def test_malformed_and_huge_coordinate(self):
        for value in (None,True,10**400,float('inf')):
            data=deepcopy(self.layout);data['boundary']['points_px'][0][0]=value
            with self.assertRaises(ValueError):h.validate_layout(data)
    def test_linked_output_parent_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();outside=root/'outside';outside.mkdir();(root/'project').mkdir();(root/'project/output').symlink_to(outside)
            with self.assertRaises(MapInputError):h.fresh_output(root/'project')
            self.assertEqual(list(outside.iterdir()),[])
    def test_output_limit_preserves_partial_only(self):
        with tempfile.TemporaryDirectory() as temp:
            path=Path(temp).resolve()/'large.json';b=h.Budget(max_output_bytes=16384+32)
            with self.assertRaisesRegex(h.TopologyBlocked,'OUTPUT_LIMIT'):h.bounded_write(path,{'rows':['x'*100]},b)
            self.assertFalse(path.exists());self.assertTrue(path.with_suffix('.json.pending').exists())
    def test_hull_and_minkowski_deadline(self):
        for operation in (lambda b:h.hull(poly([(0,0),(1,0),(0,1)]),b),lambda b:h.minkowski(poly([(0,0)]),h.disk_bounds(8)[0],F(1),b)):
            with self.assertRaises(h.TopologyBlocked):operation(h.Budget(seconds=-1))
    def test_unresolved_output_failure_keeps_compact_summary(self):
        with tempfile.TemporaryDirectory() as temp:
            out=Path(temp).resolve();budget=h.Budget(max_output_bytes=16384+16)
            result={'status':'BLOCKED','certificates':[{'witness':'x'*1000}],'unresolved':[{'band':'x'*1000} for _ in range(100)]}
            try:h.store_analysis(out,result,{'trial':'unit'},budget)
            except h.TopologyBlocked as error:result.update(status='BLOCKED',error=str(error))
            h.bounded_write(out/'summary.json',result,budget,summary=True)
            saved=json.loads((out/'summary.json').read_text());self.assertEqual(saved['status'],'BLOCKED');self.assertEqual(saved['unresolved_count'],100);self.assertNotIn('unresolved',saved)
    def test_runtime_profile_and_catalog_mismatch(self):
        bundle=json.loads((h.ROOT/'docs/design/map-topology-runtime-profile-027.r003.json').read_text())
        h.verify_production_profile(self.layout,bundle)
        changed=deepcopy(bundle);changed['sources']['Assets/DeadSurvivor/Editor/DSMapMotionVerification.cs']='0'*64
        with self.assertRaisesRegex(ValueError,'PROFILE_SOURCE_CHANGED'):h.verify_production_profile(self.layout,changed)
        changed=deepcopy(bundle);changed['runtime']['radius']=9
        with self.assertRaisesRegex(ValueError,'RUNTIME_NUMERIC_PROFILE'):h.verify_production_profile(self.layout,changed)
        changed=deepcopy(bundle);catalog=json.loads(changed['runtime']['catalog']);catalog['entries'][0]['offsets'][0]['x']=1;changed['runtime']['catalog']=json.dumps(catalog)
        with self.assertRaisesRegex(ValueError,'RUNTIME_CATALOG_MISMATCH'):h.verify_production_profile(self.layout,changed)
    def test_disappearing_source_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();p=root/'source';p.write_text('frozen');inputs={'source':h.hashlib.sha256(p.read_bytes()).hexdigest()};p.unlink()
            with self.assertRaises((MapInputError,OSError)):h.revalidate_sources(root,inputs)
    def test_nonconvex_cell_rejected(self):
        with self.assertRaisesRegex(ValueError,'NONCONVEX'):h.nerve([poly([(0,0),(2,0),(1,1),(2,2),(0,2)])],F(1),h.Budget())

class NerveTests(unittest.TestCase):
    def test_three_discs_have_hole(self):
        cells=[poly([(0,0)]),poly([(3,0)]),poly([(F(3,2),F(5,2))])]
        r=h.nerve(cells,F(8,5),h.Budget(seconds=30))
        self.assertEqual(r['status'],'PASS');self.assertEqual(r['bounded_complement_components'],1);self.assertEqual(r['triangles'],0)
    def test_triple_intersection_fills_hole(self):
        cells=[poly([(0,0)]),poly([(3,0)]),poly([(F(3,2),F(5,2))])]
        r=h.nerve(cells,F(2),h.Budget(seconds=30))
        self.assertEqual(r['status'],'PASS');self.assertEqual(r['bounded_complement_components'],0);self.assertEqual(r['triangles'],1)
    def test_base_intersection_witness_is_exact(self):
        cells=[box(-2,-2,2,2),box(0,0,4,4)]
        proof=h.certify_intersection(cells,F(1),h.Budget(),{},(0,1))
        self.assertTrue(proof['nonempty']);self.assertEqual(proof['resolution'],0)
        witness=tuple(F(x) for x in proof['witness']);self.assertTrue(all(h.convex_contains(c,witness) for c in cells))
    def test_p01_inside_and_exact_clearance_contact(self):
        cells=[box(-1,-1,1,1)]
        self.assertTrue(h.point_forbidden(cells,F(1),(F(2),F(0))))
        self.assertFalse(h.point_forbidden(cells,F(1),(F(2001,1000),F(0))))
    def test_axis_tangency_is_nonempty_not_discarded(self):
        r=h.nerve([poly([(0,0)]),poly([(2,0)])],F(1),h.Budget())
        self.assertEqual(r['status'],'PASS');self.assertEqual(r['edges'],1)
    def test_unresolved_tangency_stays_blocked(self):
        r=h.nerve([poly([(0,0)]),poly([(F(6,5),F(8,5))])],F(1),h.Budget(seconds=30),levels=(8,16))
        self.assertEqual(r['status'],'BLOCKED');self.assertIsNone(r['bounded_complement_components'])
    def test_tetrahedron_2skeleton_has_no_h1(self):
        r=h.homology(4,[(0,1),(0,2),(0,3),(1,2),(1,3),(2,3)],[(0,1,2),(0,1,3),(0,2,3),(1,2,3)],h.Budget())
        self.assertEqual(r['boundary2_rank'],3);self.assertEqual(r['bounded_complement_components'],0)
    def test_detached_obstacles_keep_beta0(self):
        r=h.nerve([box(-5,-1,-4,1),box(4,-1,5,1)],F(1,10),h.Budget())
        self.assertEqual(r['beta0'],2);self.assertEqual(r['bounded_complement_components'],0)
    def test_deadline_blocks(self):
        with self.assertRaises(h.TopologyBlocked):h.nerve([poly([(0,0)]),poly([(2,0)])],F(1),h.Budget(seconds=-1))
    def test_pair_budget_blocks_not_empty(self):
        with self.assertRaisesRegex(h.TopologyBlocked,'PAIR_LIMIT'):h.nerve([poly([(0,0)]),poly([(1,0)])],F(1),h.Budget(max_pairs=0))
    def test_frame_free_space_one_and_split_two(self):
        frame=(F(-14),F(-14),F(14),F(14));p=box(-10,-10,10,10)
        budget=h.Budget(seconds=60);outside=h.decompose(p,frame,budget,True)
        r=h.nerve(outside,F(1),budget)
        self.assertEqual(r['status'],'PASS');self.assertEqual(r['bounded_complement_components'],1)
        wall=h.decompose(box(-12,F(-1,2),12,F(1,2)),frame,budget)
        r=h.nerve(outside+wall,F(1),budget)
        self.assertEqual(r['status'],'PASS');self.assertEqual(r['bounded_complement_components'],2)

if __name__=='__main__':unittest.main()

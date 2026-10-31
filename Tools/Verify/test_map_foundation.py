"""Analytic counterexamples and contract tests; never modifies Unity."""
import copy
import contextlib
from fractions import Fraction
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from map_io import MapInputError, strict_loads, safe_relative_path, load_json, read_input_bytes
from map_oracle import Geometry, assess_sweep, sqrt_interval
from map_check import foundation_check, FIXTURES

BOUNDARY = [[-500, -500], [500, -500], [500, 500], [-500, 500]]
WALL = [[3.4, -20], [3.6, -20], [3.6, 20], [3.4, 20]]


class InputTests(unittest.TestCase):
    def test_bad_json(self):
        for text in ('{"x":1,"x":2}', '[NaN]', '[Infinity]', '[-Infinity]',
                     '{"x":1e999}', '{', '[0,]'):
            with self.subTest(text=text), self.assertRaises(MapInputError):
                strict_loads(text)
        self.assertEqual(strict_loads('{"x":[1,0.5,true,null]}'), {'x': [1, .5, True, None]})

    def test_contained_paths_and_symlinks(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            (root/'good').write_text('{}')
            (root/'link').symlink_to(root/'good')
            (root/'dirlink').symlink_to(root, target_is_directory=True)
            self.assertEqual(safe_relative_path(root, 'good'), root/'good')
            for value in ('../good', '/etc/passwd', 'a/../good', './good', 'a//good',
                          'a\\good', 'C:good', 'link', 'dirlink/good', '', None):
                with self.subTest(value=value), self.assertRaises(MapInputError):
                    safe_relative_path(root, value)
            with self.assertRaises(MapInputError):
                load_json(root/'link')
            with self.assertRaises(MapInputError):
                load_json(root/'good', max_bytes=1)

    def test_missing_file_is_blocked(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaises(MapInputError) as ctx:
                safe_relative_path(Path(folder).resolve(), 'missing')
            self.assertEqual(ctx.exception.status, 'BLOCKED')

    def test_duplicate_missing_or_empty_fixture_ids(self):
        data = load_json(FIXTURES)
        variants = [dict(data, cases=[]), dict(data, oracle_vectors=[]),
                    dict(data, cases=data['cases'] + [data['cases'][0]]),
                    dict(data, schema_version=True), dict(data, schema_version=2), [], None]
        with tempfile.TemporaryDirectory() as folder:
            p = Path(folder).resolve()/'fixtures.json'
            for value in variants:
                p.write_text(json.dumps(value))
                self.assertEqual(foundation_check(p)['status'], 'FAIL')

    def test_local_check_runs_oracle_and_propagates_failure(self):
        import run
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder).resolve()
            for state, code in [('PASS',0),('FAIL',1),('BLOCKED',2)]:
                with patch.object(run,'ROOT',root), patch.object(run,'fingerprint',return_value={}), \
                     patch.object(run,'documents',return_value={'status':'PASS'}), \
                     patch.object(run,'data_check',return_value={'status':'PASS'}), \
                     patch.object(run,'map_oracle_foundation',return_value={'status':state}) as check, \
                     patch('sys.argv',['run.py','local']), contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(run.main(),code)
                    check.assert_called_once_with(root)

    def test_failed_check_is_not_downgraded_by_later_blocker(self):
        data=load_json(FIXTURES)
        data['oracle_vectors'][0]['expected']='PASS'
        data['oracle_vectors'][1]['epsilon']=1e-20
        with tempfile.TemporaryDirectory() as folder:
            p=Path(folder).resolve()/'fixture.json';p.write_text(json.dumps(data))
            result=foundation_check(p)
            self.assertEqual(result['checks'][0]['check_status'],'FAIL')
            self.assertEqual(result['status'],'FAIL')
            self.assertEqual(result['errors'][0]['code'],'NUMERICAL_RANGE')

    def test_hashing_rejects_symlink_before_read_and_bounds_size(self):
        from map_check import fingerprints
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder).resolve();p=root/'source';p.write_text('private')
            link=root/'link';link.symlink_to(p)
            with patch.object(Path,'open',side_effect=AssertionError('must not read target')):
                with self.assertRaises(MapInputError) as ctx:
                    fingerprints([link],root)
            self.assertEqual(ctx.exception.code,'SYMLINK')
            p.write_bytes(b'x'*(1024*1024+1))
            with self.assertRaises(MapInputError) as ctx:
                fingerprints([p],root)
            self.assertEqual(ctx.exception.code,'INPUT_LIMIT')

    def test_cli_input_rejection_persists_report(self):
        import map_check
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder).resolve()
            errors=[MapInputError('SYMLINK','test'),MapInputError('INPUT_UNAVAILABLE','test','BLOCKED')]
            for error in errors:
                captured=io.StringIO()
                with patch.object(map_check,'ROOT',root), \
                     patch.object(map_check,'fingerprints',side_effect=error), \
                     contextlib.redirect_stdout(captured):
                    code=map_check.main(['oracle-selftest'])
                value=json.loads(captured.getvalue())
                report=load_json(value['report'])
                self.assertEqual(report['status'],error.status)
                self.assertEqual(report['errors'][0]['code'],error.code)
                self.assertEqual(code,1 if error.status=='FAIL' else 2)


class OracleTests(unittest.TestCase):
    def assess(self, start, end, radius=.25, obstacles=None, boundary=None, epsilon=.0001):
        g = Geometry.from_polygons(BOUNDARY if boundary is None else boundary,
                                  [WALL] if obstacles is None else obstacles)
        return assess_sweep(g, start, end, radius, epsilon)

    def test_design_vectors_and_cli_analysis(self):
        result = foundation_check()
        self.assertEqual(result['status'], 'PASS', result)
        self.assertEqual(result['unity_status'], 'NOT_RUN')
        self.assertEqual(len(result['checks']), 8)

    def test_crossing_with_both_endpoints_safe(self):
        self.assertEqual(self.assess([0, 0], [0, 0])['status'], 'PASS')
        self.assertEqual(self.assess([4.2, 0], [4.2, 0])['status'], 'PASS')
        result = self.assess([0, 0], [4.2, 0])
        self.assertEqual(result['status'], 'FAIL')
        self.assertEqual(result['clearance_kind'], 'witness_bound')
        self.assertIn('point_exact', result['witness'])

    def test_body_overlap_when_center_is_outside(self):
        result = self.assess([3.3, 0], [3.3, 0])
        self.assertEqual(result['status'], 'FAIL')
        self.assertAlmostEqual(result['body_clearance'], -.15)

    def test_stationary_inside_and_outside_boundary(self):
        for position, expected_gap in [([3.5, 0], -.35), ([500.1, 0], -.35),
                                       ([499.9, 0], -.15)]:
            result = self.assess(position, position)
            self.assertEqual(result['status'], 'FAIL')
            self.assertAlmostEqual(result['body_clearance'], expected_gap)

    def test_concave_boundary_leave_and_reenter(self):
        u = [[-3,-3],[3,-3],[3,3],[1,3],[1,-1],[-1,-1],[-1,3],[-3,3]]
        self.assertEqual(self.assess([-2,2], [-2,2], obstacles=[], boundary=u)['status'], 'PASS')
        self.assertEqual(self.assess([2,2], [2,2], obstacles=[], boundary=u)['status'], 'PASS')
        self.assertEqual(self.assess([-2,2], [2,2], obstacles=[], boundary=u)['status'], 'FAIL')

    def test_concave_obstacle_empty_notch(self):
        l = [[20,-20],[40,-20],[40,20],[30,20],[30,-10],[20,-10]]
        result = self.assess([25,0], [25,0], radius=1, obstacles=[l])
        self.assertEqual(result['status'], 'PASS')
        self.assertAlmostEqual(result['body_clearance'], 4)

    def test_parallel_touch_and_safe_control(self):
        square = [[0,0],[1,0],[1,1],[0,1]]
        for y, status in [(1.25,'BLOCKED'), (1.251,'PASS'), (1.249,'FAIL')]:
            self.assertEqual(self.assess([-2,y], [2,y], obstacles=[square])['status'], status)

    def test_corner_touch(self):
        square = [[0,0],[1,0],[1,1],[0,1]]
        result = self.assess([-1,-1], [-1,-1], radius=2**.5, obstacles=[square])
        self.assertEqual(result['status'], 'BLOCKED')

    def test_overlapping_obstacles_and_narrow_corridor(self):
        a = [[-10,-10],[-1,-10],[-1,10],[-10,10]]
        b = [[1,-10],[10,-10],[10,10],[1,10]]
        self.assertEqual(self.assess([0,-5],[0,5], radius=.9, obstacles=[a,b])['status'], 'PASS')
        self.assertEqual(self.assess([0,-5],[0,5], radius=1.1, obstacles=[a,b])['status'], 'FAIL')
        self.assertEqual(self.assess([0,0],[4.2,0], obstacles=[WALL,WALL])['status'], 'FAIL')

    def test_reverse_rotate_winding_and_start_vertex(self):
        rotate = lambda p: [-p[1], p[0]]
        for a,b in [([0,0],[3.1,0]),([0,0],[4.2,0]),([3.1499,0],[3.1499,0])]:
            expected = self.assess(a,b)
            variants = [self.assess(b,a), self.assess(a,b, obstacles=[WALL[::-1]]),
                        self.assess(a,b, obstacles=[WALL[2:]+WALL[:2]]),
                        self.assess(rotate(a), rotate(b), obstacles=[[rotate(p) for p in WALL]],
                                    boundary=[rotate(p) for p in BOUNDARY])]
            for result in variants:
                self.assertEqual(result['status'], expected['status'])
                if result['clearance_kind'] == 'minimum_clearance':
                    self.assertAlmostEqual(result['body_clearance'], expected['body_clearance'])

    def test_analytic_rectangle_grid(self):
        # Independent closed-form gap for stationary circles outside one axis-aligned box.
        box = [[0,0],[10,0],[10,10],[0,10]]
        for x in [-2,-.251,-.25,-.249,0,5,10,10.249,10.25,10.251,12]:
            for y in [-2,5,12]:
                dx,dy=max(0,-x,x-10),max(0,-y,y-10)
                distance=(dx*dx+dy*dy)**.5 if dx or dy else -min(x,y,10-x,10-y)
                gap=distance-.25
                status='FAIL' if gap < -.0001 else 'PASS' if gap > .0001 else 'BLOCKED'
                result=self.assess([x,y],[x,y],obstacles=[box])
                self.assertEqual(result['status'],status,(x,y,gap,result))
                self.assertAlmostEqual(result['body_clearance'],gap)

    def test_invalid_polygons_and_unsupported_tiny_edges(self):
        invalid = [[], [[0,0],[1,1]], [[0,0],[1,0],[1,1],[0,0]],
                   [[0,0],[2,2],[0,2],[2,0]], [[0,0],[1,0],[2,0]],
                   [[0,0],[2,0],[1,0],[1,1],[0,1]], [[0,0],[True,0],[1,1]],
                   [[0,0],[1,0],[float('nan'),1]]]
        for poly in invalid:
            with self.subTest(poly=poly), self.assertRaises(MapInputError):
                Geometry.from_polygons(poly, [])
        tiny = [[0,0],[1e-200,0],[1e-200,1],[0,1]]
        with self.assertRaises(MapInputError) as ctx:
            Geometry.from_polygons(tiny, [])
        self.assertEqual(ctx.exception.status,'BLOCKED')

    def test_invalid_or_unsupported_query(self):
        for r,e in [(0,.001),(-1,.001),(True,.001),(.25,0),(.25,float('inf')), (None,.001)]:
            with self.assertRaises(MapInputError):
                self.assess([0,0],[1,0],radius=r,epsilon=e)
        for a,b,r,e in [([1025,0],[0,0],.25,.0001),([0,0],[1e-200,0],.25,.0001),
                        ([0,0],[1,0],1e-20,.0001),([0,0],[1,0],.25,1e-20)]:
            with self.assertRaises(MapInputError) as ctx:
                self.assess(a,b,radius=r,epsilon=e)
            self.assertEqual(ctx.exception.status,'BLOCKED')

    def test_certified_interval_extremes(self):
        for q in [Fraction(0),Fraction(1,10),Fraction(2),Fraction(1,10**650),Fraction(10**6)]:
            lo,hi=sqrt_interval(q)
            self.assertLessEqual(lo*lo,q)
            self.assertGreaterEqual(hi*hi,q)
            self.assertGreaterEqual(lo,0)

    def test_no_mutation_and_hashes(self):
        before=copy.deepcopy(WALL)
        g=Geometry.from_polygons(BOUNDARY,[WALL])
        r=assess_sweep(g,[0,0],[3.1,0],.25,.0001)
        self.assertEqual(WALL,before)
        self.assertEqual(r['geometry_sha256'],g.normalized_sha256)
        self.assertEqual(len(g.input_sha256),64)


if __name__ == '__main__':
    unittest.main()

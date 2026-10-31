from copy import deepcopy
import json
from pathlib import Path
import unittest
import tempfile
from unittest.mock import patch

from map_blockout import compile_layout, assess, audit_prism, xy, area2, v, LAYOUT, judge_motion, check_hash_rows, build_recovery_plan
from map_oracle import assess_sweep
from map_blockout import normalized_geometry
from map_blockout import capture_dimensions


class BlockoutTests(unittest.TestCase):
    def test_capture_requires_identical_native_dimensions(self):
        import struct
        png=b'\x89PNG\r\n\x1a\n'+struct.pack('>I',13)+b'IHDR'+struct.pack('>II',3088,1440)
        camera=dict(width=3088,height=1440)
        self.assertEqual(capture_dimensions(camera,camera,png)['pixel_mapping'],'IDENTITY_NATIVE_PIXELS')
        with self.assertRaisesRegex(RuntimeError,'CAMERA_CAPTURE_SIZE_MISMATCH'):capture_dimensions(dict(width=1280,height=720),camera,png)
        with self.assertRaisesRegex(RuntimeError,'CAMERA_CAPTURE_SIZE_MISMATCH'):capture_dimensions(camera,dict(width=1280,height=720),png)
        with self.assertRaisesRegex(RuntimeError,'INVALID_CAPTURE_PNG'):capture_dimensions(camera,camera,b'bad')
        with self.assertRaisesRegex(RuntimeError,'UNSUPPORTED_NATIVE_CAPTURE_SIZE'):capture_dimensions(dict(width=5000,height=1440),camera,png)
    def source(self):return json.loads(LAYOUT.read_text())
    def test_exact_source_set_and_points(self):
        src=self.source();data=compile_layout(src)
        self.assertEqual(len(data['objects']),64)
        self.assertEqual(set(data['skipped_noncolliding']),{'OB-B01','OB-B02','OB-B03','OB-B04','OB-G01'})
        original={o['id']:o for o in src['objects']}
        for obj in data['objects']:
            self.assertEqual([xy(p) for p in obj['points_px']],original[obj['id']]['C']['points_px'])
        self.assertEqual([xy(p) for p in data['boundary_px']],src['boundary']['points_px'])
    def test_duplicate_source_id_rejected(self):
        src=self.source();src['objects'][1]['id']=src['objects'][0]['id']
        with self.assertRaisesRegex(ValueError,'SOURCE_OBJECT_SET'):compile_layout(src)
    def test_preview_points_do_not_change_legacy_limit(self):
        self.assertEqual(xy(v([5040,4240])),[5040,4240])
        from map_oracle import point
        with self.assertRaises(ValueError):point([5040,4240])
        for p in [v([float('nan'),0]),v([16385,0]),{'x':True,'y':0}]:
            with self.assertRaises(ValueError):xy(p)
    def test_initial_pose_all_scale_candidates(self):
        data=compile_layout(self.source())
        for s in [4,6,8]:
            for p in [[0,0],[-24,-24],[24,-24],[-24,24],[24,24]]:
                self.assertEqual(assess(data,p,p,s)['status'],'PASS')
    def test_broadphase_matches_full_oracle(self):
        data=compile_layout(self.source());geom=normalized_geometry(data)
        for a,b in [([0,0],[2,0]),([-340,-410],[-340,-410]),([500,300],[500,305])]:
            expected=assess_sweep(geom,a,b,2,.00025)
            got=assess(data,[x*4 for x in a],[x*4 for x in b],4)
            self.assertEqual(expected['status'],got['status'])
    def test_runtime_two_epsilon_policy(self):
        data=dict(origin_px=v([0,0]),boundary_px=[v(p) for p in [[-100,-100],[100,-100],[100,100],[-100,100]]],
                  objects=[dict(points_px=[v(p) for p in [[0,-50],[10,-50],[10,50],[0,50]]])],epsilon=.1)
        full=assess_sweep(normalized_geometry(data),[-8.15,0],[-8.15,0],8,.1)
        self.assertEqual(full['status'],'PASS')
        self.assertEqual(assess(data,[-8.15,0],[-8.15,0],1)['status'],'BLOCKED')

    def mesh(self,caps=None):
        p=[[0,0],[3,0],[3,1],[1,1],[1,3],[0,3]];n=6
        caps=caps or [[0,1,3],[1,2,3],[0,3,5],[3,4,5]]
        vertices=[dict(x=x,y=y,z=-q) for y in [0,2] for x,q in p];tri=[]
        for a,b,c in caps:tri.extend([c,b,a,a+n,b+n,c+n])
        for i in range(n):
            j=(i+1)%n;k=len(vertices)
            vertices.extend([dict(x=p[t][0],y=y,z=-p[t][1]) for t,y in [(i,0),(j,0),(j,2),(i,2)]])
            tri.extend([k,k+1,k+2,k,k+2,k+3])
        return p,vertices,tri
    def test_L_prism_exact_independent_fixture(self):
        p,v,t=self.mesh();result=audit_prism(p,v,t,2,1,1,baked=1)
        self.assertEqual(result['triangles'],20);self.assertAlmostEqual(result['volume'],10)
    def test_wrong_fan_can_match_signed_area_but_is_rejected(self):
        caps=[[1,2,3],[1,3,4],[1,4,5],[1,5,0]];p,v,t=self.mesh(caps)
        self.assertEqual(sum(area2([p[i] for i in row]) for row in caps),area2(p))
        with self.assertRaisesRegex(ValueError,'(TOP|BOTTOM)_WINDING'):audit_prism(p,v,t,2,1,1,baked=1)
    def test_overlapping_caps_cannot_hide_behind_area_sum(self):
        p,v,t=self.mesh([[0,1,3],[1,2,3],[0,1,3],[3,4,5]])
        with self.assertRaisesRegex(ValueError,'CAP_TRIANGLES_OVERLAP'):audit_prism(p,v,t,2,1,1,baked=1)
    def test_footprint_change_rejected(self):
        p,v,t=self.mesh();v[0]['x']+=.01
        with self.assertRaisesRegex(ValueError,'MESH_FOOTPRINT_CHANGED'):audit_prism(p,v,t,2,1,1,baked=1)
    def test_height_change_rejected(self):
        p,v,t=self.mesh();v[6]['y']=3
        with self.assertRaisesRegex(ValueError,'MESH_HEIGHT'):audit_prism(p,v,t,2,1,1,baked=1)
    def test_duplicate_bottom_cap_with_same_area_and_volume_rejected(self):
        p,v,t=self.mesh()
        # Four bottom triangles still have total area5 and contribute zero signed volume.
        replacements=[[3,1,0],[3,1,0],[3,2,1],[3,2,1]]
        for i,row in enumerate(replacements):t[i*6:i*6+3]=row
        with self.assertRaisesRegex(ValueError,'BOTTOM_CAP_TRIANGLES_OVERLAP'):audit_prism(p,v,t,2,1,1,baked=1)
    def slow_trace(self):
        data=compile_layout(self.source());slots=[[-24,-24],[24,-24],[-24,24],[24,24]];center=[0,0];actual=deepcopy(slots);frames=[]
        for i in range(40):
            nxt=[center[0],center[1]+.875];target=[[nxt[j]+p[j] for j in (0,1)] for p in slots]
            after=[[a[j]+(p[j]-a[j])*.6 for j in (0,1)] for a,p in zip(actual,target)]
            frames.append(dict(step_id=i,frame_id=i+1,input=v([0,1]),actual_dt=.05,applied_dt=.05,alpha=.125,held=False,device_id=0,
                before_center=v(center),after_center=v(nxt),before_actual=[v(p) for p in actual],after_actual=[v(p) for p in after],
                before_reserved=[v([center[j]+p[j] for j in (0,1)]) for p in slots],after_reserved=[v(p) for p in target]))
            center=nxt;actual=after
        request=dict(run_id='r',nonce='n',case_id='false slow stop',scale=4,steps=40,center=v([0,0]),input=v([0,1]),device=False,reverse_at=-1,body_offsets=[v(p) for p in slots])
        raw=dict(scope='BLOCKOUT_PREVIEW_ONLY',run_id='r',nonce='n',case_id=request['case_id'],request_sha256='s',error='',scale=4,frames=frames)
        return data,request,raw
    def test_safe_slow_motion_is_not_wall_stop(self):
        data,request,raw=self.slow_trace()
        with self.assertRaisesRegex(ValueError,'UNJUSTIFIED_REDUCED_MOTION'):judge_motion(data,request,raw,'s')
    def test_reported_alpha_must_match_observation(self):
        data,request,raw=self.slow_trace();raw['frames'][0]['alpha']=1
        with self.assertRaisesRegex(ValueError,'ALPHA_OBSERVATION_MISMATCH'):judge_motion(data,request,raw,'s')
    def test_frozen_dependency_change_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp).resolve();p=root/'Rock.mat';p.write_text('changed')
            with self.assertRaisesRegex(RuntimeError,'FROZEN_INPUT_CHANGED'):check_hash_rows([dict(path='Rock.mat',sha256='0'*64)],root)
    def recovery(self, extra=None, changed=False, registered=True):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp).resolve();before=dict(Assets={'keep':'old'},Packages={},ProjectSettings={});now=deepcopy(before)
            if changed:now['Assets']['keep']='user change'
            if extra:now['Assets'].update(extra)
            a=root/'before.json';a.write_text(json.dumps(before));s=root/'snapshot.json';s.write_text(json.dumps({'scene':'original'}));j=root/'created.json'
            rows=[dict(path='Assets/'+k,sha256=value) for k,value in (extra or {}).items()]
            if registered:j.write_text(json.dumps(dict(files=rows)))
            marker=dict(before=str(a),snapshot=str(s),created_files=str(j))
            return build_recovery_plan(marker,now,{'scene':'original'},root)
    def test_no_mutation_build_failure_can_release_pending(self):self.assertEqual(self.recovery(),[])
    def test_confirmed_partial_build_can_recover(self):
        rows=self.recovery({'DeadSurvivor/Scenes/Map018_r001/Layout.json':'known'});self.assertEqual(len(rows),1)
    def test_unconfirmed_partial_build_is_preserved(self):
        with self.assertRaisesRegex(RuntimeError,'UNCONFIRMED_CREATED'):self.recovery({'DeadSurvivor/Scenes/Map018_r001/Layout.json':'unknown'},registered=False)
    def test_unrelated_user_change_blocks_build_recovery(self):
        with self.assertRaisesRegex(RuntimeError,'EXISTING_FILE_RECOVERY_CONFLICT'):self.recovery(changed=True)

    def test_completed_build_requires_frozen_separate_report(self):
        from map_motion_run import digest
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp).resolve();before=dict(Assets={'keep':'old'},Packages={},ProjectSettings={});now=deepcopy(before)
            rel='DeadSurvivor/Scenes/Map018_r001/Material.mat';now['Assets'][rel]='normalized'
            a=root/'before.json';a.write_text(json.dumps(before));s=root/'snapshot.json';s.write_text('{}')
            j=root/'created.json';j.write_text(json.dumps(dict(files=[dict(path='Assets/'+rel,sha256='initial')])))
            report=root/'build.json';report.write_text(json.dumps(dict(status='PASS',generated=[dict(path='Assets/'+rel,sha256='normalized')],rock=dict(dependencies=[]))))
            frozen=root/'input.json';frozen.write_text(json.dumps(dict(scope='COMPLETED_OWN_BUILD_ROLLBACK_ONLY',build_report=str(report),build_report_sha256=digest(report))))
            marker=dict(before=str(a),snapshot=str(s),created_files=str(j),output=str(root),folder_absent_before=True)
            journal_bytes=j.read_bytes()
            with self.assertRaisesRegex(RuntimeError,'CREATED_FILE_RECOVERY_CONFLICT'):build_recovery_plan(marker,now,{},root)
            with patch('map_blockout.rock_inputs',return_value=[]):
                self.assertEqual(build_recovery_plan(marker,now,{},root,frozen)[0]['sha256'],'normalized')
                self.assertEqual(j.read_bytes(),journal_bytes)
                report.write_text(report.read_text()+' ')
                with self.assertRaisesRegex(RuntimeError,'COMPLETED_BUILD_INPUT_CHANGED'):build_recovery_plan(marker,now,{},root,frozen)


if __name__=='__main__':unittest.main()

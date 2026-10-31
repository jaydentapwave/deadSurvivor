#!/usr/bin/env python3
"""Compile the existing 007 candidate layout and inspect a separate 3D preview."""
import argparse
from fractions import Fraction as F
import hashlib
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import struct
import time
import uuid
from datetime import datetime
from zoneinfo import ZoneInfo

import map_motion_run as motion
from map_io import load_json, strict_loads
from map_oracle import (Geometry, assess_sweep, polygon, point as oracle_point,
                        location, cross, sub, point_distance_squared)

ROOT, PROJECT = motion.ROOT, motion.PROJECT
BASE = ROOT/'output/map-blockout-018'
LAYOUT = ROOT/'docs/design/map-meadow-007-r001-layout.json'
FOLDER = 'Assets/DeadSurvivor/Scenes/Map018_r001'
SCENE = FOLDER+'/MapBlockout018_r001.unity'
PREFAB = FOLDER+'/MapBlockout018_r001.prefab'
ROCK_INPUT = ROOT/'docs/design/map-blockout-018-rock-input.json'


def v(p):
    return dict(x=p[0], y=p[1])


def xy(p):
    if type(p) is not dict or set(p)!={'x','y'} or any(type(x) not in (int,float) or not math.isfinite(x) or abs(x)>16384 for x in p.values()):
        raise ValueError('INVALID_PREVIEW_POINT')
    return [p['x'],p['y']]


def compile_layout(source):
    if source['package'] != 'MAP-MEADOW-007' or source['revision'] != 'r001' or source['origin_px'] != [565, 580]:
        raise ValueError('SOURCE_LAYOUT_IDENTITY')
    solids = [o for o in source['objects'] if (o.get('C') or {}).get('points_px')]
    if sorted(o['id'] for o in solids) != [f'OB-{n:03d}' for n in range(1, 65)] or source['boundary']['id'] != 'PERIM-01':
        raise ValueError('SOURCE_OBJECT_SET_MISMATCH')
    origin = source['origin_px']
    norm = lambda p: [p[0]-origin[0], p[1]-origin[1]]
    Geometry.from_polygons([norm(p) for p in source['boundary']['points_px']],
                          [[norm(p) for p in o['C']['points_px']] for o in solids])
    rock = next(o for o in solids if o['id'] == 'OB-001')['C']['points_px']
    center = [sum(p[i] for p in rock)/len(rock) for i in (0, 1)]
    poly = polygon([norm(p) for p in rock])
    if location(oracle_point(norm(center)), poly) != 1:
        raise ValueError('ROCK_CENTER_NOT_INSIDE')
    return dict(package='MAP-BLOCKOUT-018', revision='r001', document_role='PREVIEW_ONLY',
                source_layout_sha256=hashlib.sha256(LAYOUT.read_bytes()).hexdigest(),
                source_image_sha256=source['source_sha256'], origin_px=v(origin),
                boundary_px=[v(p) for p in source['boundary']['points_px']],
                objects=[dict(id=o['id'], points_px=[v(p) for p in o['C']['points_px']],
                              height_logical=16+8*(n%3), source_status=o['C']['status'], adoption='NOT_ACCEPTED') for n, o in enumerate(solids)],
                routes=[dict(id=r['id'], points_px=[v(p) for p in r['points_px']]) for r in source['routes']],
                rock_object_id='OB-001', rock_center_px=v(center),
                scale_profiles=[4, 6, 8], world_scale=.02, build_scale=4,
                body_radius=8, body_height=36, epsilon=.001,
                skipped_noncolliding=[o['id'] for o in source['objects'] if o not in solids],
                height_source='AGENT_TRIAL_NOT_OBSERVED', acceptance='NOT_ACCEPTED')


def prepare():
    BASE.mkdir(parents=True, exist_ok=True)
    data = compile_layout(load_json(LAYOUT))
    path = BASE/'compiled-layout-r001.json'
    text = json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False)+'\n'
    if path.exists() and path.read_text() != text:
        raise RuntimeError('COMPILED_REVISION_EXISTS_WITH_DIFFERENT_CONTENT')
    if not path.exists():
        path.write_text(text)
    return path, data


def check_hash_rows(rows, root=PROJECT):
    for row in rows:
        path=root/row['path']
        if not path.resolve().is_relative_to(root.resolve()) or not path.is_file() or motion.digest(path)!=row['sha256']:
            raise RuntimeError('FROZEN_INPUT_CHANGED:'+row['path'])


def rock_inputs():
    frozen=load_json(ROCK_INPUT)
    summary_path=ROOT/frozen['source_summary'];measurement_path=ROOT/frozen['measurement']
    if motion.digest(summary_path)!=frozen['source_summary_sha256'] or motion.digest(measurement_path)!=frozen['measurement_sha256']:
        raise RuntimeError('ROCK_PROVENANCE_CHANGED')
    summary=load_json(summary_path);measurement=load_json(measurement_path)
    if summary['status']!='PASS' or summary['artifacts']['measurement.json']!=frozen['measurement_sha256'] or measurement['dependency_hashes']!=frozen['dependency_hashes']:
        raise RuntimeError('ROCK_FROZEN_INPUT_MISMATCH')
    rows=[dict(path=k,sha256=v) for k,v in sorted(frozen['dependency_hashes'].items())]
    check_hash_rows(rows);return rows


def build_receipt():
    receipt=load_json(BASE/'build-receipt.json');path=Path(receipt['build_report'])
    if motion.digest(path)!=receipt['build_report_sha256']:raise RuntimeError('BUILD_RECEIPT_CHANGED')
    report=load_json(path)
    if report['status']!='PASS' or report['rock']['dependencies']!=rock_inputs():raise RuntimeError('BUILD_INPUT_ADOPTION_MISMATCH')
    check_hash_rows(report['generated']);return receipt


def capture_dimensions(camera, result, png):
    size=(camera['width'],camera['height'])
    if any(type(x) is not int or not 1<=x<=4096 for x in size):raise RuntimeError('UNSUPPORTED_NATIVE_CAPTURE_SIZE')
    if len(png)<24 or png[:8]!=b'\x89PNG\r\n\x1a\n' or png[12:16]!=b'IHDR':raise RuntimeError('INVALID_CAPTURE_PNG')
    actual=struct.unpack('>II',png[16:24])
    if size!=actual or actual!=(result['width'],result['height']):raise RuntimeError('CAMERA_CAPTURE_SIZE_MISMATCH')
    return dict(status='PASS',width=size[0],height=size[1],pixel_mapping='IDENTITY_NATIVE_PIXELS',resized=False)


def build_recovery_plan(pending, current, snapshot, root=PROJECT, completed_input=None):
    before=load_json(Path(pending['before']))
    if snapshot!=load_json(Path(pending['snapshot'])):raise RuntimeError('BUILD_STATE_RECOVERY_CONFLICT')
    for group,files in before.items():
        if any(current[group].get(k)!=v for k,v in files.items()):raise RuntimeError('EXISTING_FILE_RECOVERY_CONFLICT')
    extra={('Assets/'+p):h for p,h in current['Assets'].items() if p not in before['Assets']}
    if any(set(current[g])-set(before[g]) for g in ('Packages','ProjectSettings')):raise RuntimeError('UNDECLARED_BUILD_CHANGE')
    if not extra:
        if pending.get('folder_absent_before') and (root/FOLDER).exists():raise RuntimeError('UNCONFIRMED_EMPTY_BUILD_FOLDER')
        return []
    journal=Path(pending['created_files'])
    if not journal.exists():raise RuntimeError('UNCONFIRMED_CREATED_FILES')
    declared={r['path']:r['sha256'] for r in load_json(journal)['files']}
    if completed_input is not None:
        frozen=load_json(Path(completed_input));report_path=Path(frozen['build_report'])
        if frozen.get('scope')!='COMPLETED_OWN_BUILD_ROLLBACK_ONLY' or motion.digest(report_path)!=frozen['build_report_sha256']:
            raise RuntimeError('COMPLETED_BUILD_INPUT_CHANGED')
        report=load_json(report_path)
        if report_path.resolve()!=Path(pending['output']).resolve()/'build.json' or report.get('status')!='PASS' or not pending.get('folder_absent_before'):
            raise RuntimeError('COMPLETED_BUILD_IDENTITY_CONFLICT')
        final={r['path']:r['sha256'] for r in report['generated']}
        if len(final)!=len(report['generated']) or set(final)!=set(declared):raise RuntimeError('COMPLETED_BUILD_FILE_SET_CONFLICT')
        if report['rock']['dependencies']!=rock_inputs():raise RuntimeError('COMPLETED_BUILD_ROCK_CHANGED')
        declared=final
    if extra!=declared:raise RuntimeError('CREATED_FILE_RECOVERY_CONFLICT')
    return [dict(path=p,sha256=h) for p,h in sorted(declared.items())]


def normalized_geometry(data, objects=None):
    origin = xy(data['origin_px'])
    norm = lambda p: [p[i]-origin[i] for i in (0, 1)]
    return Geometry.from_polygons([norm(xy(p)) for p in data['boundary_px']],
        [[norm(xy(p)) for p in o['points_px']] for o in (data['objects'] if objects is None else objects)])


def assess(data, start, end, scale, radius=8):
    # Exact rational AABB broad phase: skipped solids have certified >2epsilon gap.
    a, b = [F(float(x))/F(scale) for x in start], [F(float(x))/F(scale) for x in end]
    r, e = F(radius)/F(scale), F(data['epsilon'])/F(scale)
    origin = xy(data['origin_px']);near=[];skipped=0
    for obj in data['objects']:
        p = [[F(t[i])-F(origin[i]) for i in (0, 1)] for t in map(xy, obj['points_px'])]
        gap=[]
        for i in (0, 1):
            lo, hi = min(a[i], b[i]), max(a[i], b[i])
            mn, mx = min(t[i] for t in p), max(t[i] for t in p)
            gap.append(max(F(0), mn-hi, lo-mx))
        if sum(d*d for d in gap) > (r+2*e+F(1e-12))**2:
            skipped += 1
        else:
            near.append(obj)
    geometry = normalized_geometry(data, near)
    result = assess_sweep(geometry, [float(x) for x in a], [float(x) for x in b], float(r), float(e))
    lo = F(result['exact_gap_interval'][0])
    if result['status'] == 'PASS' and lo <= 2*e+F(1e-12):
        result['status'] = 'BLOCKED';result['reason_codes'] = ['RUNTIME_CLEARANCE_POLICY']
    return dict(result, broadphase_skipped=skipped, broadphase_total=len(data['objects']))


def area2(points):
    return sum(p[0]*q[1]-p[1]*q[0] for p, q in zip(points, points[1:]+points[:1]))


def clip(subject, triangle):
    out = subject
    for a, b in zip(triangle, triangle[1:]+triangle[:1]):
        source, out = out, []
        if not source:
            break
        prev = source[-1];cp = cross(sub(b, a), sub(prev, a))
        for p in source:
            c = cross(sub(b, a), sub(p, a))
            if (c >= 0) != (cp >= 0):
                t = cp/(cp-c)
                out.append((prev[0]+t*(p[0]-prev[0]), prev[1]+t*(p[1]-prev[1])))
            if c >= 0:
                out.append(p)
            prev, cp = p, c
    return out


def audit_prism(points, vertices, triangles, height, scale, k, baked=4):
    n = len(points);factor=scale/baked if baked is not None else 1
    world = [[p['x']*factor, p['y'], p['z']*factor] for p in vertices]
    expected = [[p[0]*scale*k, -p[1]*scale*k] for p in points]
    if len(triangles) != (4*n-4)*3 or len(world) != 6*n:
        raise ValueError('MESH_TOPOLOGY_COUNT')
    top=[];bottom=[];volume=0;welded={};sides={}
    def match(p):
        d=[math.hypot(p[0]-q[0], p[2]-q[1]) for q in expected]
        i=min(range(n), key=d.__getitem__)
        if d[i] > 1e-4:
            raise ValueError('MESH_FOOTPRINT_CHANGED')
        return i
    for p in world:
        match(p)
        if min(abs(p[1]),abs(p[1]-height*k)) > 1e-5:
            raise ValueError('MESH_HEIGHT_MISMATCH')
    for t in range(0, len(triangles), 3):
        ids = triangles[t:t+3]
        if any(type(i) is not int or not 0 <= i < len(world) for i in ids):
            raise ValueError('MESH_INDEX')
        a,b,c=[world[i] for i in ids]
        nodes=[(match(p),0 if abs(p[1])<1e-5 else 1) for p in [a,b,c]]
        for x,y in zip(nodes,nodes[1:]+nodes[:1]):
            key=tuple(sorted((x,y)));count,direction=welded.get(key,(0,0));welded[key]=(count+1,direction+(1 if x<y else -1))
        ab=[b[i]-a[i] for i in range(3)];ac=[c[i]-a[i] for i in range(3)]
        cy=ab[2]*ac[0]-ab[0]*ac[2]
        volume += (a[0]*(b[1]*c[2]-b[2]*c[1])+a[1]*(b[2]*c[0]-b[0]*c[2])+a[2]*(b[0]*c[1]-b[1]*c[0]))/6
        if all(abs(p[1]-height*k) < 1e-5 for p in [a,b,c]):
            if cy <= 0:
                raise ValueError('TOP_WINDING')
            source=[tuple(F(x) for x in points[match(p)]) for p in [a,b,c]]
            centroid=tuple(sum(p[i] for p in source)/3 for i in (0,1))
            poly=polygon(points)
            if location(centroid,poly)!=1:
                raise ValueError('CAP_OUTSIDE_CONCAVE_POLYGON')
            top.append(source)
        elif all(abs(p[1]) < 1e-5 for p in [a,b,c]):
            if cy >= 0:
                raise ValueError('BOTTOM_WINDING')
            source=[tuple(F(x) for x in points[match(p)]) for p in [c,b,a]]
            if location(tuple(sum(p[i] for p in source)/3 for i in (0,1)),polygon(points))!=1:raise ValueError('BOTTOM_OUTSIDE')
            bottom.append(source)
        else:
            edge=set(x[0] for x in nodes)
            if len(edge)!=2:raise ValueError('SIDE_NOT_BOUNDARY')
            x,y=sorted(edge)
            if not (y==x+1 or x==0 and y==n-1):raise ValueError('SIDE_NOT_BOUNDARY')
            key=tuple(sorted(edge));sides[key]=sides.get(key,0)+1
    for name,cap in [('TOP',top),('BOTTOM',bottom)]:
        if len(cap)!=n-2 or sum(abs(area2(t)) for t in cap)!=abs(area2([tuple(F(x) for x in p) for p in points])):
            raise ValueError(name+'_CAP_AREA_MISMATCH')
        for i,a in enumerate(cap):
            for b in cap[:i]:
                overlap=clip(a,b)
                if len(overlap)>=3 and abs(area2(overlap))>0:raise ValueError(name+'_CAP_TRIANGLES_OVERLAP')
    expected_edges={tuple(sorted((i,(i+1)%n))) for i in range(n)}
    if set(sides)!=expected_edges or any(x!=2 for x in sides.values()) or any(c!=2 or d!=0 for c,d in welded.values()):
        raise ValueError('MESH_NOT_CLOSED')
    expected_volume=abs(float(area2(points)))/2*(scale*k)**2*height*k
    if not math.isclose(volume, expected_volume, rel_tol=5e-5, abs_tol=1e-5):
        raise ValueError('PRISM_VOLUME_MISMATCH')
    return dict(vertices=len(world), triangles=len(triangles)//3, top_triangles=len(top), volume=volume)


def audit_map(data, raw):
    objects={o['id']:o for o in data['objects']}
    meshes={m['id']:m for m in raw['meshes']}
    if len(raw['meshes'])!=64 or set(meshes)!=set(objects):
        raise ValueError('MESH_OBJECT_SET_MISMATCH')
    origin=xy(data['origin_px']);result={}
    for name,m in meshes.items():
        obj=objects[name];points=[[q[i]-origin[i] for i in (0,1)] for q in map(xy,obj['points_px'])]
        result[name]=audit_prism(points,m['world_vertices'],m['triangles'],obj['height_logical'],raw['scale'],raw['k'],baked=None)
    rock=objects[data['rock_object_id']]
    poly=polygon([[q[i]-origin[i] for i in (0,1)] for q in map(xy,rock['points_px'])])
    center=tuple(F(xy(data['rock_center_px'])[i])-F(origin[i]) for i in (0,1))
    if location(center,poly)!=1:
        raise ValueError('ROCK_CENTER_OUTSIDE_C')
    radius=min(point_distance_squared(center,a,b) for a,b in zip(poly,poly[1:]+poly[:1]))
    target=[float(center[0])*raw['scale']*raw['k'],float(center[1])*-raw['scale']*raw['k']]
    measured=raw['rock_center']
    if math.hypot(measured['x']-target[0],measured['z']-target[1])>1e-4 or abs(raw['rock_bottom'])>1e-4:
        raise ValueError('ROCK_CONTACT_MISMATCH')
    max_r2=max((p['x']-target[0])**2+(p['z']-target[1])**2 for p in raw['rock_corners'])
    if max_r2 >= float(radius)*(raw['scale']*raw['k'])**2:
        raise ValueError('ROCK_NOT_CONTAINED_IN_C')
    return dict(status='PASS', objects=64, triangles=sum(r['triangles'] for r in result.values()),
                rock_contact='PASS',rock_bound_radius=math.sqrt(max_r2),details=result)


def judge_motion(data,request,raw,request_sha):
    if raw['scope']!='BLOCKOUT_PREVIEW_ONLY' or any(raw[k]!=request[k] for k in ('run_id','nonce','case_id')) or raw['request_sha256']!=request_sha:
        raise ValueError('EVIDENCE_MISMATCH')
    if raw['error'] or raw['scale']!=request['scale'] or len(raw['frames'])!=request['steps']:
        raise ValueError('INCOMPLETE_PREVIEW:'+raw['error'])
    scale=request['scale'];checks=0;last=None;frame=-1;blocked=0;stop_run=0;longest_stop=0;progress=[]
    offsets=[xy(p) for p in request['body_offsets']]
    if len(offsets)!=4:raise ValueError('INVALID_FROZEN_BODY_SET')
    for i,row in enumerate(raw['frames']):
        if row['step_id']!=i or row['frame_id']<=frame:
            raise ValueError('OBSERVATION_GAP')
        frame=row['frame_id']
        c0,c1=xy(row['before_center']),xy(row['after_center'])
        if i==0 and any(abs(a-b)>3e-4 for a,b in zip(c0,xy(request['center']))):
            raise ValueError('INITIAL_POSE_MISMATCH')
        for state in ('before','after'):
            center=xy(row[state+'_center'])
            if len(row[state+'_reserved'])!=4 or len(row[state+'_actual'])!=4:raise ValueError('BODY_COUNT')
            for j,p in enumerate(row[state+'_reserved']):
                if any(abs(a-b-d)>3e-4 for a,b,d in zip(xy(p),center,offsets[j])):raise ValueError('RESERVED_BINDING_MISMATCH')
            if i==0 and state=='before' and any(any(abs(a-b-d)>3e-4 for a,b,d in zip(xy(p),center,offsets[j])) for j,p in enumerate(row['before_actual'])):
                raise ValueError('INITIAL_ACTUAL_MISMATCH')
        if last and (last['after_center']!=row['before_center'] or last['after_actual']!=row['before_actual'] or last['after_reserved']!=row['before_reserved']):
            raise ValueError('STATE_CONTAMINATION')
        expected=xy(request['input']);reverse=request['reverse_at']>=0 and i>=request['reverse_at']
        if reverse:expected=[-p for p in expected]
        if xy(row['input'])!=expected or request['device'] and row['device_id']<=0:
            raise ValueError('INPUT_MISMATCH')
        dt=row['applied_dt']
        if not 0<=dt<=.050001 or not math.isfinite(dt) or not request['device'] and abs(dt-.05)>1e-6:
            raise ValueError('DT_MISMATCH')
        delta=[b-a for a,b in zip(c0,c1)];full=[x*140*dt for x in expected]
        length=sum(x*x for x in full);fraction=sum(a*b for a,b in zip(delta,full))/length if length else 0
        if not -.001<=fraction<=1.001 or abs(fraction*256-round(fraction*256))>.03 or any(abs(d-a*fraction)>3e-4 for d,a in zip(delta,full)):
            raise ValueError('CENTER_MOTION_MISMATCH')
        if not math.isfinite(row['alpha']) or abs(row['alpha']-fraction)>.0001:raise ValueError('ALPHA_OBSERVATION_MISMATCH')
        if not request['device'] and fraction<.99 and not reverse:
            blocked+=1;next_center=[c0[j]+full[j] for j in (0,1)]
            proposed=[(c0,next_center)]
            for j,offset in enumerate(offsets):
                target=[next_center[k]+offset[k] for k in (0,1)];old=xy(row['before_actual'][j])
                proposed.extend([(xy(row['before_reserved'][j]),target),(old,[old[k]+(target[k]-old[k])*min(1,12*dt) for k in (0,1)])])
            unsafe=False
            for a,b in proposed:
                result=assess(data,a,b,scale);checks+=1
                if result['status']=='FAIL' or F(result['exact_gap_interval'][1])<=F(2*data['epsilon'])/F(scale):unsafe=True
            if not unsafe:raise ValueError('UNJUSTIFIED_REDUCED_MOTION')
        if not reverse:
            stop_run=stop_run+1 if math.dist(c0,c1)<=.001 else 0;longest_stop=max(longest_stop,stop_run)
        progress.append(sum((c1[j]-xy(request['center'])[j])*xy(request['input'])[j] for j in (0,1)))
        if row['held']:
            if c0!=c1 or row['before_actual']!=row['after_actual']:
                raise ValueError('HELD_POSE_MOVED')
        for kind in ('center','actual','reserved'):
            before=[row['before_center']] if kind=='center' else row['before_'+kind]
            after=[row['after_center']] if kind=='center' else row['after_'+kind]
            if len(before)!=(1 if kind=='center' else 4) or len(after)!=len(before):
                raise ValueError('BODY_COUNT')
            for j,(a,b) in enumerate(zip(before,after)):
                result=assess(data,xy(a),xy(b),scale);checks+=1
                if result['status']!='PASS':raise ValueError('UNSAFE_'+kind+':'+str(result))
                if kind=='actual' and not row['held']:
                    target=xy(row['after_reserved'][j]);old=xy(a)
                    if any(abs(t-(x+(y-x)*min(1,12*dt)))>3e-4 for t,x,y in zip(xy(b),old,target)):
                        raise ValueError('FOLLOW_MISMATCH')
        last=row
    moved=math.dist(xy(raw['frames'][0]['before_center']),xy(raw['frames'][-1]['after_center']))
    if request['device'] and moved<=.1:
        raise ValueError('NO_DEVICE_PROGRESS')
    if not request['device'] and (blocked==0 or moved<=1 or longest_stop<5):
        raise ValueError('NO_WALL_STOP_OR_REVERSE_PROGRESS')
    if request['reverse_at']>=0 and progress[-1]>=max(progress[:request['reverse_at']])-30:raise ValueError('NO_REVERSE_RETURN')
    return dict(status='PASS',frames=len(raw['frames']),sweeps=checks,blocking_steps=blocked,moved=moved,stationary_steps=longest_stop,forward_peak=max(progress),final_progress=progress[-1])


class Unity:
    def __init__(self,out):
        self.out,self.counter=out,0
        self.env=os.environ.copy();self.env.update(UNITY_NO_CLI_INVOKED_TELEMETRY='1',UNITY_NO_CRASH_REPORT='1',UNITY_NO_CONSENT_PROMPT='1')
    def cli(self,args,timeout=90):
        self.counter+=1;fence=args.index('--') if '--' in args else len(args)
        p=subprocess.run(['unity',*args[:fence],'--project-path',str(PROJECT),'--format','json',*args[fence:]],env=self.env,capture_output=True,text=True,timeout=timeout)
        motion.write(self.out/f'{self.counter:03d}-command.json',dict(args=args,stdout=p.stdout,stderr=p.stderr,exit_code=p.returncode))
        if not p.stdout.strip():raise RuntimeError('UNITY_EMPTY_RESPONSE:'+p.stderr)
        blob=json.loads(p.stdout)
        if p.returncode or blob.get('success') is not True:raise RuntimeError('UNITY_COMMAND_FAILED:'+str(blob.get('errors')))
        return blob
    def evaluate(self,code):
        value=self.cli(['command','script','eval','--caller','plugin','--skill','ds-workflow','--','--code',code,'--timeout','30000'])['data']['result']
        if value.get('success') is not True:raise RuntimeError('UNITY_SCRIPT_FAILED:'+str(value))
        return value['result']
    def method(self,name,*args):
        return self.evaluate('return DeadSurvivor.Editor.DSMapBlockoutVerification.'+name+'('+','.join(json.dumps(x) for x in args)+');')
    def session(self,name,*args):
        return self.evaluate('return DeadSurvivor.Editor.DSMapMotionVerification.'+name+'('+','.join(json.dumps(x) for x in args)+');')
    def ready(self):self.cli(['status','--until-ready'])


def assert_sources_synced():
    for name in ('Runtime','Editor'):
        a={str(p.relative_to(ROOT/'UnityMigration'/name)):motion.digest(p) for p in (ROOT/'UnityMigration'/name).rglob('*.cs')}
        target=PROJECT/'Assets/DeadSurvivor'/name
        b={str(p.relative_to(target)):motion.digest(p) for p in target.rglob('*.cs')}
        if a!=b:raise RuntimeError('SOURCE_TARGET_MISMATCH:'+name)
    if motion.digest(ROOT/'UnityMigration/Data/PrototypeData.json')!=motion.digest(PROJECT/'Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json'):
        raise RuntimeError('CONTENT_MISMATCH')


def _build_at(path,data,uid,out,fail_after=0):
    u=Unity(out)
    with motion.editor_lock():
        if (motion.BASE/'unity-pending.json').exists() or (BASE/'build-pending.json').exists():raise RuntimeError('UNFINISHED_UNITY_RUN')
        assert_sources_synced();sources=motion.source_hashes()
        u.ready();snapshot=u.session('Snapshot');motion.write(out/'snapshot.json',snapshot)
        before={name:motion.tree_hashes(PROJECT/name) for name in ('Assets','Packages','ProjectSettings')}
        motion.write(out/'target-before.json',before)
        dependencies=rock_inputs()
        request=dict(data_file=str(path),data_sha256=motion.digest(path),output_report=str(out/'build.json'),run_id=uid,expected_rock_dependencies=dependencies,fail_after_assets=fail_after)
        marker=dict(run_id=uid,folder=FOLDER,folder_absent_before=not (PROJECT/FOLDER).exists(),snapshot=str(out/'snapshot.json'),before=str(out/'target-before.json'),created_files=str(out/'created-files.json'),output=str(out))
        motion.write(out/'request.json',request);motion.write(BASE/'build-pending.json',marker)
        try:
            result=json.loads(u.method('Build',str(out/'request.json')))
        except (Exception,KeyboardInterrupt) as error:
            failure=dict(status='BLOCKED',scope='BLOCKOUT_PREVIEW_BUILD_ONLY',error=str(error) or 'CANCELLED',cleanup='BLOCKED',pending=True)
            try:
                current={name:motion.tree_hashes(PROJECT/name) for name in before}
                rows=build_recovery_plan(marker,current,u.session('Snapshot'))
                if not rows:
                    (BASE/'build-pending.json').unlink();failure.update(cleanup='PASS_NO_MUTATION',pending=False)
            except Exception as recovery_error:failure['recovery_error']=str(recovery_error)
            motion.write(out/'summary.json',failure);raise
        restored=u.session('Snapshot');motion.write(out/'restored.json',restored)
        after={name:motion.tree_hashes(PROJECT/name) for name in before};motion.write(out/'target-after.json',after)
        if restored!=snapshot:raise RuntimeError('BUILD_STATE_NOT_RESTORED')
        for group,files in before.items():
            if any(after[group].get(k)!=v for k,v in files.items()):raise RuntimeError('EXISTING_PROJECT_FILE_CHANGED')
            for key in set(after[group])-set(files):
                if group!='Assets' or not key.startswith('DeadSurvivor/Scenes/Map018_r001'):
                    raise RuntimeError('UNDECLARED_NEW_ASSET:'+key)
        if result['objects']!=64 or result['data_sha256']!=motion.digest(path):raise ValueError('BUILD_RESULT_MISMATCH')
        for row in result['rock']['dependencies']:
            if motion.digest(PROJECT/row['path'])!=row['sha256']:raise RuntimeError('ROCK_DEPENDENCY_CHANGED')
        if result['rock']['dependencies']!=dependencies:raise RuntimeError('BUILD_ROCK_INPUT_CHANGED')
        rock_inputs()
        if sources!=motion.source_hashes():raise RuntimeError('BUILD_SOURCE_CHANGED')
        motion.write(BASE/'build-receipt.json',dict(build_report=str(out/'build.json'),build_report_sha256=motion.digest(out/'build.json'),scope='PREVIEW_ONLY'))
        (BASE/'build-pending.json').unlink()
        result=dict(status='PASS',scope='BLOCKOUT_PREVIEW_BUILD_ONLY',build=str(out/'build.json'),scene=SCENE,hashes={p.name:motion.digest(p) for p in out.iterdir() if p.is_file()})
        motion.write(out/'summary.json',result)
        print(json.dumps(dict(status='PASS',scene=SCENE,report=str(out/'summary.json')),ensure_ascii=False))


def build(fail_after=0):
    path,data=prepare();uid=uuid.uuid4().hex;out=BASE/('build-'+uid);out.mkdir()
    try:_build_at(path,data,uid,out,fail_after)
    except (Exception,KeyboardInterrupt) as error:
        if not (out/'summary.json').exists():motion.write(out/'summary.json',dict(status='BLOCKED',scope='BLOCKOUT_PREVIEW_BUILD_ONLY',error=str(error) or 'CANCELLED',pending=(BASE/'build-pending.json').exists(),cleanup='BLOCKED'))
        raise


def recover_build(completed_input=None):
    marker_path=BASE/'build-pending.json'
    with motion.editor_lock():
        marker=load_json(marker_path);out=BASE/('recover-'+uuid.uuid4().hex);out.mkdir();u=Unity(out);u.ready()
        snapshot=u.session('Snapshot');before=load_json(Path(marker['before']));current={g:motion.tree_hashes(PROJECT/g) for g in before}
        rows=build_recovery_plan(marker,current,snapshot,completed_input=completed_input)
        if rows:
            request=out/'request.json';motion.write(request,dict(files=rows))
            if u.method('RecoverCreatedFolder',str(request))!='REMOVED_OWN_CONFIRMED_FOLDER':raise RuntimeError('BUILD_RECOVERY_FAILED')
        after={g:motion.tree_hashes(PROJECT/g) for g in before}
        if after!=before or u.session('Snapshot')!=snapshot:raise RuntimeError('BUILD_RECOVERY_MISMATCH')
        marker_path.rename(out/'retired-build-pending.json')
        motion.write(out/'summary.json',dict(status='PASS',scope='FAILED_BUILD_OWNED_FILES_ONLY',removed_files=len(rows),target_restored=True))
        print(json.dumps(dict(status='PASS',report=str(out/'summary.json'))))


def verify():
    path,data=prepare();uid,nonce=uuid.uuid4().hex,uuid.uuid4().hex;out=BASE/uid;out.mkdir();u=Unity(out)
    report=dict(status='BLOCKED',scope='BLOCKOUT_PREVIEW_ONLY',run_id=uid,time=datetime.now(ZoneInfo('Asia/Seoul')).isoformat(),results={},g2='NOT_RUN',g3='NOT_RUN',asset_adoption='NOT_RUN')
    with motion.editor_lock():
        pending=motion.BASE/'unity-pending.json'
        if pending.exists() or (BASE/'build-pending.json').exists():raise RuntimeError('UNFINISHED_UNITY_RUN')
        sources=motion.source_hashes();snapshot=None;before=None;pid=None;captures=[]
        def capture(label,evidence):
            rel=f'Assets/MigrationVerification/Map018-{uid}-{label}.png';native=PROJECT/rel
            if native.exists() or Path(str(native)+'.meta').exists():raise RuntimeError('CAPTURE_NOT_FRESH')
            item=dict(path=rel,phase='PLANNED',run_id=uid,nonce=nonce);captures.append(item);motion.write(out/'owned-captures.json',captures)
            result=u.cli(['command','capture','game_view','--caller','plugin','--skill','ds-workflow','--','--source','screen','--width',str(evidence['width']),'--height',str(evidence['height']),'--save_path',rel.removeprefix('Assets/')])['data']['result']
            if (PROJECT/result['savedPath']).resolve()!=native.resolve():raise RuntimeError('CAPTURE_PATH_CHANGED')
            shutil.copy2(native,out/(label+'.png'))
            u.evaluate('UnityEditor.AssetDatabase.ImportAsset('+json.dumps(rel)+',UnityEditor.ImportAssetOptions.ForceSynchronousImport);return true;')
            item.update(phase='CONFIRMED',sha256=motion.digest(native),meta_sha256=motion.digest(str(native)+'.meta'));motion.write(out/'owned-captures.json',captures)
            sizes=capture_dimensions(evidence,result,native.read_bytes())
            after=json.loads(u.method('CameraView',int(evidence['elevation']),evidence['overview'],evidence['focusRock']))
            if after!=evidence:raise RuntimeError('CAMERA_CHANGED_DURING_CAPTURE')
            motion.write(out/(label+'.capture.json'),dict(**sizes,camera_before=evidence,camera_after=after,png_sha256=item['sha256'],source='screen',pose='FROZEN_AFTER_RECORDED_STEPS'))
        try:
            assert_sources_synced();u.ready();snapshot=u.session('Snapshot');pid=u.session('Ownership')['pid'];motion.write(out/'snapshot.json',snapshot)
            receipt=build_receipt();dependencies=rock_inputs()
            before={name:motion.tree_hashes(PROJECT/name) for name in ('Assets','Packages','ProjectSettings')};motion.write(out/'target-before.json',before)
            if motion.digest(PROJECT/(FOLDER+'/Layout.json'))!=motion.digest(path):raise RuntimeError('DEPLOYED_LAYOUT_CHANGED')
            units=json.loads(u.method('UnitTests'));motion.write(out/'units.json',units)
            if units['status']!='PASS':raise ValueError('UNITS_FAILED')
            plans=[];origin=xy(data['origin_px']);rock=data['objects'][0]
            catalog=json.loads(u.session('FormationCatalog'))['entries'];offsets=next(c['offsets'] for c in catalog if c['count']==4 and c['formation']==0)
            for scale in (4,6,8):
                cx=xy(data['rock_center_px'])[0];cy=min(p['y'] for p in rock['points_px'])-70/scale
                center=[(cx-origin[0])*scale,(cy-origin[1])*scale]
                for offset in [v([0,0]),*offsets]:
                    p=[center[i]+xy(offset)[i] for i in (0,1)]
                    if assess(data,p,p,scale)['status']!='PASS':raise ValueError('PLANNED_POSE_UNSAFE')
                plans.append(dict(case_id=f'WALL-REVERSE-S{scale}',scale=scale,center=v(center),input=v([0,1]),steps=60,reverse_at=40,device=False,body_offsets=offsets))
            plans.append(dict(case_id='ROCK-CAMERA-S4',scale=4,center=plans[0]['center'],input=v([0,1]),steps=40,reverse_at=-1,device=False,body_offsets=offsets))
            plans.append(dict(case_id='KEYBOARD-S4',scale=4,center=v([0,0]),input=v([1,0]),steps=30,reverse_at=-1,device=True,body_offsets=offsets))
            motion.write(out/'manifest.json',dict(run_id=uid,nonce=nonce,data_sha256=motion.digest(path),sources=sources,expected_cases=[p['case_id'] for p in plans],cases=plans))
            motion.write(pending,dict(run_id=uid,nonce=nonce,snapshot=str(out/'snapshot.json'),pid=os.getpid(),scope='BLOCKOUT_PREVIEW_ONLY'))
            u.session('Prepare',uid,nonce);u.session('StartOwned',uid,nonce);u.ready();judgments={};audits={}
            for spec in plans:
                request=dict(run_id=uid,nonce=nonce,output=str(out/(spec['case_id']+'.raw.json')),**spec)
                request_path=out/(spec['case_id']+'.request.json');motion.write(request_path,request)
                u.method('BeginCase',str(request_path));deadline=time.monotonic()+30
                while not Path(request['output']).exists() and time.monotonic()<deadline:time.sleep(.1)
                if not Path(request['output']).exists():raise RuntimeError('PREVIEW_CASE_TIMEOUT')
                raw=load_json(Path(request['output']),max_bytes=8*1024*1024)
                if raw['data_sha256']!=motion.digest(path):raise ValueError('DATA_EVIDENCE_MISMATCH')
                judgments[spec['case_id']]=judge_motion(data,request,raw,motion.digest(request_path))
                audit=strict_loads(u.method('Audit'));motion.write(out/(spec['case_id']+'.audit.json'),audit);audits[spec['case_id']]=audit_map(data,audit)
                motion.write(out/'judgments.json',judgments);motion.write(out/'mesh-audits.json',audits)
                if spec['case_id']=='ROCK-CAMERA-S4':
                    for angle in (45,55):
                        evidence=json.loads(u.method('CameraView',angle,False,False));motion.write(out/f'camera-{angle}.json',evidence);capture(f'near-{angle}',evidence)
                    evidence=json.loads(u.method('CameraView',90,True,False));motion.write(out/'camera-90.json',evidence);capture('overview',evidence)
            logs=u.cli(['command','console','get','--caller','plugin','--skill','ds-workflow','--','--tail','100','--level','error'])['data']['result']
            if logs.get('entries') or logs.get('groundTruth',{}).get('consoleErrors',0):raise RuntimeError('CONSOLE_ERRORS')
            report['results']=dict(units=units['count'],cases=len(judgments),frames=sum(r['frames'] for r in judgments.values()),sweeps=sum(r['sweeps'] for r in judgments.values()),meshes=64,console='PASS')
            report['status']='PASS'
        except (Exception,KeyboardInterrupt) as error:
            report['status']='FAIL' if isinstance(error,(ValueError,AssertionError)) else 'BLOCKED';report['error']=str(error) or 'CANCELLED'
        finally:
            try:
                if pending.exists():
                    marker=load_json(pending)
                    if marker['run_id']!=uid or marker['nonce']!=nonce:raise RuntimeError('PENDING_CHANGED')
                    motion.cleanup_owned(u.session,u.ready,uid,nonce,str(out/'snapshot.json'),snapshot,pid)
                    motion.write(out/'restored.json',dict(snapshot=u.session('Snapshot'),ownership=u.session('Ownership')))
                if before is not None:
                    rock_inputs();build_receipt()
                    after=motion.finish_owned_files(PROJECT,captures,before,lambda:sources==motion.source_hashes(),pending if pending.exists() else None,
                        lambda rel:u.evaluate('return UnityEditor.AssetDatabase.DeleteAsset('+json.dumps(rel)+');'))
                    motion.write(out/'target-after.json',after)
                report['cleanup']='PASS'
            except (Exception,KeyboardInterrupt) as error:
                report['cleanup']='BLOCKED';report['cleanup_error']=str(error)
                if report['status']!='FAIL':report['status']='BLOCKED'
            report['finished_at']=datetime.now(ZoneInfo('Asia/Seoul')).isoformat();report['artifacts']={str(p.relative_to(out)):motion.digest(p) for p in out.rglob('*') if p.is_file() and p.name!='summary.json'}
            motion.write(out/'summary.json',report)
        print(json.dumps(dict(status=report['status'],scope=report['scope'],report=str(out/'summary.json'),error=report.get('error')),ensure_ascii=False))
        return 0 if report['status']=='PASS' else 1 if report['status']=='FAIL' else 2


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('stage',choices=('prepare','build','verify','recover-build'));parser.add_argument('--inject-build-failure-after',type=int,choices=(0,3),default=0);parser.add_argument('--completed-build-input',type=Path);args=parser.parse_args()
    try:
        if args.stage=='prepare':
            p,d=prepare();print(json.dumps(dict(status='PASS',scope='SOURCE_COMPILATION_ONLY',file=str(p),objects=len(d['objects']))))
        elif args.stage=='build':build(args.inject_build_failure_after)
        elif args.stage=='recover-build':recover_build(args.completed_build_input)
        else:raise SystemExit(verify())
    except (RuntimeError,ValueError) as error:
        print(json.dumps(dict(status='BLOCKED',error=str(error)),ensure_ascii=False));raise SystemExit(2)

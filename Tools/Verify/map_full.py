#!/usr/bin/env python3
"""026 execution-request preflight and sealed evidence adjudication.

This file does not upgrade legacy fixture reports into full-map evidence. Supported
assertions are explicit; unsupported contracts and absent execution stay blocked.
"""
import argparse
from datetime import datetime
import hashlib
import json
import math
from pathlib import Path
import re
import uuid
from zoneinfo import ZoneInfo

from map_io import MapInputError, canonical_hash, load_json, safe_relative_path, reject_symlinks
from map_oracle import Geometry, assess_sweep

ROOT = Path(__file__).resolve().parents[2]
ROLE = 'MAP_EXECUTION_REQUEST_026'
FI_IDS = {f'FI-{n:02}' for n in range(1, 11)}
REQUIREMENTS = {'MC-ART-01','MC-REF-01','MC-START-01',*(f'MC-MOVE-{i:02}' for i in range(1,6)),
                'MC-BOUND-01','MC-CONN-01','MC-LIFE-01','MC-PREP-01','MC-TIME-01',
                'MC-CAMERA-01','MC-REPLAY-01','MC-CLEAN-01'}
STATUS_RANK = {'PASS':0, 'NOT_RUN':1, 'BLOCKED':2, 'FAIL':3}


def fail(code, detail='', status='FAIL'):
    raise MapInputError(code, detail or code, status)


def obj(value, keys, label):
    if type(value) is not dict or set(value) != set(keys):
        fail('FIELDS',label+': expected '+','.join(sorted(keys)))
    return value


def finite(value, label, low=None, high=None):
    if type(value) not in (int,float) or (type(value) is float and not math.isfinite(value)):
        fail('INVALID_NUMBER',label)
    if low is not None and value < low or high is not None and value > high:
        fail('NUMBER_RANGE',label)
    return value


def integer(value,label,low=0,high=100000):
    if type(value) is not int or not low <= value <= high: fail('INVALID_INTEGER',label)
    return value


def identifier(value):
    if type(value) is not str or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]{0,127}',value):
        fail('INVALID_ID',str(value))
    return value


def point(value):
    if type(value) is not list or len(value)!=2: fail('INVALID_POINT')
    return [finite(x,'coordinate',-16384,16384) for x in value]


def verify_inputs(rows,root):
    if type(rows) is not list or not rows: fail('EMPTY_INPUTS')
    seen=set()
    for row in rows:
        obj(row,{'path','sha256'},'input')
        if row['path'] in seen: fail('DUPLICATE_INPUT')
        seen.add(row['path'])
        if type(row['sha256']) is not str or not re.fullmatch('[0-9a-f]{64}',row['sha256']): fail('INVALID_HASH')
        p=safe_relative_path(root,row['path'])
        if hashlib.sha256(p.read_bytes()).hexdigest()!=row['sha256']: fail('INPUT_CHANGED',row['path'],'BLOCKED')


def normalized_geometry(value):
    obj(value,{'geometry_id','revision','coordinate_space','boundary','obstacles','sha256'},'geometry')
    identifier(value['geometry_id']);identifier(value['revision'])
    if value['coordinate_space']!='logical_xy': fail('COORDINATE_SPACE')
    raw={k:value[k] for k in ('boundary','obstacles')}
    if canonical_hash(raw)!=value['sha256']: fail('GEOMETRY_HASH')
    if type(raw['obstacles']) is not list or len(raw['obstacles'])>128: fail('OBSTACLES')
    # Power-of-two normalization is exact for binary coordinates and permits the
    # existing bounded, rational-predicate oracle without changing its contract.
    scale=16
    def poly(p):
        if type(p) is not list: fail('POLYGON')
        return [[x/scale for x in point(v)] for v in p]
    return Geometry.from_polygons(poly(raw['boundary']),[poly(p) for p in raw['obstacles']]),scale


def validate_request(data,root=ROOT):
    obj(data,{'schema_version','document_role','suite_id','revision','scope','mode','map_mode',
              'command_queue_limit','wall_timeout_s','max_evidence_bytes','inputs','expected_case_ids',
              'cases','required_fi_variants','controls','required_requirements'},'request')
    if type(data['schema_version']) is not int or data['schema_version']!=1 or data['document_role']!=ROLE: fail('SCHEMA')
    identifier(data['suite_id']);identifier(data['revision'])
    if data['scope'] not in ('G2_FIXTURE','G3_MAP') or data['mode'] not in ('FIXED_REPLAY','DEVICE_SMOKE') or data['map_mode']!='MapRequired': fail('UNSUPPORTED_MODE',status='BLOCKED')
    integer(data['command_queue_limit'],'queue',1,1024);finite(data['wall_timeout_s'],'wall',1,86400)
    integer(data['max_evidence_bytes'],'bytes',1024,1024*1024*1024)
    verify_inputs(data['inputs'],root)
    cases=data['cases'];expected=data['expected_case_ids']
    if type(cases) is not list or not 1<=len(cases)<=4096: fail('EMPTY_OR_EXCESS_CASES')
    if type(expected) is not list or any(type(i) is not str for i in expected) or len(set(expected))!=len(expected): fail('EXPECTED_IDS')
    ids=[]
    for case in cases:
        obj(case,{'case_id','parent_ids','requirement_ids','geometry','bodies','initial','input_steps','thresholds','expected','mutation','display','wall_timeout_s'},'case')
        ids.append(identifier(case['case_id']))
        for name in ('parent_ids','requirement_ids'):
            if type(case[name]) is not list or any(type(i) is not str for i in case[name]): fail('ID_LIST',name)
        normalized_geometry(case['geometry'])
        bodies=case['bodies']
        if type(bodies) is not list or not 1<=len(bodies)<=4: fail('BODIES')
        for i,b in enumerate(bodies):
            obj(b,{'slot','shape','radius'},'body')
            if type(b['slot']) is not int or b['slot']!=i or b['shape']!='circle': fail('BODY_SLOT')
            finite(b['radius'],'radius',.001,1024)
        init=case['initial'];obj(init,{'center','formation','positions','alive','seed','loot_seed'},'initial')
        point(init['center']);integer(init['formation'],'formation',0,2)
        integer(init['seed'],'seed',0,2147483647);integer(init['loot_seed'],'loot_seed',0,2147483647)
        if type(init['positions']) is not list or len(init['positions'])!=len(bodies) or type(init['alive']) is not list or len(init['alive'])!=len(bodies) or any(type(v) is not bool for v in init['alive']): fail('INITIAL_BODIES')
        for p in init['positions']:point(p)
        thresholds=case['thresholds'];obj(thresholds,{'geometry_epsilon','position_epsilon','time_epsilon','center_radius'},'thresholds')
        for name,v in thresholds.items(): finite(v,name,.000001,1024)
        steps=case['input_steps']
        if type(steps) is not list or not 1<=len(steps)<=10000:fail('STEPS')
        command_ids=set()
        for i,step in enumerate(steps):
            obj(step,{'index','frame_dt_supplied','move','commands','continue_on_rejection'},'step')
            if type(step['index']) is not int or step['index']!=i: fail('STEP_SEQUENCE')
            finite(step['frame_dt_supplied'],'dt',0,1);p=point(step['move'])
            if sum(v*v for v in p)>1.000001:fail('INPUT_MAGNITUDE')
            if type(step['continue_on_rejection']) is not bool:fail('REJECTION_POLICY')
            if type(step['commands']) is not list or len(step['commands'])>data['command_queue_limit']:fail('COMMAND_QUEUE')
            for command in step['commands']:
                obj(command,{'command_id','kind','value'},'command');identifier(command['command_id'])
                if command['command_id'] in command_ids:fail('DUPLICATE_COMMAND')
                command_ids.add(command['command_id'])
                if command['kind'] not in ('Pause','Resume','SelectFormation','CancelFormation','Revive','NextWave'):fail('UNSUPPORTED_COMMAND',command['kind'],'BLOCKED')
                integer(command['value'],'command value',-1,3)
        expected_case=case['expected'];obj(expected_case,{'status','reason','assertions'},'expected')
        if expected_case['status'] not in ('PASS','FAIL','BLOCKED') or type(expected_case['reason']) is not str:fail('EXPECTED_STATUS')
        if type(expected_case['assertions']) is not list or not expected_case['assertions']:fail('EMPTY_ASSERTIONS')
        assertion_ids=[]
        for assertion in expected_case['assertions']:
            obj(assertion,{'id','type','target'},'assertion');assertion_ids.append(identifier(assertion['id']))
            if assertion['type'] not in ('safety','center_arrival','state','timer_stopped'):fail('UNSUPPORTED_ASSERTION',assertion['type'],'BLOCKED')
            if assertion['type']=='center_arrival':point(assertion['target'])
            elif assertion['type']=='state':
                if assertion['target'] not in ('battle','prep','result','lobby'):fail('ASSERTION_TARGET')
            elif assertion['target'] is not None:fail('ASSERTION_TARGET')
        if len(set(assertion_ids))!=len(assertion_ids):fail('DUPLICATE_ASSERTION')
        mutation=case['mutation'];obj(mutation,{'fi_id','variant'},'mutation')
        if mutation['fi_id']!='none' and mutation['fi_id'] not in FI_IDS:fail('FI_ID')
        if type(mutation['variant']) is not str:fail('FI_VARIANT')
        if data['scope']=='G3_MAP' and mutation['fi_id']!='none':fail('G3_MUTATION')
        finite(case['wall_timeout_s'],'case timeout',1,data['wall_timeout_s'])
        obj(case['display'],{'status','reason'},'display')
        if case['display']['status']!='NOT_RUN' or not case['display']['reason']:fail('DISPLAY_UNSUPPORTED',status='BLOCKED')
    if len(set(ids))!=len(ids) or ids!=expected:fail('CASE_SET_MISMATCH')
    for name in ('required_fi_variants','required_requirements'):
        if type(data[name]) is not list or any(type(v) is not str for v in data[name]) or len(set(data[name]))!=len(data[name]):fail('REQUIREMENT_LIST',name)
    declared=set(data['required_requirements'])
    if not declared<=REQUIREMENTS:fail('UNKNOWN_REQUIREMENT')
    represented={r for c in cases for r in c['requirement_ids']}
    if not represented<=REQUIREMENTS|FI_IDS:fail('UNKNOWN_REQUIREMENT')
    if not declared<=represented:fail('MISSING_REQUIREMENT')
    required=set(data['required_fi_variants'])
    if not required<=FI_IDS:fail('UNKNOWN_FI_VARIANT')
    if type(data['controls']) is not dict or set(data['controls'])!=required or any(v not in ids for v in data['controls'].values()):fail('CONTROLS')
    by_id={c['case_id']:c for c in cases}
    for fi,control in data['controls'].items():
        if by_id[control]['mutation']['fi_id']!='none':fail('MUTATED_NORMAL_CONTROL')
        if not any(c['mutation']['fi_id']==fi for c in cases):fail('MISSING_FI_CASE')
    # No adapter can be silently assumed: this core supports source validation,
    # but the planned full-map snapshots/display/driver remain explicit blockers.
    return {'status':'PASS','scope':'REQUEST_PREFLIGHT_ONLY','execution_status':'NOT_RUN',
            'cases':len(cases),'request_sha256':canonical_hash(data),
            'acceptance_status':'BLOCKED','missing_evidence':['owned_frame_adapter','complete_snapshot_contract','display','FI_and_cleanup','full_space_connectivity']}


def combine(statuses):
    if not statuses:return 'NOT_RUN'
    return max(statuses,key=lambda s:STATUS_RANK[s])


def seal_check(run_dir, max_bytes=128*1024*1024):
    run_dir=reject_symlinks(run_dir)
    manifest=load_json(run_dir/'evidence-manifest.json',64*1024*1024)
    obj(manifest,{'run_id','nonce','files'},'evidence manifest')
    for key in ('run_id','nonce'):
        if type(manifest[key]) is not str or not re.fullmatch('[0-9a-f]{32}',manifest[key]):fail('RUN_ID')
    if type(manifest['files']) is not dict or not manifest['files']:fail('EMPTY_EVIDENCE')
    if 'evidence-manifest.json' in manifest['files']:fail('SELF_HASH')
    actual=set()
    for path in run_dir.rglob('*'):
        if path.is_symlink():fail('SYMLINK',str(path))
        if path.is_file() and path!=run_dir/'evidence-manifest.json':actual.add(str(path.relative_to(run_dir)))
    if set(manifest['files'])!=actual:fail('EVIDENCE_FILE_SET')
    buffers={};size=(run_dir/'evidence-manifest.json').stat().st_size
    if size>max_bytes:fail('EVIDENCE_LIMIT',status='BLOCKED')
    for name,expected in manifest['files'].items():
        if type(expected) is not str or not re.fullmatch('[0-9a-f]{64}',expected):fail('INVALID_HASH')
        path=safe_relative_path(run_dir,name);hasher=hashlib.sha256();chunks=[]
        with path.open('rb') as stream:
            while True:
                chunk=stream.read(min(1024*1024,max_bytes-size+1))
                if not chunk:break
                size+=len(chunk)
                if size>max_bytes:fail('EVIDENCE_LIMIT',status='BLOCKED')
                hasher.update(chunk)
                if name in ('request.json','observations.json'):chunks.append(chunk)
        if hasher.hexdigest()!=expected:fail('EVIDENCE_HASH',name)
        if name in ('request.json','observations.json'):buffers[name]=b''.join(chunks)
    return manifest,buffers,size


def judge_run(run_dir,root=ROOT):
    from map_io import strict_loads
    sealed,buffers,size=seal_check(run_dir)
    if set(buffers)!={'request.json','observations.json'}:fail('MISSING_EVIDENCE')
    request=strict_loads(buffers['request.json'].decode('utf-8'));preflight=validate_request(request,root)
    if size>request['max_evidence_bytes']:fail('EVIDENCE_LIMIT',status='BLOCKED')
    raw=strict_loads(buffers['observations.json'].decode('utf-8'))
    obj(raw,{'run_id','nonce','request_sha256','cases','execution_status'},'observations')
    if raw['run_id']!=sealed['run_id'] or raw['nonce']!=sealed['nonce'] or raw['request_sha256']!=preflight['request_sha256']:fail('STALE_EVIDENCE')
    if type(raw['cases']) is not list:fail('OBSERVED_CASES')
    observed=[c.get('case_id') for c in raw['cases'] if type(c) is dict]
    if len(observed)!=len(raw['cases']) or observed!=request['expected_case_ids']:fail('OBSERVATION_CASE_SET')
    if raw['execution_status'] not in ('PASS','FAIL','BLOCKED','NOT_RUN'):fail('EXECUTION_STATUS')
    # Until all frame/snapshot/FI/display/cleanup fields have consumers, do not
    # accept a producer-supplied success flag as independent runtime judgment.
    return {'status':'FAIL' if raw['execution_status']=='FAIL' else 'BLOCKED',
            'scope':'SEALED_EVIDENCE_PREFLIGHT_ONLY','run_id':sealed['run_id'],
            'nonce':sealed['nonce'],'execution_status':raw['execution_status'],
            'missing_evidence':preflight['missing_evidence'],'g2':'NOT_RUN','g3':'NOT_RUN'}


def main(argv=None):
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('command',choices=['prepare','judge'])
    p.add_argument('--request');p.add_argument('--run');args=p.parse_args(argv)
    out=ROOT/'output/map-full-026'/('preflight-'+uuid.uuid4().hex);out.mkdir(parents=True)
    result={'status':'NOT_RUN','execution_status':'NOT_RUN'}
    try:
        if args.command=='prepare':
            if not args.request:fail('REQUEST_REQUIRED')
            request_path=reject_symlinks(Path(args.request).absolute())
            if not request_path.is_relative_to(ROOT):fail('PATH_ESCAPE')
            result=validate_request(load_json(request_path,64*1024*1024))
        else:
            if not args.run:fail('RUN_REQUIRED')
            run=reject_symlinks(Path(args.run).absolute())
            if not run.is_relative_to(ROOT/'output'):fail('PATH_ESCAPE')
            result=judge_run(run)
    except (MapInputError,OSError,ValueError,KeyError,TypeError) as e:
        result={'status':getattr(e,'status','FAIL'),'error':getattr(e,'code',type(e).__name__),'detail':str(e),'execution_status':'NOT_RUN'}
    result.update(time=datetime.now(ZoneInfo('Asia/Seoul')).isoformat(),producer_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    (out/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2,allow_nan=False)+'\n')
    print(json.dumps({'status':result['status'],'report':str(out/'summary.json'),'execution_status':result.get('execution_status'),'error':result.get('error')},ensure_ascii=False))
    return {'PASS':0,'FAIL':1,'BLOCKED':2,'NOT_RUN':2}[result['status']]

if __name__=='__main__':raise SystemExit(main())

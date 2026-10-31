"""I1 local recording protocol. No Unity adapter, motion judge or G2 acceptance."""
import argparse
from contextlib import contextmanager
from datetime import datetime
import fcntl
import hashlib
import json
import math
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import uuid
from zoneinfo import ZoneInfo

from map_io import MapInputError, load_json, read_input_bytes, reject_symlinks, safe_relative_path, strict_loads

ROOT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
SCOPE = 'PROTOCOL_FIXTURE_ONLY'
IDENTITY = {'run_id', 'nonce', 'case_id'}
REASONS = {'completed', 'cancelled', 'timeout', 'interrupted'}


def require(condition, code, message='', status='FAIL'):
    if not condition:
        raise MapInputError(code, message or code, status)


def exact(value, keys):
    require(isinstance(value, dict) and set(value) == set(keys), 'INVALID_FIELDS')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def digest(path):
    return sha(read_input_bytes(path, 8*1024*1024))


def now():
    return datetime.now(ZoneInfo('Asia/Seoul')).isoformat()


def boot_identity():
    linux = Path('/proc/sys/kernel/random/boot_id')
    if linux.is_file():
        return 'linux:' + linux.read_text().strip()
    try:
        proc = subprocess.run(['/usr/sbin/sysctl', '-n', 'kern.boottime'],
                              capture_output=True, text=True, timeout=5)
        match = re.search(r'sec\s*=\s*(\d+),\s*usec\s*=\s*(\d+)', proc.stdout)
        if proc.returncode == 0 and match:
            return 'macos:' + ':'.join(match.groups())
    except (OSError, subprocess.TimeoutExpired):
        pass
    raise MapInputError('BOOT_ID_UNAVAILABLE', 'Cannot establish clock continuity', 'BLOCKED')


def producers():
    return {name: digest(TOOLS/name) for name in ('map_session.py', 'map_io.py')}


def atomic(path, raw):
    reject_symlinks(path)
    temp = path.with_name('.pending-' + uuid.uuid4().hex)
    with temp.open('xb') as stream:
        stream.write(raw); stream.flush(); os.fsync(stream.fileno())
    os.replace(temp, path)
    fd = os.open(path.parent, os.O_RDONLY)
    try: os.fsync(fd)
    finally: os.close(fd)


def save(path, value):
    atomic(path, (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False)+'\n').encode())


@contextmanager
def lock(folder):
    path = reject_symlinks(folder/'.lock')
    with path.open('a') as handle:
        try: fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise MapInputError('RUN_LOCKED', 'Another command owns this run', 'BLOCKED') from error
        try: yield
        finally: fcntl.flock(handle, fcntl.LOCK_UN)


def project_file(value):
    path = Path(value)
    if path.is_absolute():
        require('..' not in path.parts, 'INVALID_PATH')
        path = reject_symlinks(path)
        require(path.resolve().is_relative_to(ROOT.resolve()), 'PATH_ESCAPE')
        return safe_relative_path(ROOT, str(path.relative_to(ROOT)))
    return safe_relative_path(ROOT, str(value))


def run_folder(run_id):
    require(isinstance(run_id, str) and re.fullmatch('[0-9a-f]{32}', run_id), 'INVALID_RUN_ID')
    path = reject_symlinks(ROOT/'output/verification/map-sessions'/run_id)
    require(path.is_dir(), 'MISSING_RUN', status='BLOCKED')
    return path


def identifier(value):
    return isinstance(value, str) and re.fullmatch('[A-Za-z0-9_-]{1,64}', value) is not None


def count(value, low, high):
    return type(value) is int and low <= value <= high


def validate_plan(plan):
    exact(plan, {'schema_version','document_role','scope','suite_id','revision',
                 'expected_case_ids','cases','inputs','wall_timeout_s','max_payload_bytes'})
    require(type(plan['schema_version']) is int and plan['schema_version']==1
            and plan['document_role']=='I1_COLLECTION_PLAN' and plan['scope']==SCOPE, 'UNSUPPORTED_PLAN')
    require(identifier(plan['suite_id']) and identifier(plan['revision']), 'INVALID_ID')
    ids=plan['expected_case_ids']; cases=plan['cases']
    require(isinstance(ids,list) and 0<len(ids)<=128 and all(identifier(x) for x in ids), 'INVALID_CASE_IDS')
    require(len(ids)==len(set(ids)), 'DUPLICATE_CASE')
    require(isinstance(cases,list) and len(cases)==len(ids), 'CASE_SET_MISMATCH')
    for case in cases:
        exact(case, {'case_id','step_count','event_count'})
        require(count(case['step_count'],1,10000) and count(case['event_count'],0,10000), 'INVALID_COUNTS')
    require([c['case_id'] for c in cases]==ids, 'CASE_SET_MISMATCH')
    value=plan['wall_timeout_s']
    require(type(value) in (int,float) and math.isfinite(value) and 0<value<=3600, 'INVALID_TIMEOUT')
    require(count(plan['max_payload_bytes'],1,64*1024*1024), 'INVALID_PAYLOAD_LIMIT')
    inputs=plan['inputs']; paths=[]
    require(isinstance(inputs,list) and 0<len(inputs)<=128, 'INVALID_INPUTS')
    for item in inputs:
        exact(item, {'path','sha256'})
        require(isinstance(item['sha256'],str) and re.fullmatch('[0-9a-f]{64}',item['sha256']), 'INVALID_HASH')
        safe_relative_path(ROOT,item['path'])
        paths.append(item['path'])
    require(len(paths)==len(set(paths)), 'DUPLICATE_INPUT')
    return plan


def input_hashes(plan):
    result={item['path']:digest(safe_relative_path(ROOT,item['path'])) for item in plan['inputs']}
    require(all(result[i['path']]==i['sha256'] for i in plan['inputs']), 'INPUT_CHANGED', status='BLOCKED')
    return result


def append(folder, manifest, rows, phase, **details):
    elapsed=time.monotonic()-manifest['started_monotonic']
    require(math.isfinite(elapsed) and elapsed>=0, 'CLOCK_RESET', status='BLOCKED')
    row={'seq':len(rows)+1,'run_id':manifest['run_id'],'nonce':manifest['nonce'],
         'phase':phase,'timestamp':now(),'elapsed_s':elapsed,'pid':os.getpid(),'details':details}
    require(len(rows)<1024, 'JOURNAL_LIMIT', status='BLOCKED')
    raw=''.join(json.dumps(x,ensure_ascii=False,allow_nan=False)+'\n' for x in [*rows,row]).encode()
    require(len(raw)<=2*1024*1024, 'JOURNAL_LIMIT', status='BLOCKED')
    atomic(folder/'journal.jsonl',raw); rows.append(row)


def create(plan_path):
    raw=read_input_bytes(project_file(plan_path)); plan=validate_plan(strict_loads(raw.decode('utf-8')))
    inputs=input_hashes(plan); source=producers(); boot=boot_identity()
    base=reject_symlinks(ROOT/'output/verification/map-sessions');base.mkdir(parents=True,exist_ok=True)
    folder=base/uuid.uuid4().hex;folder.mkdir()
    manifest={'schema_version':1,'scope':SCOPE,'run_id':folder.name,'nonce':uuid.uuid4().hex,
              'created_at':now(),'started_monotonic':time.monotonic(),'boot_id':boot,
              'request_sha256':sha(raw),'source_hashes':source,'input_hashes':inputs,
              'expected_case_ids':plan['expected_case_ids']}
    with lock(folder):
        atomic(folder/'request.json',raw);save(folder/'manifest.json',manifest)
        append(folder,manifest,[],'READY',manifest_sha256=digest(folder/'manifest.json'))
        require(inputs==input_hashes(plan) and source==producers(), 'INIT_INPUT_CHANGED', status='BLOCKED')
    return folder


def validate_packet(packet, manifest, case):
    exact(packet, {'schema_version','scope','run_id','nonce','case_id','start_marker','end_marker','steps','events'})
    require(type(packet['schema_version']) is int and packet['schema_version']==1 and packet['scope']==SCOPE, 'INVALID_PACKET_SCHEMA')
    identity={'run_id':manifest['run_id'],'nonce':manifest['nonce'],'case_id':case['case_id']}
    def identity_matches(value):
        require(all(value.get(k)==v for k,v in identity.items()), 'EVIDENCE_MISMATCH')
    identity_matches(packet)
    for key in ('start_marker','end_marker'):
        marker=packet[key];exact(marker,IDENTITY|{'frame_id'});identity_matches(marker)
        require(type(marker['frame_id']) is int and marker['frame_id']>=0, 'INVALID_MARKER')
    steps,events=packet['steps'],packet['events']
    require(isinstance(steps,list) and len(steps)==case['step_count'], 'MISSING_STEPS')
    require(isinstance(events,list) and len(events)==case['event_count'], 'MISSING_EVENTS')
    previous=-1
    for i,step in enumerate(steps):
        exact(step, IDENTITY|{'step_id','frame_id','observer_sequence','payload'});identity_matches(step)
        require(type(step['step_id']) is int and step['step_id']==i
                and type(step['observer_sequence']) is int and step['observer_sequence']==i+1, 'STEP_SEQUENCE')
        frame=step['frame_id'];require(type(frame) is int and frame>previous, 'FRAME_SEQUENCE');previous=frame
        require(isinstance(step['payload'],dict), 'INVALID_PAYLOAD')
    require(packet['start_marker']['frame_id']<=steps[0]['frame_id']
            and steps[-1]['frame_id']<=packet['end_marker']['frame_id'], 'MARKER_RANGE')
    for i,event in enumerate(events):
        exact(event, IDENTITY|{'event_sequence','step_id','payload'});identity_matches(event)
        require(type(event['event_sequence']) is int and event['event_sequence']==i+1
                and count(event['step_id'],0,len(steps)-1), 'EVENT_SEQUENCE')
        require(isinstance(event['payload'],dict), 'INVALID_PAYLOAD')


def packet_path(folder, case_id):
    return safe_relative_path(folder, 'cases/'+case_id+'/packet.json', must_exist=False)


def read_state(folder):
    manifest=load_json(folder/'manifest.json')
    exact(manifest, {'schema_version','scope','run_id','nonce','created_at','started_monotonic',
                     'boot_id','request_sha256','source_hashes','input_hashes','expected_case_ids'})
    require(type(manifest['schema_version']) is int and manifest['schema_version']==1
            and manifest['scope']==SCOPE and manifest['run_id']==folder.name
            and isinstance(manifest['nonce'],str) and re.fullmatch('[0-9a-f]{32}',manifest['nonce'])
            and manifest['nonce']!=manifest['run_id'], 'INVALID_MANIFEST')
    require(isinstance(manifest['boot_id'],str) and bool(manifest['boot_id'])
            and type(manifest['started_monotonic']) in (int,float)
            and math.isfinite(manifest['started_monotonic']) and manifest['started_monotonic']>=0, 'INVALID_CLOCK')
    rows=[strict_loads(line) for line in read_input_bytes(folder/'journal.jsonl',2*1024*1024).decode('utf-8').splitlines()]
    require(0<len(rows)<=1024 and rows[0].get('phase')=='READY', 'INVALID_JOURNAL')
    require(rows[0].get('details',{}).get('manifest_sha256')==digest(folder/'manifest.json'), 'MANIFEST_CHANGED')
    raw=read_input_bytes(folder/'request.json');require(sha(raw)==manifest['request_sha256'], 'REQUEST_CHANGED')
    plan=validate_plan(strict_loads(raw.decode('utf-8')))
    require(manifest['expected_case_ids']==plan['expected_case_ids'] and manifest['input_hashes']==input_hashes(plan), 'PLAN_CHANGED')
    require(manifest['source_hashes']==producers(), 'PRODUCER_CHANGED', status='BLOCKED')
    cases={x['case_id']:x for x in plan['cases']};accepted={};pending=None;closing=None;closed=None;rejections=[];last=-1
    for i,row in enumerate(rows):
        exact(row, {'seq','run_id','nonce','phase','timestamp','elapsed_s','pid','details'})
        require(type(row['seq']) is int and row['seq']==i+1 and row['run_id']==manifest['run_id']
                and row['nonce']==manifest['nonce'] and isinstance(row['details'],dict), 'JOURNAL_IDENTITY')
        elapsed=row['elapsed_s'];require(type(elapsed) in (int,float) and math.isfinite(elapsed) and elapsed>=0 and elapsed>=last, 'JOURNAL_CLOCK');last=elapsed
        require(type(row['pid']) is int and row['pid']>0 and isinstance(row['timestamp'],str), 'INVALID_JOURNAL_METADATA')
        try: stamp=datetime.fromisoformat(row['timestamp'])
        except ValueError: raise MapInputError('INVALID_TIMESTAMP','Journal timestamp is not ISO8601') from None
        require(stamp.utcoffset() is not None,'INVALID_TIMESTAMP')
        phase,detail=row['phase'],row['details'];require(closed is None,'AFTER_FINALIZED')
        if i==0: continue
        if phase=='COLLECT_PLANNED':
            require(pending is None and closing is None, 'INVALID_TRANSITION')
            exact(detail,{'case_id','sha256','bytes'});cid=detail['case_id']
            require(identifier(cid) and cid in cases and cid not in accepted and isinstance(detail['sha256'],str)
                    and re.fullmatch('[0-9a-f]{64}',detail['sha256']) and count(detail['bytes'],1,8*1024*1024), 'INVALID_COLLECTION_PLAN')
            pending=detail
        elif phase=='ACCEPTED':
            require(closing is None and pending is not None and detail==pending, 'INVALID_TRANSITION')
            cid=detail['case_id'];path=packet_path(folder,cid)
            require(path.is_file(), 'MISSING_PACKET')
            data=read_input_bytes(path,8*1024*1024)
            require(sha(data)==detail['sha256'] and len(data)==detail['bytes'], 'ARTIFACT_CHANGED')
            validate_packet(strict_loads(data.decode('utf-8')),manifest,cases[cid]);accepted[cid]=detail;pending=None
        elif phase=='REJECTED':
            require(pending is None and closing is None, 'INVALID_TRANSITION')
            exact(detail,{'code','status'});require(detail['status'] in ('FAIL','BLOCKED') and isinstance(detail['code'],str), 'INVALID_REJECTION')
            rejections.append(detail)
        elif phase=='FINALIZING':
            require(closing is None and (pending is None or detail.get('reason')!='completed'), 'INVALID_TRANSITION')
            exact(detail,{'reason'});require(detail['reason'] in REASONS, 'INVALID_CLOSE_REASON');closing=row
        elif phase=='FINALIZED':
            require(closing is not None, 'INVALID_TRANSITION')
            exact(detail,{'summary_sha256','cleanup_sha256'});closed=detail
        else:
            raise MapInputError('INVALID_TRANSITION',phase)
    require(sum(x['bytes'] for x in accepted.values())<=plan['max_payload_bytes'],'EVIDENCE_LIMIT')
    pending_evidence=None
    if pending:
        path=packet_path(folder,pending['case_id'])
        pending_evidence={'case_id':pending['case_id'],'exists':path.is_file(),'sha256':None,'bytes':None}
        if path.is_file():
            data=read_input_bytes(path,8*1024*1024)
            require(sha(data)==pending['sha256'] and len(data)==pending['bytes'],'UNCOMMITTED_ARTIFACT_CHANGED')
            validate_packet(strict_loads(data.decode('utf-8')),manifest,cases[pending['case_id']])
            pending_evidence.update(sha256=sha(data),bytes=len(data))
    known={'request.json','manifest.json','journal.jsonl','.lock'}
    known.update('cases/'+x+'/packet.json' for x in accepted)
    if pending: known.add('cases/'+pending['case_id']+'/packet.json')
    if closing: known.update({'summary.json','cleanup.json'})
    leftovers={}
    for item in folder.rglob('*'):
        reject_symlinks(item)
        if item.is_file():
            rel=str(item.relative_to(folder))
            if re.fullmatch(r'\.pending-[0-9a-f]{32}',item.name): leftovers[rel]=digest(item)
            else: require(rel in known, 'UNEXPECTED_ARTIFACT',rel)
    state={'folder':folder,'manifest':manifest,'plan':plan,'rows':rows,'accepted':accepted,
           'pending':pending,'pending_evidence':pending_evidence,'closing':closing,'closed':closed,
           'rejections':rejections,'leftovers':leftovers}
    if closed:
        require(digest(folder/'summary.json')==closed['summary_sha256']
                and digest(folder/'cleanup.json')==closed['cleanup_sha256'], 'FINAL_ARTIFACT_CHANGED')
        require(load_json(folder/'cleanup.json')==cleanup(manifest), 'INVALID_CLEANUP')
        require(load_json(folder/'summary.json')==summarize(state), 'INVALID_SUMMARY')
    else:
        start=manifest['started_monotonic'];require(type(start) in (int,float) and math.isfinite(start), 'INVALID_CLOCK')
        require(manifest['boot_id']==boot_identity(), 'BOOT_CHANGED', status='BLOCKED')
        require(time.monotonic()-start>=last, 'CLOCK_RESET', status='BLOCKED')
    return state


def expired(state):
    return time.monotonic()-state['manifest']['started_monotonic']>state['plan']['wall_timeout_s']


def collect(run_id, packet_file):
    folder=run_folder(run_id)
    with lock(folder):
        state=read_state(folder);m=state['manifest'];rows=state['rows']
        require(not state['closed'] and not state['closing'], 'RUN_CLOSED', status='BLOCKED')
        require(state['pending'] is None, 'UNCOMMITTED_COLLECTION', status='BLOCKED')
        try:
            require(not expired(state), 'TIMEOUT', status='BLOCKED')
            require(len(rows)<1000, 'JOURNAL_LIMIT', status='BLOCKED')
            raw=read_input_bytes(project_file(packet_file),min(8*1024*1024,state['plan']['max_payload_bytes']))
            packet=strict_loads(raw.decode('utf-8'));require(isinstance(packet,dict), 'INVALID_PACKET')
            cid=packet.get('case_id');cases={x['case_id']:x for x in state['plan']['cases']}
            require(isinstance(cid,str) and cid in cases, 'UNEXPECTED_CASE')
            require(cid not in state['accepted'], 'DUPLICATE_CASE')
            validate_packet(packet,m,cases[cid])
            require(sum(x['bytes'] for x in state['accepted'].values())+len(raw)<=state['plan']['max_payload_bytes'], 'EVIDENCE_LIMIT', status='BLOCKED')
            destination=packet_path(folder,cid);require(not destination.exists(), 'ARTIFACT_EXISTS')
        except (MapInputError,UnicodeError) as error:
            if isinstance(error,UnicodeError): error=MapInputError('INVALID_UTF8',str(error))
            append(folder,m,rows,'REJECTED',code=error.code,status=error.status)
            raise error
        details={'case_id':cid,'sha256':sha(raw),'bytes':len(raw)}
        append(folder,m,rows,'COLLECT_PLANNED',**details)
        destination.parent.mkdir(parents=True,exist_ok=True);atomic(destination,raw)
        append(folder,m,rows,'ACCEPTED',**details)
        read_state(folder)
        return {'status':'PASS','scope':SCOPE,'unity_status':'NOT_RUN','run_id':run_id,'case_id':cid,'report':str(destination)}


def cleanup(manifest):
    return {'schema_version':1,'run_id':manifest['run_id'],'nonce':manifest['nonce'],
            'status':'NOT_RUN','scope':'UNITY_ADAPTER_UNIMPLEMENTED','owned_external_resources':[],
            'note':'Only local files and per-command OS lock; no Unity cleanup verified.'}


def summarize(state):
    m=state['manifest'];collected=[x for x in m['expected_case_ids'] if x in state['accepted']]
    missing=[x for x in m['expected_case_ids'] if x not in state['accepted']]
    reason=state['closing']['details']['reason']
    if reason=='completed' and state['closing']['elapsed_s']>state['plan']['wall_timeout_s']:
        reason='timeout'
    codes=[x['code'] for x in state['rejections']]
    if missing: codes.append('MISSING_CASES')
    if state['pending']: codes.append('UNCOMMITTED_COLLECTION')
    if state['leftovers']: codes.append('UNCOMMITTED_FILES')
    if reason!='completed': codes.append(reason.upper())
    collector=('FAIL' if any(x['status']=='FAIL' for x in state['rejections']) else
               'BLOCKED' if codes else 'PASS')
    evidence={'request.json':digest(state['folder']/'request.json'),'manifest.json':digest(state['folder']/'manifest.json')}
    for cid in collected: evidence['cases/'+cid+'/packet.json']=state['accepted'][cid]['sha256']
    return {'schema_version':1,'status':'NOT_RUN' if collector=='PASS' else collector,'scope':SCOPE,
            'run_id':m['run_id'],'nonce':m['nonce'],'collector_status':collector,
            'unity_status':'NOT_RUN','cleanup_status':'NOT_RUN','ordinary_cases_status':'NOT_RUN',
            'fi_detection_status':'NOT_RUN','axis_status':{x:'NOT_RUN' for x in 'FRBHC'},
            'expected_case_ids':m['expected_case_ids'],'collected_case_ids':collected,'missing_case_ids':missing,
            'reason_codes':list(dict.fromkeys(codes)),'close_reason':reason,
            'finished_at':state['closing']['timestamp'],'elapsed_s':state['closing']['elapsed_s'],
            'evidence_sha256':evidence,'pending_evidence':state['pending_evidence'],
            'uncommitted_files':state['leftovers']}


def finish(run_id, reason='completed'):
    require(reason in REASONS,'INVALID_CLOSE_REASON');folder=run_folder(run_id)
    with lock(folder):
        state=read_state(folder)
        if state['closed']:
            require(reason=='completed','RUN_CLOSED',status='BLOCKED')
            return load_json(folder/'summary.json')
        require(state['closing'] is None,'INCOMPLETE_FINALIZATION',status='BLOCKED')
        require(not state['pending'] or reason!='completed','UNCOMMITTED_COLLECTION',status='BLOCKED')
        if expired(state) and reason=='completed': reason='timeout'
        append(folder,state['manifest'],state['rows'],'FINALIZING',reason=reason)
        state['closing']=state['rows'][-1]
        save(folder/'cleanup.json',cleanup(state['manifest']));save(folder/'summary.json',summarize(state))
        append(folder,state['manifest'],state['rows'],'FINALIZED',summary_sha256=digest(folder/'summary.json'),cleanup_sha256=digest(folder/'cleanup.json'))
        read_state(folder)
        return load_json(folder/'summary.json')


def inspect(run_id):
    folder=run_folder(run_id)
    with lock(folder):
        state=read_state(folder)
        if state['closed']: return load_json(folder/'summary.json')
        codes=[]
        if state['pending']: codes.append('UNCOMMITTED_COLLECTION')
        if state['closing']: codes.append('INCOMPLETE_FINALIZATION')
        if expired(state): codes.append('TIMEOUT')
        status='FAIL' if any(x['status']=='FAIL' for x in state['rejections']) else 'BLOCKED' if codes or state['rejections'] else 'NOT_RUN'
        return {'status':status,'scope':SCOPE,'unity_status':'NOT_RUN','run_id':run_id,
                'collected':list(state['accepted']),'expected':state['manifest']['expected_case_ids'],
                'reason_codes':codes+[x['code'] for x in state['rejections']], 'report':str(folder/'journal.jsonl')}


def main():
    parser=argparse.ArgumentParser(description=__doc__);subs=parser.add_subparsers(dest='command',required=True)
    subs.add_parser('init').add_argument('--plan',required=True)
    for name in ('collect','finalize','abort','status'):
        sub=subs.add_parser(name);sub.add_argument('--run',required=True)
        if name=='collect': sub.add_argument('--packet',required=True)
        if name=='abort': sub.add_argument('--reason',choices=sorted(REASONS-{'completed'}),required=True)
    args=parser.parse_args()
    try:
        if args.command=='init':
            folder=create(args.plan);m=load_json(folder/'manifest.json')
            result={'status':'PASS','scope':SCOPE,'unity_status':'NOT_RUN','run_id':folder.name,'nonce':m['nonce'],'report':str(folder/'manifest.json')}
        elif args.command=='collect': result=collect(args.run,args.packet)
        elif args.command=='status': result=inspect(args.run)
        else:
            result=finish(args.run,args.reason if args.command=='abort' else 'completed')
            result={k:result[k] for k in ('status','scope','run_id','collector_status','unity_status','reason_codes')}
            result['report']=str(run_folder(args.run)/'summary.json')
        print(json.dumps(result,ensure_ascii=False))
        return 0 if result['status']=='PASS' else 1 if result['status']=='FAIL' else 2
    except (MapInputError,OSError,UnicodeError,KeyError,TypeError,AttributeError) as error:
        status=error.status if isinstance(error,MapInputError) else 'BLOCKED'
        result={'status':status,'scope':SCOPE,'unity_status':'NOT_RUN','code':getattr(error,'code','RECORDING_ERROR'),'error':str(error)[:400]}
        try:
            folder=reject_symlinks(ROOT/'output/verification/map-session-errors');folder.mkdir(parents=True,exist_ok=True)
            path=folder/(uuid.uuid4().hex+'.json');save(path,{**result,'time':now(),'run_id':getattr(args,'run',None)})
            result['report']=str(path)
        except (OSError,MapInputError): pass
        print(json.dumps(result,ensure_ascii=False));return 1 if status=='FAIL' else 2


if __name__=='__main__': raise SystemExit(main())

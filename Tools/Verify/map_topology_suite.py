#!/usr/bin/env python3
"""Fresh 12-configuration topology diagnostics; no Unity/G2/G3 acceptance."""
import argparse
from concurrent.futures import ThreadPoolExecutor,as_completed
from datetime import datetime
from fractions import Fraction as F
import hashlib,json,os,re,subprocess,sys,time,uuid
from pathlib import Path
from zoneinfo import ZoneInfo
import map_topology as h
from map_io import canonical_hash,load_json,read_input_bytes,reject_symlinks,safe_relative_path,strict_loads
ROOT=h.ROOT
ROLE='TOPOLOGY_SUITE_DIAGNOSTIC_029'
EXPECTED=[f'N{n}-F{f}' for n in range(1,5) for f in range(3)]
ARTIFACTS={'decomposition.json','incidences.json','unresolved.json'}

def now():return datetime.now(ZoneInfo('Asia/Seoul')).isoformat()
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def atomic(path,value,replace=False):
    reject_symlinks(path)
    if path.exists() and not replace:raise ValueError("SUITE_OUTPUT_EXISTS")
    raw=json.dumps(value,ensure_ascii=False,indent=2,allow_nan=False).encode()
    if len(raw)>1024*1024:raise ValueError('SUITE_OUTPUT_LIMIT')
    temporary=path.with_suffix(path.suffix+'.pending')
    with temporary.open('xb') as stream:stream.write(raw);stream.flush();os.fsync(stream.fileno())
    temporary.replace(path)

def source_inputs(profile):
    bundle=load_json(safe_relative_path(ROOT,profile));layout=load_json(ROOT/'docs/design/map-meadow-007-r001-layout.json')
    h.verify_production_profile(layout,bundle)
    paths=['docs/design/map-meadow-007-r001-layout.json',profile,'Tools/Verify/map_topology.py','Tools/Verify/map_oracle.py','Tools/Verify/map_io.py']
    child={p:digest(safe_relative_path(ROOT,p)) for p in paths};child.update(bundle['sources']);child[bundle['raw_path']]=bundle['raw_sha256']
    inputs=dict(child);inputs['Tools/Verify/map_topology_suite.py']=digest(Path(__file__));inputs['Tools/project_paths.py']=digest(ROOT/'Tools/project_paths.py')
    return inputs,child

def aggregate(rows):
    ids=[r['case_id'] for r in rows]
    if len(set(ids))!=len(ids) or not set(ids)<=set(EXPECTED):raise ValueError('DUPLICATE_OR_FOREIGN_CASE')
    for row in rows:
        if row.get('certification_status') not in ('PASS','FAIL','BLOCKED','NOT_RUN') or row.get('connectivity_status') not in ('PASS','FAIL','BLOCKED','NOT_RUN'):raise ValueError('UNKNOWN_CASE_STATUS')
    missing=[id for id in EXPECTED if id not in ids]
    certification='FAIL' if any(r['certification_status']=='FAIL' for r in rows) else 'BLOCKED' if any(r['certification_status']=='BLOCKED' for r in rows) else 'NOT_RUN' if missing or any(r['certification_status']=='NOT_RUN' for r in rows) else 'PASS'
    connectivity='FAIL' if any(r['connectivity_status']=='FAIL' for r in rows) else 'BLOCKED' if any(r['connectivity_status']=='BLOCKED' for r in rows) else 'NOT_RUN' if missing or any(r['connectivity_status']=='NOT_RUN' for r in rows) else 'PASS'
    status=next(s for s in ('FAIL','BLOCKED','NOT_RUN','PASS') if s in (certification,connectivity))
    return dict(status=status,certification_status=certification,connectivity_status=connectivity,expected=EXPECTED,completed=ids,missing=missing)

def validate_invocation(value,row,manifest,manifest_sha,pid):
    expected=dict(mode='SUITE_CHILD',suite_id=manifest['suite_id'],case_id=row['case_id'],launch_nonce=row['launch_nonce'],parent_manifest_sha256=manifest_sha)
    if value['invocation']!=expected or value['worker_pid']!=pid:raise ValueError('CHILD_INVOCATION')
    if datetime.fromisoformat(value['started_at'])<datetime.fromisoformat(manifest['created_at']):raise ValueError('OLD_CHILD_RESULT')

def artifact_bytes(folder,rows):
    if type(rows) is not dict or set(rows)!=ARTIFACTS:raise ValueError('ARTIFACT_SET')
    blobs={}
    for name,row in rows.items():
        if type(row) is not dict or set(row)!={'sha256','bytes'} or type(row['bytes']) is not int or not 0<=row['bytes']<=128*1024*1024:raise ValueError('ARTIFACT_SIZE')
        path=reject_symlinks(folder/name)
        if path.stat().st_size!=row['bytes']:raise ValueError('ARTIFACT_SIZE')
        with path.open('rb') as stream:raw=stream.read(row['bytes']+1)
        if len(raw)!=row['bytes'] or hashlib.sha256(raw).hexdigest()!=row['sha256']:raise ValueError('ARTIFACT_HASH')
        blobs[name]=raw
    return blobs

def structural_check(cells,radius,result,certificates,budget):
    count=len(cells);boxes=[h.bbox(c,radius) for c in cells];expected_pairs=set()
    for a in range(count):
        for b in range(a+1,count):
            budget.check()
            if h.boxes_overlap(boxes[a],boxes[b]):
                expected_pairs.add((a,b))
                if len(expected_pairs)>300000:raise h.TopologyBlocked('PARENT_PAIR_LIMIT')
    if type(certificates) is not list:raise ValueError('INCIDENCE_CONTAINER')
    edge_rows={};triangle_rows={}
    for row in certificates:
        budget.check()
        if type(row) is not dict:raise ValueError('INCIDENCE_ROW')
        indices=row.get('cells');kind=row.get('kind')
        size=2 if kind=='edge' else 3 if kind=='triangle' else 0
        if not size or type(indices) is not list or len(indices)!=size or any(type(i) is not int or not 0<=i<count for i in indices) or indices!=sorted(set(indices)):raise ValueError('INCIDENCE_ID')
        if type(row.get('nonempty')) is not bool:raise ValueError('UNRESOLVED_IN_SEALED_RESULT')
        table=edge_rows if size==2 else triangle_rows;key=tuple(indices)
        if key in table:raise ValueError('DUPLICATE_INCIDENCE')
        table[key]=row['nonempty']
    if set(edge_rows)!=expected_pairs or result['pair_candidates']!=len(expected_pairs) or result['pair_excluded']!=count*(count-1)//2-len(expected_pairs):raise ValueError('PAIR_COMPLETENESS')
    positive=sorted(k for k,v in edge_rows.items() if v);neighbors=[set() for _ in cells]
    for a,b in positive:neighbors[a].add(b);neighbors[b].add(a)
    cliques=set()
    for a,b in positive:
        budget.check()
        for c in neighbors[a]&neighbors[b]:
            budget.check()
            if c>b:
                cliques.add((a,b,c))
                if len(cliques)>600000:raise h.TopologyBlocked('PARENT_CLIQUE_LIMIT')
    if set(triangle_rows)!=cliques or result['triple_candidates']!=len(cliques):raise ValueError('CLIQUE_COMPLETENESS')
    seen=set();beta0=0
    for seed in range(count):
        if seed in seen:continue
        beta0+=1;stack=[seed];seen.add(seed)
        while stack:
            budget.check();node=stack.pop()
            for other in neighbors[node]-seen:seen.add(other);stack.append(other)
    # Independent ordering: DFS components and least-bit Gaussian elimination.
    edge_index={edge:i for i,edge in enumerate(positive)};pivots={};rank_bytes=0
    triangles=sorted(k for k,v in triangle_rows.items() if v)
    for a,b,c in triangles:
        vector=sum(1<<edge_index[p] for p in ((a,b),(a,c),(b,c)))
        while vector:
            budget.check();pivot=vector&-vector
            if pivot in pivots:vector^=pivots[pivot]
            else:
                rank_bytes+=sys.getsizeof(vector)
                if rank_bytes>128*1024*1024:raise h.TopologyBlocked('PARENT_RANK_MEMORY_LIMIT')
                pivots[pivot]=vector;break
    components=len(positive)-count+beta0-len(pivots)
    expected=dict(vertices=count,edges=len(positive),triangles=len(triangles),beta0=beta0,boundary2_rank=len(pivots),bounded_complement_components=components)
    if any(type(result.get(k)) is not int or result[k]!=v for k,v in expected.items()):raise ValueError('HOMOLOGY_MISMATCH')
    return expected

def judge_child(row,manifest,manifest_path,command):
    base=dict(case_id=row['case_id'],certification_status='BLOCKED',connectivity_status='BLOCKED',command_exit=command.get('exit_code'))
    if command.get('timeout'):return dict(base,error='CHILD_TIMEOUT',partial_outputs_retained=True)
    if command.get('exit_code')!=0:return dict(base,certification_status='FAIL' if command.get('exit_code')==1 else 'BLOCKED',error='CHILD_NONZERO_EXIT')
    receipt=strict_loads(command['stdout'].strip());path=reject_symlinks(Path(receipt['report'])).resolve(strict=True)
    if not path.is_relative_to(ROOT/'output/map-full-026') or path.name!='summary.json' or not path.parent.name.startswith('topology-'):raise ValueError('CHILD_REPORT_PATH')
    raw=read_input_bytes(path,65536);result=strict_loads(raw.decode());folder=path.parent
    if result.get('status')!='PASS' or result.get('scope')!='TOPOLOGY_DIAGNOSTIC_ONLY' or result.get('g2')!='NOT_RUN' or result.get('g3')!='NOT_RUN' or result.get('completion_marker')!='SEALED':raise ValueError('CHILD_NOT_SEALED')
    manifest_sha=digest(manifest_path);validate_invocation(result,row,manifest,manifest_sha,command['pid'])
    expected_inputs=dict(manifest['child_inputs']);expected_inputs[str(manifest_path.relative_to(ROOT))]=manifest_sha;expected_inputs[row['launch_path']]=digest(ROOT/row['launch_path'])
    if result['inputs']!=expected_inputs:raise ValueError('CHILD_INPUT_IDENTITY')
    if result.get('unresolved_count')!=0:raise ValueError('UNRESOLVED_CHILD')
    blobs=artifact_bytes(folder,result['artifacts']);data={name:strict_loads(raw.decode()) for name,raw in blobs.items()}
    if data['unresolved.json']!=[]:raise ValueError('UNRESOLVED_ARTIFACT')
    budget=h.Budget(seconds=120);layout=load_json(ROOT/'docs/design/map-meadow-007-r001-layout.json')
    cells,origins,radius,profile=h.candidate_cells(layout,row['count'],row['formation'],budget)
    decomposition=dict(profile=profile,cells=[dict(points=[[str(x),str(y)] for x,y in cell],origins=origin) for cell,origin in zip(cells,origins)])
    if data['decomposition.json']!=decomposition or result['profile']!=profile or result['cell_sha256']!=canonical_hash([r['points'] for r in decomposition['cells']]):raise ValueError('DECOMPOSITION_MISMATCH')
    values=structural_check(cells,radius,result,data['incidences.json'],budget)
    p01=not h.point_forbidden(cells,radius,(F(0),F(0)));connected=values['bounded_complement_components']==1 and p01
    if result.get('p01_safe') is not p01 or result.get('geometric_connectivity')!=('PASS' if connected else 'FAIL'):raise ValueError('CONNECTIVITY_MISMATCH')
    h.revalidate_sources(ROOT,manifest['inputs']);h.revalidate_sources(ROOT,result['inputs']);budget.check()
    if raw!=read_input_bytes(path,65536):raise ValueError('CHILD_EVIDENCE_CHANGED')
    artifact_bytes(folder,result['artifacts'])
    return dict(base,certification_status='PASS',connectivity_status='PASS' if connected else 'FAIL',report=str(path.relative_to(ROOT)),summary_sha256=hashlib.sha256(raw).hexdigest(),artifacts=result['artifacts'],original_started_at=result['started_at'],original_finished_at=result['time'],profile=profile,homology=values,p01_safe=p01,trust='Pinned 027 geometric incidence kernel; parent independently checks cover/frame regeneration, IDs/completeness, DFS/GF2')

def final_evidence_check(manifest_path,manifest_sha,launch_hashes,rows):
    if digest(manifest_path)!=manifest_sha:raise ValueError('MANIFEST_CHANGED')
    for name,sha in launch_hashes.items():
        if digest(reject_symlinks(ROOT/name))!=sha:raise ValueError('LAUNCH_CHANGED')
    for row in rows:
        if row.get('certification_status')!='PASS':continue
        report=reject_symlinks(safe_relative_path(ROOT,row['report']))
        raw=read_input_bytes(report,65536)
        if hashlib.sha256(raw).hexdigest()!=row['summary_sha256']:raise ValueError('CHILD_EVIDENCE_CHANGED')
        artifact_bytes(report.parent,row['artifacts'])

def execute(row,manifest,manifest_path,seconds):
    h.revalidate_sources(ROOT,manifest['inputs'])
    args=[sys.executable,str(ROOT/'Tools/Verify/map_topology.py'),'--count',str(row['count']),'--formation',str(row['formation']),'--seconds',str(seconds),'--profile',manifest['profile'],'--launch',row['launch_path']]
    started=now();p=subprocess.Popen(args,cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
    command=dict(args=args,pid=p.pid,started_at=started)
    try:stdout,stderr=p.communicate(timeout=seconds+30);command.update(exit_code=p.returncode,stdout=stdout,stderr=stderr)
    except subprocess.TimeoutExpired:
        p.kill();stdout,stderr=p.communicate();command.update(exit_code=p.returncode,stdout=stdout,stderr=stderr,timeout=True)
    command['finished_at']=now();atomic(manifest_path.parent/(row['case_id']+'-command.json'),command)
    try:return judge_child(row,manifest,manifest_path,command)
    except (ValueError,OSError,KeyError,TypeError,h.TopologyBlocked) as error:
        return dict(case_id=row['case_id'],certification_status='BLOCKED' if isinstance(error,h.TopologyBlocked) else 'FAIL',connectivity_status='BLOCKED',error=str(error))

def run(profile,seconds,jobs):
    inputs,child=source_inputs(profile);base=reject_symlinks(ROOT/'output/map-full-026');base.mkdir(exist_ok=True,parents=True);suite=uuid.uuid4().hex;out=base/('topology-suite-029-'+suite);out.mkdir()
    rows=[dict(case_id=f'N{n}-F{f}',count=n,formation=f,launch_nonce=uuid.uuid4().hex,launch_path=str((out/f'N{n}-F{f}-launch.json').relative_to(ROOT))) for n in range(1,5) for f in range(3)]
    manifest=dict(schema_version=1,document_role=ROLE,suite_id=suite,created_at=now(),profile=profile,seconds=seconds,jobs=jobs,expected_case_ids=EXPECTED,cases=rows,inputs=inputs,child_inputs=child)
    path=out/'manifest.json';atomic(path,manifest);sha=digest(path)
    for row in rows:
        launch=dict(schema_version=1,document_role='TOPOLOGY_CHILD_LAUNCH_029',suite_id=suite,case_id=row['case_id'],launch_nonce=row['launch_nonce'],manifest_path=str(path.relative_to(ROOT)),manifest_sha256=sha,count=row['count'],formation=row['formation'],seconds=seconds,profile=profile)
        atomic(ROOT/row['launch_path'],launch)
    launch_hashes={row['launch_path']:digest(ROOT/row['launch_path']) for row in rows}
    completed=[];summary=dict(scope=ROLE,g2='NOT_RUN',g3='NOT_RUN',suite_id=suite,manifest_sha256=sha,started_at=manifest['created_at'])
    try:
        with ThreadPoolExecutor(max_workers=jobs) as pool:
            futures=[pool.submit(execute,row,manifest,path,seconds) for row in rows]
            for future in as_completed(futures):
                completed.append(future.result());atomic(out/'progress.json',dict(**aggregate(completed),results=completed),replace=True)
        completed.sort(key=lambda r:EXPECTED.index(r['case_id']))
        paths=[r.get('report') for r in completed if r.get('report')]
        if len(set(paths))!=len(paths):raise ValueError('DUPLICATE_CHILD_DIRECTORY')
        h.revalidate_sources(ROOT,inputs)
        final_evidence_check(path,sha,launch_hashes,completed)
        summary.update(aggregate(completed),results=completed,completion_marker='SEALED')
    except (ValueError,OSError,KeyError,TypeError,h.TopologyBlocked) as error:summary.update(status='BLOCKED',error=str(error),results=completed,completion_marker='INCOMPLETE')
    summary['finished_at']=now();atomic(out/'summary.json',summary)
    print(json.dumps(dict(status=summary['status'],report=str(out/'summary.json'),completed=len(completed),scope=ROLE)))
    return 0 if summary['status']=='PASS' else 1 if summary['status']=='FAIL' else 2

def judge(path):
    path=reject_symlinks(path).resolve(strict=True)
    if not path.is_relative_to(ROOT/'output/map-full-026'):raise ValueError('SUITE_PATH')
    manifest_path=path/'manifest.json';manifest_raw=read_input_bytes(manifest_path,1048576);manifest=strict_loads(manifest_raw.decode());manifest_sha=hashlib.sha256(manifest_raw).hexdigest()
    if manifest['document_role']!=ROLE or manifest['expected_case_ids']!=EXPECTED or [r['case_id'] for r in manifest['cases']]!=EXPECTED:raise ValueError('SUITE_MANIFEST')
    if type(manifest.get('schema_version')) is not int or manifest['schema_version']!=1 or not re.fullmatch('[0-9a-f]{32}',manifest['suite_id']):raise ValueError('SUITE_SCHEMA')
    current,child=source_inputs(manifest['profile'])
    if current!=manifest['inputs'] or child!=manifest['child_inputs']:raise ValueError('SUITE_INPUT_SET')
    for row,(count,formation) in zip(manifest['cases'],[(n,f) for n in range(1,5) for f in range(3)]):
        if type(row['count']) is not int or type(row['formation']) is not int or (row['count'],row['formation'])!=(count,formation) or row['launch_path']!=str((path/(row['case_id']+'-launch.json')).relative_to(ROOT)) or not re.fullmatch('[0-9a-f]{32}',row['launch_nonce']):raise ValueError('SUITE_CASE_SCHEMA')
    launch_hashes={row['launch_path']:digest(reject_symlinks(ROOT/row['launch_path'])) for row in manifest['cases']}
    h.revalidate_sources(ROOT,manifest['inputs']);rows=[]
    for row in manifest['cases']:
        command=load_json(path/(row['case_id']+'-command.json'));rows.append(judge_child(row,manifest,manifest_path,command))
    paths=[r.get('report') for r in rows if r.get('report')]
    if len(set(paths))!=len(paths):raise ValueError('DUPLICATE_CHILD_DIRECTORY')
    final_evidence_check(manifest_path,manifest_sha,launch_hashes,rows)
    h.revalidate_sources(ROOT,manifest['inputs']);result=dict(**aggregate(rows),scope=ROLE,g2='NOT_RUN',g3='NOT_RUN',suite_id=manifest['suite_id'],original_started_at=manifest['created_at'],judged_at=now(),results=rows)
    output=path/('judge-'+uuid.uuid4().hex+'.json');atomic(output,result);print(json.dumps(dict(status=result['status'],report=str(output),scope=ROLE)));return 0 if result['status']=='PASS' else 1 if result['status']=='FAIL' else 2
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);sub=p.add_subparsers(dest='mode',required=True);r=sub.add_parser('run');r.add_argument('--profile',required=True);r.add_argument('--seconds',type=int,default=300);r.add_argument('--jobs',type=int,choices=(1,2),default=2);j=sub.add_parser('judge');j.add_argument('suite',type=Path);args=p.parse_args()
    if args.mode=='run' and not 1<=args.seconds<=3600:p.error('seconds outside1..3600')
    raise SystemExit(run(args.profile,args.seconds,args.jobs) if args.mode=='run' else judge(args.suite))

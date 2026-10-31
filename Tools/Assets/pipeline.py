"""Small, local asset jobs. No generation/paid submission endpoint exists here."""
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
import shutil
import subprocess
import sys
import uuid
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from project_paths import canonical_evidence_path

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'output/asset-pipeline'
TOOLS = Path(__file__).resolve().parent
BLENDER = '/Applications/Blender.app/Contents/MacOS/Blender'


class Blocked(ValueError):
    pass


class AssetFailure(ValueError):
    pass


def now():
    return datetime.now().astimezone().isoformat()


def digest(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024*1024), b''):
            value.update(chunk)
    return value.hexdigest()


def load(path):
    def pairs(items):
        d = {}
        for k, v in items:
            if k in d:
                raise ValueError('Duplicate JSON key: ' + k)
            d[k] = v
        return d
    return json.loads(Path(path).read_text(), object_pairs_hook=pairs,
                      parse_constant=lambda v: (_ for _ in ()).throw(ValueError('Nonfinite JSON')))


def child(root, rel):
    root = Path(root).resolve()
    p = Path(rel)
    if p.is_absolute() or not p.parts or any(x in ('..', '.') for x in p.parts):
        raise ValueError('Unsafe relative path')
    candidate = root / p
    if not candidate.resolve().is_relative_to(root):
        raise ValueError('Path escapes root')
    for part in (candidate, *candidate.parents):
        if part == root:
            break
        if part.is_symlink():
            raise ValueError('Symlink not allowed')
    return candidate


def atomic(path, value):
    path = Path(path)
    temp = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    with temp.open('x') as f:
        json.dump(value, f, ensure_ascii=False, indent=2, allow_nan=False)
        f.write('\n'); f.flush(); os.fsync(f.fileno())
    os.replace(temp, path)
    fd = os.open(path.parent, os.O_RDONLY)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


@contextmanager
def lock(folder):
    p = child(folder, '.lock')
    with p.open('a') as f:
        try:
            fcntl.flock(f, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as e:
            raise Blocked('JOB_LOCKED') from e
        try:
            yield
        finally:
            fcntl.flock(f, fcntl.LOCK_UN)


def tools_hashes():
    result = {name: digest(TOOLS / name) for name in
            ('pipeline.py', 'process_blender.py', 'UnityAsset.cs', 'unity_asset.py')}
    result['Tools/project_paths.py'] = digest(TOOLS.parent/'project_paths.py')
    return result


def process_dependencies():
    binary = Path(BLENDER).resolve()
    if not binary.is_file():
        raise Blocked('BLENDER_UNAVAILABLE')
    return {'tools': {name: digest(TOOLS/name) for name in ('pipeline.py','process_blender.py')},
            'blender': {'path':str(binary),'sha256':digest(binary)}}


def validate_process_dependencies(value):
    if not isinstance(value,dict) or set(value)!={'tools','blender'}:
        raise Blocked('PROCESS_DEPENDENCIES_MISSING')
    code, runtime = value['tools'], value['blender']
    if (not isinstance(code,dict) or set(code)!={'pipeline.py','process_blender.py'}
        or not isinstance(runtime,dict) or set(runtime)!={'path','sha256'}):
        raise Blocked('PROCESS_DEPENDENCIES_INVALID')
    if not isinstance(runtime['path'],str) or not Path(runtime['path']).is_absolute():
        raise Blocked('PROCESS_RUNTIME_PATH_INVALID')
    if any(not isinstance(h,str) or not re.fullmatch('[0-9a-f]{64}',h)
           for h in [*code.values(),runtime['sha256']]):
        raise Blocked('PROCESS_DEPENDENCY_HASH_INVALID')


def validate_spec(s):
    expected = {'schema', 'asset_id', 'revision', 'scope', 'provider', 'task_id',
                'account_profile', 'source_files', 'model', 'basecolor', 'normal',
                'limits', 'purpose'}
    if set(s) != expected or s['schema'] != 1 or s['scope'] != 'TRANSFER_TEST':
        raise ValueError('Unsupported or incomplete contract')
    for name in ('asset_id', 'revision', 'account_profile'):
        if not isinstance(s[name], str) or not re.fullmatch(r'[a-zA-Z0-9_-]{1,80}', s[name]):
            raise ValueError('Invalid identifier: ' + name)
    uuid.UUID(s['task_id'])
    if s['provider'] not in ('tripo-studio', 'tripo-api'):
        raise ValueError('Unsupported provider')
    if not isinstance(s['source_files'], dict) or not s['source_files']:
        raise ValueError('Empty sources')
    destinations = set()
    for rel, item in s['source_files'].items():
        child(ROOT, rel)
        if set(item) != {'sha256', 'destination'} or not re.fullmatch('[0-9a-f]{64}', item['sha256']):
            raise ValueError('Invalid source digest')
        dest = child(ROOT, item['destination'])
        if str(dest) in destinations:
            raise ValueError('Duplicate destination')
        destinations.add(str(dest))
    for field in ('model', 'basecolor', 'normal'):
        if s[field] not in s['source_files']:
            raise ValueError('Missing source role: ' + field)
    if not s['model'].lower().endswith('.fbx'):
        raise ValueError('Minimum pipeline supports FBX only')
    keys = {'triangles', 'materials', 'meshes', 'texture_size', 'horizontal_size', 'tolerance'}
    if set(s['limits']) != keys:
        raise ValueError('Missing or unknown limits')
    for k, v in s['limits'].items():
        if type(v) not in (int, float) or not math.isfinite(v) or v <= 0:
            raise ValueError('Invalid limit: ' + k)
    if any(type(s['limits'][k]) is not int for k in ('triangles', 'materials', 'meshes', 'texture_size')):
        raise ValueError('Count limits must be integers')
    if s['limits']['tolerance'] >= s['limits']['horizontal_size']:
        raise ValueError('Tolerance must be smaller than target size')
    return s


def check_sources(s):
    for rel, item in s['source_files'].items():
        p = child(ROOT, rel)
        if not p.is_file() or digest(p) != item['sha256']:
            raise Blocked('SOURCE_MISMATCH: ' + rel)


def job_dir(path):
    child(ROOT, 'output/asset-pipeline')
    p = canonical_evidence_path(path)
    if p.is_absolute():
        try:
            p = p.relative_to(BASE)
        except ValueError as e:
            raise ValueError('Job outside asset output') from e
    return child(BASE, p)


def event(j, phase, **details):
    j['phase'] = phase
    j['events'].append({'seq': len(j['events']) + 1, 'time': now(), 'phase': phase, **details})


def read_job(folder):
    """Validate immutable evidence, without claiming it matches current tools."""
    j = load(child(folder, 'job.json'))
    s = validate_spec(load(child(folder, 'spec.json')))
    if type(j['schema']) is not int or j['schema'] not in (1,2) or j['spec_sha256'] != digest(folder / 'spec.json'):
        raise Blocked('CONTRACT_CHANGED')
    if j['schema']==2:
        validate_process_dependencies(j.get('process_dependencies'))
    if j['task_id'] != s['task_id'] or j['provider'] != s['provider']:
        raise Blocked('TASK_IDENTITY_CHANGED')
    if j['phase'] not in ('CREATED', 'SOURCE_READY', 'PROCESSED'):
        raise Blocked('UNKNOWN_PHASE')
    if not j['events'] or j['events'][-1]['phase'] != j['phase'] or [e['seq'] for e in j['events']] != list(range(1, len(j['events']) + 1)):
        raise Blocked('JOURNAL_INCOMPLETE')
    raw = {'raw/' + item['destination']: item['sha256'] for item in s['source_files'].values()}
    if j['phase'] == 'CREATED' and j['artifacts']:
        raise Blocked('UNCOMMITTED_ARTIFACTS')
    if j['phase'] in ('SOURCE_READY', 'PROCESSED'):
        if any(j['artifacts'].get(k) != v for k,v in raw.items()):
            raise Blocked('RAW_MANIFEST_INCOMPLETE')
    if j['phase'] == 'PROCESSED':
        processed = j.get('processed', '')
        if not re.fullmatch(r'process-[0-9a-f]{32}', processed):
            raise Blocked('PROCESS_RECEIPT_MISSING')
        required = {'model.fbx','basecolor.png','normal.png','before.png','after.png','receipt.json','contract-result.json','blender.log'}
        if set(j['artifacts']) != set(raw) | {processed+'/'+name for name in required}:
            raise Blocked('PROCESS_MANIFEST_INCOMPLETE')
    elif j['phase'] == 'SOURCE_READY' and set(j['artifacts']) != set(raw):
        raise Blocked('RAW_MANIFEST_UNEXPECTED')
    check_sources(s)
    for rel, value in j['artifacts'].items():
        p = child(folder, rel)
        if not p.is_file() or digest(p) != value:
            raise Blocked('ARTIFACT_MISMATCH: ' + rel)
    if j['phase'] == 'PROCESSED':
        receipt = load(child(folder, j['processed']+'/receipt.json'))
        verdict = judge(receipt['measurement'], s['limits'], unity=False)
        if verdict['status'] != 'PASS' or load(child(folder,j['processed']+'/contract-result.json')) != verdict:
            raise AssetFailure('CACHED_CONTRACT_FAILED')
    return j, s


def verify_job(folder):
    j, s = read_job(folder)
    if j['schema']==1 and j['tools']!=tools_hashes():
        raise Blocked('TOOLS_CHANGED: legacy job retained; init --reuse-from can reuse raw only')
    if j['schema']==2 and j['process_dependencies']!=process_dependencies():
        raise Blocked('PROCESS_DEPENDENCY_CHANGED: start a new job with --reuse-from')
    return j, s


def reuse_into(folder, j, s, source):
    old = job_dir(source)
    with lock(old):
        original, old_spec = read_job(old)
        original_hash = digest(old/'job.json')
        fields = ('provider','task_id','account_profile','source_files','model','basecolor','normal')
        if original['phase']=='CREATED' or any(s[k]!=old_spec[k] for k in fields):
            raise Blocked('REUSE_SOURCE_MISMATCH_OR_INCOMPLETE')
        processed = (original['schema']==2 and original['phase']=='PROCESSED'
                     and s==old_spec and original['process_dependencies']==j['process_dependencies'])
        copied = {}
        for rel, expected in original['artifacts'].items():
            if not processed and not rel.startswith('raw/'):
                continue
            target=child(folder,rel);target.parent.mkdir(parents=True,exist_ok=True)
            with child(old,rel).open('rb') as src, target.open('xb') as dst:
                shutil.copyfileobj(src,dst)
            if digest(target)!=expected:
                raise Blocked('REUSE_COPY_MISMATCH')
            copied[rel]=expected
        read_job(old)
        if digest(old/'job.json')!=original_hash or process_dependencies()!=j['process_dependencies']:
            raise Blocked('REUSE_SOURCE_OR_TOOL_CHANGED')
        j['artifacts']=copied
        if processed: j['processed']=original['processed']
        j['reuse']={'source_job':str(old.relative_to(BASE)), 'source_job_sha256':original_hash,
                    'scope':'RAW_AND_PROCESSED' if processed else 'RAW_ONLY'}
        event(j,'PROCESSED' if processed else 'SOURCE_READY',action='reuse',**j['reuse'])
        atomic(folder/'job.json',j)
        verify_job(folder)


def init(spec, reuse_from=None):
    s = validate_spec(load(spec)); check_sources(s)
    child(ROOT, 'output/asset-pipeline')
    BASE.mkdir(parents=True, exist_ok=True)
    folder = child(BASE, s['asset_id'] + '/' + uuid.uuid4().hex)
    folder.mkdir(parents=True)
    atomic(folder / 'spec.json', s)
    j = {'schema': 2, 'run_id': folder.name, 'phase': 'CREATED', 'created_at': now(),
         'provider': s['provider'], 'task_id': s['task_id'], 'source_mode': 'existing',
         'generation_requests': 0, 'spec_sha256': digest(folder / 'spec.json'),
         'tools': tools_hashes(), 'process_dependencies':process_dependencies(), 'events': [], 'artifacts': {}}
    event(j, 'CREATED', reason='Recorded existing completed asset; no remote call')
    atomic(folder / 'job.json', j)
    if reuse_from is not None:
        with lock(folder):
            try:
                reuse_into(folder,j,s,reuse_from)
            except (ValueError,OSError) as error:
                # Keep the failed new attempt and the unchanged old job for inspection.
                atomic(folder/'reuse-failure.json',{'status':'BLOCKED','error':str(error),'time':now()})
                raise Blocked('REUSE_BLOCKED: '+str(error)+'; evidence '+str(folder)) from error
    return folder


def resume(path, stop_after_source=False):
    folder = job_dir(path)
    with lock(folder):
        j, s = verify_job(folder)
        if j['phase'] == 'CREATED':
            raw = child(folder, 'raw'); raw.mkdir(exist_ok=True)
            for rel, item in s['source_files'].items():
                dst = child(raw, item['destination']); dst.parent.mkdir(parents=True, exist_ok=True)
                if dst.exists():
                    if digest(dst) != item['sha256']:
                        raise Blocked('PARTIAL_COPY_CONFLICT')
                else:
                    with child(ROOT, rel).open('rb') as src, dst.open('xb') as target:
                        shutil.copyfileobj(src, target)
                if digest(dst) != item['sha256']:
                    raise Blocked('COPY_MISMATCH')
                j['artifacts'][str(dst.relative_to(folder))] = item['sha256']
            event(j, 'SOURCE_READY'); atomic(folder / 'job.json', j)
        if stop_after_source or j['phase'] == 'PROCESSED':
            return j
        attempt = child(folder, 'process-' + uuid.uuid4().hex)
        attempt.mkdir()
        event(j, 'SOURCE_READY', attempt=attempt.name, action='process-start')
        atomic(folder / 'job.json', j)
        cmd = [BLENDER, '--background', '--factory-startup', '--disable-autoexec',
               '--python-exit-code', '1', '--python', str(TOOLS / 'process_blender.py'),
               '--', str(folder / 'spec.json'), str(folder / 'raw'), str(attempt)]
        with (attempt / 'blender.log').open('w') as log:
            p = subprocess.run(cmd, stdout=log, stderr=subprocess.STDOUT, timeout=240)
        if p.returncode:
            event(j, 'SOURCE_READY', action='process-blocked', attempt=attempt.name, reason='BLENDER_FAILED')
            atomic(folder / 'job.json', j)
            raise Blocked('BLENDER_FAILED; attempt log retained')
        receipt = load(attempt / 'receipt.json')
        verdict = judge(receipt['measurement'], s['limits'], unity=False)
        atomic(attempt / 'contract-result.json', verdict)
        if verdict['status'] != 'PASS':
            event(j, 'SOURCE_READY', action='process-failed', attempt=attempt.name, result_sha256=digest(attempt/'contract-result.json'))
            atomic(folder / 'job.json', j)
            raise AssetFailure('PROCESS_CONTRACT_FAILED: ' + ','.join(verdict['errors']))
        verify_job(folder)
        for p in attempt.iterdir():
            if p.is_file():
                j['artifacts'][str(p.relative_to(folder))] = digest(p)
        j['processed'] = attempt.name
        event(j, 'PROCESSED'); atomic(folder / 'job.json', j)
        return j


def judge(m, limits, unity=True):
    errors = []
    try:
        tris = m['triangles']
        if type(tris) is not int or not 0 < tris <= limits['triangles']:
            errors.append('TRIANGLE_BUDGET')
        for name in ('meshes', 'materials'):
            if type(m[name]) is not int or m[name] != limits[name]:
                errors.append(name.upper() + '_COUNT')
        size, center = m['size'], m['center']
        if len(size) != 3 or len(center) != 3:
            raise ValueError('bounds vector')
        numbers = [*size, *center, m['bottom']]
        if any(type(v) not in (int, float) or not math.isfinite(v) for v in numbers):
            raise ValueError('nonfinite bounds')
        if min(size) <= 0:
            errors.append('DEGENERATE_BOUNDS')
        if abs(max(size[0], size[2]) - limits['horizontal_size']) > limits['tolerance']:
            errors.append('TARGET_SIZE')
        if max(abs(m['bottom']), abs(center[0]), abs(center[2])) > limits['tolerance']:
            errors.append('GROUND_PIVOT')
        textures = m['textures']
        if set(textures) != {'basecolor', 'normal'}:
            errors.append('TEXTURE_SLOTS')
        for value in textures.values():
            if len(value) != 2 or any(type(v) is not int or not 0 < v <= limits['texture_size'] for v in value):
                errors.append('TEXTURE_SIZE')
        if unity and (m['shader'] != 'Universal Render Pipeline/Lit' or m['shader_supported'] is not True or m['forbidden_components'] != []):
            errors.append('UNITY_COMPONENT_OR_SHADER')
    except (KeyError, ValueError, TypeError):
        errors.append('INVALID_MEASUREMENT')
    return {'status': 'FAIL' if errors else 'PASS', 'errors': errors, 'limits': limits,
            'scope': 'transfer-test contract only; not game acceptance'}


def query_task(path, runner=subprocess.run):
    folder = job_dir(path)
    with lock(folder):
        j, s = verify_job(folder)
        if s['provider'] != 'tripo-api':
            raise Blocked('STUDIO_TASK_IS_NOT_API_TASK')
        cli = ROOT / 'output/tools/tripo/node_modules/.bin/tripo'
        cmd = [str(cli), 'task', 'get', s['task_id'], '--profile', s['account_profile'], '--json', '--quiet', '--no-open']
        env = os.environ.copy()
        env.update(TRIPO_HOME=str(ROOT / 'output/tripo-state'), TRIPO_ANALYTICS_ENABLED='0', TRIPO_NO_UPDATE_CHECK='1')
        try:
            p = runner(cmd, env=env, capture_output=True, text=True, timeout=30, cwd=ROOT)
            if p.returncode:
                raise Blocked('TASK_QUERY_FAILED')
            result = json.loads(p.stdout)
            if result.get('task_id') != s['task_id']:
                raise Blocked('TASK_QUERY_ID_MISMATCH')
            status = result.get('status')
            if status not in ('queued', 'running', 'success', 'failed', 'cancelled', 'unknown', 'banned', 'expired'):
                raise Blocked('TASK_QUERY_STATUS_UNKNOWN')
        except (subprocess.TimeoutExpired, ValueError):
            event(j, j['phase'], action='query-blocked'); atomic(folder / 'job.json', j)
            raise Blocked('TASK_QUERY_BLOCKED; retained task ID; no submission') from None
        event(j, j['phase'], action='query', remote_status=status)
        atomic(folder / 'job.json', j)
        return {'task_id': s['task_id'], 'remote_status': status, 'generation_requests': 0}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    subs = parser.add_subparsers(dest='command', required=True)
    initialize=subs.add_parser('init')
    initialize.add_argument('--spec', required=True)
    initialize.add_argument('--reuse-from', help='Verify and copy eligible stages into a new job')
    for name in ('resume', 'status', 'query-task'):
        p = subs.add_parser(name); p.add_argument('--job', required=True)
        if name == 'resume':
            p.add_argument('--stop-after-source', action='store_true')
    args = parser.parse_args()
    try:
        if args.command == 'init':
            folder=init(args.spec,args.reuse_from);j=load(folder/'job.json')
            result = {'job':str(folder),'phase':j['phase'],'reuse':j.get('reuse',{}).get('scope','NONE')}
        elif args.command == 'resume':
            j = resume(args.job, args.stop_after_source)
            result = {'phase': j['phase'], 'run_id': j['run_id'], 'generation_requests': 0,
                      'report':str(job_dir(args.job)/'job.json')}
        elif args.command == 'query-task':
            result = query_task(args.job)
        else:
            folder = job_dir(args.job)
            with lock(folder):
                j, _ = verify_job(folder)
            result = {'phase': j['phase'], 'run_id': j['run_id'], 'task_id': j['task_id'], 'events': len(j['events'])}
        print(json.dumps({'status': 'PASS', **result}, ensure_ascii=False)); return 0
    except (ValueError, OSError, subprocess.TimeoutExpired) as e:
        status = 'BLOCKED' if isinstance(e, (Blocked, subprocess.TimeoutExpired)) else 'FAIL'
        print(json.dumps({'status': status, 'error': str(e)}, ensure_ascii=False)); return 2 if status == 'BLOCKED' else 1


if __name__ == '__main__':
    sys.exit(main())

"""Read-only verification of transient InputSettings ownership journals."""
from pathlib import Path
import hashlib
from map_io import strict_loads, read_input_bytes, reject_symlinks


def verified_scopes(root,run_id,nonce,required=True):
    base=reject_symlinks(Path(root)/'output/map-full-026/input-scopes')
    if not base.is_dir():
        if required:raise RuntimeError('MISSING_INPUT_SCOPE_JOURNAL')
        return []
    rows=[]
    for path in sorted(base.glob(run_id+'-'+nonce+'-*.json')):
        reject_symlinks(path)
        raw=read_input_bytes(path,1024*1024);data=strict_loads(raw.decode('utf-8'))
        if (data.get('scope')!='OWNED_VIRTUAL_INPUT_CONFIGURATION' or data.get('owner')!=run_id+':'+nonce
                or data.get('phase')!='RESTORED' or data.get('error') or data.get('devicesBefore')!=data.get('devicesAfter')):
            raise RuntimeError('INPUT_SCOPE_NOT_RESTORED:'+path.name)
        for key in ('devicesBefore','devicesAfter'):
            states=data.get(key)
            if type(states) is not list:raise RuntimeError('INPUT_DEVICE_EVIDENCE_MISSING')
            ids=[]
            for state in states:
                if type(state) is not dict or set(state)!={'id','enabled'} or type(state['id']) is not int or state['id']<1 or type(state['enabled']) is not bool:raise RuntimeError('INPUT_DEVICE_EVIDENCE_MALFORMED')
                ids.append(state['id'])
            if len(ids)!=len(set(ids)) or ids!=sorted(ids):raise RuntimeError('INPUT_DEVICE_EVIDENCE_MALFORMED')
        rows.append(dict(path=str(path.relative_to(root)),sha256=hashlib.sha256(raw).hexdigest(),
                         original_id=data['originalId'],clone_id=data['cloneId']))
    if not rows and required:raise RuntimeError('MISSING_INPUT_SCOPE_JOURNAL')
    return rows


def verify_then_release(root,run_id,nonce,release,required=True,expected_count=None):
    """Keep recovery ownership until all scope evidence has been validated."""
    rows=verified_scopes(root,run_id,nonce,required=required)
    if expected_count is not None and len(rows)!=expected_count:raise RuntimeError('INPUT_SCOPE_COUNT')
    result=release()
    return rows,result

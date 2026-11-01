#!/usr/bin/env python3
"""Execute a frozen 028 representative Editor comparison. Never a whole-map gate."""
import argparse,json,subprocess,sys,uuid,re
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo
import map_blockout as b
from map_io import strict_loads
from map_input_scope import verified_scopes
from map_art_audit import no_links,bounded
m=b.motion

def check_inputs(profile):
    for rel in profile['sources']:no_links(m.ROOT/rel)
    if m.source_hashes()!=profile['sources']:raise ValueError('SOURCE_PROFILE_CHANGED')
    for rel,sha in profile['files'].items():
        p=m.ROOT/rel;no_links(p)
        if not p.resolve(strict=True).is_relative_to(m.ROOT) or m.digest(p)!=sha:raise ValueError('FROZEN_FILE_CHANGED:'+rel)

def canonical_initial(value):
    value=dict(value);value['gameplay_snapshot']=strict_loads(value['gameplay_snapshot']);return value

REQUIRED={'initial-state.json','fixed-start-draft.json','fixed-start-draft.png','frame-progress.json','units.json','stop-progress.json','representative-combat.json','game.json','game.png','owned-captures.json'}
def evidence(folder,artifacts):
    if not REQUIRED.issubset(artifacts):raise ValueError('EVIDENCE_MANIFEST_OMISSION')
    raw={}
    for rel,sha in artifacts.items():
        artifact=folder/rel;no_links(artifact)
        if not artifact.resolve(strict=True).is_relative_to(folder):raise ValueError('ARTIFACT_PATH')
        data=bounded(artifact)
        if __import__('hashlib').sha256(data).hexdigest()!=sha:raise ValueError('RUN_ARTIFACT_CHANGED')
        if rel in REQUIRED:raw[rel]=data
    return raw
def four_party(a,z):
    if len(a.get('party',[]))!=4 or len(z.get('party',[]))!=4:raise ValueError('FOUR_PARTY_OBSERVATIONS_REQUIRED')
def run(path):
    no_links(path);path=path.resolve(strict=True)
    if not path.is_relative_to(m.ROOT):raise ValueError("PROFILE_OUTSIDE_PROJECT")
    raw=bounded(path);profile=strict_loads(raw.decode())
    revision=profile.get('revision')
    if type(revision)!=str or not re.fullmatch(r'r[0-9]{3}',revision) or revision=='r000' or path.name!='map-art-028-profile.'+revision+'.json':raise ValueError('PROFILE_REVISION')
    if profile['role']!='REPRESENTATIVE_EDITOR_COMPARISON_028' or profile['stages']!=[28,29]:raise ValueError('PROFILE_IDENTITY')
    check_inputs(profile)
    base=m.ROOT/'output/map-art-028';no_links(base);out=base/('accept-'+uuid.uuid4().hex);out.mkdir()
    started=datetime.now(ZoneInfo('Asia/Seoul'));result=dict(status='BLOCKED',scope=profile['role'],started_at=started.isoformat(),profile_sha256=__import__('hashlib').sha256(raw).hexdigest(),g2='NOT_RUN',g3='NOT_RUN',g4='NOT_RUN',g5='NOT_RUN',performance_build='USER_RUN',cleanup='NOT_RUN',runs=[])
    m.write(out/'summary.json',result)
    try:
        data=strict_loads((m.ROOT/'Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json').read_text())
        for stage in profile['stages']:
            check_inputs(profile)
            process=subprocess.run([sys.executable,str(m.ROOT/'Tools/Verify/map_playable.py'),str(stage)],cwd=m.ROOT,capture_output=True,text=True)
            m.write(out/f'stage-{stage}-command.json',dict(exit_code=process.returncode,stdout=process.stdout,stderr=process.stderr))
            if process.returncode:
                result['cleanup']='BLOCKED' if (m.BASE/'unity-pending.json').exists() else 'CHILD_FAILED_REVIEW_REQUIRED'
                raise ValueError('NATIVE_STAGE_FAILED:'+str(stage))
            receipt=strict_loads(process.stdout.strip());report_path=Path(receipt['report']);no_links(report_path)
            if not report_path.resolve(strict=True).is_relative_to(m.ROOT/'output/map-blockout-018'):raise ValueError('REPORT_PATH')
            report_bytes=bounded(report_path);report=strict_loads(report_bytes.decode());folder=report_path.parent
            if report['status']!='PASS' or report['cleanup']!='PASS' or report['stage']!=stage or datetime.fromisoformat(report['time'])<started:raise ValueError('NATIVE_RECEIPT')
            if report['sources']!=profile['sources']:raise ValueError('NATIVE_SOURCE_PROFILE')
            checked=evidence(folder,report['artifacts'])
            retained=out/f'stage-{stage}';retained.mkdir()
            for name,content in checked.items():
                with (retained/name).open('xb') as stream:stream.write(content)
            with (retained/'native-summary.json').open('xb') as stream:stream.write(report_bytes)
            def blob(name):return strict_loads(checked[name].decode())
            captures=blob('owned-captures.json')
            if not captures or any(c['phase']!='CONFIRMED' or c['run_id']!=captures[0]['run_id'] or c['nonce']!=captures[0]['nonce'] for c in captures):raise ValueError('CAPTURE_OWNERSHIP')
            uid,nonce=captures[0]['run_id'],captures[0]['nonce']
            if folder.name!=f'playable-{stage}-{uid}':raise ValueError('RUN_IDENTITY')
            scopes=verified_scopes(m.ROOT,uid,nonce)
            if len(scopes)!=1 or scopes!=report['input_scopes']:raise ValueError('INPUT_SCOPE_REVALIDATION')
            initial=canonical_initial(blob('initial-state.json'))
            if initial!=profile['initial']:raise ValueError('INITIAL_GAMEPLAY_MISMATCH:'+str(stage))
            draft=blob('fixed-start-draft.json')
            if draft['camera']!=profile['camera'] or draft['elapsed']!=0 or draft['enemies'] or draft['shots']:raise ValueError('FIXED_CAPTURE_CONDITIONS')
            frame=blob('frame-progress.json');a,z=frame['before'],frame['after']
            if not(z['frame']>a['frame'] and z['elapsed']>a['elapsed'] and z['updateInputD'] and z['updateInput']==dict(x=1.0,y=0.0) and z['center']['x']>a['center']['x']):raise ValueError('NATIVE_INPUT_PROGRESS')
            four_party(a,z)
            sweeps=[]
            for label,p,q in [('center',a['center'],z['center']),*[(f'party-{i}',p,q) for i,(p,q) in enumerate(zip(a['party'],z['party']))]]:
                proof=b.assess(data,b.xy(p),b.xy(q),4)
                if proof['status']!='PASS':raise ValueError('OBSERVED_SEGMENT_UNSAFE:'+label)
                sweeps.append(dict(label=label,proof=proof))
            units=blob('units.json')
            required=profile['unit_labels'][str(stage)]
            if units['status']!='PASS' or set(units['checks'])!=set(required):raise ValueError('UNIT_SET_MISMATCH')
            m.write(out/f'stage-{stage}-observed-segment-oracle.json',dict(scope='OBSERVED_ENDPOINT_SEGMENTS_ONLY',status='PASS',sweeps=sweeps))
            result['runs'].append(dict(stage=stage,report=str(report_path.relative_to(m.ROOT)),report_sha256=__import__('hashlib').sha256(report_bytes).hexdigest(),retained_artifacts={name:__import__('hashlib').sha256(content).hexdigest() for name,content in checked.items()},fixed_capture=str((retained/'fixed-start-draft.png').relative_to(m.ROOT)),combat_capture=str((retained/'game.png').relative_to(m.ROOT))))
            m.write(out/'summary.json',result)
        check_inputs(profile)
        if bounded(path)!=raw:raise ValueError('PROFILE_CHANGED_DURING_RUN')
        if (m.BASE/'unity-pending.json').exists():raise ValueError('UNRESTORED_OWNED_SESSION')
        result['cleanup']='PASS';result['status']='PASS_REPRESENTATIVE_EDITOR_ONLY'
    except Exception as error:
        result.update(status='FAIL',error=str(error))
        if (m.BASE/'unity-pending.json').exists():result.update(status='BLOCKED',cleanup='BLOCKED',pending=str((m.BASE/'unity-pending.json').relative_to(m.ROOT)))
    result['finished_at']=datetime.now(ZoneInfo('Asia/Seoul')).isoformat();m.write(out/'summary.json',result)
    print(json.dumps(dict(status=result['status'],report=str(out/'summary.json'),error=result.get('error'))));return 0 if result['status']=='PASS_REPRESENTATIVE_EDITOR_ONLY' else 1
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('profile',type=Path);args=parser.parse_args();raise SystemExit(run(args.profile))

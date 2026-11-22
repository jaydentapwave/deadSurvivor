"""DQ r2 capture adapter. Missing r2 measurements stay BLOCKED, never synthesized.
Invoked by hd2d_main_gate.py dq while the real Main battle is active.
"""
import datetime
import hashlib
import json
import subprocess
import time
from pathlib import Path


def run(root, out, kit):
    import gate
    out.mkdir(parents=True, exist_ok=True)
    cap = out / 'frames'
    if (out / 'manifest.json').exists():
        raise ValueError('Use a fresh output directory; capture declarations are immutable')

    def dump(p, value):
        p.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')

    def evaluate(code):
        result = subprocess.run(['unity', '--json', 'command', 'script', 'eval',
            '--project-path', str(root), '--code', code], capture_output=True, text=True, timeout=120)
        envelope = json.loads(result.stdout)
        if result.returncode or not envelope.get('success'):
            raise RuntimeError(envelope)
        answer = envelope['data']['result']
        if not answer.get('success', True):
            raise RuntimeError(answer)
        return answer.get('result')

    config = gate.read(kit.parent / 'config/gate.default.json')
    config['deferred']['G19'] = 'User 2026-11-18 / stage9: performance is a separate final stage; no sampling in this run.'
    config['checks']['G08']['thresholds']['source_height'] = 56
    config['checks']['G08']['source'] = 'Art spec K50: 56 pixels, PPU48. Perspective depth variation remains allowed by K33; whole-image integer-scale test is not r2 acceptance.'
    config['checks']['G11']['thresholds']['background_max'] = .07
    config['checks']['G11']['source'] = 'Art spec K34: background movement error <= .07. No measurement fixture supplied here.'
    dump(out / 'config.json', config)
    m = dict(schema='25d-gate-manifest/1', run_id='DQ-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ'),
        required_plates=['DQMain'], files={}, sources=[], capture_record='record', plates=[],
        required_settings=dict(quality='Mobile', renderScale=1., srpBatcher=True))

    def add(key, path, size=None):
        m['files'][key] = dict(path=str(path.resolve()))
        if size:
            m['files'][key]['size'] = size
        return key

    main = add('main', cap / 'start-1920x1080.png', [1920,1080])
    moves = [add('move'+str(i), cap / ('motion-'+str(i)+'-1920x1080.png'), [1920,1080]) for i in range(8)]
    aspects = [add('wide', cap/'start-2340x1080.png',[2340,1080]), add('four-three',cap/'start-1440x1080.png',[1440,1080])]
    for label in ['path','water','bridge','camp','edge']:
        for width in [1920,2340,1440]:
            add(label+'-'+str(width),cap/(label+'-'+str(width)+'x1080.png'),[width,1080])
    add('overview',cap/'overview-1920x1920.png',[1920,1920])
    add('tour',out/'tour.json')
    for key, value in list(m['files'].items()):
        if value['path'].endswith('.png'):
            add(key+'-before',Path(value['path'][:-4]+'-before.json'))
    add('settings',out/'settings.json'); add('camera',out/'camera.json'); add('projection',out/'projection.json'); add('record',out/'capture-record.json')
    # All game inputs, plus this adapter and approved criteria. Exclude unrelated untracked files.
    tracked = subprocess.check_output(['git','ls-files','-z','Assets/Game'], cwd=root).decode().split('\0')
    owned = ['Assets/Game/03.Resources/Art/HD2D/DQAppliedFx',
        'Assets/Game/03.Resources/Resources/Art/DQ/AttackFx.asset',
        'Assets/Game/03.Resources/Resources/Art/DQ/BoarTelegraph.asset',
        'Assets/Game/02.Scripts/Features/Combat/Data/DQTelegraphTiles.cs',
        'Assets/Game/02.Scripts/Effect/Motion/Data/DQAttackFxLibrary.cs',
        'Assets/Game/02.Scripts/Editor/MainSceneDQFinishMigration.cs',
        'Assets/Game/02.Scripts/Editor/DQFinishEffectsVerification.cs']
    sources = {root / p for p in tracked if p and (root/p).is_file()}
    for item in owned:
        p = root/item
        sources.update(x for x in p.rglob('*') if x.is_file()) if p.is_dir() else sources.add(p)
        if Path(str(p)+'.meta').is_file(): sources.add(Path(str(p)+'.meta'))
    sources.update([root/'Tools/hd2d_main_gate.py',root/'Tools/dq_main_gate.py',root/'docs/art/아트-기준서.md'])
    for i, p in enumerate(sorted(sources)):
        m['sources'].append(add('source'+str(i), p))
    # Uninstrumented fields intentionally absent: generic gate catches Missing => BLOCKED.
    # G09 toon steps / G18 long directional shadow are obsolete r2 contracts; do not invent passing inputs.
    m['plates'] = [dict(id='DQMain',role='candidate',main=main,motion=moves,aspects=aspects,
        capture_settings='settings',performance=['settings'],projection_observation='camera',
        expected_camera=dict(mode='persp',angle=18,fov=20,height_px=112),regions=dict(props=[],monsters=[],ground=[]),
        lighting_region=[0,0,1920,1080])]
    plan_hash = gate.plan_hash(m)
    dump(out/'declaration.json',dict(run_id=m['run_id'],phase='pre_capture',manifest_plan_sha256=plan_hash))
    m['declaration'] = dict(path='declaration.json',sha256=gate.sha(out/'declaration.json'))
    dump(out/'manifest.json',m)
    before = {k:gate.sha(m['files'][k]['path']) for k in m['sources']}
    dump(out/'source-before.json',before)
    # All outputs and expected projection declared above before any image is rendered.
    settings = evaluate('var c=UnityEngine.Camera.main; var p=UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset; return new {quality=UnityEngine.QualitySettings.names[UnityEngine.QualitySettings.GetQualityLevel()],renderScale=p.renderScale,srpBatcher=UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching,mode=c.orthographic?"ortho":"persp",angle=c.transform.eulerAngles.x,fov=c.fieldOfView,distance=c.GetComponent<TiltedCameraRig>().Distance,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,cameraId=c.GetInstanceID()};')
    dump(out/'settings.json',settings)
    # Distance/mask require the selected actual sprite plane, not a guessed camera-ground distance.
    # Preserve actual mode/angle/fov separately; G06 remains BLOCKED without its mask.
    camera = dict(mode=settings['mode'],angle=settings['angle'],fov=settings['fov'],distance=settings['distance'],res=[1920,1080],char_px=56,ppu=48)
    dump(out/'camera.json',camera)
    import view
    solved = view.solve(**camera)
    focal_height = solved['person_screen_share']*1080
    projection_ok = (settings['scene']=='Assets/Game/01.Scene/Main.unity' and settings['mode']=='persp'
        and abs(settings['angle']-18)<.001 and abs(settings['fov']-20)<.001
        and abs(focal_height-112)<=config['checks']['G06']['thresholds']['height_tolerance_px'])
    dump(out/'projection.json',dict(status='PASS' if projection_ok else 'FAIL',observed=settings,
        view_solver=solved,focal_height_px=focal_height,target_focal_height_px=112,
        scope='Observed Main rig projection / independent view.py math at focus plane. Not actual member-mask height, which varies with depth; generic G06 remains BLOCKED.'))
    code = ('DQBackgroundVerification.Output='+json.dumps(str(cap))+'; DQLightingVerification.Output='+json.dumps(str(out/'receiver'))+'; '
        'UnityEngine.Time.timeScale=1; DQBackgroundVerification.CaptureAspectSet("start"); DQBackgroundVerification.CaptureOverview(); DQBackgroundVerification.RunTour(); return "tour started";')
    dump(out/'capture-start.json',dict(result=evaluate(code),utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),plan_hash=plan_hash))
    deadline = time.monotonic()+125
    while not (out/'tour.json').is_file() and time.monotonic()<deadline:
        time.sleep(.5)
    if not (out/'tour.json').is_file():
        dump(out/'capture-error.json',dict(status='BLOCKED',reason='Tour did not finish in 125 seconds'))
    after = {k:gate.sha(m['files'][k]['path']) for k in m['sources']}
    for f in m['files'].values():
        p=Path(f['path'])
        if p.is_file(): f['sha256']=gate.sha(p)
    record=dict(run_id=m['run_id'],manifest_plan_sha256=plan_hash,source_before=before,source_after=after,
        files={k:f['sha256'] for k,f in m['files'].items() if k!='record' and 'sha256' in f})
    dump(out/'capture-record.json',record)
    m['files']['record']['sha256']=gate.sha(out/'capture-record.json')
    dump(out/'manifest.json',m)
    report=gate.evaluate(out/'manifest.json',out/'config.json')
    report['dq_projection_status']='PASS' if projection_ok else 'FAIL'
    if not projection_ok: report['status']='FAIL'
    report['dq_limitations']=['G06: actual selected-sprite mask/distance not instrumented; observed projection is bound but not substituted for the missing height measurement.',
        'G09/G18 generic toon/long-shadow expectations are superseded by r2. No passing substitute measurement supplied.',
        'G11/K55 and G20 require independent instrumentation/review; missing fields are BLOCKED.',
        'G19 deferred, no performance samples collected. G04 only checks actual capture settings.']
    dump(out/'gate-report.json',report)
    (out/'gate-summary.md').write_text(gate.summary(report))
    print(report['status'],out/'gate-report.json')
    return 0 if report['status']=='PASS' else 1

#!/usr/bin/python3
"""3dkit asset pipeline: one path for every 3D asset. Common skeleton + a profile (3dkit/profiles/) for the style steps.

Run from the project root with /usr/bin/python3 (numpy + Pillow). unity/check/capture/migrate need the Unity Editor open
on this project with the 3dkit package (Packages/manifest.json "com.studiokit.kit3d": "file:../3dkit/unity").
  assets.py [--profile NAME] <command> ...     --profile overrides 3dkit.config.json assets.profile for one run
  spec [--markdown]                 standard table of the active profile (+ catalog standards_override)
  profile                           the active profile: stages, shader, texture slots
  list                              catalog assets
  gen <cmd> <tripo asset>           Tripo generation: freeze|dryrun|submit|fetch|status|reconcile <asset>
                                    | anim <asset> <step>|reset-<step>          (kitlib/tripo.py)
  palette <name> [--force]          build a palette that has 'fixed' + 'seed' in the catalog (e.g. char)
  prep <id...>|--all                texture stage of the profile -> <work_root>/<id>/prep/
  unity <id...>|--all               Unity: texture import, material, model import, prefab, Mesh LOD, project hooks
  check                             measure registered assets + build scenes in Unity, judge against the standard
  capture [--tag T]                 game-angle and front line-ups (<work_root>/<T>-lineup_*.png)
  migrate <name>                    one-time AssetDatabase moves from the catalog's migrations (GUIDs kept)
  selftest                          offline tests (no Unity, no network)
A new asset: catalog entry (kind, folder, source or tripo block; a budget with the user's approval text for paid steps)
-> gen -> (Blender if over the triangle cap) -> prep -> unity -> check -> capture -> the user looks at it.
"""
import json
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from kitlib import config as C  # noqa: E402
from kitlib import pipeline as P  # noqa: E402


def main(argv=None):
    a = list(sys.argv[1:] if argv is None else argv)
    profile = None
    if '--profile' in a:
        i = a.index('--profile')
        profile = a[i + 1]
        del a[i:i + 2]
    if not a:
        raise SystemExit(__doc__)
    cmd, rest = a[0], a[1:]
    if cmd == 'selftest':
        return selftest()
    kit = C.Kit(profile=profile)
    if cmd == 'spec': return P.spec(kit, '--markdown' in rest)
    if cmd == 'profile': return P.show_profile(kit)
    if cmd == 'list': return P.list_assets(kit)
    if cmd == 'gen':
        from kitlib import tripo
        tripo.setup(kit)
        sys.argv = ['assets.py gen', *rest]
        return tripo.main()
    if cmd == 'palette': return P.palette(kit, rest[0], '--force' in rest)
    if cmd == 'prep': return P.prep(kit, rest)
    if cmd == 'unity': return P.unity_build(kit, rest)
    if cmd == 'check': sys.exit(P.check(kit))
    if cmd == 'capture': return P.capture(kit, rest[rest.index('--tag') + 1] if '--tag' in rest else 'lineup')
    if cmd == 'migrate': return P.migrate(kit, rest[0])
    raise SystemExit(__doc__)


# ---------------- selftest ----------------
def check_project(kit):
    """the project's catalog against the active profile; every frozen Tripo request must be reproduced exactly"""
    from kitlib import tripo as TR
    cat = kit.cat
    ids = [a['id'] for a in cat['assets']]
    assert len(ids) == len(set(ids)), 'duplicate ids'
    for a in cat['assets']:
        kit.standard(a['kind'])
        if a.get('tripo'):
            assert a['tripo']['budget'] in cat['budgets'], a['id']
        if a['method'] == 'build' and a['status'] == 'adopted':
            assert kit.abs(a['source']['texture']).exists(), ('MISSING_SOURCE', a['id'])
        if a.get('variant_of'):
            assert a['variant_of'] in ids, a['id']
    for name, s in (cat.get('anim_steps') or {}).items():
        assert s['type'] in ('check', 'rig', 'retarget') and isinstance(s.get('est'), int), name
    for b, v in cat['budgets'].items():
        assert all(isinstance(t, str) and t for t in (v.get('step_approvals') or {}).values()), b
    TR.setup(kit)
    checked = 0
    for asset in TR.SPECS:
        f = TR.adir(asset) / 'request-frozen.json'
        if f.exists():
            req = json.loads(f.read_text())
            imgs = TR.concepts(asset)
            img = imgs[0] if len(imgs) == 1 else imgs
            assert TR.args_for(asset, img, False) == req['planned_args'], ('ARGS_DIFFER', asset)
            assert ([TR.sha(x) for x in img] if isinstance(img, list) else TR.sha(img)) == req['concept_sha256'], ('CONCEPT_DIFFERS', asset)
            b = TR.budget(asset)   # the frozen request belongs to the asset's current budget (no silent budget move)
            assert req.get('scope') in (f'BUDGET_{b}', cat['budgets'][b].get('legacy_scope')), ('BUDGET_MOVED', asset, req.get('scope'))
            checked += 1
    spent = {b: TR.spent(b) for b in cat['budgets']}
    assert all(spent[b] <= int(cat['budgets'][b]['cap']) for b in spent), spent
    # judge counterexamples: a built monster over the cap with a real shadow fails; a build scene using a trial folder
    # or unregistered art fails
    monster = next((a for a in P.adopted(cat) if a['method'] == 'build' and a.get('source', {}).get('model')), None)
    if monster and kit.profile_name == 'lowpoly':
        s = kit.standard(monster['kind'])
        trial = cat['project']['trial_roots'][0]
        fake = dict(items=[dict(id=monster['id'], folder_exists=True, model_child=True,
                                prefab=dict(tris=s['tris_max'] + 200, bones=0, materials=1, height=1, feet=0, centre=0, shadows=['On'], materialPaths=[]),
                                material=dict(shader=kit.material_shader(), props={k: v for k, v in kit.profile['check']['material_props'].items()}),
                                textures={'albedo': dict(width=s['texture'], android=f"ASTC_4x4@{s['texture']}", ios=f"ASTC_4x4@{s['texture']}", mip=True, alpha='None')})],
                    scenes=[dict(scene='X.unity', deps=[kit.apath(trial + '/x.mat'), kit.art_root + '/Characters/Foo/x.mat'], materials=[], textures=[])])
        _, fails = P.judge(kit, fake)
        assert any(f"tris {s['tris_max'] + 200}" in f for f in fails) and any('shadow' in f for f in fails), fails
        assert any('trial-folder' in f for f in fails) and any('unregistered art ' + kit.art_root + '/Characters/Foo/x.mat' in f for f in fails), fails
    return f'project catalog OK ({len(ids)} assets, profile {kit.profile_name}, {checked} frozen Tripo requests reproduced, spent {spent})'


def check_profiles():
    """a temp project: switching assets.profile changes the numbers, the texture stage, the shader, the slots and the
    import/LOD settings; --profile overrides the file for one run"""
    from PIL import Image
    import numpy as np
    d = Path(tempfile.mkdtemp(prefix='3dkit-profiles-'))
    (d / 'src').mkdir()
    Image.fromarray(np.full((64, 64, 3), (200, 80, 60), np.uint8)).save(d / 'src/a.png')
    Image.fromarray(np.full((64, 64, 3), (128, 128, 255), np.uint8)).save(d / 'src/n.png')
    cat = dict(schema=C.CATALOG_SCHEMA, standards_override={}, budgets={}, palettes={},
               project=dict(trial_roots=['Tests'], shader_allowed={}, naming={}),
               assets=[dict(id='boss_a', kind='boss', status='adopted', method='build', folder='Art/Boss/a',
                            source=dict(texture='output/src/a.png', model='output/src/a.fbx', maps=dict(normal='output/src/n.png')))])
    (d / 'cat.json').write_text(json.dumps(cat))
    def cfg(profile):
        (d / C.CONFIG_NAME).write_text(json.dumps(dict(schema=C.CONFIG_SCHEMA, assets=dict(
            profile=profile, catalog='cat.json', content_root='Assets/G', art_root='Assets/G/Art', work_root='output/work'))))
        return C.Kit(config_path=d / C.CONFIG_NAME)
    (d / 'output').mkdir(); (d / 'src').rename(d / 'output/src')
    low, mid = cfg('lowpoly'), cfg('midpoly')
    assert low.standard('boss') != mid.standard('boss') and mid.standard('boss')['maps'] == ['albedo', 'normal']
    assert C.Kit(config_path=d / C.CONFIG_NAME, profile='lowpoly').profile_name == 'lowpoly', '--profile override'
    assert P.stage_of(low, cat['assets'][0]) == 'palette' and P.stage_of(mid, cat['assets'][0]) == 'passthrough'
    # midpoly: prep (passthrough, both maps) -> Unity request
    import contextlib, io
    with contextlib.redirect_stdout(io.StringIO()):
        P.prep(mid, ['boss_a'])
    rec = json.loads((d / 'output/work/boss_a/prep/prep.json').read_text())
    size = mid.standard('boss')['texture']
    assert rec['method'] == 'passthrough' and set(rec['maps']) == {'albedo', 'normal'} and rec['maps']['albedo']['size'] == size, rec
    req = P.build_request(mid, cat['assets'])
    it = req['items'][0]
    assert req['material']['shader'] == 'Universal Render Pipeline/Lit' and req['material']['slots'] == {'albedo': '_BaseMap', 'normal': '_BumpMap'}
    assert req['material']['map_keywords'] == {'normal': '_NORMALMAP'} and it['mesh_lod'] is True
    assert it['textures']['normal']['settings']['type'] == 'NormalMap' and it['textures']['normal']['settings']['srgb'] is False
    assert it['textures']['albedo']['settings']['type'] == 'Default' and it['prefab'].endswith('/a.prefab')
    assert req['prefab']['light_probes'] == 'BlendProbes' and it['preset'] == mid.profile['material']['presets']['character']
    # lowpoly would palette-quantize it, with the palette shader role (unmapped in this temp project -> refused)
    try:
        low.material_shader(); raise AssertionError('unmapped role accepted')
    except SystemExit as e:
        assert 'NO_SHADER_FOR_ROLE:palette_lit' in str(e)
    low.data['assets']['shaders'] = {'palette_lit': 'Test/Palette'}
    # judge with the midpoly numbers
    s = mid.standard('boss')
    tex = {m: dict(width=size, android=f'ASTC_4x4@{size}', ios=f'ASTC_4x4@{size}', mip=True, alpha='None') for m in ('albedo', 'normal')}
    meas = lambda tris: dict(items=[dict(id='boss_a', folder_exists=True, model_child=True, textures=tex,
                                         prefab=dict(tris=tris, bones=40, materials=1, height=1, feet=0, centre=0, shadows=['On'], materialPaths=[]),
                                         material=dict(shader='Universal Render Pipeline/Lit', props={}))], scenes=[])
    assert P.judge(mid, meas(s['tris_max']))[1] == [], P.judge(mid, meas(s['tris_max']))[1]
    assert any('tris' in f for f in P.judge(mid, meas(s['tris_max'] + 1))[1])
    assert any('tris' in f for f in P.judge(low, meas(s['tris_max']))[1]), 'lowpoly numbers must reject a midpoly boss'
    return f'profiles OK (lowpoly <-> midpoly by assets.profile; midpoly boss {s["tris_target"]}/{s["tris_max"]} tris, {size}px albedo+normal, URP Lit, Mesh LOD)'


def selftest():
    from kitlib import texture as T
    print(T.selftest())
    r = subprocess.run([sys.executable, str(HERE / 'selftest_tripo.py')], capture_output=True, text=True)
    print(r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr[-400:])
    if r.returncode:
        raise SystemExit('TRIPO_SELFTEST_FAILED')
    print(check_profiles())
    try:
        kit = C.Kit()
    except SystemExit as e:
        print('project catalog: SKIPPED', e)
        return
    print(check_project(kit))
    print('assets selftest PASS')


if __name__ == '__main__':
    main()

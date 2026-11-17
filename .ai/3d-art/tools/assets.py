#!/usr/bin/env python3
"""3dkit asset pipeline: one path for every 3D asset. Common skeleton + a profile (<kit>/profiles/) for the style steps.

Run from the project root with the interpreter in harness.python (numpy + Pillow). unity/check/capture/migrate need the Unity
Editor open on this project with the kit package com.studiokit.kit3d (installed: Packages/com.studiokit.kit3d).
Every refusal reads CODE:detail -> next: <action>.
  assets.py [--profile NAME] <command> ...     --profile overrides 3dkit.config.json assets.profile for one run

 Look up
  spec [--markdown]                 standard table of the active profile (+ catalog standards_override)
  profile                           the active profile: stages, shader, texture slots
  list                              catalog assets
  metrics [--record]                agent results per asset: attempts, credits, adoption (+ append to harness.metrics)
 Catalog
  catalog-set <id|-> <key.path> <json>   change one value under the catalog lock (docs/art/.catalog.lock), format kept,
                                    schema checked. '-' = top level (e.g. budgets.s5.cap)
 Paid generation (Tripo; kitlib/tripo.py)
  gen estimate|freeze|dryrun|submit|fetch [--wait S]|status|reconcile <tripo asset>
  gen anim <tripo asset> <step>|reset-<step>      paid rig/retarget steps (not the measuring tools under `anim`)
 Blender (0 credits)
  convert <in.glb> <out_dir>        Tripo GLB -> model.fbx + basecolor.png + normal.png
  fit <id> [--height H] [--bury B] [--corner] [--tol T] [--strict] [--width W] [--depth D] [--pre-yaw DEG] [--out DIR]
                                    terrain module: Blender exact bounds + cell-boundary weld (std 3.2). Height from catalog asset 'fit' or the flag
  armfix <in.fbx> <out_dir> [--target-degrees 15 | --add-degrees D] [--max-degrees 20] [--gate 0.05] [--no-gate] [--actions walk] [--no-rest-fingers] [--step N]
                                    arms out + finger bones at bind pose + hand-in-body measure (fix.json)
  attach <char.fbx> <out_dir> [--hat F] [--hat-bone Head] [--prop F] [--prop-bone Right_Hand] [--hat-offset x,y,z] [--prop-offset x,y,z] [--hat-sink m]
                                    hat on the head bone, hand prop on a hand bone, separate meshes (K13/K14)
 Textures
  palette <name> [--force]          build a palette that has 'fixed' + 'seed' in the catalog (e.g. char)
  orm <model.glb> <out.png>         midpoly: Tripo GLB metallicRoughness -> smoothness map (A = 1 - roughness) for source.maps.smoothness
  prep <id...>|--all                texture stage of the profile -> <work_root>/<id>/prep/
 Unity (Editor open)
  unity <id...>|--all               texture import, material, model import, prefab, Mesh LOD, project hooks
  check                             measure registered assets + build scenes, judge against the standard (report has run id + fingerprint)
  capture [--tag T]                 game-angle and front line-ups (<work_root>/<T>-lineup_*.png)
  migrate <name>                    one-time AssetDatabase moves from the catalog's migrations (GUIDs kept)
  anim <probe|frames|metrics|sheet|fbxcheck> ...   measuring tools for clips (kitlib/anim.py, rule 3d-animation.md)
 Kit
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
from kitlib.errors import fail, need  # noqa: E402


def main(argv=None):
    a = list(sys.argv[1:] if argv is None else argv)
    profile = None
    if '--profile' in a:
        i = a.index('--profile')
        if i + 1 >= len(a):
            fail('USAGE', '--profile needs a name', 'assets.py --profile midpoly <command>')
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
    if cmd == 'palette': return P.palette(kit, need(rest, 1, 'palette <name> [--force]')[0], '--force' in rest)
    if cmd == 'catalog-set':
        cid, key, val = need(rest, 3, 'catalog-set <id|-> <key.path> <json value>')
        return print(catalog_set(kit, cid, key, val))
    if cmd == 'metrics':
        from kitlib import metrics as M
        return M.main(kit, rest)
    if cmd == 'orm':
        from kitlib import texture as T
        glb, out = need(rest, 2, 'orm <model.glb> <out.png>')
        orm = T.glb_orm_image(glb)
        if orm is None:
            fail('NO_METALLIC_ROUGHNESS_IN_GLB', glb, 'this GLB has no ORM map; leave source.maps.smoothness out')
        return print('ORM', json.dumps(T.smoothness_from_orm(orm, out)))
    if cmd in ('fit', 'armfix', 'attach', 'convert'):
        from kitlib import blender as B
        if cmd == 'fit': return B.fit(kit, rest)
        script = dict(armfix='fix_arm_swing', attach='attach_props', convert='convert_glb')[cmd]
        return B.passthrough(kit, script, rest, f'USAGE: assets.py {cmd} <input> <out_dir> [options] (see 3d-tools.md)')
    if cmd == 'anim':
        from kitlib import anim
        return anim.main(kit, rest)
    if cmd == 'prep': return P.prep(kit, rest)
    if cmd == 'unity': return P.unity_build(kit, rest)
    if cmd == 'check': sys.exit(P.check(kit))
    if cmd == 'capture':
        tag = 'lineup'
        if '--tag' in rest:
            tag = need(rest[rest.index('--tag') + 1:], 1, 'capture [--tag T]')[0]
        return P.capture(kit, tag)
    if cmd == 'migrate': return P.migrate(kit, need(rest, 1, 'migrate <name>')[0])
    fail('UNKNOWN_COMMAND', cmd, 'run assets.py without arguments for the command list')


# ---------------- catalog edit under the lock ----------------
def catalog_lock(kit):
    """the catalog's OS lock file (<catalog folder>/.catalog.lock), the same one parallel agents hold while they edit"""
    import fcntl
    f = open(kit.catalog_path.parent / '.catalog.lock', 'a+')
    fcntl.flock(f, fcntl.LOCK_EX)
    return f


def catalog_set(kit, cid, key, raw):
    """set one value (asset id or '-' for the top level, dotted key) with the lock held; the file keeps indent=1 and every
    other line, and the result must still pass the catalog schema"""
    from kitlib import schema as S
    try:
        value = json.loads(raw)
    except ValueError:
        if raw[:1] in '[{"':
            fail('BAD_JSON', raw[:40], 'give a JSON value, e.g. \'"adopted"\' or 2400 or \'["a","b"]\'')
        value = raw
    lk = catalog_lock(kit)
    try:
        text = kit.catalog_path.read_text()
        cat = json.loads(text)
        if text.rstrip('\n') != json.dumps(cat, ensure_ascii=False, indent=1):
            fail('CATALOG_FORMAT_CHANGED', kit.catalog_path.name, 'someone wrote it in another format; restore indent=1 first (B1)')
        node = cat if cid == '-' else next((a for a in cat['assets'] if a['id'] == cid), None)
        if node is None:
            fail('UNKNOWN_ASSET', cid, 'use an id from `assets.py list`, or - for the top level')
        parts = key.split('.')
        for k in parts[:-1]:
            node = node.setdefault(k, {})
            if not isinstance(node, dict):
                fail('NOT_AN_OBJECT', key, f'{k} is not an object')
        old = node.get(parts[-1], '(none)')
        node[parts[-1]] = value
        bad = S.check(cat, 'catalog')
        if bad:
            fail('CATALOG_INVALID', '; '.join(bad[:3]), 'the change would break the catalog schema; nothing was written')
        tmp = kit.catalog_path.with_suffix('.json.tmp')
        tmp.write_text(json.dumps(cat, ensure_ascii=False, indent=1) + '\n')
        tmp.replace(kit.catalog_path)
    finally:
        lk.close()
    return f'CATALOG_SET {cid} {key}: {json.dumps(old, ensure_ascii=False)} -> {json.dumps(value, ensure_ascii=False)}'



# ---------------- selftest ----------------
def check_project(kit):
    """the project's catalog against the active profile; every frozen Tripo request must be reproduced exactly"""
    from kitlib import tripo as TR
    cat = kit.cat
    ids = [a['id'] for a in cat['assets']]
    assert len(ids) == len(set(ids)), ('DUPLICATE_IDS', sorted({i for i in ids if ids.count(i) > 1}))
    for a in cat['assets']:
        kit.standard(a['kind'])
        if a.get('tripo'):
            assert a['tripo']['budget'] in cat['budgets'], ('UNKNOWN_BUDGET', a['id'], a['tripo']['budget'])
        if a['method'] == 'build' and a['status'] == 'adopted':
            assert kit.abs(a['source']['texture']).exists(), ('MISSING_SOURCE', a['id'])
        if a.get('variant_of'):
            assert a['variant_of'] in ids, ('UNKNOWN_VARIANT_OF', a['id'])
        assert all(r in ids for r in a.get('replaces') or []), ('UNKNOWN_REPLACES', a['id'])
        if a.get('fit'):
            assert isinstance(a['fit'].get('height'), (int, float)) and a['fit']['height'] > 0, ('BAD_FIT', a['id'])
    sup = P.superseded_adopted(cat)
    assert not sup, ('SUPERSEDED_STILL_ADOPTED', sup)
    for name, s in (cat.get('anim_steps') or {}).items():
        assert s['type'] in ('check', 'rig', 'retarget') and isinstance(s.get('est'), int), ('BAD_ANIM_STEP', name)
    for b, v in cat['budgets'].items():
        assert all(isinstance(t, str) and t for t in (v.get('step_approvals') or {}).values()), ('EMPTY_STEP_APPROVAL', b)
    TR.setup(kit)
    checked = 0
    for asset in TR.SPECS:
        f = TR.adir(asset) / 'request-frozen.json'
        if f.exists():
            req = json.loads(f.read_text())
            imgs = TR.concepts(asset)
            img = imgs[0] if len(imgs) == 1 else imgs
            # image paths may be absolute and from another worktree of the same project; their content is the sha check below
            assert TR.args_without_images(TR.args_for(asset, img, False)) == TR.args_without_images(req['planned_args']), ('ARGS_DIFFER', asset)
            assert ([TR.sha(x) for x in img] if isinstance(img, list) else TR.sha(img)) == req['concept_sha256'], ('CONCEPT_DIFFERS', asset)
            b = TR.budget(asset)   # the frozen request belongs to the asset's current budget (no silent budget move)
            assert req.get('scope') in (f'BUDGET_{b}', cat['budgets'][b].get('legacy_scope')), ('BUDGET_MOVED', asset, req.get('scope'))
            checked += 1
    spent = {b: TR.spent(b) for b in cat['budgets']}
    assert all(spent[b] <= int(cat['budgets'][b]['cap']) for b in spent), ('OVER_CAP', {b: f"{spent[b]}>{cat['budgets'][b]['cap']}" for b in spent if spent[b] > int(cat['budgets'][b]['cap'])})
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
    Image.fromarray(np.dstack([np.zeros((64, 64, 3), np.uint8), np.full((64, 64), 180, np.uint8)])).save(d / 'src/s.png')
    cat['assets'].append(dict(id='boss_s', kind='boss', status='adopted', method='build', folder='Art/Boss/s',
                              source=dict(texture='output/src/a.png', model='output/src/a.fbx',
                                          maps=dict(normal='output/src/n.png', smoothness='output/src/s.png'))))
    (d / 'cat.json').write_text(json.dumps(cat))
    def cfg(profile):
        (d / C.CONFIG_NAME).write_text(json.dumps(dict(schema=C.CONFIG_SCHEMA, assets=dict(
            profile=profile, catalog='cat.json', content_root='Assets/G', art_root='Assets/G/Art', work_root='output/work'))))
        return C.Kit(config_path=d / C.CONFIG_NAME)
    (d / 'output').mkdir(); (d / 'src').rename(d / 'output/src')
    low, mid = cfg('lowpoly'), cfg('midpoly')
    assert low.standard('boss') != mid.standard('boss') and mid.standard('boss')['maps'] == ['albedo', 'normal']
    # lowpoly: no smoothness slot / optional maps, and the request carries no map_floats (behaviour unchanged)
    assert 'maps_optional' not in low.standard('boss') and not low.profile['material'].get('map_floats')
    assert P.maps_of(low, cat['assets'][1]) == ['albedo'] and P.maps_of(mid, cat['assets'][1]) == ['albedo', 'normal', 'smoothness']
    assert C.Kit(config_path=d / C.CONFIG_NAME, profile='lowpoly').profile_name == 'lowpoly', '--profile override'
    assert P.stage_of(low, cat['assets'][0]) == 'palette' and P.stage_of(mid, cat['assets'][0]) == 'passthrough'
    # midpoly: prep (passthrough, both maps) -> Unity request
    import contextlib, io
    with contextlib.redirect_stdout(io.StringIO()):
        P.prep(mid, ['boss_a', 'boss_s'])
    rec = json.loads((d / 'output/work/boss_a/prep/prep.json').read_text())
    size = mid.standard('boss')['texture']
    assert rec['method'] == 'passthrough' and set(rec['maps']) == {'albedo', 'normal'} and rec['maps']['albedo']['size'] == size, rec
    req = P.build_request(mid, cat['assets'])
    it = req['items'][0]
    # smoothness map is optional: absent on boss_a, present on boss_s (RGBA kept, linear, alpha from input, Lit slot + keyword)
    assert 'smoothness' not in it['textures'] and 'smoothness' not in rec['maps']
    srec = json.loads((d / 'output/work/boss_s/prep/prep.json').read_text())
    assert set(srec['maps']) == {'albedo', 'normal', 'smoothness'}, srec
    assert np.asarray(Image.open(d / 'output/work/boss_s/prep/smoothness.png')).shape == (size, size, 4)
    assert np.asarray(Image.open(d / 'output/work/boss_s/prep/smoothness.png'))[0, 0, 3] == 180
    st = req['items'][1]['textures']['smoothness']['settings']
    assert st['srgb'] is False and st['alpha'] == 'FromInput' and st['type'] == 'Default', st
    assert req['items'][1]['textures']['smoothness']['dst'].endswith('_smoothness.png') or 'smoothness' in req['items'][1]['textures']['smoothness']['dst']
    assert req['material']['slots']['smoothness'] == '_MetallicGlossMap' and req['material']['map_keywords']['smoothness'] == '_METALLICSPECGLOSSMAP'
    assert req['material']['map_floats'] == {'smoothness': {'_Smoothness': 1, '_SmoothnessTextureChannel': 0}}
    assert req['material']['shader'] == 'Universal Render Pipeline/Lit' and req['material']['slots'] == {'albedo': '_BaseMap', 'normal': '_BumpMap', 'smoothness': '_MetallicGlossMap'}
    assert req['material']['map_keywords'] == {'normal': '_NORMALMAP', 'smoothness': '_METALLICSPECGLOSSMAP'} and it['mesh_lod'] is True
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
    # prefab size: terrain module keeps its real size (fit height + bury, top face at 0), catalog height sets a character/prop height,
    # default 1 with the feet at 0; the judge expects exactly that
    assert P.prefab_size(dict(fit=dict(height=1.25, bury=0.05))) == (1.3, 'top') and P.prefab_size(dict(fit=dict(height=1.9))) == (1.95, 'top')
    assert P.prefab_size(dict(height=1.3)) == (1.3, 'feet') and P.prefab_size({}) == (1.0, 'feet')
    rq = P.build_request(mid, [dict(cat['assets'][0], fit=dict(height=1.25, bury=0.05)), dict(cat['assets'][1], height=1.3)])['items']
    assert (rq[0]['target_height'], rq[0]['anchor']) == (1.3, 'top') and (rq[1]['target_height'], rq[1]['anchor']) == (1.3, 'feet')
    mod = lambda h, feet, **kw: dict(items=[dict(id='boss_a', folder_exists=True, model_child=True, textures=tex, textures_ok=True, **kw,
                                         prefab=dict(tris=100, bones=40, materials=1, height=h, feet=feet, centre=0, shadows=['On'], materialPaths=[]),
                                         material=dict(shader='Universal Render Pipeline/Lit', props={}))], scenes=[])
    # the expected size comes from the catalog entry: the Unity result never echoes the request (S7: every adopted prop with a
    # catalog height failed "want 1.000" because the judge read expect_height from the measured item)
    boss = next(a for a in mid.cat['assets'] if a['id'] == 'boss_a')
    saved = {k: boss.pop(k) for k in ('fit', 'height') if k in boss}
    boss['fit'] = dict(height=1.25, bury=0.05)
    assert P.judge(mid, mod(1.3, -1.3))[1] == []
    assert any('not normalised' in f for f in P.judge(mid, mod(1.0, 0.0))[1]), 'module squeezed to height 1 must fail'
    assert any('not normalised' in f for f in P.judge(mid, mod(1.3, 0.0))[1]), 'module with its feet at 0 must fail'
    del boss['fit']; boss['height'] = 2.6
    assert P.judge(mid, mod(2.6, 0.0))[1] == [], 'a prop at its catalog height passes'
    assert any('not normalised' in f for f in P.judge(mid, mod(1.0, 0.0))[1]), 'a prop squeezed to 1 must fail'
    del boss['height']
    assert P.judge(mid, mod(1.0, 0.0))[1] == [], 'no fit or height: default 1'
    boss.update(saved)
    # animation (catalog 'anim'): plain clip names, idle default, loop = all clips, Speed transitions only with idle + walk;
    # no 'anim' = None (static import unchanged); bad default / duplicate clips / unknown loop clip refused
    an = P.anim_of(dict(id='x', anim=dict(clips=['idle', 'walk'])), 'Assets/G/Art/X', 'x')
    assert an == dict(clips=['idle', 'walk'], default='idle', loop=['idle', 'walk'], controller='Assets/G/Art/X/x.controller',
                      parameter='Speed', threshold=0.1), an
    an2 = P.anim_of(dict(id='y', anim=dict(clips=['idle', 'attack'], default='attack', loop=['idle'])), 'F', 'y')
    assert an2['default'] == 'attack' and an2['loop'] == ['idle'] and an2['parameter'] is None and an2['threshold'] is None, an2
    an3 = P.anim_of(dict(id='v', anim=dict(clips=['idle', 'walk', 'attack'], loop=['idle', 'walk'], overlay=dict(clip='attack', trigger='Attack', mask_root='Spine'))), 'F', 'v')
    assert an3['overlay'] == dict(clip='attack', trigger='Attack', mask_root='Spine') and 'overlay' not in an, an3
    try:
        P.anim_of(dict(id='v', anim=dict(clips=['idle'], overlay=dict(clip='attack', trigger='A', mask_root='S'))), 'F', 'v'); raise AssertionError('overlay accepted')
    except SystemExit:
        pass
    an4 = P.anim_of(dict(id='w', anim=dict(clips=['idle', 'walk', 'attack', 'hit'], loop=['idle', 'walk'], overlays=[dict(clip='attack', trigger='Attack', mask_root='hips'), dict(clip='hit', trigger='Hit', mask_root='hips')])), 'F', 'w')
    assert [o['trigger'] for o in an4['overlays']] == ['Attack', 'Hit'], an4
    for bad in ([dict(clip='nope', trigger='A', mask_root='S')], [dict(clip='hit', trigger='A', mask_root='S'), dict(clip='hit', trigger='B', mask_root='S')]):
        try:
            P.anim_of(dict(id='w', anim=dict(clips=['idle', 'hit'], overlays=bad)), 'F', 'w'); raise AssertionError('overlays accepted')
        except SystemExit:
            pass
    assert P.anim_of(dict(id='z'), 'F', 'z') is None and P.anim_of(dict(id='z', anim={}), 'F', 'z') is None
    for bad in (dict(clips=['idle'], default='walk'), dict(clips=['idle', 'idle']), dict(clips=['idle'], loop=['walk']), dict(clips=[])):
        try:
            P.anim_of(dict(id='b', anim=bad), 'F', 'b'); raise AssertionError(('accepted', bad))
        except SystemExit as e:
            assert 'BAD_ANIM' in str(e), e
    # attached parts (catalog 'parts'): the renderer name maps to the part asset's own material; none = {} (one material as before)
    boss = next(a for a in mid.cat['assets'] if a['id'] == 'boss_a')
    boss.update(anim=dict(clips=['idle', 'walk']), parts=dict(Hat='boss_s'))
    rq2 = {i['id']: i for i in P.build_request(mid, mid.cat['assets'])['items']}
    assert rq2['boss_a']['part_materials'] == {'Hat': 'Assets/G/Art/Boss/s/s.mat'} and rq2['boss_a']['animation']['controller'] == 'Assets/G/Art/Boss/a/a.controller'
    assert rq2['boss_s']['part_materials'] == {} and rq2['boss_s']['animation'] is None
    # judge: each part may bring one material on top of materials_max; without parts the extra material still fails
    mats = lambda n: dict(items=[dict(id='boss_a', folder_exists=True, model_child=True, textures=tex,
                                      prefab=dict(tris=100, bones=40, materials=n, height=1, feet=0, centre=0, shadows=['On'], materialPaths=[]),
                                      material=dict(shader='Universal Render Pipeline/Lit', props={}))], scenes=[])
    assert P.judge(mid, mats(2))[1] == [] and any('materials 3' in f for f in P.judge(mid, mats(3))[1])
    del boss['parts']
    assert any('materials 2' in f for f in P.judge(mid, mats(2))[1])
    try:
        P.part_materials(mid, dict(id='q', parts=dict(Hat='nope')), {a['id']: a for a in mid.cat['assets']}); raise AssertionError('unknown part accepted')
    except SystemExit as e:
        assert 'BAD_PART' in str(e)
    del boss['anim']
    # W2/J12: a version and the one it replaced may not both be adopted (by name X_rN, or the catalog 'replaces' list)
    ver = lambda *rows: dict(assets=[dict(id=i, status=s, **({'replaces': r} if r else {})) for i, s, r in rows])
    assert P.superseded_adopted(ver(('pot', 'adopted', None), ('pot_r2', 'adopted', None))) == [('pot', 'pot_r2')]
    assert P.superseded_adopted(ver(('pot', 'trial', None), ('pot_r2', 'adopted', None))) == []
    assert P.superseded_adopted(ver(('t', 'trial', None), ('t_r2', 'adopted', None), ('t_r3', 'adopted', None))) == [('t_r2', 't_r3')]
    assert P.superseded_adopted(ver(('pot_s2', 'adopted', None), ('pot_s2_r2', 'trial', None), ('pot', 'adopted', None))) == []
    assert P.superseded_adopted(ver(('hero_s4', 'adopted', None), ('hero_s5', 'adopted', ['hero_s4']))) == [('hero_s4', 'hero_s5')]
    assert P.superseded_adopted(ver(('a_r10', 'adopted', None), ('a_r9', 'adopted', None))) == [('a_r9', 'a_r10')]
    # no adopted asset: the check does not die on an empty table
    rows0, fails0 = P.judge(mid, dict(items=[], scenes=[]))
    assert rows0 == [] and fails0 == []
    import shutil
    shutil.rmtree(d, ignore_errors=True)
    return f'profiles OK (lowpoly <-> midpoly by assets.profile; midpoly boss {s["tris_target"]}/{s["tris_max"]} tris, {size}px albedo+normal, URP Lit, Mesh LOD, anim block, part materials)'


def check_contract():
    """005: schema subset, config paths inside the project, refusals with a next action, catalog-set under the lock"""
    from kitlib import schema as S, errors as E
    print(S.selftest())
    d = Path(tempfile.mkdtemp(prefix='3dkit-contract-'))
    base = dict(schema=C.CONFIG_SCHEMA, assets=dict(profile='lowpoly', catalog='cat.json', content_root='Assets/G', art_root='Assets/G/Art', work_root='output/work'))
    def kit_with(cfg, cat=None):
        (d / C.CONFIG_NAME).write_text(json.dumps(cfg))
        (d / 'cat.json').write_text(json.dumps(cat or dict(schema=C.CATALOG_SCHEMA, budgets={}, assets=[
            dict(id='a1', kind='prop_small', status='trial', method='build', folder='Art/a1')]), ensure_ascii=False, indent=1) + '\n')
        try:
            return C.Kit(config_path=d / C.CONFIG_NAME)
        except SystemExit as e:
            return E.code_of(e)
    assert isinstance(kit_with(base), C.Kit)
    out = kit_with(dict(base, assets=dict(base['assets'], work_root='../elsewhere')))
    assert out.startswith('CONFIG_INVALID') and 'outside the project' in out, out
    assert kit_with(dict(base, assets=dict(base['assets'], profile=3))).startswith('CONFIG_INVALID')
    bad_cat = dict(schema=C.CATALOG_SCHEMA, budgets={}, assets=[dict(id='a1', kind='x', status='adopt', method='build', folder='f')])
    assert kit_with(base, bad_cat).startswith('CATALOG_INVALID'), 'status typo must fail'
    k = kit_with(base)
    msg = catalog_set(k, 'a1', 'status', 'adopted')
    after = (d / 'cat.json').read_text()
    assert msg.startswith('CATALOG_SET a1 status') and json.loads(after)['assets'][0]['status'] == 'adopted' and after.endswith('}\n')
    try:
        catalog_set(k, 'a1', 'status', 'adopt'); raise AssertionError('schema-breaking edit written')
    except SystemExit as e:
        assert E.code_of(e).startswith('CATALOG_INVALID') and json.loads((d / 'cat.json').read_text())['assets'][0]['status'] == 'adopted'
    catalog_set(k, '-', 'budgets.b1', '{"cap": 10, "approval": "사용자 원문"}')
    assert json.loads((d / 'cat.json').read_text())['budgets']['b1']['cap'] == 10
    try:
        catalog_set(k, 'zz', 'status', 'trial'); raise AssertionError('unknown id accepted')
    except SystemExit as e:
        assert E.code_of(e) == 'UNKNOWN_ASSET:zz' and '-> next:' in str(e)
    try:
        E.need([], 1, 'palette <name>'); raise AssertionError('missing argument accepted')
    except SystemExit as e:
        assert str(e).startswith('USAGE:palette <name>')
    import shutil
    shutil.rmtree(d, ignore_errors=True)
    return 'contract OK (config paths inside the project, schema on load, catalog-set under the lock keeps the format, refusals carry next:)'


def selftest():
    from kitlib import texture as T
    print(T.selftest())
    print(check_contract())
    r = subprocess.run([sys.executable, str(HERE / 'selftest_tripo.py')], capture_output=True, text=True)
    print(r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr[-400:])
    if r.returncode:
        raise SystemExit('TRIPO_SELFTEST_FAILED')
    from kitlib import modfit
    print(modfit.selftest())
    from kitlib import anim
    print(anim.selftest())
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

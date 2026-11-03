#!/usr/bin/python3
"""050: the official asset pipeline (one path for every asset; standard + registry = docs/art/asset-catalog.json).

Run with /usr/bin/python3 (numpy + Pillow) from the project root, Unity editor open on this project.
  assets.py spec [--markdown]        print the mobile standard (numbers come from the catalog)
  assets.py list                     registered assets
  assets.py gen <cmd> <tripo asset>  Tripo generation (freeze|dryrun|submit|fetch|status|reconcile|anim ...), Tools/Art/asset_tripo.py
  assets.py palette-char [--force]   build the 8x4 character palette from the registered character textures
  assets.py prep <id...>|--all       texture -> spec size -> palette colours (output/assets/<id>/prep/)
  assets.py unity <id...>|--all      Unity: texture import, shared-shader material, prefab, actor-set entries (Tools/Art/AssetBuild.cs)
  assets.py check                    measure every registered asset + the build scenes and judge against the standard
  assets.py capture [--tag T]        game-angle and front line-ups of the actor set (output/assets/<T>-lineup_*.png)
  assets.py migrate <name>           one-time AssetDatabase moves listed in the catalog ("migrations")
  assets.py selftest                 offline tests (no Unity, no network)
A new asset: add a catalog entry (kind, folder, source or tripo block; a tripo budget with the user's approval text), then
gen -> (Blender only if over the triangle cap) -> prep -> unity -> check -> capture -> user looks at it.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import asset_texture as T  # noqa: E402

CATALOG_PATH = ROOT / 'docs/art/asset-catalog.json'
OUT = ROOT / 'output/assets'
SHADER = 'DeadSurvivor/LowPolyPalette'
CHAR_KINDS = ('hero', 'monster', 'boss')


def catalog():
    return json.loads(CATALOG_PATH.read_text())


def apath(p):
    """catalog paths: 'Assets/...' and 'output/...' as written, anything else under Assets/DeadSurvivor/"""
    return p if p.startswith(('Assets/', 'output/')) else 'Assets/DeadSurvivor/' + p


def adopted(cat):
    return [a for a in cat['assets'] if a.get('status') == 'adopted']


def by_id(cat, ids):
    table = {a['id']: a for a in cat['assets']}
    missing = [i for i in ids if i not in table]
    if missing:
        raise SystemExit('UNKNOWN_ASSET:' + ','.join(missing))
    return [table[i] for i in ids]


def pick(cat, args, pred):
    if args == ['--all']:
        return [a for a in adopted(cat) if pred(a)]
    if not args:
        raise SystemExit('GIVE_IDS_OR_--all')
    return by_id(cat, args)


def name_of(a):
    return Path(a['folder']).name


def palette_json(cat, kind):
    return ROOT / apath(cat['palettes'][cat['standards'][kind]['palette']]['json'])


# ---------------- spec ----------------
def spec(markdown):
    cat = catalog()
    rows = [('종류', '삼각형 목표/상한', '텍스처', '뼈', '재질', '그림자', '색')]
    for k, s in cat['standards'].items():
        if s.get('color') == 'exempt':
            continue
        tex = f"{s['texture']}²" if s['texture'] else '없음(팔레트 칸 UV)'
        color = {'palette_texture': '텍스처 팔레트화', 'palette_uv': '면→팔레트 칸'}[s['color']] + f" ({s['palette']})"
        rows.append((s['label'], f"{s['tris_target']:,} / {s['tris_max']:,}", tex, str(s['bones_max']), str(s['materials_max']),
                     {'real': '실제', 'blob': '바닥 원', 'off': '없음'}[s['shadow']], color))
    if markdown:
        print('| ' + ' | '.join(rows[0]) + ' |\n|' + '---|' * len(rows[0]))
        for r in rows[1:]:
            print('| ' + ' | '.join(r) + ' |')
    else:
        w = [max(len(r[i]) for r in rows) for i in range(len(rows[0]))]
        for r in rows:
            print('  '.join(x.ljust(w[i]) for i, x in enumerate(r)))


def list_assets():
    for a in catalog()['assets']:
        print(f"{a['id']:<16} {a['kind']:<10} {a['status']:<8} {a['method']:<20} {a['folder']}")


# ---------------- palette / prep ----------------
def char_textures(cat):
    return {a['id']: ROOT / apath(a['source']['texture']) for a in adopted(cat)
            if a['method'] == 'palette_texture' and cat['standards'][a['kind']]['palette'] == 'char'}


def palette_char(force):
    cat = catalog(); p = cat['palettes']['char']
    png, js = ROOT / apath(p['png']), ROOT / apath(p['json'])
    if (png.exists() or js.exists()) and not force:
        raise SystemExit('PALETTE_EXISTS (a palette change re-colours every character: add a new revision instead, or --force)')
    rec = T.build_char_palette(char_textures(cat), p['fixed'], png, js, p['seed'],
                               identity_textures={a['id'] for a in adopted(cat) if a.get('identity')})
    print(json.dumps(dict(palette=str(png.relative_to(ROOT)), cells=[c['hex'] for c in rec['cells']]), ensure_ascii=False))


def prep(args):
    cat = catalog()
    for a in pick(cat, args, lambda a: a['method'] == 'palette_texture'):
        if a['method'] != 'palette_texture':
            raise SystemExit('NOT_A_PALETTE_TEXTURE_ASSET:' + a['id'])
        s = cat['standards'][a['kind']]
        d = OUT / a['id'] / 'prep'; d.mkdir(parents=True, exist_ok=True)
        cell = list(cat['palettes']['char']['fixed']).index(a['identity']) if a.get('identity') else None
        rec = T.quantize(ROOT / apath(a['source']['texture']), palette_json(cat, a['kind']), s['texture'], d / 'albedo.png',
                         rows=a.get('palette_rows'), base_cell=a.get('base_cell'), compress=a.get('compress', 1.0), snap_cell=cell)
        if a.get('identity'):
            rec['identity_cell'] = cell
            rec['identity_share'] = round(T.identity_share(d / 'albedo.png', palette_json(cat, a['kind']), rec['identity_cell']), 4)
        (d / 'prep.json').write_text(json.dumps(rec, ensure_ascii=False, indent=1) + '\n')
        print(f"PREP {a['id']}: {s['texture']}px cells={len(rec['cells_used'])} err={rec['oklab_error']}"
              + (f" identity={rec['identity_share']}" if a.get('identity') else ''))


# ---------------- Unity ----------------
def unity(entry, request):
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / f'{entry}-request.json').write_text(json.dumps(request, ensure_ascii=False, indent=1) + '\n')
    res = OUT / f'{entry}-result.json'
    res.unlink(missing_ok=True)
    p = subprocess.run(['unity', 'command', 'script', 'run', '--project-path', str(ROOT), '--', '--file', 'Tools/Art/AssetBuild.cs',
                        '--entry', f'DSAssetBuild.{entry}'], cwd=ROOT, capture_output=True, text=True, timeout=900)
    text = p.stdout + p.stderr
    if not res.exists():
        m = re.search(r'(Exception|error)[^\n]{0,400}', text)
        raise SystemExit(f'UNITY_{entry.upper()}_FAILED: ' + (m.group(0) if m else text[-600:]))
    return json.loads(res.read_text())


def build_request(cat, assets):
    items = []
    base = {a['id']: a for a in cat['assets']}
    for a in assets:
        if a['method'] != 'palette_texture' or a['kind'] not in CHAR_KINDS:
            raise SystemExit('UNITY_BUILD_SUPPORTS_CHARACTERS_ONLY:' + a['id'])
        s = cat['standards'][a['kind']]; n = name_of(a)
        albedo = OUT / a['id'] / 'prep' / 'albedo.png'
        if not albedo.exists():
            raise SystemExit('RUN_PREP_FIRST:' + a['id'])
        hero = a['kind'] == 'hero'
        items.append(dict(id=a['id'], kind=a['kind'], name=n, folder=apath(a['folder']), texture_src=str(albedo), texture_size=s['texture'],
                          material_name=('Hero_' if hero else 'Monster_') + n, prefab_name=None if hero else 'Monster_' + n,
                          model_src=None if hero else str(ROOT / apath(a['source']['model'])), yaw=float(a['source'].get('yaw', 0)),
                          shadow=s['shadow'], preset=cat['presets']['character'],
                          actor_list=(a.get('actor') or {}).get('list'), actor_id=(a.get('actor') or {}).get('id')))
    hb = base['hero_base']
    heroes = [i for i in items if i['kind'] == 'hero']
    return dict(shader=SHADER, actor_set=actor_set_path(cat), items=items,
                hero_prefab=apath(hb['folder'] + '/' + hb['prefab']) if any(i['id'] == 'hero_bruno' for i in heroes) else None,
                hero_default_item='hero_bruno')


def actor_set_path(cat):
    a = next(x for x in cat['assets'] if x['id'] == 'actor_set')
    return apath(a['folder'] + '/' + a['file'])


def unity_build(args):
    cat = catalog()
    r = unity('Build', build_request(cat, pick(cat, args, lambda a: a['method'] == 'palette_texture' and a['kind'] in CHAR_KINDS)))
    for i in r['items']:
        print(f"BUILT {i['id']}: {i['material']}" + (f" prefab={i['prefab']}" if i['prefab'] else ''))
    if r.get('heroDefault'):
        print('HERO_PREFAB_DEFAULT', r['heroDefault']['material'])


# ---------------- check ----------------
def build_scenes():
    txt = (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text()
    return [m.group(2) for m in re.finditer(r'enabled: (\d)\s+path: (\S+)', txt) if m.group(1) == '1']


def check_request(cat):
    items = []
    by = {a['id']: a for a in cat['assets']}
    for a in adopted(cat):
        f = apath(a['folder']); n = name_of(a); it = dict(id=a['id'], folder=f, prefab=None, material=None, texture=None, model_child=False)
        if a['method'] == 'palette_texture' and a['kind'] == 'hero':
            b = by[a['variant_of']]
            it.update(prefab=apath(b['folder'] + '/' + b['prefab']), material=f'{f}/Hero_{n}.mat', texture=f'{f}/{n}_albedo.png')
        elif a['method'] == 'palette_texture' and a['kind'] in CHAR_KINDS:
            it.update(prefab=f'{f}/Monster_{n}.prefab', material=f'{f}/Monster_{n}.mat', texture=f'{f}/{n}_albedo.png', model_child=True)
        elif a['method'] == 'existing_rig':
            it.update(prefab=f"{f}/{a['prefab']}")
        elif a['method'] == 'existing_palette_uv' and a.get('model'):
            it.update(prefab=f"{f}/{a['model']}")
        items.append(it)
    return dict(items=items, scenes=build_scenes(), actor_set=actor_set_path(cat))


def judge(cat, m):
    std = cat['standards']; common = cat['common']
    trial = tuple(apath(x) for x in common['trial_roots'])
    # an entry with 'file' owns that one file, not its folder (050 impl review M1: actor_set sits in Art/Characters)
    folders = tuple(apath(a['folder'] + '/' + a['file']) if a.get('file') else apath(a['folder']).rstrip('/') + '/' for a in adopted(cat))
    allowed = set(common['shader_allowed'])
    rows, fails = [], []
    by = {a['id']: a for a in cat['assets']}
    for it in m['items']:
        a = by[it['id']]; s = std[a['kind']]; bad = []
        if not it['folder_exists']:
            bad.append('folder missing')
        pf = it.get('prefab') or {}
        if pf.get('missing'):
            bad.append('prefab missing')
        tris = pf.get('tris')
        if tris is not None and 'tris_max' in s and tris > s['tris_max']:
            bad.append(f"tris {tris} > {s['tris_max']}")
        if pf and 'bones_max' in s and pf.get('bones', 0) > s['bones_max']:
            bad.append(f"bones {pf['bones']} > {s['bones_max']}")
        if pf and 'materials_max' in s and a['method'] != 'existing_rig' and pf.get('materials', 0) > s['materials_max']:
            bad.append(f"materials {pf['materials']}")
        if a['method'] == 'palette_texture':
            mat = it.get('material') or {}
            if not mat or mat.get('shader') != SHADER:
                bad.append(f"shader {mat.get('shader') if mat else None}")
            if mat and mat.get('top', 0) != 0:
                bad.append('top projection on')
            t = it.get('texture') or {}
            want = f"ASTC_4x4@{s['texture']}"
            if t.get('missing') or t.get('width') != s['texture'] or t.get('android') != want or t.get('ios') != want or not t.get('mip') or t.get('alpha') != 'None':
                bad.append(f"texture {t}")
            if a['kind'] in ('monster', 'boss'):
                if abs(pf.get('height', 0) - 1) > .02 or abs(pf.get('feet', 1)) > .02 or pf.get('centre', 1) > .02:
                    bad.append(f"prefab not normalised h={pf.get('height')} feet={pf.get('feet')} c={pf.get('centre')}")
                if s['shadow'] == 'blob' and pf.get('shadows') != ['Off']:
                    bad.append(f"shadow {pf.get('shadows')}")
                if any(p.startswith(trial) for p in pf.get('materialPaths', [])):
                    bad.append('prefab material in a trial folder')
        if a['method'] == 'existing_rig' and any(p.startswith(trial) for p in pf.get('materialPaths', [])):
            bad.append('default material in a trial folder: ' + ','.join(pf['materialPaths']))
        rows.append((a['id'], a['kind'], '-' if tris is None else f'{tris:,}', (it.get('texture') or {}).get('android', '-'), 'FAIL: ' + '; '.join(bad) if bad else 'PASS'))
        fails += [f"{a['id']}: {b}" for b in bad]
    act = m.get('actors') or {}
    for e in act.get('monsters', []):
        if e['yawOffset'] != 0:
            fails.append(f"actor set monster {e['id']}: yawOffset {e['yawOffset']} (front must be baked, +Z)")
    for sc in m['scenes']:
        deps = sc['deps']
        for d in deps:
            if d.startswith(trial):
                fails.append(f"{Path(sc['scene']).name}: trial-folder dependency {d}")
            elif d.startswith('Assets/DeadSurvivor/Art/') and not d.startswith(folders):
                fails.append(f"{Path(sc['scene']).name}: unregistered art {d}")
        for mt in sc['materials']:
            if mt['shader'] not in allowed:
                fails.append(f"{Path(sc['scene']).name}: shader {mt['shader']} on {mt['path']}")
        for t in sc['textures']:
            size = max(t['t'].get('width') or 0, t['t'].get('height') or 0)   # imported size (the importer max only caps it)
            if size > 256:
                fails.append(f"{Path(sc['scene']).name}: texture over 256: {t['path']} ({size})")
    return rows, fails


def check():
    cat = catalog()
    m = unity('Check', check_request(cat))
    rows, fails = judge(cat, m)
    w = [max(len(str(r[i])) for r in rows) for i in range(4)]
    for r in rows:
        print('  '.join(str(x).ljust(w[i]) for i, x in enumerate(r[:4])) + '  ' + r[4])
    scene_fails = [f for f in fails if not any(f.startswith(r[0] + ':') for r in rows)]
    for f in scene_fails:
        print('FAIL', f)
    report = dict(status='FAIL' if fails else 'PASS', fails=fails, measured=m)
    (OUT / 'check-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=1) + '\n')
    print(f"CHECK {report['status']}: {len(rows)} assets, {len(m['scenes'])} build scenes, {len(fails)} problem(s). report: output/assets/check-report.json")
    return 0 if not fails else 1


def capture(tag):
    r = unity('Capture', dict(actor_set=actor_set_path(catalog()), tag=tag))
    for s in r['shots']:
        print('SHOT', s)


def migrate(name):
    moves = catalog()['migrations'][name]
    r = unity('Move', dict(moves=moves))
    for x in r['moves']:
        print(x['state'], x['from'], '->', x['to'])


# ---------------- selftest ----------------
def selftest():
    print(T.selftest())
    cat = catalog()
    ids = [a['id'] for a in cat['assets']]
    assert len(ids) == len(set(ids)), 'duplicate ids'
    for a in cat['assets']:
        assert a['kind'] in cat['standards'], a['id']
        if a.get('tripo'):
            assert a['tripo']['budget'] in cat['budgets'], a['id']
        if a['method'] == 'palette_texture':
            assert (ROOT / apath(a['source']['texture'])).exists() or a['status'] != 'adopted', a['id']
    # Tripo promotion: every frozen 045/046 request must be reproduced exactly from the catalog, budgets must group the
    # assets exactly as the old stages did, and the spent totals must be the ones the old journals give
    import asset_tripo as AT
    legacy_stage = {'stone045': 2, 'boulder045': 2, 'tree045': 2, 'hero_bruno045': 3, 'hero_base045v2': 3,
                    'monster_ranged046': 4, 'monster_charger046': 4, 'monster_armored046': 4, 'monster_bomber046': 4}
    stage_budget = {2: '045-s2', 3: '045-s3', 4: '046-monsters'}
    assert set(AT.SPECS) == set(legacy_stage), sorted(AT.SPECS)
    checked = 0
    for asset, st in legacy_stage.items():
        assert AT.budget(asset) == stage_budget[st], asset
        f = AT.adir(asset) / 'request-frozen.json'
        if f.exists():
            req = json.loads(f.read_text())
            imgs = AT.concepts(asset); img = imgs[0] if len(imgs) == 1 else imgs
            assert AT.args_for(asset, img, False) == req['planned_args'], ('ARGS_DIFFER', asset)
            assert ([AT.sha(x) for x in img] if isinstance(img, list) else AT.sha(img)) == req['concept_sha256'], ('CONCEPT_DIFFERS', asset)
            checked += 1
    caps = {b: AT.BUDGETS[b]['cap'] for b in stage_budget.values()}
    assert caps == {'045-s2': 240, '045-s3': 255, '046-monsters': 340}, caps
    spent = {b: AT.spent(b) for b in caps}
    assert all(spent[b] <= caps[b] for b in caps), spent
    assert AT.approval('hero_base045v2').startswith('User approved the stage-3 v2'), 'v2 approval'
    assert AT.step_auth('hero_base045v2', 'attack').startswith('User approved the hero attack'), 'attack approval'
    # judge: a monster over the cap and with a real shadow fails; a build scene using a trial folder fails
    fake = dict(items=[dict(id='monster_ranged', folder_exists=True, prefab=dict(tris=1200, bones=0, materials=1, height=1, feet=0, centre=0, shadows=['On'], materialPaths=[]),
                            material=dict(shader=SHADER, top=0), texture=dict(width=128, android='ASTC_4x4@128', ios='ASTC_4x4@128', mip=True, alpha='None'))],
                scenes=[dict(scene='X.unity', deps=['Assets/DeadSurvivor/Scenes/Proto045/Art/Hit045.mat'], materials=[], textures=[])], actors={})
    fake['scenes'][0]['deps'].append('Assets/DeadSurvivor/Art/Characters/Foo/x.mat')
    _, fails = judge(cat, fake)
    assert any('tris 1200' in f for f in fails) and any('shadow' in f for f in fails) and any('trial-folder' in f for f in fails), fails
    assert any('unregistered art Assets/DeadSurvivor/Art/Characters/Foo/x.mat' in f for f in fails), fails
    print(f'assets selftest PASS (catalog {len(ids)} assets, {checked} frozen Tripo requests reproduced, spent {spent})')


def main():
    a = sys.argv[1:]
    if not a:
        raise SystemExit(__doc__)
    cmd, rest = a[0], a[1:]
    if cmd == 'spec': return spec('--markdown' in rest)
    if cmd == 'list': return list_assets()
    if cmd == 'gen':
        import asset_tripo
        sys.argv = ['asset_tripo.py', *rest]
        return asset_tripo.main()
    if cmd == 'palette-char': return palette_char('--force' in rest)
    if cmd == 'prep': return prep(rest)
    if cmd == 'unity': return unity_build(rest)
    if cmd == 'check': sys.exit(check())
    if cmd == 'capture': return capture(rest[rest.index('--tag') + 1] if '--tag' in rest else 'lineup')
    if cmd == 'migrate': return migrate(rest[0])
    if cmd == 'selftest': return selftest()
    raise SystemExit(__doc__)


if __name__ == '__main__':
    main()

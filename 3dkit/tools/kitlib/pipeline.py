"""3dkit asset pipeline stages: spec, list, palette, prep (texture stage), unity (import + material + prefab + LOD +
project register), check (measure in Unity, judge here), capture, migrate.

Which texture stage an asset gets is decided by the profile: standards[kind].color
  palette_texture -> palette (kitlib/texture.py quantize)      texture -> passthrough      palette_uv -> not a tool stage
Ported from Deadsurvivor Tools/Art/assets.py (050); the judge rules are the same, with the project values (paths,
shader, naming, trial folders, allowed shaders) read from the config and catalog instead of constants.
"""
import json
import re
from pathlib import Path

from . import texture as T
from . import unity as U

COLOR_STAGE = {'palette_texture': 'palette', 'texture': 'passthrough', 'palette_uv': None, 'exempt': None}
COLOR_LABEL = {'palette_texture': '텍스처 팔레트화', 'palette_uv': '면→팔레트 칸', 'texture': '텍스처(원본 색)'}
SHADOW_LABEL = {'real': '실제', 'blob': '바닥 원', 'off': '없음'}
DEFAULT_NAMING = {'material': '{name}', 'prefab': '{name}', 'texture': '{name}_{map}'}
MOBILE = ('Android', 'iPhone')


# ---------------- catalog helpers ----------------
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


def maps_of(kit, a):
    return a.get('texture_maps') or kit.standard(a['kind']).get('maps') or ['albedo']


def stage_of(kit, a):
    return COLOR_STAGE.get(kit.standard(a['kind']).get('color'))


def naming(kit, kind):
    n = (kit.cat.get('project') or {}).get('naming') or {}
    return {**DEFAULT_NAMING, **(n.get('default') or {}), **(n.get(kind) or {})}


def hook_data(kit):
    """catalog project.hook_data, passed to the project's C# hooks; 'asset:<id>' becomes that asset's path"""
    by = {a['id']: a for a in kit.cat['assets']}
    out = {}
    for k, v in ((kit.cat.get('project') or {}).get('hook_data') or {}).items():
        if isinstance(v, str) and v.startswith('asset:'):
            a = by[v[6:]]
            v = kit.apath(a['folder'] + '/' + a['file']) if a.get('file') else kit.apath(a['folder'])
        out[k] = v
    return out or None


def texture_import(kit, map_name):
    imp = kit.profile['import']
    return {**imp['texture'], **((imp.get('texture_by_map') or {}).get(map_name) or {})}


# ---------------- spec / list / profile ----------------
def spec(kit, markdown):
    rows = [('종류', '삼각형 목표/상한', '텍스처', '뼈', '재질', '그림자', '색')]
    for k in kit.profile['standards']:
        s = kit.standard(k)
        if s.get('color') == 'exempt':
            continue
        maps = s.get('maps') or ['albedo']
        if s['texture']:
            tex = f"{s['texture']}²" + ('' if maps == ['albedo'] else f" ({'·'.join(maps)})")
        else:
            tex = '없음(팔레트 칸 UV)' if s['color'] == 'palette_uv' else '없음'
        color = COLOR_LABEL[s['color']] + (f" ({s['palette']})" if s.get('palette') else '')
        rows.append((s['label'], f"{s['tris_target']:,} / {s['tris_max']:,}", tex, str(s['bones_max']), str(s['materials_max']),
                     SHADOW_LABEL[s['shadow']], color))
    if markdown:
        print('| ' + ' | '.join(rows[0]) + ' |\n|' + '---|' * len(rows[0]))
        for r in rows[1:]:
            print('| ' + ' | '.join(r) + ' |')
    else:
        print(f'profile {kit.profile_name} (numbers: profile, overridden kinds: {sorted((kit.cat.get("standards_override") or {}).keys()) or "none"})')
        w = [max(len(r[i]) for r in rows) for i in range(len(rows[0]))]
        for r in rows:
            print('  '.join(x.ljust(w[i]) for i, x in enumerate(r)))


def list_assets(kit):
    for a in kit.cat['assets']:
        print(f"{a['id']:<16} {a['kind']:<10} {a['status']:<8} {a['method']:<20} {a['folder']}")


def show_profile(kit):
    p = kit.profile
    print(json.dumps(dict(profile=kit.profile_name, label=p.get('label'), shader=kit.material_shader(),
                          stages=dict(generate=p['generate']['tripo']['model'], decimate=p['decimate']['method'],
                                      texture={k: COLOR_STAGE.get(kit.standard(k).get('color')) for k in p['standards']},
                                      lod=p['lod']['method']),
                          slots=p['material']['slots'], scene_texture_max=p['check']['scene_texture_max']), ensure_ascii=False, indent=1))


# ---------------- palette / prep ----------------
def palette(kit, name, force):
    cat = kit.cat
    p = cat['palettes'][name]
    if 'fixed' not in p:
        raise SystemExit('PALETTE_NOT_BUILDABLE:' + name)
    png, js = kit.abs(p['png']), kit.abs(p['json'])
    if (png.exists() or js.exists()) and not force:
        raise SystemExit('PALETTE_EXISTS (a palette change re-colours every asset that uses it: add a new revision instead, or --force)')
    srcs = {a['id']: kit.abs(a['source']['texture']) for a in adopted(cat)
            if a['method'] == 'build' and stage_of(kit, a) == 'palette' and kit.standard(a['kind']).get('palette') == name}
    rec = T.build_char_palette(srcs, p['fixed'], png, js, p['seed'], identity_textures={a['id'] for a in adopted(cat) if a.get('identity')})
    print(json.dumps(dict(palette=str(png.relative_to(kit.root)), cells=[c['hex'] for c in rec['cells']]), ensure_ascii=False))


def prep(kit, args):
    cat = kit.cat
    for a in pick(cat, args, lambda a: a['method'] == 'build' and stage_of(kit, a)):
        if a['method'] != 'build':
            raise SystemExit('NOT_A_BUILD_ASSET:' + a['id'])
        s = kit.standard(a['kind'])
        stage = stage_of(kit, a)
        if not stage:
            raise SystemExit(f"NO_TEXTURE_STAGE:{a['id']}:{s.get('color')}")
        d = kit.work / a['id'] / 'prep'
        d.mkdir(parents=True, exist_ok=True)
        if stage == 'palette':
            pal = cat['palettes'][s['palette']]
            pal_json = kit.abs(pal['json'])
            cell = list(pal['fixed']).index(a['identity']) if a.get('identity') else None
            rec = T.quantize(kit.abs(a['source']['texture']), pal_json, s['texture'], d / 'albedo.png',
                             rows=a.get('palette_rows'), base_cell=a.get('base_cell'), compress=a.get('compress', 1.0), snap_cell=cell)
            if a.get('identity'):
                rec['identity_cell'] = cell
                rec['identity_share'] = round(T.identity_share(d / 'albedo.png', pal_json, cell), 4)
            line = f"{s['texture']}px cells={len(rec['cells_used'])} err={rec['oklab_error']}" + (f" identity={rec['identity_share']}" if a.get('identity') else '')
        else:
            srcs = {'albedo': a['source']['texture'], **(a['source'].get('maps') or {})}
            missing = [m for m in maps_of(kit, a) if m not in srcs]
            if missing:
                raise SystemExit(f"MISSING_MAP:{a['id']}:{','.join(missing)} (give source.maps or set texture_maps on the asset)")
            rec = dict(maps={m: T.passthrough(kit.abs(srcs[m]), s['texture'], d / f'{m}.png', m) for m in maps_of(kit, a)})
            line = f"{s['texture']}px maps={','.join(rec['maps'])}" + (' UPSCALED' if any(v['upscaled'] for v in rec['maps'].values()) else '')
        rec.update(method=stage, profile=kit.profile_name)
        (d / 'prep.json').write_text(json.dumps(rec, ensure_ascii=False, indent=1) + '\n')
        print(f"PREP {a['id']}: {line}")


# ---------------- Unity build ----------------
def build_request(kit, assets):
    prof = kit.profile
    by = {a['id']: a for a in kit.cat['assets']}
    items, ids = [], set()
    for a in assets:
        if a['method'] != 'build':
            raise SystemExit('NOT_A_BUILD_ASSET:' + a['id'])
        s = kit.standard(a['kind'])
        if not stage_of(kit, a):
            raise SystemExit(f"NO_TEXTURE_STAGE:{a['id']}:{s.get('color')}")
        n, f, nm = name_of(a), kit.apath(a['folder']), naming(kit, a['kind'])
        tex = {}
        for m in maps_of(kit, a):
            src = kit.work / a['id'] / 'prep' / f'{m}.png'
            if not src.exists():
                raise SystemExit('RUN_PREP_FIRST:' + a['id'])
            tex[m] = dict(src=str(src), dst=f"{f}/{nm['texture'].format(name=n, map=m)}.png", size=s['texture'], settings=texture_import(kit, m))
        model = (a.get('source') or {}).get('model')
        preset = prof['material']['presets'][s['preset']] if s.get('preset') else {}
        items.append(dict(id=a['id'], kind=a['kind'], name=n, folder=f, textures=tex,
                          material=f"{f}/{nm['material'].format(name=n)}.mat", preset=preset,
                          model_src=str(kit.abs(model)) if model else None, model_dst=f"{f}/{n}.fbx" if model else None,
                          prefab=f"{f}/{nm['prefab'].format(name=n)}.prefab" if model else None,
                          yaw=float(a['source'].get('yaw', 0)), shadow=s['shadow'],
                          mesh_lod=bool(s.get('lod')) and prof['lod']['method'] == 'mesh_lod', project=a.get('project')))
        ids.add(a['id'])
    # a shared base prefab (variant_of) takes its renderer material from its default_variant, when that variant is built
    bases = []
    for a in assets:
        b = by.get(a.get('variant_of') or '')
        if b and b.get('default_variant') in ids and not any(x['item'] == b['default_variant'] for x in bases):
            bases.append(dict(prefab=kit.apath(b['folder'] + '/' + b['prefab']), item=b['default_variant']))
    m = prof['material']
    return dict(profile=kit.profile_name,
                material=dict(shader=kit.material_shader(), slots=m['slots'], colors=m.get('colors') or {}, instancing=bool(m.get('instancing')),
                              keyword_toggles=m.get('keyword_toggles') or {}, map_keywords=m.get('map_keywords') or {}),
                model_import=prof['import']['model'], prefab=prof['prefab'], items=items, base_defaults=bases, hooks=hook_data(kit))


def unity_build(kit, args):
    r = U.run(kit, 'Build', build_request(kit, pick(kit.cat, args, lambda a: a['method'] == 'build' and stage_of(kit, a))))
    for i in r['items']:
        print(f"BUILT {i['id']}: {i['material']}" + (f" prefab={i['prefab']}" if i.get('prefab') else ''))
    for b in r.get('baseDefaults') or []:
        print('BASE_PREFAB_DEFAULT', b['prefab'], '<-', b['material'])
    if r.get('register') is not None:
        print('REGISTERED', json.dumps(r['register'], ensure_ascii=False))


# ---------------- check ----------------
def build_scenes(kit):
    txt = (kit.root / 'ProjectSettings/EditorBuildSettings.asset').read_text()
    return [m.group(2) for m in re.finditer(r'enabled: (\d)\s+path: (\S+)', txt) if m.group(1) == '1']


def check_request(kit):
    by = {a['id']: a for a in kit.cat['assets']}
    items = []
    for a in adopted(kit.cat):
        f, n = kit.apath(a['folder']), name_of(a)
        it = dict(id=a['id'], folder=f, prefab=None, material=None, textures={}, model_child=False)
        if a['method'] == 'build':
            nm = naming(kit, a['kind'])
            it['material'] = f"{f}/{nm['material'].format(name=n)}.mat"
            it['textures'] = {m: f"{f}/{nm['texture'].format(name=n, map=m)}.png" for m in maps_of(kit, a)}
            if (a.get('source') or {}).get('model'):
                it.update(prefab=f"{f}/{nm['prefab'].format(name=n)}.prefab", model_child=True)
            elif a.get('variant_of'):
                b = by[a['variant_of']]
                it['prefab'] = kit.apath(b['folder'] + '/' + b['prefab'])
        elif a['method'] == 'existing_rig':
            it['prefab'] = f"{f}/{a['prefab']}"
        elif a['method'] == 'existing_model' and a.get('model'):
            it['prefab'] = f"{f}/{a['model']}"
        items.append(it)
    return dict(items=items, scenes=build_scenes(kit), content_root=kit.content_root, art_root=kit.art_root,
                material_props=list(kit.profile['check'].get('material_props') or {}), slots=list(kit.profile['material']['slots'].values()),
                hooks=hook_data(kit))


def judge(kit, m):
    cat, prof = kit.cat, kit.profile
    proj = cat.get('project') or {}
    trial = tuple(kit.apath(x) for x in proj.get('trial_roots', []))
    # an entry with 'file' owns that one file, not its folder (050 impl review M1: the actor set sits in Art/Characters)
    folders = tuple(kit.apath(a['folder'] + '/' + a['file']) if a.get('file') else kit.apath(a['folder']).rstrip('/') + '/' for a in adopted(cat))
    shader = kit.material_shader()
    allowed = set(proj.get('shader_allowed') or {}) | {shader}
    fmt = {p: prof['import']['texture']['platforms'][p] for p in MOBILE}
    props = prof['check'].get('material_props') or {}
    scene_max = prof['check']['scene_texture_max']
    by = {a['id']: a for a in cat['assets']}
    rows, fails = [], []
    for it in m['items']:
        a = by[it['id']]
        s = kit.standard(a['kind'])
        bad = []
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
        if a['method'] == 'build':
            mat = it.get('material') or {}
            if not mat or mat.get('shader') != shader:
                bad.append(f"shader {mat.get('shader') if mat else None}")
            for k, want in props.items():
                got = (mat.get('props') or {}).get(k) if mat else None
                if mat and got != want:
                    bad.append(f'material {k} {got} (want {want})')
            for mp, t in (it.get('textures') or {}).items():
                want = {p: f"{fmt[p]}@{s['texture']}" for p in MOBILE}
                if t.get('missing') or t.get('width') != s['texture'] or t.get('android') != want['Android'] or t.get('ios') != want['iPhone'] \
                        or not t.get('mip') or t.get('alpha') != 'None':
                    bad.append(f'texture {mp} {t}')
            if it.get('model_child'):     # a prefab the tool built from a model: root (feet) -> Visual -> model of height 1
                if abs(pf.get('height', 0) - 1) > .02 or abs(pf.get('feet', 1)) > .02 or pf.get('centre', 1) > .02:
                    bad.append(f"prefab not normalised h={pf.get('height')} feet={pf.get('feet')} c={pf.get('centre')}")
                if s['shadow'] == 'blob' and pf.get('shadows') != ['Off']:
                    bad.append(f"shadow {pf.get('shadows')}")
                if any(p.startswith(trial) for p in pf.get('materialPaths', [])):
                    bad.append('prefab material in a trial folder')
        if a['method'] == 'existing_rig' and any(p.startswith(trial) for p in pf.get('materialPaths', [])):
            bad.append('default material in a trial folder: ' + ','.join(pf['materialPaths']))
        alb = (it.get('textures') or {}).get('albedo') or {}
        rows.append((a['id'], a['kind'], '-' if tris is None else f'{tris:,}', alb.get('android', '-'), 'FAIL: ' + '; '.join(bad) if bad else 'PASS'))
        fails += [f"{a['id']}: {b}" for b in bad]
    fails += [str(p) for p in m.get('project_problems') or []]
    for sc in m['scenes']:
        name = Path(sc['scene']).name
        for d in sc['deps']:
            if d.startswith(trial):
                fails.append(f'{name}: trial-folder dependency {d}')
            elif d.startswith(kit.art_root + '/') and not d.startswith(folders):
                fails.append(f'{name}: unregistered art {d}')
        for mt in sc['materials']:
            if mt['shader'] not in allowed:
                fails.append(f"{name}: shader {mt['shader']} on {mt['path']}")
        for t in sc['textures']:
            size = max(t['t'].get('width') or 0, t['t'].get('height') or 0)   # imported size (the importer max only caps it)
            if size > scene_max:
                fails.append(f"{name}: texture over {scene_max}: {t['path']} ({size})")
    return rows, fails


def check(kit):
    m = U.run(kit, 'Check', check_request(kit))
    rows, fails = judge(kit, m)
    w = [max(len(str(r[i])) for r in rows) for i in range(4)]
    for r in rows:
        print('  '.join(str(x).ljust(w[i]) for i, x in enumerate(r[:4])) + '  ' + r[4])
    for f in fails:
        if not any(f.startswith(r[0] + ':') for r in rows):
            print('FAIL', f)
    report = dict(status='FAIL' if fails else 'PASS', profile=kit.profile_name, fails=fails, measured=m)
    (kit.work / 'check-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=1) + '\n')
    rel = (kit.work / 'check-report.json').relative_to(kit.root)
    print(f"CHECK {report['status']}: {len(rows)} assets, {len(m['scenes'])} build scenes, {len(fails)} problem(s). report: {rel}")
    return 0 if not fails else 1


# ---------------- capture / migrate ----------------
def capture(kit, tag):
    bg = (kit.data['assets'].get('capture') or {}).get('background') or [0.5, 0.5, 0.5]
    r = U.run(kit, 'Capture', dict(tag=tag, background=bg, hooks=hook_data(kit)))
    for s in r['shots']:
        print('SHOT', Path(s).relative_to(kit.root) if Path(s).is_absolute() else s)


def migrate(kit, name):
    r = U.run(kit, 'Move', dict(moves=kit.cat['migrations'][name]))
    for x in r['moves']:
        print(x['state'], x['from'], '->', x['to'])

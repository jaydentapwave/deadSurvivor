"""3dkit asset pipeline stages: spec, list, palette, prep (texture stage), unity (import + material + prefab + LOD +
project register), check (measure in Unity, judge here), capture, migrate.

Which texture stage an asset gets is decided by the profile: standards[kind].color
  palette_texture -> palette (kitlib/texture.py quantize)      texture -> passthrough      palette_uv -> not a tool stage
Ported from an earlier project (Tools/Art/assets.py, 050); the judge rules are the same, with the project values (paths,
shader, naming, trial folders, allowed shaders) read from the config and catalog instead of constants.
"""
import json
import re
import shutil
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


def superseded_adopted(cat):
    """(old, new) pairs where a version and the version it replaced are both adopted (W2: pot + pot_r2).
    A version replaces what its catalog 'replaces' lists, and by name: X_rN replaces X and X_r2 .. X_r(N-1)."""
    on = {a['id'] for a in adopted(cat)}
    pairs = []
    for a in adopted(cat):
        old = set(a.get('replaces') or [])
        m = re.match(r'^(.+)_r(\d+)$', a['id'])
        if m:
            old |= {m[1]} | {f'{m[1]}_r{k}' for k in range(2, int(m[2]))}
        pairs += [(o, a['id']) for o in sorted(old & on)]
    return pairs


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
    """maps this asset uses: the standard's required maps, plus each optional map (standard `maps_optional`) the asset's
    source.maps provides. An asset with texture_maps lists its maps itself."""
    if a.get('texture_maps'):
        return a['texture_maps']
    s = kit.standard(a['kind'])
    given = (a.get('source') or {}).get('maps') or {}
    return (s.get('maps') or ['albedo']) + [m for m in s.get('maps_optional') or [] if m in given]


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
            if a.get('color_fit'):    # asset 'color_fit': <name in catalog color_targets> = list of regions dark -> bright {name, hex, l_std}
                regs = (cat.get('color_targets') or {}).get(a['color_fit'])
                if not regs:
                    raise SystemExit(f"UNKNOWN_COLOR_TARGET:{a['id']}:{a['color_fit']} (catalog color_targets)")
                shutil.copyfile(d / 'albedo.png', d / 'albedo_prefit.png')
                split = geometry_split(kit, a, d, len(regs)) if a.get('color_fit_split') else None
                rec['color_fit'] = dict(target=a['color_fit'], **T.color_fit(d / 'albedo_prefit.png', d / 'albedo.png', regs, split=split))
                rec['maps']['albedo']['out_sha256'] = T.sha(d / 'albedo.png')
                line += ' colorfit=' + ' '.join(f"{r['name']}:empty" if r.get('empty') else f"{r['name']}:{r['before']}->{r['after']}(dE{r['delta_e_to_target']})" for r in rec['color_fit']['regions'])
                if a.get('smoothness_fit'):   # asset 'smoothness_fit': true -> each colour region's smoothness map mean moves to its 'smoothness' (art standard 3.5)
                    if 'smoothness' not in rec['maps'] or any('smoothness' not in r for r in regs):
                        raise SystemExit(f"SMOOTHNESS_FIT_NEEDS_MAP_AND_VALUES:{a['id']} (source.maps.smoothness + color_targets.{a['color_fit']}[].smoothness)")
                    if split:
                        wts, rog = split['weights'], split['region']
                    else:
                        wts, rog = T.fit_regions(T.load_rgb(d / 'albedo_prefit.png'), len(regs))[1], list(range(len(regs)))
                    rec['smoothness_fit'] = T.smoothness_fit(d / 'smoothness.png', regs, wts, rog)
                    rec['maps']['smoothness']['out_sha256'] = rec['smoothness_fit']['out_sha256']
                    line += f" smoothness={rec['smoothness_fit']['mean_before']}->{rec['smoothness_fit']['mean_after']}"
                    if rec['smoothness_fit']['warn']:
                        line += f" WARN {rec['smoothness_fit']['warn']}"
        rec.update(method=stage, profile=kit.profile_name)
        (d / 'prep.json').write_text(json.dumps(rec, ensure_ascii=False, indent=1) + '\n')
        print(f"PREP {a['id']}: {line}")


def geometry_split(kit, a, d, n_regions):
    """asset 'color_fit_split': {"by": "height", "top_band": <m>} -> colour regions placed by where each texel sits on the
    module (Blender reads the UVs of source.model; height = fit height + bury). The catalog gives top_band (art standard 3.2)."""
    sp = a['color_fit_split']
    if sp.get('by') != 'height' or 'top_band' not in sp or not a.get('fit'):
        raise SystemExit(f"BAD_COLOR_FIT_SPLIT:{a['id']} (needs a terrain 'fit' and {{'by': 'height', 'top_band': m}})")
    from . import blender as B
    B.run(kit, 'uv_geometry', [kit.abs(a['source']['model']), (d / 'uvgeo.npz').resolve()])
    P, N = T.uv_geometry_maps(d / 'uvgeo.npz', T.load_rgb(d / 'albedo.png').shape[0])
    height = float(a['fit']['height']) + float(a['fit'].get('bury', 0.05))
    return T.geometry_split(P, N, n_regions, height, float(sp['top_band']))


# ---------------- Unity build ----------------
def prefab_size(a):
    """(height m, anchor) the prefab's model is scaled to: a terrain module (catalog 'fit') keeps its real size - fit height + bury,
    top face at y = 0; an asset with a catalog 'height' gets that height with the feet at 0; the default is 1 with the feet at 0"""
    f = a.get('fit')
    if f:
        return float(f['height']) + float(f.get('bury', 0.05)), 'top'
    return float(a.get('height') or 1), 'feet'


ANIM_PARAMETER = 'Speed'     # float on the generated AnimatorController: idle -> walk above ANIM_THRESHOLD, walk -> idle below
ANIM_THRESHOLD = 0.1


def anim_of(a, prefab_dir, name):
    """catalog 'anim': {"clips": ["idle", "walk"], "default": "idle", "loop": [...]} -> the Unity request's animation block, else None
    (a static asset imports exactly as before: animation off, no Animator). Clip names are the plain names; the FBX clip
    'Armature|Armature|walk' is found by its last '|' part. loop defaults to every clip. Speed transitions only when idle and walk
    are both there."""
    an = a.get('anim')
    if not an:
        return None
    clips = list(an.get('clips') or [])
    if not clips or len(set(clips)) != len(clips):
        raise SystemExit(f"BAD_ANIM_CLIPS:{a['id']} (give 'anim': {{'clips': [unique plain names], 'default': one of them}})")
    default = an.get('default', clips[0])
    if default not in clips:
        raise SystemExit(f"BAD_ANIM_DEFAULT:{a['id']}:{default}")
    loop = list(an.get('loop', clips))
    if any(c not in clips for c in loop):
        raise SystemExit(f"BAD_ANIM_LOOP:{a['id']}")
    move = 'idle' in clips and 'walk' in clips
    out = dict(clips=clips, default=default, loop=loop, controller=f'{prefab_dir}/{name}.controller',
               parameter=ANIM_PARAMETER if move else None, threshold=ANIM_THRESHOLD if move else None)
    # 'overlay': {"clip": "attack", "trigger": "Attack", "mask_root": "Spine"} = a second layer playing that clip on one bone subtree
    # 'overlays': [...] = the same, one layer each, for a creature with several one-shot clips (attack and hit)
    def checked(ov):
        if ov.get('clip') not in clips or ov['clip'] == default or not ov.get('trigger') or not ov.get('mask_root'):
            raise SystemExit(f"BAD_ANIM_OVERLAY:{a['id']}")
        return dict(clip=ov['clip'], trigger=ov['trigger'], mask_root=ov['mask_root'])
    if an.get('overlay'):
        out['overlay'] = checked(an['overlay'])
    if an.get('overlays'):
        out['overlays'] = [checked(ov) for ov in an['overlays']]
        used = [ov['clip'] for ov in out['overlays']] + ([out['overlay']['clip']] if 'overlay' in out else [])
        if len(set(used)) != len(used):
            raise SystemExit(f"BAD_ANIM_OVERLAY:{a['id']} (one clip in two layers)")
    return out


def part_materials(kit, a, by):
    """catalog 'parts': {"Hat": "hat_s5", "Prop": "shovel_s5"} (renderer name from attach_props -> catalog id) -> renderer name ->
    that asset's material path. Without it every renderer of the model gets the asset's own material (as before)."""
    out = {}
    for label, pid in (a.get('parts') or {}).items():
        b = by.get(pid)
        if not b or b['method'] != 'build':
            raise SystemExit(f"BAD_PART:{a['id']}:{label}:{pid}")
        out[label] = f"{kit.apath(b['folder'])}/{naming(kit, b['kind'])['material'].format(name=name_of(b))}.mat"
    return out


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
        th, anchor = prefab_size(a)
        preset = prof['material']['presets'][s['preset']] if s.get('preset') else {}
        items.append(dict(id=a['id'], kind=a['kind'], name=n, folder=f, textures=tex,
                          material=f"{f}/{nm['material'].format(name=n)}.mat", preset=preset,
                          model_src=str(kit.abs(model)) if model else None, model_dst=f"{f}/{n}.fbx" if model else None,
                          prefab=f"{f}/{nm['prefab'].format(name=n)}.prefab" if model else None,
                          yaw=float(a['source'].get('yaw', 0)), shadow=s['shadow'], target_height=th, anchor=anchor,
                          mesh_lod=bool(s.get('lod')) and prof['lod']['method'] == 'mesh_lod', project=a.get('project'),
                          animation=anim_of(a, f, n) if model else None, part_materials=part_materials(kit, a, by) if model else {}))
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
                              keyword_toggles=m.get('keyword_toggles') or {}, map_keywords=m.get('map_keywords') or {},
                              map_floats=m.get('map_floats') or {}),
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
                it['expect_height'], it['expect_anchor'] = prefab_size(a)
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
    # art a scene builder or effect script makes itself: registered as a folder, no catalog entry (and no asset standard)
    folders += tuple(kit.apath(x).rstrip('/') + '/' for x in proj.get('scene_generated_roots') or [])
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
        # attached parts (catalog 'parts') bring their own material each: allowed on top of the standard's count
        if pf and 'materials_max' in s and a['method'] != 'existing_rig' and pf.get('materials', 0) > s['materials_max'] + len(a.get('parts') or {}):
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
                        or not t.get('mip') or t.get('alpha') != texture_import(kit, mp).get('alpha', 'None'):
                    bad.append(f'texture {mp} {t}')
            if it.get('model_child'):     # a prefab the tool built from a model: root (feet) -> Visual -> model of its target height
                want_h, anchor = prefab_size(a)     # from the catalog: the Unity result does not echo the request's expect_height
                want_feet = -want_h if anchor == 'top' else 0
                # an animated model has an approved size its walk was tuned to (B27: a clip edit shrank the hero 1.5%)
                tol = (.005 if a.get('anim') else .02) * max(want_h, 1)
                if abs(pf.get('height', 0) - want_h) > tol or abs(pf.get('feet', 1) - want_feet) > tol or pf.get('centre', 1) > .02:
                    bad.append(f"prefab not normalised h={pf.get('height')} (want {want_h:.3f}) feet={pf.get('feet')} (want {want_feet:.3f}) c={pf.get('centre')}")
                if s['shadow'] == 'blob' and pf.get('shadows') != ['Off']:
                    bad.append(f"shadow {pf.get('shadows')}")
                if any(p.startswith(trial) for p in pf.get('materialPaths', [])):
                    bad.append('prefab material in a trial folder')
        if a['method'] == 'existing_rig' and any(p.startswith(trial) for p in pf.get('materialPaths', [])):
            bad.append('default material in a trial folder: ' + ','.join(pf['materialPaths']))
        alb = (it.get('textures') or {}).get('albedo') or {}
        rows.append((a['id'], a['kind'], '-' if tris is None else f'{tris:,}', alb.get('android', '-'), 'FAIL: ' + '; '.join(bad) if bad else 'PASS'))
        fails += [f"{a['id']}: {b}" for b in bad]
    fails += [f'SUPERSEDED_STILL_ADOPTED {o} (replaced by {n})' for o, n in superseded_adopted(cat)]
    fails += [str(p) for p in m.get('project_problems') or []]
    # the checks below only mean something if they look at the real game (B37, B38)
    game_scene = ((kit.data.get('harness') or {}).get('play') or {}).get('scene')
    if game_scene and game_scene not in [sc['scene'] for sc in m['scenes']]:
        fails.append(f'GAME_SCENE_NOT_IN_BUILD {game_scene}')
    built = [a for a in adopted(cat) if a['method'] == 'build']
    if built and not any(kit.apath(a['folder']).startswith(kit.art_root + '/') for a in built):
        fails.append(f'ART_ROOT_HAS_NO_ASSETS {kit.art_root}')
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
    # B44: the report says which run and which tree it describes, so a gate never reads an older run's report as this one's
    import os
    import time
    import uuid
    from . import evidence as E
    run_id = os.environ.get('KIT_CHECK_RUN') or uuid.uuid4().hex
    fp = E.check_fingerprint(kit.root, kit.data)
    started = time.strftime('%F %T')
    m = U.run(kit, 'Check', check_request(kit))
    rows, fails = judge(kit, m)
    w = [max(len(str(r[i])) for r in rows) for i in range(4)] if rows else [0] * 4   # no adopted asset: nothing to list, still judge the scenes
    for r in rows:
        print('  '.join(str(x).ljust(w[i]) for i, x in enumerate(r[:4])) + '  ' + r[4])
    for f in fails:
        if not any(f.startswith(r[0] + ':') for r in rows):
            print('FAIL', f)
    report = dict(status='FAIL' if fails else 'PASS', run_id=run_id, fingerprint=fp, started=started, finished=time.strftime('%F %T'),
                  profile=kit.profile_name, fails=fails, measured=m)
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

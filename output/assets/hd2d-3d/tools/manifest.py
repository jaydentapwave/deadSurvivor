# manifest.json: 에셋 id → 파일·삼각형·크기·팔레트·판독 결과. Unity 넣기 단계가 이것을 읽는다.
# /usr/bin/python3 tools/manifest.py <judge 결과 json ...>   (작업 폴더 = hd2d-3d, 뒤에 준 판독 결과가 앞을 덮는다)
import sys, json, glob, os
BASE = 'output/assets/hd2d-3d'
USE = {  # 지금 배치(field_layout.json)의 그룹/그림 → 대신할 3D
    'tree_v1': 'tree,rim / tree_02', 'tree_v2': 'tree,rim / tree_02', 'tree_v3': 'tree,rim / tree_02',
    'bush_v1': 'bush / bush_01', 'bush_v2': 'bush / bush_02', 'boulder_v1': 'boulder / boulder_01', 'boulder_v2': 'boulder / boulder_01',
    'stone_v1': 'stone,shore / rock_01', 'stone_v2': 'stone,shore / rock_02', 'wall_a': 'wall_0~8 / rock_01·rock_02 (토막 1.0m)',
    'wall_b': 'wall_0~8 / rock_01·rock_02 (토막 1.0m)', 'grass_v1': 'flowers / grass_tuft_01', 'grass_v2': 'flowers / grass_tuft_02',
    'flower_white': 'flowers / flower_01', 'flower_yellow': 'flowers / flower_02', 'flower_purple': 'flowers / flower_03'}
BUDGET = {'tree': 600, 'bush': 300, 'boulder': 300, 'stone': 120, 'wall': 200, 'grass': 40, 'flower': 40}
judge = {}
for p in sys.argv[1:]:
    for name, r in json.load(open(p)).items():
        k = name.replace('hd3d_', '')
        rd = {}
        for reader, a in r['readers'].items():
            rd[reader] = {'scores': a.get('scores'), 'major_defects': a.get('major_defects'), 'defects': a.get('defects')}
        judge[k] = {'source': p, 'image_sha256': r['sha256'], 'readers': rd}
out = {'schema': 'hd2d-3d-manifest/1', 'units': '1 unit = 1 m, pivot = ground centre, +Y up (FBX axis_forward -Z, up Y, FBX_SCALE_ALL)',
       'texel_density': '48 texel/m (UV built at 48/m, Closest filter)', 'assets': {}}
for k in USE:
    m = json.load(open(f'{k}/mesh.json')); pal = json.load(open(f'{k}/palette.json'))
    b = m['bounds']; size = [round(b[1][i] - b[0][i], 3) for i in range(3)]
    fam = k.split('_')[0]
    j = judge.get(k)
    ok = None
    if j:
        ok = all(a['scores'] and a['scores']['style'] is not None and a['scores']['style'] >= 3 for a in j['readers'].values())
    out['assets']['hd2d_' + k] = {
        'replaces': USE[k], 'fbx': f'{BASE}/{k}/model.fbx', 'glb': f'{BASE}/{k}/model.glb', 'albedo': f'{BASE}/{k}/albedo.png',
        'albedo_size': pal['tex'], 'tris': len(m['tris']), 'tri_budget': BUDGET[fam],
        'size_m': {'x_width': size[0], 'depth': size[1], 'height': size[2]},
        'palette': pal['palette'], 'texels': pal['texels'],
        'smooth_parts': ['leaf'] if fam in ('tree', 'bush') else (['rock (auto-smooth 48°)'] if fam in ('boulder', 'stone') else []),
        'judge': j, 'judge_style_both_ge3': ok}
json.dump(out, open('manifest.json', 'w'), ensure_ascii=False, indent=1)
for k, a in out['assets'].items():
    print(k, a['tris'], a['size_m'], a['judge_style_both_ge3'])

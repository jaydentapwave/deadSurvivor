#!/usr/bin/env python3
"""045: measure in Play (windowed editor) whether heroes face their movement: body yaw from the hand bones (probe
HeroViewModels |body=) vs the movement yaw from the party centre. Not a gate; used to test the facing hypothesis
(humanoid playback faces the Animator object's +Z, so rotating the model object breaks facing) and to re-check miss S3-4.
Usage: KEYART045_SCENE=<scene> /usr/bin/python3 Tools/Proto/facing_check_045.py"""
import importlib.util, json, math, os, sys, time
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('k45', HERE / 'keyart_045.py'); k = importlib.util.module_from_spec(spec)
sys.argv = [sys.argv[0]]; spec.loader.exec_module(k)
res = dict(scene=k.SCENE); protected_before = k.fingerprints(k.PROTECTED)
try:
    k.report['open_attempted'] = True
    k.call('OpenScene', k.SCENE)
    k.cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', 'true'], 60); k.report['run_in_background_override'] = True
    k.call('EnterPlay'); k.wait(lambda s: s.get('playing') and s.get('state') == 'lobby', 'enter_play', 60, retries=5)
    k.select_only(4); k.call('Click', k.START); k.wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'battle'); time.sleep(2.5)
    raw = k.move_sample(); (ax, ay), (bx, by) = json.loads(raw['move_centres'])
    move_yaw = math.degrees(math.atan2(bx - ax, -(by - ay))); res['move_yaw'] = round(move_yaw, 1)
    mb = k.parse_models(raw['move_b']); res['heroes'] = {}
    for h, v in mb.items():
        body = float(v.get('body', 'nan')); res['heroes'][h] = dict(body=round(body, 1), off=round(k._ang(body, move_yaw), 1), mesh=v.get('asset', '').split('/')[-1], yaw_visual=v.get('yaw'))
    res['all_within_35'] = all(x['off'] <= 35 for x in res['heroes'].values())
    res['diag'] = raw.get('move_diag')
except Exception as e:
    res['error'] = str(e)[:600]
finally:
    k.attempt('exit_play', lambda: k.call('ExitPlay'))
    k.attempt('wait_edit_mode', lambda: k.wait(lambda s: not s.get('playing'), 'exit_play', 60, retries=5) and 'EDIT_MODE')
    k.attempt('quality', lambda: k.call('RestoreQuality'))
    k.attempt('side_effect_dirty', lambda: k.call('ClearSideEffectDirty'))
    if k.report.get('run_in_background_override'):
        k.attempt('run_in_background', lambda: k.cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', 'false'], 60))
    k.attempt('scene_setup', lambda: k.call('Restore', retries=3))
    after = k.fingerprints(k.PROTECTED); res['protected_changed'] = [p for p in k.PROTECTED if protected_before[p] != after[p]]
    res['cleanup_errors'] = k.report.get('cleanup_errors')
    (k.OUT / 'facing.json').write_text(json.dumps(res, ensure_ascii=False, indent=1)); print(json.dumps(res, ensure_ascii=False))

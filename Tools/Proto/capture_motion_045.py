#!/usr/bin/env python3
"""045 stage 3 v2: hero showcase in Play (windowed editor): both squads (all six heroes), standing and running in four
directions, centre crops without the pause dialog, for the user's judgement and the independent visual review (D1-D3). Reuses the 045 runner's helpers (probe calls, cleanup). Not a verification gate.
Usage: KEYART045_SCENE=<scene> /usr/bin/python3 Tools/Proto/capture_motion_045.py
Output: output/keyart-target-045/r001/run-<id>/motion/ frames + motion-<dir>.gif (centre crop x3) + report.json.
The party is moved with the touch joystick (044 probe), right / up / left / down, ~12 frames each; protected files are
fingerprinted before/after like the runner."""
import importlib.util, json, os, shutil, sys, time
from pathlib import Path

HERE = Path(__file__).resolve().parent
os.environ.setdefault('KEYART045_SCENE', 'Assets/DeadSurvivor/Scenes/Proto045/Proto045_Main_s3b.unity')
spec = importlib.util.spec_from_file_location('k45', HERE / 'keyart_045.py'); k = importlib.util.module_from_spec(spec)
sys.argv = [sys.argv[0]]; spec.loader.exec_module(k)
from PIL import Image

MOT = k.OUT / 'motion'; MOT.mkdir()
k.report['stage'] = 'motion'; frames = {}
protected_before = k.fingerprints(k.PROTECTED)


def grab(label, i):
    rel = f'output/keyart045/{k.RUN}/motion-{label}-{i:02d}.png'
    k.cli(['command', 'capture', 'game_view', '--', '--source', 'screen', '--width', '1280', '--height', '720', '--save_path', rel], 120)
    dst = MOT / f'{label}-{i:02d}.png'; shutil.copy2(k.ROOT / 'Assets' / rel, dst); return dst


try:
    k.report['open_attempted'] = True
    k.call('OpenScene', k.SCENE)
    k.report['run_in_background_before'] = k.evaluate('return UnityEditor.PlayerSettings.runInBackground.ToString();')
    k.cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', 'true'], 60); k.report['run_in_background_override'] = True
    k.call('EnterPlay'); k.wait(lambda s: s.get('playing') and s.get('state') == 'lobby', 'enter_play', 60, retries=5)
    # both squads (B: bruno hans lena yuki, C: lena yuki bolt mira) so all six heroes are shown; no pause dialog
    for squad, picks in (('B', (0, 1, 2, 3)), ('C', (2, 3, 4, 5))):
        lobby = k.state()
        for i in list(lobby['selected']): k.call('Submit', 'ToggleSelection', i)
        for i in picks: k.call('Submit', 'ToggleSelection', i)
        k.wait(lambda s: sorted(s['selected']) == list(picks), 'select_' + squad)
        k.call('Click', k.START); k.wait(lambda s: s['state'] == 'battle' and s['wave'] == 1, 'battle_' + squad); time.sleep(2.5)
        k.report['screen_resized'] = True; k.call('Resize', 1280, 720, cls=k.SCREEN); time.sleep(1.0)
        frames[f'{squad}-idle'] = [grab(f'{squad}-idle', 0)]
        # the same standing squad at the other two aspect ratios (impl review v2 r002 #2), then back to 1280x720
        for w_, h_ in ((1560, 720), (1024, 768)):
            k.call('Resize', w_, h_, cls=k.SCREEN); time.sleep(1.0)
            rel = f'output/keyart045/{k.RUN}/motion-{squad}-idle-{w_}x{h_}.png'
            k.cli(['command', 'capture', 'game_view', '--', '--source', 'screen', '--width', str(w_), '--height', str(h_), '--save_path', rel], 120)
            dst = MOT / f'{squad}-idle-{w_}x{h_}.png'; shutil.copy2(k.ROOT / 'Assets' / rel, dst); frames[f'{squad}-idle-{w_}x{h_}'] = [dst]
        k.call('Resize', 1280, 720, cls=k.SCREEN); time.sleep(1.0)
        k.report['input_scope_begin'] = k.call('BeginInputScope', cls=k.TOUCH); k.report.pop('input_scope_end', None)
        rect = json.loads(k.call('MovementRect', cls=k.TOUCH).split('|')[0])
        # touch start at the lower-left of the movement area so the drawn joystick does not cover the party at the centre
        cx, cy = rect['x'] + rect['width'] * 0.15, rect['y'] + rect['height'] * 0.5   # left-middle: all four directions stay inside the area
        for label, (dx, dy) in (('right', (90, 0)), ('up', (0, 90)), ('left', (-90, 0)), ('down', (0, -90))):
            k.call('Touch', 'began', float(cx), float(cy), cls=k.TOUCH); time.sleep(0.15)
            k.call('Touch', 'moved', float(cx + dx), float(cy + dy), cls=k.TOUCH); time.sleep(0.6)
            moved = k.state(); frames[f'{squad}-{label}'] = [grab(f'{squad}-{label}', i) for i in range(8)]
            k.report.setdefault('moving_state', {})[f'{squad}-{label}'] = dict(joystickActive=moved['joystickActive'], center=moved['center'])
            k.call('Touch', 'ended', float(cx + dx), float(cy + dy), cls=k.TOUCH); time.sleep(0.6)
        k.call('RemoveTouch', cls=k.TOUCH); k.report['input_scope_end'] = k.call('EndInputScope', cls=k.TOUCH)
        st = k.state()['state']
        if st == 'prep':      # wave 1 (30 s) may end during the capture: start the next wave, then end the run
            k.call('Click', k.NEXT); k.wait(lambda s: s['state'] == 'battle', 'next_' + squad); st = 'battle'
        if st == 'battle': k.call('KillParty')   # the squad may already have fallen during the capture
        k.wait(lambda s: s['state'] == 'result', 'defeat_' + squad)
        k.call('Click', k.REFORM); k.wait(lambda s: s['state'] == 'lobby', 'lobby_' + squad)
    k.report['flow'] = 'PASS'
except Exception as e:
    k.report['error'] = str(e)[:1500]
finally:
    k.attempt('touch_device', lambda: k.call('RemoveTouch', cls=k.TOUCH))
    k.report['texture_sha256'] = {h: k.digest(f'Assets/DeadSurvivor/Scenes/Proto045/Art_s3b/hero045v2_{h}.png') for h in ('bruno', 'hans', 'lena', 'yuki', 'bolt', 'mira')} \
        if (k.ROOT / 'Assets/DeadSurvivor/Scenes/Proto045/Art_s3b').exists() else None
    k.report['scene_sha256'] = k.digest(k.SCENE)
    if k.report.get('input_scope_begin') and not k.report.get('input_scope_end'):
        k.attempt('input_scope', lambda: k.call('EndInputScope', cls=k.TOUCH))
    if k.report.get('screen_resized'): k.attempt('game_view_size', lambda: k.call('Restore', cls=k.SCREEN))
    k.attempt('exit_play', lambda: k.call('ExitPlay'))
    k.attempt('wait_edit_mode', lambda: k.wait(lambda s: not s.get('playing'), 'exit_play', 60, retries=5) and 'EDIT_MODE')
    k.attempt('quality', lambda: k.call('RestoreQuality'))
    k.attempt('side_effect_dirty', lambda: k.call('ClearSideEffectDirty'))
    if k.report.get('run_in_background_override'):
        k.attempt('run_in_background', lambda: k.cli(['command', 'editor.playmode', 'set_run_in_background', '--', '--enable', 'false'], 60))
    k.attempt('scene_setup', lambda: k.call('Restore', retries=3))
    after = k.fingerprints(k.PROTECTED)
    k.report['protected_changed'] = [p for p in k.PROTECTED if protected_before[p] != after[p]]
    # centre crop (party is at the screen centre; camera follows) x3 nearest -> GIF per direction
    gifs = []; sheet_rows = []
    for label, fs in frames.items():
        ims = []
        for f in fs:
            im = Image.open(f).convert('RGB'); w, h = im.size
            c = im.crop((w // 2 - 150, h // 2 - 110, w // 2 + 150, h // 2 + 110)); ims.append(c.resize((c.width * 3, c.height * 3), Image.NEAREST))
        if len(ims) > 1:
            g = MOT / f'motion-{label}.gif'; ims[0].save(g, save_all=True, append_images=ims[1:], duration=120, loop=0); gifs.append(g.name)
        sheet_rows.append((label, ims[0], ims[len(ims) // 2]))
    if sheet_rows:   # one 3x sheet: per sequence the first and the middle frame
        W3, H3 = sheet_rows[0][1].size; sh = Image.new('RGB', (W3 * 2, H3 * len(sheet_rows)))
        for r, (_, a, b) in enumerate(sheet_rows): sh.paste(a, (0, r * H3)); sh.paste(b, (W3, r * H3))
        sh.save(MOT / 'showcase-3x.png'); k.report['sheet_rows'] = [r[0] for r in sheet_rows]
    k.report['gifs'] = gifs; k.report['status'] = 'PASS' if k.report.get('flow') == 'PASS' and not k.report['protected_changed'] and not k.report.get('cleanup_errors') else 'FAIL'
    k.save(); print(json.dumps(dict(status=k.report['status'], error=k.report.get('error'), cleanup_errors=k.report.get('cleanup_errors'),
                                    protected_changed=k.report['protected_changed'], out=str(MOT.relative_to(k.ROOT)), gifs=gifs), ensure_ascii=False))

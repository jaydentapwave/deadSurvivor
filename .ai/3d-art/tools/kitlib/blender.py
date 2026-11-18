"""Run the kit's Blender post-process scripts (tools/blender/*.py) headless. Blender path: 3dkit.config.json "blender"."""
import subprocess
from pathlib import Path

from . import modfit

SCRIPTS = Path(__file__).resolve().parents[1] / 'blender'
OK_MARKS = {'fit_module': 'FIT_OK', 'fix_arm_swing': 'FIX_OK', 'attach_props': 'ATTACH_OK', 'convert_glb': 'CONVERT_OK', 'uv_geometry': 'UVGEO_OK', 'anim_fbxcheck': 'FBXCHECK_OK'}


def run(kit, script, args):
    exe = kit.data.get('blender')
    if not exe or not Path(exe).exists():
        raise SystemExit(f'NO_BLENDER (set "blender" in 3dkit.config.json): {exe}')
    r = subprocess.run([exe, '-b', '--factory-startup', '-P', str(SCRIPTS / f'{script}.py'), '--', *map(str, args)],
                       capture_output=True, text=True)
    out = r.stdout + r.stderr
    if r.returncode or OK_MARKS[script] not in out:
        raise SystemExit(f'BLENDER_{script.upper()}_FAILED:\n' + out[-1500:])
    return next(l for l in out.splitlines() if l.startswith(OK_MARKS[script]))


def fit(kit, rest):
    """assets.py fit <id> [--height H] [--bury B] [--corner] [--tol T] [--strict] [--width W] [--depth D] [--pre-yaw DEG] [--out DIR]"""
    from . import pipeline as P
    if not rest or rest[0].startswith('--'):
        raise SystemExit('GIVE_AN_ASSET_ID')
    a = P.by_id(kit.cat, [rest[0]])[0]
    f = dict(a.get('fit') or {})
    opt = lambda n: rest[rest.index(n) + 1] if n in rest else None
    height = opt('--height') or f.get('height')
    if height is None:
        raise SystemExit(f"NO_MODULE_HEIGHT:{a['id']} (give --height or a catalog 'fit': {{'height': ...}}; values are art standard 3.1/3.2)")
    bury = opt('--bury') or f.get('bury', 0.05)
    corner = '--corner' in rest or bool(f.get('corner'))
    tol = opt('--tol') or f.get('tol', modfit.WELD_TOL)
    width = opt('--width') or f.get('width', 1.0)
    depth = opt('--depth') or f.get('depth', 1.0)
    pre_yaw = opt('--pre-yaw') if opt('--pre-yaw') is not None else f.get('pre_yaw', 0)
    out = Path(opt('--out')) if opt('--out') else kit.work / a['id'] / 'fit'
    args = [kit.abs(a['source']['model']), out.resolve(), float(height), float(bury), '--tol', float(tol)] + (['--corner'] if corner else []) + (['--strict'] if '--strict' in rest else []) + ['--width', float(width), '--depth', float(depth)] + (['--pre-yaw', float(pre_yaw)] if float(pre_yaw) else [])
    run(kit, 'fit_module', args)
    import json
    r = json.loads((out / 'fit.json').read_text())
    print(f"FIT {a['id']}: {r['bounds_before']['min']}..{r['bounds_before']['max']} -> {r['bounds_after']['min']}..{r['bounds_after']['max']}, "
          f"welded {r['welded_vertices']} vertices on {','.join(r['welded_axes'])} -> {out}/model.fbx (point the catalog source.model there)")
    for w in r.get('warnings', []):
        print('WARN', w)


def passthrough(kit, script, rest, usage):
    if len(rest) < 2 or rest[0].startswith('--'):
        raise SystemExit(usage)
    print(run(kit, script, [Path(rest[0]).resolve(), Path(rest[1]).resolve(), *rest[2:]])[:300])

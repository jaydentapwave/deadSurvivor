"""3dkit: what a Play run proves it ran against. A fingerprint of the working tree under some paths: blob ids of tracked files
with changed and new files overlaid. Committing part of the tree does not change it (rule 2: keep evidence separate by kind)."""
import hashlib
import json
import subprocess
import time
from pathlib import Path


def _git(root, *a, stdin=None):
    return subprocess.run(['git', *a], cwd=root, capture_output=True, text=True, input=stdin, check=True).stdout


def fingerprint(root, paths, exclude=('/Editor/', '__pycache__')):
    keep = lambda p: not any(x in p for x in exclude)
    blobs = {}
    for line in _git(root, 'ls-files', '-s', '--', *paths).splitlines():
        meta, p = line.split('\t', 1)
        if keep(p):
            blobs[p] = meta.split()[1]
    changed = _git(root, 'diff', '--name-only', 'HEAD', '--', *paths).splitlines()
    new = _git(root, 'ls-files', '-o', '--exclude-standard', '--', *paths).splitlines()
    for p in changed + new:
        if not keep(p):
            continue
        if (Path(root) / p).is_file():
            blobs[p] = _git(root, 'hash-object', '--', p).strip()
        else:
            blobs.pop(p, None)
    return hashlib.sha1(json.dumps(sorted(blobs.items())).encode()).hexdigest()[:12]


KIT_TOOLS = Path(__file__).resolve().parents[1]


def local_packages(root):
    """project-relative folders of the Unity packages that live in this repository: embedded ones (Packages/<name>/package.json)
    and manifest dependencies written as file:<path> inside the project"""
    root = Path(root).resolve()
    out = set()
    pk = root / 'Packages'
    for pj in pk.glob('*/package.json'):
        out.add(str(pj.parent.relative_to(root)))
    try:
        deps = json.loads((pk / 'manifest.json').read_text()).get('dependencies') or {}
    except (OSError, ValueError):
        deps = {}
    for v in deps.values():
        if isinstance(v, str) and v.startswith('file:'):
            d = (pk / v[5:]).resolve()
            if d.is_dir() and (d == root or root in d.parents):
                out.add(str(d.relative_to(root)))
    return sorted(out)


def check_inputs(root, data):
    """what `assets.py check` reads: the art (harness.gate.art_paths), the catalog, the config, the kit itself (tools, profiles,
    the Unity package). A report made for the same fingerprint describes this tree (gate reuses it, B44)."""
    root = Path(root).resolve()
    a = data.get('assets') or {}
    paths = list(((data.get('harness') or {}).get('gate') or {}).get('art_paths') or [])
    paths += [x for x in (a.get('catalog'), '3dkit.config.json') if x]
    prof = root / a['profiles_dir'] if a.get('profiles_dir') else KIT_TOOLS.parent / 'profiles'
    for d in (KIT_TOOLS, prof):
        d = d.resolve()
        if d == root or root in d.parents:
            paths.append(str(d.relative_to(root)))
    return sorted(set(paths + local_packages(root)))


def check_fingerprint(root, data):
    return fingerprint(root, check_inputs(root, data), exclude=('__pycache__',))


def path(root):
    return Path(root) / 'output/hook-state/play-evidence.json'


def read(root):
    p = path(root)
    return json.loads(p.read_text()) if p.exists() else {}


def write(root, kind, **data):
    d = read(root)
    d[kind] = dict(at=time.strftime('%F %T'), **data)
    p = path(root)
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(d, ensure_ascii=False, indent=1))

"""3dkit settings: the project's 3dkit.config.json + one profile from 3dkit/profiles/ + the project's asset catalog.

Single source per value (docs: 3dkit/workflows/asset-standard.md):
- profile (kit)      style values: stage methods, per-kind standard numbers, material slots/presets, import settings,
                     check rules, Tripo model/params
- 3dkit.config.json  project values: which profile (assets.profile = the one switch), paths, shader role -> name,
                     capture background, Tripo CLI/state/key file
- catalog (project)  asset registry, budgets + approval text, palettes, project rules/naming/hook data, standards_override
The config is found from the current folder upward (or KIT3D_CONFIG). The profile can be overridden for one run with
--profile NAME without editing the file.
"""
import copy
import json
import os
from pathlib import Path

KIT = Path(__file__).resolve().parents[2]          # <project>/3dkit
PROFILES = KIT / 'profiles'
CONFIG_NAME = '3dkit.config.json'
CONFIG_SCHEMA = '3dkit-config/1'
PROFILE_SCHEMA = '3dkit-profile/1'
CATALOG_SCHEMA = '3dkit-asset-catalog/2'
DEFAULT_SHADERS = {'lit': 'Universal Render Pipeline/Lit'}   # kit defaults for shader roles a project did not map


def find_config(start=None):
    env = os.environ.get('KIT3D_CONFIG')
    if env:
        return Path(env).resolve()
    d = Path(start or Path.cwd()).resolve()
    for p in (d, *d.parents):
        if (p / CONFIG_NAME).is_file():
            return p / CONFIG_NAME
    raise SystemExit(f'NO_{CONFIG_NAME} (run from the project root or set KIT3D_CONFIG)')


def load_profile(name):
    p = PROFILES / f'{name}.json'
    if not p.is_file():
        raise SystemExit(f'UNKNOWN_PROFILE:{name} (have: {", ".join(sorted(x.stem for x in PROFILES.glob("*.json")))})')
    prof = json.loads(p.read_text())
    if prof.get('schema') != PROFILE_SCHEMA or prof.get('name') != name:
        raise SystemExit(f'BAD_PROFILE:{name}')
    return prof


class Kit:
    """resolved settings for one run"""

    def __init__(self, config_path=None, profile=None, catalog=None):
        self.config_path = Path(config_path or find_config()).resolve()
        self.root = self.config_path.parent
        self.data = json.loads(self.config_path.read_text())
        if self.data.get('schema') != CONFIG_SCHEMA:
            raise SystemExit('BAD_CONFIG_SCHEMA:' + str(self.data.get('schema')))
        a = self.data['assets']
        self.profile_name = profile or a['profile']
        self.profile = load_profile(self.profile_name)
        self.content_root = a['content_root'].rstrip('/')
        self.art_root = a['art_root'].rstrip('/')
        self.work = self.root / a['work_root']
        self.catalog_path = self.root / a['catalog']
        self.cat = catalog if catalog is not None else json.loads(self.catalog_path.read_text())
        if self.cat.get('schema') != CATALOG_SCHEMA:
            raise SystemExit('BAD_CATALOG_SCHEMA:' + str(self.cat.get('schema')))

    # ---- paths ----
    def apath(self, p):
        """catalog paths: 'Assets/', 'Packages/' and 'output/' as written, anything else under assets.content_root"""
        return p if p.startswith(('Assets/', 'Packages/', 'output/')) else self.content_root + '/' + p

    def abs(self, p):
        return self.root / self.apath(p)

    # ---- profile values ----
    def standard(self, kind):
        """profile standards[kind] with the catalog's standards_override[kind] keys on top"""
        s = self.profile['standards'].get(kind)
        if s is None:
            raise SystemExit(f'KIND_NOT_IN_PROFILE:{kind} ({self.profile_name})')
        s = copy.deepcopy(s)
        s.update((self.cat.get('standards_override') or {}).get(kind) or {})
        return s

    def standard_source(self, kind):
        return 'catalog standards_override' if kind in (self.cat.get('standards_override') or {}) else f'profile {self.profile_name}'

    def shader(self, role):
        name = (self.data['assets'].get('shaders') or {}).get(role) or DEFAULT_SHADERS.get(role)
        if not name:
            raise SystemExit(f'NO_SHADER_FOR_ROLE:{role} (map it in {CONFIG_NAME} assets.shaders)')
        return name

    def material_shader(self):
        return self.shader(self.profile['material']['shader'])

    def tripo(self):
        return self.data.get('tripo') or {}

"""3dkit settings: the project's 3dkit.config.json + one profile + the project's asset catalog.

Single source per value (docs: the 3d-art pack rule 3d-asset-standard.md):
- profile (kit)      style values: stage methods, per-kind standard numbers, material slots/presets, import settings,
                     check rules, Tripo model/params
- 3dkit.config.json  project values: which profile (assets.profile = the one switch), paths, shader role -> name,
                     capture background, Tripo CLI/state/key file
- catalog (project)  asset registry, budgets + approval text, palettes, project rules/naming/hook data, standards_override
The config is found from the current folder upward (or KIT3D_CONFIG). The profile can be overridden for one run with
--profile NAME without editing the file. Config and catalog are checked against tools/schemas/*.schema.json on load, and
every project path in the config must stay inside the project (absolute tool paths such as tripo.cli and blender may not).

Where the kit lives does not matter: the tools find the project from the config, and the profiles next to the tools
(<kit home>/profiles; installed: .ai/3d-art/profiles, kit development: the pack's payload) unless assets.profiles_dir says
otherwise."""
import copy
import json
import os
from pathlib import Path

from . import schema as S
from .errors import fail

KIT_TOOLS = Path(__file__).resolve().parents[1]     # the tools folder (kitlib's parent)
KIT_HOME = KIT_TOOLS.parent                          # installed: <project>/.ai/3d-art
PROFILES = KIT_HOME / 'profiles'
CONFIG_NAME = '3dkit.config.json'
CONFIG_SCHEMA = '3dkit-config/1'
PROFILE_SCHEMA = '3dkit-profile/1'
CATALOG_SCHEMA = '3dkit-asset-catalog/2'
DEFAULT_SHADERS = {'lit': 'Universal Render Pipeline/Lit'}   # kit defaults for shader roles a project did not map
# project paths: (section, key). They must resolve inside the project root.
PROJECT_PATHS = (('assets', 'catalog'), ('assets', 'content_root'), ('assets', 'art_root'), ('assets', 'work_root'), ('assets', 'spec'),
                 ('assets', 'spec_approval'), ('assets', 'profiles_dir'), ('tripo', 'home'), ('tripo', 'key_env_file'), ('tripo', 'new_work_root'))


def find_config(start=None):
    p = project_config(start)
    if p is None:
        fail(f'NO_{CONFIG_NAME}', str(Path(start or Path.cwd())), 'run from the project root (the folder with 3dkit.config.json) or set KIT3D_CONFIG')
    return p


def project_config(start=None):
    """the config path, or None. KIT3D_CONFIG wins; else the first folder upward from start (default: cwd) that has one."""
    env = os.environ.get('KIT3D_CONFIG')
    if env:
        return Path(env).resolve()
    d = Path(start or Path.cwd()).resolve()
    for p in (d, *d.parents):
        if (p / CONFIG_NAME).is_file():
            return p / CONFIG_NAME
    return None


def project_root(start=None):
    p = project_config(start)
    return p.parent if p else None


def inside(root, rel):
    """True if the project path rel stays inside root (no absolute path, no .. escape)"""
    if os.path.isabs(rel):
        return False
    r = Path(root).resolve()
    return (r / rel).resolve() == r or r in (r / rel).resolve().parents


def config_problems(data, root=None):
    errs = S.check(data, 'config')
    if root is not None:
        for sec, key in PROJECT_PATHS:
            v = (data.get(sec) or {}).get(key)
            if isinstance(v, str) and not inside(root, v):
                errs.append(f'$.{sec}.{key}: {v!r} is outside the project (use a path relative to the project root)')
    return errs


def load_profile(name, profiles=None):
    d = Path(profiles or PROFILES)
    p = d / f'{name}.json'
    if not p.is_file():
        fail('UNKNOWN_PROFILE', name, f'set assets.profile to one of: {", ".join(sorted(x.stem for x in d.glob("*.json"))) or "(none in " + str(d) + ")"}')
    prof = json.loads(p.read_text())
    if prof.get('schema') != PROFILE_SCHEMA or prof.get('name') != name:
        fail('BAD_PROFILE', name, f'{p} must have schema {PROFILE_SCHEMA} and name {name}')
    return prof


class Kit:
    """resolved settings for one run"""

    def __init__(self, config_path=None, profile=None, catalog=None, data=None):
        """catalog/data: in-memory catalog and config (replay.py puts a fault into copies); they pass the same checks"""
        self.config_path = Path(config_path or find_config()).resolve()
        self.root = self.config_path.parent
        self.data = data if data is not None else json.loads(self.config_path.read_text())
        if self.data.get('schema') != CONFIG_SCHEMA:
            fail('BAD_CONFIG_SCHEMA', str(self.data.get('schema')), f'{CONFIG_NAME} must start with "schema": "{CONFIG_SCHEMA}"')
        if 'assets' not in self.data:
            fail('CONFIG_INVALID', 'no assets section', f'add assets.profile/catalog/content_root/art_root/work_root to {CONFIG_NAME} (see the 3d-art setup template)')
        bad = config_problems(self.data, self.root)
        if bad:
            fail('CONFIG_INVALID', '; '.join(bad[:4]), f'fix {CONFIG_NAME} (schema: tools/schemas/config.schema.json)')
        a = self.data['assets']
        self.profiles = self.root / a['profiles_dir'] if a.get('profiles_dir') else PROFILES
        self.profile_name = profile or a['profile']
        self.profile = load_profile(self.profile_name, self.profiles)
        self.content_root = a['content_root'].rstrip('/')
        self.art_root = a['art_root'].rstrip('/')
        self.work = self.root / a['work_root']
        self.catalog_path = self.root / a['catalog']
        self.cat = catalog if catalog is not None else json.loads(self.catalog_path.read_text())
        if self.cat.get('schema') != CATALOG_SCHEMA:
            fail('BAD_CATALOG_SCHEMA', str(self.cat.get('schema')), f'the catalog must start with "schema": "{CATALOG_SCHEMA}"')
        bad = S.check(self.cat, 'catalog')
        if bad:
            fail('CATALOG_INVALID', '; '.join(bad[:4]), f'fix {a["catalog"]} (schema: tools/schemas/catalog.schema.json)')

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
            fail('KIND_NOT_IN_PROFILE', f'{kind} ({self.profile_name})', f'use a kind the profile defines ({", ".join(sorted(self.profile["standards"]))}) or add it to the profile')
        s = copy.deepcopy(s)
        s.update((self.cat.get('standards_override') or {}).get(kind) or {})
        return s

    def standard_source(self, kind):
        return 'catalog standards_override' if kind in (self.cat.get('standards_override') or {}) else f'profile {self.profile_name}'

    def shader(self, role):
        name = (self.data['assets'].get('shaders') or {}).get(role) or DEFAULT_SHADERS.get(role)
        if not name:
            fail('NO_SHADER_FOR_ROLE', role, f'map it in {CONFIG_NAME} assets.shaders')
        return name

    def material_shader(self):
        return self.shader(self.profile['material']['shader'])

    def tripo(self):
        return self.data.get('tripo') or {}

    def python(self):
        """the interpreter the tools run with (numpy + Pillow). harness.python, else the one running now"""
        import sys
        return (self.data.get('harness') or {}).get('python') or sys.executable

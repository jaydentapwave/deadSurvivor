"""Canonical personal project paths and one explicitly approved legacy entry point."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APPROVED_PROJECT = Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor')
LEGACY_ROOT = Path('/Users/hyeongwoo/Documents/ChatGPT/데드 서바이버')
DATA_RELATIVE = 'Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json'


def canonical_evidence_path(value):
    """Translate only the verified former root; never resolve the remaining suffix."""
    path = Path(value)
    if ROOT != APPROVED_PROJECT or not path.is_absolute() or not path.is_relative_to(LEGACY_ROOT):
        return path
    relative = path.relative_to(LEGACY_ROOT)
    if '..' in relative.parts:
        raise ValueError('LEGACY_PATH_ESCAPE')
    trusted = LEGACY_ROOT.is_symlink() and LEGACY_ROOT.resolve(strict=True) == ROOT
    if not trusted:
        gitfile = LEGACY_ROOT / '.git'
        trusted = (gitfile.is_file() and not gitfile.is_symlink()
                   and gitfile.read_text() == 'gitdir: ' + str(ROOT / '.git') + '\n'
                   and all((LEGACY_ROOT / name).is_symlink()
                           and (LEGACY_ROOT / name).resolve(strict=True) == ROOT / name
                           for name in ('Tools', 'output')))
    if not trusted:
        raise ValueError('LEGACY_ROOT_CONFLICT')
    return ROOT / relative


def require_project(root=ROOT, project=APPROVED_PROJECT):
    """The active code/data is inside the approved working project, not a mirror."""
    root, project = Path(root).resolve(), Path(project).resolve()
    if root != project or project != APPROVED_PROJECT:
        raise ValueError('PROJECT_ROOT_MISMATCH')
    required = [project / DATA_RELATIVE, project / 'ProjectSettings/ProjectVersion.txt',
                project / 'Packages/manifest.json', project / 'Assets/DeadSurvivor/Runtime/DSGame.cs',
                project / 'Assets/DeadSurvivor/Editor/DSVerification.cs']
    for path in required:
        if not path.is_file():
            raise ValueError('PROJECT_SOURCE_MISSING')
        for parent in (path, *path.parents):
            if parent == root:
                break
            if parent.is_symlink():
                raise ValueError('PROJECT_SOURCE_SYMLINK')
    return project

"""Strict local input primitives; no Unity calls and no credential access."""
import hashlib
import json
import math
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from project_paths import canonical_evidence_path, require_project



class MapInputError(ValueError):
    def __init__(self, code, message, status='FAIL'):
        super().__init__(message)
        self.code, self.status = code, status


def canonical_hash(value):
    data = json.dumps(value, ensure_ascii=False, sort_keys=True,
                      separators=(',', ':'), allow_nan=False).encode('utf-8')
    return hashlib.sha256(data).hexdigest()


def strict_loads(text):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise MapInputError('DUPLICATE_KEY', 'Duplicate JSON key: ' + key)
            result[key] = value
        return result

    def reject(value):
        raise MapInputError('NONFINITE_NUMBER', 'Non-finite JSON number: ' + value)

    def finite(value):
        if isinstance(value, float) and not math.isfinite(value):
            raise MapInputError('NONFINITE_NUMBER', 'Non-finite parsed number')
        if isinstance(value, dict):
            for item in value.values():
                finite(item)
        elif isinstance(value, list):
            for item in value:
                finite(item)

    try:
        value = json.loads(text, object_pairs_hook=pairs, parse_constant=reject)
        finite(value)
        return value
    except MapInputError:
        raise
    except (ValueError, RecursionError) as error:
        raise MapInputError('INVALID_JSON', str(error)) from error


def reject_symlinks(path):
    path = Path(path).absolute()
    for parent in (*reversed(path.parents), path):
        if parent.is_symlink():
            raise MapInputError('SYMLINK', 'Symbolic link is not an input: ' + str(parent))
    return path


def safe_relative_path(root, relative, must_exist=True):
    if (not isinstance(relative, str) or not relative or '\\' in relative
            or ':' in relative or Path(relative).is_absolute()
            or any(p in ('', '.', '..') for p in relative.split('/'))):
        raise MapInputError('INVALID_PATH', 'Expected a contained relative file path')
    root = reject_symlinks(root)
    if not root.is_dir():
        raise MapInputError('MISSING_ROOT', 'Evidence root does not exist')
    path = reject_symlinks(root / relative)
    if not path.resolve().is_relative_to(root.resolve()):
        raise MapInputError('PATH_ESCAPE', 'Path leaves the evidence root')
    if must_exist and not path.is_file():
        raise MapInputError('MISSING_FILE', 'Evidence file does not exist: ' + relative,
                            'BLOCKED')
    return path


def read_input_bytes(path, max_bytes=1024 * 1024):
    path = reject_symlinks(path)
    try:
        with path.open('rb') as stream:
            raw = stream.read(max_bytes + 1)
        if len(raw) > max_bytes:
            raise MapInputError('INPUT_LIMIT', 'JSON exceeds input byte limit', 'BLOCKED')
        return raw
    except MapInputError:
        raise
    except (OSError, UnicodeError) as error:
        raise MapInputError('INPUT_UNAVAILABLE', str(error), 'BLOCKED') from error


def load_json(path, max_bytes=1024 * 1024):
    raw = read_input_bytes(canonical_evidence_path(path), max_bytes)
    try:
        return strict_loads(raw.decode('utf-8'))
    except UnicodeError as error:
        raise MapInputError('INVALID_UTF8', str(error)) from error

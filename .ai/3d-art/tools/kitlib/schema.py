"""3dkit: validate a project file against a JSON Schema in tools/schemas/ (a small subset, standard library only, so the
tools keep running on a bare python3). Supported: type, enum, required, properties, additionalProperties (bool), items,
minimum. Unknown keys of a schema are ignored, unknown keys of the data are allowed unless additionalProperties is false."""
import json
from pathlib import Path

SCHEMAS = Path(__file__).resolve().parents[1] / 'schemas'
TYPES = {'string': str, 'integer': int, 'number': (int, float), 'boolean': bool, 'object': dict, 'array': list, 'null': type(None)}


def _is(value, name):
    if name in ('integer', 'number') and isinstance(value, bool):
        return False
    return isinstance(value, TYPES[name])


def errors(data, schema, path='$'):
    out = []
    t = schema.get('type')
    if t is not None:
        names = t if isinstance(t, list) else [t]
        if not any(_is(data, n) for n in names):
            return [f'{path}: want {"|".join(names)}, got {type(data).__name__}']
    if 'enum' in schema and data not in schema['enum']:
        out.append(f'{path}: {data!r} not in {schema["enum"]}')
    if 'minimum' in schema and _is(data, 'number') and data < schema['minimum']:
        out.append(f'{path}: {data} < {schema["minimum"]}')
    if isinstance(data, dict):
        for k in schema.get('required') or []:
            if k not in data:
                out.append(f'{path}: missing {k}')
        props = schema.get('properties') or {}
        for k, v in data.items():
            if k in props:
                out += errors(v, props[k], f'{path}.{k}')
            elif schema.get('additionalProperties') is False:
                out.append(f'{path}: unknown key {k}')
            elif isinstance(schema.get('additionalProperties'), dict):
                out += errors(v, schema['additionalProperties'], f'{path}.{k}')
    if isinstance(data, list) and isinstance(schema.get('items'), dict):
        for i, v in enumerate(data):
            out += errors(v, schema['items'], f'{path}[{i}]')
    return out


def load(name):
    return json.loads((SCHEMAS / f'{name}.schema.json').read_text())


def check(data, name):
    return errors(data, load(name))


def selftest():
    s = dict(type='object', required=['a'], properties=dict(a=dict(type='integer', minimum=0), b=dict(type='array', items=dict(enum=['x', 'y'])),
                                                             c=dict(type='object', additionalProperties=False, properties=dict(d=dict(type=['string', 'null'])))))
    cases = [
        (dict(a=1, b=['x'], c=dict(d=None)), 0),
        (dict(b=['x']), 1),                         # missing a
        (dict(a=True), 1),                          # bool is not an integer
        (dict(a=-1), 1),                            # minimum
        (dict(a=1, b=['z']), 1),                    # enum in items
        (dict(a=1, c=dict(e=1)), 1),                # additionalProperties false
        (dict(a=1, c=dict(d=3)), 1),                # type list
        ([], 1),                                    # wrong top type
    ]
    bad = [(d, n, errors(d, s)) for d, n in cases if len(errors(d, s)) != n]
    assert not bad, bad
    for name in ('config', 'catalog'):
        load(name)
    return f'schema OK ({len(cases)} cases, schemas: config, catalog)'

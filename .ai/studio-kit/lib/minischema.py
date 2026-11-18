"""킷 스키마용 JSON Schema 검사기. 표준 라이브러리만 쓴다.

킷의 스키마(director-handoff/schemas)가 쓰는 키워드만 구현한다. 모르는 키워드를 만나면
조용히 넘어가지 않고 SchemaError를 낸다. 스키마에 새 키워드를 쓰려면 여기에 먼저 구현한다.

  errors(값, 스키마)   → [(경로 튜플, 메시지)] 경로 순으로 정렬
  validate(값, 스키마) → 문제가 있으면 첫 번째를 ValidationError로 낸다
  check_schema(스키마)  → 스키마 전체를 훑어 모르는 키워드가 있으면 SchemaError를 낸다

$ref는 같은 문서 안(#/...)만 따라간다. format은 검사하지 않는다.
"""
import re

ANNOTATIONS = {"$schema", "$id", "$comment", "$defs", "title", "description", "default", "examples", "format"}


class SchemaError(ValueError):
    pass


class ValidationError(ValueError):
    def __init__(self, message, path=()):
        super().__init__(message)
        self.message, self.path = message, tuple(path)


def _is_type(value, name):
    if name == "null":
        return value is None
    if name == "boolean":
        return isinstance(value, bool)
    if name == "integer":
        return (isinstance(value, int) and not isinstance(value, bool)) or (isinstance(value, float) and value.is_integer())
    if name == "number":
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    if name == "string":
        return isinstance(value, str)
    if name == "array":
        return isinstance(value, list)
    if name == "object":
        return isinstance(value, dict)
    raise SchemaError(f"모르는 type: {name!r}")


def _equal(a, b):
    """JSON 기준 같음. 파이썬의 True == 1 을 같다고 보지 않는다."""
    if isinstance(a, bool) or isinstance(b, bool):
        return isinstance(a, bool) and isinstance(b, bool) and a == b
    if isinstance(a, (int, float)) and isinstance(b, (int, float)):
        return a == b
    if type(a) is not type(b):
        return False
    if isinstance(a, list):
        return len(a) == len(b) and all(_equal(x, y) for x, y in zip(a, b))
    if isinstance(a, dict):
        return a.keys() == b.keys() and all(_equal(a[k], b[k]) for k in a)
    return a == b


def _number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def _resolve(ref, root):
    if not ref.startswith("#"):
        raise SchemaError(f"같은 문서 안의 $ref만 쓸 수 있다: {ref}")
    node = root
    for part in [p for p in ref[1:].split("/") if p]:
        part = part.replace("~1", "/").replace("~0", "~")
        try:
            node = node[int(part)] if isinstance(node, list) else node[part]
        except (KeyError, IndexError, ValueError):
            raise SchemaError(f"$ref가 가리키는 곳이 없다: {ref}") from None
    return node


def _walk(value, schema, root, path, out):
    if schema is True:
        return
    if schema is False:
        out.append((path, "False schema does not allow " + repr(value)))
        return
    if not isinstance(schema, dict):
        raise SchemaError(f"스키마는 객체나 불리언이어야 한다: {schema!r}")
    for key, arg in schema.items():
        if key in ANNOTATIONS or key in ("properties", "additionalProperties"):
            continue  # properties와 additionalProperties는 아래에서 함께 본다
        if key == "$ref":
            _walk(value, _resolve(arg, root), root, path, out)
        elif key == "type":
            names = arg if isinstance(arg, list) else [arg]
            if not any(_is_type(value, n) for n in names):
                out.append((path, f"{value!r} is not of type " + ", ".join(repr(n) for n in names)))
        elif key == "enum":
            if not any(_equal(value, e) for e in arg):
                out.append((path, f"{value!r} is not one of {arg!r}"))
        elif key == "const":
            if not _equal(value, arg):
                out.append((path, f"{arg!r} was expected"))
        elif key == "required":
            if isinstance(value, dict):
                out.extend((path, f"{k!r} is a required property") for k in arg if k not in value)
        elif key == "items":
            if isinstance(value, list):
                for i, v in enumerate(value):
                    _walk(v, arg, root, path + (i,), out)
        elif key == "pattern":
            if isinstance(value, str) and not re.search(arg, value):
                out.append((path, f"{value!r} does not match {arg!r}"))
        elif key == "minLength":
            if isinstance(value, str) and len(value) < arg:
                out.append((path, f"{value!r} is too short"))
        elif key == "maxLength":
            if isinstance(value, str) and len(value) > arg:
                out.append((path, f"{value!r} is too long"))
        elif key == "minItems":
            if isinstance(value, list) and len(value) < arg:
                out.append((path, f"{value!r} is too short"))
        elif key == "maxItems":
            if isinstance(value, list) and len(value) > arg:
                out.append((path, f"{value!r} is too long"))
        elif key == "minProperties":
            if isinstance(value, dict) and len(value) < arg:
                out.append((path, f"{value!r} does not have enough properties"))
        elif key == "maxProperties":
            if isinstance(value, dict) and len(value) > arg:
                out.append((path, f"{value!r} has too many properties"))
        elif key == "uniqueItems":
            if arg and isinstance(value, list) and any(_equal(a, b) for i, a in enumerate(value) for b in value[i + 1:]):
                out.append((path, f"{value!r} has non-unique elements"))
        elif key == "minimum":
            if _number(value) and value < arg:
                out.append((path, f"{value!r} is less than the minimum of {arg!r}"))
        elif key == "maximum":
            if _number(value) and value > arg:
                out.append((path, f"{value!r} is greater than the maximum of {arg!r}"))
        elif key == "exclusiveMinimum":
            if _number(value) and value <= arg:
                out.append((path, f"{value!r} is less than or equal to the minimum of {arg!r}"))
        elif key == "exclusiveMaximum":
            if _number(value) and value >= arg:
                out.append((path, f"{value!r} is greater than or equal to the maximum of {arg!r}"))
        elif key == "allOf":
            for sub in arg:
                _walk(value, sub, root, path, out)
        elif key == "anyOf":
            if not any(not _collect(value, sub, root, path) for sub in arg):
                out.append((path, f"{value!r} is not valid under any of the given schemas"))
        elif key == "oneOf":
            hits = sum(1 for sub in arg if not _collect(value, sub, root, path))
            if hits == 0:
                out.append((path, f"{value!r} is not valid under any of the given schemas"))
            elif hits > 1:
                out.append((path, f"{value!r} is valid under each of {hits} schemas, expected exactly one"))
        elif key == "not":
            if not _collect(value, arg, root, path):
                out.append((path, f"{value!r} should not be valid under {arg!r}"))
        else:
            raise SchemaError(f"구현하지 않은 스키마 키워드: {key!r} (minischema.py에 먼저 구현한다)")
    if isinstance(value, dict):
        props = schema.get("properties") or {}
        for k, sub in props.items():
            if k in value:
                _walk(value[k], sub, root, path + (k,), out)
        extra = schema.get("additionalProperties", True)
        if extra is not True:
            unknown = [k for k in value if k not in props]
            if extra is False:
                if unknown:
                    out.append((path, "Additional properties are not allowed ("
                                + ", ".join(repr(k) for k in unknown) + (" was" if len(unknown) == 1 else " were")
                                + " unexpected)"))
            else:
                for k in unknown:
                    _walk(value[k], extra, root, path + (k,), out)


def _collect(value, schema, root, path):
    out = []
    _walk(value, schema, root, path, out)
    return out


VALUE_KEYWORDS = {"type", "enum", "const", "required", "pattern", "minLength", "maxLength", "minItems", "maxItems",
                  "minProperties", "maxProperties", "uniqueItems", "minimum", "maximum", "exclusiveMinimum",
                  "exclusiveMaximum", "$ref"}
ONE_SCHEMA = {"items", "additionalProperties", "not"}
MANY_SCHEMAS = {"allOf", "anyOf", "oneOf"}
NAMED_SCHEMAS = {"properties", "$defs"}


def check_schema(schema, root=None):
    root = schema if root is None else root
    if isinstance(schema, bool):
        return
    if not isinstance(schema, dict):
        raise SchemaError(f"스키마는 객체나 불리언이어야 한다: {schema!r}")
    for key, arg in schema.items():
        if key == "$ref":
            _resolve(arg, root)
        elif key in ONE_SCHEMA:
            check_schema(arg, root)
        elif key in MANY_SCHEMAS:
            for sub in arg:
                check_schema(sub, root)
        elif key in NAMED_SCHEMAS:
            for sub in arg.values():
                check_schema(sub, root)
        elif key not in VALUE_KEYWORDS and key not in ANNOTATIONS:
            raise SchemaError(f"구현하지 않은 스키마 키워드: {key!r} (minischema.py에 먼저 구현한다)")


def errors(value, schema):
    found = _collect(value, schema, schema, ())
    return sorted(found, key=lambda e: [str(p) for p in e[0]])


def validate(value, schema):
    found = errors(value, schema)
    if found:
        path, message = found[0]
        where = "/".join(str(p) for p in path) or "(root)"
        raise ValidationError(f"{where}: {message}", path)

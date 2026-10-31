"""Independent circle/segment oracle for bounded logical fixtures, not a game solver.

Exact binary-rational predicates preserve thin crossings. Distances are exposed
as certified floating intervals; no C# gameplay success flags are consumed.
"""
from dataclasses import dataclass
from fractions import Fraction as F
import math

from map_io import MapInputError, canonical_hash

LIMIT = 1024.0
MIN_LENGTH = F(1e-12)


def number(value, label):
    if type(value) not in (int, float):
        raise MapInputError('INVALID_NUMBER', label + ' must be a number, not bool/null')
    try:
        value = float(value)
    except OverflowError as error:
        raise MapInputError('NONFINITE_NUMBER', label) from error
    if not math.isfinite(value):
        raise MapInputError('NONFINITE_NUMBER', label)
    return value


def point(value):
    if not isinstance(value, (list, tuple)) or len(value) != 2:
        raise MapInputError('INVALID_POINT', 'Point must have exactly two coordinates')
    p = tuple(number(v, 'coordinate') for v in value)
    if any(abs(v) > LIMIT for v in p):
        raise MapInputError('COORDINATE_RANGE', 'Coordinate exceeds fixture range', 'BLOCKED')
    return tuple(F(v) for v in p)


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1]


def cross(a, b):
    return a[0] * b[1] - a[1] * b[0]


def at(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def edges(poly):
    return zip(poly, poly[1:] + poly[:1])


def on_segment(p, a, b):
    return cross(sub(p, a), sub(b, a)) == 0 and all(
        min(a[i], b[i]) <= p[i] <= max(a[i], b[i]) for i in (0, 1))


def intersections(a, b, c, d):
    """Exact parameters on AB; collinear overlap returns its clipped ends."""
    r, s = sub(b, a), sub(d, c)
    if r == (0, 0):
        return {F(0)} if on_segment(a, c, d) else set()
    den = cross(r, s)
    q = sub(c, a)
    if den:
        t, u = cross(q, s) / den, cross(q, r) / den
        return {t} if 0 <= t <= 1 and 0 <= u <= 1 else set()
    if cross(q, r):
        return set()
    axis = 0 if r[0] else 1
    t0, t1 = (c[axis] - a[axis]) / r[axis], (d[axis] - a[axis]) / r[axis]
    lo, hi = max(F(0), min(t0, t1)), min(F(1), max(t0, t1))
    return {lo, hi} if lo <= hi else set()


def polygon(value):
    if isinstance(value, dict) or (isinstance(value, list) and value
            and isinstance(value[0], list) and value[0]
            and isinstance(value[0][0], (list, dict))):
        raise MapInputError('UNSUPPORTED_TOPOLOGY', 'Holes/multiple boundaries unsupported', 'BLOCKED')
    if not isinstance(value, (list, tuple)) or len(value) < 3:
        raise MapInputError('INVALID_POLYGON', 'Polygon needs at least three points')
    if len(value) > 256:
        raise MapInputError('GEOMETRY_LIMIT', 'Too many polygon vertices', 'BLOCKED')
    pts = tuple(point(p) for p in value)
    if len(set(pts)) != len(pts):
        raise MapInputError('DUPLICATE_VERTEX', 'Repeated polygon vertex')
    segments = list(edges(pts))
    for a, b in segments:
        if dot(sub(b, a), sub(b, a)) < MIN_LENGTH ** 2:
            raise MapInputError('NUMERICAL_RANGE', 'Edge shorter than supported minimum', 'BLOCKED')
    for i, (a, b) in enumerate(segments):
        for j in range(i + 1, len(segments)):
            c, d = segments[j]
            hits = intersections(a, b, c, d)
            adjacent = j == i + 1 or (i == 0 and j == len(segments) - 1)
            if hits and (not adjacent or len(hits) > 1):
                raise MapInputError('SELF_INTERSECTION', 'Polygon edges cross or overlap')
    area = sum(cross(a, b) for a, b in segments)
    if not area:
        raise MapInputError('ZERO_AREA', 'Polygon has zero area')
    return pts if area > 0 else tuple(reversed(pts))


def location(p, poly):
    """-1 outside, 0 boundary, 1 inside. All operands remain rational."""
    inside = False
    for a, b in edges(poly):
        if on_segment(p, a, b):
            return 0
        if (a[1] > p[1]) != (b[1] > p[1]):
            x = a[0] + (p[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1])
            if p[0] < x:
                inside = not inside
    return 1 if inside else -1


def point_distance_squared(p, a, b):
    ab, ap = sub(b, a), sub(p, a)
    length = dot(ab, ab)
    t = max(F(0), min(F(1), dot(ap, ab) / length)) if length else F(0)
    delta = sub(p, at(a, b, t))
    return dot(delta, delta)


def segment_distance_squared(a, b, c, d):
    if intersections(a, b, c, d):
        return F(0)
    return min(point_distance_squared(a, c, d), point_distance_squared(b, c, d),
               point_distance_squared(c, a, b), point_distance_squared(d, a, b))


def sqrt_interval(q):
    if q == 0:
        return F(0), F(0)
    rounded = float(q)
    low_q = max(0.0, math.nextafter(rounded, -math.inf))
    high_q = math.nextafter(rounded, math.inf)
    low = max(0.0, math.nextafter(math.sqrt(low_q), -math.inf))
    high = math.nextafter(math.sqrt(high_q), math.inf)
    for _ in range(8):
        if F(low) ** 2 <= q <= F(high) ** 2:
            return F(low), F(high)
        low = max(0.0, math.nextafter(low, -math.inf))
        high = math.nextafter(high, math.inf)
    raise MapInputError('NUMERICAL_UNCERTAINTY', 'Could not certify distance interval', 'BLOCKED')


def outward(value, lower):
    rounded = float(value)
    if lower and F(rounded) > value:
        rounded = math.nextafter(rounded, -math.inf)
    if not lower and F(rounded) < value:
        rounded = math.nextafter(rounded, math.inf)
    return rounded


@dataclass(frozen=True)
class Geometry:
    boundary: tuple
    obstacles: tuple
    input_sha256: str
    normalized_sha256: str

    @classmethod
    def from_polygons(cls, boundary, obstacles):
        if not isinstance(obstacles, (list, tuple)):
            raise MapInputError('INVALID_OBSTACLES', 'Obstacle list is required')
        if len(obstacles) > 128:
            raise MapInputError('GEOMETRY_LIMIT', 'Too many obstacles', 'BLOCKED')
        outer, solids = polygon(boundary), tuple(polygon(p) for p in obstacles)
        as_json = lambda p: [[float(x), float(y)] for x, y in p]
        normalized = {'boundary': as_json(outer), 'obstacles': [as_json(p) for p in solids]}
        return cls(outer, solids, canonical_hash({'boundary': boundary, 'obstacles': obstacles}),
                   canonical_hash(normalized))


def _result(q, radius, epsilon, kind, surface, witness=None, negative=False):
    lo, hi = sqrt_interval(q)
    if negative:
        lo, hi = -hi, -lo
    lo, hi = lo - radius, hi - radius
    status = 'FAIL' if hi < -epsilon else 'PASS' if lo > epsilon else 'BLOCKED'
    # A forbidden-region witness can prove failure, never safety of the whole path.
    if witness is not None and status == 'PASS':
        status = 'BLOCKED'
    result = {'status': status, 'clearance_kind': kind, 'surface': surface,
              'body_clearance': float((lo + hi) / 2),
              'lower_bound': outward(lo, True), 'upper_bound': outward(hi, False),
              'exact_gap_interval': [str(lo), str(hi)],
              'reason_codes': [] if status == 'PASS' else
              ['SWEPT_PENETRATION' if status == 'FAIL' else 'NUMERICAL_BOUNDARY']}
    if witness is not None:
        result['witness'] = witness
    return result


def assess_sweep(geometry, start, end, radius, epsilon):
    if not isinstance(geometry, Geometry):
        raise MapInputError('MISSING_GEOMETRY', 'Validated geometry required')
    a, b = point(start), point(end)
    radius, epsilon = number(radius, 'radius'), number(epsilon, 'epsilon')
    for label, value in [('radius', radius), ('epsilon', epsilon)]:
        if value <= 0:
            raise MapInputError('INVALID_' + label.upper(), label + ' must be positive')
        if not 1e-12 <= value <= LIMIT:
            raise MapInputError('NUMERICAL_RANGE', label + ' outside supported range', 'BLOCKED')
    length_sq = dot(sub(b, a), sub(b, a))
    if 0 < length_sq < MIN_LENGTH ** 2:
        raise MapInputError('NUMERICAL_RANGE', 'Sweep shorter than supported minimum', 'BLOCKED')
    radius, epsilon = F(radius), F(epsilon)
    surfaces = [('boundary', geometry.boundary, True)] + [
        ('obstacle:' + str(i), p, False) for i, p in enumerate(geometry.obstacles)]
    results = []
    for label, poly, is_boundary in surfaces:
        ts = {F(0), F(1)}
        for c, d in edges(poly):
            ts.update(intersections(a, b, c, d))
        ts = sorted(ts)
        samples = ts + [(lo + hi) / 2 for lo, hi in zip(ts, ts[1:])]
        witnesses = []
        for t in samples:
            p = at(a, b, t)
            where = location(p, poly)
            forbidden = where < 0 if is_boundary else where > 0
            if forbidden:
                q = min(point_distance_squared(p, c, d) for c, d in edges(poly))
                witnesses.append(_result(q, radius, epsilon, 'witness_bound', label,
                    {'t_exact': str(t), 'point_exact': [str(v) for v in p],
                     'point': [float(v) for v in p]}, negative=True))
        if witnesses:
            results.extend(witnesses)
        else:
            q = min(segment_distance_squared(a, b, c, d) for c, d in edges(poly))
            results.append(_result(q, radius, epsilon, 'minimum_clearance', label))
    priority = {'FAIL': 0, 'BLOCKED': 1, 'PASS': 2}
    worst = min(results, key=lambda r: (priority[r['status']], r['lower_bound']))
    return dict(worst, geometry_sha256=geometry.normalized_sha256,
                surface_count=len(surfaces), scope='logical_fixture_circle_sweep')

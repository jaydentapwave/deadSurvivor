#!/usr/bin/env python3
"""아이템 데이터와 기획서 표 대조(에디터 없이). 기준: docs/features/f3-combat/plan.md C절의 두 표(C1 대표 8종, C2 나머지 24종).

  python3 -B ./tools/check_item_table.py                  표의 32행 중 파일이 있는 것만 대조한다. 없는 것은 "없음"으로 출력(exit 0)
  python3 -B ./tools/check_item_table.py --ids a,b,c      그 id만 대조한다. 파일이 없으면 실패(exit 1), 표에 없는 id면 exit 2
  python3 -B ./tools/check_item_table.py --require-all    32개 파일이 모두 있어야 통과
  python3 -B ./tools/check_item_table.py --root <폴더>    다른 아이템 폴더(<id>.json이 있는 곳)를 대조한다
  python3 -B ./tools/check_item_table.py --plan <파일>    다른 기획서를 읽는다
  python3 -B ./tools/check_item_table.py --self-test      지금 파일이 통과하고, 표에서 만든 맞는 파일 24종이 통과하고,
                                                          틀린 파일 4종(mode 다름, shape 다름, 값 다름, 인접 빠짐)이 각각 실패하는지 본다

보는 것: mode, shape 칸 목록(순서 무관), power·interval·range(표에 있는 것), 기본 효과 종류·값(baseEffect·baseValue),
추가 효과 종류·값·대상·인접(bonusEffect·bonusValue·bonusTarget·bonusNeedsAdjacent), effectInterval·effectRadius(표에 없으면 0).
표의 "—"는 효과가 없다는 뜻이다(종류 "", 값 0). 효과 이름은 데이터 문자열 그대로다(ItemEffectKind, 대소문자 그대로).
종료 코드: 0 통과, 1 다름(또는 지정한 파일 없음), 2 돌릴 수 없음(기획서·표·폴더를 읽지 못함).
"""
import json
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_PLAN = PROJECT_ROOT / "docs" / "features" / "f3-combat" / "plan.md"
DEFAULT_ROOT = PROJECT_ROOT / "Assets" / "Game" / "03.Resources" / "Resources" / "Data" / "Item"
SECTION_TITLE = "### C. 아이템 데이터"
TABLE_ROW_COUNT = 32
MODES = ("melee", "sweep", "projectile", "explosion", "chain", "heal", "healArea", "passive")
EFFECTS = ("def", "hp", "attack", "haste", "range", "radius", "pierce", "extraShots", "chain", "pulse", "shield",
           "startShield", "heal", "share")
BONUS_TARGETS = ("attack",) + tuple(m for m in MODES if m != "passive")
NUMBER_FIELDS = ("effectInterval", "effectRadius")
EMPTY = ("—", "-", "")
TOLERANCE = 1e-6


class TableError(Exception):
    """기획서 표를 읽지 못함(exit 2)."""


def read_table(plan_path):
    """C절 표 두 개를 읽어 {id: 기대값} (표 순서)로 돌려준다."""
    if not plan_path.is_file():
        raise TableError(f"기획서가 없다: {plan_path}")
    try:
        lines = plan_path.read_text(encoding="utf-8").splitlines()
    except (OSError, UnicodeDecodeError) as e:
        raise TableError(f"기획서를 읽지 못했다: {e}") from None
    start = next((i for i, line in enumerate(lines) if line.startswith(SECTION_TITLE)), None)
    if start is None:
        raise TableError(f"기획서에 '{SECTION_TITLE}' 절이 없다")
    end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith("### ")), len(lines))
    rows = {}
    header = None
    for line in lines[start + 1:end]:
        if not line.startswith("|"):
            header = None
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if header is None:
            header = cells
            continue
        if all(set(c) <= set("-: ") for c in cells):
            continue
        row = dict(zip(header, cells))
        item_id = row.get("id", "")
        if item_id in rows:
            raise TableError(f"표에 id '{item_id}'가 두 번 있다")
        rows[item_id] = parse_row(row)
    if len(rows) != TABLE_ROW_COUNT:
        raise TableError(f"표의 아이템 행이 {len(rows)}개 — {TABLE_ROW_COUNT}개여야 한다")
    return rows


def parse_row(row):
    item_id = row.get("id", "")
    if not re.fullmatch(r"[a-z][a-z0-9_]*", item_id):
        raise TableError(f"표의 id '{item_id}'를 읽지 못했다")
    label = f"표 {item_id}"
    mode = row.get("`mode`", "").strip("`")
    if mode not in MODES:
        raise TableError(f"{label}: mode '{mode}'를 모른다")
    shape_text = row.get("`shape` (x, y)", "")
    shape = [(int(x), int(y)) for x, y in re.findall(r"\((-?\d+)\s*,\s*(-?\d+)\)", shape_text)]
    if not shape:
        raise TableError(f"{label}: shape '{shape_text}'를 읽지 못했다")
    expected = {"id": item_id, "mode": mode, "shape": shape, "stats": {},
                "baseEffect": "", "baseValue": 0.0, "bonusEffect": "", "bonusValue": 0.0,
                "bonusTarget": "", "bonusNeedsAdjacent": False, "effectInterval": 0.0, "effectRadius": 0.0}
    if "기본 효과" in row:
        # C1 표: power·interval·range 칸이 따로 있다
        for field in ("power", "interval", "range"):
            text = row.get(f"`{field}`", "")
            if text not in EMPTY:
                expected["stats"][field] = to_number(text, label, field)
        base_text = row["기본 효과"]
    else:
        # C2 표: "피해 · 간격 · 사거리" 또는 기본 효과 하나
        base_text = row.get("피해·간격·사거리 / 기본 효과", "")
        numbers = [p.strip() for p in base_text.split("·")]
        if len(numbers) == 3:
            for field, text in zip(("power", "interval", "range"), numbers):
                expected["stats"][field] = to_number(text, label, field)
            base_text = "—"
    if base_text not in EMPTY:
        effect, value, condition = parse_effect(base_text, label)
        if condition is not None:
            raise TableError(f"{label}: 기본 효과 '{base_text}'에 조건이 있다")
        expected["baseEffect"], expected["baseValue"] = effect, value
    bonus_text = row.get("추가 효과(조건)", "")
    if bonus_text not in EMPTY:
        effect, value, condition = parse_effect(bonus_text, label)
        expected["bonusEffect"], expected["bonusValue"] = effect, value
        parse_condition(condition, expected, label)
    return expected


def parse_effect(text, label):
    """'attack 0.14(attack, 인접)' → ('attack', 0.14, 'attack, 인접')"""
    match = re.fullmatch(r"(\w+)\s+(-?[\d.]+)\s*(?:\((.*)\))?", text.strip())
    if match is None:
        raise TableError(f"{label}: 효과 '{text}'를 읽지 못했다")
    effect = match.group(1)
    if effect not in EFFECTS:
        raise TableError(f"{label}: 효과 이름 '{effect}'를 모른다")
    return effect, to_number(match.group(2), label, effect), match.group(3)


def parse_condition(condition, expected, label):
    if condition is None:
        return
    for token in (t.strip() for t in condition.split(",")):
        named = re.fullmatch(r"`(\w+)`\s+(-?[\d.]+)", token)
        if token == "조건 없음":
            continue
        if token == "인접":
            expected["bonusNeedsAdjacent"] = True
        elif named is not None and named.group(1) in NUMBER_FIELDS:
            expected[named.group(1)] = to_number(named.group(2), label, named.group(1))
        elif token in BONUS_TARGETS:
            expected["bonusTarget"] = token
        else:
            raise TableError(f"{label}: 조건 '{token}'을 모른다")


def to_number(text, label, field):
    try:
        return float(text)
    except ValueError:
        raise TableError(f"{label}: {field} '{text}'가 수가 아니다") from None


def compare(expected, path):
    """파일과 표 행이 다른 점 목록(같으면 빈 목록)."""
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as e:
        return [f"JSON을 읽지 못했다: {e}"]
    if not isinstance(data, dict):
        return ["JSON 최상위가 객체가 아니다"]
    diffs = []

    def differ(field, want, have):
        diffs.append(f"{field}: 표 {want!r}, 파일 {have!r}")

    def check_number(field, want):
        have = data.get(field)
        if isinstance(have, bool) or not isinstance(have, (int, float)) or abs(have - want) > TOLERANCE:
            differ(field, want, have)

    def check_exact(field, want):
        if data.get(field) != want:
            differ(field, want, data.get(field))

    check_exact("id", expected["id"])
    check_exact("mode", expected["mode"])
    cells = data.get("shape")
    try:
        have_shape = sorted((c["x"], c["y"]) for c in cells)
    except (TypeError, KeyError):
        have_shape = cells
    if have_shape != sorted(expected["shape"]):
        differ("shape", sorted(expected["shape"]), have_shape)
    for field, want in expected["stats"].items():
        check_number(field, want)
    for kind, value in (("baseEffect", "baseValue"), ("bonusEffect", "bonusValue")):
        check_exact(kind, expected[kind])
        check_number(value, expected[value])
    check_exact("bonusTarget", expected["bonusTarget"])
    check_exact("bonusNeedsAdjacent", expected["bonusNeedsAdjacent"])
    for field in NUMBER_FIELDS:
        check_number(field, expected[field])
    return diffs


def report(table, root, ids, is_all_required):
    if not root.is_dir():
        print(f"아이템 폴더가 없다: {root}", file=sys.stderr)
        return 2
    if ids is not None:
        unknown = [i for i in ids if i not in table]
        if unknown:
            print(f"표에 없는 id: {', '.join(unknown)}", file=sys.stderr)
            return 2
    targets = ids if ids is not None else list(table)
    must_exist = ids is not None or is_all_required
    passed, missing, failed = [], [], []
    for item_id in targets:
        path = root / f"{item_id}.json"
        if not path.is_file():
            missing.append(item_id)
            print(f"  {item_id}: 없음" + (" (실패)" if must_exist else ""))
            continue
        diffs = compare(table[item_id], path)
        if diffs:
            failed.append(item_id)
            print(f"  {item_id}: 다름")
            for d in diffs:
                print(f"    - {d}")
        else:
            passed.append(item_id)
            print(f"  {item_id}: 같음")
    summary = f"같음 {len(passed)}, 다름 {len(failed)}, 없음 {len(missing)} (대상 {len(targets)})"
    if failed or (must_exist and missing):
        print(f"ITEM-TABLE: FAIL ({summary})")
        return 1
    print(f"ITEM-TABLE: OK ({summary})")
    return 0


def write_item(path, expected):
    """표 행에서 맞는 파일을 만든다(자체 시험용)."""
    data = {"id": expected["id"], "name": expected["id"], "mode": expected["mode"],
            "shape": [{"x": x, "y": y} for x, y in expected["shape"]],
            "power": 0, "interval": 0, "range": 0}
    data.update(expected["stats"])
    for field in ("baseEffect", "baseValue", "bonusEffect", "bonusValue", "bonusTarget", "bonusNeedsAdjacent",
                  "effectInterval", "effectRadius"):
        data[field] = expected[field]
    data["icon"] = ""
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def self_test(table, root, plan_path):
    def run(folder, *args):
        result = subprocess.run([sys.executable, "-B", __file__, "--plan", str(plan_path), "--root", str(folder), *args],
                                capture_output=True, text=True)
        return result.returncode, result.stdout + result.stderr

    def edit(path, change):
        data = json.loads(path.read_text(encoding="utf-8"))
        change(data)
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")

    present = [i for i in table if (root / f"{i}.json").is_file()]
    if not present:
        print(f"SELF-TEST: 대조할 지금 파일이 없다: {root}", file=sys.stderr)
        return 2
    adjacent_id = next(i for i, e in table.items() if e["bonusNeedsAdjacent"])
    wrong_cases = [
        ("mode 다름", "sword", lambda d: d.update(mode="sweep"), "mode:"),
        ("shape 다름", "axe", lambda d: d["shape"].pop(), "shape:"),
        ("값 다름", "shortbow", lambda d: d.update(power=d["power"] + 1), "power:"),
        ("인접 빠짐", adjacent_id, lambda d: d.update(bonusNeedsAdjacent=False), "bonusNeedsAdjacent:"),
    ]
    failures = []
    with tempfile.TemporaryDirectory() as tmp:
        base = Path(tmp) / "base"
        base.mkdir()
        for item_id in present:
            shutil.copy(root / f"{item_id}.json", base / f"{item_id}.json")
        code, out = run(base, "--ids", ",".join(present))
        print(f"  지금 파일 {len(present)}종: exit {code}, {'통과(맞음)' if code == 0 else '틀림'}")
        if code != 0:
            failures.append(f"지금 파일이 통과하지 않는다:\n{out}")

        full = Path(tmp) / "full"
        shutil.copytree(base, full)
        for item_id, expected in table.items():
            if not (full / f"{item_id}.json").is_file():
                write_item(full / f"{item_id}.json", expected)
        code, out = run(full, "--require-all")
        print(f"  표에서 만든 맞는 파일 포함 {len(table)}종(--require-all): exit {code}, {'통과(맞음)' if code == 0 else '틀림'}")
        if code != 0:
            failures.append(f"표에서 만든 맞는 파일이 통과하지 않는다:\n{out}")

        partial = Path(tmp) / "partial"
        shutil.copytree(full, partial)
        (partial / f"{adjacent_id}.json").unlink()
        for label, args, want in (("파일 하나 없음(옵션 없음)", (), 0), ("파일 하나 없음(--require-all)", ("--require-all",), 1),
                                  ("파일 하나 없음(--ids)", ("--ids", adjacent_id), 1)):
            code, out = run(partial, *args)
            print(f"  {label}: exit {code}, {'맞음' if code == want else '틀림'}")
            if code != want:
                failures.append(f"{label}: exit {code} — {want}이어야 한다\n{out}")

        for name, item_id, change, expected_line in wrong_cases:
            case_dir = Path(tmp) / f"case_{item_id}"
            shutil.copytree(full, case_dir)
            edit(case_dir / f"{item_id}.json", change)
            code, out = run(case_dir, "--ids", item_id)
            hit = [line.strip() for line in out.splitlines() if expected_line in line]
            print(f"  {name}({item_id}): exit {code}, {'실패함(맞음)' if code == 1 and hit else '틀림'}"
                  f"{' — ' + hit[0] if hit else ''}")
            if code != 1 or not hit:
                failures.append(f"{name}: exit {code}, 기대한 줄('{expected_line}') {'있음' if hit else '없음'}\n{out}")
    if failures:
        print("SELF-TEST: FAIL")
        for f in failures:
            print(f"  - {f}")
        return 1
    print(f"SELF-TEST: OK (지금 파일·표에서 만든 파일 통과, 틀린 파일 {len(wrong_cases)}종 모두 실패)")
    return 0


def take_value(args, flag):
    if flag not in args:
        return None
    i = args.index(flag)
    if i + 1 >= len(args):
        raise ValueError(f"{flag} 뒤에 값이 없다")
    value = args[i + 1]
    del args[i:i + 2]
    return value


def main(argv):
    args = argv[1:]
    try:
        root_text = take_value(args, "--root")
        plan_text = take_value(args, "--plan")
        ids_text = take_value(args, "--ids")
    except ValueError as e:
        print(e, file=sys.stderr)
        return 2
    flags = {flag: flag in args for flag in ("--self-test", "--require-all")}
    args = [a for a in args if a not in flags]
    if args:
        print(__doc__.strip(), file=sys.stderr)
        return 2
    root = Path(root_text).resolve() if root_text else DEFAULT_ROOT
    plan_path = Path(plan_text).resolve() if plan_text else DEFAULT_PLAN
    ids = None
    if ids_text is not None:
        ids = [i.strip() for i in ids_text.split(",") if i.strip()]
        if not ids:
            print("--ids에 id가 없다", file=sys.stderr)
            return 2
    try:
        table = read_table(plan_path)
    except TableError as e:
        print(f"표를 읽지 못했다: {e}", file=sys.stderr)
        return 2
    try:
        if flags["--self-test"]:
            return self_test(table, root, plan_path)
        return report(table, root, ids, flags["--require-all"])
    except OSError as e:
        print(f"파일·폴더를 다루지 못했다: {e}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))

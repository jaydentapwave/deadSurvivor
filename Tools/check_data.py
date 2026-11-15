#!/usr/bin/env python3
"""게임 데이터 검사(에디터 없이). 스키마: docs/features/f1-skeleton/plan.md "데이터 스키마".

  python3 -B ./tools/check_data.py              지금 데이터를 검사한다. 문제가 있으면 줄마다 출력하고 exit 1
  python3 -B ./tools/check_data.py --root <폴더> 다른 데이터 폴더를 검사한다(Character/ Item/ Wave/ Run/ 이 있는 곳)
  python3 -B ./tools/check_data.py --self-test  지금 데이터가 통과하고, 틀린 데이터 4종(id 중복, 없는 startItem,
                                                웨이브 order 빈칸, 캐릭터 수 4 아님)이 각각 실패하는지 본다

보는 것: JSON 형식, 필수 칸과 형, id = 파일 이름, id 중복, 가리키는 대상(startItem → Item), 웨이브 order 1..N 빈칸 없음,
개수 범위(캐릭터 4, 아이템 4 이상, 웨이브 1~20), 값 범위. 모르는 칸은 통과시킨다(뒤 기능이 같은 파일에 칸을 더한다).
종료 코드: 0 통과, 1 문제 있음, 2 실행할 수 없음(데이터 폴더 없음).
"""
import json
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

DEFAULT_ROOT = Path(__file__).resolve().parent.parent / "Assets" / "Game" / "03.Resources" / "Resources" / "Data"
CHARACTER_COUNT = 4
ITEM_MIN = 4
WAVE_RANGE = (1, 20)
COLOR = re.compile(r"#[0-9a-fA-F]{6}")


def is_int(v):
    return isinstance(v, int) and not isinstance(v, bool)


def is_num(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def is_text(v):
    return isinstance(v, str) and v.strip() != ""


def load_kind(root, kind, problems):
    """종류 폴더의 (파일 이름 줄기, 내용) 목록. 형식이 틀린 파일은 문제에 적고 뺀다."""
    folder = root / kind
    if not folder.is_dir():
        problems.append(f"{kind}/: 폴더가 없다")
        return []
    entries = []
    for path in sorted(folder.glob("*.json")):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except ValueError as e:
            problems.append(f"{kind}/{path.name}: JSON 형식 오류: {e}")
            continue
        if not isinstance(data, dict):
            problems.append(f"{kind}/{path.name}: 최상위가 객체가 아니다")
            continue
        entries.append((path.stem, data))
    return entries


def check_fields(label, data, spec, problems):
    for name, (test, wording) in spec.items():
        if name not in data:
            problems.append(f"{label}: '{name}' 칸이 없다")
        elif not test(data[name]):
            problems.append(f"{label}: '{name}' = {data[name]!r} — {wording}")


def check_ids(kind, entries, problems):
    """id = 파일 이름, id 중복. 고유한 id 집합을 돌려준다."""
    seen = {}
    for stem, data in entries:
        cid = data.get("id")
        if not is_text(cid):
            continue
        if cid != stem:
            problems.append(f"{kind}/{stem}.json: id '{cid}'가 파일 이름과 다르다")
        if cid in seen:
            problems.append(f"{kind}: id '{cid}' 중복 ({seen[cid]}.json, {stem}.json)")
        else:
            seen[cid] = stem
    return set(seen)


def check(root):
    """문제 목록과 요약 줄 목록."""
    problems, notes = [], []

    items = load_kind(root, "Item", problems)
    for stem, data in items:
        check_fields(f"Item/{stem}.json", data, {
            "id": (is_text, "글자여야 한다"),
            "name": (is_text, "글자여야 한다"),
        }, problems)
    item_ids = check_ids("Item", items, problems)
    if len(items) < ITEM_MIN:
        problems.append(f"Item: {len(items)}개 — {ITEM_MIN}개 이상이어야 한다")

    characters = load_kind(root, "Character", problems)
    for stem, data in characters:
        label = f"Character/{stem}.json"
        check_fields(label, data, {
            "id": (is_text, "글자여야 한다"),
            "order": (is_int, "정수여야 한다"),
            "name": (is_text, "글자여야 한다"),
            "role": (is_text, "글자여야 한다"),
            "hp": (lambda v: is_int(v) and v > 0, "0보다 큰 정수여야 한다"),
            "atk": (lambda v: is_num(v) and v > 0, "0보다 큰 수여야 한다"),
            "def": (lambda v: is_num(v) and 0 <= v <= 0.8, "0~0.8이어야 한다"),
            "startItem": (is_text, "Item id여야 한다"),
            "color": (lambda v: isinstance(v, str) and COLOR.fullmatch(v), "#rrggbb여야 한다"),
        }, problems)
        start = data.get("startItem")
        if is_text(start) and start not in item_ids:
            problems.append(f"{label}: startItem '{start}'인 Item이 없다")
    check_ids("Character", characters, problems)
    if len(characters) != CHARACTER_COUNT:
        problems.append(f"Character: {len(characters)}개 — {CHARACTER_COUNT}개여야 한다(원정대 4명 고정, 기획서 A3)")
    orders = sorted(d["order"] for _, d in characters if is_int(d.get("order")))
    if orders != list(range(1, len(characters) + 1)):
        problems.append(f"Character: order {orders} — 1..{len(characters)} 중복·빈칸 없이여야 한다")

    waves = load_kind(root, "Wave", problems)
    for stem, data in waves:
        check_fields(f"Wave/{stem}.json", data, {
            "id": (is_text, "글자여야 한다"),
            "order": (is_int, "정수여야 한다"),
            "duration": (lambda v: is_num(v) and v > 0, "0보다 큰 초여야 한다"),
            "hasBurningTime": (lambda v: isinstance(v, bool), "true/false여야 한다"),
        }, problems)
    check_ids("Wave", waves, problems)
    if not WAVE_RANGE[0] <= len(waves) <= WAVE_RANGE[1]:
        problems.append(f"Wave: {len(waves)}개 — {WAVE_RANGE[0]}~{WAVE_RANGE[1]}개여야 한다")
    wave_orders = sorted(d["order"] for _, d in waves if is_int(d.get("order")))
    if wave_orders != list(range(1, len(waves) + 1)):
        problems.append(f"Wave: order {wave_orders} — 1..{len(waves)} 중복·빈칸 없이여야 한다")
    total = sum(d["duration"] for _, d in waves if is_num(d.get("duration")))
    notes.append(f"웨이브 {len(waves)}개, 시간 합 {total:g}초")

    rules_path = root / "Run" / "run_rules.json"
    rules = load_single(rules_path, problems)
    if rules is not None:
        check_fields("Run/run_rules.json", rules, {
            "simStep": (lambda v: is_num(v) and 0 < v <= 0.1, "0보다 크고 0.1 이하인 초여야 한다"),
            "burningStartRatio": (lambda v: is_num(v) and 0 <= v <= 1, "0~1이어야 한다"),
            "burningSeconds": (lambda v: is_num(v) and v > 0, "0보다 큰 초여야 한다"),
            "speedOptions": (lambda v: isinstance(v, list) and v and all(is_num(n) and n > 0 for n in v),
                             "0보다 큰 수의 목록이어야 한다"),
            "defaultSeed": (lambda v: is_int(v) and v >= 0, "0 이상 정수여야 한다(0 = 시작 시각에서 뽑음)"),
            "defaultEnemyCountScale": (lambda v: is_num(v) and v >= 0, "0 이상이어야 한다"),
            "unitScale": (lambda v: is_num(v) and v > 0, "0보다 커야 한다"),
            "fieldCells": (lambda v: is_int(v) and v > 0, "0보다 큰 정수여야 한다"),
        }, problems)

    combat_path = root / "Run" / "placeholder_combat.json"
    combat = load_single(combat_path, problems)
    if combat is not None:
        label = "Run/placeholder_combat.json"
        check_fields(label, combat, {
            "memberSpots": (lambda v: isinstance(v, list) and all(
                isinstance(p, dict) and is_num(p.get("x")) and is_num(p.get("y")) for p in v),
                "{x, y} 목록이어야 한다"),
            "weapon": (lambda v: isinstance(v, dict), "객체여야 한다"),
            "enemy": (lambda v: isinstance(v, dict), "객체여야 한다"),
            "spawnCount": (lambda v: is_int(v) and v >= 0, "0 이상 정수여야 한다"),
            "spawnMargin": (lambda v: is_num(v) and v >= 0, "0 이상이어야 한다"),
            "burningIntervalScale": (lambda v: is_num(v) and v > 0, "0보다 커야 한다"),
        }, problems)
        spots = combat.get("memberSpots")
        if isinstance(spots, list) and len(spots) != len(characters):
            problems.append(f"{label}: memberSpots {len(spots)}개 — 캐릭터 수({len(characters)})와 같아야 한다")
        if isinstance(combat.get("weapon"), dict):
            check_fields(f"{label} weapon", combat["weapon"], {
                "damage": (lambda v: is_num(v) and v > 0, "0보다 커야 한다"),
                "interval": (lambda v: is_num(v) and v > 0, "0보다 큰 초여야 한다"),
                "range": (lambda v: is_num(v) and v > 0, "0보다 커야 한다"),
            }, problems)
        if isinstance(combat.get("enemy"), dict):
            check_fields(f"{label} enemy", combat["enemy"], {
                "hp": (lambda v: is_num(v) and v > 0, "0보다 커야 한다"),
                "damage": (lambda v: is_num(v) and v >= 0, "0 이상이어야 한다"),
                "speed": (lambda v: is_num(v) and v > 0, "0보다 커야 한다"),
                "radius": (lambda v: is_num(v) and v > 0, "0보다 커야 한다"),
                "attackInterval": (lambda v: is_num(v) and v > 0, "0보다 큰 초여야 한다"),
                "contactGap": (lambda v: is_num(v) and v >= 0, "0 이상이어야 한다"),
                "color": (lambda v: isinstance(v, str) and COLOR.fullmatch(v), "#rrggbb여야 한다"),
            }, problems)
    return problems, notes


def load_single(path, problems):
    label = f"{path.parent.name}/{path.name}"
    if not path.is_file():
        problems.append(f"{label}: 파일이 없다")
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except ValueError as e:
        problems.append(f"{label}: JSON 형식 오류: {e}")
        return None
    if not isinstance(data, dict):
        problems.append(f"{label}: 최상위가 객체가 아니다")
        return None
    return data


def report(root):
    problems, notes = check(root)
    for line in notes:
        print(line)
    if problems:
        print(f"DATA: FAIL ({len(problems)}건)")
        for p in problems:
            print(f"  - {p}")
        return 1
    print("DATA: OK")
    return 0


def self_test(root):
    """틀린 데이터 4종이 각각 실패하고, 지금 데이터는 통과하는지."""
    def edit_json(path, change):
        data = json.loads(path.read_text(encoding="utf-8"))
        change(data)
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")

    def break_duplicate(d):
        shutil.copy(d / "Item" / "sword.json", d / "Item" / "sword_copy.json")

    def break_start_item(d):
        first = sorted((d / "Character").glob("*.json"))[0]
        edit_json(first, lambda data: data.update(startItem="no_such_item"))

    def break_wave_gap(d):
        middle = sorted((d / "Wave").glob("*.json"))[1]
        middle.unlink()

    def break_character_count(d):
        sorted((d / "Character").glob("*.json"))[-1].unlink()

    cases = [("id 중복", break_duplicate, "중복"), ("없는 startItem", break_start_item, "startItem"),
             ("웨이브 order 빈칸", break_wave_gap, "Wave: order"), ("캐릭터 수 4 아님", break_character_count, "Character: 3개")]
    failures = []
    base_problems, _ = check(root)
    if base_problems:
        failures.append("지금 데이터가 통과하지 않는다: " + "; ".join(base_problems))
    for name, breaker, expected in cases:
        with tempfile.TemporaryDirectory() as tmp:
            copy = Path(tmp) / "Data"
            shutil.copytree(root, copy, ignore=shutil.ignore_patterns("*.meta"))
            breaker(copy)
            problems, _ = check(copy)
            hit = [p for p in problems if expected in p]
            exit_code = subprocess.run([sys.executable, "-B", __file__, "--root", str(copy)],
                                       capture_output=True, text=True).returncode
            status = "실패함(맞음)" if hit and exit_code == 1 else "틀림"
            print(f"  {name}: exit {exit_code}, {status} — {hit[0] if hit else problems}")
            if not hit:
                failures.append(f"{name}: 기대한 문제('{expected}')가 나오지 않았다")
            if exit_code != 1:
                failures.append(f"{name}: exit {exit_code} — 1이어야 한다")
    if failures:
        print("SELF-TEST: FAIL")
        for f in failures:
            print(f"  - {f}")
        return 1
    print("SELF-TEST: OK (지금 데이터 통과, 틀린 데이터 4종 모두 실패)")
    return 0


def main(argv):
    args = argv[1:]
    root = DEFAULT_ROOT
    if "--root" in args:
        i = args.index("--root")
        if i + 1 >= len(args):
            print("--root 뒤에 폴더가 없다", file=sys.stderr)
            return 2
        root = Path(args[i + 1]).resolve()
        del args[i:i + 2]
    is_self_test = "--self-test" in args
    if is_self_test:
        args.remove("--self-test")
    if args:
        print(__doc__.strip(), file=sys.stderr)
        return 2
    if not root.is_dir():
        print(f"데이터 폴더가 없다: {root}", file=sys.stderr)
        return 2
    return self_test(root) if is_self_test else report(root)


if __name__ == "__main__":
    sys.exit(main(sys.argv))

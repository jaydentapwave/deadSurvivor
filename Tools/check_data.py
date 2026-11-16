#!/usr/bin/env python3
"""게임 데이터 검사(에디터 없이). 스키마: docs/features/f1-skeleton/plan.md·f2-movement/plan.md·f3-combat/plan.md "데이터 스키마".

  python3 -B ./tools/check_data.py              지금 데이터를 검사한다. 문제가 있으면 줄마다 출력하고 exit 1
  python3 -B ./tools/check_data.py --root <폴더> 다른 데이터 폴더를 검사한다(Character/ Item/ Wave/ Run/ 이 있는 곳)
  python3 -B ./tools/check_data.py --self-test  지금 데이터가 통과하고, 틀린 데이터 13종(F1: id 중복, 없는 startItem,
                                                웨이브 order 빈칸, 캐릭터 수 4 아님 / F2: 진형 자리 3개, 진형 order 빈칸,
                                                자리 평균 ≠ 0, memberSpots 남음 / F3: 모르는 mode, interval 0인 발동 아이템,
                                                4×4 밖이거나 끊긴 shape, 모르는 효과 종류, passive 시작 아이템)이 각각 실패하는지 본다

보는 것: JSON 형식, 필수 칸과 형, id = 파일 이름, id 중복, 가리키는 대상(startItem → Item), 웨이브 order 1..N 빈칸 없음,
개수 범위(캐릭터 4, 아이템 4~40, 웨이브 1~20, 진형 1~6), 진형 자리(수 = 캐릭터 수, 평균 0, 중복 없음)(F2), 값 범위,
아이템 발동 방식·효과·칸 모양(F3), 전투 규칙 파일(F3). 모르는 칸은 통과시킨다(뒤 기능이 같은 파일에 칸을 더한다).
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
ITEM_RANGE = (4, 40)
MODES = ("melee", "sweep", "projectile", "explosion", "chain", "heal", "healArea", "passive")
BASE_EFFECTS = ("attack", "def", "hp", "heal", "haste", "range")
BONUS_EFFECTS = BASE_EFFECTS + ("shield", "startShield", "radius", "pierce", "extraShots", "chain", "pulse", "share")
COUNT_EFFECTS = ("pierce", "extraShots", "chain")
BONUS_TARGETS = ("", "attack") + tuple(m for m in MODES if m != "passive")
SHAPE_SIZE = 4
SHAPE_MAX_CELLS = 16
WAVE_RANGE = (1, 20)
FORMATION_RANGE = (1, 6)
MEAN_TOLERANCE = 0.01
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
        check_item_combat(f"Item/{stem}.json", data, problems)
    item_ids = check_ids("Item", items, problems)
    item_modes = {d.get("id"): d.get("mode") for _, d in items}
    if not ITEM_RANGE[0] <= len(items) <= ITEM_RANGE[1]:
        problems.append(f"Item: {len(items)}개 — {ITEM_RANGE[0]}~{ITEM_RANGE[1]}개여야 한다")
    missing_modes = [m for m in MODES if m not in item_modes.values()]
    if missing_modes:
        problems.append(f"Item: 발동 방식 {missing_modes}인 아이템이 없다 — 8가지가 모두 한 번 이상 있어야 한다")
    notes.append(f"아이템 {len(items)}개, 발동 방식 {len(MODES) - len(missing_modes)}/{len(MODES)}가지")

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
            "sprite": (lambda v: isinstance(v, str), "글자여야 한다(비면 임시 도형)"),
        }, problems)
        start = data.get("startItem")
        if is_text(start) and start not in item_ids:
            problems.append(f"{label}: startItem '{start}'인 Item이 없다")
        elif is_text(start) and item_modes.get(start) == "passive":
            problems.append(f"{label}: startItem '{start}'의 발동 방식이 passive다 — 시작 아이템은 발동 아이템이어야 한다")
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
            "enemy": (lambda v: isinstance(v, dict), "객체여야 한다"),
            "spawnCount": (lambda v: is_int(v) and v >= 0, "0 이상 정수여야 한다"),
            "spawnMargin": (lambda v: is_num(v) and v >= 0, "0 이상이어야 한다"),
            "burningIntervalScale": (lambda v: is_num(v) and v > 0, "0보다 커야 한다"),
        }, problems)
        if "memberSpots" in combat:
            problems.append(f"{label}: 'memberSpots' 칸이 남아 있다 — F2 진형(Formation/)이 대체했다(F2 기획서 데이터 스키마)")
        if "weapon" in combat:
            problems.append(f"{label}: 'weapon' 칸이 남아 있다 — F3이 지웠다(아이템 발동 전투)")
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

    rules = load_single(root / "Combat" / "combat_rules.json", problems)
    if rules is not None:
        positive = (lambda v: is_num(v) and v > 0, "0보다 커야 한다")
        spec = {name: positive for name in ("rangeUnit", "hasteCap", "healPeriod", "shieldPeriod", "meleeHalfAngle",
                                            "sweepHalfAngle", "projectileSpeed", "projectileSpread", "explosionRadius",
                                            "explosionTriggerGap", "chainHopRange", "firedSeconds")}
        spec["defCap"] = (lambda v: is_num(v) and 0 < v <= 1, "0보다 크고 1 이하여야 한다")
        spec["chainBaseHits"] = (lambda v: is_int(v) and v > 0, "0보다 큰 정수여야 한다")
        check_fields("Combat/combat_rules.json", rules, spec, problems)

    check_formations(root, len(characters), problems, notes)

    move_rules = load_single(root / "Party" / "move_rules.json", problems)
    if move_rules is not None:
        check_fields("Party/move_rules.json", move_rules, {
            "moveSpeed": (lambda v: is_num(v) and v > 0, "0보다 커야 한다(바닥 단위/초)"),
            "followRate": (lambda v: is_num(v) and v > 0, "0보다 커야 한다(1/초)"),
            "joystickRadius": (lambda v: is_num(v) and v > 0, "0보다 커야 한다(캔버스 단위)"),
        }, problems)
    return problems, notes


def check_formations(root, slot_count, problems, notes):
    """진형(F2): 개수 1~6, order 1..N 빈칸 없음, slots 수 = 캐릭터 수, 자리 평균 (0, 0) ±0.01, 같은 자리 두 번 없음."""
    formations = load_kind(root, "Formation", problems)
    for stem, data in formations:
        label = f"Formation/{stem}.json"
        check_fields(label, data, {
            "id": (is_text, "글자여야 한다"),
            "order": (is_int, "정수여야 한다"),
            "name": (is_text, "글자여야 한다"),
            "slots": (lambda v: isinstance(v, list) and all(
                isinstance(p, dict) and is_num(p.get("x")) and is_num(p.get("y")) for p in v),
                "{x, y} 목록이어야 한다"),
        }, problems)
        slots = data.get("slots")
        if not isinstance(slots, list) or not all(
                isinstance(p, dict) and is_num(p.get("x")) and is_num(p.get("y")) for p in slots):
            continue
        if len(slots) != slot_count:
            problems.append(f"{label}: slots {len(slots)}개 — 캐릭터 수({slot_count})와 같아야 한다")
        if slots:
            mean_x = sum(p["x"] for p in slots) / len(slots)
            mean_y = sum(p["y"] for p in slots) / len(slots)
            if abs(mean_x) > MEAN_TOLERANCE or abs(mean_y) > MEAN_TOLERANCE:
                problems.append(f"{label}: 자리 평균 ({mean_x:g}, {mean_y:g}) — (0, 0) ±{MEAN_TOLERANCE}여야 한다")
        points = [(p["x"], p["y"]) for p in slots]
        if len(set(points)) != len(points):
            problems.append(f"{label}: 같은 자리가 두 번 있다 {points}")
    check_ids("Formation", formations, problems)
    if not FORMATION_RANGE[0] <= len(formations) <= FORMATION_RANGE[1]:
        problems.append(f"Formation: {len(formations)}개 — {FORMATION_RANGE[0]}~{FORMATION_RANGE[1]}개여야 한다")
    formation_orders = sorted(d["order"] for _, d in formations if is_int(d.get("order")))
    if formation_orders != list(range(1, len(formations) + 1)):
        problems.append(f"Formation: order {formation_orders} — 1..{len(formations)} 중복·빈칸 없이여야 한다")
    notes.append(f"진형 {len(formations)}개")


def check_item_combat(label, data, problems):
    """F3 아이템 칸: mode, shape, power·interval·range, 효과, 대상, 파동 값, icon."""
    mode = data.get("mode")
    if mode not in MODES:
        problems.append(f"{label}: 'mode' = {mode!r} — {'·'.join(MODES)} 중 하나여야 한다")
    check_shape(label, data.get("shape"), problems)
    for name in ("power", "interval", "range"):
        v = data.get(name)
        if not is_num(v):
            problems.append(f"{label}: '{name}' = {v!r} — 수여야 한다")
        elif mode == "passive" and v != 0:
            problems.append(f"{label}: '{name}' = {v!r} — passive면 0이어야 한다")
        elif mode in MODES and mode != "passive" and v <= 0:
            problems.append(f"{label}: '{name}' = {v!r} — 발동 아이템은 0보다 커야 한다")
    check_effect(label, data, "baseEffect", "baseValue", BASE_EFFECTS, problems)
    bonus = data.get("bonusEffect")
    check_effect(label, data, "bonusEffect", "bonusValue", BONUS_EFFECTS, problems)
    if bonus in COUNT_EFFECTS and is_num(data.get("bonusValue")) and data["bonusValue"] != int(data["bonusValue"]):
        problems.append(f"{label}: 'bonusValue' = {data['bonusValue']!r} — {bonus}는 정수여야 한다")
    target = data.get("bonusTarget")
    if target not in BONUS_TARGETS:
        problems.append(f"{label}: 'bonusTarget' = {target!r} — \"\", attack, 발동 방식 이름(passive 제외) 중 하나여야 한다")
    elif bonus == "" and target != "":
        problems.append(f"{label}: 'bonusTarget' = {target!r} — bonusEffect가 비면 \"\"여야 한다")
    if not isinstance(data.get("bonusNeedsAdjacent"), bool):
        problems.append(f"{label}: 'bonusNeedsAdjacent' = {data.get('bonusNeedsAdjacent')!r} — true/false여야 한다")
    for name in ("effectInterval", "effectRadius"):
        v = data.get(name)
        if not is_num(v):
            problems.append(f"{label}: '{name}' = {v!r} — 수여야 한다")
        elif bonus == "pulse" and v <= 0:
            problems.append(f"{label}: '{name}' = {v!r} — pulse면 0보다 커야 한다")
        elif bonus != "pulse" and v != 0:
            problems.append(f"{label}: '{name}' = {v!r} — pulse가 아니면 0이어야 한다")
    if not isinstance(data.get("icon"), str):
        problems.append(f"{label}: 'icon' = {data.get('icon')!r} — 글자여야 한다(비면 이름 표시)")


def check_effect(label, data, kind_field, value_field, allowed, problems):
    kind, value = data.get(kind_field), data.get(value_field)
    if kind != "" and kind not in allowed:
        problems.append(f"{label}: '{kind_field}' = {kind!r} — \"\" 또는 {'·'.join(allowed)} 중 하나여야 한다")
    if not is_num(value):
        problems.append(f"{label}: '{value_field}' = {value!r} — 수여야 한다")
    elif kind == "" and value != 0:
        problems.append(f"{label}: '{value_field}' = {value!r} — {kind_field}가 비면 0이어야 한다")
    elif kind != "" and value <= 0:
        problems.append(f"{label}: '{value_field}' = {value!r} — 0보다 커야 한다")


def check_shape(label, shape, problems):
    """칸 1~16개, 0 ≤ x, y ≤ 3, 겹침 없음, 상하좌우로 이어짐."""
    if not isinstance(shape, list) or not shape or not all(
            isinstance(c, dict) and is_int(c.get("x")) and is_int(c.get("y")) for c in shape):
        problems.append(f"{label}: 'shape' — 정수 {{x, y}} 칸 1개 이상의 목록이어야 한다")
        return
    cells = [(c["x"], c["y"]) for c in shape]
    if len(cells) > SHAPE_MAX_CELLS:
        problems.append(f"{label}: 'shape' 칸 {len(cells)}개 — {SHAPE_MAX_CELLS}개 이하여야 한다")
    if any(not (0 <= x < SHAPE_SIZE and 0 <= y < SHAPE_SIZE) for x, y in cells):
        problems.append(f"{label}: 'shape' {cells} — 4×4 밖 칸이 있다(0 ≤ x, y ≤ 3)")
    if len(set(cells)) != len(cells):
        problems.append(f"{label}: 'shape' {cells} — 겹친 칸이 있다")
    seen, stack = {cells[0]}, [cells[0]]
    unique = set(cells)
    while stack:
        x, y = stack.pop()
        for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if n in unique and n not in seen:
                seen.add(n)
                stack.append(n)
    if seen != unique:
        problems.append(f"{label}: 'shape' {cells} — 상하좌우로 이어지지 않은(끊긴) 칸이 있다")


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

    def edit_item(d, name, change):
        edit_json(d / "Item" / f"{name}.json", change)

    def break_mode(d):
        edit_item(d, "sword", lambda data: data.update(mode="laser"))

    def break_interval(d):
        edit_item(d, "shortbow", lambda data: data.update(interval=0))

    def break_shape(d):
        edit_item(d, "wand", lambda data: data.update(shape=[{"x": 0, "y": 0}, {"x": 2, "y": 0}, {"x": 4, "y": 0}]))

    def break_effect(d):
        edit_item(d, "ironshield", lambda data: data.update(baseEffect="speed"))

    def break_passive_start(d):
        first = sorted((d / "Character").glob("*.json"))[0]
        edit_json(first, lambda data: data.update(startItem="ironshield"))


    def break_formation_slot_count(d):
        edit_json(d / "Formation" / "balanced.json", lambda data: data["slots"].pop())

    def break_formation_order_gap(d):
        edit_json(d / "Formation" / "line.json", lambda data: data.update(order=5))

    def break_formation_mean(d):
        edit_json(d / "Formation" / "wedge.json", lambda data: data["slots"][0].update(y=60))

    def break_member_spots(d):
        edit_json(d / "Run" / "placeholder_combat.json",
                  lambda data: data.update(memberSpots=[{"x": 0, "y": 0}] * 4))

    cases = [("id 중복", break_duplicate, "중복"), ("없는 startItem", break_start_item, "startItem"),
             ("웨이브 order 빈칸", break_wave_gap, "Wave: order"), ("캐릭터 수 4 아님", break_character_count, "Character: 3개"),
             ("진형 자리 3개", break_formation_slot_count, "slots 3개"),
             ("진형 order 빈칸", break_formation_order_gap, "Formation: order"),
             ("자리 평균 ≠ 0", break_formation_mean, "자리 평균"),
             ("memberSpots 남음", break_member_spots, "memberSpots"),
             ("모르는 mode", break_mode, "'mode'"), ("interval 0인 발동 아이템", break_interval, "'interval'"),
             ("4×4 밖이거나 끊긴 shape", break_shape, "'shape'"), ("모르는 효과 종류", break_effect, "'baseEffect'"),
             ("passive 시작 아이템", break_passive_start, "passive")]
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
    print(f"SELF-TEST: OK (지금 데이터 통과, 틀린 데이터 {len(cases)}종 모두 실패)")
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

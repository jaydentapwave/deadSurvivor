#!/usr/bin/env python3
"""게임 데이터 검사(에디터 없이). 스키마: docs/features/f1-skeleton/plan.md·f2-movement/plan.md·f3-combat/plan.md·f4-monsters/plan.md·f5-bag/plan.md·f6-feedback/plan.md "데이터 스키마".

  python3 -B ./tools/check_data.py              지금 데이터를 검사한다. 문제가 있으면 줄마다 출력하고 exit 1
  python3 -B ./tools/check_data.py --root <폴더> 다른 데이터 폴더를 검사한다(Character/ Item/ Wave/ Run/ Monster/ Spawn/ 이 있는 곳)
  python3 -B ./tools/check_data.py --art-root <폴더> 그림 폴더(AIGenerated/가 있는 곳). 기본은 데이터 폴더 기준 ../../Art
  python3 -B ./tools/check_data.py --self-test  지금 데이터가 통과하고, 틀린 데이터 32종(F1: id 중복, 없는 startItem,
                                                웨이브 order 빈칸, 캐릭터 수 4 아님 / F2: 진형 자리 3개, 진형 order 빈칸,
                                                자리 평균 ≠ 0, memberSpots 남음 / F3: 모르는 mode, interval 0인 발동 아이템,
                                                4×4 밖이거나 끊긴 shape, 모르는 효과 종류, passive 시작 아이템 / F4: 없는 몬스터 가중치,
                                                다섯 밖의 행동, 행동 칸 묶음 빠짐, weight 0, 몬스터 id 중복, 가중치 없는 웨이브 / F5: 등급 배수 줄어듦,
                                                minCount > maxCount, 모든 lootWeight 0, 가방에 안 들어가는 시작 아이템 / F6: 피해 숫자 maxActive 0,
                                                다섯 밖의 alwaysShowBehaviors, 색 형식 틀림 / 그림 넣기: 그림 파일이 없는 캐릭터 키,
                                                뒤 그림만 없는 캐릭터 키, 그림 파일이 없는 몬스터 키, 형식이 틀린 키, sprite 칸이 없는 몬스터,
                                                모르는 moveStyle)이 각각 실패하는지 본다

보는 것: JSON 형식, 필수 칸과 형, id = 파일 이름, id 중복, 가리키는 대상(startItem → Item), 웨이브 order 1..N 빈칸 없음,
개수 범위(캐릭터 4, 아이템 4~40, 웨이브 1~20, 진형 1~6), 진형 자리(수 = 캐릭터 수, 평균 0, 중복 없음)(F2), 값 범위,
아이템 발동 방식·효과·칸 모양(F3), 전투 규칙 파일(F3),
등급·가방 규칙·전리품 규칙 파일과 아이템 lootWeight(F5), 피드백 규칙 파일(F6), 캐릭터·몬스터 그림 키와 그림 파일·
몬스터 moveStyle·AIGenerated/ PNG 이름 유일성(그림 넣기 D절). 모르는 칸은 통과시킨다(뒤 기능이 같은 파일에 칸을 더한다).
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
MONSTER_RANGE = (1, 20)
COLOR = re.compile(r"#[0-9a-fA-F]{6}")
SPRITE_KEY = re.compile(r"[a-z0-9_]*")
MOVE_STYLES = ("squash", "hop")


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


def default_art_root(root):
    """데이터 폴더(…/03.Resources/Resources/Data) 기준 ../../Art."""
    return root.parent.parent / "Art"


def check(root, art_root=None):
    """문제 목록과 요약 줄 목록."""
    problems, notes = [], []
    art_root = default_art_root(root) if art_root is None else art_root

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
            "sprite": (lambda v: isinstance(v, str) and SPRITE_KEY.fullmatch(v), "비거나 [a-z0-9_]+여야 한다(비면 임시 도형)"),
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

    check_monsters(root, waves, problems)
    # 몬스터 파일 형식 문제는 check_monsters가 이미 적었다
    check_sprites(art_root, characters, load_kind(root, "Monster", []), problems, notes)

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
        if "memberSpots" in combat:
            problems.append(f"{label}: 'memberSpots' 칸이 남아 있다 — F2 진형(Formation/)이 대체했다(F2 기획서 데이터 스키마)")
        if "weapon" in combat:
            problems.append(f"{label}: 'weapon' 칸이 남아 있다 — F3이 지웠다(아이템 발동 전투)")

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

    check_bag_loot(root, items, characters, problems, notes)
    check_feedback(root, problems)
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


def check_bag_loot(root, items, characters, problems, notes):
    """F5: 등급 파일, 가방 규칙(4×4, 시작 칸, 아이템이 들어감), 전리품 규칙, 아이템 lootWeight."""
    grades = load_single(root / "Grade" / "grades.json", problems)
    if grades is not None:
        grade_list = grades.get("grades")
        if not isinstance(grade_list, list) or not grade_list:
            problems.append("Grade/grades.json: 'grades' — 등급 1개 이상의 목록이어야 한다")
        else:
            seen, prev_mult = set(), None
            for i, grade in enumerate(grade_list):
                label = f"Grade/grades.json grades[{i}]"
                if not isinstance(grade, dict):
                    problems.append(f"{label}: 객체여야 한다")
                    continue
                check_fields(label, grade, {
                    "id": (is_text, "글자여야 한다"),
                    "name": (is_text, "글자여야 한다"),
                    "color": (lambda v: isinstance(v, str) and COLOR.fullmatch(v), "#rrggbb여야 한다"),
                    "mult": (lambda v: is_num(v) and v > 0, "0보다 커야 한다"),
                }, problems)
                gid, mult = grade.get("id"), grade.get("mult")
                if is_text(gid) and gid in seen:
                    problems.append(f"Grade/grades.json: id '{gid}' 중복")
                seen.add(gid)
                if is_num(mult) and prev_mult is not None and mult <= prev_mult:
                    problems.append(f"{label}: 'mult' = {mult!r} — 앞 등급({prev_mult!r})보다 커야 한다")
                if is_num(mult):
                    prev_mult = mult
            notes.append(f"등급 {len(grade_list)}개")

    bag = load_single(root / "Bag" / "bag_rules.json", problems)
    if bag is not None:
        label = "Bag/bag_rules.json"
        check_fields(label, bag, {
            "bagWidth": (lambda v: v == SHAPE_SIZE and is_int(v), f"{SHAPE_SIZE}여야 한다(게임 기획서 S5 4×4)"),
            "bagHeight": (lambda v: v == SHAPE_SIZE and is_int(v), f"{SHAPE_SIZE}여야 한다(게임 기획서 S5 4×4)"),
            "startCell": (lambda v: isinstance(v, dict) and is_int(v.get("x")) and is_int(v.get("y")), "정수 {x, y}여야 한다"),
            "autoEquip": (lambda v: isinstance(v, dict) and is_num(v.get("hpWeight")) and v["hpWeight"] >= 0
                          and is_num(v.get("shieldWeight")) and v["shieldWeight"] >= 0,
                          "{hpWeight, shieldWeight} 0 이상 수여야 한다"),
        }, problems)
        width, height, start = bag.get("bagWidth"), bag.get("bagHeight"), bag.get("startCell")
        if is_int(width) and is_int(height) and isinstance(start, dict) and is_int(start.get("x")) and is_int(start.get("y")):
            sx, sy = start["x"], start["y"]
            if not (0 <= sx < width and 0 <= sy < height):
                problems.append(f"{label}: 'startCell' ({sx}, {sy}) — 가방({width}×{height}) 안이어야 한다")
            shapes = {d.get("id"): d.get("shape") for _, d in items}
            for item_id, shape in shapes.items():
                cells = shape_cells(shape)
                if cells is not None and any(x >= width or y >= height for x, y in cells):
                    problems.append(f"Item/{item_id}.json: 'shape' — 회전 0에서 가방({width}×{height})에 들어가지 않는다")
            for stem, data in characters:
                cells = shape_cells(shapes.get(data.get("startItem")))
                if cells is not None and any(not (0 <= sx + x < width and 0 <= sy + y < height) for x, y in cells):
                    problems.append(f"Character/{stem}.json: 시작 아이템 '{data.get('startItem')}'이 시작 칸 ({sx}, {sy})에서 "
                                    f"가방에 들어가지 않는다")

    loot = load_single(root / "Loot" / "loot_rules.json", problems)
    if loot is not None:
        label = "Loot/loot_rules.json"
        positive = (lambda v: is_num(v) and v > 0, "0보다 커야 한다")
        check_fields(label, loot, {
            "minCount": (lambda v: is_int(v) and v >= 1, "1 이상 정수여야 한다"),
            "maxCount": (lambda v: is_int(v) and v >= 1, "1 이상 정수여야 한다"),
            "earlyWaveMax": (lambda v: is_int(v) and v >= 0, "0 이상 정수여야 한다"),
            "earlyBonusFactor": positive,
            "mergeWaveMin": (lambda v: is_int(v) and v >= 0, "0 이상 정수여야 한다"),
            "mergeFactor": positive,
            "lootSeedOffset": (is_int, "정수여야 한다"),
        }, problems)
        lo, hi = loot.get("minCount"), loot.get("maxCount")
        if is_int(lo) and is_int(hi) and lo > hi:
            problems.append(f"{label}: minCount {lo} > maxCount {hi} — 1 ≤ minCount ≤ maxCount여야 한다")

    weights = []
    for stem, data in items:
        w = data.get("lootWeight", 1)
        if not is_num(w) or w < 0:
            problems.append(f"Item/{stem}.json: 'lootWeight' = {w!r} — 0 이상 수여야 한다(없으면 1)")
        else:
            weights.append(w)
    if items and not any(w > 0 for w in weights):
        problems.append("Item: lootWeight > 0인 아이템이 없다 — 전리품 후보가 하나 이상이어야 한다")
    notes.append(f"전리품 후보 {sum(1 for w in weights if w > 0)}개")


def shape_cells(shape):
    """정수 {x, y} 목록이면 (x, y) 목록, 아니면 None(형식 문제는 check_shape가 적는다)."""
    if not isinstance(shape, list) or not shape or not all(
            isinstance(c, dict) and is_int(c.get("x")) and is_int(c.get("y")) for c in shape):
        return None
    return [(c["x"], c["y"]) for c in shape]


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


def check_monsters(root, waves, problems):
    """F4: 몬스터 5종 파일, 생성 규칙, 웨이브의 네 칸(spawnCount·hpScale·damageScale·typeWeights)."""
    positive = (lambda v: is_num(v) and v > 0, "0보다 커야 한다")
    ratio = (lambda v: is_num(v) and 0 < v <= 1, "0보다 크고 1 이하여야 한다")
    count = (lambda v: is_int(v) and v >= 1, "1 이상 정수여야 한다")
    color = (lambda v: isinstance(v, str) and COLOR.fullmatch(v), "#rrggbb여야 한다")
    groups = {
        "contact": {"interval": positive},
        "projectile": {"range": positive, "approachRatio": ratio, "retreatRatio": ratio, "interval": positive,
                       "shotSpeed": positive, "shotTravelScale": positive, "shotHitRadius": positive, "shotColor": color},
        "charge": {"triggerRange": positive, "rallyRadius": positive, "rallyMax": count, "windupSeconds": positive,
                   "dashSeconds": positive, "dashSpeed": positive, "dashHitRadius": positive, "recoverSeconds": positive,
                   "cooldown": positive},
        "bomb": {"fuseDistance": positive, "fuseSeconds": positive, "blastRadius": positive, "swarmTriggerRange": positive,
                 "swarmSeconds": positive, "swarmSpeed": positive, "flockRadius": positive, "separationRadius": positive,
                 "cohesion": ratio, "alignment": ratio, "separation": positive, "steerRate": positive, "groupSize": count,
                 "groupSpread": positive},
    }
    group_of = {"contact": "contact", "armored": "contact", "projectile": "projectile", "charge": "charge", "bomb": "bomb"}

    monsters = load_kind(root, "Monster", problems)
    for stem, data in monsters:
        label = f"Monster/{stem}.json"
        check_fields(label, data, {
            "id": (is_text, "글자여야 한다"),
            "behavior": (lambda v: v in group_of, f"{'·'.join(group_of)} 중 하나여야 한다"),
            "hp": positive,
            "damage": (lambda v: is_num(v) and v >= 0, "0 이상이어야 한다"),
            "speed": positive,
            "radius": positive,
            "contactGap": (lambda v: is_num(v) and v >= 0, "0 이상이어야 한다"),
            "color": color,
        }, problems)
        group = group_of.get(data.get("behavior"))
        if group is None:
            continue
        if not isinstance(data.get(group), dict):
            problems.append(f"{label}: 행동 '{data['behavior']}'에 필요한 '{group}' 칸 묶음이 없다")
            continue
        check_fields(f"{label} {group}", data[group], groups[group], problems)
        if group == "projectile" and all(is_num(data[group].get(k)) for k in ("approachRatio", "retreatRatio")) \
                and data[group]["retreatRatio"] >= data[group]["approachRatio"]:
            problems.append(f"{label} projectile: retreatRatio가 approachRatio보다 작아야 한다")
    monster_ids = check_ids("Monster", monsters, problems)
    if not MONSTER_RANGE[0] <= len(monsters) <= MONSTER_RANGE[1]:
        problems.append(f"Monster: {len(monsters)}개 — {MONSTER_RANGE[0]}~{MONSTER_RANGE[1]}개여야 한다")

    def check_weights(label, weights):
        if not isinstance(weights, list) or not weights:
            problems.append(f"{label}: 가중치가 1개 이상인 목록이어야 한다")
            return
        seen = set()
        for entry in weights:
            if not isinstance(entry, dict):
                problems.append(f"{label}: {entry!r} — {{monster, weight}} 객체여야 한다")
                continue
            monster, weight = entry.get("monster"), entry.get("weight")
            if monster in seen:
                problems.append(f"{label}: 몬스터 '{monster}'가 두 번 있다")
            seen.add(monster)
            if monster not in monster_ids:
                problems.append(f"{label}: 가리키는 몬스터 '{monster}'가 없다")
            if not (is_num(weight) and weight > 0):
                problems.append(f"{label}: '{monster}' weight = {weight!r} — 0보다 커야 한다")

    for stem, data in waves:
        label = f"Wave/{stem}.json"
        check_fields(label, data, {
            "spawnCount": (lambda v: is_int(v) and v >= 0, "0 이상 정수여야 한다"),
            "hpScale": positive,
            "damageScale": positive,
        }, problems)
        if "typeWeights" not in data:
            problems.append(f"{label}: 'typeWeights' 칸이 없다")
        else:
            check_weights(f"{label} typeWeights", data["typeWeights"])

    spawn = load_single(root / "Spawn" / "spawn_rules.json", problems)
    if spawn is None:
        return
    label = "Spawn/spawn_rules.json"
    check_fields(label, spawn, {
        "spawnMargin": (lambda v: is_num(v) and v >= 0, "0 이상이어야 한다"),
        "spawnEndGap": (lambda v: is_num(v) and v >= 0, "0 이상인 초여야 한다"),
        "initialAttackDelay": (lambda v: is_num(v) and v >= 0, "0 이상인 초여야 한다"),
        "burningIntervalScale": positive,
        "burningSpread": (lambda v: is_num(v) and v >= 0, "0 이상인 라디안이어야 한다"),
        "heading": (lambda v: isinstance(v, dict), "객체여야 한다"),
        "recycle": (lambda v: isinstance(v, dict), "객체여야 한다"),
    }, problems)
    if "burningTypeWeights" not in spawn:
        problems.append(f"{label}: 'burningTypeWeights' 칸이 없다")
    else:
        check_weights(f"{label} burningTypeWeights", spawn["burningTypeWeights"])
    heading = spawn.get("heading")
    if isinstance(heading, dict):
        share = (lambda v: is_num(v) and 0 <= v <= 1, "0~1이어야 한다")
        check_fields(f"{label} heading", heading, {"front": share, "left": share, "right": share, "back": share,
                                                   "jitter": (lambda v: is_num(v) and v >= 0, "0 이상인 라디안이어야 한다")},
                     problems)
        parts = [heading.get(k) for k in ("front", "left", "right", "back")]
        if all(is_num(v) for v in parts) and abs(sum(parts) - 1) > 1e-6:
            problems.append(f"{label} heading: front+left+right+back = {sum(parts):g} — 1이어야 한다")
    recycle = spawn.get("recycle")
    if isinstance(recycle, dict):
        check_fields(f"{label} recycle", recycle, {
            "outsideMargin": (lambda v: is_num(v) and v >= 0, "0 이상이어야 한다"),
            "holdSeconds": positive,
            "placeMargin": (lambda v: is_num(v) and v >= 0, "0 이상이어야 한다"),
            "maxPerCheck": count,
            "checkInterval": positive,
            "minAttackDelay": (lambda v: is_num(v) and v >= 0, "0 이상인 초여야 한다"),
        }, problems)


def check_sprites(art_root, characters, monsters, problems, notes):
    """그림 넣기 D절: 그림 키 형식, 키가 있으면 그림 파일, 몬스터 sprite 칸 필수와 moveStyle, AIGenerated/ PNG 이름 유일성."""
    generated = art_root / "AIGenerated"
    character_filled = 0
    for stem, data in characters:
        key = data.get("sprite")
        if not (isinstance(key, str) and SPRITE_KEY.fullmatch(key)) or key == "":
            continue
        character_filled += 1
        for side in ("front", "back"):
            path = generated / "Characters" / key / side / f"{key}_{side}.png"
            if not path.is_file():
                problems.append(f"Character/{stem}.json: sprite '{key}'의 그림 {path.relative_to(art_root)}이 없다")
    monster_filled = 0
    for stem, data in monsters:
        label = f"Monster/{stem}.json"
        if "sprite" not in data:
            problems.append(f"{label}: 'sprite' 칸이 없다(비면 임시 도형, 칸은 모든 몬스터 파일에 있어야 한다)")
        else:
            key = data["sprite"]
            if not (isinstance(key, str) and SPRITE_KEY.fullmatch(key)):
                problems.append(f"{label}: 'sprite' = {key!r} — 비거나 [a-z0-9_]+여야 한다")
            elif key != "":
                monster_filled += 1
                path = generated / "Monsters" / key / f"{key}.png"
                if not path.is_file():
                    problems.append(f"{label}: sprite '{key}'의 그림 {path.relative_to(art_root)}이 없다")
        if data.get("moveStyle") not in MOVE_STYLES:
            problems.append(f"{label}: 'moveStyle' = {data.get('moveStyle')!r} — {'·'.join(MOVE_STYLES)} 중 하나여야 한다")
    seen = {}
    for path in sorted(generated.rglob("*.png")) if generated.is_dir() else []:
        if path.name in seen:
            problems.append(f"AIGenerated: 그림 이름 '{path.name}' 중복 — {seen[path.name]}, {path.relative_to(art_root)}")
        else:
            seen[path.name] = path.relative_to(art_root)
    notes.append(f"그림 키 캐릭터 {character_filled}/{len(characters)}, 몬스터 {monster_filled}/{len(monsters)}")


FEEDBACK_BEHAVIORS = ("contact", "projectile", "charge", "armored", "bomb")


def check_feedback(root, problems):
    """Feedback/feedback_rules.json(F6 데이터 스키마): 칸 모두, 범위, 색 형식, alwaysShowBehaviors."""
    rules = load_single(root / "Feedback" / "feedback_rules.json", problems)
    if rules is None:
        return
    label = "Feedback/feedback_rules.json"
    positive = (lambda v: is_num(v) and v > 0, "0보다 커야 한다")
    non_negative = (lambda v: is_num(v) and v >= 0, "0 이상이어야 한다")
    color = (lambda v: isinstance(v, str) and COLOR.fullmatch(v), "#rrggbb여야 한다")
    groups = {
        "damageNumber": {
            "lifetime": positive, "risePx": non_negative,
            "fadeStartRatio": (lambda v: is_num(v) and 0 <= v <= 1, "0~1이어야 한다"),
            "offsetPx": (is_num, "수여야 한다"), "fontSizePx": positive,
            "maxActive": (lambda v: is_int(v) and 1 <= v <= 200, "1~200 정수여야 한다"),
            "textColor": color, "outlineColor": color,
        },
        "hpLoss": {"holdSeconds": non_negative, "shrinkSeconds": positive, "color": color},
        "shake": {"amplitudePx": (lambda v: is_int(v) and 0 <= v <= 4, "0~4 정수여야 한다"),
                  "seconds": non_negative, "minInterval": non_negative},
        "haptic": {"enabled": (lambda v: isinstance(v, bool), "true/false여야 한다"), "minInterval": non_negative},
        "monsterHp": {
            "alwaysShowBehaviors": (lambda v: isinstance(v, list) and all(isinstance(b, str) for b in v), "글자 목록이어야 한다"),
            "lingerSeconds": non_negative,
            "maxActive": (lambda v: is_int(v) and 0 <= v <= 400, "0~400 정수여야 한다"),
            "widthPx": (lambda v: is_int(v) and v > 0, "0보다 큰 정수여야 한다"),
            "heightPx": (lambda v: is_int(v) and v > 0, "0보다 큰 정수여야 한다"),
            "offsetPx": (is_num, "수여야 한다"), "fillColor": color, "backColor": color,
        },
    }
    for group, spec in groups.items():
        data = rules.get(group)
        if not isinstance(data, dict):
            problems.append(f"{label}: '{group}' 칸 묶음이 없다")
            continue
        check_fields(f"{label} {group}", data, spec, problems)
    behaviors = (rules.get("monsterHp") or {}).get("alwaysShowBehaviors")
    if isinstance(behaviors, list):
        for b in behaviors:
            if b not in FEEDBACK_BEHAVIORS:
                problems.append(f"{label} monsterHp: alwaysShowBehaviors '{b}' — {list(FEEDBACK_BEHAVIORS)} 중 하나여야 한다")
        if len(set(behaviors)) != len(behaviors):
            problems.append(f"{label} monsterHp: alwaysShowBehaviors {behaviors} — 중복이 없어야 한다")


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


def report(root, art_root):
    problems, notes = check(root, art_root)
    for line in notes:
        print(line)
    if problems:
        print(f"DATA: FAIL ({len(problems)}건)")
        for p in problems:
            print(f"  - {p}")
        return 1
    print("DATA: OK")
    return 0


def self_test(root, art_root):
    """틀린 데이터가 종류마다 실패하고, 지금 데이터는 통과하는지. 임시 폴더에 Resources/Data 사본과 Art/AIGenerated의 빈 PNG 자리를 만든다."""
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

    def break_weight_target(d):
        edit_json(d / "Wave" / "wave01.json", lambda data: data["typeWeights"].append({"monster": "no_such_monster", "weight": 1}))

    def break_behavior(d):
        edit_json(d / "Monster" / "slime.json", lambda data: data.update(behavior="fly"))

    def break_behavior_group(d):
        edit_json(d / "Monster" / "mushroom.json", lambda data: data.pop("projectile"))

    def break_weight_zero(d):
        edit_json(d / "Wave" / "wave01.json", lambda data: data["typeWeights"][0].update(weight=0))

    def break_monster_duplicate(d):
        shutil.copy(d / "Monster" / "slime.json", d / "Monster" / "slime_copy.json")

    def break_wave_weights(d):
        edit_json(d / "Wave" / "wave03.json", lambda data: data.pop("typeWeights"))
    def break_grade_mult(d):
        edit_json(d / "Grade" / "grades.json", lambda data: data["grades"][2].update(mult=1.2))

    def break_loot_count(d):
        edit_json(d / "Loot" / "loot_rules.json", lambda data: data.update(minCount=6, maxCount=5))

    def break_loot_weight(d):
        for path in sorted((d / "Item").glob("*.json")):
            edit_json(path, lambda data: data.update(lootWeight=0))

    def break_start_cell(d):
        edit_json(d / "Bag" / "bag_rules.json", lambda data: data.update(startCell={"x": 3, "y": 3}))

    def break_feedback_max(d):
        edit_json(d / "Feedback" / "feedback_rules.json", lambda data: data["damageNumber"].update(maxActive=0))

    def break_feedback_behavior(d):
        edit_json(d / "Feedback" / "feedback_rules.json", lambda data: data["monsterHp"].update(alwaysShowBehaviors=["boss"]))

    def break_feedback_color(d):
        edit_json(d / "Feedback" / "feedback_rules.json", lambda data: data["hpLoss"].update(color="red"))

    def tmp_art(d):
        return default_art_root(d) / "AIGenerated"

    def break_character_art(d):
        edit_json(d / "Character" / "bruno.json", lambda data: data.update(sprite="nobody"))

    def break_character_back(d):
        (tmp_art(d) / "Characters" / "lena" / "back" / "lena_back.png").unlink()

    def break_monster_art(d):
        edit_json(d / "Monster" / "slime.json", lambda data: data.update(sprite="ghost"))

    def break_sprite_key(d):
        edit_json(d / "Monster" / "slime.json", lambda data: data.update(sprite="../slime"))

    def break_monster_sprite_field(d):
        edit_json(d / "Monster" / "boar.json", lambda data: data.pop("sprite"))

    def break_move_style(d):
        edit_json(d / "Monster" / "puffer.json", lambda data: data.update(moveStyle="fly"))

    cases = [("id 중복", break_duplicate, "중복"), ("없는 startItem", break_start_item, "startItem"),
             ("웨이브 order 빈칸", break_wave_gap, "Wave: order"), ("캐릭터 수 4 아님", break_character_count, "Character: 3개"),
             ("진형 자리 3개", break_formation_slot_count, "slots 3개"),
             ("진형 order 빈칸", break_formation_order_gap, "Formation: order"),
             ("자리 평균 ≠ 0", break_formation_mean, "자리 평균"),
             ("memberSpots 남음", break_member_spots, "memberSpots"),
             ("모르는 mode", break_mode, "'mode'"), ("interval 0인 발동 아이템", break_interval, "'interval'"),
             ("4×4 밖이거나 끊긴 shape", break_shape, "'shape'"), ("모르는 효과 종류", break_effect, "'baseEffect'"),
             ("passive 시작 아이템", break_passive_start, "passive"),
             ("없는 몬스터를 가리키는 가중치", break_weight_target, "몬스터 'no_such_monster'가 없다"),
             ("다섯 밖의 행동", break_behavior, "'behavior' = 'fly'"),
             ("행동 칸 묶음 빠짐", break_behavior_group, "'projectile' 칸 묶음이 없다"),
             ("weight 0", break_weight_zero, "weight = 0"),
             ("몬스터 id 중복", break_monster_duplicate, "Monster: id 'slime' 중복"),
             ("가중치 없는 웨이브", break_wave_weights, "wave03.json: 'typeWeights' 칸이 없다"),
             ("등급 배수 줄어듦", break_grade_mult, "'mult'"), ("minCount > maxCount", break_loot_count, "minCount"),
             ("모든 lootWeight 0", break_loot_weight, "lootWeight > 0"), ("시작 칸 (3, 3)", break_start_cell, "시작 칸 (3, 3)"),
             ("피해 숫자 maxActive 0", break_feedback_max, "damageNumber: 'maxActive'"),
             ("다섯 밖의 alwaysShowBehaviors", break_feedback_behavior, "alwaysShowBehaviors 'boss'"),
             ("색 형식 틀림", break_feedback_color, "hpLoss: 'color'"),
             ("그림 파일이 없는 캐릭터 키", break_character_art, "sprite 'nobody'의 그림"),
             ("뒤 그림만 없는 캐릭터 키", break_character_back, "lena_back.png이 없다"),
             ("그림 파일이 없는 몬스터 키", break_monster_art, "sprite 'ghost'의 그림"),
             ("형식이 틀린 키(../slime)", break_sprite_key, "'sprite' = '../slime'"),
             ("sprite 칸이 없는 몬스터", break_monster_sprite_field, "boar.json: 'sprite' 칸이 없다"),
             ("모르는 moveStyle", break_move_style, "'moveStyle' = 'fly'")]
    failures = []
    base_problems, _ = check(root, art_root)
    if base_problems:
        failures.append("지금 데이터가 통과하지 않는다: " + "; ".join(base_problems))
    for name, breaker, expected in cases:
        with tempfile.TemporaryDirectory() as tmp:
            copy = Path(tmp) / "Resources" / "Data"
            shutil.copytree(root, copy, ignore=shutil.ignore_patterns("*.meta"))
            for png in (art_root / "AIGenerated").rglob("*.png"):
                place = tmp_art(copy) / png.relative_to(art_root / "AIGenerated")
                place.parent.mkdir(parents=True, exist_ok=True)
                place.touch()
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
    art_root = None
    if "--art-root" in args:
        i = args.index("--art-root")
        if i + 1 >= len(args):
            print("--art-root 뒤에 폴더가 없다", file=sys.stderr)
            return 2
        art_root = Path(args[i + 1]).resolve()
        del args[i:i + 2]
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
    art_root = default_art_root(root) if art_root is None else art_root
    return self_test(root, art_root) if is_self_test else report(root, art_root)


if __name__ == "__main__":
    sys.exit(main(sys.argv))

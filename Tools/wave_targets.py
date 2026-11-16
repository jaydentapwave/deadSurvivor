#!/usr/bin/env python3
"""웨이브 목표값 판정기(에디터 없이). 기획서: docs/features/f4-monsters/plan.md D절, 목표: docs/game/gdd.md 9절.

  python3 -B ./tools/wave_targets.py                       데이터로 잴 수 있는 목표(M2·M3)를 모든 웨이브에서 본다
  python3 -B ./tools/wave_targets.py --waves 1,3,6         고른 웨이브만 본다
  python3 -B ./tools/wave_targets.py --metrics <파일>      진입점 기록(PlayableMetricsRecorder JSON)의 M2·M3·M5·M7·실행 = 데이터도 본다
  python3 -B ./tools/wave_targets.py --require M5,M7       고른 목표가 미측정이면 실패로 본다(회귀용)
  python3 -B ./tools/wave_targets.py --root <데이터 폴더>   다른 데이터 폴더(Wave/ Monster/ Run/)를 본다
  python3 -B ./tools/wave_targets.py --self-test           틀린 데이터·기록이 각각 FAIL, 맞는 것이 OK인지 본다

목표(게임 기획서 9절에서 옮김. 콘텐츠 대기열은 이 표를 고치지 않는다):

  | 목표 | 범위                                             | 데이터 판정                         | 기록 판정(--metrics)                              |
  |------|--------------------------------------------------|-------------------------------------|---------------------------------------------------|
  | M2   | 웨이브 1 98 → 마지막 웨이브 407 직선, 각 ±5%     | round(spawnCount × 기본 적 수 배율) | 배율 1 판의 끝까지 간 웨이브의 정규 생성 수        |
  | M3   | 첫 등장: projectile 3, charge 4, armored 5, bomb 6 | 가중치 > 0인 첫 웨이브              | 배율 1, 웨이브 1부터 기록한 판의 실제 첫 웨이브    |
  | M5   | 가만히 선 원정대가 지는 웨이브 2~5               | 미측정                              | 배율 1, 봇 Move 없음, 진 판의 버틴 웨이브 + 1      |
  | M7   | 마지막 웨이브 버닝 타임 프레임 95백분위 ≤ 16.7ms | 미측정                              | 배율 1 판의 표본(살아 있는 적 최대 수 함께 출력)   |
  | 실행 = 데이터 | 정규 생성 수 = round(spawnCount × 그 판 배율) | —                                | 모든 판의 끝까지 간 웨이브                          |

출력: 행마다 "목표 웨이브/행동 값 목표 OK|FAIL|미측정".
종료 코드: 0 통과, 1 목표 밖(또는 --require 목표 미측정), 2 돌릴 수 없음(파일 없음·형식 오류).
"""
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

DEFAULT_ROOT = Path(__file__).resolve().parent.parent / "Assets" / "Game" / "03.Resources" / "Resources" / "Data"
M2_FIRST, M2_LAST, M2_TOLERANCE = 98, 407, 0.05
M3_FIRST_WAVE = {"projectile": 3, "charge": 4, "armored": 5, "bomb": 6}
M5_RANGE = (2, 5)
M7_LIMIT_MS = 16.7
BOT_MOVE = 1
OK, FAIL, UNMEASURED = "OK", "FAIL", "미측정"


class InputError(Exception):
    pass


def read_json(path):
    if not path.is_file():
        raise InputError(f"파일이 없다: {path}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except ValueError as e:
        raise InputError(f"JSON 형식 오류: {path}: {e}")


def load_data(root):
    """(웨이브 목록 order 순, 몬스터 id → 행동, 기본 적 수 배율)."""
    if not root.is_dir():
        raise InputError(f"데이터 폴더가 없다: {root}")
    behaviors = {}
    for path in sorted((root / "Monster").glob("*.json")):
        data = read_json(path)
        behaviors[data.get("id")] = data.get("behavior")
    waves = [read_json(path) for path in sorted((root / "Wave").glob("*.json"))]
    if not waves:
        raise InputError(f"웨이브 파일이 없다: {root / 'Wave'}")
    for wave in waves:
        for key in ("order", "spawnCount", "typeWeights"):
            if key not in wave:
                raise InputError(f"웨이브 {wave.get('id')}: '{key}' 칸이 없다")
    waves.sort(key=lambda w: w["order"])
    scale = read_json(root / "Run" / "run_rules.json").get("defaultEnemyCountScale")
    if not isinstance(scale, (int, float)):
        raise InputError("run_rules.json: defaultEnemyCountScale이 수가 아니다")
    return waves, behaviors, scale


def m2_target(wave, wave_count):
    if wave_count <= 1:
        return M2_FIRST
    return round(M2_FIRST + (M2_LAST - M2_FIRST) * (wave - 1) / (wave_count - 1))


def is_m2_near(count, target):
    return abs(count - target) <= M2_TOLERANCE * target


def wave_behaviors(wave, behaviors):
    return {behaviors.get(e.get("monster")) for e in wave["typeWeights"] if (e.get("weight") or 0) > 0}


def data_rows(waves, behaviors, scale, selected):
    rows = []
    wave_count = len(waves)
    for n in selected:
        count = round(waves[n - 1]["spawnCount"] * scale)
        target = m2_target(n, wave_count)
        rows.append(("M2", f"웨이브 {n}", count, f"{target}±{M2_TOLERANCE:.0%}", OK if is_m2_near(count, target) else FAIL))
    for behavior, target in M3_FIRST_WAVE.items():
        found = [n for n in selected if behavior in wave_behaviors(waves[n - 1], behaviors)]
        first = min(found) if found else 0
        is_early = any(n < target for n in found)
        if target in selected:
            is_ok = target in found and not is_early
        else:
            is_ok = not is_early
        rows.append(("M3", behavior, first, f"웨이브 {target}" + ("" if target in selected else "(고른 목록 밖: 앞선 등장만 봄)"),
                     OK if is_ok else FAIL))
    rows.append(("M5", "데이터", "-", f"{M5_RANGE[0]}~{M5_RANGE[1]}", UNMEASURED))
    rows.append(("M7", "데이터", "-", f"≤{M7_LIMIT_MS}ms", UNMEASURED))
    return rows


def metric_rows(waves, metrics):
    runs = metrics.get("runs") if isinstance(metrics, dict) else None
    if not isinstance(runs, list):
        raise InputError("기록 파일에 runs 목록이 없다")
    wave_count = len(waves)
    check_runs(runs, wave_count)
    rows = []
    as_data = []
    for k, run in enumerate(runs, 1):
        for w in run.get("waves", []):
            if not w.get("reachedEnd"):
                continue
            expected = round(waves[w["wave"] - 1]["spawnCount"] * run["enemyCountScale"])
            as_data.append(("실행 = 데이터", f"판 {k} 웨이브 {w['wave']}", w["spawned"], expected,
                            OK if w["spawned"] == expected else FAIL))
    rows += as_data or [("실행 = 데이터", "기록", "-", "끝까지 간 웨이브", UNMEASURED)]

    full = [(k, run) for k, run in enumerate(runs, 1) if run.get("enemyCountScale") == 1]
    m2 = []
    for k, run in full:
        for w in run.get("waves", []):
            if w.get("reachedEnd"):
                target = m2_target(w["wave"], wave_count)
                m2.append(("M2(기록)", f"판 {k} 웨이브 {w['wave']}", w["spawned"], f"{target}±{M2_TOLERANCE:.0%}",
                           OK if is_m2_near(w["spawned"], target) else FAIL))
    rows += m2 or [("M2(기록)", "배율 1 판", "-", "끝까지 간 웨이브", UNMEASURED)]

    m3 = []
    for k, run in full:
        if run.get("firstWave") != 1:
            continue
        recorded = run.get("waves", [])
        last_wave = max((w["wave"] for w in recorded), default=0)
        for behavior, target in M3_FIRST_WAVE.items():
            first = next((w["wave"] for w in sorted(recorded, key=lambda w: w["wave"]) if w.get(behavior, 0) > 0), 0)
            if first == 0 and last_wave < target:
                m3.append(("M3(기록)", f"판 {k} {behavior}", "-", f"웨이브 {target}(웨이브 {last_wave}에서 끝남)", UNMEASURED))
            else:
                m3.append(("M3(기록)", f"판 {k} {behavior}", first, f"웨이브 {target}", OK if first == target else FAIL))
    rows += m3 or [("M3(기록)", "배율 1·웨이브 1부터 기록한 판", "-", "첫 등장", UNMEASURED)]

    m5 = []
    for k, run in full:
        if run.get("botModes", 0) & BOT_MOVE or run.get("outcome") != "Lose":
            continue
        lost_wave = run["wavesSurvived"] + 1
        m5.append(("M5", f"판 {k} 진 웨이브", lost_wave, f"{M5_RANGE[0]}~{M5_RANGE[1]}",
                   OK if M5_RANGE[0] <= lost_wave <= M5_RANGE[1] else FAIL))
    rows += m5 or [("M5", "배율 1·이동 없음·진 판", "-", f"{M5_RANGE[0]}~{M5_RANGE[1]}", UNMEASURED)]

    m7 = []
    for k, run in full:
        burning = run.get("burning") or {}
        if burning.get("samples", 0) <= 0:
            continue
        p95 = burning["p95Ms"]
        m7.append(("M7", f"판 {k} 웨이브 {wave_count} 버닝(표본 {burning['samples']}, 적 최대 {burning.get('maxAlive')})",
                   f"{p95:.2f}ms", f"≤{M7_LIMIT_MS}ms", OK if p95 <= M7_LIMIT_MS else FAIL))
    rows += m7 or [("M7", "배율 1 판의 마지막 웨이브 버닝", "-", f"≤{M7_LIMIT_MS}ms", UNMEASURED)]
    return rows


def check_runs(runs, wave_count):
    """기록 형식(기획서 D절: 형식 오류는 exit 2). 판정이 읽는 칸의 타입과 웨이브 범위만 본다."""
    def is_num(v):
        return isinstance(v, (int, float)) and not isinstance(v, bool)

    def is_int(v):
        return isinstance(v, int) and not isinstance(v, bool)

    for k, run in enumerate(runs, 1):
        if not isinstance(run, dict):
            raise InputError(f"기록 판 {k}: 객체가 아니다")
        for key, ok in (("enemyCountScale", is_num), ("firstWave", is_int), ("botModes", is_int),
                        ("wavesSurvived", is_int), ("waves", lambda v: isinstance(v, list))):
            if key in run and not ok(run[key]) or key in ("enemyCountScale", "waves") and key not in run:
                raise InputError(f"기록 판 {k}: '{key}' 칸이 없거나 형식이 틀렸다")
        for w in run["waves"]:
            if not isinstance(w, dict) or not is_int(w.get("wave")) or not 1 <= w["wave"] <= wave_count \
                    or not is_int(w.get("spawned")) or not isinstance(w.get("reachedEnd"), bool):
                raise InputError(f"기록 판 {k}: 웨이브 기록 {w!r} — wave 1~{wave_count} 정수, spawned 정수, reachedEnd true/false여야 한다")
        burning = run.get("burning")
        if burning is not None and (not isinstance(burning, dict) or not is_int(burning.get("samples", 0))
                                    or burning.get("samples", 0) > 0 and not is_num(burning.get("p95Ms"))):
            raise InputError(f"기록 판 {k}: 'burning' 형식이 틀렸다")


def judge(root, selected_text, metrics_path, required):
    waves, behaviors, scale = load_data(root)
    if selected_text is None:
        selected = list(range(1, len(waves) + 1))
    else:
        try:
            selected = sorted({int(t) for t in selected_text.split(",") if t.strip()})
        except ValueError:
            raise InputError(f"--waves 형식 오류: {selected_text}")
        if not selected or selected[0] < 1 or selected[-1] > len(waves):
            raise InputError(f"--waves는 1~{len(waves)} 안이어야 한다: {selected_text}")
    rows = data_rows(waves, behaviors, scale, selected)
    if metrics_path is not None:
        rows += metric_rows(waves, read_json(metrics_path))
    for goal in required:
        goal_rows = [r for r in rows if r[0].split("(")[0] == goal]
        if all(r[4] == UNMEASURED for r in goal_rows):
            rows.append((goal, "--require", "-", "측정 필요", FAIL))
    for goal, subject, value, target, status in rows:
        print(f"{goal} {subject} {value} {target} {status}")
    failures = [r for r in rows if r[4] == FAIL]
    print(f"TARGETS: {'FAIL' if failures else 'OK'} (행 {len(rows)}개, FAIL {len(failures)}개)")
    return 1 if failures else 0


# --- 자체 시험 -------------------------------------------------------------

LEGACY_COUNTS = [98, 132, 167, 201, 235, 270, 304, 338, 372, 407]
LEGACY_WEIGHTS = [
    [("slime", 100)], [("slime", 100)], [("slime", 80), ("mushroom", 20)],
    [("slime", 68), ("mushroom", 20), ("boar", 12)], [("slime", 60), ("mushroom", 20), ("boar", 12), ("rock", 8)],
] + [[("slime", 55), ("mushroom", 20), ("boar", 12), ("rock", 8), ("puffer", 5)]] * 5
BEHAVIOR_OF = {"slime": "contact", "mushroom": "projectile", "boar": "charge", "rock": "armored", "puffer": "bomb"}


def good_metrics():
    def wave_row(n, reached=True):
        row = {"wave": n, "reachedEnd": reached, "spawned": LEGACY_COUNTS[n - 1], "expected": LEGACY_COUNTS[n - 1],
               "burningSpawned": 0}
        for behavior in M3_FIRST_WAVE.keys() | {"contact"}:
            row[behavior] = 0
        for monster, _ in LEGACY_WEIGHTS[n - 1]:
            row[BEHAVIOR_OF[monster]] = 10
        return row
    win = {"seed": 7, "enemyCountScale": 1, "firstWave": 1, "botModes": 7, "outcome": "Win", "wavesSurvived": 10,
           "maxAliveEnemies": 120, "waves": [wave_row(n) for n in range(1, 11)],
           "burning": {"p95Ms": 12.0, "meanMs": 9.0, "maxMs": 30.0, "samples": 600, "maxAlive": 120}}
    stand = {"seed": 5, "enemyCountScale": 1, "firstWave": 1, "botModes": 4, "outcome": "Lose", "wavesSurvived": 2,
             "maxAliveEnemies": 80, "waves": [wave_row(1), wave_row(2), wave_row(3, reached=False)],
             "burning": {"p95Ms": -1, "meanMs": -1, "maxMs": -1, "samples": 0, "maxAlive": 0}}
    return {"runs": [win, stand]}


def self_test(root):
    def write_json(path, data):
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")

    def make_good(data_dir):
        for n, path in enumerate(sorted((data_dir / "Wave").glob("*.json"), key=lambda p: read_json(p)["order"]), 1):
            wave = read_json(path)
            wave["spawnCount"] = LEGACY_COUNTS[n - 1]
            wave["typeWeights"] = [{"monster": m, "weight": w} for m, w in LEGACY_WEIGHTS[n - 1]]
            write_json(path, wave)

    def edit_wave(data_dir, n, change):
        path = next(p for p in (data_dir / "Wave").glob("*.json") if read_json(p)["order"] == n)
        wave = read_json(path)
        change(wave)
        write_json(path, wave)

    def bad_count(d, m):
        edit_wave(d, 2, lambda w: w.update(spawnCount=round(132 * 1.06)))

    def bad_projectile(d, m):
        edit_wave(d, 2, lambda w: w["typeWeights"].append({"monster": "mushroom", "weight": 20}))

    def bad_as_data(d, m):
        m["runs"][0]["waves"][3]["spawned"] -= 1

    def bad_stand(d, m):
        m["runs"][1].update(wavesSurvived=0, waves=m["runs"][1]["waves"][:1])
        m["runs"][1]["waves"][0]["reachedEnd"] = False

    def bad_frame(d, m):
        m["runs"][0]["burning"]["p95Ms"] = 20.0

    def bad_record(d, m):
        m["runs"][0]["waves"][0]["wave"] = 11

    cases = [
        ("맞는 데이터·기록", None, [], 0, None),
        ("적 수 웨이브 2 목표 +6%", bad_count, [], 1, "M2 웨이브 2 140"),
        ("사격형 첫 등장 웨이브 2", bad_projectile, [], 1, "M3 projectile 2"),
        ("기록 생성 수 ≠ 데이터", bad_as_data, [], 1, "실행 = 데이터 판 1 웨이브 4"),
        ("가만히 선 판이 웨이브 1에서 짐", bad_stand, [], 1, "M5 판 2 진 웨이브 1"),
        ("프레임 95백분위 20ms", bad_frame, [], 1, "M7 판 1"),
        ("--require M5, 기록 없음", "no-metrics", ["--require", "M5"], 1, "M5 --require"),
        ("기록 웨이브 번호가 범위 밖(11)", bad_record, [], 2, "돌릴 수 없음: 기록 판 1"),
    ]
    failures = []
    for name, breaker, extra, expected_exit, expected_line in cases:
        with tempfile.TemporaryDirectory() as tmp:
            data_dir = Path(tmp) / "Data"
            shutil.copytree(root, data_dir, ignore=shutil.ignore_patterns("*.meta"))
            make_good(data_dir)
            metrics = good_metrics()
            if callable(breaker):
                breaker(data_dir, metrics)
            args = [sys.executable, "-B", __file__, "--root", str(data_dir), *extra]
            if breaker != "no-metrics":
                metrics_path = Path(tmp) / "metrics.json"
                write_json(metrics_path, metrics)
                args += ["--metrics", str(metrics_path), "--require", "M5,M7"]
            result = subprocess.run(args, capture_output=True, text=True)
            lines = result.stdout.splitlines()
            if expected_exit == 2:
                lines = result.stderr.splitlines()
                hit = [l for l in lines if l.startswith(expected_line)]
            elif expected_line is None:
                hit = [l for l in lines if l.startswith("TARGETS: OK")]
            else:
                hit = [l for l in lines if l.startswith(expected_line) and l.endswith(FAIL)]
            is_ok = result.returncode == expected_exit and hit
            print(f"  {name}: exit {result.returncode}, {'맞음' if is_ok else '틀림'} — {hit[0] if hit else lines[-1:]}")
            if not is_ok:
                failures.append(f"{name}: exit {result.returncode}(기대 {expected_exit}), 기대 행 {expected_line!r} "
                                f"{'있음' if hit else '없음'}\n" + result.stdout + result.stderr)
    if failures:
        print("SELF-TEST: FAIL")
        for f in failures:
            print(f"  - {f}")
        return 1
    print(f"SELF-TEST: OK ({len(cases)}종: 맞는 것 통과, 틀린 것 모두 FAIL)")
    return 0


def main(argv):
    args = argv[1:]

    def take(flag):
        if flag not in args:
            return None
        i = args.index(flag)
        if i + 1 >= len(args):
            raise InputError(f"{flag} 뒤에 값이 없다")
        value = args[i + 1]
        del args[i:i + 2]
        return value

    try:
        root_text = take("--root")
        root = Path(root_text).resolve() if root_text else DEFAULT_ROOT
        waves_text = take("--waves")
        metrics_text = take("--metrics")
        required = []
        while "--require" in args:
            required += [g.strip() for g in take("--require").split(",") if g.strip()]
        is_self_test = "--self-test" in args
        if is_self_test:
            args.remove("--self-test")
        if args:
            print(__doc__.strip(), file=sys.stderr)
            return 2
        if is_self_test:
            return self_test(root)
        return judge(root, waves_text, Path(metrics_text) if metrics_text else None, required)
    except InputError as e:
        print(f"돌릴 수 없음: {e}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))

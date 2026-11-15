#!/usr/bin/env python3
"""플레이 가능 판정: 에디터 Play에서 판정 경로(route.json)를 처음부터 끝까지 밟아 본다.

"기능이 다 채택됐다"가 아니라 "켜서 한 판이 돈다"를 확인한다. 대기열의 회귀 검사(regress)와
게임 단위 진행의 종료 조건으로 쓴다. 손으로 돌려도 된다.

  python3 -B <유니티 프로젝트>/tools/playable.py                 경로 전체를 밟는다
  python3 -B <유니티 프로젝트>/tools/playable.py --check         경로 파일의 형식만 본다(에디터를 건드리지 않는다)
  python3 -B <유니티 프로젝트>/tools/playable.py --until <번호>  그 단계까지만 밟는다(만드는 중에 쓴다)

선택: --project <유니티 프로젝트>(기본: 이 파일의 윗폴더), --route <파일>(기본: tools/playable/route.json),
      --out <폴더>(기본: <프로젝트>/Logs/playable/latest. 캡처와 report.json이 생긴다)

route.json:
  {
    "schema": 1,
    "scene": "Assets/Game/01.Scene/Main.unity",   (선택) Play 전에 열 씬
    "size": [1080, 1920],                          (선택) 캡처 크기. 기본 1080x1920
    "sizes": [[1080, 1920], [1080, 2340]],         (선택) "capture": "all"일 때 찍을 크기들. 기본은 9:16, 9:19.5, 9:21, 3:4
    "time_scale": 1,                               (선택) Play 중 시간 배율
    "steps": [
      {"id": "G1", "name": "시작 화면", "wait": "<C# bool 식>", "timeout": 20, "capture": "all"},
      {"id": "G2", "name": "한 판 시작", "do": "<C# 문장>", "wait": "<C# bool 식>"},
      {"id": "G3", "name": "점프", "key": "Space"},
      {"id": "G4", "name": "결과 화면", "wait": "<C# bool 식>", "timeout": 120, "expect": "<C# bool 식>", "capture": true}
    ]
  }

단계 하나는 적힌 것만 이 순서로 한다: do(C# 실행) → key(키 입력) → tap([가로, 세로] 0~1, 왼쪽 아래가 0,0) →
sleep(초) → wait(식이 참이 될 때까지, timeout 초·기본 10) → expect(식이 지금 참인가) → capture(true 또는 "all").
단계가 끝날 때마다 콘솔 오류 수를 본다. 늘었으면 그 단계에서 실패한다("allow_errors": true면 넘어간다).

C# 식과 문장은 게임이 내놓는 판정용 진입점(.ai/rules/playable-check.md)을 부른다. 화면 좌표를 누르는 tap은
에디터 창이 OS 포커스를 잃으면 UI 버튼을 못 누르므로, 버튼은 do로 누른다.

끝나면 항상 Play를 끈다. 시작할 때 이미 Play 중이면 끄고 새로 시작한다.

종료 코드: 0 통과, 1 실패(어느 단계에서 끊겼는지 출력), 2 실행할 수 없음(unity 명령·프로젝트·경로 파일 문제, 에디터 미연결).
환경 변수: PLAYABLE_UNITY(unity 명령, 기본 unity), PLAYABLE_PLAY_TIMEOUT(Play 진입 대기 초, 기본 120).
"""
import json
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

UNITY = os.environ.get("PLAYABLE_UNITY", "unity")
PLAY_TIMEOUT = int(os.environ.get("PLAYABLE_PLAY_TIMEOUT", "120"))
POLL = float(os.environ.get("PLAYABLE_POLL", "0.5"))
DEFAULT_SIZES = [[1080, 1920], [1080, 2340], [1080, 2520], [1536, 2048]]  # 9:16, 9:19.5, 9:21, 3:4
STEP_KEYS = {"id", "name", "do", "key", "tap", "sleep", "wait", "timeout", "expect", "capture", "allow_errors"}
ACTIONS = ("do", "key", "tap", "sleep", "wait", "expect", "capture")
TOP_KEYS = {"schema", "scene", "size", "sizes", "time_scale", "steps"}


class CannotRun(Exception):
    """판정을 돌릴 수 없다(게임 탓이 아니다)."""


def is_size(v):
    return (isinstance(v, list) and len(v) == 2
            and all(isinstance(n, int) and not isinstance(n, bool) and 0 < n <= 4096 for n in v))


def is_number(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def check_route(route):
    """경로 파일의 형식 문제 목록. 비어 있으면 통과."""
    if not isinstance(route, dict):
        return ["최상위가 객체가 아니다"]
    problems = [f"모르는 키: {k}" for k in sorted(set(route) - TOP_KEYS)]
    if route.get("schema") != 1:
        problems.append("schema가 1이 아니다")
    if "scene" in route and not (isinstance(route["scene"], str) and route["scene"].endswith(".unity")):
        problems.append("scene은 .unity로 끝나는 경로여야 한다")
    if "size" in route and not is_size(route["size"]):
        problems.append("size는 [가로, 세로](1~4096 정수)여야 한다")
    if "sizes" in route and not (isinstance(route["sizes"], list) and route["sizes"]
                                 and all(is_size(s) for s in route["sizes"])):
        problems.append("sizes는 [가로, 세로]의 목록이어야 한다")
    if "time_scale" in route and not (is_number(route["time_scale"]) and 0 < route["time_scale"] <= 100):
        problems.append("time_scale은 0보다 크고 100 이하인 수여야 한다")
    steps = route.get("steps")
    if not isinstance(steps, list) or not steps:
        return problems + ["steps가 비어 있다"]
    ids = []
    for i, step in enumerate(steps, 1):
        if not isinstance(step, dict):
            problems.append(f"{i}번째 단계가 객체가 아니다")
            continue
        sid = step.get("id")
        label = sid if isinstance(sid, str) and sid else f"{i}번째 단계"
        if not (isinstance(sid, str) and re.fullmatch(r"[A-Za-z][A-Za-z0-9_-]*", sid)):
            problems.append(f"{label}: id가 없거나 모양이 틀리다(예: G1)")
        else:
            ids.append(sid)
        if not (isinstance(step.get("name"), str) and step["name"].strip()):
            problems.append(f"{label}: name이 없다")
        problems += [f"{label}: 모르는 키 {k}" for k in sorted(set(step) - STEP_KEYS)]
        if not any(k in step for k in ACTIONS):
            problems.append(f"{label}: 하는 일이 없다({', '.join(ACTIONS)} 중 하나 이상)")
        for k in ("do", "key", "wait", "expect"):
            if k in step and not (isinstance(step[k], str) and step[k].strip()):
                problems.append(f"{label}: {k}는 비어 있지 않은 문자열이어야 한다")
        if "tap" in step and not (isinstance(step["tap"], list) and len(step["tap"]) == 2
                                  and all(is_number(n) and 0 <= n <= 1 for n in step["tap"])):
            problems.append(f"{label}: tap은 [가로, 세로](0~1)여야 한다")
        for k in ("sleep", "timeout"):
            if k in step and not (is_number(step[k]) and 0 < step[k] <= 3600):
                problems.append(f"{label}: {k}는 0보다 크고 3600 이하인 초여야 한다")
        if "timeout" in step and "wait" not in step:
            problems.append(f"{label}: timeout은 wait와 함께 쓴다")
        if "capture" in step and step["capture"] not in (True, "all"):
            problems.append(f"{label}: capture는 true 또는 \"all\"이다")
        if "allow_errors" in step and not isinstance(step["allow_errors"], bool):
            problems.append(f"{label}: allow_errors는 true/false다")
    for dup in sorted({i for i in ids if ids.count(i) > 1}):
        problems.append(f"{dup}: id가 두 번 나온다")
    if not any(isinstance(s, dict) and ("wait" in s or "expect" in s) for s in steps):
        problems.append("확인(wait·expect)이 하나도 없다. 입력만 넣고 끝나는 경로는 판정이 아니다")
    return problems


def unity(*args, timeout=60):
    """(종료 코드, 출력). CLI가 스스로 끝나지 않으면 124."""
    try:
        r = subprocess.run([UNITY, "--json", "--non-interactive", "--no-banner", *args],
                           capture_output=True, text=True, timeout=timeout)
    except subprocess.TimeoutExpired:
        return 124, f"unity {' '.join(args[:3])} 명령이 {timeout}초 안에 끝나지 않았다"
    return r.returncode, (r.stdout + r.stderr).strip()


def tail(text, n=8):
    return "\n".join(str(text).splitlines()[-n:])


class Editor:
    def __init__(self, project):
        self.project = project

    def command(self, group, name, *args, timeout=60):
        return unity("command", group, name, "--project-path", self.project, *args, timeout=timeout)

    def eval(self, code, timeout=60):
        return self.command("script", "eval", "--code", code, "--timeout", str(int(timeout)), timeout=timeout + 30)

    def value(self, expr):
        """식의 값을 JSON으로. 못 얻으면 (None, 출력)."""
        rc, out = self.eval(f"return ({expr});")
        if rc:
            return None, out
        try:
            return json.loads(out.splitlines()[-1]), out
        except (ValueError, IndexError):
            return None, out

    def state(self):
        rc, out = unity("status", "--project-path", self.project, timeout=30)
        try:
            instances = (json.loads(out).get("data") or {}).get("instances") or []
        except ValueError:
            return None
        return instances[0].get("state") if instances else None

    def errors(self):
        """콘솔 오류 수. 읽지 못하면 None."""
        rc, out = self.command("console", "status", timeout=30)
        try:
            return int((json.loads(out).get("groundTruth") or {})["consoleErrors"])
        except (ValueError, KeyError, TypeError, AttributeError):
            return None

    def error_lines(self):
        rc, out = self.command("console", "get", "--level", "error", "--tail", "5", timeout=30)
        try:
            return [str(e.get("message") or e)[:300] for e in json.loads(out).get("entries") or []]
        except (ValueError, AttributeError):
            return [tail(out, 5)]

    def wait_playing(self, want, timeout):
        end = time.monotonic() + timeout
        while time.monotonic() < end:
            v, _ = self.value("UnityEngine.Application.isPlaying")
            if v is want:
                return True
            time.sleep(POLL)
        return False

    def capture(self, path, size):
        return self.command("capture", "game_view", "--width", str(size[0]), "--height", str(size[1]),
                            "--output-path", str(path), timeout=60)


def cs_string(text):
    return '"' + text.replace("\\", "\\\\").replace('"', '\\"') + '"'


def run_step(ed, step, route, out_dir, index):
    """(통과 여부, 이유, 캡처 경로들)"""
    shots = []
    if "do" in step:
        rc, out = ed.eval(step["do"])
        if rc:
            return False, "do 실행 실패:\n" + tail(out), shots
    if "key" in step:
        rc, out = ed.command("runtime", "simulate_key", "--key", step["key"])
        if rc:
            return False, "key 입력 실패:\n" + tail(out), shots
    if "tap" in step:
        size, out = ed.value('new int[] { UnityEngine.Screen.width, UnityEngine.Screen.height }')
        if not (isinstance(size, list) and len(size) == 2):
            return False, "화면 크기를 읽지 못했다:\n" + tail(out), shots
        x, y = int(step["tap"][0] * size[0]), int(step["tap"][1] * size[1])
        rc, out = ed.command("runtime", "simulate_pointer", "--x", str(x), "--y", str(y))
        if rc:
            return False, "tap 입력 실패:\n" + tail(out), shots
    if "sleep" in step:
        time.sleep(step["sleep"])
    if "wait" in step:
        limit = step.get("timeout", 10)
        end = time.monotonic() + limit
        last = ""
        while True:
            v, last = ed.value(step["wait"])
            if v is True:
                break
            if time.monotonic() >= end:
                return False, f"wait가 {limit}초 안에 참이 되지 않았다: {step['wait']}\n마지막 응답: {tail(last, 3)}", shots
            time.sleep(POLL)
    if "expect" in step:
        v, out = ed.value(step["expect"])
        if v is not True:
            return False, f"expect가 참이 아니다: {step['expect']}\n응답: {tail(out, 3)}", shots
    if "capture" in step:
        sizes = route.get("sizes", DEFAULT_SIZES) if step["capture"] == "all" else [route.get("size", DEFAULT_SIZES[0])]
        for w, h in sizes:
            path = out_dir / f"{index:02d}-{step['id']}-{w}x{h}.png"
            rc, out = ed.capture(path, (w, h))
            if rc or not path.exists():
                return False, f"캡처 실패({w}x{h}):\n" + tail(out), shots
            shots.append(str(path))
    return True, "", shots


def run(project, route, out_dir, until=None):
    """report 사전을 돌려준다. 돌릴 수 없으면 CannotRun."""
    if not shutil.which(UNITY):
        raise CannotRun(f"unity 명령이 없다 ({UNITY})")
    ed = Editor(project)
    st = ed.state()
    if st != "ready":
        raise CannotRun(f"에디터가 준비되지 않았다(상태: {st or '연결 없음'}). tools/editor_ready.py로 먼저 연다")
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True)

    report = {"schema": 1, "project": project, "started": time.strftime("%Y-%m-%dT%H:%M:%S%z"),
              "result": "fail", "broke_at": None, "reason": "", "steps": []}
    began = time.monotonic()
    try:
        v, _ = ed.value("UnityEngine.Application.isPlaying")
        if v is True:
            ed.command("editor.playmode", "stop")
            if not ed.wait_playing(False, 60):
                raise CannotRun("이미 켜져 있던 Play를 끄지 못했다")
        if route.get("scene"):
            rc, out = ed.eval("UnityEditor.SceneManagement.EditorSceneManager.OpenScene("
                              + cs_string(route["scene"]) + ");")
            if rc:
                raise CannotRun(f"씬을 열지 못했다({route['scene']}):\n" + tail(out))
        ed.command("console", "clear")
        rc, out = ed.command("editor.playmode", "start", timeout=PLAY_TIMEOUT)
        if rc or not ed.wait_playing(True, PLAY_TIMEOUT):
            report.update(broke_at="(Play 진입)", reason="Play에 들어가지 못했다:\n" + tail(out))
            return report
        if route.get("time_scale", 1) != 1:
            ed.command("runtime", "set_time_scale", "--scale", str(route["time_scale"]))
        seen = ed.errors() or 0
        if seen:
            report.update(broke_at="(Play 진입)", reason="Play에 들어가자마자 콘솔 오류:\n" + "\n".join(ed.error_lines()))
            return report

        for index, step in enumerate(route["steps"], 1):
            t0 = time.monotonic()
            ok, reason, shots = run_step(ed, step, route, out_dir, index)
            now = ed.errors()
            if ok and now is not None and now > seen and not step.get("allow_errors"):
                ok, reason = False, "콘솔 오류가 늘었다:\n" + "\n".join(ed.error_lines())
            seen = now if now is not None else seen
            report["steps"].append({"id": step["id"], "name": step["name"], "result": "pass" if ok else "fail",
                                    "seconds": round(time.monotonic() - t0, 1), "captures": shots,
                                    **({"reason": reason} if reason else {})})
            if not ok:
                shot = out_dir / f"{index:02d}-{step['id']}-fail.png"
                size = route.get("size", DEFAULT_SIZES[0])
                if not ed.capture(shot, size)[0] and shot.exists():
                    report["steps"][-1]["captures"].append(str(shot))
                report.update(broke_at=step["id"], reason=reason)
                return report
            if until and step["id"] == until:
                report["partial_until"] = until
                break
        report["result"] = "pass"
        return report
    finally:
        ed.command("editor.playmode", "stop")
        ed.wait_playing(False, 30)
        report["seconds"] = round(time.monotonic() - began, 1)


def main(argv):
    args = argv[1:]

    def take(flag):
        if flag in args:
            i = args.index(flag)
            if i + 1 >= len(args):
                raise CannotRun(f"{flag} 뒤에 값이 없다")
            value = args[i + 1]
            del args[i:i + 2]
            return value
        return None

    try:
        project_arg, route_arg, out_arg, until = take("--project"), take("--route"), take("--out"), take("--until")
        check_only = "--check" in args
        if check_only:
            args.remove("--check")
        if args:
            print(__doc__.strip(), file=sys.stderr)
            return 2
        project = Path(project_arg or Path(__file__).resolve().parent.parent).resolve()
        route_path = Path(route_arg).resolve() if route_arg else project / "tools" / "playable" / "route.json"
        if not route_path.is_file():
            raise CannotRun(f"판정 경로 파일이 없다: {route_path}\n게임 기획서의 판정 경로를 route.json으로 옮긴다"
                            "(.ai/rules/playable-check.md)")
        try:
            route = json.loads(route_path.read_text(encoding="utf-8"))
        except ValueError as e:
            raise CannotRun(f"{route_path}: JSON 문법 오류: {e}")
        problems = check_route(route)
        if problems:
            raise CannotRun(f"{route_path}:\n" + "\n".join(f"  - {p}" for p in problems))
        if until and until not in [s["id"] for s in route["steps"]]:
            raise CannotRun(f"--until {until}: 그런 단계가 없다")
        if check_only:
            print(f"{route_path}: OK ({len(route['steps'])}단계)")
            return 0
        if not (project / "ProjectSettings").is_dir():
            raise CannotRun(f"유니티 프로젝트가 아니다: {project}")
        out_dir = Path(out_arg).resolve() if out_arg else project / "Logs" / "playable" / "latest"
        report = run(str(project), route, out_dir, until)
    except CannotRun as e:
        print(f"PLAYABLE: CANNOT-RUN\n{e}")
        return 2

    (out_dir / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    done = sum(1 for s in report["steps"] if s["result"] == "pass")
    total = len(route["steps"])
    if report["result"] == "pass":
        scope = f"{report['partial_until']}까지 " if report.get("partial_until") else ""
        print(f"PLAYABLE: {'PARTIAL' if scope else 'PASS'}\n{scope}{done}/{total}단계 통과, {report['seconds']}초")
    else:
        print(f"PLAYABLE: FAIL\n끊긴 곳: {report['broke_at']} ({done}/{total}단계 통과)\n{report['reason']}")
    print(f"보고서: {out_dir / 'report.json'}")
    return 0 if report["result"] == "pass" else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))

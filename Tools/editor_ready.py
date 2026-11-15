#!/usr/bin/env python3
"""유니티 에디터가 명령을 받을 수 있는지 확인하고, 꺼졌거나 멈췄으면 다시 연다.

대기열 러너의 준비 확인(queue.yaml의 ready)에 적어 쓴다. 손으로 돌려도 된다.

  python3 -B <유니티 프로젝트>/tools/editor_ready.py [<유니티 프로젝트 경로>]
  python3 -B <유니티 프로젝트>/tools/editor_ready.py --help

순서:
  1. 이 프로젝트를 연 에디터가 없으면 연다(unity open --until-ready).
     에디터 프로세스는 떠 있는데 unity status에 아직 안 나오면(처음 열 때 패키지 받기·가져오기에 몇 분 걸린다)
     꺼진 것으로 보지 않고 나올 때까지 기다린다. 상한(EDITOR_READY_OPEN_TIMEOUT)을 넘기면 실패로 끝낸다.
     가져오는 중인 에디터를 닫지 않는다.
  2. 있는데 준비가 안 됐으면 기다린다. 끝내 안 되면 닫고 다시 연다.
  3. 스크립트를 다시 컴파일해 오류가 0개인지 본다(unity recompile).

컴파일 오류는 다시 열어도 고쳐지지 않으므로 다시 열지 않고 실패로 끝낸다.
다시 열기는 한 번만 한다. 저장하지 않은 씬 변경은 닫을 때 버려진다.

종료 코드: 0 준비됨, 1 준비 안 됨(이유를 출력), 2 실행할 수 없음(unity 명령이나 프로젝트가 없음).
환경 변수: EDITOR_READY_UNITY(unity 명령, 기본 unity), EDITOR_READY_OPEN_TIMEOUT(초, 기본 600),
EDITOR_READY_COMPILE_TIMEOUT(초, 기본 300), EDITOR_READY_POLL(연결을 기다리며 다시 묻는 간격 초, 기본 5),
EDITOR_READY_PS(프로세스 목록 명령, 기본 ps -axo command=. 이 명령을 못 돌리는 환경에서는 프로세스 확인을 건너뛴다).
"""
import json
import os
import shlex
import shutil
import subprocess
import sys
import time
from pathlib import Path

UNITY = os.environ.get("EDITOR_READY_UNITY", "unity")
OPEN_TIMEOUT = int(os.environ.get("EDITOR_READY_OPEN_TIMEOUT", "600"))
COMPILE_TIMEOUT = int(os.environ.get("EDITOR_READY_COMPILE_TIMEOUT", "300"))
POLL = float(os.environ.get("EDITOR_READY_POLL", "5"))
PS = shlex.split(os.environ.get("EDITOR_READY_PS", "ps -axo command="))


def unity(*args, timeout):
    """(종료 코드, 출력). CLI가 스스로 끝나지 않으면 124."""
    try:
        r = subprocess.run([UNITY, "--json", "--non-interactive", "--no-banner", *args],
                           capture_output=True, text=True, timeout=timeout + 60)
    except subprocess.TimeoutExpired:
        return 124, f"unity {args[0]} 명령이 {timeout + 60}초 안에 끝나지 않았다"
    return r.returncode, (r.stdout + r.stderr).strip()


def tail(text, n=20):
    return "\n".join(text.splitlines()[-n:])


def state(project):
    """이 프로젝트를 연 에디터의 상태. 없으면 None."""
    _, out = unity("status", "--project-path", project, timeout=30)
    try:
        instances = (json.loads(out).get("data") or {}).get("instances") or []
    except ValueError:
        return None
    return instances[0].get("state") if instances else None


def process_up(project):
    """이 프로젝트를 연 에디터 프로세스가 떠 있는가(-projectPath <이 프로젝트>로 뜬 프로세스). 알 수 없으면 False."""
    try:
        r = subprocess.run(PS, capture_output=True, text=True, timeout=30)
    except (OSError, subprocess.TimeoutExpired):
        return False
    paths = {project.rstrip("/"), str(Path(project).resolve()).rstrip("/")}
    for line in r.stdout.splitlines():
        low = line.lower()
        at = low.find("-projectpath")
        if at < 0:
            continue
        rest = line[at + len("-projectpath"):].strip().strip("\"'")
        for p in paths:
            if rest == p or rest.startswith((p + " ", p + "/ ", p + '"', p + "'")) or rest == p + "/":
                return True
    return False


def wait_connect(project):
    """떠 있는 에디터가 unity status에 나올 때까지 기다린다. (상태, 프로세스가 아직 떠 있는가)."""
    deadline = time.monotonic() + OPEN_TIMEOUT
    while True:
        st = state(project)
        if st is not None:
            return st, True
        if not process_up(project):
            return None, False
        if time.monotonic() >= deadline:
            return None, True
        time.sleep(POLL)


def open_editor(project):
    return unity("open", project, "--until-ready", "--timeout", str(OPEN_TIMEOUT), timeout=OPEN_TIMEOUT)


def reopen(project):
    unity("close", project, "--force", "--timeout", "30", timeout=60)
    return open_editor(project)


def recompile(project):
    return unity("recompile", "--project-path", project, "--timeout", str(COMPILE_TIMEOUT), timeout=COMPILE_TIMEOUT)


def main():
    args = sys.argv[1:]
    if "-h" in args or "--help" in args:
        print(__doc__.strip())
        return 0
    if len(args) > 1 or any(a.startswith("-") for a in args):
        print("쓰는 법: editor_ready.py [<유니티 프로젝트 경로>] (도움말: --help)")
        return 2
    project = str(Path(args[0] if args else Path(__file__).resolve().parent.parent).resolve())
    if not (Path(project) / "ProjectSettings").is_dir():
        print(f"유니티 프로젝트가 아니다: {project}")
        return 2
    if not shutil.which(UNITY):
        print(f"unity 명령이 없다 ({UNITY})")
        return 2

    reopened = False
    st = state(project)
    if st is None and process_up(project):
        print("에디터 프로세스는 떠 있는데 아직 연결되지 않았다(가져오는 중일 수 있다). 기다린다")
        st, alive = wait_connect(project)
        if st is None and alive:
            print(f"에디터가 {OPEN_TIMEOUT}초 안에 연결되지 않았다. 가져오기가 끝났는지, 창이 떠서 막혔는지 에디터를 본다")
            return 1
    if st is None:
        print("에디터가 꺼져 있다. 연다")
        rc, out = open_editor(project)
        reopened = True
    elif st != "ready":
        print(f"에디터가 준비되지 않았다({st}). 기다린다")
        rc, out = unity("status", "--project-path", project, "--until-ready", "--timeout", "300", timeout=300)
        if rc:
            print("기다려도 준비되지 않는다. 닫고 다시 연다")
            rc, out = reopen(project)
            reopened = True
    else:
        rc, out = 0, ""
    if rc:
        print("에디터를 열지 못했다(창이 떠서 막혔거나 시간이 넘었다):\n" + tail(out))
        return 1

    rc, out = recompile(project)
    if rc and not reopened and state(project) != "ready":
        # 에디터가 답하지 않아서 실패한 경우만 다시 연다. 답하는데 실패했으면 컴파일 오류다
        print("에디터가 답하지 않는다. 닫고 다시 연다")
        rc, out = reopen(project)
        if rc:
            print("에디터를 다시 열지 못했다:\n" + tail(out))
            return 1
        rc, out = recompile(project)
    if rc:
        print("컴파일이 통과하지 않는다:\n" + tail(out))
        return 1
    print("에디터 준비됨")
    return 0


if __name__ == "__main__":
    sys.exit(main())

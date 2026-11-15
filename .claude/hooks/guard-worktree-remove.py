#!/usr/bin/env python3
"""PreToolUse(Bash): guard `git worktree remove`.

Removal proceeds silently ONLY when, for every worktree path in the command:
  1. the worktree has no uncommitted changes (tracked or untracked), AND
  2. its checked-out branch is merged into the matching `epic/{suffix}`
     (suffix match: claude/foo -> epic/foo) or into main (local or origin).
Otherwise asks the user at the permission prompt — the deletion criteria live here, not in prose.
"""
import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hooklib  # noqa: E402


def git(cwd, *args):
    try:
        p = subprocess.run(["git", "-C", cwd, *args], capture_output=True, text=True, timeout=10)
        return p.returncode, p.stdout.strip()
    except Exception:  # noqa: BLE001
        return 1, ""


def main():
    data = hooklib.read_input()
    cmd = (data.get("tool_input") or {}).get("command") or ""
    if "worktree" not in cmd:
        return
    paths = []
    found = False
    for cwd, toks in hooklib.segments(cmd):
        parsed = hooklib.git_args(cwd, toks)
        if not parsed:
            continue
        gcwd, args = parsed
        if args[:2] != ["worktree", "remove"]:
            continue
        found = True
        targets = [t for t in args[2:] if not t.startswith("-")]
        paths += [(gcwd, hooklib.resolve(t, gcwd)) for t in targets]
    if not found:
        return
    if not paths:
        hooklib.decide("ask", "git worktree remove 감지 — 대상 경로를 파싱하지 못했습니다. 삭제 기준(clean + epic/main 머지 완료) "
                              "충족 여부를 확인하고 사용자 승인 후 진행하세요.")
    for repo, wt in paths:
        if not os.path.isdir(wt):
            hooklib.decide("ask", f"worktree 경로 '{wt}' 를 확인할 수 없습니다. 삭제 기준 충족 여부를 수동 확인 후 사용자 승인이 필요합니다.")
        rc, branch = git(wt, "rev-parse", "--abbrev-ref", "HEAD")
        if rc or not branch or branch == "HEAD":
            hooklib.decide("ask", f"worktree '{wt}' 의 브랜치를 확인할 수 없습니다(detached HEAD 가능). 사용자 승인이 필요합니다.")
        rc, status = git(wt, "status", "--porcelain")
        if rc or status:
            hooklib.decide("ask", f"worktree '{wt}' (branch: {branch}) 에 커밋되지 않은 변경사항이 있습니다. 삭제하면 유실됩니다 — 사용자 승인이 필요합니다.")
        suffix = branch.rsplit("/", 1)[-1]
        merged = False
        for target in (f"epic/{suffix}", f"origin/epic/{suffix}", "main", "origin/main"):
            if git(repo, "rev-parse", "--verify", "--quiet", target)[0] == 0 and \
               git(repo, "merge-base", "--is-ancestor", branch, target)[0] == 0:
                merged = True
                break
        if not merged:
            hooklib.decide("ask", f"worktree '{wt}' 의 branch '{branch}' 가 epic/{suffix} 또는 main 에 머지되지 않았습니다 "
                                  "(PR 미완료 가능성). 사용자 승인이 필요합니다.")


if __name__ == "__main__":
    main()

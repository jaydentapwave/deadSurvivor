#!/usr/bin/env python3
"""PreToolUse(Bash): block project-forbidden git operations.

Blocks (for git commands that run inside this project):
  1. `git add .` / `git add -A` / `git add --all` (and combined flags like -Av)  — stage specific files only
  2. `git push` that targets main/master in any refspec form (main, +main, :main, HEAD:main, refs/heads/main).
     Exception: the first push to an empty remote (the remote has no branches yet), so a new repository
     can get its main. If the remote cannot be read, the push stays blocked.
  3. `gh pr create` with base main/master whose head branch is not epic/*. The head is `--head` when
     given (an `owner:` prefix is ignored), otherwise the current branch of the folder the command runs in.
Commands that explicitly run in another repository (`cd <outside>` or `git -C <outside>`) are skipped,
e.g. a separate notes repo that legitimately pushes to its own main.
"""
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hooklib  # noqa: E402

MAIN = re.compile(r"^\+?(?:[^:]*:)?(?:refs/heads/)?(main|master)$")


def current_branch(cwd):
    try:
        return subprocess.run(["git", "-C", cwd, "rev-parse", "--abbrev-ref", "HEAD"],
                              capture_output=True, text=True, timeout=5).stdout.strip()
    except Exception:  # noqa: BLE001
        return ""


def remote_is_empty(cwd, remote):
    """True only when the remote answers and has no branches at all (a brand-new repository)."""
    try:
        r = subprocess.run(["git", "-C", cwd, "ls-remote", "--heads", remote],
                           capture_output=True, text=True, timeout=15,
                           env={**os.environ, "GIT_TERMINAL_PROMPT": "0"})
    except Exception:  # noqa: BLE001
        return False
    return r.returncode == 0 and not r.stdout.strip()


def push_args(rest):
    """Split `git push` arguments into (remote, refspecs). No positional argument means the default remote."""
    positional, skip = [], False
    for t in rest:
        if skip:
            skip = False
        elif t in ("-o", "--push-option", "--repo", "--receive-pack", "--exec"):
            skip = True
        elif not t.startswith("-"):
            positional.append(t)
    return (positional[0] if positional else "origin"), positional[1:]


def main():
    data = hooklib.read_input()
    cmd = (data.get("tool_input") or {}).get("command") or ""
    if "git" not in cmd and "gh" not in cmd:
        return
    for cwd, toks in hooklib.segments(cmd):
        name = os.path.basename(toks[0])
        if name == "gh" and toks[1:3] == ["pr", "create"]:
            if not hooklib.inside_project(cwd):
                continue
            base = head = None
            for i, t in enumerate(toks):
                if t in ("--base", "-B") and i + 1 < len(toks):
                    base = toks[i + 1]
                elif t.startswith("--base="):
                    base = t.split("=", 1)[1]
                elif t in ("--head", "-H") and i + 1 < len(toks):
                    head = toks[i + 1]
                elif t.startswith("--head="):
                    head = t.split("=", 1)[1]
            # The PR's head decides, not the folder the command happens to run in:
            # `--head epic/x` from a claude/* checkout is fine, `--head claude/x` from an epic/* checkout is not.
            head = head.split(":", 1)[-1] if head else current_branch(cwd)
            if base in ("main", "master") and not head.startswith("epic/"):
                hooklib.decide("deny", "Project rule: main/master 대상 PR은 epic/* 브랜치에서만 만든다"
                                       f"(이 PR의 head: {head or '알 수 없음'}). "
                                       "claude/{name} → epic/{name} 으로 PR을 만들거나 `--head epic/{name}`을 준다.")
            continue
        parsed = hooklib.git_args(cwd, toks)
        if not parsed:
            continue
        gcwd, args = parsed
        if not hooklib.inside_project(gcwd) or not args:
            continue
        sub, rest = args[0], args[1:]
        if sub == "add":
            for t in rest:
                if t in (".", "--all", "-A") or (re.match(r"^-[A-Za-z]+$", t) and "A" in t):
                    hooklib.decide("deny", "Project rule (CLAUDE.md): Stage only specific files — never "
                                           "`git add .` / `-A` / `--all`. Use explicit file paths.")
        elif sub == "push":
            remote, refspecs = push_args(rest)
            if any(MAIN.match(t) for t in refspecs) and not remote_is_empty(gcwd, remote):
                # An empty remote (no branches at all) is a brand-new repository: nothing to protect yet.
                hooklib.decide("deny", "Project rule (CLAUDE.md): epic→main merge is NOT automated. "
                                       "Wait for explicit user instruction before pushing to main/master. "
                                       "(원격에 브랜치가 하나도 없을 때의 첫 push만 허용된다.)")

if __name__ == "__main__":
    main()

"""Shared helpers for this project's Claude Code hooks (python3 only, no third-party packages).

Every hook reads the hook input JSON from stdin and, when it has something to say,
prints one JSON object on stdout. Hooks never raise: on unexpected input they stay silent,
except guards, which ask the user instead of silently allowing.
"""
import json
import os
import re
import shlex
import sys

PROJECT_DIR = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()


def read_input():
    try:
        return json.load(sys.stdin)
    except Exception:  # noqa: BLE001
        return {}


def decide(decision, reason):
    """PreToolUse permission decision: 'deny' or 'ask'."""
    print(json.dumps({"hookSpecificOutput": {
        "hookEventName": "PreToolUse", "permissionDecision": decision,
        "permissionDecisionReason": reason}}, ensure_ascii=False))
    sys.exit(0)


def add_context(event, text):
    print(json.dumps({"hookSpecificOutput": {"hookEventName": event, "additionalContext": text}},
                     ensure_ascii=False))
    sys.exit(0)


_SPLIT = re.compile(r"&&|\|\||;|\||\n")


def segments(cmd):
    """Split a shell command line into simple-command segments, tracking a leading `cd`.

    Yields (cwd, tokens) where cwd is the directory the segment runs in (best effort) and
    tokens are shlex tokens with leading VAR=value assignments removed.
    """
    cwd = PROJECT_DIR
    for raw in _SPLIT.split(cmd):
        raw = raw.strip().lstrip("(").rstrip(")").strip()
        if not raw:
            continue
        try:
            toks = shlex.split(raw)
        except ValueError:
            toks = raw.split()
        while toks and re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", toks[0]):
            toks = toks[1:]
        if not toks:
            continue
        if toks[0] == "cd" and len(toks) > 1:
            cwd = resolve(toks[1], cwd)
            continue
        yield cwd, toks


def resolve(path, base):
    path = os.path.expanduser(path)
    if not os.path.isabs(path):
        path = os.path.join(base, path)
    return os.path.realpath(path)


def inside_project(path):
    proj = os.path.realpath(PROJECT_DIR)
    path = os.path.realpath(path)
    return path == proj or path.startswith(proj + os.sep)


def git_args(cwd, toks):
    """If tokens are a git invocation, return (effective_cwd, args_after_global_options); else None."""
    if not toks or os.path.basename(toks[0]) != "git":
        return None
    i = 1
    while i < len(toks) and toks[i].startswith("-"):
        if toks[i] == "-C" and i + 1 < len(toks):
            cwd = resolve(toks[i + 1], cwd)
            i += 2
            continue
        if toks[i] in ("-c", "--git-dir", "--work-tree", "--namespace") and i + 1 < len(toks):
            i += 2
            continue
        i += 1
    return cwd, toks[i:]


def is_review_agent(agent_type):
    """A review agent is one whose definition (.claude/agents/<name>.md) asks for a `VERDICT:` line.
    Derived from the repo so new reviewers are covered without editing hooks."""
    name = (agent_type or "").split(":")[-1]
    if not name or "/" in name or "\\" in name:
        return False
    path = os.path.join(PROJECT_DIR, ".claude", "agents", f"{name}.md")
    try:
        with open(path, encoding="utf-8") as f:
            return "VERDICT:" in f.read()
    except OSError:
        return False

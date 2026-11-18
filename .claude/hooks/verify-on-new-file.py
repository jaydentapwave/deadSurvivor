#!/usr/bin/env python3
"""PreToolUse(Write): when a NEW .cs file is about to be created under Assets/Game/02.Scripts/,
find sibling classes whose names share the new class's responsibility keywords and inject them as
context, so the assistant verifies before declaring a brand-new system.

Background: `.ai/rules/verify-existing-code.md` mandates "grep existing classes before creating new ones",
but a text rule alone is unreliable. This hook enforces the procedural part at tool-call level; the rule file
keeps the normative part (why, anti-patterns, learnings). Information only: never blocks.
Works for both a monorepo (<client>/Assets/...) and a Unity project at the repo root.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hooklib  # noqa: E402

MARK = "Assets/Game/02.Scripts/"
GENERIC = {"Service", "Manager", "Controller", "Presenter", "Factory", "Registry", "Handler", "View", "Model",
           "Event", "Data", "UI", "Util", "Utils", "Const", "Popup", "Element", "Base", "Game", "Async"}


def main():
    data = hooklib.read_input()
    path = (data.get("tool_input") or {}).get("file_path") or ""
    if not path:
        return
    if not os.path.isabs(path):
        path = os.path.join(hooklib.PROJECT_DIR, path)
    norm = path.replace("\\", "/")
    idx = norm.find("/" + MARK)
    if not norm.endswith(".cs") or idx < 0 or os.path.exists(path):
        return
    root = norm[: idx + len(MARK)]
    if not os.path.isdir(root):
        return
    cls = os.path.splitext(os.path.basename(norm))[0]
    spaced = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1 \2", re.sub(r"([a-z0-9])([A-Z])", r"\1 \2", cls.replace("_", " ")))
    keywords = sorted({t for t in spaced.split() if re.match(r"^[A-Z][A-Za-z0-9]+$", t) and t not in GENERIC})
    lines = []
    for kw in keywords:
        matches = []
        for dirpath, _dirs, files in os.walk(root):
            for f in files:
                if f.endswith(".cs") and kw in f and f != cls + ".cs":
                    matches.append(os.path.relpath(os.path.join(dirpath, f), hooklib.PROJECT_DIR))
        if matches:
            lines.append(f"  [keyword: {kw}]")
            lines += [f"    - {m}" for m in sorted(matches)[:8]]
    if lines:
        hooklib.add_context("PreToolUse",
                            f"[verify-hook] 신규 클래스 '{cls}' 생성 직전.\n"
                            "같은 키워드를 가진 기존 클래스가 발견되어 알려둠 — 분류 결정 전에 1회 Read 검증 권장.\n"
                            + "\n".join(lines)
                            + "\n\n→ 같은 책임의 클래스가 이미 있다면 그 위치/패턴을 따른다. 분류 추론보다 코드 실증 우선.\n"
                              "   규범·anti-pattern 근거: .ai/rules/verify-existing-code.md")


if __name__ == "__main__":
    main()

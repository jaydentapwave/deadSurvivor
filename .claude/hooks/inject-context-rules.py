#!/usr/bin/env python3
"""UserPromptSubmit: inject context-specific rule files only when the prompt mentions related keywords.

Each area CLAUDE.md always loads its core rules via @import. The rest are injected by this hook
according to .ai/inject.tsv (tab-separated, lines starting with # are comments):
    <rule file path from repo root>  <label>  <English regex>  <Korean regex>
English regexes are matched against the lower-cased prompt, Korean ones against the original.
Use "-" for an empty regex column. Area setups (/unity-setup etc.) append their own lines.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hooklib  # noqa: E402


def main():
    prompt = hooklib.read_input().get("prompt") or ""
    table = os.path.join(hooklib.PROJECT_DIR, ".ai", "inject.tsv")
    if not prompt or not os.path.isfile(table):
        return
    lower = prompt.lower()
    parts = []
    with open(table, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if not line.strip() or line.startswith("#"):
                continue
            cols = (line.split("\t") + ["", "", ""])[:4]
            path, label, en, ko = cols
            hit = False
            try:
                if en and en != "-" and re.search(en, lower):
                    hit = True
                elif ko and ko != "-" and re.search(ko, prompt):
                    hit = True
            except re.error:
                continue
            file = os.path.join(hooklib.PROJECT_DIR, path)
            if hit and os.path.isfile(file):
                with open(file, encoding="utf-8") as rf:
                    parts.append(f"[Auto-injected: {label or path} — prompt mentioned related keywords]\n{rf.read()}")
    if parts:
        hooklib.add_context("UserPromptSubmit", "\n\n".join(parts))


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Stop: a director step session may not end before it writes its handoff file.

The director starts each step with `claude -p` and sets DIRECTOR_HANDOFF to the absolute path
the step must write (see director-run references/runner.md). Without that variable this hook does
nothing, so ordinary sessions are never affected.

When the file is missing the hook blocks the stop once and tells the session what to write.
If the session is already continuing because of this hook (stop_hook_active), it lets the stop
through so a stuck session cannot loop; the director then sees the missing file and reports it.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hooklib  # noqa: E402


def main():
    target = os.environ.get("DIRECTOR_HANDOFF", "").strip()
    if not target:
        return
    if os.environ.get("DIRECTOR_REVIEW_ONLY", "").strip():
        # A review-only session started for a Codex step (director-run scripts/codex-step.py): the verdict
        # hook still logs next to DIRECTOR_HANDOFF, but the handoff itself is written by the Codex session.
        return
    data = hooklib.read_input()
    if data.get("stop_hook_active"):
        return
    path = os.path.expanduser(target)
    if os.path.isfile(path) and os.path.getsize(path) > 0:
        return
    reason = (
        f"디렉터 단계 세션은 끝나기 전에 핸드오프 파일을 써야 합니다: {path}\n"
        "/director-handoff 형식으로 지금 작성하세요. 일을 다 못 했으면 status: partial 또는 blocked와 "
        "이유, open_questions를 적으면 됩니다. 실행하지 않은 관문은 pass로 적지 않습니다."
    )
    print(json.dumps({"decision": "block", "reason": reason}, ensure_ascii=False))
    sys.exit(0)


if __name__ == "__main__":
    try:
        main()
    except Exception:  # noqa: BLE001
        pass

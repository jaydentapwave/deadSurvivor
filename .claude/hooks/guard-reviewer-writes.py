#!/usr/bin/env python3
"""PreToolUse(Write|Edit|MultiEdit|NotebookEdit): review agents may not change project files.

Review agents (see hooklib.is_review_agent) judge a diff in a fresh context; the main session fixes.
Their prompts already say "do not modify files", but some need Write for scratch work (for example
algorithm-tester extracts code into a standalone file). This hook turns the prompt's request into a
rule: inside a review agent, writes inside the project are denied, writes elsewhere (scratchpad, /tmp)
pass. Bash writes are not covered; the agent prompt still forbids them.
Main-session and non-review-agent tool calls are never affected.
Like other guards (see hooklib), an unexpected error inside a subagent asks instead of silently allowing.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hooklib  # noqa: E402


def main(data):
    agent = data.get("agent_type") or ""
    if not data.get("agent_id") or not hooklib.is_review_agent(agent):
        return
    tool_input = data.get("tool_input") or {}
    path = tool_input.get("file_path") or tool_input.get("notebook_path") or ""
    if path and hooklib.inside_project(hooklib.resolve(path, hooklib.PROJECT_DIR)):
        hooklib.decide("deny", (
            f"{agent.split(':')[-1]}는 리뷰 에이전트라 프로젝트 파일을 고칠 수 없다: {path}. "
            "고칠 점은 답에 file:line과 방향으로 적고, 시험용 파일은 스크래치 폴더에 만든다."))


if __name__ == "__main__":
    payload = hooklib.read_input()
    try:
        main(payload)
    except SystemExit:
        raise
    except Exception:  # noqa: BLE001
        # Only a subagent call can be a review agent; never interrupt the main session on a hook bug.
        if isinstance(payload, dict) and payload.get("agent_id"):
            hooklib.decide("ask", "리뷰 에이전트 쓰기 검사 훅이 오류로 판단하지 못했다. 이 쓰기를 허용할지 확인한다.")

#!/usr/bin/env python3
"""SubagentStop: review agents must end with a VERDICT line, and director steps keep a record of it.

A review agent here is any agent whose definition (.claude/agents/<agent_type>.md) asks for a
`VERDICT:` line. The list is read from the repo, not written into this hook.

1. When a review agent stops without a `VERDICT: PASS | FAIL | NOT ASSESSED` line, the hook sends it
   back once to add one. If it is already continuing because of this hook (stop_hook_active), it lets
   the stop through so a stuck agent cannot loop.
2. When DIRECTOR_HANDOFF is set (a director step, see director-run references/runner.md), the hook
   appends {agent, verdict} to `<handoff without .md>.agents.jsonl`. The director's
   `check.py verify-step` compares the handoff's `agent:` gates with this record, so a gate cannot say
   PASS for a review that did not run or ended in FAIL.

Where the verdict is read from (SubagentStop input of Claude Code 2.1: agent_id, agent_type,
agent_transcript_path, last_assistant_message (optional), stop_hook_active, plus the common
session_id / transcript_path / cwd):
  a. `last_assistant_message` — the text of the last assistant message. It is empty when the agent
     ends with a tool call, which is the normal case now: subagents hand their final report back
     through a `SubagentHandback` tool call, not a text block.
  b. the agent transcript — text blocks and the report inside hand-back tool calls of the current turn.
  c. the calling session's transcript (`transcript_path`) — the Agent tool result for this agent_id,
     used only when the agent transcript cannot be read.
(b) and (c) are re-read for up to 2 seconds because the files can be written after the stop event.

Ordinary sessions only get (1). The hook never raises.
"""
import json
import os
import re
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hooklib  # noqa: E402

VERDICT = re.compile(r"^[\s`*>#-]*VERDICT:\s*(PASS|FAIL|NOT ASSESSED)\b", re.M)


RETRY_SECONDS = 2.0  # how long to wait for a transcript that is still being written
RETRY_STEP = 0.25


def read_entries(path):
    path = os.path.expanduser(path or "")
    if not path:
        return []
    try:
        with open(path, encoding="utf-8") as f:
            lines = f.readlines()
    except OSError:
        return []
    entries = []
    for line in lines:
        try:
            entry = json.loads(line)
        except ValueError:
            continue  # a half-written last line: the retry reads it again
        if isinstance(entry, dict):
            entries.append(entry)
    return entries


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for v in value.values():
            yield from strings(v)
    elif isinstance(value, list):
        for v in value:
            yield from strings(v)


def answer_texts(content):
    """Text an assistant message says to its caller: text blocks, and the report inside a hand-back
    tool call. Current Claude Code subagents deliver their final answer as a `SubagentHandback`
    tool call (input.message), so the last assistant message often has no text block at all."""
    if isinstance(content, str):
        return [content]
    out = []
    for c in content or []:
        if not isinstance(c, dict):
            continue
        if c.get("type") == "text":
            out.append(c.get("text") or "")
        elif c.get("type") == "tool_use" and "handback" in str(c.get("name") or "").lower():
            out.extend(strings(c.get("input")))
    return out


def from_agent_transcript(entries):
    """Newest answer first. Stops at the prompt that started the current turn, so a continued agent
    that answers without a verdict is not credited with the verdict of its earlier turn."""
    for entry in reversed(entries):
        # Only conversation entries count. The transcript also holds bookkeeping entries such as
        # `api-request-blob`, which replay an EARLIER assistant message (role "assistant") after the
        # prompt of a continued turn. Reading those credited a continued agent with its previous
        # verdict (FAIL then PASS was logged as FAIL, FAIL) whenever the new answer was not flushed yet.
        if entry.get("type") not in (None, "user", "assistant"):
            continue
        msg = entry.get("message") or {}
        content = msg.get("content")
        if msg.get("role") == "user":
            is_tool_result = isinstance(content, list) and any(
                isinstance(c, dict) and c.get("type") == "tool_result" for c in content)
            if not is_tool_result:
                return None
            continue
        if msg.get("role") != "assistant":
            continue
        for text in answer_texts(content):
            m = VERDICT.search(text)
            if m:
                return m.group(1)
    return None


def from_main_transcript(path, agent_id):
    """The calling session's record of this agent's result (written when the Agent tool returns)."""
    if not agent_id:
        return None
    for entry in reversed(read_entries(path)):
        result = entry.get("toolUseResult")
        if not isinstance(result, dict) or result.get("agentId") != agent_id:
            continue
        for key in ("handbackReport", "content", "result"):
            for text in strings(result.get(key)):
                m = VERDICT.search(text)
                if m:
                    return m.group(1)
        return None
    return None


def find_verdict(data, wait=RETRY_SECONDS):
    """Look in the hook input first, then the agent's transcript, then the calling session's transcript.
    The transcripts can lag behind the stop event, so re-read for a short while before giving up."""
    for key in ("last_assistant_message", "result", "output"):
        value = data.get(key)
        m = VERDICT.search(value) if isinstance(value, str) else None
        if m:
            return m.group(1)
    agent_path, main_path = data.get("agent_transcript_path"), data.get("transcript_path")
    if not agent_path and not main_path:
        return None
    deadline = time.monotonic() + wait
    while True:
        entries = read_entries(agent_path)
        verdict = from_agent_transcript(entries)
        if verdict is None and not entries:
            # Only when the agent transcript cannot be read: with a readable transcript the calling
            # session's record can only hold an earlier turn of the same agent (a stale verdict).
            verdict = from_main_transcript(main_path, data.get("agent_id"))
        if verdict or time.monotonic() >= deadline:
            return verdict
        time.sleep(RETRY_STEP)


def main():
    data = hooklib.read_input()
    agent = data.get("agent_type") or ""
    if not hooklib.is_review_agent(agent):
        return
    verdict = find_verdict(data)
    target = os.environ.get("DIRECTOR_HANDOFF", "").strip()
    if target:
        log = os.path.splitext(os.path.expanduser(target))[0] + ".agents.jsonl"
        os.makedirs(os.path.dirname(log) or ".", exist_ok=True)
        with open(log, "a", encoding="utf-8") as f:
            f.write(json.dumps({"agent": agent.split(":")[-1], "verdict": verdict or "MISSING",
                                "agent_id": data.get("agent_id"), "at": int(time.time())},
                               ensure_ascii=False) + "\n")
    if verdict is None and not data.get("stop_hook_active"):
        print(json.dumps({"decision": "block", "reason": (
            "답의 첫 줄을 `VERDICT: PASS`, `VERDICT: FAIL`, `VERDICT: NOT ASSESSED — <이유>` 중 하나로 다시 써라. "
            "호출한 세션이 이 줄을 관문 증거로 옮긴다. 판단할 입력이 없었으면 NOT ASSESSED다."
        )}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except Exception:  # noqa: BLE001
        pass

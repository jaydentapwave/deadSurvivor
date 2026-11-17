#!/bin/sh
# 디렉터가 띄우는 Claude 세션(단계 세션, 대기열 항목, Codex 실행기의 리뷰 세션)은 Opus 5.5, 추론 medium으로 돈다.
# 사용자 2026-11-15: "디렉터를 제외하고는 웬만하면 medium으로 설정을 변경해주고 앞으로도 그렇게 해줘"
# 사용자 2026-11-17: "서브에이전트도 동일하게 opus5.5 meduem으로 작업을진행해줘" — 세션 안의 하위 에이전트도 Opus 5.5.
export CLAUDE_CODE_SUBAGENT_MODEL=claude-opus-5-5
exec claude --model claude-opus-5-5 --effort medium "$@"

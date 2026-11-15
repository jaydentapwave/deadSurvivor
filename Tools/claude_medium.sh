#!/bin/sh
# 디렉터가 띄우는 Claude 세션(단계 세션, 대기열 항목, Codex 실행기의 리뷰 세션)은 추론 medium으로 돈다.
# 사용자 2026-11-15: "디렉터를 제외하고는 웬만하면 medium으로 설정을 변경해주고 앞으로도 그렇게 해줘"
exec claude --effort medium "$@"

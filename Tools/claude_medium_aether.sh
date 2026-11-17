#!/bin/sh
# Aether 영상 프레임 단계·대기열 항목용: Opus 5.5 medium(claude_medium.sh) + 이 세션에만 Aether MCP를 붙인다.
# 프로젝트·전역 MCP 설정은 바꾸지 않는다(인증은 이 컴퓨터에 이미 있음). 사용자 2026-11-17 "Aether로 (손질 포함)".
D="$(cd "$(dirname "$0")" && pwd)"
exec "$D/claude_medium.sh" --mcp-config "$D/aether-mcp.json" \
  --allowedTools "mcp__aether__get_credit mcp__aether__create_image_upload mcp__aether__sprite_zenith mcp__aether__sprite_pulsar mcp__aether__get_job_status mcp__aether__list_my_results" "$@"

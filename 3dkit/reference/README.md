# 참고 구현 (실행용 아님)

이전 프로젝트 Deadsurvivor에서 실제로 쓰던 도구를 2026-11-03에 그대로 복사했다. **이 폴더의 코드는 실행하지 않는다.** 프로젝트 경로·게임 C# 타입·장면·실행기 이름이 박혀 있다. [하네스 설계도](../workflows/harness-blueprint.md)의 단계(H0~H4)에서 `tools/`·`unity/`로 다시 지을 때 읽는다. 개인 절대 경로는 `<PROJECT_ROOT>`, `<USER_SITE_PACKAGES>`로 바꿔 두었다.

| 파일 | 하던 일 | 새로 지을 때 바꿀 곳 | 설계도 단계 |
|---|---|---|---|
| `tools/Hooks/claude_guard.py` | Claude PreToolUse·UserPromptSubmit hook. 전체 Play 이유 강제, 같은 주제 3번째 Codex 검토 차단, 맥락 토큰 알림. fail-open, `selftest` | `Tools/Harness/tier-policy.json` 읽기 → `3dkit.config.json`. selftest의 실행기 이름 | H0 |
| `tools/Hooks/claude-settings.json` | 위 hook을 연결하던 `.claude/settings.json` | 스크립트 경로 → `3dkit/tools/guard.py` | H0 |
| `tools/Harness/play_probe.py` | 측정 Play: 로비 → 스쿼드 → 출전 → 웨이브 건너뛰기 → HP 고정·밤 → 캡처·성능 중앙값 → 종료 → 정리 | `DeadSurvivor.DSGame` 등 타입 이름, 기본 장면, `Mobile_RPAsset` 이름 | H1 |
| `tools/Harness/protect.py` | 실행 전 깨끗했던 부작용 파일만 되돌리기, 사본 보관 | 글꼴 경로 | H1 |
| `tools/Harness/session_time.py` | Claude 세션 JSONL에서 사용자 요청 단위 시간·토큰 표 | 세션 폴더 경로 계산 | H1 |
| `tools/Harness/tier.py`, `tier-policy.json` | 짧은/전체 Play 고르기, 게임 동작·시각 해시 분리, 증거 기록 | 경로 패턴(`Assets/DeadSurvivor/**`), 실행기 목록 | H2 |
| `tools/Play/keyart_045.py` | 짧은/전체 Play 실행기(모드 허용 목록, 실패 시점 상태, 화면비, 부하) | 장면·단계 이름, 절대 경로 | H2 |
| `tools/Play/DSKeyArt045Verification.cs` | 위 실행기의 Unity 쪽(Editor C#) | 네임스페이스, `Target()`의 프로젝트 경로 검사 | H1·H2 (`unity/` 패키지로) |
| `tools/Art/assets.py` | 에셋 정식 경로 CLI: `spec·list·gen·palette-char·prep·unity·check·capture·migrate·selftest` | 카탈로그 경로, 폴더 규칙, 로우폴리 전용 처리 | H3 |
| `tools/Art/asset_tripo.py` | Tripo 생성: 요청 고정·드라이런·제출·받기·상태·대조, 예산 승인 가드, 잠금·재개 일지, 재전송 금지 | Tripo 경로·환경변수를 설정에서 | H3 |
| `tools/Art/asset_texture.py` | 텍스처 팔레트화(OKLab 최근접 + 3×3 다수결), 팔레트 생성 | 미드폴리 색 방식에 따라 선택 기능으로 | H3 |
| `tools/Art/AssetBuild.cs` | Unity 쪽 가져오기: 텍스처 설정, 공용 셰이더 재질, 프리팹, 캐릭터 세트 | `Root` 경로 상수, 셰이더 이름, 폴더 | H3 (`unity/` 패키지로) |
| `tools/Art/selftest_asset_tripo.py` | Tripo 가드 반례 시험(오프라인) | 모듈 경로 | H3 |
| `shaders/Water045.shader` | 이전 물 셰이더(면 물, 로우폴리) | 참고만. 미드폴리 물은 새로 | P4 |

카탈로그 예시(이전 프로젝트의 실제 카탈로그)는 키트를 만든 게임 프로젝트 쪽 `docs/art/previous/asset-catalog.json`에 있다. 3dkit에는 넣지 않았다(게임 고유 내용).

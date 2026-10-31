# Tripo Studio → Unity 바위 1개 전송 시험

- 실행일: 2026-10-31 KST. 생성 자산 표시 시각 13:48, Unity 가져오기 13:54경, 회귀 검사 시작 13:56:01. 개별 명령의 정확한 시작/종료 시각은 `output/tripo-studio-test/*.invocation.json`에 기록했다.
- 범위: [L1 시험 설계](../design/tripo-studio-transfer-test.md). Studio에서 일반 바위 1개를 생성하고 실제 FBX 파일을 내려받아 지정된 개인 Unity 프로젝트에서 렌더링했다.
- 결론: **Studio 화면 조작 → 생성 → FBX/텍스처 다운로드 → Unity 재질 연결 → 실제 화면 확인까지 PASS.** API 크레딧 구매는 사용하지 않았다. 최종 맵 구현·아트 품질 승인·성능 검증과는 구분한다.

## 승인과 비용

사용자는 무료 계정의 공개 생성에 대해 “이번 바위 1개만 공개 생성 허용”이라고 답했다. 공개 예외는 프로젝트 이름·지도·원본 이미지를 포함하지 않는 일반 바위 하나와 표시 비용 40크레딧에만 적용했다. 승인 후 생성 버튼을 한 번 눌렀으며 재생성·유료 구매·외부 공유 버튼은 사용하지 않았다. 프로젝트 파일을 업로드하지 않았다.

| 항목 | 이번 관찰 |
|---|---|
| 서비스 | 국제 Tripo Studio, 로그인된 브라우저 |
| 생성 모델 | HD 모델 H2.5 레거시, 텍스트, 지오메트리 및 텍스처 |
| 공개 범위 | 승인된 일반 바위 1개 |
| 크레딧 | 200 → 160, 40 사용 |
| 생성 작업 ID | `21300150-1f34-4e8d-ae0b-a0ca509bf443` |
| 생성 입력 | 설계 문서의 370자 영문 프롬프트 그대로 |
| 내보내기 | FBX, Blender 프리셋, 현재 4K 텍스처 |
| 무료 내보내기 표시 | 15회 → 14회 |
| 추가 결제 | 없음 |

[Tripo 결과](https://studio.tripo3d.ai/ko/workspace/generate/21300150-1f34-4e8d-ae0b-a0ca509bf443) · [생성 전](../../output/tripo-studio-test/before-generation.jpg) · [생성 완료](../../output/tripo-studio-test/generated-rock-full.jpg) · [내보내기 완료](../../output/tripo-studio-test/export-complete.jpg).

## 실제 파일과 Unity 적용

[원본 ZIP](../../output/tripo-studio-test/source/tripo-rock-21300150.zip)은 18,551,626바이트다. FBX 1개와 basecolor, normal, roughness, metallic, rm 텍스처 5개를 포함한다. 원본 텍스처는 모두 4096×4096이다.

- ZIP SHA-256: `18ae66f5316c90b0b2d7131b17e4a0dc89803696562bd9ba6709f986d3b73ff7`
- FBX SHA-256: `a6db517860630cbeafca352fd907056f3aa73a6efabaec34931d90b51f081b95`
- 전체 파일 지문과 관찰값: [증거 JSON](../../output/tripo-studio-test/evidence.json).

실제 대상은 `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor`, Unity 6000.4.3f1이다. 읽기 전용 동기화 비교에서 코드 차이가 없었으며 자동 동기화는 실행하지 않았다. 기존 게임 씬이 중지 상태이고 미저장 변경과 reload 비활성 설정이 없는 것을 확인한 후 작업했다.

새 자산 위치는 `Assets/DeadSurvivor/Art/Tests/TripoRock21300150`이다. 이 폴더에 원본 FBX/텍스처, `TripoRock_TestOnly.prefab`, `TripoRock_Test_URP.mat`, `PreviewGround.mat`, `TripoRockPreview.unity`를 추가했다. 기존 파일 변경·삭제는 없으며, 추가된 상위 폴더의 `.meta`를 포함해 [변경 목록 및 지문](../../output/tripo-studio-test/unity-changes.json)에 기록했다. 캡처는 Unity의 `Assets/MigrationVerification` 아래에도 생성되고 본 저장소의 검증 폴더로 복사된다.

Unity 기본 가져오기만으로 품질을 보장한다고 주장하지 않는다. 이번에는 별도 URP/Lit 재질을 만들고 basecolor와 normal을 연결했다. 노멀맵 가져오기 유형을 설정하고 두 텍스처의 Unity 최대 크기를 2048로 지정했다. roughness/metallic/rm은 원본 보관만 하며 재질에 연결하지 않았다. 표면은 시험용 고정값(smoothness 0.12, metallic 0, normal strength 0.7)을 사용했다. 프리팹의 가로/깊이 중 긴 축을 약 2 Unity 단위로 맞추고 바닥 기준을 정렬했다. 이는 실제 바위 치수의 근거가 아니다.

| 메시 관찰 위치 | 삼각형 | 정점 |
|---|---:|---:|
| Tripo Studio 표시 | 501,262 | 257,458 |
| Unity 가져오기 후 | 501,226 | 303,046 |

값은 각각 실제 표시/조회값이다. 변환 후 차이의 원인은 별도 분석하지 않았다. low-poly라는 프롬프트가 실제 경량 메시를 보장하지 않는다는 점은 확인했다. 현재 원본을 다수 배치하는 최종 게임 자산으로 채택하지 않는다.

## 실행 검증

| 검사 | 상태 | 근거·범위 |
|---|---|---|
| Studio 생성 | PASS | 새 작업 ID, 완료 모델, 잔액 160 확인 |
| 실제 다운로드 | PASS | 브라우저 다운로드 완료, ZIP 및 6개 모델/텍스처 파일 확보 |
| Unity 가져오기 | PASS | FBX GameObject, 메시 1개, Renderer 1개, URP/Lit 재질 확인 |
| 실제 Play 프레임 | PASS | `Time.frameCount` 809 → 820, server-side wait `met=true` |
| 바위 화면 시각 확인 | PASS | 모델·표면·그림자가 표시되며 오류 재질의 자홍색 출력 없음 |
| 바위 시험 콘솔 | PASS | 컴파일 실패 없음, 오류 0 |
| 원래 씬·Play·background 복구 | PASS | 원래 게임 씬, 재생 중지, 미저장 변경 없음, background=false 복구 |
| auto-tick 원래 간격 복구 | BLOCKED | 처음 활성화 호출 전 간격을 기록하지 않아 동일 값 복구 여부 확인 불가. 이미 활성화된 상태였으며 시험 시 16ms로 설정 |
| 기존 게임 회귀 | PASS | 현재 자산 상태의 컴파일·695 규칙·실제 입력·3개 비율 24화면 자동 검사·콘솔·정리 |
| 회귀 화면 시각 확인 | PASS | 메인이 1280×720 정비, 1024×768 최대 장착 전투 화면을 직접 확인. 24장 전체의 수동 검수로 주장하지 않음 |
| 경량화·LOD·모바일 성능 | NOT_RUN | 이번 시험은 전송 가능성 확인 |
| 바위 충돌·길찾기·전투맵 배치 | NOT_RUN | 원본 바위 프리팹에 충돌체를 추가하지 않음 |
| 레퍼런스 일치·최종 미술 승인 | NOT_RUN | 일반 바위 시험이며 Verdant Meadows 원본을 입력하지 않음 |
| 실제 맵/맵 하네스 | NOT_RUN | 기존 미구현 상태 유지 |

[실제 Unity 바위 화면](../../output/tripo-studio-test/unity-rock-preview.png) · [가져오기 결과](../../output/tripo-studio-test/import-rock.stdout.json) · [프레임 근거](../../output/tripo-studio-test/preview-frames.stdout.json) · [콘솔 근거](../../output/tripo-studio-test/preview-console.stdout.json) · [회귀 검사 원자료](../../output/verification/20261031-135601-f7a448d6/summary.json).

자동 tick은 활성화 호출 때 이미 켜져 있었으며, 16ms로 설정해 시험과 회귀 검사에 사용했다. 원래 간격 값은 미기록이므로 tick 간격까지 완전 복구했다고 주장하지 않는다. 원래 background=false는 명령 결과와 최종 확인으로 구분해 검증한다. 이번 바위와 미리보기 씬은 기존 게임 씬이나 빌드 씬 목록에 연결하지 않았다.

Unity 최초 실행은 씬 로드 후 종료되어 연결되지 않았다. `unity open --wait`로 실행 연결을 유지한 두 번째 실행에서 연결 및 실제 시험이 성공했다. 첫 실패를 숨기거나 다른 프로젝트/헤드리스 대체 결과를 사용하지 않았다. 시험 도구는 Homebrew를 통해 Unity CLI 1.0.0-beta.14에서 beta.15로 갱신했다. 전역 Codex/Claude 설정·Unity 패키지·ProjectSettings는 변경하지 않았다.

## 독립 검토와 최종 판정

독립 validator-client는 스펙 준수 PASS, 전송·렌더링 동작 계약 PASS로 판정했다. 파일 무결성·승인 범위·변경 경계·Unity 프레임 및 이미지·복구 근거와 현재 `all --screens` 보고서의 소스/대상/검사기 지문·원자료 해시 일치를 확인했다. 지적에 따라 복구 PASS를 씬·Play·background로 한정하고, auto-tick 원래 간격 복구는 BLOCKED로 분리했다. 최종 `/usr/bin/python3 Tools/Verify/gate.py check`의 결과와 경로는 [증거 JSON](../../output/tripo-studio-test/evidence.json)의 `final_gate`에서 확인한다. gate PASS도 위 BLOCKED/NOT_RUN 항목을 성공으로 바꾸지 않는다.

이번에 입증한 것은 **Studio의 기존 크레딧으로 에이전트가 직접 생성하고 Unity로 옮겨 표시할 수 있다는 것**이다. 이 무료 H2.5 결과만으로 유료 모델의 품질, 비공개 생성, 상업 이용 권한, 대량 생산의 일관성이나 비용을 검증했다고 확대하지 않는다. 다음 제작 전에는 이 원본의 경량화 가능성과 목표 카메라에서의 미술 방향을 별도로 평가해야 한다.

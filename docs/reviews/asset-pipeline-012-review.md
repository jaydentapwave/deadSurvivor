# ASSET-PIPELINE-012 최소 구현 검증

2026-10-31 KST · L2 · [설계](../design/asset-pipeline-minimum.md) · [실행 사용법](../../Tools/Assets/README.md).

기존 일반 바위 한 개의 원본 보존 → 중단/재개 → Blender 경량화 → Unity 후보/실측 → 실제 프레임/캡처 → 편집 상태 복구를 구현했다. 유료 생성·다운로드 API·외부 업로드는 실행하지 않았다. 추가 생성 요청 수는0이다. 최초 실패의 폰트 변경은 당시 BLOCKED였으나 정상 종료 후 최초 바이트 지문으로 복구했다. 실패와 복구는 별도 이력으로 남긴다.

## 구현과 범위

`Tools/Assets/pipeline.py`, `process_blender.py`, `unity_asset.py`, `UnityAsset.cs`, `test_pipeline.py` 및 바위 전용 JSON 계약. Unity에 검사 스크립트를 상시 설치하지 않고 기존 CLI의 일회 실행을 사용한다. 새 Unity 자산은 개인 대상의 `Assets/DeadSurvivor/Art/Tests/Asset012_<실행 UUID>/` 아래 후보로만 저장한다. 게임 코드/맵 C·배치·hook·gate 기준은 변경하지 않았다.

작업 ID·기존 Studio 출처·계약/도구/원본 지문, 원자 저장/잠금, 재개 단계와 실패를 기록한다. 실제 수락된 API ID의 읽기 전용 조회 어댑터는 가짜 CLI로만 검증했다. 새 제출/원격 다운로드/과금 중복 보장/강제 종료 후 자동 Unity 복구/전체 A5·G5·성능은 미구현 또는 NOT_RUN이다.

수치 기준은 처리 전에 동결한 시험 규격이며 최종 게임 자원 예산이 아니다. Blender 5.2.2 LTS, Unity 6000.4.3f1, Unity CLI 1.0.0-beta.15를 사용했다. 원본 FBX SHA-256은 `a6db517860630cbeafca352fd907056f3aa73a6efabaec34931d90b51f081b95`다.

## 독립 검토와 수정

design-reviewer `/root/asset012_design`: D12-01 유한한 양의 삼각형/크기 및 목표 크기 하한, D12-02 변경 전 복구 journal·소유권·tick 저장과 미완료 실행 차단을 반영하고 PASS 재확인.

validator-client `/root/asset012_validator`는 읽기 전용 코드·실행 증거 검토를 수행했다. 다음 지적을 수정하고 코드 재검토 PASS를 받았다.

| 지적 | 수정·검증 |
|---|---|
| V12-01 빈 PROCESSED 기록이 통과 가능 | 단계/이벤트·원본/필수 처리 파일·receipt·실제 계약 결과의 완전성 요구, 반례 추가 |
| V12-02 실제 품질 실패가 BLOCKED로 기록 | 관측된 계약 실패는 FAIL, 도구/환경 문제와 분리 |
| V12-03 재로드 후 무효한 선택 instance ID | GlobalObjectId로 선택 기록/복구 |
| V12-04 전역 SaveAssets가 사용자 자산 저장 | 후보 개별 자산 저장으로 제한, 미저장 자산 시작 차단 |
| V12-05 측정 후 파일 변경 탐지 누락 | 측정 전후 및 최종 의존/후보 지문 비교 |
| V12-06 최종 검사 예외가 summary 저장을 건너뜀 | 마지막 검사를 독립 catch·상태/원인 기록 후 summary 원자 저장. 원본 변조·target symlink 반례 추가 |

최종 코드/문서/자산 실행 증거도 validator-client가 PASS로 확인했다. 현재 도구4개와 job/27시험 지문 일치, 최종 요약의 모든 artifact 지문·백업226개·전후 상태 동일·기존 파일 변화0을 독립 대조했다.

새 게임 기하 알고리즘은 작성하지 않았으며 Blender 내장 modifier와 Unity 실측을 사용한다. 별도의 algorithm-tester 대상 게임 계산 변경은 없다.

## 실제 시험 근거

최신 27개 단위 시험의 실행 시각·소스 지문·로그는 [시험 결과](../../output/asset-pipeline/asset012-final/unit-tests.json), [로그](../../output/asset-pipeline/asset012-final/unit-tests.log)에 보존했다. 계약 하한/상한·비정상 수치·경로 이탈·원본/완료 파일 변조·작업 잠금·중단 재개·CLI timeout/ID 불일치·최종 검사 실패 보존을 포함한다. 실제 네트워크 생성 복구 시험이 아니다.

| 검사 | 상태 | 근거·범위 |
|---|---|---|
| 단위 시험27개 | PASS | 위 최신 결과/소스 지문 |
| 원본 복사 후 종료·같은 ID 재개 | PASS | `72a38d36e0494023953926986177f760` job, 별도 CLI 프로세스에서 SOURCE_READY→PROCESSED |
| Blender 처리 | PASS | 원본501,262 → 처리9,500 삼각형, 크기/원점/2K 텍스처, 전후 이미지 |
| Unity 규격·실제 프레임·콘솔·복구 | PASS | [14:57:10~14:57:17 결과](../../output/asset-pipeline/rock-21300150/72a38d36e0494023953926986177f760/unity-916e3f7c3e1847328128c27d8940f06b/summary.json). Unity 원본501,226, 처리9,492 삼각형. 프레임5→33, 오류0, 상태 전후 동일·기존 파일 변경0 |
| 기존 게임 회귀 | PASS | [14:57:41 실행](../../output/verification/20261031-145741-2f81c071/summary.json). 695규칙·입력·3비율24화면·컴파일/콘솔/복구·지문 안정성. 최종 후보 추가 전의 중간 회귀 기록 |
| V12-06 수정 후 최종 통합 재시험 | PASS | [15:03:25~15:03:31 결과](../../output/asset-pipeline/rock-21300150/eb6917c0e2444ae09ad3f1607f1c2155/unity-69f5e6272f3f4368aa0c9babb64262e8/summary.json). 같은 원본으로 새 job·중단 재개·Blender·Unity 재실행. 최종 지문/복구/기존파일 변화0, 프레임2→32, 오류0 |
| 최종 기존 게임 회귀 | PASS | [15:03:56 실행](../../output/verification/20261031-150356-5a936679/summary.json). 최종 후보/원본 폰트 지문 기준 컴파일·695규칙·입력·3비율24화면·오류0·복구·소스 안정성 |
| 실제 API 생성/다운로드, 모바일 성능·최종 미술·맵 적용 | NOT_RUN | 이번 TRANSFER_TEST 범위 밖 |

원본 Unity 프리팹은 TRIANGLE_BUDGET으로 FAIL을 검출했다. 이는 불량 탐지의 정상 결과이며 처리본 검사와 분리한다. Blender와 Unity가 보고하는 삼각형 수를 혼용하지 않는다. 완료된 작업을 다시 resume했을 때 재처리하지 않고 기존 지문/결과를 확인하는 것도 시험했다.

## 직접 본 화면

Blender before/after와 실제 Unity camera 캡처를 직접 열어 확인했다. 바위의 큰 덩어리와 지면 접촉은 유지되고 누락된 메시·분홍 재질은 보이지 않는다. 경량화 후 면의 각진 느낌이 부드러워졌고 Unity 조명에서는 더 밝게 보인다. 이 관찰은 전송/표시 확인이며 원본 스타일 동등성이나 최종 미술 합격을 뜻하지 않는다. 실제 게임 카메라와 장애물/통로 판독은 NOT_RUN이다. 자동 요약의 visual_review_status는 NOT_RUN으로 보존하고 [직접 검토 결과와 이미지 지문](../../output/asset-pipeline/asset012-final/visual-review.json)을 별도 기록한다. 최종 Unity 실측은 삼각형9,492, 메시/재질 각1, basecolor/normal 각2048², 크기1.95134×1.971388×2, 바닥/중심 오차 약1.2×10⁻⁷, 금지 컴포넌트0이다. [완료 후 재개 결과](../../output/asset-pipeline/asset012-final/completed-resume.json)에서 job 내용/지문이 그대로이며 중복 처리·생성은 없었다.

## 실패 이력과 미해결 사항

1. 첫 Blender 제한 실행은 Metal 초기화에서 종료되어 BLOCKED로 기록됐다. 허용된 로컬 Blender 실행으로 같은 job을 재개했다. 원본·실패 출력은 보존했다.
2. 최초 Unity 실행 `c7be57c77c1e4b4ab8bc60e22fd283e4 / b57131691d8f46b48e40a4ef77962002`는 모델/화면은 통과했지만 Play 중 편집 전용 Snapshot 호출로 cleanup FAIL이었다. 중지 후 JsonUtility 복원에서도 scenes가 null이었다. Play 중에는 소유 정보만 읽고, 중지 후 JObject로 상태를 읽도록 수정했다.
3. 해당 실행에서 background CLI의 프로젝트 설정 변경과 `PrototypeFont.asset` 파일 변경도 탐지했다. background 설정은 정확한 원래 해시로 복원했고 원래 씬·선택·재생 정지·tick도 확인했다. **당시 폰트 파일의 이전 바이트는 백업이 없어 복원 BLOCKED**였다. 최초 원인 전체를 확정하지 않았으며 현재 파일을 임의 재생성하지 않았다. 원래 해시 `fde81941546290831f56390ecc9438e1b919e9f23fbddb3f0e9bf4df00a9d02c`, 이후 `2cb8e1d73c8b8cc4462ee32ce9050f435964232cadfaaddd0141690c1805bb77`. [수동 복구 기록](../../output/asset-pipeline/rock-21300150/c7be57c77c1e4b4ab8bc60e22fd283e4/unity-b57131691d8f46b48e40a4ef77962002/manual-recovery.json). 과거 FAIL을 PASS로 바꾸지 않았다.
4. 이후 Unity 시험부터 대상 파일 사전 바이트 백업과 해시 대조를 추가했다. `916e…` 시험의 백업211개·기존 파일 변화0은 독립 검토에서도 확인했다. 이후 전송 성공만으로 최초 폰트 변경의 원상 복원을 판단하지 않았다.
5. 기존 게임 회귀 뒤 미저장 dynamic font atlas 3개가 다음 시험 시작을 차단했다. 디스크 파일은 변하지 않았으며 직접 reimport와 texture unload/reload로 dirty 상태가 해소되지 않아 차단 기록을 보존했다. 독립 검토 후 씬/선택/폰트/TMP/대상 파일을 백업하고 Unity를 강제 종료 없이 정상 종료했다. **종료 시 PrototypeFont가 최초 시험 전의 `fde819…` 지문으로 정확히 돌아왔고 다른 대상 파일은 동일했다.** [최초 폰트 원본 복구 PASS](../../output/asset-pipeline/asset012-final/editor-restart/font-original-recovery.json)를 독립 검토자가 최초 target-before/현재 파일과 직접 대조했다. guard/ClearDirty/baseline은 완화하지 않았다.
6. 첫 `unity open --until-ready`는 ready 이후 연결이 사라져 상태/로그를 확인했다. `unity open --wait`로 실행 세션을 유지하여 같은 개인 프로젝트가 ready가 됐고 재시작 전 세션 tick을 복구했다. 명령 오류/차단은 성공 기록으로 사용하지 않는다.

최종 기록 경로·상태·소스 지문은 `output/asset-pipeline/asset012-final/final-checks.json`에 정리한다. 문서 기록 후 local/gate를 다시 실행하며, 문서 검사의 PASS를 실제 자산 시험이나 미복구 항목의 성공으로 대체하지 않는다.

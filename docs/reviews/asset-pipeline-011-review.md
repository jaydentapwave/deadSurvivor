# ASSET-PIPELINE-011 문서 반영 검토

2026-10-31 KST · L2 설계 작업. 세 공개 프로젝트의 필요한 설계를 기존 워크플로우와 맵 하네스에 연결했다. 코드/런타임/Unity/외부 설치/생성/hook/baseline 변경은 없다.

## 범위와 근거

- [채택 근거와 브리핑](../design/ai-3d-reference-adoption.md): 고정 커밋·스타 조회 시점·문서/코드 차이·AD01~07.
- [자산 워크플로우](../workflows/ai-3d-assets.md): A0~A5, CLI 유지, 작업 복구·원본 보존·후처리·Unity/시각 인수.
- [자산 하네스](../design/ai-3d-asset-harness.md): 별도 asset schema/sidecar, AC/AFI 변형, 사전 인수와 등록 트랜잭션, 무효화·gate 구현 순서.
- [자산 양식](../templates/ai-3d-asset-package.md) 및 기존 개발/맵 산출물/검증 gate/맵 구현·데이터 계약/착수 명세/맵 양식/CLI 결정/README·현재상태·할일·하네스·기획 입구 연결.
- 기존 맵 MC/FI, map v1 필드와 기하 판정은 유지한다. 전체 맵·자산 실행 검증을 이번 결과로 대신하지 않는다.

## 독립 검토

design-reviewer: `/root/asset011_design`, 관련 로컬 문서만 읽기 전용 검토. 브리핑 PASS 후 상세 계약에서 아래 두 지적을 받고 수정했다.

| 지적 | 실패 시나리오 | 수정 / 재확인 |
|---|---|---|
| AD-R01 | 등록 전에 AC-09 등록 후 검사를 요구하여 순환 의존·지문 재작성 발생 | pre-summary→불변 후보→pending→AC-09-post receipt→approved→역참조 없는 final summary. 재검토 PASS |
| AD-R02 | P2 Blender 구현 완료에 후속 Unity 측정/S2/S3까지 요구 | AC-04/05 process/import, AC-06 S1/S2/S3, A/P별 필수 변형 표로 분리. 재검토 PASS |

validator-client `/root/asset011_validator` 최종 결과: 스펙 준수 PASS / 동작 계약 PASS(문서 설계 정합성). 19개 변경 문서의 링크·map v1/MC/FI 보존·G5 추가 조건·AC/AFI 변형·등록/복구·불명 제출·미구현 gate 표현을 검토했으며 수정 필수 지적은 없다. 기하 계산/런타임 변경이 없어 algorithm-tester는 이번 작업에서 해당 없음이다. 실제 자산/Unity 실행은 NOT_RUN이다.

## 실제 검사

| 검사 | 상태 | 실행·근거 | 한계 |
|---|---|---|---|
| 사전 설계/상세 수정 재검토 | PASS | 위 design-reviewer 결과 | 문서 정합성, 실제 실행 아님 |
| local 문서·데이터·H1 분석 | PASS | `python3 Tools/Verify/run.py local`, [초기 결과](../../output/verification/20261031-143323-a6d8a576/summary.json), [검토 기록 추가 후 gate 내부 결과](../../output/verification/20261031-143438-3a08afbb/summary.json) | 최초 문서110개, 후속111개 링크/데이터 참조·H1 분석8개·소스 안정성. 새 자산 AC/AFI 아님 |
| 최종 독립 문서 검토 | PASS | 위 validator-client 결과 | 문서 정합성; 실제 실행 아님 |
| 통합 gate | PASS | `/usr/bin/python3 Tools/Verify/gate.py check`, [14:34:38 실행](../../output/verification/gate-20261031-143438-96224a45/gate.json) | 문서·준비151항목·원본/도면 지문·기존 런타임 증거 확인. 새 자산 gate 미구현 |
| 자산 AC/AFI·Tripo live·Blender·Unity | NOT_RUN | 이번 범위는 문서 설계 | 구현/단계별 수치·환경 인수 필요 |

기존 공개 조사 사본은 `output/research/ai-3d-popular-20261031/`에서 읽었다. 바위 전송을 새 자산 AC/AFI 성공으로 재사용하지 않는다. gate는 변경되지 않은 런타임/실제 대상에 일치하는 기존 `20261031-141344-c3389907` all --screens 증거를 참조했으며, 이번에 Unity를 새로 실행한 결과가 아니다.

문서 검토의 입력 지문·변경 전 사본은 프로젝트 `output/verification/asset-pipeline-011-docs/`에 기록한다. 위 gate 이후 본 검토 결과를 문서에 기록했으므로, 변경 완료 후 gate를 다시 실행해 최종 파일의 새 소스 지문을 남긴다. 그 실행의 원본 경로/해시/상태는 같은 output 폴더의 `final-checks.json`에 기록한다. 이 회고 문서를 결과 경로 변경만으로 반복 수정하지 않으며 완료 보고에는 마지막 gate 상태를 사용한다.

## 남은 실행 작업

P1 계약/작업 기록·가짜 공급자 복구부터 구현할 수 있다. P2~P4 후처리/Unity/등록·gate 및 G5 실제 맵 연결은 미구현이다. P5 live 생성에는 요청별 기능·계정·비용·공개 범위 확인이 필요하다. 기존 맵 H1 전체 인수/집계와 H2~H4는 별도 미완료로 유지한다.

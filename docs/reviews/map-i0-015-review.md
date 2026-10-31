# MAP-I0-015 착수 검사와 설계 검토

2026-10-31 KST · [I0 브리핑](../design/map-i0-015.md). 초기 단계는 읽기 전용 검사와 구현 책임 고정이었고 미저장 상태로 BLOCKED였다. 사용자 저장 확인 후 정상 저장·현재 전제 검사·새 회귀/복구·독립 검토/gate를 통과하여 **I0 완료(PASS)**로 판정했다. I1~I5 실제 구현/맵 동작은 NOT_RUN이다.

| 항목 | 상태 | 근거·한계 |
|---|---|---|
| 코드/데이터 비교 | PASS | [현재24개 비교](../../output/map-i0-015/comparison.json), Runtime/Editor C#·PrototypeData 일치. 자동 동기화0 |
| Editor/CLI 환경 | PASS | `output/map-i0-015/cli-version.json`, `editor-status.json`, environment 사본. Unity6000.4.3f1·CLI1.0.0-beta.15·PID15210·개인 대상 ready |
| 씬/설정 관측 | PASS | [Snapshot](../../output/map-i0-015/editor-snapshot.json): 정지·씬clean·선택없음·background=false·tick true/16·reload옵션꺼짐. 읽기 전후 파일 동일 |
| 초기 미저장 내용 보존/쓰기 전제 | BLOCKED(과거) | [초기 전제 검사](../../output/map-i0-015/preconditions.json): 당시 사용자 확인 전 미저장 내용을 보존하고 중단. 초기 final 기록은 initial-blocked-checks.json으로 보존 |
| 독립 설계 검토 | PASS | `/root/map015_design`, I0-D01 수정 재검토 PASS. 설계 지적 해소이며 I0 전체 실행 PASS 아님 |
| 사용자 확인 후 정상 저장 | PASS | [저장 결과](../../output/map-i0-015/resume-saved/save-result.json): Save Project 성공, 전후327파일 보존, PrototypeFont.asset1개 변경. 중간 raw dirty2개로 BLOCKED로 기록된 뒤 아래 분류로 전제 재판정 |
| 두 Font Texture의 분류 | PASS | [실제 속성](../../output/map-i0-015/resume-saved/font-cache-identification.json): 고정 원본·NotEditable·subAsset·dynamic Font 소유 참조·바이트 보존을 확인. 파생 캐시로 분류하며 raw dirty2개를 지우지 않음 |
| Unity 재시작·맵 이동/충돌 | NOT_RUN | 재시작 불필요, 실제 맵은 후속 I1~I5. 기존 all을 새 맵 검증으로 재사용하지 않음 |

I0-D01: 타입/이름과 디스크 지문만으로 미저장 Texture2D 내용의 소유권·재생성을 보장할 수 없다. 디스크 백업을 메모리 백업으로 대체하지 않고, 소유·재생성 근거 또는 복구 가능한 메모리 사본이 없으면 종료를 막도록 수정했다. 추가 읽기에서 PlayerSettings도 확인되어 종료/강제 종료/SaveAssets/ClearDirty/대상 쓰기를 하지 않았다.

사용자가 변경을 확인·저장했다고 응답한 뒤 I0-D02(원래 Save Project 저장·전후 보존), D03(호출/dirty/차이 gate)를 설계에 추가하여 재검토 PASS를 받았다. native SaveToSerializedFileAndForget 진단은 실제 파일0개로 실패하여 복원 백업으로 사용하지 않았다. EditorJsonUtility JSON은 설정 진단 관측으로만 보존했다. 이후 실제 Save Project가 성공했고, 편집 불가 Font 파생 캐시는 원본 소유 관계까지 확인하여 별도 독립 검토 PASS를 받았다. 현재 세션은 유지하고 어떤 dirty 플래그도 ClearDirty로 지우지 않았다.

채택한 다음 구현 경계는009 설계의 DSGame 단일 상태 작성자·LegacyNoMap/MapRequired·공통 프레임/명령 큐·순수 기하/이동 제안·독립 관찰/판정이다. I1/I2 실행 껍데기와 정상/FI 구현을 별도 묶음으로 남긴다. 알고리즘 코딩/수치 채택이 없어 이번 계산 동작 검증은 NOT_RUN이며 I2에서 algorithm-tester를 적용한다.

저장 후 현재 상태·양쪽 파일·패키지를 재확인하고 변경된 런타임으로 새 전체 회귀를 수행한다. local·최종 gate와 독립 validator-client 결과는 `output/map-i0-015/final-checks.json`에 시각/보고서/소스 지문과 함께 기록한다. 문서 gate PASS만으로 편집 상태의 미해결 차단이나 실제 맵 검증을 대체하지 않는다.

## 저장 후 회귀와 종료 상태

[새 all --screens](../../output/verification/20261031-154832-0379abd0/summary.json)는 컴파일·695규칙·실제 입력·3비율24화면·콘솔·복구·소스/대상 안정성 PASS다. 이는 기존 게임 회귀이며 I1~I5 맵 동작 검증이 아니다.

검사 후 정상 저장이 글리프/문자 매핑·atlas 데이터만 추가 반영한 결과는 [post-regression-save-result](../../output/map-i0-015/resume-saved/post-regression-save-result.json)에 FAIL로 보존했다. 독립 설계 검토에서 저장 체크포인트와 추가 변경의 범위를 확인한 뒤, 추가 파일도 보존하고 사용자 저장 직후의 PrototypeFont.asset 한 파일만 복구·동기 재임포트했다. .meta와 다른 파일은 유지했고 저장 확인 전 버전으로 복원하지 않았다.

[복구 결과](../../output/map-i0-015/resume-saved/checkpoint-restore.json)의 체크포인트 SHA는095b5fc67063776161b479761eeedda828ac3834569aed138ff42379abe7be98, 추가 변경 보존본은bc75dfe280677f14d6d6be5595e4ae4273a037f2468e7e45212eff174f4bfb21다. 복구 후 전체 런타임이 새 all의 gate_after와 정확히 같으며 기준 변경으로 맞춘 결과가 아니다.

[최종 전제](../../output/map-i0-015/resume-saved/final-preconditions.json): 동일PID/세션·씬/선택/background/tick 원상태, 편집 가능한 dirty0·확인된 편집 불가 Font 캐시 raw2·양쪽 코드 차이0. 재시작/ClearDirty는 하지 않았다. Native/JSON 진단의 실패와 초기 BLOCKED 이력은 보존한다. 최종 독립 검토/local/gate 근거는 final-checks.json에 기록한다.

최종 validator-client `/root/map015_validator`: 스펙/관측·복구 계약 PASS, 필수 지적0. 현재376파일과 새 all 종료 지문 일치, 추가 변경 보존본·정확한 사용자 저장 체크포인트 복구, 최종 편집 상태·local/gate 원자료를 독립 확인했다. 현재 I0는 PASS이며 초기 BLOCKED와 중간 FAIL은 역사 기록으로 유지한다. 후속 I1~I5는 NOT_RUN이다.

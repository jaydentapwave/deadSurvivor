# MAP-PREP-008 준비 통합 검증

- 날짜/범위: 2026-10-31 KST. L1 문서 통합, 제작 전 준비 1~4. 게임/하네스 코드·Unity 프로젝트 변경 없음.
- 산출물: [착수 명세](../design/map-first-implementation-spec.md), [불확실성 대장](../design/map-first-implementation-uncertainty.csv), [선택 기록](../decisions/map-first-trial-baseline.md). README·현재상태·기획 입구·할일·규격/하네스 현황 연결 갱신.
- 적용 원본: 007 정책/배치/보정/하네스, 기존 단계별 MC/FI, 006 변환 계약. 005/007 사례·원본 PNG·layout·과거 inputs 지문을 수정하지 않음.
- 원본 확인: 보유 VerdantMeadowsFullMap.png를 이미지 도구로 직접 다시 확인. 새 이미지/도면 생성 없음. 새로운 원작 충돌 관찰이나 시각 정밀 측량으로 보고하지 않음.

| 검사 | 상태 | 근거 | 한계 |
|---|---|---|---|
| 대장 등록/참조 대조 | PASS | [73개 확인](../../output/verification/map-prep-008-20261031-130137/preparation.json), 입력 SHA-256 포함 | 69행+경계, 원본 포인터·RT·역할·C BLOCKED 보존. 충돌 타당성 아님 |
| 초안 gate | PASS | [실행 기록](../../output/verification/gate-20261031-130136-05afafdf/gate.json) | local 및 151개 기존 맵 준비 검사. 최종 문서 수정 후 다시 실행 |
| 독립 validator-client | PASS | prep008_validator: 스펙 준수 PASS / 문서상 동작 계약 PASS, 필수 수정 없음 | 실제 Unity 동작 검토 아님 |
| 실제 G1 전체·G2~G5 인수 | NOT_RUN | 실행 없음 | C/수치/입력열/하네스 미완료, 실행 준비 BLOCKED |
| Codex native Stop 신뢰/발동 | NOT_RUN | 이전 설정 상태 유지 | 이번 준비 범위에서 자동 활성화 주장 안 함 |

이번 기록은 새 실행 증거를 사용하며 과거 게임 PASS를 현재 맵 PASS로 재사용하지 않는다. 최종 gate는 모든 문서 수정 후 새 실행 폴더의 gate.json에 입력 지문과 함께 남긴다.

독립 검토의 선택적 의견에 따라 README의 과거 04/03 표기를 이전 적용안/원본 분석 자료로 정리했다. 규격/하네스 문서에는 이미 구현된 007 정적 검사와 미구현 런타임의 현황 구분을 반영했다.

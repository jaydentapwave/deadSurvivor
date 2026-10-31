# 023 장애물과 전투 연결

- 규모 L2. 사용자 요청 1–3 중 세 번째. [설계와 CH-021 범위 결정](../design/map-playable-021-023.md). 별도 opt-in 개발 장면이며 원본 DeadSurvivor 장면과 007/018 자료는 보존한다.
- 구현: 적 반경18/25별 안전 경로·생성·재배치, 돌진 sweep 제한, 양측 투사체의 피해보다 먼저 벽 검사, 근접/연쇄 각 홉/펄스/폭발의 시야 검사. 회복은 벽 통과 허용하며 사망자는 회복으로 부활하지 않는다. 원형 효과를 확정 피해 영역으로 표시하지 않고 실제 명중 숫자를 표시한다.
- 탐색: 격자32·방문2048 상한, 이동 경로2회와 생성/재배치1회를 별도 예산으로 배정한다. 적 갱신 시작 순서 순환, 실패 정지/재시도, 생성 quota 보존. 작은 통로의 경로 완전성·대규모 성능을 보장하는 알고리즘은 아니다.
- 독립 구현/계산 검토 지적 해소: NAV-01/V6 벽 가까운 대상에게 접근 목표를 반경+4로 설정해 근접 범위 진입, NAV-02 실패 적의 탐색 순서 편향 해소, V5 성공 재배치의 오래된 경로 제거. 각각 실제 동작 반례를 추가했다. 시험의 큰 좌표 상한과 화면 설정 누출도 수정했다.

## 현재 소스의 실행 증거

2026-10-31 22시 KST. 각 보고서는 실행 시각·소스 지문·원자료/PNG 해시를 보관한다.

| 대상 | 결과 | 근거 |
|---|---|---|
| 전투·이동 경계/정상 대조군 | PASS,1698회 assertion | `output/map-blockout-018/playable-23-fdb9a88bcd254bd285dc0f82488ae569/summary.json` 및 units.json |
| 독립 정확 기하 판정 | PASS,659구간 | 같은 폴더 independent-path-oracle.json. 반경18/25의 계획 경로와 각320실제 적 이동 구간을 Fraction oracle로 별도 판정 |
| 실제64후보 맵 | PASS,240스텝의 파티 점유·적 구간 검사 | 같은 units.json의 actual-map-* 및 game.json/PNG |
| 실제 프레임 | PASS,frame6→10,elapsed12.15→12.35 | 같은 frame-progress.json. 최종 화면 적38개, 실제 파티·HUD·명중 숫자 |
| 양측 투사체 | PASS | 벽 관통 시 피해·명중·폭발 없음과, 열린 경로 정상 명중 대조군 |
| 근접/연쇄/펄스/폭발/회복 | PASS | 각 피해 원점의 시야와 회복 정책·사망자 비부활 |
| 좁은 접근·탐색 공정성·재배치 | PASS | 벽 근처 실제 피해, 앞20실패 적 뒤의 접근 가능 적 이동, 재배치 ID/HP 유지·경로 무효화·100안전 구간 |
| 현재 소스의 022 파티 회귀 | PASS,79검사 | `output/map-blockout-018/playable-22-7baf7e850973431b99e456f24e8a17b1/summary.json` |
| 기존 게임 전체 | PASS | `output/verification/20261031-222929-9ee89d0a/summary.json`. 컴파일·695규칙·입력·24캡처·프레임·화면·정리 |
| 새 저장 장면 생성 | PASS | `output/map-blockout-018/playable-23-build/summary.json`, 새 scene/meta만 생성·편집 상태 복원 |
| 저장 장면 직접 Play/출전/D입력 | PASS | `output/map-blockout-018/saved-023-2735c1bebd1a4f9db9cb8d0f309ea21b/summary.json`. 실제 저장 장면의 바인딩과 프레임·시간·중심 이동, 가상 입력 제거·편집 상태/파일 복원 |

독립 역할 play_algorithm이 현재 소스61개·원자료39개 지문과 수학/실행 증거를 검토해 PASS. play_validator가 코드·기능·전체 게임/022/저장 장면 증거를 별도 검토한다. 실행 중 새 콘솔 오류0, 캡처 카메라 전후 일치, 소스 안정성, 기존 파일·장면·선택·배경 설정 복원 PASS. 공유 PrototypeFont 사용자 원본 fde819… 유지.

## 사용과 남은 범위

Unity에서 `Assets/DeadSurvivor/Scenes/Map021/Map023_CombatExperimental.unity`를 열어 Play, 생존자 선택, 출전한다. WASD 이동, Space 일시정지, 1 정비 시험, 기존 UI로 진형·부활·다음 웨이브를 이용한다. 추가 Tripo 크레딧은 사용하지 않았다.

전체 G2/G3/G4, full map v1 세션/FI/watchdog, 모든 통로·진형·목표 위치 조합, 최종 충돌 실측·미술/시각 인수, 407마리 등 최대 규모 성능, 빌드 타깃별 동작은 NOT_RUN이다. 이번 기능 PASS로 승격하지 않는다. 사용자 최종 화면 채택은 별도다.

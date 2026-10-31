# MAP-MEADOW-007 r001 — 근거대장

2026-10-30 KST. 확인 가능한 주장과 그 한계를 함께 기록한다. 외부 자료 확인은 공개 웹과 프로젝트 로컬 파일에 한정했다. 출처 링크가 있다는 이유만으로 우리 설계의 타당성이 입증되지는 않는다.

| ID | 실제 확인 자료와 위치 | 지지하는 사실/사용 범위 | 지지하지 않는 주장 |
|---|---|---|---|
| E01 | [원본 지도 PNG](../art/maps/references/VerdantMeadowsFullMap.png), 1195×1110. [출처 파일 페이지](https://thespellbrigade.wiki.gg/wiki/File:VerdantMeadowsFullMap.png), [맵 페이지](https://thespellbrigade.wiki.gg/wiki/Verdant_Meadows). 로컬 이미지를 직접 열어 확인 | 전역 군집, 불규칙 외곽, 위 가로길/중앙 굴곡/아래 분기, 좌상 곡선·우상 작은 군집·우하 사선·좌하 끊어진 원형. 각 OB의 원본 위치는 005 검색 박스 및 007 V 점으로 역추적 | 원작 충돌, 숨은 지면, 정확한 크기, 카메라 각도, 원작 동작. 현재 웹 직접 열기는 403이며 새 원본/최신 버전 대조 성공으로 표시하지 않음 |
| E02 | [005 객체](map-meadow-005-r001-objects.csv), [통로](map-meadow-005-r001-routes.csv) | 기존 ID·원본 검색 범위·후보 경유점의 연속성 | 실측 윤곽·안전 경로 아님. E01에서 파생된 내부 분석이지 독립된 원작 증거 아님 |
| E03 | [006 계약](map-meadow-006-r001-contract.md), [통로 정책](map-meadow-006-r001-route-policy.csv) | 좌표식·띠 형태 보존·RT018 내부 연결 필요성의 설계 근거 | 실제 s/k·폭·충돌·정책 사용자 채택 아님 |
| E04 | [DSGame.Formation.cs](../../UnityMigration/Runtime/DSGame.Formation.cs), FormationOffset/SelectFormation | 1~4인 재중심, 세 진형, 고정 슬롯, 정비 때 PendingFormation 선택 | 장애물 대응·진형 압축·자동 회전은 구현되어 있지 않음 |
| E05 | [DSGame.Combat.cs](../../UnityMigration/Runtime/DSGame.Combat.cs), Simulate | 이동140, 추종 min(1,12dt), 죽은 캐릭터 이동 생략, 정비/일시정지 시 진행 안 함 | 이동 반경·지형 충돌·안전한 부활 보장 없음 |
| E06 | [DSGame.Run.cs](../../UnityMigration/Runtime/DSGame.Run.cs), Offset/NextWave | 슬롯 수 Selected.Count, 출발 시 Pending 적용, 웨이브·시간·적 상태 변경 순서 | 출발 사전 충돌 검사 없음 |
| E07 | [DSGame.Inventory.cs](../../UnityMigration/Runtime/DSGame.Inventory.cs), Revive | 정비 무료 부활, 현 Formation의 슬롯 배치 | 부활 지점 충돌 검사 없음 |
| E08 | [DSWorld.cs](../../UnityMigration/Runtime/DSWorld.cs), 캐릭터 Draw/체력바; [화면·하네스 사례](맵-하네스-사례명세.md) | 실제 표현과 신체 충돌은 별도 측정 대상. 기존 화면비 시험 범위 참조 | 체력바 폭36이나 피격 반경을 신체 이동 반경으로 사용하면 안 됨 |
| E09 | [Unity 6.0 CCD 공식 문서](https://docs.unity.com/en-us/engine/6000.0/manual/physics-section/physics-overview/collision-section/collision-detection/continuous), Continuous collision detection 본문, 2026-10-30 확인 | 스텝 사이 충돌을 놓칠 수 있어 구간 검사가 필요한 기술 근거 | CCD 설정만으로 사용자 정의 Vector2 이동/Lerp가 보호되거나 정확성이 보장된다는 뜻 아님 |
| E10 | [Unity Camera.orthographicSize](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/camera/orthographicsize), Remarks, 2026-10-30 확인 | 직교 투영 세로 범위=2×size, 가로는 화면비에 의존 | 기울어진 카메라에서 지면에 보이는 길이와 세로 범위를 동일시하면 안 됨. 원작 투영/각도는 입증하지 않음 |
| E11 | [Unity 공식 테스트 안내](https://unity.com/how-to/automated-tests-unity-test-framework), Test two ways/Character movement tests, 2026-10-30 확인 | 데이터/Editor 검사와 실제 여러 프레임에 걸친 Play Mode 시험을 구분하는 근거 | 이번에 UTF 설치/실행했다거나 기존 도구가 맵을 검사한다는 뜻 아님. 문서의 예전 패키지 버전을 설치 권고로 전용하지 않음 |
| E12 | [04 적용안](3D배경-우리맵-적용설계-04.md), 크기와 카메라 실험 | 45/55도, 0.85/1/1.15 비교안의 최초 내부 출처. 한 변수씩 비교하는 기존 계획을 유지 | 원작에서 측정한 수치 아님. 외부 근거 없는 실험 간격이므로 후보로만 사용 |
| E13 | [하네스 원본](맵-워크플로우-하네스-설계.md), [사례 원본](맵-하네스-사례명세.md), [검사기](../../Tools/Verify/run.py) | 단계별 필수 목록, FI-01~10, 상태·지문·복구 규칙, 기존 local의 제한 | 원작의 개발 도구를 재현했다는 주장이나 런타임 PASS 근거 아님 |
| E14 | [공식 Steam 소개](https://store.steampowered.com/app/2904000/The_Spell_Brigade/), About/소개, 2026-10-30 확인 | 1~4인 온라인 협동 서바이버 게임이라는 배경 | 우리 게임의 한 입력 고정 진형·막힘 대응을 그 게임의 검증된 규칙이라고 추정할 수 없음 |
| E16 | [DSGame.cs](../../UnityMigration/Runtime/DSGame.cs), Update | 프레임 dt를 min(unscaledDeltaTime,.05)×Config.speed로 요청하고 E05에서 남은 웨이브 시간으로 다시 제한 | .05가 배속 후 최종 dt 상한이라는 주장 불가 |
| E15 | 사용자 요청과 [산출물 규격](../workflows/map-artifact-contract.md) | 내부 벽·돌/원본 충실도/건축 도면 표현/일관성/제작 보류라는 직접 요구 | 규격 준수는 재미·충돌 타당성의 증거가 아님 |

근거 종류: OBSERVED(이미지/코드 관찰), DERIVED(관찰에서 계산), PROPOSED(우리 설계 판단), UNKNOWN(출처로 결정 불가). PROPOSED에는 출처뿐 아니라 선택 이유·검증 사례·실패 시 재검토 조건을 붙인다. 모든 신규 정책은 사용자 최종 채택 및 런타임 검증과 분리한다.

007의 폴리곤 점은 E01 수작업 거친 외피 관찰이다. 픽셀 좌표가 정수로 기록됐다고 ±1px 정확도를 주장하지 않는다. C와 V가 같은 객체는 외피 전체를 막아 보는 보수적 시험 후보로 선택한 것이며, 실제 원작 충돌의 복원 결과가 아니다. U는 해당 외피의 접지 불확실 영역이고 오차 허용폭이 아니다. E01:OB-xxx 표기는 원본 이미지의 005 검색 영역과 007 정점 목록을 지칭한다.

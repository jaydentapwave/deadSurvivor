# MAP-BLOCKOUT-018 r001 — 3D 배치·바위·크기 비교

2026-10-31 · L2 · 상태: 별도 미리보기 구현. 실행 상태는 [검증 기록](../reviews/map-blockout-018-review.md)을 따른다. 사용자 요구는 기본 도형으로 실제 맵 구조/장애물을 배치하고 기존 바위로 크기·가림을 확인하며 구현을 계속하는 것이다. 원본은 [007 layout](map-meadow-007-r001-layout.json), [006 좌표](map-meadow-006-r001-contract.md), [원본 이미지](../art/maps/references/VerdantMeadowsFullMap.png)다. 원본 이미지를 직접 열어 외곽/64군집/곡선 띠와 하단 원형 내부의 빈 공간을 확인했다. 관찰 C는 여전히 후보/BLOCKED이며 원작 충돌 정답으로 승격하지 않는다.

## 범위와 제공물

기존 게임 씬을 바꾸지 않고 별도 `MapBlockout018_r001` Unity 씬을 만든다. source69행 중64개의 C 다각형을 그대로 높이 있는 기본 메시로 생성하고, 조사 경계4행은 개별 벽으로 만들지 않으며 PERIM-01 한 외곽으로 표시한다. OB-G01 비충돌 부모와060~063 분리 조각의 빈 내부를 보존한다. 원본 위치/회전/종횡비를 늘이거나 객체를 빼지 않는다. 007 공통 도면 네 판은 배치 원본으로 연결하고 3D 캡처는 별도 판으로 보존한다.

기존 처리 바위 prefab 하나를 읽기 전용으로 참조해 한 후보 OB 안에 배치한다. 자산/모델은 변경하지 않으며 충돌은 C를 유지한다. 모델의 바닥 중심과 가로 투영 반경을 측정하고 C 내부의 증명된 원에 들어가도록 표시 scale을 정한다. 최종 A5/G5 채택이 아닌 배치/가림 시험이다. 새 Tripo 생성/비용은0이다.

`DSMapBlockoutPreview`는 이 장면 전용의 4인 원기둥 표시와 키보드 탐색을 제공한다. DSGame.FormationOffset와 DSMapMotion을 재사용한다. 원래 DSGame 전투/부활/정비·명령 큐를 연결한 것으로 표시하지 않는다. 이동140·추종12dt·예약 슬롯·반경8은017 시험 값/기존 공식을 보존하고 s=4/6/8, k=.02, 장애물 논리 높이 h_q=16/24/32(월드 .32/.48/.64), proxy 논리 높이36(월드 .72)은 명시적 탐색 제안으로 기록한다. 최종 몸체/축척/카메라 채택이 아니다. 원점 P01=[565,580], X=k*s*(px-565), Z=-k*s*(py-580), Y=k*h_q다. 카메라45/55/90도는 지면 기준 elevation이며 같은 방위각/중심/직교 크기에서 비교한다. 처음 P01의 실제/예약 안전성은 사전 질의 후 시작하며 무효면 더 좁히거나 벽을 지우지 않고 BLOCKED다.

전체 맵 좌표가 기존 fixture 범위1024를 넘으므로 `DSMapGeometry`에 명시적 `coordinateLimit`(기본1024 유지, 최대16384)을 추가한다. DSMapMotion은 해당 geometry 범위로 초기/후보를 검사한다. Python 독립 oracle는 기존 기본범위를 바꾸지 않고 관측 좌표·반경/epsilon을 s로 나눠 원본-원점 정규화 공간에서 판정한다. 변환 스케일을 몰래 신체/속도에 중복 적용하지 않는다.

## 구현 파일과 소유권

- `Tools/Verify/map_blockout.py`: 후보 자료 인수/원본·자산 지문·경계/64 ID 검증, 전개 JSON/별도 검증 실행과 집계. 현재 local/all 의미와017 CLI는 유지한다.
- `Runtime/DSMapGeometry.cs`, `DSMapMotion.cs`: 명시적 범위만 확장. default1024와 모든 기존 사례는 유지.
- `Runtime/DSMapBlockoutPreview.cs` 및 메시/데이터 보조 파일: 단순 다각형 ear clipping과 extrusion, XZ 지면·경계/64장애물·4인 탐색·직교 카메라(45/55/90). 새 물리 Collider는 쓰지 않고 논리 solver가 차단한다. 자료 누락/잘못된 다각형·triangulation 실패는 중단한다. hull/bbox 대체 없음.
- `Editor/DSMapBlockoutVerification.cs`: 새 장면/자료만 생성하고 기존 prefab 의존 지문을 고정한다. 기존 source/target 비교 후 적용. 씬/자료는 새 고유 폴더에 저장하고 기존 파일이 있으면 내용/소유권 확인 없이 덮지 않는다. Build Settings/기존 씬/PlayerSettings/Font/패키지 저장 금지.
- UI는 기존 uGUI/TMP 방식의 작은 조작 설명·상태/후보 표지만 제공한다. WASD 탐색·전체/근접 시점·45/55도 비교를 제공하며 실제 게임 HUD 검증으로 전용하지 않는다. 표제에 PREVIEW/후보를 명시한다.

preview 정적 메시/재질은 새 폴더 소유이며 원래 바위/공유 재질을 수정하지 않는다. 종료 시 런타임 proxy용으로 생성한 네이티브 메시/재질만 해제한다. 새 preview 폴더에만 생성 메시/재질과 scene/data를 저장하여 Play 전에도 전체 3D 배치를 볼 수 있게 한다. 원래 게임/바위/공유 자산은 저장하지 않는다. 런타임 proxy 메시/재질과 전용 TMP font/material/atlas만 종료 시 해제한다. 기존 TTF는 읽기 전용으로 참조하며 PrototypeFont의 동적 캐시를 공유하지 않는다. 정적 배치는 s4로 저장하고 s6/8에서는 XZ만 같은 비율로 표시하며 높이/파티 크기는 유지한다. 바위는 별도 자식으로 위치만 변환하고 실제 크기는 고정해 모델 비율을 찌그러뜨리지 않는다.

## 실제 검증과 복구

017의 검증된 OS lock/pending·SessionState/run/nonce/PID·root marker·ready/phase 복구 경로를 재사용하고, 새 preview root 하나를 명시적으로 등록한다. marker/다른 owner·예상 밖 Play 전환을 우회하지 않는다. 이때 생성할 root의 이름/ID를 계획/등록하고 이후 root set을 대조한다. 씬 native settings는017 보호값을 변경하지 않고 local light/camera/material로 표현한다. 캡처는 호출 전 intent, 소유 해시/부재 검사, PNG/meta 삭제와 전체 이전 대상 지문 대조 후 pending 제거를 유지한다. 새 저장 장면/자료는 생성 완료 후 별도 인수된 입력이며 검증 중 변경하지 않는다. 부모 finally 정리와 외부 watchdog 미구현을 구분한다.

검사 계획: 독립 설계+계산 검토→메인 구현→단위/로컬→독립 코드 검토/수정→현재017 actual42 회귀→018 실제 프레임/키보드/표시와 자료 비교→기존all --screens→독립 실행 근거 검토→문서/local/gate.

- 64 ID/vertex 변환 왕복·메시 바닥 projection과 C 일치, concave triangulation 면적/내부 보존·위/아래 winding·변 extrusion 확인. 원본 도형 hash와 생성물 연결을 기록.
- s4/6/8: 초기 안전성·동일 입력/시간의 이동·벽 앞 정지/반전·독립 Center/실제/예약 sweep. 실패 후보를 숨기거나 통과시키려고 크기를 바꾸지 않는다. 대표 입력은 진단이며 전체 RT/G3 성공을 주장하지 않는다.
- 동일 위치45/55/전체90도 캡처와 frustum 바닥 교차/화면비 기록. 바위/파티 바닥 투영·화면 가림을 기록한다. 최종 visibility 임계값/실기기 성능은 미채택.
- 정상/실패 자료, 기존 바위 dependency 변조·좌표범위·mesh area/누락·소유 root 불일치 거부 확인. 실제 GameView 프레임과 입력/관측 스텝 결합, 콘솔/정리·기존 파일 불변 확인.

이번은 G2/G3 전 제작 후보를 눈으로/동적으로 확인할 수 있는 별도 preview다. 기존 I2 전체/map v1·I3 안전 전환·전체 FI/G2 인수·I4 최종 수치 채택·G3/G5는 남는다. 사용자 구현 요청을 해당 게이트의 소급 승인으로 취급하지 않는다. 설계/검사 PASS와 실제 맵/미술 채택은 구분한다.

## 사용법과 구현 결과

Unity 메뉴 `Dead Survivor > Map Blockout 018 > Open Preview Scene` 또는 `Assets/DeadSurvivor/Scenes/Map018_r001/MapBlockout018_r001.unity`를 연다. 저장 장면은 Play 전에도 지형/64개 메시/바위를 표시한다. Play에서 4인 proxy와 조작 안내를 만든다. WASD/방향키 이동, Tab 전체/추적, C 45/55도, V 바위/파티 초점이다. 기본 저장 축척은 s4이며 Inspector의 ImageScale 후보4/6/8은 Play 전에 정한다. 원래 게임 시작 씬/Build Settings는 유지한다.

```sh
python3 Tools/Verify/map_blockout.py prepare
python3 Tools/Verify/map_blockout.py build
python3 Tools/Verify/map_blockout.py verify
```

`build`는 새 폴더가 있을 때 덮지 않는다. 저장 중 실패는 `build-pending.json`과 `created-files.json`을 보존하며 `recover-build`가 기존 파일 불변/원래 scene setup/확인된 새 파일 지문을 모두 검사한 뒤 해당 폴더만 제거한다. 정상 저장으로 새 재질이 정규화된 경우, 원 journal을 바꾸지 않고 완료 Build 보고서의 경로·SHA·동일 파일 집합을 별도 동결한 `--completed-build-input`이 필요하다. 단순히 실패를 피하려고 기준을 다시 쓰는 방식은 허용하지 않는다.

Play 중 사용하는 TMP font는 기존 TTF에서 새로 만들며 font/material/atlas를 종료 때 해제한다. 기존 공유 PrototypeFont를 저장하지 않는다. URP light 보조 구성요소는 소유 트리 봉인 전에 준비한다. 백그라운드 실행은 Play 진입 후에만 켜고 종료 전에 원래 값으로 되돌려 Enter/Exit Play 자동 저장의 PlayerSettings 부작용을 막는다. 소유 트리 검사는 완화하지 않는다.

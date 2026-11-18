# F1 기획서 근거 메모

[plan.md](plan.md)의 긴 근거. 2026-11-15에 파일을 직접 읽어 확인한 것.

## 렌더 설정 (F절)
- `ProjectSettings/GraphicsSettings.asset` 기본 파이프라인 = `PC_RPAsset`(guid 4b83569d…). `QualitySettings.asset` 품질 단계 Mobile → `Mobile_RPAsset`(guid 5e6cbd92…), PC → `PC_RPAsset`, 지금 단계 1(PC).
- 두 RP 자산의 `m_RendererDataList`는 항목 하나(PC f288ae1f…, Mobile 65bc7dbf…), `m_DefaultRendererIndex: 0`.
- `Assets/Scenes/`·`Assets/DeadSurvivor/` 장면의 카메라 `m_RendererIndex`는 6개 모두 −1(기본).
- 그래서 목록 끝에 2D 렌더러를 덧붙이고 새 카메라만 1번을 고르면 예전 장면의 렌더는 바뀌지 않는다. 품질 단계·Graphics 설정의 RP 자산을 바꾸면 편집기와 예전 장면 전체가 바뀐다.
- `Packages/manifest.json`에 `com.unity.2d.*` 패키지는 없고 `com.unity.modules.tilemap`과 URP 17.4가 있다.
- Player Settings: `defaultScreenOrientation: 4`(자동 회전), 세로 두 방향 0, 가로 두 방향 1. `activeInputHandler: 1`(Input System).

## 예전 수치에서 온 값 (E절)
- 일반형 적: HP 20, 피해 5, 속도 63, 반지름 18(`PrototypeData.json` `monsters`).
- 단궁: 피해 13, 간격 0.7, 사거리 8(`items`). 사거리 × 42 = 336(`DSGame.EquipmentCombat.cs` 79행).
- 접촉: 거리 ≤ 반지름 + 5, 쿨 1초(중장갑 1.8)(`DSGame.Combat.cs` 293~299행). 받는 피해 × (1 − 방어)(14행).
- 생성 거리: `ViewWidth 1180`, `ViewHeight 748`의 대각선 절반 + 70(`DSGame.cs` 13행, `DSGame.Combat.cs` 67행).
- 기본 진형: (−24,−24)·(24,−24)·(−24,24)·(24,24)(`DSGame.Formation.cs` 14행).
- 버닝 타임: 6웨이브부터, 시작 = 웨이브 시간 ÷ 2, 10초, 간격 = 일반 생성 간격 ÷ 2, 일반 80%·돌진 20%(`DSGame.Combat.cs` 111~131행). F1은 유형 비율을 빼고 임시 적 한 종류로 낸다(유형은 F4).

## `unitScale` 출발값 (FA4)
- 예전 화면 폭 1180단위를 가상 폭 640px에 맞추면 0.5424px/단위. PPU 48이라 월드 유닛으로 0.0113.
- 2:1 압축 때문에 세로로는 360px ÷ (0.5424 × 0.5) ≈ 1327단위가 보여 예전(748)보다 바닥이 세로로 더 보인다. 그래서 적 생성 거리를 고정값(768)으로 두지 않고 "보이는 바닥 영역 대각선 절반 + 70"으로 계산한다(예전 규칙과 같은 뜻).
- 거리 감각이 어색하면 F2에서 데이터 값만 바꾼다.

## 판정 경로 시간 배율 (H절)
- 경로 전체를 20배로 돌리면 웨이브 1(30초)이 1.5초 만에 끝나 G2의 "남은 시간이 줄어든다"를 확인하기 전에 정비로 넘어갈 수 있다. 그래서 1배로 시작하고 G3·G7의 `do`에서 `Time.timeScale`을 올린다.
- `tools/playable.py`는 `do`를 `script eval`로 그대로 실행하므로 문장 여러 개를 쓸 수 있다(`Tools/playable.py` 207행). 단계 id는 `[A-Za-z][A-Za-z0-9_-]*`(98행)라 `f1-flows.json`의 `P1`·`WIN` 등이 맞다.

# DQ 본 게임 적용 7단계 — 빛 설계 브리핑

- 상태: 구현 전 검토 중. 구현·Unity 명령·캡처·마감 판정은 아직 하지 않았다.
- 기준 커밋: `98b6e2f186bdd17bcbaf02f4400070a119dd3a16` (8단계 병합 직후).
- 브랜치: `claude/pack-expedition-2d-dq-apply`.
- 규모: **L2**. PC/Mobile URP 자산의 soft-shadow 지원이 모두 꺼져 있다. 이를 켜고 Main 전용 후처리/그림자 수신을 연결하는 렌더 파이프라인 설정 변경이다. 색·조명 수치 조정만으로 분류하지 않는다.
- 요청: 20261123-night-1to6 7단계 및 디렉터 05:55 추가. 총 상한 100분, 마감 리뷰 한 번과 수정 확인 한 번. 이번 응답은 사용자 지정 Codex→Claude 사전 리뷰 경계에서 멈춘다.

## 원본과 현재 확인한 사실

원본은 `docs/art/아트-기준서.md` r2, `docs/features/dq-apply/시각-요소-목록.md` 16~21행 및 몸 재질의 빛 수신 열이다. 앞 단계 입력:

- `.director/outbox/20261123-night-1to6-5-r3.md`
- `.director/outbox/20261123-night-1to6-6.md`
- `.director/inbox/20261123-night-1to6-8-r2.md`

직접 읽은 코드에서 확인한 사실:

| 소유처 | 현재 | 이번 변경 방향 |
|---|---|---|
| `Assets/Settings/{Mobile,PC}_RPAsset.asset` | `m_SoftShadowsSupported: 0`, shadow distance 35 | 이행 메뉴가 soft shadow 지원을 설정. 카메라 거리/실제 보이는 바닥까지 그림자 도달 확인 후 거리 결정. 성능 측정하지 않음 |
| `03.Resources/Settings/HD2D_BattleVolume.asset` | Vignette active, intensity 0.52, smoothness 0.04; Bloom threshold 2 | Main 전용 DQ 프로필로 명시 연결, Vignette off/0. 타원형 어두운 띠의 **원인 후보**이며 실제 스택/켬끔 쌍으로 인과 확인 전 확정하지 않음 |
| `03.Resources/Shaders/HD2D/HD2D_FieldSurface.shader` | shadowAttenuation을 step(.5)로 자르고 고정 갈색 계수 곱함 | soft attenuation 수신·채도 유지 음영. 형상/배치/높이/UV/가림 계약 보존 |
| `03.Resources/Shaders/HD2D/HD2D_SpriteLit.shader` | 고정 ambient (.43,.32,.27), hard step 수신; atlas flip·Appearance·깊이 bias·3패스 보유 | DQ2의 SampleSH/광색/노출 및 셀 경계 보간을 이 계약 안에 이식. 시안 shader 전체 교체 금지 |
| `Effect/HD2DView/View/UprightContactShadow.cs` | 프레임별 실루엣 메시+불투명 발 픽셀 GPU readback 캐시 | DQ에서는 공유 둥근 얼룩 경로로 교체. 동작 프레임마다 readback/mesh 생성하지 않음 |
| `Effect/Motion/FootShadowView.cs` | 몸 sprite 정점 폭으로 폭 변경, terrain normal에 회전 | 같은 둥근 얼룩 텍스처/공유 재질 사용, 지형 높이에 붙임. atlas 여백 폭 사용하지 않음 |
| `Features/Party/View/MemberView.cs` | shadow caster와 contact 초기화·terrain 전달 | DQ 얼룩 한 벌로 연결, 옛 하드/실루엣 중복 그림자 제거. 동작 재생/타격 소비 코드 유지 |
| `Features/Monster/View/MonsterView.cs` | FootShadowView, sprite 변경시 몸 폭 계산 | 둥근 그림자 연결만 수정. DQ 동작/phase/pool 계약 유지 |
| `Features/Field/View/FieldView.cs:33~50` | 비평지에서 `footShadowMaterial` 복제 후 `HD2D_GroundShadow`로 강제 교체, OnDestroy에서 복제본 해제 | DQ 전용 serialized 공유 그림자 재질을 평지/비평지 공통 반환. DQ는 복제/셰이더 강제교체 경로를 타지 않음. 기존 비DQ 경로와 그 해제 계약은 유지 |
| `03.Resources/Shaders/HD2D/HD2D_{GroundShadow,ContactShadow,FootShadow}.shader` | GroundShadow는 Point alpha .5 clip·고정 농도, Contact/FootShadow는 정사영식 receiver 깊이 복원 | DQ 경로에서 사용하지 않음. 신규 `DQ_BlobShadow.shader`에 연속 alpha·원근 깊이 수신 구현. 옛 shader를 단순 재사용한다고 가정하지 않음 |
| `03.Resources/Shaders/HD2D/HD2D_BandBlur.shader` | 상단 uv.y .76부터, 고정 2px/혼합 .5; 깊이 참조 없음 | 상단 .75부터 시작, 대원 깊이 초점·몬스터 띠 보호를 실제 카메라 깊이와 연결. 월드 HP/숫자 뒤처리 순서 보존 |
| `Effect/Ambient/AmbientEffectsPresenter.cs` | 기존 pool 소유 ParticleSystem, 전투 진입/종료/Dispose 정리 | 새 시스템 없이 기존 먼지 프리팹을 작은 빛점으로 조정. 입력/게임 난수와 분리 |

경로 앞의 `02.Scripts/`, `03.Resources/`는 `Assets/Game/` 기준이다.
시안 출발값은 `Editor/HD2D/RoundDQ1/DQLookdevBuilder.cs:335`의 해 Euler(48,35,0), intensity1.05, color(1,.96,.86), ambient(.035,.09,.065), Bloom1.4/.1/.4. **출발값이지 본 게임 PASS 값이 아니다.** DQ2 `CharacterLit.shader`의 canopy map은 고정 월드 범위·시안 전용이므로 복사하지 않는다. 본 게임 실제 URP 그림자·환경광을 사용한다.

## 변경 범위와 계약

새 이행 진입점 계획: `Assets/Game/02.Scripts/Editor/MainSceneDQLightMigration.cs`, 메뉴 `DeadSurvivor/DQ/Apply Main light`.

1. 실행 전 Play 아님, 대상 프로젝트/열린 Main/저장 상태 확인. 입력 누락은 fail-fast. 이전 활성 장면과 변경 대상 자산의 바이트를 보존하여 실패 시 복구한다. 성공 때 자산/장면 저장 후 dirty=false 확인.
2. Main 전용 DQ 볼륨/빛 재질을 명시적으로 만들거나 갱신한다. 이미 있으면 생략하는 방식 대신 승인된 값으로 재적용한다. Main의 해 1개·RenderSettings·Volume 참조·캐릭터 프리팹 연결·DQ renderer 기능 설정을 메뉴 하나가 소유한다.
3. PC/Mobile URP의 soft shadow 지원을 둘 다 켜서 품질 전환 시 모양이 달라지지 않게 한다. 다른 renderer feature·렌더 배율·카메라18°/FOV20·AA 정책은 유지한다. shader에 soft-shadow variant를 추가하고 수신에서 hard step을 제거한다.
4. 둥근 그림자는 공유 bilinear alpha 텍스처/공유 재질을 쓴다. 몬스터마다 재질 복제·프레임별 GPU readback 금지. **재질 공급처·수신 셰이더까지 아래 DR-01 계약으로 교체**하며, 옛 depth/bank-mask 구현을 그대로 쓴다는 뜻이 아니다. 크기는 불투명 몸 폭, 이동·hop·fall에서도 발 앵커는 유지. 죽음/풀 반환/재사용/Dispose에서 고아 그림자 0.
5. 캐릭터 셰이더는 atlas rect 바깥 이웃 셀을 읽지 않게 범위를 제한하고 padding을 포함해 검사한다. Point 임포트·PPU48·피벗·flip·Appearance·피격 흰색·깊이 bias·DepthOnly 및 그림자 alpha 계약 유지. 대기/걷기/공격 atlas가 바뀌어도 같은 sharedMaterial 진입점 사용. 투사체 등 같은 셰이더 소비처에 DQ 전용 조명 옵션이 원치 않게 적용되지 않도록 재질 단위로 분리한다.
6. 흐림은 기존 full-screen pass 안에서 매개변수화한다. camera depth로 초점의 선명함을 보존하고 게임 몬스터가 설 수 있는 띠의 세기를 제한한다. UI/HP/피해 숫자는 기존 후처리 뒤 패스. 먼 대기는 땅/소품/캐릭터 간 같은 월드 깊이 정책으로 밝고 푸르게, 초점 근처에는 영향 최소화. 배경 albedo 자체를 파랗게 다시 칠하지 않는다.

예상 수정: 위 표 파일, Main, DQ renderer/신규 DQ volume·그림자·몸 재질, 관련 prefab, `HD2D_FieldSurface.shader`의 조명 구간, 필요 시 물 재질의 발광 값, 기존 캡처 도구의 7단계 수집 연결, 관련 EditMode 시험. 새 texture는 게임에서 실제 쓰는 둥근 그림자만. 배경 모델/원본 그림/배치 JSON/높이 데이터/규칙·세이브·공격 시점·판정 경로 기대식 변경0. 구현 중 표 밖 파일이 필요하면 원인과 최소 범위를 기록한다.

### DR-01 — 재질 공급·연속 alpha·원근 받침면 (필수 수정 반영)

추가 구현 범위를 명시한다: `Features/Field/View/FieldView.cs`, 신규 `03.Resources/Shaders/HD2D/DQ_BlobShadow.shader`, 신규 `Effect/HD2DView/View/DQBlobShadow.cs`, 신규 `03.Resources/Materials/HD2D/DQ_BlobShadow.mat` 및 실사용 그림자 texture. 기존 `UprightContactShadow`·`FootShadowView`는 비DQ 호환용으로 남기고 DQ actor에서는 새 컴포넌트 한 벌만 활성화한다.

- **소유권**: Main light 편집기 이행 메뉴가 공유 texture/material **영속 자산**을 생성·갱신하고 Main FieldView의 새 serialized `dqBlobShadowMaterial` 및 DQ actor prefab에 같은 자산을 연결한다. `FieldView.TerrainShadowMaterial`은 DQ 자산이 연결됐을 때 지형 평탄 여부보다 먼저 그 자산을 반환한다. 따라서 기존 `new Material`+shader 강제교체 분기를 DQ는 타지 않는다. 공유 자산은 runtime이 만들거나 Destroy하지 않는다. 기존 `_terrainShadowMaterial`은 비DQ 전용이며 FieldView의 기존 OnDestroy 소유를 유지한다. `WorldPresenter`의 대원 SetGround/몬스터 SetTerrain 호출이 전달한 참조도 같은 공유 자산이어야 한다.
- **메시 소유**: actor view가 DQBlobShadow를 한 번 초기화하고 그 컴포넌트가 actor당 작은 지형 추종 메시 한 벌만 소유한다. 기존 `GroundDecalMath`/terrain grid를 재사용하고 받침면·위치·몸 폭이 바뀔 때만 같은 메시 버퍼를 갱신한다. 몸의 애니메이션 atlas를 그림자 텍스처로 쓰지 않는다. 비탈·턱에 대한 vertex 높이를 grid로 구하고 작은 offset으로 띄운다. 풀 반환은 renderer 비활성, 재사용은 기존 컴포넌트/메시/공유 자산 재사용, OnDestroy에서 자기 메시만 해제. 비DQ 그림자 컴포넌트는 DQ 초기화 때 비활성화하여 중복 표시0.
- **연속 alpha**: 전용 bilinear alpha 텍스처를 `sampler_LinearClamp`로 읽고 `factor = lerp(1, shadowTint, saturate(alpha * opacity))`를 출력하여 `Blend DstColor Zero`, ZWriteOff로 곱한다. alpha .5 clip·Point 샘플·고정 농도·기존 stencil의 첫 조각 독점은 사용하지 않는다. alpha0은 항등색1이며 완전 투명 texel만 버려도 중간 alpha는 유지한다. 수백 actor가 같은 material/texture를 사용하며 색/농도를 actor별 material/MPB로 복제하지 않는다. 서로 다른 actor 얼룩이 겹칠 때의 곱셈 중첩과 한 actor의 옛 그림자 중복을 구분한다.
- **원근 수신**: DQ shader는 `DeclareDepthTexture.hlsl`의 SceneDepth를 읽고, reversed-Z/플랫폼 near-clip 처리를 거친 device depth와 `UNITY_MATRIX_I_VP`로 `ComputeWorldSpacePosition`을 사용하여 실제 receiver를 복원한다. `lerp(near,far,raw)`는 금지. 복원된 표면과 지형에 맞춘 그림자 위치의 거리/높이를 비교하여 다른 받침면·수직 턱으로 번지는 부분을 거부한다. 기존 깊이 허용오차 .04m를 넓혀 통과시키지 않는다. 은폐된 물체/배경 뒤 얼룩도 ZTest LEqual과 receiver 검사로 거부한다.
- **깊이/물 순서**: Main camera의 URP depth texture를 명시 요구하고 DQ renderer의 depth copy가 opaque 뒤·그림자 transparent draw **전에** 생성되는지 확인한다. DQ 얼룩 queue2990, 기존 DQ 물 queue3000 유지(물은 ZWriteOff). bank mask는 공유 material에 한 번 연결하며 월드 bounds는 실제 기존 마스크 데이터에서 읽는다. 바닥 받침 종류는 메시 vertex 데이터로 전달하여 actor별 material 복제 없이 물/턱의 다른 높이 영역을 거부한다. mask는 분류 보조이며 원근 depth 수신을 대체하지 않는다. 원근 receiver world 오차를 알려진 평지/비탈/턱 표본에서 확인한다.
- **L7-03/L7-08에 고정할 검증**: 평지→비탈→물가→턱 이동 중 대원/5종의 공급 재질·shader·texture instance ID가 동일하고 DQ runtime material clone0, alpha0/.25/.5/.75/1 표본 출력과 경계4배, 지형 접촉·다른 받침면 침범0을 확인한다. 풀 반환→재사용에서 같은 공유 자산/메시를 재사용하고 renderer 복구, actor Destroy 뒤 자기 메시 제거·공유 자산 생존, FieldView 파괴로 공유 material이 사라지지 않음을 확인한다. depth 사전입력 누락은 성공으로 처리하지 않는다.

### DR-02~04 — 공유 설정·atlas·패스 순서 구체화 (후속 제안 반영)

- **DR-02 공유 영향**: PC/Mobile RPAsset의 soft-shadow 지원은 여러 renderer가 함께 쓰므로 Main 전용 변경이 아니다. 이 공유 변경은 기존 `Light.shadows=Soft`인 다른 장면의 모양에도 영향을 줄 수 있음을 기록한다. 의도한 공유 설정 변경은 soft 지원과 필요 shadow distance뿐이며 각각 변경 전/후와 이유를 기록한다. 이행 전후 다른 Light·재질·renderer 자산의 값/해시가 그대로인지 대조하고, 같은 RPAsset을 쓰는 renderer 목록을 기록한다. Main 전용 volume/feature/material 연결과 구분한다. 대상 외 Light/renderer 값은 바꾸지 않으며 성능 계측은 하지 않는다.
- **DR-03 atlas 갱신 소유**: MemberView/MonsterView 각각의 모든 `bodyRenderer.sprite =` 경로를 동일 private sprite 적용 함수로 모은다. sprite 대입과 **같은 호출 안에서** 해당 textureRect의 UV min/max를 반 texel 안쪽으로 줄인 `_DQSpriteUVRect`를 renderer property block에 기록한다. shader 네 tap은 각각 이 rect로 clamp한다. 별도 LateUpdate에서 뒤늦게 갱신하지 않는다. GetPropertyBlock→rect 갱신→SetPropertyBlock으로 Appearance/기존 블록을 보존하며, MonsterView.AdvanceAppearance도 기존 Get→Set을 유지한다. flip은 기존 vertex 처리로만, rect는 원본 UV 범위다. static/idle/walk/attack/hit/fall/lying·방향·초기화·pool 재사용 모두 이 함수를 거친다. 관련 시험은 frame 전환 첫 렌더의 rect/texture 일치 및 Appearance 보존, 좌우반전·atlas 가장자리4배 누출0을 확인한다. SpriteAtlas rotation off/FullRect를 이행에서 검증하고 불일치는 fail-fast한다.
- **DR-04 depth와 event600 순서**: DQ_MainRenderer의 full-screen blur feature는 현재 requirements0이므로 메뉴에서 `ScriptableRenderPassInput.Depth`를 명시한다(숫자 상수 대신 설치된 URP enum/API 사용). 위 DR-01의 pre-transparent depth 생성 요구와 별개로 선언한다. feature의 기존 color-buffer 요청은 유지한다. 두 feature가 event600인 상태에서 목록은 **blur→world indicators**를 명시 설정하고 메뉴2회에도 동일한 순서를 유지한다. capture에서 실제 feature type/name/order/event/requirements, 활성 depth texture 크기/생성 시점, 알려진 깊이 표본 값을 기록한다. 같은 프레임으로 흐림 띠 안의 HP·피해 숫자 켬끔 쌍을 직접 열어 blur가 표시에 선행하는지 검증한다. 깊이·순서 불일치 시 L7-04/L7-08 FAIL이며 입력 없음 fallback으로 PASS 처리하지 않는다.

## 단위와 최초 연결

- U0 병합 확인: 사전 리뷰 PASS 후 **가장 먼저** `python3 -B tools/editor_ready.py "$PWD"` → `Apply Main screen` → `Apply Main background` → `Apply characters`, 매 단계 저장 확인 → 짧은 Play1장 직접 열기. 여기서 두 단계 접점 결함은 이 단계에서 수정.
- U1 해/하늘빛/그림자/가장자리: Main light 메뉴+설정+수신 shader. 컴파일0, 같은 프레임 빛/그림자/Vignette 켬끔 쌍. 첫 실제 연결은 새 배경 위 새 대원·몬스터의 그림자/그늘 변화.
- U2 발밑 얼룩·대원 빛: shared resource 연결, 관련 시험과 그늘→햇빛 이동 연속, 풀 반환/재사용 경계 확인.
- U3 화면 효과: 초점·상하 흐림·대기·Bloom·소량 빛점. 효과 켬끔 동일 프레임 비교, 몬스터 띠4배 crop 직접 열기.
- U4 마감: 관련 EditMode 및 디렉터 요구 전체 묶음1회, playable1회, 아래 캡처/측정, 단위 커밋 후 마감 리뷰 요청. FAIL 묶음만 수정, 해당 시험과 FAIL 리뷰만 수정 확인. 성능0.

각 단위 메뉴2회 성공 응답·참조·자산 hash 동일을 확인한다. 기존 화면/배경/캐릭터 메뉴를 다시 돌려도 마지막 Main light 재적용으로 같은 결과여야 한다. 단위별 컴파일/범위 시험 후 지정 파일만 stage·커밋. 런타임 전체 판정 중복 실행 금지.

## 수용 기준 (결과와 분리)

| ID | 사용할 수 있게 될 결과 | 확인 방법 |
|---|---|---|
| L7-01 | 병합 후 새 배경 위 새 대원/몬스터, compile0·메뉴 저장 상태 정상 | U0 순서 실행 로그·소스 지문·Play 캡처 직접 열람 |
| L7-02 | K43 해1개+하늘빛, 좌상단 해, 진하고 약간 부드러운 유색 그늘 | 실제 Light/Volume/품질/URP값 기록, 동일 프레임 켬끔, C4 기준서3.9 원문 범위. C3·C5·C6·C9도 원문 기준으로 영향 확인 |
| L7-03 | 몸통폭 둥근 얼룩·부드러운 경계·받침 높이에 붙음·옛 하드 중복0 | 대원/5종 몬스터·평지/비탈/물가/턱·이동/쓰러짐/풀 재사용. 동일 sharedMaterial/texture 참조와 자원 정리 시험, 화면4배 |
| L7-04 | K44 대원 깊이 선명, 상단25% 흐림·몬스터 띠 읽힘, 밝고 푸른 먼 곳 | 켬끔 쌍·행별 ROI 사전선언, C7 맨위/초점 .15~.5·몬스터띠/초점≥.7, 위쪽 몬스터4배. 아래 흐림도 K44/기준서와 대조 |
| L7-05 | Vignette0·가장자리 어두운 타원 띠0, Bloom은 물/빛/마법에만, 빛점 소량 | 실제 volume stack/타 활성볼륨 조회, 같은 내용 켬끔 및 이동 연속 비교. 재질별 HDR/Bloom mask 표본. 수치만으로 화면 PASS 주장 금지 |
| L7-06 | K54/K55 대원이 그늘·빛색 수신, 햇빛 풀 대비 L*−4~−10, sharpness .7~.87 | 몸/풀 ROI 분리·alpha로 배경 제외, sRGB→Lab 방식 기록, 그늘/햇빛 동일원화 쌍. atlas 경계/flip/새 동작 타격·피격 보존 확인 |
| L7-07 | 6단계 같은 범위의 맵/화면 검증, 문턱 보존 | 조감+시작/길/물가/다리/야영지/가장자리+이동8장, 1920×1080/2340×1080/1440×1080. 정상 입력으로 경계 도착 확인; 선언/hash/직접 본 경로·SHA256·관찰 기록 |
| L7-08 | 게임 계약 불변·멱등 메뉴·관련 시험 양수·한 판·리뷰 완료 | compile0, 관련 EditMode+전체 묶음1회, playable 기존 경로1회, 메뉴2회 hash같음/복구/dirty0, spec-reviewer·validator-client·algorithm-tester 마감 묶음 |

측정은 기준서3.9의 C3~C8 중 빛/색 영향 항목과 K54/55를 남긴다. C8≥25는 추정 문턱으로 FAIL을 지우지 않되 독립 화면 판독 없이 혼자 재색칠 반복을 열지 않는다. 기존 C4 .60274 FAIL은 앞 단계 보고이며 이번 성공/실패값이 아니다.

본게임 관문은 `tools/hd2d_main_gate.py`의 기존 연결을 읽고 원근 대응된 범위만 사용한다. 사전 ROI→plan→capture→finish의 source/hash 결속을 지킨다. 원근 미지원으로 BLOCKED인 기존 관문은 빛 단계에서 문턱/카메라 기대를 낮추지 않고 그대로 남긴다(완전 대응은9단계). 새 검사 도구를 만들기보다 기존 수집/측정에 필요한 최소 옵션만 보탠다. 추가 검사는 같은 프레임 켬끔 증거로 기존 중복 정지 캡처를 대체할 수 있는지 회고에 적고, 필수 범위는 빼지 않는다.

## 이미 결정된 것·기준 문서와 다른 점

- 걷기6~8 Aether 대신 사용자 확정24 조각 회전, idle12/attack7/fall10+lying. 8단계 확정 예외 Bruno fall12+lying도 유지. 동작을 다시 만들지 않는다.
- 피격 흰 깜빡임+1px 밀림 유지. 이번은 조명 재질과의 양립만 본다.
- 소품6은 기존 모델+무료 새질감, 나무3/덤불1(결함2종 제외). 재생성/유료0.
- K16 UI 유지, 새 카메라 자리·크기만9단계. 성능K47은 별도 마감, 이번 측정0.
- 옛 정사영30°·도트 배경·올리브 값은 폐기. 킷 템플릿의 하드 실루엣/저해상도 배경 규칙보다 기준서의 매끈한 배경·부드러운 둥근 얼룩을 우선한다.
- **배경 모델·배치6단계 몫, 이 단계 판정 밖. 새 대원/몬스터 그림·동작8단계 몫, 이 단계 판정 밖. 효과·투사체·예고 라인·UI 그림9단계 몫, 이 단계 판정 밖. 성능은 별도 단계, 이 단계 판정 밖.** 기존8단계 코드는 그대로 유지하며 빛 접점 회귀는7단계가 책임진다.
- 디렉터가 본 다리·천막·깃발 도트 격자는6단계 잔여로 기록만. 나무 각진 형상은 모델 변경 없이 조명으로 나아지는 만큼만.
- 전체 EditMode 앞 보고5FAIL(원본 누락2/슬롯102대153/30°대18°/깊이 기대)은 합친 현재 다시 실행해 분류. 옛30° 시험만 새18° 기준 근거로 수정 가능; 실제 결함을 기대식 완화로 숨기지 않음. 이전 N9 FAIL·C11 원본부재·boar 가린 표본 미확인은 해소 전 유지.

## 환경·복구·현재 관문 상태

- 쓰기는 이 worktree만. 원래 프로젝트의 미커밋 파일을 읽거나 가져오지 않았다. 허용 output/outbox 외 원본 접근 금지. push/PR/merge/배포/외부 서비스/패키지 설치0.
- 시작에 기존 미추적 RoundDQ2/3 `.meta` 다수만 있었으며 수정/삭제/stage하지 않았다.
- 이 worktree에 지시문의 `.ai/rules/25d-{screen-structure,render-pitfalls,ui,effects}.md` 4개가 없다. 같은 킷의 `skills/25d-art-setup/template/root/.ai/rules/` 원본을 직접 읽었다. 규칙 설치·전역 설정 변경은 하지 않았다. 실제 훅 주입이 실행됐다고 주장하지 않는다.
- project-doctor `--host codex --require design.write`: configured/findings0, exit0. 설정 검사일 뿐 컴파일/런타임 근거 아님.
- **컴파일·메뉴 재실행·EditMode·playable·캡처·측정: NOT_RUN.** 사전 설계 리뷰 전에는 구현·자산 변경하지 않는 사용자 지시에 따라 보류. 과거 PASS는 현재 PASS로 이어 쓰지 않았다. 현재 본 캡처0.
- design-reviewer 최초 판정: **`VERDICT: FAIL`**. 사용자 전달 기록: Codex gpt-6.1-sol/medium/read-only, 82초, job `20261123-055224-364f9a`. 최초 브리핑 SHA256 `8215e55b3e1bb8edce48f83a5da933c1f5f66adbd2ad41ae47c64994b1c19c25` 일치 확인. 이번은 DR-01 설계 수정1회이며 수정 확인 판정은 아직 없다. spec/validator/algorithm은 구현 뒤 요청. 자기 검토를 독립 리뷰로 표시하지 않음.
- 실제 원본 PNG의 부재/허용 경로 문제는6단계에서 남았다. 구현 마감 때 허용된 reference 폴더 원본의 존재를 확인하고, 없으면 E1만으로 원작 대조 PASS를 쓰지 않는다.
- 예외 복구: 캡처의 카메라·timeScale·renderer/volume 임시 상태는 finally에서 복원. 실패한 이행은 저장 전 변경을 복구하고 시작 hash와 대조. 다른 세션 자산은 되돌리지 않는다.
- 시간/토큰: 이번은 읽기·설계 단계만 수행. 세부 native 사용량은 실행기 집계가 없어 미확인(0으로 쓰지 않음). 전체100분 상한에 사전 리뷰 전 사용 시간도 포함하여 재개 시 남은 예산을 확인한다. 광범위 문서 출력 잘림이 발생해 핵심 절을 재조회했으며 효율 개선 후보는 파일/절별 제한 조회다.

## 수정 확인 입력 — DR-01 수정1회

필수 DR-01을 수용했다. 원인은 그림자 소비 view와 texture 연결만 추적하고, `WorldPresenter`가 전달하는 `FieldView.TerrainShadowMaterial`의 비평지 분기 및 최종 shader의 alpha/depth 식을 끝까지 읽지 않은 것이다. 공급처/셰이더를 위 표와 DR-01 절에 추가했다. 후속 DR-02~04도 이번 브리핑에 반영했다. 수용 기준 L7-01~08 및 숫자 문턱은 유지했다. 반박으로 돌려보낸 지적은 없다.

워크플로우 갱신 후보는 이 설계의 DR-01 검증에 반영했다: 재질 변경 검토 시 **소비 view→재질 공급자→지형 분기→최종 shader**를 추적하고 평지/비평지 자원 동일성을 확인한다. 별도 전체 검사 도구는 추가하지 않고 기존 L7-03/L7-08 수명·경계 검사에 포함한다. 프로젝트 공통 워크플로우 파일의 추가 변경은 최종 회고 때 결정하며 현재는 미반영이다.

다음 열린 지적은 최초 리뷰에서 그대로 유지한다(이번 문서 수정으로 닫지 않음):

- 범위 밖/증거 없음 — design-reviewer: 기존 N9 FAIL, C11 원본 부재, 전체 EditMode 5 FAIL, 원근 Main 관문 BLOCKED는 이번 설계 수정으로 해소되지 않음.
- 증거 없음 — design-reviewer: 위 내용은 코드·문서를 정적으로 확인한 것임. 화면 품질과 런타임 수명은 미확인.
- 기준 문서 — design-reviewer: 지정된 25d-* 규칙을 작업 트리에서 찾지 못했음. 브리핑이 언급한 대체 원본의 내용은 독립 확인하지 못했음.

대체 규칙의 독립 확인 경로는 `/Users/hyeongwoo/Desktop/Workspace/studio-kit/plugins/studio-kit/skills/25d-art-setup/template/root/.ai/rules/` 아래 `25d-screen-structure.md`, `25d-render-pitfalls.md`, `25d-ui.md`, `25d-effects.md`다. 원본의 정사영/도트/하드 그림자 견본보다 이번 사용자 지시·기준서가 우선한다. 이 경로를 전달하는 것은 앞 리뷰가 확인한 것으로 바꾸어 쓰는 뜻이 아니다.

이번 수정의 검사는 문서 수준이다: 코드·자산 변경0, 수용 기준8개/DR-01~04 대응·참조 파일 존재·문서 공백 검사. Unity 컴파일/메뉴/EditMode/Play/캡처는 계속 NOT_RUN이며 설계 PASS 뒤 U0에서 시작한다.

## 리뷰 요청

`design-reviewer`에게 최초 FAIL의 DR-01 수정 확인을 요청한다. 함께 구체화한 DR-02~04와 L7-03/L7-04/L7-06/L7-08의 계약 일관성을 본다. 기준은 L7-01~08이며 숫자/범위를 낮추지 않았다. 위 열린 지적은 다시 리뷰를 여는 이유가 아니다. 이 문서는 구현 전 계획으로 픽셀 결과나 컴파일 성공 근거가 아니다. PASS 뒤 U0부터 시작한다. 최종 핸드오프 `.director/outbox/20261123-night-1to6-7.md`는 사용자 지정대로 구현·명령 관문·커밋·마감 리뷰 판정을 받은 뒤 작성한다.

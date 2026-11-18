# HD-2D 사전 조사: 바로 적용할 수 있게 정리

- 작성: 2026-11-17, Claude(디렉터). 사용자 원문: "한번 정릴르 쭉해서 추후에 바로적용을 할 수 있게 해줘 , 문서로 남겨나줘"
- 짝 문서: [HD-2D 전환 계획](hd2d-전환-계획.md). 계획은 "무엇을 어떤 순서로", 이 문서는 "어떤 값과 방법으로"다.
- 조사 범위: 에디터를 쓰지 않고 할 수 있는 것 전부. 다음 네 갈래로 나눠 동시에 조사했고, 디렉터가 결과를 모아 확인했다.
  1. 유니티 기술: 웹 문서와 설치된 URP 17.4.0 패키지 소스
  2. 이 프로젝트의 설정과 자산
  3. HD-2D 화면 레퍼런스
  4. studio-kit 구조
- 표기:

| 표기 | 뜻 |
|---|---|
| [소스 확인] | 이 프로젝트에 설치된 패키지 소스를 직접 읽음 |
| [파일 확인] | 이 프로젝트의 파일을 직접 읽음 |
| [공식 문서] | Unity 6000.4 문서. 다른 판이면 판을 적음 |
| [커뮤니티] | 포럼·블로그 |
| [계산] | 공식으로 계산한 값 |
| [추측] | 확인 안 됨 → 시안에서 본다 |

- 이 문서의 값은 **시작값**이다. 휴대폰 실기기 숫자는 아직 하나도 없다.

## 1. 결론: 바로 적용할 결정

| # | 정할 것 | 결론(추천) | 근거 | 시안에서 확인 |
|---|---|---|---|---|
| D1 | 카메라 방식 | **시안에서 둘 다 찍고, 추천은 "정사영 3D + 위아래 흐림"** | 원근은 화면 위쪽 도트가 아래쪽의 0.37~0.73배로 작아진다[계산]. 픽셀 흔들림은 정사영에서만 완전히 없앨 수 있다[커뮤니티]. URP 흐림(DoF)은 정사영에서 맞지 않지만, 화면 위아래 흐림은 카메라와 상관없다[소스 확인]. 지금의 보이는 범위 계산을 거의 그대로 쓸 수 있다 | 같은 장면을 정사영과 원근(화각 20°)으로 나란히 비교. 원근이 주는 깊이감을 사용자가 고른다 |
| D2 | 카메라 각도 | 정사영이면 **30°**가 지금과 같은 바닥 범위다(세로 0.5 = sin 30°)[계산]. 40°는 바닥이 덜 보인다(세로 7.5 → 5.8칸)[계산]. 레퍼런스 전투 화면은 33~50°이고 출발값 후보는 약 40°다(5절) | 4절, 5절 | 30°·40° 캡처 비교 |
| D3 | 도트 재질 | **URP Lit Shader Graph**: Opaque, Alpha Clip 0.5, Two Sided, 좌우 반전·색은 Custom Function(`UnityFlipSprite`, `unity_SpriteColor`), 법선 고정, 그림자 내기·받기는 스크립트로 켬 | `Sprite-Lit-Default`는 3D 렌더러에서 빛을 안 받고(Unlit로 그려짐) 그림자 패스도 없다[소스 확인]. 지금 게임은 `Sprite-Unlit-Default`를 쓴다[파일 확인] | 좌우 반전·색·그림자가 함께 되는지(버그 IN-127136 증상), 명암이 자연스러운지 |
| D4 | 도트 선명도 | Point 필터·mip 끔(이미 그렇다[파일 확인]), **안티에일리어싱 끔**, **Render Scale 1.0**(모바일 설정이 지금 0.8[파일 확인]), 스프라이트는 카메라 각도만큼 기울여 세움(돌리는 빌보드 아님) | FXAA·SMAA는 픽셀 경계를 뭉갠다. 1 미만 배율은 업스케일로 흐려진다[추측]. 카메라를 돌리지 않으니 기울이기가 단순하다[추측] | 카메라가 따라 움직일 때 떨림(연속 캡처) |
| D5 | 화면 위아래 흐림 | **화면 Y 기준 흐림**: Full Screen Pass Renderer Feature + Fullscreen Shader Graph, 코드 없음 | 정사영에서도 되고 DoF보다 싸다[소스 확인·추측]. 원근을 고르면 Gaussian DoF(먼 쪽만 흐림, Max Radius 1, 고품질 끔)도 후보. Bokeh는 쓰지 않는다 | 흐림 띠 폭(레퍼런스 5절 값으로 시작) |
| D6 | 빛 번짐·색 | Bloom: Dual 필터, Quarter 축소, 반복 3~4, 고품질 끔, 문턱 1.0. Tonemapping Neutral, Vignette 0.2, HDR R11G11B10, Grading LDR | [공식 문서](Bloom 모바일 권장). 예전 3D 판 `Look045`(ACES, Bloom 0.3, 화이트밸런스 +6, 대비 8, 채도 12)는 참고값 | 컨셉 색(K7)과 측정 대조 |
| D7 | 그림자 | 해 1개, 단계(cascade) 1, 1024~2048, 거리 = 보이는 범위, **하드 그림자**. **대원 4명과 큰 소품만 실제 그림자**, 몬스터는 바닥에 눕힌 그림자 판(지금 `shadow.png` 24×12) | 그림자 패스는 그림자를 내는 것마다 그리기 1회를 더한다. 400마리면 비싸다[추측]. 예전 049에서도 몬스터 바닥 원 그림자를 채택했다 | 몬스터 실제 그림자 켬·끔 비교 |
| D8 | 많이 그리기 | **SRP Batcher 켜기**(지금 PC·모바일 모두 꺼짐[파일 확인]). 스프라이트는 아틀라스 1장 + 재질 1개. 넘치면 몬스터만 `Graphics.RenderMeshInstanced` | SpriteRenderer의 SRP Batcher 지원은 2023.1부터, 6000.0.38에서 회귀 수정[Unity 포럼]. GPU Resident Drawer는 MeshRenderer 전용이고 OpenGL ES에서 안 된다[공식 문서] | Frame Debugger에서 묶이는지, 그리기 수 |
| D9 | 땅 | **커스텀 메시**(예전 046 방식의 삼각 격자) 또는 ProBuilder(설치 필요). Unity Terrain은 쓰지 않음. 질감은 **1유닛 = 48텍셀**, Point | 도트 밀도를 캐릭터와 맞춘다(PPU 48). Terrain은 모바일 비용과 필터 제어 문제[추측] | 바닥 도트와 캐릭터 도트가 같은 크기로 보이는지 |
| D10 | 예전 소품 | **모양은 쓴다, 칠은 새로.** 예전 모델은 면마다 팔레트 칸 단색이다(질감 없음)[파일 확인]. 우리 색으로 바꾸는 건 팔레트 그림 교체로 쉽고, 도트 질감은 **월드 투영(삼면 투영) 도트 질감**으로 UV 없이 입혀 본다 | 043 셰이더에 이미 위에서 투영·디더 페이드 기능이 있다[파일 확인] | 팔레트 바꾸기만 vs 투영 도트 질감 |
| D11 | 전투장 크기 | 지금 전투장은 변 45m 마름모(약 2,048㎡, 캐릭터 1m 가정)다. 예전 046은 82×81m(영웅 0.8m)이다 → 캐릭터 1m로 맞추면 약 102×101m라 **3~4.6배 넓다**[계산]. 예전 배치 도구(`layout_046.py`)로 새 경계에 맞춰 다시 뽑는다. 한 구역을 잘라 쓰는 것은 시안용 | 3절 | 시안은 한 구역 |
| D12 | 가려짐 | 소품은 **디더 페이드**, 대원 4명은 스텐실 실루엣(Render Objects, 코드 없음). 가림 감지는 카메라→대원 4회 | [추측] 비용 최소안 | 디더 무늬가 도트와 어울리는지 |
| D13 | 렌더 기능 만들기 | 시작은 코드 없이(Full Screen Pass, Render Objects). 직접 만들면 Render Graph(`RecordRenderGraph`)로 | Unity 6 URP는 옛 방식(Compatibility Mode)을 없앤다[공식 문서] | Render Graph Viewer에서 패스가 합쳐지는지 |
| D14 | 앞뒤 정렬 | 깊이 버퍼가 맡는다(Opaque + 알파 클립). 2D 렌더러의 "화면 Y 정렬"은 필요 없음 | D3 | 겹친 몬스터 정렬 |
| D15 | 인물 화면 크기 | **지금 13.3% 그대로 시작**하고, 시안에서 10%를 한 판 더 찍어 비교 | 레퍼런스는 대개 10%(8~14%)다. 작게 하면 몬스터 무리가 더 많이 보이지만 도트 확대 배율이 줄고(1080줄에서 3배 → 2.25배), 아트 기준서 K1·K2(48px, 13%)와 판정 기준이 바뀐다[계산] | 13.3% vs 10% 캡처 |
| D16 | 정수 배율 | 3D 렌더러에는 도트 고정 카메라가 없어서 **기기마다 정수 배율을 보장할 수 없다**. 1080줄·13.3%면 3.0배로 정수다[계산]. 다른 해상도에서는 도트 폭이 1px씩 고르지 않을 수 있다 | 배율이 3배 이상이면 덜 보인다[추측]. 낮은 해상도로 그린 뒤 키우면 정수 배율이 되지만, 3D 배경까지 뭉개져 HD-2D가 아니게 된다[추측] | 2340×1080, 2048×1536 캡처에서 도트 고르기 |

## 2. 기술 상세

### 2.1 도트 재질 (D3)

- [소스 확인] `Sprite-Lit-Default`의 패스는 `Universal2D`, `NormalsRendering`, `UniversalForward`이고, `UniversalForward`는 Unlit 함수다. 그래서 3D 렌더러에서는 빛을 받지 않는다. ShadowCaster 패스도 없다.
- [소스 확인] Shader Graph의 Sprite Lit·Sprite Custom Lit 타깃도 ShadowCaster를 만들지 않는다.
- [소스 확인] 좌우 반전(flipX)과 스프라이트 색은 셰이더가 처리한다(`Core2D.hlsl`의 `UnityFlipSprite(pos, unity_SpriteProps.xy)`, `unity_SpriteColor`). 두 변수는 `UnityInput.hlsl`에 선언돼 있어 Custom Function에서 읽을 수 있다.
- [커뮤니티] Unity 6.1에서 Debug 인스펙터로 Cast Shadows를 켜도 Sprite 셰이더로는 그림자가 안 생겼고, Lit로 바꾸면 생겼다. 6000.0.60에서 `unity_SpriteColor`를 곱하면 그림자 패스가 깨진다는 보고가 있다(IN-127136, 해결 여부 미확인).
- **만드는 법**:
  1. URP Lit Shader Graph를 만든다. Opaque, Alpha Clipping 0.5, Two Sided.
  2. Vertex에서 Custom Function으로 `UnityFlipSprite`를 적용한다.
  3. Fragment에서 메인 텍스처 × `unity_SpriteColor`.
  4. 법선은 고정값(위쪽에 가까운 방향)으로 덮는다. 스프라이트 메시에 법선이 없을 수 있다[추측].
  5. `SpriteRenderer.shadowCastingMode = On`과 `receiveShadows = true`는 코드에서 켠다. 일반 인스펙터에는 없다.
- 카메라 쪽으로 기울인 판의 그림자는 해 각도에 따라 짧거나 얇게 나올 수 있다. 그러면 "그림자만 내는" 세운 판을 따로 둔다[추측].
- 맞았을 때 하얗게 번쩍이는 재질(`SpriteFlashWhite.mat`, GUI/Text Shader)도 같은 셰이더의 "흰색 덮기" 값으로 바꿔야 한다[파일 확인 + 추측].

### 2.2 도트 선명도 (D4)

- [커뮤니티] Point 필터와 mip 끔이 기본이다. 카메라가 움직일 때 픽셀이 흔들리는 문제는 정사영에서만 완전히 없앨 수 있다.
  - https://www.gamedeveloper.com/design/working-with-pixel-art-sprites-in-unity-the-camera
- [추측] 원근이면 화각을 좁게(15~25°) 하고 각도를 고정한다.
- [추측] 안티에일리어싱:
  - 알파 클립 가장자리에는 MSAA가 효과가 없다.
  - FXAA·SMAA는 픽셀을 뭉갠다.
  - 그래서 끈다.
- [커뮤니티, 인터뷰 요약] HD-2D 제작사는 평평한 스프라이트를 3D 배경에 녹이는 일을 가장 큰 과제로 꼽았다. 카메라가 얼마나 돌아가면 그림이 무너지는지 한계를 찾았고, 큰 화면에서 허전하지 않게 타일 변형과 색 밀도를 늘렸다.
  - https://www.unrealengine.com/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style (원문은 403으로 못 열어 검색 요약)
  - https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/

### 2.3 화면 효과 (D5·D6)

- [공식 문서] DoF Gaussian은 "빠르지만 제한적"이다. 먼 쪽만 흐리고, Max Radius가 1을 넘으면 깨진다. Bokeh는 가까운 쪽과 먼 쪽을 모두 흐리지만 비싸다.
  - https://docs.unity3d.com/6000.4/Documentation/Manual/urp/depth-of-field-volume-override-reference.html
- [공식 이슈] GLES3에서 Bokeh를 쓰면 회색 화면이 되는 버그가 있었다(구버전, Unity 6 재현 여부 미확인).
- [소스 확인] **URP 17.4의 DoF는 정사영에서 맞지 않는다.** Gaussian(`GaussianDepthOfField.shader` 70행)과 Bokeh(`BokehDepthOfField.shader` 38행)가 원근용 깊이 변환(`LinearEyeDepth`)만 쓰고 정사영 분기가 없다.
- **화면 위아래 흐림 만드는 법**:
  - Full Screen Pass Renderer Feature를 쓴다. Fetch Color Buffer를 켜서 `_BlitTexture`로 받는다.
  - Fullscreen Shader Graph에서 화면 Y로 흐림 세기를 정하고, 세로·가로 블러를 2패스로 건다.
  - 비용은 블러 2패스 정도다[추측].
  - https://docs.unity3d.com/6000.4/Documentation/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html
- [공식 문서] Bloom 모바일 설정: Filter Dual(모바일용 빠른 방식) 또는 Kawase, Downscale Quarter, Max Iterations를 낮춤, High Quality Filtering 끔.
  - https://docs.unity3d.com/6000.4/Documentation/Manual/urp/post-processing-bloom.html
- [추측] 톤매핑, 색 보정, 비녜트는 한 패스(Uber)에 합쳐져서 추가 비용이 작다.

### 2.4 그림자 (D7)

- [추측, 일반 관행] 모바일 시작값: 해 1개, 단계 1, 1024~2048, 거리는 보이는 범위까지, 소프트 끔.
- [공식 문서 6000.6] 데칼은 모바일(타일 GPU)에서 Screen Space 방식을 권장한다. Rendering Layers를 쓰면 프리패스가 늘어 비효율적이다.
  - https://docs.unity3d.com/Manual/urp/renderer-feature-decal-reference.html
- [추측] 400마리 그림자는 데칼 400개보다 같은 아틀라스의 눕힌 그림자 판이 훨씬 싸다.

### 2.5 많이 그리기 (D8)

- [Unity 포럼·이슈] SpriteRenderer의 SRP Batcher 지원은 2023.1부터다. Dynamic Batch로 떨어지던 회귀는 6000.0.38f1에서 고쳤다. 커스텀 재질과 MaterialPropertyBlock이 SRP 배칭을 깬다는 보고가 있다[커뮤니티].
  - https://discussions.unity.com/t/srp-batcher-spriterenderer/918871
- [추측] SRP Batcher는 set-pass를 줄이고 그리기 수는 줄이지 않는다. 그래서 대략 그리기 = 색 패스 400 + 그림자 N + (필요하면) 깊이 패스다.
- [공식 문서] GPU Resident Drawer는 MeshRenderer 전용이고, MaterialPropertyBlock 없이 Forward+에서만 되며 OpenGL ES에서는 안 된다. 우리에게는 해당이 없다.
  - https://docs.unity3d.com/6000.4/Documentation/Manual/urp/gpu-resident-drawer.html
- 넘치면 몬스터만 `Graphics.RenderMeshInstanced`로 그린다. 쿼드 하나에 인스턴스별 아틀라스 UV·프레임을 준다[추측].
- 예전 049 교훈: 인스턴싱으로 그리기 수는 크게 줄었지만 렌더 시간은 오히려 늘었다. **잰 뒤에 정한다.**

### 2.6 땅과 소품의 도트 질감 (D9·D10)

- [추측] 바닥 질감은 1유닛 = 48텍셀(PPU 48)이다. 타일은 48×48 또는 96×96, Point 필터. mip을 끄면 먼 곳에 물결무늬가 생길 수 있는데, 정사영이면 거리가 같아 문제가 작다.
- [추측] 로우폴리를 도트처럼 보이게 하는 법: 작은 질감, Point 샘플링, Posterize 노드나 팔레트 LUT.
- 지금 풀·흙길 그림은 48×24 마름모 타일이다[파일 확인]. 3D 바닥에는 **정사각으로 이어지는 질감**이 필요하다. 마름모 4장을 정사각 이음으로 다시 그리거나 새로 만든다.
- 킷 도구 `tileprev.py`는 가로 이음만 본다. 정사각 질감의 가로·세로 이음을 보려면 90° 돌려 한 번 더 돌리거나 기능을 더해야 한다(7절)[킷 확인].

### 2.7 가려짐 (D12)

- [추측]
  - 디더 투명(화면 Bayer 무늬로 알파 클립, Opaque 유지)이 가장 싸다.
  - 스텐실 실루엣은 대상마다 그리기 1회가 더 들어서 대원 4명만 쓴다.

### 2.8 렌더 기능 (D13)

- [공식 문서] Unity 6 URP는 Render Graph가 기본이다. Compatibility Mode는 더 개발하지 않고 없앨 예정이다. 직접 만드는 기능은 `RecordRenderGraph`로 쓴다.
  - https://docs.unity3d.com/Manual/urp/upgrade-guide-unity-6-1.html

## 3. 이 프로젝트의 사실 [파일 확인]

### 3.1 지금 렌더 설정

| 항목 | PC 설정 | 모바일 설정 | HD-2D에서 |
|---|---|---|---|
| 품질 단계 | PC(1, 지금 선택), Standalone 기본 | Mobile(0), Android·iOS 기본 | 측정은 모바일 기준 |
| 렌더러 목록 | [0] PC_Renderer(Forward+, SSAO), [1] Game2DRenderer | [0] Mobile_Renderer(Forward, 기능 없음), [1] Game2DRenderer | 카메라를 0번으로(`MainSceneBuilder.cs:22,116`) |
| HDR / MSAA | 켬 / 끔 | 켬 / 끔 | 그대로 |
| Render Scale | 1.0 | **0.8** | **1.0으로**(D4) |
| 해 그림자 | 2048, 거리 50, 단계 4, 소프트 High | 1024, 단계 1, 소프트 끔 | 모바일 값에 가깝게(D7) |
| 깊이·불투명 질감 | 켬 | 끔 | 화면 흐림에 필요하면 켬 |
| **SRP Batcher** | **꺼짐** | **꺼짐** | **켬**(D8) |
| 볼륨 프로필 | `DefaultVolumeProfile`(중립), `SampleSceneProfile`, 예전 `Look045` | 같음 | 새 프로필(D5·D6) |

- 지금 게임 카메라는 2D 렌더러(1번)에 화면 효과 끔(`m_RenderPostProcessing: 0`)이다.
- 빛(Light2D)은 게임 어디에도 없다. 아트 기준서 K10에는 Light2D라고 적혀 있지만 실제로는 쓰지 않는다.

### 3.2 지금 도트 그림

- 재질: 대부분 `Sprite-Unlit-Default`다. 맞았을 때 번쩍임은 `SpriteFlashWhite.mat`(GUI/Text Shader)이고, 장식은 타일맵 재질을 쓴다(`FieldView.cs:32`).
- 임포트(프리셋 `Art/Presets/AIGeneratedSprite.preset`): PPU 48, 피벗 발 가운데, Point, mip 없음, Clamp, 압축 없음. **3D에서도 그대로 쓸 수 있다.**
- 아틀라스: `Characters.spriteatlasv2`(캐릭터·몬스터·효과·아이템·UI·그림자), `Ground.spriteatlasv2`(바닥·장식). 둘 다 Point, 여백 2.
- 그림:

| 종류 | 내용 | 크기 |
|---|---|---|
| 캐릭터 | 4명, 앞·뒤·걷기·공격·쓰러짐, 114장 | 64×64 |
| 몬스터 | — | 64×64 |
| 바닥 | 풀 4장, 흙길 9장 | 48×24 마름모 |
| 장식 | 나무 75×81, 울타리 24×30, 돌 2종, 꽃 3, 풀 2 | 그림마다 다름 |
| 그림자 | `shadow.png` | 24×12 |

### 3.3 축척

| 값 | 지금 2D 게임 | 예전 3D(046) |
|---|---|---|
| 캐릭터 키 | 48px = 1.0유닛(그림 칸 64px = 1.33유닛) | 영웅 0.8m, 몬스터 0.52m |
| 전투장 | 마름모, 대각선 64칸 = 64유닛, 변 45.3m, 약 2,048㎡(1유닛 = 1m 가정)[계산] | 82.0×80.6m, 약 6,037㎡ |
| 화면 세로에 보이는 바닥 | 7.5유닛 | 12m(정사영 6) |
| 카메라 | 정사영 3.75, 2D(세로 0.5 = 30° 상당) | 정사영 6, 55° 내려다봄 |
| 몬스터 생성 거리 | 보이는 범위 + 70 = 약 958 바닥 단위 ≈ 10.8유닛. 화면 크기와 화면비에 따라 바뀜(`WorldPresenter.cs:234` → `MonsterSpawnService.cs:147`) | — |
| 원정대 이동 속도 | 140 바닥/초 = 1.58유닛/초 | — |

- 1 바닥 단위 = 0.0113유닛이다(`run_rules.json`의 `unitScale`).

### 3.4 예전 3D 자산 (`Assets/DeadSurvivor`, 고치지 않음)

| 무엇 | 내용 | 쓰는 법 |
|---|---|---|
| 배치 `Layout046.json` | 78개: 바위 24, 덤불 12, 돌 고리 10, 돌 줄 9, 키아트 고리 5, 폐허 5, 숲 4, 기둥 3, 연못 2, 호 2, 호수 1, 제단 1. 길 3갈래 | 종류와 비율 참고 |
| 소품 `Props046.json` | 613개: 덤불 445(경계 밖 391), 나무 135(밖 124), 바위 33. 길은 곡선(폭 1.6m), 흙 타원 135 | 숲 경계 방식 참고 |
| 배치 도구 `Tools/Map/layout_046.py` | 입력: `ROADS`(187행), `REGION_KINDS`(193행), `SPACING`(201행, 74~88px), 시작점, 간격, 길 폭, 폐허 중심. 1 배치 px = 0.08m | **새 경계(마름모 45m)로 다시 뽑는다** |
| 맵 짓기 `Tools/Map/build_map_046.cs` | 바닥은 흔든 삼각 격자(칸 2.4m)이고 Terrain은 없음. 길은 흙 바닥 + 조약돌 프리즘. 물은 부채꼴 메시. 폐허는 상자. 240px마다 메시를 합침. 삼각형 약 63만(나무 18.5만, 가장자리 12.2만, 돌 9.2만, 장식 8.2만) | 바닥 메시 만드는 방식 참고. **삼각형이 휴대폰에 많음** → 줄임 |
| 모델 | 나무 `tree045.fbx`(1,368삼각, 2.4m), 바위 `boulder045.fbx`(576, 0.69m), 돌 `stone045.fbx`(159, 0.25m, 시험) | 모양 재사용(D10) |
| 셰이더 | `DSLowPolyPalette.shader`(URP SimpleLit 기반): 면 UV가 64×32 팔레트 그림(8×4칸)을 가리킴. 높이 그라데이션, 위 투영, 바람, **디더 페이드**, 구름 그림자. 물은 `Water045.shader` | 팔레트 교체로 색 맞추기. 디더 페이드는 D12에 참고 |
| 화면 효과 | `Look045.asset`: ACES, Bloom 0.3(문턱 0.95), 화이트밸런스 +6, 노출 0.08, 대비 8, 채도 12 | D6 참고값 |

### 3.5 패키지

- 있음: URP 17.4.0, Shader Graph 17.4.0, Input System 1.19, VContainer, UniTask, R3, AI Navigation, Timeline. 로컬 `com.studiokit.kit3d`(`file:../3dkit/unity`, 예전 3D 도구, 에디터 전용).
- 없음: ProBuilder, Cinemachine, Splines, 2D 패키지. 바닥 높낮이를 손으로 다듬으려면 ProBuilder 설치를 검토한다(D9).

## 4. 카메라 계산

### 4.1 정사영 3D (추천 후보)

- 바닥에서 보이는 깊이 = 정사영 세로 반값 ÷ sin(내려다보는 각)[계산].

| 각도 | 화면 세로 반값 3.75일 때 보이는 바닥 깊이(앞뒤 각각) | 지금과 비교 |
|---|---|---|
| 30° | 7.5 | 같다 |
| 35° | 6.5 | 87% |
| 40° | 5.8 | 78% |
| 45° | 5.3 | 71% |

- 캐릭터를 카메라 쪽으로 기울여 세우면 화면 속 키는 각도와 상관없이 같다(13.3%).
- 지금 보이는 범위 계산(`UpdateViewExtent`)은 세로 0.5를 sin(각도)로 바꾸면 그대로 쓸 수 있다. **30°면 지금 숫자와 같다.**

### 4.2 원근 (비교 후보)

- 계산 조건: 캐릭터가 화면 가운데에서 지금과 같은 크기(화면 높이의 13.3%), 화면비 16:9. 단위는 유닛. "가까운/먼"은 화면 아래/위 가장자리가 바닥에 닿는 곳(바라보는 점 기준)이다. "크기 비"는 화면 위 도트 크기 ÷ 화면 아래 도트 크기다.

| 각도 | 화각 | 카메라 거리 | 가까운 쪽 | 먼 쪽 | 크기 비 |
|---|---|---|---|---|---|
| 30° | 15° | 28.5 | −6.1 | +9.7 | 0.63 |
| 30° | 20° | 21.3 | −5.7 | +10.8 | 0.53 |
| 30° | 30° | 14.0 | −5.1 | +14.0 | 0.37 |
| 40° | 15° | 28.5 | −5.0 | +6.9 | 0.73 |
| 40° | 20° | 21.3 | −4.8 | +7.4 | 0.65 |
| 40° | 30° | 14.0 | −4.4 | +8.6 | 0.52 |

- 지금은 앞뒤가 ±7.5로 같고 크기 비는 1.0이다.
- 원근이면 **아래(가까운 쪽)가 좁아진다** → 아래에서 오는 몬스터를 늦게 본다. 바라보는 점을 아래로 옮겨 맞춰야 한다.
- 보이는 바닥이 사다리꼴이라 몬스터 생성 거리 계산을 새로 짜야 한다(순수 함수 + 시험).
- 좁은 화각 추가 계산(40°): 화각 8°는 크기 차 1.18배(카메라 거리 53.6), 12°는 1.29배(35.7), 15°는 1.37배(28.5)다. 레퍼런스의 약 1.35배는 40°·화각 12~15° 근처다[계산].
- 계산 스크립트(레포 밖, 다시 만들 수 있음):
  - 카메라 거리 D = 1 ÷ (2 × k × tan(화각/2)), k = 인물 화면 키 비율 = 1/7.5(=0.1333…). 0.133으로 반올림하면 화각 8°에서 53.76이 나와 표(53.6)와 어긋난다(11-18 킷 `view.py` selftest에서 확인)
  - 크기 비는 표에서 "위 ÷ 아래"(예 0.78), 문장의 "1.29배"는 그 역수 "아래 ÷ 위"다. 바닥에 누운 도트의 위아래 비는 크기 비의 제곱이다(40°·12°에서 0.60)
  - 높이 h = D·sin(각도), 뒤로 물린 거리 = D·cos(각도)
  - 화면 아래·위 광선의 각 = 각도 ± 화각/2
  - 바닥까지 거리 = h ÷ tan(광선 각), 광선 길이 = h ÷ sin(광선 각)
  - 크기 비 = 아래 광선 길이 ÷ 위 광선 길이

## 5. 레퍼런스 관찰

### 5.1 수집 개요

- 레퍼런스: `hd2d-study-20261117`. 모드는 **study**로, 기법 값만 가져오고 캐릭터·에셋·UI·이름·구도는 가져오지 않는다.
- 메인 레퍼런스는 그대로 우리 컨셉(PE-G001~G004, follow)이다.
- 공식 Steam 상점 스크린샷 10장(1920×1080)을 모았다. 실패는 0장이다.

| 게임 | 장 | 장면 |
|---|---|---|
| TRIANGLE STRATEGY | 4 | 전투 |
| OCTOPATH TRAVELER | 2 | — |
| OCTOPATH TRAVELER II | 1 | — |
| LIVE A LIVE | 2 | — |
| DRAGON QUEST III HD-2D Remake | 1 | 낮은 각도 들판뿐 |

- 기록 위치:
  - [manifest](../art/reference/hd2d-study-20261117/manifest.json): 출처 URL, SHA-256
  - [관찰 문서](../art/reference/hd2d-study-20261117/hd2d-관찰.md): 1~7절, "기준서로 넘길 값" 표, "가져오지 않는 것"
  - 원본 그림은 `output/reference/hd2d-study-20261117/`(Git 제외)에 있다. **생성 입력이나 게임 에셋으로 쓰지 않는다.**
- 디렉터가 HD-R004(전투, 마름모 광장 내려다봄)를 직접 열어 확인했다:
  - 대각선으로 높이 내려다보는 각도이고 바닥 격자가 45° 돌아 있다. 우리 아이소 구도와 같다.
  - 화면 위아래가 강하게 흐리고 가운데 띠만 선명하다.
  - 가장자리 어둡게가 짙다.
  - 인물이 화면 높이의 약 8%다(추정).
  - 3D 담장이 멀수록 약간 좁아진다. 원근은 약하게 있다.

### 5.2 값 (관찰 문서 "기준서로 넘길 값"에서 옮김)

| 항목 | 레퍼런스 | 우리 출발값 후보 |
|---|---|---|
| 카메라 각도 | 전투 33~50°, 탐험 25~35°, 들판 20~30°(추정) | **약 40°, 하늘 안 보임** |
| 원근 세기 | 화면 안 앞뒤 크기 차 약 1.35배(추정). 4.2절 계산으로는 40°·화각 12~15°에 해당 | 1.2배 이하(화각 약 8°) 또는 정사영 |
| 선명한 띠 | 전투 화면 높이의 20~50%, 탐험 30~70%(측정). 주인공은 10장 모두 띠 안 | **60~70%로 넓게**, 가운데보다 약간 아래. 적이 사방에서 오기 때문 |
| 흐림 | 아래(가까운 쪽)가 더 강함 | 위·아래 각 15%만, 약하게 |
| 가장자리 어둡게 | 전투 0.05~0.19, 낮 0.83~1.09(모서리 밝기 ÷ 가운데 밝기, 측정) | **0.6~0.8** |
| 빛 번짐 | 아주 밝은 픽셀은 화면의 0.01~4.2%(측정) | 불빛·마법에만. 문턱을 높게 |
| 인물 그림자 | 빛 방향으로 늘어진 인물 모양 그림자, 키의 1~1.5배(추정). 빛은 왼쪽 위 | 대원만 모양 그림자(D7) |
| 그림자 색 | 검정이 아닌 진한 적갈색 #170403~#1e0805(측정) | 컨셉 색 안에서 따뜻한 짙은 색 |
| 인물 화면 키 | 화면 높이의 8~14%, 대개 10%(추정). 도트 한 칸 = 화면 3~5px, 또렷함 | 지금은 13.3%. 수백 마리를 담으려면 10%도 후보(D15) |
| 배경 채도 | 인물보다 낮음(낮 들판 28~32%, 측정) | 바닥·소품 채도를 인물보다 낮게 |
| 점광 | 등불 빛이 인물 그림에도 비침 | 밤(2단) 등불에 그대로 |

### 5.3 레퍼런스로 확인하지 못한 것

- 정지 그림이라 움직임(흐림·입자 변화, 카메라 이동)은 못 봤다.
- 건물 뒤로 들어간 인물을 비춰 보이게 하는 장면이 없었다.

## 6. 1단계 시안 작업 지시 초안

다시 시작할 때 이 절을 Codex 실행기 지시문의 바탕으로 쓴다. 본 게임과 빌드는 건드리지 않는다.

1. **준비**
   - 에디터 상태를 확인한다(`python3 -B ./tools/editor_ready.py`). 사람이 플레이 중이면 기다린다.
   - 새 장면 `Assets/Game/01.Scene/Lookdev_HD2D.unity`를 만든다. 빌드 목록에는 넣지 않는다.
   - 새 자산은 `Assets/Game/03.Resources/Art/HD2D/`(재질·셰이더·볼륨), 장면을 짓는 에디터 코드는 `Assets/Game/02.Scripts/Editor/`에 둔다. 다시 쓸 부분(카메라 장치, 도트 재질, 흐림 셰이더)은 게임 이름 없이 만든다(계획 9단계).
2. **렌더**
   - 카메라는 3D 렌더러(0번)를 쓰고 화면 효과를 켠다.
   - 모바일 설정 Render Scale 1.0과 SRP Batcher 켬은 **시안 장면에서 측정할 때만** 바꾸고 끝나면 되돌린다. 본 게임 설정을 바꾸는 일은 2단계 결정 뒤에 한다.
3. **도트 재질**: D3 방식의 Lit Shader Graph를 만든다. 대원 4명·몬스터 5종(각 몇 마리)·효과 1~2개에 붙이고, 카메라 각도만큼 기울여 세운다.
4. **바닥**:
   - 평평한 메시에 풀 질감(48텍셀/유닛)을 깐다. 지금 풀 타일로 정사각 이음 질감을 임시로 만든다.
   - 흙길 한 줄을 깐다.
   - 예전 046 구역(큰길·돌담·연못)을 복사해 축척을 맞춘다. 캐릭터 1m 기준이면 예전 모델 ×1.25다.
5. **소품**: 예전 바위·나무·돌담을 복사한다. ① 팔레트만 우리 색으로, ② 월드 투영 도트 질감. 두 가지를 같은 장면에 반씩 놓는다.
6. **빛**: 해 1개(오후, 따뜻한 색), 하늘빛, 하드 그림자. 대원과 소품은 실제 그림자, 몬스터는 눕힌 그림자 판.
7. **화면 효과**: 위아래 흐림(D5), Bloom·Tonemapping·Vignette(D6).
8. **카메라 다섯 판**:

| 판 | 카메라 | 소품 |
|---|---|---|
| A | 정사영 30° | 효과 없음, 예전 맵 그대로(로우폴리 색) |
| B | 정사영 30° | HD-2D 전부 |
| C | 정사영 40° | HD-2D 전부 |
| D | 원근 40°, 화각 12° | HD-2D 전부, 위아래 흐림. 앞뒤 크기 차 약 1.3배로 레퍼런스와 비슷함[계산] |
| E | 원근 40°, 화각 8° | HD-2D 전부. 크기 차 약 1.2배[계산] |
| F | B 또는 C 중 나은 것 | 인물 화면 키 10%(D15) |

- 흐림·가장자리 어둡게·빛 번짐 시작값은 5.2절이다: 선명한 띠 60~70%, 위·아래 15%, 가장자리 0.6~0.8.

9. **캡처**: 판마다 16:9 한 장, 카메라를 옆으로 움직이는 연속 캡처 8장(떨림 확인), 21:9와 4:3 한 장씩.
10. **측정**: 몬스터 300마리 판을 모바일 설정으로 20초 동안 잰다(049 방식): 그리기 호출, set-pass, 렌더·프레임 시간, 삼각형, 그림자 그리기 수. 몬스터 실제 그림자 켬·끔 두 번.
11. **미리보기**: 로컬 HTML 한 장에 컨셉 PE-G001 | 지금 게임 | A~E | 성능 표를 담는다. 디렉터가 캡처를 모두 직접 연 뒤 사용자 브라우저로 연다.
12. **다 됐다고 볼 기준**:
    - 캡처를 디렉터가 직접 봤다.
    - 도트가 흐려지거나 떨리지 않는다.
    - 좌우 반전·색·그림자가 함께 된다.
    - 대원·몬스터가 바닥과 색으로 갈린다(K8).
    - 300마리 판의 숫자가 표에 있다.

## 7. studio-kit 2.5D 팩 설계 초안 [킷 확인]

### 7.1 이름

- 후보 `25d-art`와 `hd2d-art`. 둘 다 킷 검사를 통과한다(이름 규칙에 정규식이 없고, `2d-art-setup`처럼 숫자로 시작해도 된다).
- `25d-art`는 `2d-art`와 헷갈리기 쉽다는 의견이 있다. **9단계 시작 때 정한다.** 사용자에게 보이는 이름이 아니라서 묻지 않고 정해도 된다.

### 7.2 담을 파일 (기존 팩을 따라 만든다)

| 파일 | 내용 | 따라 할 것 |
|---|---|---|
| `skills/<팩>-setup/SKILL.md` | 값: `ART_DOCS_DIR`, `CLIENT_DIR`, `ART25D_PYTHON`, `UNITY_VERSION`. 미리 확인: art·2d-art 팩 설치, URP 6000.x | 2d-art-setup(105줄) |
| `template/root/.ai/rules/` | 카메라(방식·각도·보이는 범위), 도트 조명(재질·그림자), 화면 효과, 가려짐, 성능 | 3d-art 규칙 6개 |
| `template/root/.ai/<팩>/config.json` | 도구 설정 | 2d-art `config.json` |
| `template/inject.fragment.tsv`, `gitignore.fragment`, `capabilities.fragment.yaml` | 규칙 주입, 산출물 무시, 능력: `<팩>.scene`(카메라·빛·효과·가려짐), `<팩>.convert`(2D→2.5D) | 3d-art 조각 |
| `payload/root/.ai/<팩>/tools/` | `view.py`(각도·화각 → 보이는 범위, 화면 속 키, 크기 비. 4절 계산), `texel.py`(질감 도트 크기 검사), 정사각 이음 검사, 화면 효과 프리셋 JSON. 모두 `selftest` | 2d-art 도구 |
| `payload/root/Packages/com.studiokit.kit25d/` | **Runtime**: 카메라 장치, 기울인 도트 판, 가려짐 비침. 셰이더: 도트 Lit(D3), 위아래 흐림(D5). **Editor**: 장면·프리셋 적용. Tests(보이는 범위 계산). 프로젝트 안에 복사해 넣고 manifest는 고치지 않음 | kit3d. **kit3d에는 Runtime·Tests가 없어서 처음 만드는 구조다** |
| `scripts/install_test.py` | art + 2d-art + 2.5D 함께 설치 시험 | 2d-art(302줄) |
| `references/교훈-*.md` | 이번 작업의 문제·해결 | 3d-art |
| `skills/<팩>-convert/SKILL.md` | 2D 게임을 2.5D로 바꾸는 플레이북(계획 3·4단계 순서와 점검 목록) | 2d-art-produce 구조 |
| `skills/<팩>-scene/SKILL.md` | 시안·장면 만들기 플레이북(계획 1·5·6단계) | 3d-art-produce 흐름 |

### 7.3 고칠 기존 킷 파일

- `art-spec/templates/아트-기준서.md`: 3.2(카메라)와 3.6(예산)에 2.5D 값을 넣고, 기계가 읽는 `25d-view` 블록(`2d-measure`와 같은 모양)을 더한다.
- `game-direction/templates/direction.md:15`, `game-spec`의 화면 예시: "2.5D(도트 캐릭터 + 3D 배경)" 보기를 더한다.
- `director-game/SKILL.md:104`: 지금은 "2D면 2d-art-setup, 3D면 3d-art-setup" 한 문장이 전부다. 여기에 "2.5D면 2d-art-setup + <팩>-setup(+ 소품이 3D면 3d-art-setup)"을 더한다. **이게 다음 게임에서 자동으로 고르게 하는 연결점이다.**
- `scripts/validate.py`: 값 목록(320~325행, 507~509행), 새 설치 시험 함수와 `main()` 등록.
- README(스킬 표, 구성, 릴리스 절, 로드맵), `plugin.json` 0.21.0 → 0.22.0, `docs/next-validation.md`의 "아직 확인되지 않은 것"에 2.5D 항목.

### 7.4 개발 방법

- 킷의 스킬은 `~/.claude/skills`에 스킬마다 링크돼 있고, 링크는 킷 저장소 본 작업 폴더(main)를 가리킨다.
- **별도 git worktree에서 작업하면 다른 프로젝트에 영향이 없다.**
- **본 작업 폴더에서 갈래를 바꾸면 모든 프로젝트에 바로 영향이 간다.**
- **worktree에서 `install.py`를 돌리지 않는다.** 모든 링크가 그쪽으로 옮겨지고, worktree에 없는 스킬의 링크가 지워진다. 시험은 `install.py --dest <임시 폴더>`나 worktree 경로를 직접 가리켜서 한다.
- 이 게임에는 지금 예전 3D 도구가 로컬 패키지로 연결돼 있다(`file:../3dkit/unity`). 2.5D 패키지를 깔 때 이름이 겹치지 않는지 본다.

## 8. 아직 모르는 것 (시안이나 실기기에서만 알 수 있음)

| 무엇 | 언제 알 수 있나 |
|---|---|
| 도트 Lit 재질에서 좌우 반전·색·그림자가 함께 되는지(IN-127136) | 1단계 시안 |
| 기울여 세운 판의 그림자 모양이 괜찮은지 | 1단계 시안 |
| 정사영 vs 원근, 어느 쪽이 "제대로 된 느낌"인지 | 1단계 사용자 고르기 |
| 위아래 흐림 띠의 폭과 세기 | 1단계 시안(레퍼런스 5절 값으로 시작) |
| 인물 화면 키 13.3%와 10% 중 무엇이 나은지 | 1단계 사용자 고르기 |
| 기기 해상도마다 도트 폭이 고르지 않은 정도 | 1단계 시안(화면비별 캡처) |
| 예전 소품을 팔레트 교체만 할지, 투영 도트 질감을 입힐지 | 1단계 시안 |
| 300~400마리에서 그리기 수와 프레임(SRP Batcher 켬) | 1단계(에디터 모바일 설정), 7단계 |
| 정사영에서 해 그림자·데칼이 정상인지 | 1단계 시안 |
| 실기기 GPU 시간 | 2단 휴대폰 빌드 |

## 9. 출처

- Unity 6000.4 문서:
  - DoF: https://docs.unity3d.com/6000.4/Documentation/Manual/urp/depth-of-field-volume-override-reference.html
  - Bloom: https://docs.unity3d.com/6000.4/Documentation/Manual/urp/post-processing-bloom.html
  - Full Screen Pass: https://docs.unity3d.com/6000.4/Documentation/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html
  - GPU Resident Drawer: https://docs.unity3d.com/6000.4/Documentation/Manual/urp/gpu-resident-drawer.html
- Unity 문서(다른 판):
  - 데칼: https://docs.unity3d.com/Manual/urp/renderer-feature-decal-reference.html
  - Unity 6.1 업그레이드 안내: https://docs.unity3d.com/Manual/urp/upgrade-guide-unity-6-1.html
  - Compatibility Mode: https://docs.unity3d.com/6000.2/Documentation/Manual/urp/compatibility-mode.html
- Unity 포럼·이슈:
  - https://discussions.unity.com/t/srp-batcher-spriterenderer/918871
  - https://issuetracker.unity3d.com/issues/sprite-renderer-does-not-issue-draw-calls-correctly-when-using-srp-batcher
  - https://discussions.unity.com/t/sprite-vertexcolor-isnt-accessible-anymore/1530694
  - https://issuetracker.unity3d.com/issues/android-urp-gray-screen-on-android-with-opengles-3-when-depth-of-field-mode-is-set-to-bokeh
  - https://discussions.unity.com/t/depth-of-field-scatter-blurs-everything/521780
- 기타:
  - https://www.gamedeveloper.com/design/working-with-pixel-art-sprites-in-unity-the-camera
  - https://en.wikipedia.org/wiki/HD-2D
  - https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/
- 패키지 소스: `Library/PackageCache/com.unity.render-pipelines.universal@f76d6966885c`(URP 17.4.0). `Sprite-Lit-Default` 패스, `Core2D.hlsl`, `UnityInput.hlsl`, `GaussianDepthOfField.shader:70`, `BokehDepthOfField.shader:38`, `SSAO.hlsl`.

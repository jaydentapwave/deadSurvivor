# 높은 턱 그림자 수신 — 구현 전 설계 검토

상태: **L2, design-reviewer DR-RB-01 수정 확인 대기. 이 설계의 런타임 구현은 아직 하지 않았다.**

기준 커밋 `8b6d9dae37ac0c2079a8c8174046845c1b8ffaea`. 앞 spec 수정 확인 `VERDICT: FAIL` (job `20261123-074748-37c915`), L7-03 partial / L7-08 met. 해당 필수 지적은 모두 수용했고 반박한 지적0이다. 이 문서는 renderer pass 추가가 필요한 새 변경 범위에 대한 검토이며, 앞 설계 PASS를 이번 변경의 승인으로 사용하지 않는다.

## 문제와 실제 증거

`output/hd2d-dq/apply/7/recheck3/raised-bank/matrix.json`: 기존 Main bank 메시의 위쪽 삼각형에서 XZ (18,15)→(19,15) 5점을 각 종(Bruno/5몬스터)에 적용했다. 게임 grid 높이 대신 실제 bank 삼각형 y를 사용했고 mesh/재질/shader를 바꾸지 않았다. 루트는 약 .338~.339m, grid 대비 받침은 약 .36m다. 30프레임 모두 그림자 수신0이다. 후보 world 오차는 약 .117m 이상으로 `.04m`를 넘는다. 개별 JSON·프레임·ID·source hash는 같은 폴더에 있다. 전체 게임 이동 규칙이나 정상 입력 경로를 증명하는 fixture가 아니다.

원인은 기존 은폐 계약과 새 받침면 수신 계약의 접점이다.

- `MainSceneDQBackgroundMigration.cs:68~70`: 장식 턱을 통과하는 기존 reference-depth 계약을 보존하여 Bank `_FlatDepth=1`.
- `DQMainSurface.shader:74,119,173`: 실제 bank 표면에 색을 그리지만 depth는 href(grid) 위치의 clip depth다. 그 값과 실제 색 픽셀의 UV로 world를 복원하면 bank 물리 위치와 어긋난다.
- `DQ_BlobShadow.shader:40~54`: 현재 SceneDepth 하나를 실제 receiver로 복원하고 `.04m`로 자른다. 따라서 실제 윗면에 fixture를 올리면 정상 수신까지 거부된다.

기준 문서 `light-design-brief.md` DR-01의 알려진 높은 턱 수신 확인을 아직 충족하지 못한다. 수신0을 PASS로 승격하지 않는다. 앞 경계의 bank 여섯 칸도 **미확인: 경계 표본 확보 / 높은 받침 수신 미검증**으로 구분한다.

## 보존할 계약

- 게임의 평면 바닥 좌표·이동·스폰·충돌·세이브·playable 기대식 변경0. 새 턱 오르기 규칙0.
- Bank 기존 메시·둥근 윤곽·배치·`_FlatDepth=1`·기존 카메라 깊이/대원 통과 은폐 계약을 유지한다.
- `.04m` 문턱, alpha 연속성, 지원 mask, queue2990→물3000, 공유 재질/texture, actor별 reusable mesh를 유지한다.
- 기준서·기존 승인 설계서는 고치지 않는다. 새 설계와 검증 기록만 별도 문서로 둔다.

## 제안: Main bank 물리 깊이만 그림자에 별도 공급

기존 카메라 depth와 actor 은폐는 유지한다. **최종 opaque 색을 쓴 표면이 bank인 픽셀만** bank 물리 깊이를 별도로 공급한다. 물리 깊이의 대소로 화면 가시성을 추론하지 않는다. 렌더 파이프라인에 pass 하나를 추가하므로 L2로 분류하여 구현 전에 검토받는다.

### 데이터 흐름과 소유권

1. `FieldView.BuildLandscape`가 이미 만드는 bank MeshRenderer 목록을 소유하고 readonly로 노출한다. 전역 registry·프레임별 FindObjects·새 게임 데이터는 만들지 않는다. FieldView 재생성/파괴 시 목록도 갱신/해제한다.
2. 기존 `WorldPresenter`의 배경 생성 직후 연결에서 Main 카메라의 `DQSceneLighting`에 그 목록을 한 번 전달한다. 생성 순서가 바뀌어 빈 목록이면 입력 미준비로 명시하고 오래된 renderer를 재사용하지 않는다. DI 생성자/등록/상태 소유권은 바꾸지 않는다.
3. 화면 소유권은 실제 Main opaque **ForwardLit 색 패스와 같은 fragment의 stencil 기록**으로 정한다. 프로젝트에서 쓰지 않는 user bit `0x02`를 전용으로 사용한다. `DQMainSurface.ForwardLit`는 기존 alpha/dither/depth/ZTest를 그대로 거친 뒤 Bank material만 Ref=2, 다른 surface는 Ref=0으로 `WriteMask=2 / Comp Always / Pass Replace / Fail Keep / ZFail Keep`한다. `HD2D_SpriteLit.ForwardLit`는 Ref=0으로 같은 bit만 쓴다. DepthOnly/ShadowCaster에는 소유권 기록을 넣지 않는다. 따라서 색을 실제로 덮은 non-bank opaque가 이 bit를 지우며 같은 깊이의 LEqual 겹침도 실제 색의 최종 쓰기 순서와 일치한다. 기존 그림자 user bit `0x01`·URP 예약 bit는 읽거나 쓰지 않는다.
4. 소유권 입력의 지원 범위를 고정한다. 앞 Main 활성 재질 증거 `recheck1/bloom/materials.json`의 opaque/cutout은 `DQ/Main surface`와 `HD2D_SpriteLit` 두 shader다(물·그림자·표시·먼지는 transparent). 이행/시작 검사에서 Main opaque/cutout의 shader·ForwardLit stencil 계약·카메라 depth/stencil attachment·renderer stencil override OFF를 확인한다. 기존 FieldView 및 member/monster/enemy-shot 생성 경계도 같은 지원 계약을 확인한다. 미지원 opaque shader, 사용자 override, stencil 없는 attachment, 지원 범위 밖 새 opaque가 있으면 **물리 bank 입력을 invalid로 하고 실패를 남긴다**. 지원하지 않는 opaque가 소유 bit를 지울 것이라고 가정하지 않는다. Main 활성 renderer/재질 목록을 캡처 증거에 고정하며 이 계약은 임의의 외부 shader에 대한 일반 보증이 아니다. 이행은 전역 stencil 설정이나 다른 프로젝트를 바꾸지 않는다.
5. 새 `DQBankReceiverFeature`는 Main 전용 DQ base camera에서만 실행한다. opaque/cutout이 모두 그려진 뒤, transparent가 시작되기 전에, 실제 bank material의 새 `BankReceiver` pass를 그린다. 새 pass는 기존 `DQMainSurface`의 **같은 Vert/ClipSurface/Depth 계산**을 재사용한다. 기존 reference raw depth를 SV_Depth로 출력하여 기존 active depth/stencil attachment에 `ZTest Equal / ZWrite Off / Stencil Comp Equal Ref=2 ReadMask=2 WriteMask=0`을 적용한다. 별도 color에는 실제 bank raw depth를 출력한다. 이렇게 소유 bit와 reference-depth가 둘 다 맞는 픽셀만 남긴다. `_FlatDepth`를 바꾸거나 physical depth로 hardware ZTest를 하지 않는다. 별도 override material/복제 shader에 reference 식을 다시 쓰지 않는다.
6. RenderGraph는 카메라 실제 해상도에 맞는 **R32_SFloat 물리 raw device-depth color target**을 프레임 범위로 소유한다. 기존 active depth/stencil attachment는 read-only로 연결하고 수정하지 않는다. color만 clear=-1, bank raw는 [0,1]. stencil을 가진 원본 attachment의 sample 수와 color target이 같아야 한다. 현재 Main MSAA1 계약을 검사하고, 불일치는 임의 resolve 없이 invalid/FAIL로 남긴다. depth/stencil 원본을 별도로 clear하거나 기존 bit를 바꾸지 않는다. 외부 RTHandle 영속 소유·Texture2D 생성·프레임별 readback은 없다.
7. 생산 pass builder는 color Write와 active depth/stencil Read를 선언하고 `SetGlobalTextureAfterPass`로 `_DQBankReceiverDepth`를 공개한다. 전역 유효 flag=1과 실제 크기는 **생산 draw 뒤 같은 GPU command stream**에 기록하며 허용된 global-state 변경을 선언한다. C# record 시점에서 flag=1을 먼저 쓰지 않는다. URP 17.4.0 `DrawObjectsPass.cs:239`의 `UseAllGlobalTextures(true)`가 transparent 소비를 선언하므로 생산→transparent 의존성과 texture 수명이 연결된다. 구현 때 RenderGraph viewer/명령 증거로 생산 완료→texture 공개/flag→queue2990 소비→물3000 순서를 확인한다. pass를 cull한 프레임은 flag=1을 남기지 않는다. 기존 SceneDepth copy·blur/world-indicator 순서는 유지한다.
8. 대상 카메라 시작에 GPU flag=0을 먼저 기록하고, 생산 성공 때만 1로 올린다. `DQSceneLighting`의 카메라 end에서 후속 GPU reset을 기록해 0으로 되돌린다. 비대상/overlay/preview camera에서도 시작 flag=0이 생산/소비보다 먼저 실행돼야 한다. disable·Dispose·크기 변경 때 다음 카메라가 stale texture를 읽지 않도록 invalid 상태를 보장한다. 같은 GPU flag에 CPU `Shader.SetGlobal`과 command-buffer 쓰기를 섞지 않는다. bank가 없는 화면은 명시적 빈 입력, target 생성/소유권 지원 실패는 NOT_RUN/FAIL이고 높은 턱 성공으로 쓰지 않는다.

### 수신 선택 — 가시성 먼저, 위치 복원은 나중

- 기존 SceneDepth raw를 읽는다. bank 물리 texture는 **해당 프레임·카메라의 유효 flag가 1인 경우에만**, 같은 픽셀 UV에서 Point/Clamp mip0로 읽는다. Bilinear/Trilinear/MSAA resolve/주변 유효값 보충을 하지 않는다.
- 값이 유한하고 `0 <= raw <= 1`이면 생산 pass가 **원본 색 패스의 bank 소유권 + 기존 reference 깊이 일치**를 확인한 픽셀이다. 그때만 실제 bank raw를 `ComputeWorldSpacePosition`의 입력으로 사용한다. -1 sentinel/NaN/범위 밖/화면 밖은 bank 입력 없음이다. 기존 SceneDepth로 계산하되 높은 받침이 성공했다고 처리하지 않는다.
- **`bankPhysical < sceneOpaque` 비교식으로 가시성을 판정하지 않는다.** 반례 10/11/12에서 opaque=11이 색과 depth를 최종 소유하므로 stencil bit=0이며 bank reference=12도 ZTest Equal에 실패한다. 생산 target은 -1이고 물리10은 소비되지 않는다. bank reference와 opaque 깊이가 같은 경우도 최종 색을 쓴 opaque가 stencil을0으로 덮으므로 bank 입력이 없다.
- 선택한 raw를 지금과 같은 플랫폼/reversed-Z 처리와 `ComputeWorldSpacePosition`으로 복원한다. `.04m`와 bank/support 분류는 그대로다. bank 경사 옆면·아래 바닥·반대 받침은 실제 깊이와 거리/분류로 거부한다. 원래 바닥 actor의 그림자가 bank 윗면에 번지지 않아야 한다. 물3000 순서는 보존한다.
- native color raw는 해당 bank fragment의 실제 clip 위치에서 얻고 SV_Depth는 기존 reference 식에서 얻는다. camera jitter/투영/동적 해상도/UV origin/viewport가 둘 다 원래 카메라와 같아야 한다. 경계에서 -1과 유효값이 섞인 가짜 raw를 만들지 않는다. Point 읽기 후 값 유효성 검사를 먼저 하고 역투영한다.
- 새로운 stencil 기록은 두 공유 shader의 color pass에만 추가되며 shader를 함께 쓰는 다른 장면에서도 user mask0x02를0으로 지울 수 있다. 현재 레포에서 user mask0x02 사용0을 확인했으며 color/depth 변경0·기존 mask0x01 유지·다른 camera 결과 불변을 RB-04에서 확인한다. 비Main Bank 재질은 Ref 기본0이고 Main Bank만 이행 메뉴가 Ref2로 설정한다.

## 예정 파일 (아직 미구현)

- 새 `Effect/HD2DView/View/DQBankReceiverFeature.cs`. `DQMainSurface.shader`의 기존 HLSLINCLUDE를 재사용하는 `BankReceiver` pass와 opaque ForwardLit stencil 기록. 별도 깊이 shader/material은 만들지 않는다.
- `FieldView.cs`의 bank renderer 목록, `WorldPresenter.cs`의 생성 직후 연결, `DQSceneLighting.cs`의 카메라 입력 수명.
- `HD2D_SpriteLit.shader`의 opaque/cutout ForwardLit 소유 bit 지우기와 Main opaque 생성 경계의 지원 여부 검사. 미지원 입력은 invalid로 기록.
- `DQ_BlobShadow.shader`의 소유권 확인된 receiver 선택·Point/sentinel 검사. 기존 SceneDepth 복원/문턱과 재질 공급 경로는 보존.
- `MainSceneDQLightMigration.cs`: Main renderer feature·Bank owner Ref2를 메뉴로 생성/갱신. 지원 shader/pass·stencil attachment/override를 검사한다. 새 자산을 snapshot/rollback 목록에 포함. 두 번 실행한 자산 hash 동일·실패 복구·dirty=false 확인.
- 관련 EditMode 시험 및 기존 `DQLightingVerification`의 범위별 수집 확장. 장면/재질은 이행 메뉴 외 손편집0.

## 수용 기준과 최소 확인

| ID | 조건 | 확인 |
|---|---|---|
| RB-01 | 실제 높은 bank 받침에서 6종×5프레임 수신이 양수이고 world 오차≤.04m, 다른 받침 침범0 | 같은 `StartTransitionMatrix(true)` 실제 bank 삼각형·root 높이·receiver 후보/수신/거부·mesh/material/texture ID, 연속 capture. 입력 누락은 성공 아님 |
| RB-02 | actor가 바닥에 있을 때 턱 위/수직면으로 잘못 번짐0, 높은 받침과 낮은 바닥의 구분 유지 | bank 경계 양쪽 높이 fixture. diagnostic coverage 문턱 .01을 통과한 후보의 실제 출력 차이만 집계. 더 옅은 픽셀은 판정 범위 밖임을 기록. **physical=10 / opaque=11 / reference=12 겹침 및 reference=opaque 동률을 추가: 가림 픽셀의 그림자 ON/OFF 출력 차이0, bank target sentinel, owner bit0을 확인.** 또한 opaque 뒤(bank가 화면 소유), bank만 있는 양성 표본을 짝으로 확인 |
| RB-03 | 기존 평지/비탈/물가 회귀 없음, 원래 boundary 표본은 높은 턱 완료로 분류하지 않음 | 앞 matrix 관련 구역만 재실행, 수신0은 NOT_RUN 유지. 새 물/게임 규칙 만들지 않음 |
| RB-04 | 카메라/자원 수명과 은폐 계약 유지 | Main/다른 camera, 크기 변경, disable/re-enable, FieldView 재생성; 기존 SceneDepth/Bank `_FlatDepth` 불변·공유자원 identity·stale flag0. **비Main/overlay 연속, 생산 pass 미실행, 미지원 opaque, stencil/sample 불일치에서 flag0 및 가림 픽셀 차이0.** 기존 stencil mask0x01 보존. bank 윤곽의 sentinel↔유효값 경계·최종1픽셀·화면가장자리에서 Point raw가 -1 또는 실제 원본값이고 범위 밖 역투영0 |
| RB-05 | 이행·명령 관문과 독립 리뷰 | compile0·관련 EditMode 양수·메뉴2회/복구·짧은 Play. 구현 뒤 spec-reviewer/validator-client/algorithm-tester 중 변경이 닿는 리뷰 요청. 9단계 전체 테스트·playable은 단계 마지막1회 |

## 이 설계에서 판정하지 않는 것

효과·투사체·Bloom 양성·멧돼지 예고·UI·원근 Main 관문·전체 범위 대조표·preview는 **9단계 뒤 단위 몫, 이 설계 판정 밖**. 성능은 별도 단계, 이 단계 판정 밖(추가 bank draw/target 비용은 미측정 위험으로 기록). C4 추정 FAIL/G20 미실행, K55 NOT_RUN, N9 FAIL, C11 BLOCKED와 기존 전체 시험 실패는 그대로다.

## 이미 결정된 것·기준서와 다른 점

걷기24/대기12/공격7, Bruno fall12+lying·다른 대원10+lying, 피격추천안, 소품유료0, 나무3·덤불1, 기존UI유지, 성능별도. 새 생성/패키지 설치/외부전송0. 이번 제안은 이 결정이나 기준을 낮추지 않고, 기존 bank 은폐 계약과 실제 높이 그림자 수신을 동시에 유지하기 위한 별도 렌더 입력이다.

## 열린 지적·검토 요청

- 앞 spec 필수: 실제 높은 턱 수신 증거 부족 → 실제 30프레임으로 수신 실패 확인. 수정 설계 검토를 먼저 요청하며, runtime 수정 완료라고 쓰지 않는다.
- 앞 spec 필수: Bruno bank coverage 혼동 → 경계 표본/높은 받침 수신 상태를 이번 입력에서 분리했다.
- 앞 후속: 기존 수신0 11프레임은 NOT_RUN 유지. 침범0은 diagnostic 후보 영역에 한정. 앞 120개 값·해시는 리뷰가 확인했으나 이번 새 설계 PASS의 근거는 아니다.
- 이 검토에서는 **receiver 입력 분리 방식·기존 은폐 불변·RenderGraph pass 수명/카메라 격리·정면 opaque 가림 비교의 타당성·최소 검증 범위**를 판정한다. API 구현·실행 검증은 승인 뒤 수행한다.


## design-reviewer DR-RB-01 수정1회

수신 판정 **`VERDICT: FAIL`**, job `20261123-075643-a04b1b`. 이번 문서 수정 대조 기준은 `b1d392d488e31e044260452ec8af468ff6962939`다. DR-RB-01은 필수로 수용했다. 원인은 실제 물리 위치와 화면에서 색을 쓴 소유권을 같은 깊이 비교로 판단한 것이다. 처음 제안한 physical-depth 대소 선택을 삭제하고 **원본 opaque 색 패스의 소유 stencil → 원본 reference-depth ZTest → 실제 위치 복원**으로 방식을 바꿨다. 별도 clone shader도 없애 원본 reference 식을 재사용한다. RB-02에 10/11/12와 동률 opaque 겹침의 `가림 픽셀 출력 차이0`을 추가했다. RB-01의 양성30프레임·.04m는 유지한다. DR-RB-02/03은 후속 제안으로 받아 생산→공개/flag→transparent 소비 순서와 Point/sentinel 경계 규칙을 명시했다. 반박한 필수 지적0.

### 실제 읽은 설치 원본

- `Library/PackageCache/com.unity.render-pipelines.universal@f76d6966885c/package.json`:17.4.0.
- 같은 패키지 `Runtime/Passes/DrawObjectsPass.cs:239`: `UseAllGlobalTextures(true)`.
- 같은 패키지 `Runtime/StencilUsage.cs:7`: user bits0~3. 기존 `HD2D_{ContactShadow,FootShadow,GroundShadow}.shader`는 WriteMask1. 이번 후보 user mask0x02(비트 인덱스1)는 이 shader/레포 검색에서 사용0이다. `DQ_MainRenderer.asset`의 기존 stencil override는0, renderingMode는0(forward)다.
- 작업 트리에 없는 두 규칙의 읽기 원본은 `/Users/hyeongwoo/Desktop/Workspace/studio-kit/plugins/studio-kit/skills/25d-art-setup/template/root/.ai/rules/25d-render-pitfalls.md`와 같은 폴더 `25d-screen-structure.md`다. 설치/전역 설정은 변경하지 않는다. 앞 리뷰의 해당 두 규칙 정합성 **NOT ASSESSED**는 과거 판정 그대로이며 이 수정만으로 PASS라 적지 않는다.

### 검증·열린 지적의 범위

이번 변경은 이 브리핑만이다. 문서 일관성·원본 경로·깊이 선택 반례의 산술 대조만 확인하며, 새 런타임 컴파일/EditMode/Play/카메라 전환/메뉴 복구/RenderGraph 실행은 **NOT_RUN**이다. 앞 compile0·9/9는 그 소스의 기록이지 이번 렌더 설계 구현의 독립 실행 증거가 아니다. 앞 실제 높은 받침 수신30프레임 FAIL·L7-03 미충족, 기존11프레임 NOT_RUN·C4 FAIL·K55 NOT_RUN·N9 FAIL·C11 BLOCKED는 보존한다.

워크플로우 갱신 후보: 비표준 SV_Depth 표면에서는 **색 소유권/은폐 판정**과 **실제 위치 복원**을 분리하고, 서로 깊이 순서가 다른 3표면 반례와 깊이 동률을 설계 때 함께 적는다. 이 문서의 수신 선택/RB-02에 반영했다. 새 전체 검사를 만들지 않고 기존 RB-02 켬끔 쌍의 가림 영역 차이 확인에 통합한다. stencil 입력 지원 범위 밖을 성공 fallback으로 사용하지 않는다.

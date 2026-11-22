# 높은 턱 그림자 수신 — 구현 전 설계 검토

상태: **L2, design-reviewer 대기. 이 설계의 런타임 구현은 아직 하지 않았다.**

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

기존 카메라 depth와 actor 은폐는 유지한다. Main bank만 실제 표면 깊이를 따로 기록하고 DQBlobShadow 수신에서 그 깊이를 선택한다. 렌더 파이프라인에 pass 하나를 추가하므로 L2로 분류하여 구현 전에 검토받는다.

### 데이터 흐름과 소유권

1. `FieldView.BuildLandscape`가 이미 만드는 bank MeshRenderer 목록을 소유하고 readonly로 노출한다. 전역 registry·프레임별 FindObjects·새 게임 데이터는 만들지 않는다. FieldView 재생성/파괴 시 목록도 갱신/해제한다.
2. 기존 `WorldPresenter`의 배경 생성 직후 연결에서 Main 카메라의 `DQSceneLighting`에 그 목록을 한 번 전달한다. 생성 순서가 바뀌어 빈 목록이면 입력 미준비로 명시하고 오래된 renderer를 재사용하지 않는다. DI 생성자/등록/상태 소유권은 바꾸지 않는다.
3. 새 `DQBankReceiverFeature`는 Main 전용 DQ 카메라에서만 실행한다. 실제 bank renderer를 override 깊이 재질로 그린다. 다른 scene/preview/overlay/반사 camera에는 입력을 공급하지 않는다.
4. URP RenderGraph가 카메라 해상도에 맞는 **R32_SFloat raw device-depth 색 텍스처 + 전용 depth attachment**를 프레임 범위로 소유한다. clear=-1은 bank 없음, [0,1]은 실제 raw depth다. MSAA1. 외부 RTHandle 영속 소유와 프레임별 Texture2D 생성/readback은 없다.
5. opaque 뒤·transparent 이전에 bank를 그려 `_DQBankReceiverDepth`로 공개한다. RenderGraph에 attachment/전역 소비와 수명을 선언하며 해상도·유효 카메라 flag를 함께 공급한다. 정확한 URP 버전 API·event 순서는 구현 전에 설치된 패키지 코드로 확인한다. 기존 blur/world-indicator 순서와 기존 SceneDepth copy는 바꾸지 않는다.
6. `DQSceneLighting`의 기존 카메라 begin/end 수명에 유효 flag 초기화/복구를 연결한다. 비대상 camera·disable·Dispose·크기 변경 뒤 stale texture를 읽지 않는다. bank가 없는 화면은 명시적 빈 입력, target 생성 실패는 NOT_RUN/FAIL이고 높은 턱 성공으로 쓰지 않는다.

### 수신 선택

- 기존 SceneDepth raw를 읽는다. 같은 UV에 유효한 bank 물리 depth가 있으면 둘의 **linear eye depth**를 비교한다. bank가 opaque depth보다 가까울 때만 bank 물리 depth를 선택한다. 앞에 다른 실제 opaque 물체가 있으면 그 SceneDepth가 더 가까워 bank 입력을 쓰지 않는다.
- 선택한 raw를 지금과 같은 플랫폼/reversed-Z 처리와 `ComputeWorldSpacePosition`으로 복원한다. `.04m`와 bank/support 분류를 그대로 적용한다.
- bank의 경사 옆면·아래 바닥·반대 받침은 실제 깊이와 거리/분류가 달라 잘려야 한다. actor가 원래 바닥에 있을 때 bank 윗면으로 그림자가 번지지 않아야 한다. 물3000 순서는 보존한다.
- 새 깊이 shader는 실제 bank 정점 위치를 쓴다. camera 지터/투영/동적 해상도 UV는 기존 Main 카메라와 동일하게 하고 `_FlatDepth`를 복사하지 않는다. 기존 bank material·공유 blob material을 actor마다 복제하지 않는다.

## 예정 파일 (아직 미구현)

- 새 `Effect/HD2DView/View/DQBankReceiverFeature.cs`, `Shaders/HD2D/DQBankReceiverDepth.shader`.
- `FieldView.cs`의 bank renderer 목록, `WorldPresenter.cs`의 생성 직후 연결, `DQSceneLighting.cs`의 카메라 입력 수명.
- `DQ_BlobShadow.shader`의 receiver 선택. 기존 SceneDepth 복원/문턱과 재질 공급 경로는 보존.
- `MainSceneDQLightMigration.cs`: Main renderer feature·공유 depth material을 메뉴로 생성/갱신. 새 자산을 snapshot/rollback 목록에 포함. 두 번 실행한 자산 hash 동일·실패 복구·dirty=false 확인.
- 관련 EditMode 시험 및 기존 `DQLightingVerification`의 범위별 수집 확장. 장면/재질은 이행 메뉴 외 손편집0.

## 수용 기준과 최소 확인

| ID | 조건 | 확인 |
|---|---|---|
| RB-01 | 실제 높은 bank 받침에서 6종×5프레임 수신이 양수이고 world 오차≤.04m, 다른 받침 침범0 | 같은 `StartTransitionMatrix(true)` 실제 bank 삼각형·root 높이·receiver 후보/수신/거부·mesh/material/texture ID, 연속 capture. 입력 누락은 성공 아님 |
| RB-02 | actor가 바닥에 있을 때 턱 위/수직면으로 잘못 번짐0, 높은 받침과 낮은 바닥의 구분 유지 | bank 경계 양쪽 높이 fixture. diagnostic coverage>.01 후보 영역의 실제 출력 차이만 집계. 더 옅은 픽셀은 판정 범위 밖임을 기록 |
| RB-03 | 기존 평지/비탈/물가 회귀 없음, 원래 boundary 표본은 높은 턱 완료로 분류하지 않음 | 앞 matrix 관련 구역만 재실행, 수신0은 NOT_RUN 유지. 새 물/게임 규칙 만들지 않음 |
| RB-04 | 카메라/자원 수명과 은폐 계약 유지 | Main/다른 camera, 크기 변경, disable/re-enable, FieldView 재생성; 기존 SceneDepth/Bank `_FlatDepth` 불변·공유자원 identity·stale flag0 |
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

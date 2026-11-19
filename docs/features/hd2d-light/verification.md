# 빛 단계 실행 기록

기준 커밋: 12e0567. 게임 높낮이 fee4ae3, 문서 1a6e4b9. L1 작업, 설계 사전 리뷰 없음. 단계별 구현·해당 검사·커밋 → L6 전체 검사 1회 → Claude 마감 리뷰. 직접 리뷰 호출 없음.

기준 문서와 다른 점: r8.1 K21 비탈 명암 L1b 및 풀밭 생성기 수정 허용을 적용. r7 메시 실제 그림자 정책이 구 판 그림자 설명보다 우선. Bloom은 구 설계 1.8/.4 대신 3.12의 2/.1. G19 성능은 7단계, G20·K21 눈가림은 디렉터 몫. 노치·발 반폭 문구·물 위 바닥 그림·원본 모델/질감 수정은 이 단계 판정 밖.

이미 결정된 것: seed8 높이·배치·게임 좌표 유지, 평지 키워드 OFF 경로 보존. 유료 생성·순간 점광·push 없음. 다른 세션 dirty 파일 보존.

## L0 실제 값 / 기준 / 차이

사전 검사: 2026-11-19 project-doctor configured/findings0 PASS; editor_ready PASS; Unity 6000.4.3f1, 해당 프로젝트 Main, isPlaying=false; console errors0(cursor1352). CLI1.0.1 최신.

| 항목 | 3.12 기준 | 지금 값(저장/실시간) | 차이 |
|---|---|---|---|
| 해 | 32/135°, Hard, bias .05/.02 | 동일, 흰색 intensity1 | 0 |
| 그림자 RP | 35m,1단,Mobile1024/PC2048 | Mobile35/1/1024, PC2048(live AssetDatabase) | 0 |
| 환경광 | Flat Color, 따뜻한 짙은 갈색 | (.43,.32,.27) Flat, SpriteLit 내부에도 같은 상수 | 연결 개선 검토 |
| 툰 | .58/.80/1, .25/.68 | SpriteLit 일치; FieldSurface .60/.82/1, .25/.68 | 배경 램프 .02 차이 |
| 높이 경로 | r8.1 비탈 명암 | mesh normals 있으나 ground 색에는 사용 안 함 | L1b 추가 |
| 흐림 | v.16~.76,9탭 | 동일, 오프셋1.8px·추가 radial dim .82 | 별도 어둡게 제거 검토 |
| Bloom | 2/.1/.25/Dual/Quarter | 동일 | 0 |
| 톤/HDR | Neutral/R11G11B10 | Neutral, supportsHDR, _32Bits | 0 |
| Vignette/색 조정 | .6~.8, 색0에서 | 전투 Profile에 없음, RP 기본 Profile Vignette .2/.2 + 셰이더 dim | 중복 어둡게 |
| 입자 | <=36,1.4/s | 본 게임 먼지 없음 | L5 |
| 효과 밖 | WorldIndicator 후처리 뒤/UI Overlay | 기존 renderer 기능·UI 유지 | L3 실제 화면 재확인 |
| 그림자 실제 정책 | 최신 r7 메시, 풀/꽃 Off | 메시 유지 | 구 3.12 문구 최신화 대상 |

L0 촬영 시도: (1) recompile 시작 응답 직후 playable 실행은 연결 없음 BLOCKED; (2) Editor assembly MonoBehaviour AddComponent 실패 → App/#if UNITY_EDITOR로 이전; (3) 정상 생성됐으나 AutoNext만으로 wave4 패배; (4) scale.05+Equip은 wave6 도달, 지나치게 약한 적이 즉사하여 숫자/HP 동시 화면이 드물다. 시각 fixture를 보완하며 검사를 약화하지 않는다. 과거 성공 캡처를 현재 성공으로 사용하지 않는다.

넘김7: main1 C15의 Vignette .2/.2는 8장 모두 동일, 비1.077~1.091 변화는 화면 내용에 따른 측정 착시. 넘김8: main1 실제 마스크 C10 rim/top1.200109 PASS, 테 배율 재증가하지 않음.


## 단위별 기록

L0: 촬영 fixture 보완 후 4/12단계(UNIT까지) PASS, 28.5초, wave6/frame60/numbers3/hpBars2. source12e0567 + 이번 촬영 도구 변경. 컴파일 completed/failed=false. 기준 캡처1920×1080 `output/hd2d-light/L0-start.png`·`L0-battle.png`, 렌더 진단 `L0-diagnostics/`. 직접 연 것: 시작 화면(풀 사선 띠/차가운 잎), 전투 화면(피해 숫자24와2HP막대), main.png(동일 전투 UI제외), blur-off.png(중앙 변화 거의 없음, 가장자리 선명). 열람 파일 해시 viewed.json에 기록.

| baseline 측정 | 현재 | 기준/상태 |
|---|---|---|
| C3 | #999765, ΔE6.811, S.3464 | <=12/.30~.42 PASS |
| C8 | V차−.258824 | −.30~−.20 PASS |
| C9 | V.247059 | <=.36 PASS |
| C10 | 테1.200109/옆.430384 | 1.1~1.3/.25~.5 PASS |
| G18 | world1.155508/height1 | 1~1.5 PASS |
| 그림자색 | H52.174,V.247 | 추정 H15~50 FAIL. 그림에서도 올리브로 읽힘; L1에서 따뜻한 갈색·진함 조정 |
| 툰 진단 |153/209/255 3단 |2~3 PASS (clear color는 제외; B8 동일 규칙) |
| C15 | .788769(UI없는 동일프레임) |.6~.8 PASS. UI 포함 .604867와 섞지 않음 |
| Bloom pair | 풀차0/전체최대0 | 풀<=1 PASS, 효과>0 FAIL[추정]. 이 표본에는 발광 핵이 없어 L3에서 해당 표본 추가 |
| 흐림 | measure.json 행별 고주파·blur-off쌍 | 실제 경계는 L0-full 최신 measure; 유효화소2LSB 이하 half 정밀도 차 제외 |
| C16 | 풀S.3464<인물S.5152 |PASS, 판정제외 |

누락 원인/후보: 재컴파일 started를 completed로 오인하지 않기; Editor 어셈블리 MonoBehaviour를 붙이지 않기; 플레이 fixture는 예산/생존·필수효과 동시표시를 처음부터 고정. 이 작업 흐름에 반영, 공통 규칙 추가 없음. session_time.py는 기본이 Claude 로그를 읽어 현재 Codex 시간으로 쓸 수 없음. 해당 출력은 현재 사용량 증거가 아님; Codex 토큰 API미제공, 직접 하위 에이전트0. 이후 단위 시작/끝 시각을 기록.

### L1 (2026-11-19 05:56 UTC 전후)

그림자 배경배율 .44/.43/.39→.52/.45/.36, 곱셈 그림자 .44/.43/.39→.62/.53/.40(선형). 해·환경광·거리·그림자 정책은 이미 기준과 같아 유지. 컴파일 up-to-date/failed=false; 해당 흐름UNIT4/12 PASS, frame60 일치. main.png·sun-off.png 직접 열어 대원/나무 그림자가 발·밑동에 붙고 갈색이 됨을 확인. 길이1.155508(1~1.5)·색 H41.538/V.298(추정15~50/>.10) PASS. C3 ΔE6.811 그대로. 검정으로 눌리던 배경그림자가 덜 진해짐. 근거 `output/hd2d-light/L1-measure/measure.json`, L1/frame.json·shadow.json. 3.12 값을 같은 커밋에 고침. 놓친 것: 없음. 전체Play/전체시험/성능 NOT_RUN(이 단위 대상 밖).

### L1b

높이 격자 mesh에서 재계산된 normal의XZ만4배, dot.47/.59에서 .90/1/1.07의3단. 셰이더 `_TERRAIN_HEIGHT` 안 ground에만 적용. 정점·높이 데이터·이동·판정·OFF 경로 그대로. 컴파일 오류0, 지형 지정흐름7/7 PASS(26.8초). 직접 연 것 k21/T2.png·T7.png·Main1-hill.png: 넓고 경계가 있는 밝고 어두운 띠, 세부 풀 결 유지. T2 시각 fixture는 정상 이동 한계 밖, 새 정상 도달 주장 없음. C3는 k21/measure/measure.json; G20/K21의 독립 눈가림은 NOT_RUN(디렉터 몫), 이것을 PASS로 바꾸지 않음. 높이·field_layout 파일 해시 변경 없음.

### L2

FieldSurface 램프 .60/.82/1→.58/.80/1(문턱.25/.68), SpriteLit는 이미 일치. 기존 턱 옆면 그림자 중복 제외·몬스터 수신Off 유지. 컴파일 failed=false, ShaderHasError=false/messages0. 시작흐름3/3 PASS23.6초, 진단148/204/255의3단 PASS. L2/main.png·정확한 [420,96,1644,804] Point4배 closeup.png 직접 열어 바위·식물 도트 계단/단색면, 중앙 부드러운 그라데이션 없음 확인. 최종ON G09/G11은 L3/L6에서 같은 원본 관문으로 다시 판단(아직 전체아트FAIL 유지).

### L3 — 부분 구현, 미해결 유지

Vignette override(.23→.26, smoothness.2), 색 조정0 추가; Bloom2/.1/.25/Dual/Quarter·Neutral·32bits 유지. BandBlur 중복 radial dim 제거; 2출력px Point9탭, 경계에서0으로 줄이는 것은 오프셋이 아니라 강도(.35→.5). 중앙은 즉시 원색 반환(half 누산 오차 제거). 에디터 값은 L3-edit-values.json(Play=false). 지정흐름UNIT4/12 PASS32.2초, shader/script 컴파일 오류0. L3-valid/main.png·bloom-off.png 직접 열어 HP/숫자 선명·중앙 도트·밖 약한 흐림을 봄.

C15 .837780(.23)→.815059(.26): 0.6~0.8 **FAIL**. 흐림 실측경계 v .143056/.782870(.35)→.148611/.781019(.5): 아래 PASS, 위 허용 상한.78보다.001019 커 **FAIL**. 측정은2LSB 이하 누산잡음 제외한 행별 차·고주파곡선 저장. 단위2회 수정 실패 상한이므로 같은 항목을 혼자 세번째 튜닝하지 않고 열린 항목으로 보존, 다른 단위 계속. 문턱 변경 없음.

Bloom: 풀 평균차0 PASS, 효과 쌍 전체 최대차0 **FAIL[추정]**. 실제 explosion_3을 기존 풀/재질로 같은 정지 frame60에 추가한 시각 시험이다. 흰 핵2.4→6→32 시도에서도0. 카메라 ViaScripting 캐시 발견→CaptureCamera.UpdateVolumeStack 추가, intensity0 OFF로 재확인했으나 여전히0. 미입증 핵32/6 변경은 원래2.4(Pulse2.2/Heal1.6)로 되돌림. 앞선 캐시미갱신 쌍은 무효, 마지막 valid쌍도 FAIL을 유지한다. Unity GammaToLinearSpace(2)=4.594794; 앞 commentary 약4.95는 정정. 원인이 확정됐다는 초기 해석도 철회한다. 이 추정 검사만으로 추가 그림 변경 반복을 열지 않음.

L3 진단 Prepare는 'No unobstructed 61,440-pixel grass panel'로 실패했다. 아직 Main2 선언을 하지 않았으며 기존 판 상자를 바꾸지 않았다. L6에서 다른 정상 이동 초점을 사전 선택하거나 차단을 그대로 기록한다.

놓친 것/워크플로우 후보: HDR 기준과 effect core만으로 원인 확정하지 않기; 동일프레임 토글은 실제 camera volumeStack 값을 함께 남기기; SaveAssets 대신 지정 SaveAssetIfDirty(자동 shader-default 부수 변경은 기존 authored 값 보존하며 Editor SerializedObject로 제거). 공통 워크플로우 확대 없이 이 기록에 반영.

### L4

C3·C8·C9·C10이 이미 범위 안이어서 해/환경광/색조정0을 추가로 바꾸지 않음. 허용된 grass 생성기의 큰 bilinear 명암 얼룩만 제거(그 역할은 높이 명암으로 이동);125000개 native 풀잎 조각·4색·Point·길/물/zone 마스크 유지. 잎 UV tint(1.15,1,.85)로 청록을 따뜻하게, 모델/알베도 그림/UV 안 바꿈.

컴파일 failed=false, 생성기 기존시험2/2 PASS1.955초. 본 게임 시작 지정흐름3/3 PASS. L4/main.png 직접 열어 넓은 풀 얼룩 대비가 줄고 풀잎 결이 유지됨을 봄; 아직 G20 반복무늬 재판독 NOT_RUN(디렉터). 실제 측정: C3 #999765 ΔE6.811/S.342466 PASS; C8 −.274510 PASS; C9 V.243137 PASS; C10 테1.200726/옆.438986 PASS(테 배율1.2 유지); C16 풀S.3425<인물S.5724 PASS, 판정제외. paint.py 실제 풀 crop 측정 L4-paint.json.

작은 지적 처리: 대원 그림자 길이1.1555/갈색/덜 진함 L1, 가장자리 L3 FAIL 유지(C15 L4재측정.809385), 잎은 재질 tint. 강 경계 갈색 한 줄은 현재 K19 실루엣 테로 남아 있어 미해결, 배낭-길 명도는 배낭만 바꾸려면 채택 그림의 부위 색 판단이 필요해 미해결로 유지(이번 단계에서 새 원본 아트 수정 안 함). 이 둘을 고쳤다고 주장하지 않음.

### L5

Effect/Ambient의 scoped entry point→ResourceService 프리팹 로드→PoolManager.Get<ParticleSystem>, 전투에서만 재생/그 외 Clear·중단, Dispose 반환·전역 포즈 복원. 먼지는 layer0 반투명, 후처리 뒤 layer26/Overlay UI 위에 그리지 않음. Renderer 그림자Off, 점광0. native 최대36/rate1.4/수명18. 물 무늬는 1/2048 UV(월드1/48m) 정수칸0/1/2/1, 깃발은 오른쪽 꼭짓점만 최대±3 native칸, 왼쪽 고정. 4Hz 계단3포즈, 실제 geometry/물 경계 변경 없음.

컴파일 completed/failed=false. 새 EditMode9/9 PASS7.14초(0/.249999/.25/.5/.75/1/큰시간, 잘못된시간, 실제ParticleSystem.Emit100→cap36 및rate 검증). 흐름4/4 PASS32초. 연속8장 상태는 L5/states.csv: 정확15 Unity프레임·게임.25초 간격, 활성3/3/3/4/4/4/5/5, rate전부1.4, limit전부36, 포즈0/1/2/1/0/1/2/1.

직접 연 것 L5/motion-sheet.png(8장, 베기/회복/피해 숫자가 실제 움직임), L5/detail-sheet.png(원본에서 물·깃발 영역 잘라 Point4배): 물 밝은 점 무늬·깃발 천이3포즈로 움직이고 왼쪽 깃대 붙음/격자 계단 유지. 별도모델·스프라이트 프레임을 재생성하지 않음. sheet.py의 UNVERIFIED 표시는 G20 전 관측자료임을 명시하며 PASS로 꾸미지 않음. 원본캡처1920×1080.

문제/후보: run.next_action이 L1에서 뒤늦게 갱신됐음을 발견해 L5 현재상태로 바로잡음(중간 단계 메모 갱신 누락); 다음 상태 변경 전에 갱신하도록 이번 마감에서 반영.

### L6 첫 판 → G11/G12에 한정한 L3 재확인

전체Editor622/622 PASS, 전체플레이8/8 PASS154.8초. Main2-20261119T063050Z 사전선언·수집·finish 후 원본gate: G09 PASS(tree soft.145620/stone.078864,3단), G11 FAIL(hero.002402/background.137972), G12 FAIL(hue45.14). G03/G20은 실제 리뷰파일이 없어 BLOCKED, G19 N/A. G12 상자[994,528,1034,549]는 대원·피격흰색과 겹친 슬라임으로 실제 보라 몸 색을 제대로 재지 못함(캡처 직접 확인). 수집 이후 상자 수정 안 함.

첫 판/선언/캡처를 main2-attempt1/{gate,plates}로 보존. L6에서 값을 몰래 고치지 않고 L3 재확인으로 되돌아감: G11 화면 색 변동을 줄이려Vignette.18. C15 목표.6~.8은 유지하며 미완료를 숨기지 않음. G12는 같은 재질·기본 데이터 슬라임을 전투 중 잠깐 멈춘 색 판독 자리로 분리, 겹친 전투 표적은 버섯으로 구분. 게임 데이터/코드/재질 색/관문 문턱을 안 바꾸고 새 그림 이전에 다시 선언. 게임 동작 파일이 안 바뀌어 전체Editor/전체Play는 위 결과를 그대로 사용(재반복 없음).

추가 누락: L6 임시흐름에 sizes가 없어 UI capture-all이 기본 세로4종을 찍음. 이 캡처는 요구한 가로4종 증거로 사용하지 않음; 재확인 흐름에 정확한 가로4종을 명시. 첫 판의 카메라 직접 캡처3종+추가2340은 원래 올바른 크기.


### L6 최종 재수집 — 리뷰 전 체크포인트

2026-11-19 06:42:36 UTC 사전 선언 `Main2-20261119T064236Z`, plan SHA256 `59c776c00ef31c55682f53033693e1648677827f7d219d97c2006cada53ba2dc`. 원본킷 gate/config를 그대로 실행. 판 id는 수집기 기존 `Main1`, 결과 폴더는 `output/hd2d-관문/main2/`다. 해/볼륨/흐림 **최종 효과 ON** 결과다. before/after1424개 일치, 06:55UTC 재확인도 불일치0(`.director/evidence/20261118-hd2d-light/1/source-recheck.json`). 첫 판과 재수집의 역할·상자를 수집 뒤 바꾸지 않았다. 수집 배치는 통상 전투 자체의 성공 증거가 아니라 색 판독 fixture이며, 게임 동작은 별도 전체 흐름으로 검증했다.

| 판정 | 최종값 | 기준 / 결과 |
|---|---|---|
| G09 | 나무3단/soft .052963, 돌3단/soft .093168 | 2~3단/soft≤.15 PASS |
| G11 | 인물 MAE .002962, 배경 .061024 | 각각≤.10 PASS |
| G12 | 색상차131.731°, ΔE72.974 | 원본 gate PASS |
| G18 | 1.128800 | 1~1.5 PASS |
| C3 | #9b9666, ΔE7.4517, S.342105 | ≤12/.30~.42 PASS |
| C8 | 나무−풀V −.270588 | −.30~−.20 PASS |
| C9 | #2b4136,V.254902 | ≤.36 PASS |
| C10 | 테1.200726/옆.438986 | 1.1~1.3/.25~.5 PASS |
| C16 | 풀S.3421<대원S.4970 | WARN 용도, 판정 제외 |
| C15 | 본판1.165921, 이동8장1.165656~1.181919 | .6~.8 **FAIL** |
| 흐림 경계(최종 별도 동일프레임 진단) | v .148611/.778241 | .16/.76±.02 PASS, `L6-effects-measure/measure.json` |
| Bloom(같은 최종 진단) | 풀 평균차0, 효과 포함 전체 최대차0 | 풀≤1LSB PASS, 효과>0 **FAIL[추정]** |
| 먼지 | 연속8장3~5개/rate1.4、native cap36 시험 | ≤36/1.4 PASS |

자동 관문 12 PASS: G01·G02·G04·G06·G08·G11·G07·G09·G12·G13·G15·G18。G03 **BLOCKED**(사전선언 일치/필수 해시 일치, review.json 미수령), G19 **N/A**(deferred 그대로), G20 **BLOCKED**(디렉터 판독 미수령). 전체 **FAIL**. 숫자 G09/G11은 해결했지만 3D T4/전체 아트 완료로 승격하지 않는다. 존재하지 않는 G05/G10 등의 행을 임의로 만들거나 실행한 것으로 세지 않는다.

L6-effects 지정흐름4/4 PASS29.9초. 실제 explosion_3 추가·같은 frame60의 BloomON/OFF 두 장을 직접 열었다. 둘은 동일 해시 `ca99df4aa4c8fc9f270b87f638898013cc7aa124116cf9dedd8bebc3b4e0ea7c`, 밝은 핵은 보이나 번짐의 켬/끔 차이는 확인되지 않는다. 최종 흐림 경계는 앞 .26 시험의 위쪽 FAIL에서 .18 최종값 아래 PASS로 바뀌었으며, 관문 문턱은 그대로다. 아래 흐림≥위 흐림의 별도 대응 질감 실측은 **NOT_RUN**, 셰이더 띠 함수와 화면 확인만으로 수치 PASS라 하지 않는다.

K21 최종 입력은 `output/hd2d-light/k21-final/{T2,T7,Main1-hill}.png`, 흐름7/7 PASS31.1초. 세 장을 직접 열어 밝고 어두운 비탈 띠·풀잎 결·턱을 봤다. 디렉터의 동일 눈가림 질문·두 판독자 T2≥2/평지보다 높음은 **NOT_RUN**. 이전 지형 B0는 풀 그림/후처리 버전이 달라 엄밀한 최종 대조판과 동일 소스가 아님을 브리핑에 넘긴다. 디렉터가 같은 조건의 평지 대조를 필요로 하면 다음 재수집에서 먼저 고정한다.

#### art-core-rules 8절 점검

| 항목 | 확인 / 미확인 및 직접 본 것 |
|---|---|
| 그림체 | 확인: L2 Point4배 단색면·격자, L5 물/깃발3포즈의 또렷한 칸. 최종 G09 PASS. G20 스타일 독립 판단 미확인 |
| 카메라 각도 | 확인: 정사영30° 기존값, 본판/4화면비에서 영웅/배경 결 유지 |
| 크기 | 확인: 기존 월드/PPU 유지, 해상도별 정수배율. 새 먼지1/48m, 물1/48m 이동 |
| 게임 동작 | 확인: 전체 EditMode622/622 PASS37.88초, 전체 판정경로8/8 PASS154.8초 각1회. 앞의 단위 실패도 보존 |
| 같은 화면의 다른 것 | 확인: 8방향 대원·몬스터·3D소품·HUD·피해 숫자·HP와 실제 전투 효과. G12/G13 PASS. 원본 모델/그림 수정 없음 |
| 바뀐 것이 닿는 곳 | 확인: layer26 WorldIndicators 후처리/흐림 뒤, OverlayUI 효과 밖, 먼지layer0. UI막대/숫자 정상 화면 선명. 별도 아래 띠로 옮긴 숫자6개 선명; HP막대 아래 띠 직접 판독은 **미확인**(같은 시험에서 막대가 확실히 보이지 않음). 렌더러 순서만으로 완료 주장 안 함 |
| 화면비 | 확인: 1920×1080·2340×1080·2520×1080·2048×1536 실제 네 장 직접 열음. 상대 UV 띠/가장자리 효과·HUD 위치, 태블릿 정수3배. 같은 비율의 숫자 실측은 미확인, C15 자체 FAIL. 실제 노치 기기 미확인(다른 단계 후속) |

4화면비 근거 `L6-recheck-play/09-ASPECTS-*.png`. 첫 임시 흐름 sizes 누락으로 생성한 세로 자료는 요구 화면비 증거에서 제외, 명시적4크기로 고쳐 해당 흐름만 재실행했다. `indicator-blur-test.png`는 layer26 표시들을 아래v.08로 잠깐 옮긴 뒤 복구한 소유 Play 진단이다.

최종 `battle-main.png`1920×1080을 직접 다시 열었다. UI를 숨긴 기존 판 수집 카메라 결과라 HUD 포함 증거는 위4화면비 파일이다. 5단계 `output/hd2d-background/battle-main.png`도 직접 열어 비교: (1) 최종 그림자는 갈색이며 덜 검게 눌림, (2) 잎 청록 감소·넓은 풀 얼룩 대비 감소, (3) 실제 물/깃발/먼지 움직임은 L5 연속 시트에서 확인. 예전 그림은 3D/지형 전환 전이라 배치·모양도 다르다. 이 비교를 빛만의 격리 비교라고 하지 않으며 L0/L1 동일 frame60 쌍을 빛 변화 근거로 쓴다.

Unity 검사에서38개재질에 새 셰이더 기본값만 자동 직렬화됨. `L6-serialized-defaults.json` 전부 onlyShaderDefaults=true; 기존 색/질감/키워드/배율 제거·변경0. final1424 소스 지문에 포함된 상태로 L6 커밋에 기록한다. 변경 없는 기본값을 없애려고 추가 SaveAssets/역직렬화 반복을 하지 않았다(앞 L3 후보를 이 판단으로 갱신). 생성 Unity YAML 일부 빈 필드 뒤 공백은 diff --check WARN이며 의미 변경이 아니다. 코드/문서 검사와 구별한다.

#### 열린 항목·되풀이 상한

- C15: .23→.26 두 수정 실패(.8378→.8151). 별도 필수 G11 재확인 .18에서 G11 PASS, C15 본판1.1659/시작진단.87725로 여전히 FAIL. 같은 설정인데 장소·내용에 따라 비가 달라짐. 볼륨 .18/.2는 이동8장 동일하므로 넘김7 출렁임은 내용에 따른 측정 착시로 분류. 목표는 완화하지 않음. 다음은 디렉터 화면 판독으로 기준의 측정 방법/시각 결함을 나눌 지점이다.
- Bloom: core6/32 방식·캐시갱신 방식도 쌍 차0. 원인은 미확인, 임시재질 수정 모두 철회. 추정 숫자만으로 혼자 새 그림 수정을 반복하지 않음. 디렉터 G20 판독 요청.
- 흐림 최종 실측경계 PASS로 앞 FAIL은 이월하지 않음. 아래≥위 강도 대응실측·HP막대 아래 띠 직접 확인은 미완료로 남김.
- 강 경계 갈색 선·배낭/길 명도 지적은 미해결. 원본 아트·모델 새 생성 금지/색 부위 채택 판단 필요, 해결했다고 꾸미지 않음. 둘째 천막 그늘 면은 최종판에서 확인되나 독립 판독은 대기. 돌담 사선/얇은 인상은 원본 담당 전달 그대로.
- G20 ground 반복·K21 비탈 읽힘은 디렉터 전용 판단 대기. 노치/발 반폭 문구/물 위 바닥 그림 인상은 단계 밖, 수정하지 않음.

#### 시간·토큰·상태

신뢰 가능한 시작05:39:10UTC부터 문서 마감06:55UTC까지 약75분50초(앞선 최초 문서 읽기 시간은 미계측). 상세 활동은 `.director/evidence/20261118-hd2d-light/1/time-and-tokens.json`. Codex 토큰 API 미제공 **NOT_AVAILABLE**, Claude실행0/서브에이전트0/리뷰0. session_time.py의 기존 출력은 다른 Claude세션이어서 현재 사용량으로 인용하지 않는다. 가장 큰 낭비는 L3 원인 가설 확정 전 캡처 반복·L6 물리 배치와 요구 화면비 누락. 후보는 작업 개선 기록에 반영했고, 같은 변경에 전체시험 반복은 실행하지 않았다. 결함 못 잡은 고비용 검사 제거는 근거 부족으로 이번에 없음.

06:55UTC 소유 Play 종료 후 Application.isPlaying=false/isCompiling=false 직접 확인. 성능 측정/로그 열람 **NOT_RUN**(7단계). 다른 dirty 파일 보존, push 없음. 마감 리뷰3종은 **NOT_RUN**(사용자 지시로 Claude가 실행), 결과를 자가 검토로 대체하지 않는다. 현재 상태 **needs-review**, 최종 핸드오프는 실제 VERDICT 수령 뒤 작성한다.


## 첫 리뷰 수정 확인 (2026-11-19)

받은 판정 원문: algorithm-tester `VERDICT: FAIL`, spec-reviewer `VERDICT: FAIL`, validator-client `VERDICT: NOT ASSESSED`. 마지막 것은 출력 강제 종료로 결과 미수령, **not-run**(사용자 지정). 이 세션은 리뷰를 호출하지 않았고 받은 FAIL을 자가 PASS로 바꾸지 않는다. 훅 기록 `.director/outbox/20261118-hd2d-light-1.agents.jsonl`.

| 지적 | 조치 / 근거 | 현재 |
|---|---|---|
| ALG1 깃발 normal/Fade `_FlagWind=0` | 두 재질1로 수정, 실제 AssetDatabase 재질 회귀시험. 실제 시간 흐름 연속8장 재촬영 | 구현 확인, 독립 수정리뷰 대기 |
| SPEC1 C15 미완료 | 자동튜닝하지 않음. 기존2회 실패와 새 판1.165011 보존. 실제 화면 결함과 측정 내용 의존을 디렉터가 나눠 방향 결정할 때까지 정지 | **FAIL / 사람 결정 필요** |
| SPEC2 아래≥위 흐림 강도 | 같은4출력px Point줄무늬 두 판, 각 띠 중간 깊이(v.08/.88), 동일 프레임ON/OFF 수집. 내부96×44px HF감소율 아래.129723≥위.124385 | 명령 검사 PASS, 독립 수정리뷰 대기 |
| SPEC3 흐림 띠 안 HP막대 | 실제 MonsterHpBarViewFactory→PoolManager로 생성, layer26 원본track/fill·비율.625. v.08에 놓고 중립 바탕에서ON/OFF. 두 장 직접 열어 가로32px 막대·채움 경계 선명 확인, 막대 포함88×34px영역 max/mean차0 | 명령 검사 PASS, 독립 수정리뷰 대기 |
| SPEC4 재고 집계 | 27행 자산재고0차이를 반영하여 요약40개/남은3개(2,42,43)로 수정. L3 수용조건 실패는 별도 유지 | 문서 수정, 독립 수정리뷰 대기 |

### 실행과 직접 본 것

project-doctor configured/findings0, editor_ready PASS. script eval로 시작Play=false, 해당 프로젝트 port7800/Unity6000.4.3f1 확인. 수정은 재질2·테스트·기존Editor촬영기·기존측정도구·지정문서뿐. runtime 게임 C#·Main·렌더러·볼륨·흐림 셰이더 변경 없음. `.ai/rules/fix-and-recheck.md` 및 원본킷 동명 파일은 검색해도 없어서 사용자 메시지의 상한/재확인 규칙을 직접 적용, 새 전역규칙 설치 없음.

새 회귀시험 첫9/10 실패: Resources.Load 경로가03.Resources 밖이라null(실제 재질이0이라는 실패와 별개인 시험 경로 결함). AssetDatabase 경로로 고친 뒤 재컴파일 completed/errors0. 직후 테스트 요청은 Pipeline 재연결 전 EDITOR_NOT_RUNNING으로 **BLOCKED**였고, status ready/isPlayingfalse 확인 후 재실행 `tests3.json` **10/10 PASS7.62초**. 전체623개를 돌렸다고 하지 않는다. 이전622/622와 전체판정8/8은 이전 코드의 실제 기록이며 이번에는 반복하지 않음(시각재질/Editor시험만 변경).

`review1/play/report.json`5/5 PASS32.0초(기존wave6·실제모션8장·blurreview쌍). `review1/flag-play/report.json`3/3 PASS32.0초(깃발만 실제 카메라에 가운데 놓는 시각 fixture). flag/states.csv에서 게임시간.25초 간격·pose1/2/1/0/1/2/1/0·normal/fade모두1. 기존수집기는 내구표적/낮은 적 배수 시각fixture로서 실제 한판 성공 증거와 구별된다.

직접 열람: `review1/flag-sheet.png`(원본8장1920×1080→중앙깃발잘라Point4배, sheet.py UNVERIFIED는 독립리뷰 전 표기). 끝 천이0/오른쪽/왼쪽의3자리로 바뀌고 깃대는 고정, 또렷한 격자. 기존 L5 시트의 물과 깃발을 함께 보았다는 기록은 **깃발 비활성 상태를 놓친 오판**이었다. L5의 깃발 움직임 관측 주장은 철회하고 이 수정8장으로 대체한다. 물 순수계산/먼지 상태와 별개로 기록한다.

`review1/blur/{texture-on,texture-off,hp-on,hp-off}.png` 네 장1920×1080 직접 열람. transient URP Unlit point stripe quad와 실제 풀HP막대만 올린 소유 정지Play이며 scene/재질자산을 저장하지 않음. feature만OFF로 쌍을 만들고 finally복구/Release. `texture-measure/measure.json`: onHF아래12.477825/off14.337762, 위12.507473/off14.284211, 감소율1−on/off. **서로 다른 띠 폭의 같은 정규화 깊이 .5** 비교이며 화면 가장자리에서 동일 픽셀거리 비교라고 하지 않는다. HF는수평방향 인접픽셀차 평균, 9탭경계 판정과 별개. `hp-measure/measure.json`: box[916,976,1004,1010] 차0, hp-renderers.txt에 원본U7/layer26/viewport.08. backdrop 영역이평탄하므로 배경 흐림을 막대 선명도 변화와 혼동하지 않음.

깃발 수정이 이동 판에 닿으므로 이전 main2를 덮지 않고 `output/hd2d-관문/main2-review1/`에 새 선언 **Main2-Review1-20261119T072855Z**(hash05a9289185be9c40fddcdfaf7d40c5614e6c7cb7038b0d127980784e5edfa781), 소스1424개로 수집. 지정흐름8/8 PASS72.5초, finish before/after일치, source-check.json mismatch0. 원본kit gate/config 유지, 최종ON **12PASS/G03BLOCKED/G19N/A/G20BLOCKED**, 전체FAIL. G09/G11은 이전수치와같이PASS. C3/C8/C9/C10도같이PASS, C15 **1.165011 FAIL**. `review1/plates/Main1-main.png` 직접 열어 깃발·동일전투색 확인, battle-main.png를 이 최신본으로 갱신, 앞그림은review1/battle-before-flag-fix.png로 보존.

### 누락 원인 / 워크플로우 갱신

1. 깃발은 시간계산만 시험하고 연결재질의 실제 branch enable을 검증하지 않아 비활성화를 놓쳤다. 실제 두 재질 검사10번째 시험과 집중8장캡처를 추가하여 **이 작업에 반영**. 대신 깃발 수정을 위해 전체판정/전체시험을 반복하지 않음.
2. G09 `_Diagnostic`은 비탈계산 이전에 return한다. 기존148/204/255 3단은 **소품 툰 램프** 증거이며 L1b 비탈 명암의 독립3단진단으로 쓰지 않는다. 비탈은 높이경로 계산·실제K21캡처·디렉터판독을 따로 본다. 브리핑에 반영.
3. 부분재고 갱신 후 요약을 같은단위에서 다시 세지 않음→40/3 정정. 자산 재고와 수용조건을 분리해 유지하도록 기준서에 반영.
4. 위아래강도/HP각각 별도 증거가 필요했는데 경계/숫자로 대신 추정. 같은질감ON/OFF와 실제풀HP막대 시험을 기존촬영기/측정기 한 곳에 추가. 별도 일회성 측정스크립트 추가0.
5. 재컴파일completed 직후 연결이아직없을수있음→status ready를 확인하고 테스트 재실행. 표준화 후보로만 기록, 킷 수정 없음. host기본python3에는numpy없으므로 문서 지정 `/usr/bin/python3` 사용. 첫 측정 명령의실행불가를 성공으로세지 않음.

### 중단 사유

**사람이 정해야 하는 경우**: spec-reviewer 필수 C15 지적은 'Don't keep auto-tuning; the director should separate the measurement method from a real screen defect, decide the direction, then re-check against the same criterion.'다. 기준값.6~.8 변경/실패수용 없이 디렉터 판단이 먼저다. 다른필수수정은 한꺼번에 적용·관련검사·커밋 후 **partial**로 넘긴다. 이 열린 판단만을 이유로 재리뷰를 요청하지 않음. 디렉터가 방향을 정한 뒤 변경에 닿는 확인과 아직PASS아닌 algorithm-tester/spec-reviewer/validator-client만 재실행해야 한다. 기존FAIL두개/not-run하나를 유지한다.


## attempt 2 — 디렉터 D-L1~D-L4 적용, 마감 리뷰 요청

입력: 지시문 끝 D-L1~D-L4(2026-11-19 16:4x), `attempt2/directive.json`에 이번에 읽은 파일 해시. 기준 커밋d48e66b, 수정 확인의 기준은a0a8a21(첫 리뷰 뒤 d48e66b 수정도 아직 독립PASS 미수령). 디렉터 결정이 앞 partial의 중단 사유를 해소했으므로 재개했다. 사람에게 질문/추가 승인 요청0, 하위/리뷰 직접호출0.

### D-L1 — 같은 프레임 Vignette 효과 세기

C15를 `(모서리 ON/OFF) ÷ (가운데 ON/OFF)`로 잰다. 범위.6~.8·출발.7 그대로, 기준서3.9 잼 칸·3.12를 함께 갱신. 원 밝기비1.165는 화면 내용이 섞인 값이므로 과거 수치로 보존하며 효과 세기 PASS/FAIL로 더 쓰지 않는다. 측정은 기존 Tools/hd2d_main_gate.py의 `--vignette-pair`, 계획의같은옵션은8쌍/동일프레임메타데이터를 사전선언한다. 새 측정 스크립트 없음.

첫.18쌍은 동일해시였으며 **무효 진단**이다. 촬영기의 `Camera.UpdateVolumeStack()`는카메라별 stack을채우지만 전역stack을선택하지 않는다. URP의SingleCameraRequest는보통CameraStack렌더의전역선택을거치지 않아 같은프레임OFF값이렌더에안들었다. CaptureCamera에서배경요청뒤 `VolumeManager.instance.stack=data.volumeStack`을명시하고 finally전역stack/target/Canvas를복구한다. 각PNG옆*.settings.json에사용값기록. 이 수정뒤 .18은 ON=.18/OFF=0, 실제효과비.949028로 FAIL. 앞 Bloom동일쌍/오프라인가설도같은촬영결함영향을받아무효화하고유효한새쌍을다시찍었다. 게임/URP패키지코드를수정한것이아니다.

| 회차 | Vignette 세기/부드러움 | D-L1 C15 | G11 배경 | 판정 |
|---|---|---|---|---|
| 출발값 유효화 | .18/.2 | .949028 | 앞 유효한.18판.061024(이번튜닝판아님) | C15 FAIL |
| 후보1 | .5/.04 | .681207 | .126302 | G11 FAIL |
| 후보2/최종 | .5/.03 | .726230 | .094162 | 두 조건 PASS |

둥글게Off·중심(.5,.5)·검정은유지. 조명/색조정/그림자/흐림값은추가로바꾸지않음. 값튜닝은딱2후보, 기준완화/세번째값튜닝없음. 후보1/2의원본쌍·선언·finish를attempt2에보존.

최종8자리비는 main2/C15.json: .726230/.726848/.727541/.728218/.729099/.729738/.730234/.730674 **전부PASS**. 볼륨 .5/.03 일정. C15 ON/OFF는각자리에서Unityframe동일/timeScale0을assert하고Frame메타데이터도사전선언/해시고정. 측정기자체시험4/4 PASS(중립1·일괄노출상쇄1·모서리.7·검정기준오류).

직접본한계: 최종모서리끝에완전검정영역/급한경계가보인다. 수치PASS를그림채택PASS로승격하지않음. **G20 판독에이문제를명시**한다. 관련그림4화면비를직접열었다.

### D-L2 — 실제 화염구 심 프레임

실제 SpriteAtlas/ProjectileViewFactory/풀의 `fireball_right_1`을기존Fire재질로보이는자리에놓아정지wave6/frame60촬영. 원본그림에서흰심분류(hi≥.96,S<.12,alpha≥.5)19픽셀, 실제효과상자[1131,464,1173,500]. final-bloom-measure에서풀상자평균차0≤1LSB, **효과상자1픽셀·최대1LSB·평균.00022046LSB>0**. 문장수치로옮긴추정검사PASS다. 원본Burst/Shader/Material연결과actualcore2.4는bloom-final/core.json. Bloom문턱2/세기.1/퍼짐.25/Dual/Quarter·FireCore2.4모두유지(차0일때만올리는조건이성립하지않음). paid/질감/점광0.

bloom-final/main.png·bloom-off.png를직접열어흰심이보임확인. **육안으로번짐차를확실히구별하기어렵다**. 숫자1LSB PASS를강한Bloom효과가보인다고과장하지않는다. G20에서시각적충분성을읽는다. 앞core6/32가설/동일해시FAIL은역사적실패기록으로유지하되현재의유효쌍을대체하지못한다.

### D-L3 — 최종 렌더 소스의 평지 대조

flat-flow는B0기존자리(0,0),seed8,첫전투직전0속도,대원자리±.55/±.35를사용. 높이JSON만heightsMm전부0으로잠깐바꿔동기가져오기→Play→`k21-final/flat-B0.png`1920×1080. finally에서seed8원본바이트를복구/동기가져오기. hash양쪽 **ea871183c69ed43942710999d19526dcbe8efbe9fab8ede33ca30569fc273656** 일치, grid.IsFlat=true/partyHeight0실측. 커밋에평지자료차이0.

복구뒤같은최종볼륨에서T2/T7/Main1-hill을다시촬영(7/7 PASS29.3초),세장과B0직접열어봄. source-final.json에이전review1대비Assets렌더입력변경은촬영기(07:42:48)와볼륨(07:48:55)두개뿐이며둘다B0복구07:52:56전에바뀜. 이후는렌더링하지않는측정기만수정. 높이외실제게임코드·그림·모델·재질은같다. 전단계세그림은attempt2/k21-before보존. **두판독자T2≥2/평지보다높음은여전히NOT_RUN**, 디렉터가같은눈가림질문으로한다.

### D-L4 — 정해진 범위

공유BackgroundOutline은깊이불연속을고정갈색(52,20,14)·문턱.055·1저해상도텍셀로덮는다. 물/둑재질에는그테만약하게하는값이없다. 고정테아래물알베도를바꿔도선색은안바뀌며공유테색/문턱변경은나무/바위/천막/벽의K19도바꾼다. '안되면그대로둔다'에따라공유테/모델/질감을건드리지않고**강선유지(D-L4로정함)**. C9 .254902·C10 1.200726/.438986 PASS. **배낭/길명도는D-L4로수용**, 디렉터가완성계획7절에올린기존미커밋행을보존한다. 두항목을같은필수지적으로다시열지않도록리뷰브리핑에명시.

### 최종 main2·누락 복구·단계별 비용

새run **Main2-20261119T080016Z**, 선언hash **f1a080001d1012ed13c987cfe78f52247a020e2e2de8fa586e82e1197bb617f1**, source1424개. 원본main2는attempt2/main2-before, attempt1핸드오프도별도보존. 원본킷gate/config수정0/G19deferred그대로.

- 첫준비흐름에서Vector3의Newtonsoft순환참조를사전발견→소유Play를중단했고COLLECT가의도적으로guardFAIL(7/12)한로그를보존. 선언/새판완료로보고하지않음. ps/pgrep조회는환경권한이막아사용하지않았고다른프로세스/에디터를조작하지않음.
- x/y/z숫자로고친흐름은COLLECT까지**8/12 PASS**, main판/카메라이동8장/진단/closeup까지완료. 다음C15ScriptEval에서VContainer확장Resolve<T>임포트가없어컴파일실패. 게임컴파일결함이아닌지시문fixture컴파일결함이다.
- Resolve(typeof)명시해석으로고쳐**누락된C15쌍·Bloom·HP/강도·UI4화면비만복구**(paired-captures-play9/9 PASS61.1초). 본판수집/전체게임재실행0. sourcebefore/after1424동일. 이복구는별도Play이며모두동일seed/정지waveFrame60이지만G11본판과C15-on0의PNG해시는서로다르다. **각ON/OFF쌍안에서만같은Unity프레임**임을assert했다. 한장면비교기준을몰래바꾸거나G11자료를OFF로대체하지않음.

최종원본gate **12PASS/G03BLOCKED(review.json없음)/G19N/A/G20BLOCKED**, 전체FAIL/T4미완료유지. G09·G11·G12·G13·G18PASS. C3 ΔE7.4517/S.342105·C8−.270588·C9 .254902·C10 1.200726/.438986PASS. 마지막화면비4종/HP ONOFF/동일질감ONOFF/battle-main을직접열었다. Vignette정규화UV·HUD자리유지, 가장자리급경계는4종모두보임. 새battle-main1920×1080을최종판으로갱신, 이전것은attempt2/battle-before-attempt2.png.

흐림은값/셰이더변경0. 자연장면의행별추정경계가최종색조건에서 **.129167/.781944 FAIL**로나왔음을보존한다. 낮은질감대비때문에2LSB초과차가경계가까이서나타나지않는측정문제로분리. 같은실제카메라/FullScreenPass에고대비4출력px Point줄무늬를임시로화면전체에놓고ON/OFF추가측정(3/3 PASS23.4초), **같은측정기/같은차2LSB/같은.02행곡선기준/같은범위**에서경계v **.149537/.774537 PASS**. 새게임자산·후처리값·기대식수정0. 두calibrationPNG직접열어선명띠와밖의줄무늬변화를확인. 자연장면FAIL을삭제하지않고교정표본PASS와분리하여리뷰한다.

최종matchingtexture강도감소율 **아래.127441≥위.125689**, 실제poolHP v.08의88×34픽셀ON/OFF차0/선명(두장직접봄). EditorAmbient시험**10/10 PASS7.42초**, 측정기selftest**4/4PASS**. 새runtimeC#·Main·RP/Renderer변경0, 전체EditMode/전체판정재반복0. 마지막컴파일completed/errors0, 소유Play종료·console errors0. 성능/G19/유료/외부전송/push/PR/merge/직접리뷰호출0.

### 누락 원인·워크플로우 반영

1. UpdateVolumeStack이전역선택까지한다고가정해서동일프레임쌍을무효로만들었다. 로컬URP원본을읽어SingleCameraRequest와local/globalstack을구별, 촬영기finally복구+PNG별실제값에반영. '쌍효과0이면재질탓'가설은철회. **교훈반영됨**.
2. C15원밝기비는효과값이아니었음→D-L1이중비/노출상쇄시험/8쌍사전선언을기존도구에반영. 관문수치/킷은유지. **반영됨**.
3. 외부ScriptEval은소스의using/UnityVector3JSON처리를그대로쓰지못함→좌표스칼라·명시적DI해석을흐름에반영. 서로다른fixture오류와완료된본판을분리하여누락부분만복구. **반영됨**. 추가사전검사후보는임시flow의컴파일/직렬화확인, 대신뺄것은그오류때문에전체본판/전체플레이를다시찍기.
4. 자연질감으로작은흐림강도의경계위치를추정하면대비에따라경계가이동함→고대비진단도함께보고,자연FAIL유지. **이작업진단흐름에반영**,측정문턱삭제/완화없음.

시간·토큰은attempt2/time-and-tokens.json. 신뢰가능한시작07:38:52UTC, 앞문서읽기일부미계측. 가장비효율적인부분은같은프레임촬영결함진단과fixture구문오류복구였다. 전체게임검사반복으로메우지않음. Codex토큰API없음/외부리뷰토큰미제공, 직접하위/리뷰호출0. **현재needs-review**, 마감3종(algorithm-tester/spec-reviewer/validator-client)외부판정을기다린다. 첫FAIL2·NOTASSESSED1을자가PASS로바꾸지않음.

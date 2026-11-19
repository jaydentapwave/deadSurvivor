# Claude 마감 리뷰 요청 — 빛 L0~L6 (첫 회차)

구현자는 Codex. 사용자 지시로 이 세션은 리뷰 에이전트를 호출하지 않았다. Claude 리뷰 세션에서 **validator-client·spec-reviewer·algorithm-tester**를 실행한다. design-reviewer 사전 관문은 없다. 기준 커밋 **12e0567**, 대상은 이 문서와 L6 직렬화 기록을 포함한 마지막 `chore(light): record final checks and pending review` 커밋(같은 브랜치 `claude/pack-expedition-2d`). `git diff 12e0567..<해당 커밋>`을 사용하고 다른 세션의 미커밋 변경은 제외한다. 첫 리뷰이므로 carried_from/반려한 이전 지적/기수집 리뷰 지적 없음.

최종 핸드오프 `.director/outbox/20261118-hd2d-light-1.md`는 **리뷰 VERDICT 수령 뒤** 쓴다. 이 파일은 리뷰 입력 브리핑이다. 실제 판정의 `VERDICT:`를 원문 그대로 관문 evidence에 옮기고, 미수령은 blocked, NOT ASSESSED는 not-run으로 기록한다. PASS로 꾸미지 않는다.

## 입력과 소유 범위

- 지시문: `.director/runs/20261118-hd2d-light/steps/1-Deadsurvivor.md` 전체, 특히 L0~L6·추가 L1b/A~C·측정·반복상한.
- 기준: `docs/art/아트-기준서.md` r8.1 K10/K18/K21/K23~26, 2.1⑤·3.3 27행·3.9 C3/C8/C9/C10/C15/C16·3.12·5절7/8번.
- 전체 근거: `docs/features/hd2d-light/{design-brief,verification}.md`. 이전 `docs/features/hd2d-screen/design-brief.md`, `docs/features/hd2d-background/prep.md`, `.director/outbox/20261118-hd2d-background-1.md`, `.director/outbox/20261118-hd2d-terrain-1.md`, `.director/evidence/20261118-hd2d-3d/3/phase6-followups.md`.
- 사용자 최신 추가가 우선: r7 실제 메시 그림자(풀/꽃Off), r8.1 높이·좌표를 바꾸지 않는 비탈 명암, grass 코드 생성기의 무늬 수정 허용. 구 3.12의 판 그림자/작은 돌Off 문구로 회귀시키지 않는다.
- 이 단계에서 안 보는 것: **7단계 성능·G19**, UI 설계/새 그림·원본 모델/질감 재생성, 노치 실기기·발 반폭 문구·물 위 바닥 그림 인상(지형 후속). 성능 로그는 열지 않는다. G20/K21 독립 눈가림은 디렉터 몫, 이 리뷰의 자가 판독으로 대체하지 않는다.

## 역할별 파일·수용 기준

**validator-client** — L0/L1/L1b/L2/L3/L4/L5/L6, art-core2/6/8, D-T9.

- `Assets/Game/02.Scripts/Effect/Ambient/{AmbientEffectsPresenter,AmbientMotionMath}.cs`, `App/GameSceneLifetimeScope.cs`, `App/HD2DLightCapture.cs`(`#if UNITY_EDITOR`), `Editor/HD2DMainPlateCapture.cs`.
- `Assets/Game/Tests/EditMode/AmbientMotionMathTests.cs`, `Assets/Game/03.Resources/Prefab/HD2DAmbientDust.prefab`, `Materials/HD2D/HD2D_AmbientDust.mat`, `Shaders/HD2D/HD2D_AmbientDust.shader`.
- `Shaders/HD2D/HD2D_{FieldSurface,BandBlur,GroundShadow,ContactShadow,HardShadowCard}.shader`, `Settings/HD2D_BattleVolume.asset`, `Materials/HD2D/Scenery/` tint/flag·`Textures/HD2D/FieldGrassOnly.png`(실제 경로는 git diff 확인; native 풀 생성물).
- `Tools/hd2d_main_gate.py`, `Tools/playable/hd2d-light-flows.json`. 기존 `Tools/playable.py`는 바꾸지 않음.
- 확인 내용: ResourceService/PoolManager/DI/상태전환/Dispose·씬 파괴 순서·입자cap36/rate1.4·반투명/WorldIndicators/UI층·순간 점광 없음·height/게임좌표/OFF경로유지·최종ON 원본gate와 소스지문 일치.

**spec-reviewer** — L0~L6/L1b, K21/K24/K25/K26, C3/C8/C9/C10/C15/C16, G03/G09/G11/G12/G13/G18, 넘김7/8.

- 위 기준/verification 및 `docs/game/hd2d-완성-실행계획.md`6절, `docs/game/hd2d-작업-개선-기록.md`.
- 실제 수치/실패/미확인을 완료로 올리지 않았는지, 범위 밖 변경·문턱 완화·선언 뒤 판 교체 없는지 확인. G09/G11 숫자 PASS를 T4/전체 아트 완료로 오해하지 않는다.

**algorithm-tester** — L1b/L5의 반복·분기 순수계산, K21/K24 및 먼지36/1.4.

- `AmbientMotionMath.cs`·`AmbientMotionMathTests.cs`: 4Hz 0/1/2/1, 경계/장시간/잘못된 입력, 실제 ParticleSystem cap/rate 시험 범위.
- `HD2D_FieldSurface.shader`: 물 UV native 1/48m 계단·깃발0/±native칸·높이 전용 normalXZ4배 3단문턱.
- `Tools/hd2d_field_textures.py`, 기존 `Tools/test_hd2d_field_textures.py`: 큰 얼룩만 제거, 풀잎 native4색/마스크·재현성. `L4-native-blades.json` 이전/현재 차는 픽셀별 RGB동일±7 이하(명암만), 기존시험2/2 PASS.

## 최종 증거 / 실제 실패

- `.director/evidence/20261118-hd2d-light/1/` source-recheck.json(1424 mismatch0), viewed-captures.json, time-and-tokens.json.
- 전체 EditMode `output/hd2d-light/L6-editmode.json` **622/622 PASS37.88초**, 전체판정 `L6-full-play/report.json` **8/8 PASS154.8초** 각1회. 이후 코드 변경 없이 volume.26→.18만 바뀌어 전체 반복 안 함. L5 지정 새시험9/9 PASS. 최종엔 isPlaying=false.
- 최종 **Main2-20261119T064236Z**, `output/hd2d-관문/main2/{manifest,gate-report,gate-summary,source-before,capture-record}.json/md`, 판 id는 **Main1**. 실제 파일은 manifest.files 경로 그대로. 선언 hash `59c776c00ef31c55682f53033693e1648677827f7d219d97c2006cada53ba2dc`.
- 효과 ON G09 tree soft.052963/stone.093168·3단 PASS, G11 hero.002962/bg.061024 PASS. 12PASS/G03BLOCKED(review 미수령)/G19N/A/G20BLOCKED. 전체 **FAIL** 유지. 첫 실패판은 `output/hd2d-light/main2-attempt1/` 보존, 선언 뒤 상자를 수정하지 않음.
- **C15 FAIL**: .23→.26 조정도 .8378→.8151,2회 상한. 필수 G11 별도 재확인 .18에서 최종 본판1.1659, 이동1.1657~1.1819, 시작 진단.87725. 볼륨은8장.18/.2 일정; 내용 비의 변화를 코드 출렁임이라 하지 않음. 목표.6~.8 유지.
- **Bloom FAIL[추정]**: 실제 explosion_3 동일멈춤 frame60, 최종 `L6-effects/{main,bloom-off}.png` 동일해시/최대차0. core6/32/캐시 갱신 시도도0. 원인미확인, 임시재질 변경 철회. 디렉터가 실제 화면 결함을 판독하기 전 추정 검사만으로 새 그림 수정 반복 안 함.
- 흐림 경계 최종 v.148611/.778241 PASS±.02(`L6-effects-measure/measure.json`), 앞 .26의 위경계FAIL을 갱신. 아래≥위 대응강도 실측은 미완료. HP막대 아래 띠 직접 판독 미확인; 숫자6개는 선명. 렌더러 layer26 이벤트후/UI Overlay 구조는 확인됨.
- C3 ΔE7.4517/S.3421, C8−.2706,C9 .2549,C10 1.2007/.4390 PASS.C16은 WARN만.
- 최종 K21 `output/hd2d-light/k21-final/{T2,T7,Main1-hill}.png`, L5연속8장 `L5/{motion-sheet,detail-sheet}.png`, 실제4화면비 `L6-recheck-play/09-ASPECTS-*.png` 직접 열람. G20/K21은 디렉터 판독 요청. 최종 B0 대조판은 이 실행에서 안 만들었으며 과거 B0는 소스가 다름.
- 최종 전투 `output/hd2d-light/battle-main.png`1920×1080, HUD포함은4화면비자료. 5단계 battle-main은 다른3D/지형 소스라 빛만 비교한 동일조건 자료로 인용하지 않음.
- 강 경계 갈색 한 줄/배낭과 길 명도 지적 미해결, 새 원본 아트 수정 안 함. 둘째 천막 그늘 면·돌담 사선 무늬 인상은 독립 판독/원본 담당 전달 그대로.

## 기준 문서와 다른 점 / 이미 결정된 것

1. 최신 사용자 추가 L1b/grass코드 생성기 허용·r7 메시 그림자 정책이 구 지시보다 우선. 해·높이·배치·좌표·평지 OFF경로는 유지.
2. Bloom 원본2/.1, 구 screen 설계1.8/.4로 되돌리지 않음. 값이 안맞으면 기준을 낮추지 않음.
3. 고정 wave6/frame60 fixture는 낮은 적 배수·표적 보강을 사용하며 전체 승패 시험과 분리. Main2는 같은 정상카메라위치에서 물리적 가림 없는 슬라임/버섯을 분리한 시각 판. 실제 플레이 성공은 별도 full-play로 입증.
4. 첫 Main2 G11/G12 실패 보존 뒤 새 선언. G12의 피격흰색/가림을 상자 변경으로 회피하지 않음. 최종ON 판만 최종 G09/G11 근거.
5. C15와Bloom은 되풀이 상한/추정수치 정책으로 열린 채 리뷰 요청. 흐림 세부 미확인도 숨기지 않음. 이것을 수용/유예 승인으로 해석하지 않음.
6.38개재질 새 셰이더 기본값 자동 직렬화는 `L6-serialized-defaults.json`로 authored 변경0 증명; 소스지문 포함/커밋. 다른세션 dirty파일은 제외. 유료생성·성능·push·리뷰호출0.

리뷰 뒤 FAIL만 수정 확인 요청한다. 여기서 스스로 적은 관측은 독립 VERDICT가 아니다.

# 현재 수정 확인 — validator-client만

수정 기준 **deac03331e5ba80d38250e7b9330279744326932**, 대상은 이번 metadata 수정 커밋(최종 응답/핸드오프 state.commit). **algorithm-tester·spec-reviewer는 VERDICT: PASS 유지, 다시 부르지 않는다. validator-client의 VERDICT: FAIL 한 건만 수정 확인**한다. 틀린 지적으로 반려한 항목 없음.

필수 수정 V-META: `Assets/Game/02.Scripts/Editor/HD2DMainPlateCapture.cs`의Vignette복구finally에서복구된카메라stack을즉시갱신·전역선택한뒤요약settings.json을쓴다. 기존렌더전stack선택코드는private메서드로추출해그대로재사용. C15함수·그림렌더전처리·자료/셰이더/Volume값·runtime게임코드수정0.

증거: `output/hd2d-light/metadata-fix/{compile.json,play/report.json,assertions.json}`. 실제8쌍 `pairs/0~7/`에서요약.5=ON.5=live.5,OFF0,부드러움.03일치. 지정4/4흐름47.8초/8assertPASS. ON/OFF첫쌍직접열람. 기존attempt2의요약0은결함이력으로보존,새수정결과는metadata-fix/pairs를본다. 원본main2와C15계산은deac033증거로유지하며현재커밋새gate로재실행했다고하지않는다.

수용 기준: validator-client **V-META**(원445행 요약설정복구순서). 기준문서와다른점없음. 이미정해진D-L1~4유지. **이번판정밖**: PASS받은알고리즘/기획의재검, G20/K21·검정모서리/미세Bloom품질(디렉터), 자연장면추정FAIL/교정PASS분리, 지시문미추적/Font한계, G19성능(7단계). 후속열린지적만으로재리뷰를늘리지않는다. 핸드오프review_open에최신원문을기록한다.

---
아래는 deac033에서 이미 끝난 attempt2의 첫 마감 리뷰 브리핑이다. 현재 재호출 목록이 아니다.

# attempt 2 마감 리뷰 요청 — D-L1~D-L4

Claude 리뷰 세션에서 **algorithm-tester·spec-reviewer·validator-client**를 실행한다. 구현자는Codex이며직접리뷰호출0. 이전판정algorithm-tester/spec-reviewer `VERDICT: FAIL`, validator-client `VERDICT: NOT ASSESSED`(출력강제종료로미수령). **PASS인리뷰0**, 세역할만다시받는다. 기준범위12e0567→이번마감커밋, 수정확인diff **a0a8a21→이번마감커밋**(d48e66b의깃발수정도아직독립PASS를못받음). 정확한대상해시는최종응답/핸드오프state.commit. 반려한틀린지적없음.

입력: `.director/runs/20261118-hd2d-light/steps/1-Deadsurvivor.md`의끝**D-L1~D-L4**가우선. `docs/art/아트-기준서.md` K21/K24/K25/K26·2.1⑤·3.9·3.12, `docs/features/hd2d-light/verification.md`맨끝attempt2. 이전실패/오판/로그도그문서에남겼다. 현재숫자PASS를전체아트/T4완료로읽지않는다.

## 역할과 수용 기준

- **algorithm-tester**: ALG1/L5 깃발normal/Fade `_FlagWind=1`(d48e66b), `Assets/Game/Tests/EditMode/AmbientMotionMathTests.cs`10개, 실제`review1/flag-sheet.png`/states.csv3자세. 새**D-L1/C15** `Tools/hd2d_main_gate.py`의이중비/노출상쇄/0기준거부/8쌍프레임assert/추가파일사전선언, selftest4/4. 기존물·비탈계산은첫리뷰의케이스PASS와소스동일, 소품`_Diagnostic`3단은비탈계산전return임을구별. game현재`Effect/Ambient/{AmbientMotionMath,AmbientEffectsPresenter}.cs`유지.
- **spec-reviewer**: SPEC1~4·D-L1~4·L3/L5/L6, K21/K25/K26, C3/C8/C9/C10/C15/C16/WARN, G03/G09/G11/G12/G13/G18, 넘김7/8. `verification.md`최신표/`아트-기준서.md`와실제증거대조. C15범위그대로이며**잼칸만디렉터가정정**. 재고40/3, L3수용과재고분리. B0동일렌더소스·seed8높이복구, D-L4수용을지적재개로바꾸지않음.
- **validator-client**: 첫NOTASSESSED이므로실제구현도확인. `Effect/Ambient/`·`App/GameSceneLifetimeScope.cs`·`App/HD2DLightCapture.cs`(Editor전용)·Assets/Game/03.Resources/Resources/Prefab/HD2DAmbientDust.prefab·Materials/HD2D/HD2D_AmbientDust.mat·HD2D_AmbientDust.shader·pool/phase/Dispose. 이번변경`Editor/HD2DMainPlateCapture.cs`의SingleCameraRequest **local→global VolumeStack선택/finally복구**와Vignette동일프레임OFF/설정기록, `Settings/HD2D_BattleVolume.asset` .5/.03. HP/WorldIndicators/UI효과밖. `HD2D_FieldSurface.shader`등기존런타임계산은이번에수정0. 전체622/8앞실행·이번지정10개·이번렌더흐름증거구별.

## 최신 실행 증거

`output/hd2d-관문/main2/` 새run **Main2-20261119T080016Z**, plan hash **f1a080001d1012ed13c987cfe78f52247a020e2e2de8fa586e82e1197bb617f1**, source1424 before/after일치. 판id Main1. manifest/gate-report/gate-summary/C15.json의실제파일경로를사용. **12PASS/G03BLOCKED/G19N/A/G20BLOCKED**, 전체FAIL/T4미완료. G09·G11최종ON만근거, OFF진단으로대체안함.

- D-L1 후보1 .5/.04→C15.681207PASS/G11.126302FAIL, 후보2 **.5/.03→C15.726230/G11.094162둘다PASS**. 최종8자리 C15 .726230~.730674, 각쌍Unityframe같고timeScale0. 문턱.6~.8/.10유지.
- D-L2 `attempt2/bloom-final/` 실제fireball_right_1 흰심프레임/원본FireCore2.4. 같은프레임ONOFF의풀평균차0/효과상자1픽셀최대1LSB>0. **매우약한차이며육안번짐구분어려움**, G20판독대기. 현재차0조건이아니라HDR심을더올리지않음. 앞동일해시쌍은stack선택결함으로무효, 임시6/32원복상태유지.
- `attempt2/final-tests.json`10/10 PASS7.42초(nativeParticleSystem cap36/rate1.4/수명18·실제두깃발재질). `measure-selftest.log`4/4PASS. runtime게임코드/장면/RP/Renderer수정0라전체게임/전체Editor재반복0.
- `attempt2/blur-final/`: 동일Point질감ONOFF강도아래.127441≥위.125689, 실제poolHP v.08에서ONOFF영역차0·직접열람. 자연장면행경계 **.129167/.781944 FAIL보존**. 추가고대비Point진단`attempt2/band-calibration/`(임시quad,자산저장0)에서**같은기존측정기/노이즈기준/행문턱**으로 .149537/.774537 PASS. 값/기대식수정0, 자연대비에따른위치추정문제와분리해검토.
- D-L3 `output/hd2d-light/k21-final/{flat-B0,T2,T7,Main1-hill}.png`. 높이원본복구SHA256 ea871183c69ed43942710999d19526dcbe8efbe9fab8ede33ca30569fc273656. source-final.json에Assets렌더입력변경두개가B0전에완료된시각/해시기록, 이후측정도구만추가수정(렌더에관여안함). B0/T2등직접열람. 독립K21점수NOT_RUN.
- 최종UI4화면비 `attempt2/paired-captures-play/09-ASPECTS-*.png`, `battle-main.png`1920×1080직접열람. **모서리완전검정영역/급한경계가보임**. 숫자통과로채택판독을대신하지않는다.

## 기준과 다른 점·이미 결정된 것·이전 열린 지적

1. **D-L1**은화면내용이섞인원비대신효과ON/OFF이중비로재는디렉터결정이다. 범위/관문문턱은그대로. Vignette두후보만튜닝. CaptureStack선택결함은Editor촬영기에서고쳤고게임URP나킷을고치지않음.
2. **D-L2**실제효과심이보이는유효쌍에서>0이므로Core2.4유지. 더뚜렷한번짐이필요한지는G20디렉터판독. 추정검사PASS를시각채택으로승격하지않음.
3. **D-L3**동일렌더소스B0자료추가/높이복구완료. 같은기획의눈가림질문·점수판정은디렉터몫. **D-L4로정함**: 강경계는공유테라물/둑값만으로격리조정불가하여유지, 배낭/길명도수용(디렉터가사용자확인목록에올림). 새원본그림/모델을요구하는지적으로다시열지않는다.
4. 최종본판COLLECT는한번완료. 이어진C15 ScriptEval구문(Resolve<T>using없음)실패는명시적타입해석으로고쳐**누락쌍만별도Play복구**했다. 같은seed/정지waveFrame60이지만G11본판PNG와C15-on0은동일해시가아니다. 각ON/OFF쌍내부동일Unityframeassert, 사전선언/1424소스불변 유지. 실패를삭제/새성공으로덮지않음.
5. 첫리뷰의열린지적원문은핸드오프review_open에보존·최신처리는본문구별. G03/G20 BLOCKED와전체FAIL유지. 다른세션DamageNumber SDF.asset 미커밋차이보존, 실제소스지문포함이지만커밋단독완전재현으로주장안함. 지시문미추적한계는이번directive.json해시부터확인가능.
6. **이단계판정밖**: G19/성능(7단계,로그열지않음), 원본아트/모델재생성, UI디자인새작업, 노치실기기/발반폭문구/물위바닥그림인상. G20/K21은디렉터가Tools/hd2d_judge.py와같은눈가림질문으로실행, 리뷰에이전트가대체안함. 유료생성·점광·push/PR/merge0.

직접본목록/시간/소스지문/실패구분은attempt2/{viewed-captures,source-final,time-and-tokens}.json과verification.md. 판정은각역할의실제VERDICT로반환한다. 현재세션이자가PASS를적지않았다.

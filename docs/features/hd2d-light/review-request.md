# attempt 4 마감 리뷰 — D-L8

기준 **0a2841e**, 대상은 핸드오프 state.commit/최종 JSON. **validator-client·spec-reviewer만** 요청한다. algorithm-tester의 D-L6 순수 쌓기 계산 VERDICT: PASS(job20261119-184711-a288ca)는 attempt3에서 이어 쓴다. 이번에는 해당 C#·계산을 바꾸지 않았다. 직접 리뷰 호출0, 틀린 지적으로 반려한 것 없음.

입력: `.director/runs/20261118-hd2d-light/steps/1-Deadsurvivor.md` 끝 **D-L8**. 사용자 결정으로 기존 D-L6의 진형 간격 동결을 해제했다. K21 r003 **PASS**(Codex T2 3>평지1, Claude2>1), 비탈/빛/화면 효과를 손대지 않았다. 이전 G20 r003은 Codex readability2 한 칸 FAIL이며 새 판독은 디렉터가 한다.

## 파일·역할·수용 기준

- **validator-client**: `Assets/Game/03.Resources/Resources/Data/Formation/{balanced,wedge,line}.json`, `Data/Feedback/feedback_rules.json`. 기존 `FormationService.GetSlot`, `DamageNumberPresenter.GetStartPosition`의 연결과 실제 영향 확인. 생성자·DI·상태·정리·C# 변경0. 세 진형 슬롯에 동일1.5배, 모양/순서/대원 ID 유지. 숫자 offsetPx5→80, 수40·수명.65·font8·상승12·D-L6 쌓기 불변. 원래 전체 판정 경로8개 기대식과 튜토리얼65f2860의 regular 출발 계약 유지.
- **spec-reviewer**: 위 데이터와 `Tools/hd2d_main_gate.py`의 feedback 옵션·선언 추가, `verification.md` 끝 attempt4·기준서 D-L8 주석·아래 증거. **D8-FORMATION, D8-NUMBER, D8-GAME, D8-GATE** 대조. '거의 겹치지 않음'을 임의 임계값으로 바꾸지 않고 실제 전후 수치와 그림을 제공했다.

## 증거와 한계

`output/hd2d-light/attempt4/`:

- 같은 seed8/wave6 frame60 before/main.png → final-feedback/main.png를 직접 열었다. 캐릭터 원본 alpha≥128의 불투명 경계를 GPU로 읽고 실제 body Transform/flip/pivot/카메라로1920×1080에 투영. `Tools/hd2d_main_gate.py feedback`이 네 상자의 모든 쌍 겹침 합÷네 몸 상자 면적 합을 계산한다. 글자는 활성 digit의 상자를 단어별로 합친다. 상체는 상자의 위50%라는 측정 정의다.
- **몸 비율 .071732843→.004198260(7.1733%→.4198%)**, 최댓쌍14.8936%→2.1277%. 공격 자세 상자 끝부분 약2px 접촉은 남고 몸4명은 구분된다. D-L8.json의 clearBodies=false는 **엄밀한0%가 아니라는 진단**, 사용자 조건에0% 문턱을 새로 넣거나 완화한 것이 아니다.
- 숫자 offset72 후보는 상체121.548px²가 남아 FAIL. 80으로 올린 최종 **몸/상체 겹침0/0px²**(앞2461.265/1400.412). 표시수·수명·폰트는 불변. 숫자 배치의 최종 취향/가독성은 G20 디렉터 판독.
- compile.json: up_to_date, 오류0. FormationServiceTests5/5·DamageNumberMotionTests8/8·PartyMoveServiceTests9/9 **총22/22**. selftest는 D-L8 반례3/3와 기존7/7.
- full-play/report.json **전체8/8 PASS148.6초**. full-route.json은 원본 tools/playable/route.json의 **metrics 출력 경로만** 바꿨고 기대식·8단계·4화면비 유지. wave-targets.log **42행 FAIL0/TARGETS OK**; M2/M3 정상·M5 진 웨이브3(2~5). M7은 수집끔/미측정, 성능판정으로 쓰지 않는다.
- 최종 main2 **Main2-20261119T101437Z**, source1451/files1519. 자동12PASS/G03·G20 BLOCKED/G19 N/A, 전체 아트/T4 미완료. D-L8.json의 전후/state/image를 **사전 선언**했으며 이후 역할·상자·필수 파일 변경0. G09 .050569/.107034, G11 .005731/.087343 PASS. 판독·문턱·킷 변경0.
- battle-main.png와 final-play/11-ASPECTS-* 네 장 직접 열람. K21·빛·모델·그림·UI를 새로 고치거나 K21을 다시 판독하지 않았다.
- 다른 세션의 튜토리얼·Scope·Font·DitherTent/Wood 미커밋은 고치지도 stage하지도 않는다. 수집 당시 Scope snapshot은 실제 source SHA와 일치한다. 현재 트리의 미커밋 소스를 포함한 증거이며 **커밋 단독 완전 재현으로 주장하지 않는다**. 그 뒤 변화가 생기면 캡처 시점과 분리한다.

## 이미 결정됨·판정 밖

D-L1~7의 값/메시/후처리/그림자/쌓기 유지, D-L4 배낭·길 명도 수용. **풀 잔무늬·흙길 경계·그림자 띠·다리 그림자·이동 중 도트 흔들림은 D-L8 판정 밖**, 고치지 않고 후속 목록에 남긴다. G19/성능은7단계, 로그 열람0. push/PR/merge·외부 전송·유료 생성0. 이전 열린 지적은 attempt3 원문 기록과 핸드오프에 보존하되 **K21 미실행 부분은 r003 PASS로 해소**됐음을 구별한다. 새 G20 PASS나 전체 아트 완료를 구현자가 대신 쓰지 않는다.

수집 후 외부 변경: Field/Presenter/WorldPresenter.cs, Field/View/FieldView.cs, Shaders/HD2D/HD2D_FieldSurface.shader 세 파일이 다른 세션에서 바뀌었다. **D8는 이 파일들을 수정/stage하지 않는다.** attempt4/source-drift.json과 capture 소스 사본(원 수집 SHA 일치)을 제공한다. 수집 당시12PASS/1451 전후 일치와 현재 전체 소스 통합 미확인을 구별한다. 현재 파일로 기존 판을 완전히 재현한다고 주장하지 않는다. 이 외부 변경 때문에 K21/빛 수정이나 이번 범위 밖 재구현을 열지 않는다.


## attempt 4 마감 리뷰 수령

대상 `e313696`(기준 `0a2841e`)의 validator-client **VERDICT: PASS**(job `20261119-192903-986920`, 98초), spec-reviewer **VERDICT: PASS**(job `20261119-192902-3b256a`, 78초)를 수령했다. 필수 수정 없음. 두 리뷰가 전후 캡처와 기록을 확인했고 validator-client는 측정 함수를 제공 state로 실행해 몸 겹침 7.1733%→0.4198%, 숫자–몸/상체 0px²를 재현했다. Unity·시험·관문 재실행은 하지 않았다. 이번 마감은 문서만 변경하며 새 리뷰를 요청하지 않는다.

기존 algorithm-tester **VERDICT: PASS**는 attempt 3에서 이어 쓴다(D-L6 순수 계산 불변). K21 r003 PASS는 디렉터 판독으로 유지한다. D8 구현/마감 리뷰 완료와 전체 아트/T4 완료는 구별한다. 새 G20 가독성 채택·G03/G20 BLOCKED, 자연 장면 흐림 추정 FAIL/교정 PASS, 미세 Bloom, 외부 세션 변경 후 현재 소스 통합 미확인은 그대로 열려 있다. 몸 상자의 잔여 약2px 접촉과 `clearBodies=false`도 삭제하지 않았다.

근거: `output/hd2d-light/attempt4/review-closure.json`, 핸드오프 attempt 4. 시간은 위 두 리뷰 실행 시간이며 병렬 합계/현재 세션 전체 시간으로 해석하지 않는다. 토큰은 제공 API가 없어 NOT_AVAILABLE. 직접 리뷰 호출·게임 변경·추가 Play·push·PR·merge 없음.

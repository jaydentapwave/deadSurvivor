# attempt 3 마감 리뷰 요청 — D-L5~D-L7

기준 커밋 **65f2860**(튜토리얼 세션이 별도로 커밋한 현재 부모), 대상은 핸드오프 `state.commit`과 최종 JSON에 적힌 구현 커밋이다. 시작 기준은 015308f였다. **이번 변경 파일만** 본다. 튜토리얼·App scope·Font·DitherTent/Wood의 다른 세션 변경은 고치거나 스테이징하지 않았다. 커밋 단독 완전 재현으로 주장하지 않는다.

입력 원본: `.director/runs/20261118-hd2d-light/steps/1-Deadsurvivor.md` 맨 끝 **D-L5~D-L7**, `docs/art/아트-기준서.md` 3.12·K21·C3/C15, `verification.md` 맨 끝 attempt3. 지시문은 Git 미추적이며 현재 해시는 attempt3/directive.json에 남긴다. 기준 문턱·킷·관문 상자·기존 효과 기대식은 바꾸지 않았다. 틀린 지적으로 반려한 항목 없음.

## 리뷰 역할과 수용 기준

- **validator-client — D-L6만**: `Assets/Game/02.Scripts/Features/Feedback/{Factory/DamageNumberViewFactory,Presenter/DamageNumberPresenter,Registry/DamageNumberViewRegistry,Utils/DamageNumberMotion}.cs`, `Features/Combat/View/BurstView.cs`, 해당 두 EditMode 시험. 같은 MonsterId 숫자만 최신 원점을 공유하고 오래된 숫자는 글자 높이+2px 간격으로 위에, 좌우 ±3px. 기존 수·수명·글꼴·합치기·반환·phase 정리 유지. 검/도끼 베기 body만 Upright의 local Z +.6m(카메라 안쪽), root/판정/진형/알파 불변. Initialize와 다른 효과 Update에서 위치 초기화. DI·구독·상태 소유 구조 변경 없음.
- **algorithm-tester — D-L6 쌓기 계산만**: `DamageNumberMotion.GetStackOffsetPx/GetStackOrigin`·`DamageNumberMotionTests.cs`. 같은/다른/미할당 대상, 이동하는 대상, 같은 프레임·서로 다른 나이, 기존 숫자 제거·새 등록·풀 재사용, 오름 간격과 기존 Merge/수명/상한 보존을 독립 실행한다. 숫자가 나이 순으로 등록되는 Presenter 경로도 확인. D-L5 측정 함수의 재리뷰는 이번 요청 범위가 아니다.
- **spec-reviewer — D-L5/D-L7 값·측정·D-L6 요구 보존**: Volume 세기 .52/부드러움 .04/rounded Off/색 .425, 노출0. FieldSurface의 높이 켠 경로: 법선 XZ×4와 **걷는 땅 기준 높이 i.href**로 넓은4단(.03/.16/.30m, .94/1.02/1.13/1.25); worn/soil 혼합 .18/.45. 실제 높이·이동·평지 OFF·질감 원본 불변. `Tools/hd2d_main_gate.py`의 D-L5 옵션은 동일 80×80 모서리, 노출 상쇄한 ON/OFF 방사32구간, 이전 판과 같은 방법으로 비교한다. G03 사전 선언 뒤 역할·상자·요구 파일 변경 없음.

## 실행 근거

`output/hd2d-light/attempt3/`:

- compile-d6.json, compile-final.json: 컴파일 오류0. DamageNumberMotionTests **8/8**, CombatEffectTests **38/38**. measurement-selftest.log **7/7**(기존 C15 4개 + 부드러운/no-black 통과·검정 급경계 거부·방사 노출 상쇄).
- fx-diagnostic-play/report.json **8/8**(96초). 원본 art-insert-flows의 FX1/FX2·FXSEQ 검/도끼·전투 동시 표시 기대식 그대로다. 두 베기 21장씩 모든5프레임 관측. unrelated 로비4화면비 캡처를 제외한 범위 흐름이며 원본 전체 경로 PASS가 아니다. 첫 fx-play는 장비 지급 단계 인덱스 오류로 FAIL, 소스 수정 없는 범위 재실행에서는 재현 안 됨. 원인 미확정으로 보존.
- 최종 final-play/report.json **10/10**, main2 새run **Main2-20261119T093359Z**, source **1447** 수집 전후 일치. manifest 파일 **1510**. `output/hd2d-관문/main2/{manifest,gate-report,C15,D-L5,C10}.json`이 최종 근거다.
- 자동 **12 PASS / G03 BLOCKED / G19 N/A / G20 BLOCKED**, 전체 FAIL·T4 미완료 유지. G09 tree .050569/stone .107034·진단3단, G11 hero .002962/background .087343. G12/G13/G18 PASS.
- C15 **.757792~.759539**, 모서리 최저 밝기 **18.9158>0**, 검정 **0%**, 방사 최대 인접 차 **≤.173687 < 이전 .652067**. 8쌍마다 같은 Unity frame/timeScale0, 실제 floor·rounded·노출도 frame-pair.json에 기록. C15는 이 파일의 쌍만 사용하며 final-measure의 원 화면 밝기 비와 혼동하지 않는다.
- C3 **ΔE8.5681 / S.34722**, C8 **−.24314**, C9 **.25490**, C10 **1.27540/.46629**. C9는 기존 `L6-colour-regions.json`의 실제 강 [900,190,1000,215] 상자다. finish의 오래된 고정 water telemetry [700,150,850,250]는 다리라 C9 근거로 쓰지 않는다. C16은 제외/WARN 규칙 유지.
- 최종 `k21-final2/{T2,T7,Main1-hill,flat-B0}.png` 네 장, 높이 복구 SHA **ea871183c69ed43942710999d19526dcbe8efbe9fab8ede33ca30569fc273656**. 최종 볼륨·턱 윗면 수정 뒤 네 장을 다시 찍었다. K21 점수는 디렉터가 낸다.
- 전투 battle-main.png와 UI4화면비 final-play/10-ASPECTS-*.png, FX 연속 시트·K21 네 장 직접 열람. 모서리에 순검정 잘림은 없지만 옅은 호형 전이는 보인다. G20 채택은 수치로 대신하지 않는다.

## 기준과 다른 점·이미 결정된 것·열린 지적

D-L1~4 결정 유지: C15 같은 프레임 이중비, Bloom 심2.4와 미세 차는 이전 판독 항목, B0 임시 평지 후 원본 복구, 강선은 원본 그림 수정 없이 유지·배낭/길 명도는 수용. 진형 간격·나무 각진 수관·UI·노치·발 반폭 문구·물 위 바닥 인상은 이번 범위 밖. 성능/G19는 7단계이며 로그 열람0.

D-L5 넓은 .8/.6/rounded On +노출1 방식은 C3/C15 통과에도 G08/G09/G11 실패로 폐기했다. 두 번째 방식은 중앙 영향을 줄이고 검정 floor만 없애 문턱을 그대로 통과했다. D-L7 첫 식이 장식 턱의 +.36m까지 높이로 읽어 C10 .95966 FAIL → 실제 걷는 땅 기준 높이로 고쳐1.27540. 실패 판은 attempt3/run1-*·failed-*·c10-failed-*에 보존했다.

이전 spec-reviewer/validator-client/algorithm-tester의 열린 지적 원문은 핸드오프 review_open에 유지한다. G20/K21의 이전 판독은 **FAIL**, 새 최종 판은 **NOT_RUN**이며 전체 아트/T4를 완료로 바꾸지 않는다. 자연장면 흐림 추정 FAIL과 교정 PASS의 분리도 삭제하지 않는다. 이 열린 사항만으로 이번 범위 밖 재구현/리뷰를 늘리지 않는다.

구현자는 직접 리뷰를 호출하지 않았다. 앞 회차 세 리뷰의 PASS는 역사적 판정으로만 보존하며 이번에 닿은 D-L5~7을 대신 통과시키지 않는다.

D-L7의 3~4단 결정이 원 L1b 2~3단보다 우선한다. 소품 G09 진단 2~3단과 구별한다. 마지막 OFF 보호에서 worn/soil 감소를 높이 ON에만 제한했다. ON 본판 PNG는 직전 통과 판과 바이트 동일, 평지 B0는 새 소스로 재촬영했다.

마감 후 **다른 세션의 소스 변경 1건**: App/GameSceneLifetimeScope.cs에 FirstExpeditionDialoguePresenter 등록이 추가돼 현재 해시가 수집 때와 다르다. `attempt3/source-drift.json`에 기록했다. 그 한 줄만 제외한 읽기 전용 snapshot `GameSceneLifetimeScope.capture.cs`가 **원래 수집 SHA256과 정확히 일치**함을 확인했다(실제 Assets 파일은 수정0). 수집 전후1447개 일치·당시12PASS 기록과 **현재 전체 소스 재현 미확인**을 구별한다. 선언/manifest를 새 파일로 재결속하지 않았다. 이 후속 튜토리얼 통합은 이번 D-L6 리뷰 범위 밖이며, 현재 G03/전체아트/T4 완료로 보고하지 않는다.

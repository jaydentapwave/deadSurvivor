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

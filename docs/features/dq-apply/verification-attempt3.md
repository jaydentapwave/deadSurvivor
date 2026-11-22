# attempt3 검증 기록 (승인 기준 아님)

## 옮긴 정사영 식 단언

| 시험 | 전 | 후 | 이유 |
|---|---|---|---|
| MonsterSpawnTests.SpawnDistanceByVisibleArea | 거리=옛 대칭반크기 대각선+70 | 기존후보거리이상·들판안·실제투영발밖(경계제약은명시) | 후보를원근프러스텀밖으로밀라는디렉터결정2. margin70값/분포/수/HP단언유지 |
| MonsterRecycleTests.FarBehindMovedAfterHold | 옛상자x/y변중하나일치·각축상자이하 | 실제투영발밖또는경계제약·들판안·거리>기존대각선 | recycle도같은원근계산. HoldSeconds/MaxPerCheck/공격delay/앞쪽적불변단언유지 |

새계산시험은3화면비×4중심/경계×8방향×최대shake양끝에서들판안/방향보존/원시발·몸밖또는먼경계제약을검사한다. 실제소비처반전pivot과terrain재교차/허용량도시험한다. 런타임결과는실행후추가하며단위시험PASS를실제프레임PASS로승격하지않는다.

## 마감 FAIL 수정 대응 (독립 수정 확인 전)

| 지적 | 구현 대응 | 검증 |
|---|---|---|
| spec1 기준/결과 혼합 | 승인8a56e7b 문서를원복하고허용된디렉터결정2의4.3절및그계약을가리키는5절두행만개정. 이전14절검증기록은verification-attempt2.md로분리 | 승인diff대조. 신규시도결과는본문서에만기록 |
| spec2/client1/algorithm A05 | 같은선택방향의실제원근발/보수적몸상자+기존margin+최대shake를검사해첫들판경계까지밀기. Spawn/Recycle같은ResolvePlacement. 난수·주기·수·타입·HP데이터불변 | 3화면비×4중심경계×8방향×4shake순수계산,실제서비스60표본. 기존raw카운터·방향·기대식그대로,경계제약bool추가기록 |
| spec3 | SnapCard 지형로컬접면재교차4회+실제height재샘플. 이동한계2/48m초과면원발위치유지. Member/Monster 실제flipX pivot전달 | 실제MonsterView소비처반전pivot,평지/비탈grounded·bounded. near/behind/far는그림보정없이복귀 |
| spec4 | Editor update에서새Time.frameCount마다1장,8장수집 | r3-motion.json의실제frame/time,기존CLI왕복8표본과구분 |
| spec5/6 | 일반/near가림,사망/부활/풀반환/disable복구,피격몸·HP·숫자같은frame묶음 | r3-lifecycle-frames.json, r3-motion.json. 마지막near코드는그대로포함한최종runtime원문지문/Play |
| client2 | SpriteLit 원clip XY/W유지하고biasedZ/biasedW의NDC깊이만옮김 | Main 실제GPU검사 bias0/.2209 x800px가까운표지bbox와픽셀수동일 |
| client3 | Projectile/EnemyShot Upright를18도카메라Up방향.5m로복원,root/그림자바닥유지 | 이행으로만프리팹수정,실제플레이투사체/프리팹위치확인 |
| client4 | 전역SaveAssets제거,대상만SaveAssetIfDirty. Scene저장의참조dirty자산자동저장을막고메모리내용/dirtyflag보존 | prefabs·saved 두지점에실제변경+예외주입,11파일및meta bytes/GUID·renderer참조·열린scene복구. 다른wood재질dirty sentinel내용/flag/파일hash유지,그후2회이행동일 |

기준서r2는바꾸지않았다. 뷰/스폰계산의틀을바꾸는것은이번inbox결정2의허용범위다. standaloneTestRunContext도실제PerspectiveFrame을설정해옛스폰공식을선택하는설정은남기지않았다. 논리ViewHalfExtent는이동한계와기존recycle선정규칙보존용이며최종스폰/재배치거리를보장하는식으로더이상쓰지않는다.

## 놓친 것·워크플로우 반영

- float 화면좌표의1mm차를빼는새입력시험에서소거오차로방향오차가.001을넘었다. 평면의직선은원근투영에서도직선이므로측정구간을10cm로잡고같은오차문턱을유지했다. 시험/기준을느슨하게하지않음.
- 임시검증스크립트가private PlayableProbe.Resolve를public으로가정해컴파일실패. 실제Container.Resolve(Type)로호출. 프로덕션API를검증편의를위해넓히지않았다.
- SaveScene이비대상dirty참조를저장하는문제는첫실패주입이검출했다. 명시적대상저장만으로충분하다고가정하지않고외부dirty내용/flag/hash보존을실제로추가검사했다. 소스단위rollback과다른사람dirty보존은함께시험한다.
- 연속프레임은CLI요청8회가아닌게임frame번호로수집하고모든증거에frame/time을결속한다. 위두항목은이작업의검증절차에반영완료,공용규칙파일은지정범위밖이라변경0. 새유료/패키지/성능측정0.

## 최종 명령·프레임 증거

- `recompile-r3-status.json`: completed/errors0. `editmode-r3-final.json`: DQScreen119/119 PASS,skip0. 마지막추가시험은현재화면비의실제이동한계를데이터에서읽으며119개를다시확인했다.
- 최종runtime전체 `r3-playable-main-final/report.json`:8/8 PASS,141.2초. `r3-playable-terrain-final/report.json`:33/34,N9_RESULT FAIL. 기대식·baseline변경0(`r3-expectations-unchanged.json`). 런타임C#/shader의수집전후지문 drift0는`runtime-r3-final-source-{before,after}.json`.
- 전용N9발 북동서남35/0/1/0,몸35/4/3/0. 화면안42건모두boundaryConstrained=true,최소발y=.6262779(먼쪽). 경계제약없는화면안0. `r3-final-boundary-exception-report.json`에원시행을보존했고기존FAIL을PASS로고치지않음.
- native GameView1920×1080,2340×1080,2048×1536을실제로설정한최대shake4방향×중심/경계5의실제서비스60표본,6000관측. 경계제약없는가시출현0,경계제약인데가까운쪽(y≤.5)0,먼경계가시1270. `r3-placement-matrix*.json`, `r3-placement-*.csv`, `r3-recycle-*.csv`. 이fixture는일시정지중실제서비스를직접진행한배치시험이며자연게임1판은위기본경로가별도증명한다.
- 움직임8장은frame6587~6594 인접프레임. 첫행IsFlashOn1,숫자1,몸/HP좌표동시기록(`r3-motion.json`). 가림도3558~3565 인접실제프레임:alive→down→revived→monster-pool-return→disabled→reacquired→near-crossing→near-restored(`r3-lifecycle-frames.json`). disabled에서창0/정상재질ID복원,재취득시대원창복원. 앞의일시정지동일framefixture와구분한다.
- 세화면비의경계는각각정상입력으로실제한계까지도달후캡처:9.842309m,7.65480947m,13.31453m. `r3-capture-session.json`각이벤트에위치/한계기록. 캡처전native화면비와World의투영snapshot을갱신했다.
- Main실제GPU표지56칸111px(약112px/10.4%목표). bias0와.2209에서 x1666~1776,y454~564,픽셀수12321동일(`r3-gpu-probe.json`),clipXY/W가보존됨. 실제대원새56칸아트는8단계몫이며바꿨다고주장하지않는다.
- 새소스의출현연출도`r3-appearance-observed.json`실측110update,elapsed0→.300487548,shader0→1. 실시간첫행~끝행약.2797초. 게임판정변화0.
- `migration-r3-check.json`:prefabs/saved두실패주입,target/meta bytes및GUID·scene복구·외부dirty값/flag/hash보존PASS. 성공후연속2회hash drift0. Finalruntime변경은이행코드/저장자산에닿지않는잘못된계산입력가드였고이행결과는동일하다.
- `r3-opened-images.json`은실제로본경로·sha·관찰·원본/시트구분. D1과Main시작을직접열어대조. 원본레퍼런스이미지는여전히작업트리부재/허용범위밖이라NOT_RUN. SourceD1/E1색·새그림과현재Main배경색차이는6~8단계몫이다.
- 기존Main숫자관문은이전OPEN-01 BLOCKED검사부적합유지. 이번에PASS로승격하지않고9단계연결담당에인계한다. 성능측정0.
- 캡처보조스크립트설정리팩터링과기존프로세스로드가겹쳐1회fixture설정파일부재로실패했다. 최종경로PASS는그대로보존하고캡처만새프로세스로재개,최종capture/extra완료. 이전실패로그도보존했다. 런타임게임실패와구분한다.

이결과는구현자명령/판독이며독립수정확인PASS가아니다. FAIL3종1회수정확인후같은지적이남으면inbox지시대로더반복하지않고질문/핸드오프로넘긴다.

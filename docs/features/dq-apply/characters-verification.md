# DQ 적용 8단계 — r3 필수 지적 수정·확인 기록

현재 상태: 구현자 검증 통과, **독립 수정 확인 대기**. 최종 핸드오프는 리뷰 판정 수신 후 `.director/outbox/20261123-night-1to6-8-r2.md`에 쓴다. 승인 기준은 `characters-brief.md`의 df10136 원본으로 고정했고, 구현 결과·승인된 Bruno12장 예외는 `characters-changes.md`로 분리했다. 아래 r2 기록은 a843198 당시의 이력이다.

## 받은 리뷰와 수정 대응

- spec-reviewer, job20261123-043521-cb3dc8: `VERDICT: FAIL`. C8-01/02/03/06/07 met, C8-04/05/08 partial. 승인 브리핑 변경, 소비/합성/정리 사유 기록과 실제 렌더·몬스터 presenter 수명 시험 누락을 필수 수정으로 수용했다.
- validator-client, job20261123-043512-5a8ac8: `VERDICT: FAIL`. 실제 WorldPresenter→MemberView→카메라 경계 및 공격별 정리 사유 증거 누락을 수용했다. 이전 리뷰가 이미지를 열지 않아 발·크기·그림자 정렬은 독립 시각 PASS가 아니었다는 한계도 보존한다.
- algorithm-tester, job20261123-043517-920fe5: `VERDICT: FAIL`. 기존40케이스36PASS/4FAIL 기록은 그대로 두고, 2배속 phase 시계·동일 캐릭터 새 판 idle·경계 직전 조기 전환을 수정했다. 그 리뷰의 실제 Unity/카메라/atlas 미실행 한계는 새 성공으로 바꿔 적지 않는다.
- design-reviewer: 이전 job20261123-034539-34753d의 `VERDICT: PASS`를 이어 받음. 승인 기준 파일은 더 이상 실행 상태 문서로 사용하지 않는다. 이번 자체 검사를 독립 리뷰 PASS로 적지 않는다.

| 필수 지적 | 수정 | 실행 근거 |
|---|---|---|
| 승인 기준 변경 | brief를 df10136 바이트로 복구·고정, 예외/결과 별도 변경 기록 | r3/approved-brief-check.json byteEqual=true, SHA256 dfdc5298db594629852f548e7923c8ae5a844d5ac3a5a44b5147997a9133a669 |
| batch 합성/정리 내역 | DQHitBuffer가 공격 id·Step·방향·batch·적용 id·rendered/coalesced 또는 정리 사유를 각각 emit. WorldPresenter가 최근512행 ring에 보존, PlayableProbe.DQHitAudit로 노출 | r3/hit-audit.csv: 실제 id2/Step3/right가 id3/Step4/left에 합성됨; 둘 다 같은batch/적용id3로 기록 |
| 사망/비활성/부활/웨이브/새 판/Dispose 구분 | suppressed-by-death, inactive, revive, wave-start, run-start, dispose를 삭제 전에 공격별 기록. 사유 없는 Clear 금지, Dispose 중복 호출과 이후 LateTick/렌더/이벤트 차단 | 실제 CombatService.ResetRun/PrepareWave/ReviveMember 및 WorldPresenter 경유 시험; r3/hit-audit.csv |
| 실제 렌더 경계 | 실제 Main scope의 두 무기·큰 Step 피해/동일 Step 새 준비, 첫 Main 렌더의 타격 장, 다른 방향 다중 Step 합성, 중복 callback, 미렌더 보존 | r3/boundary-status-scene-render.json 1/1 PASS, render-boundaries.txt의9개 경계 그룹 |
| SceneView/추가 카메라 | 실제 URP endCameraRendering을 관찰. Main 비활성 동안 SceneView4회/추가 카메라4회 렌더에도 pending3 보존. Main을 켠 첫 렌더에서 실제 hit id1/eventFrame2 표시·소비 | r3/render-boundaries.txt, hit-audit.csv. SceneView cameraType도 확인. 실제 렌더 소비는 버퍼 직접 호출로 대체하지 않음 |
| 몬스터 presenter clear/재시작/중복 반환 | 실제 모델 사망→registry 분리→deathList, 산 view와 detached view의 clear, 두 번 LateTick, 두 view 동시 재사용의 객체 구별, 재시작과 Dispose | 동일 Main UnityTest: registry0/deathList0, 같은 pool 객체 중복 대여0, 중복 반환 예외0 |
| 몬스터 배속 | Windup/Dash/Recover/Fuse는 모델 countdown의 경과 시간을 view로 전달. view가 phase 진입/최종 장을 소유하며, 렌더 delta 적분으로 phase를 추정하지 않음. 접촉/발사·사망 표시 delta도 배속 반영 | 실제 RunService.AdvanceFrame(1/60), 속도2: 23걸음 boar clock0.7666669/frame5; 35걸음 puffer clock1.166667/frame6; pause 뒤 불변 |
| 새 판 idle | run-start/revive에 view 시각 상태 초기화, 새 판 첫 LateTick에서 이전 idle delta를 더하지 않음. wave-start는 타격 기록만 정리해 이미 쓰러진 몸의 fall을 다시 시작하지 않음 | 같은 Bruno idle0.55초/frame6 → 실제 RunService 새 판 → idle frame0 |
| 경계 직전 조기 전환 | FrameAt의 +0.001ms 비교 제거, 경계 전/후 float 시험 추가 | t=.0999995→0, .1000005→1, .2999995→1, .3000005→2; r3/dq-tests-status.json |

## r3 실행 관문과 확인 범위

근거 루트 `output/hd2d-dq/apply/8/r3/`. 수정 기준 a843198. 최종 소스 지문과 명령 결과 사본은 `.director/evidence/20261123-night-1to6/8/3/`에 동결한다.

| 관문 | 결과 | 범위 |
|---|---|---|
| project-doctor | PASS | configured/findings0, 설정 확인만 |
| 최종 소스 재컴파일 | PASS | compile-final.json errors0/warnings0 |
| DQ EditMode | PASS | dq-tests-status.json 27/27, 프레임 경계·공격별 audit 및 기존 DQ 데이터/방향/실제 임포트·풀 재사용 |
| Main 경계 통합 | PASS | boundary-status-scene-render.json UnityTest1/1, 실제 SceneView·추가 카메라·Main 렌더와 실제 서비스/presenter를 통한9개 경계 그룹. 통제 fixture의 모델 쓰기는 시험에만 있음 |
| 최종 에디터 | PASS | editor-final.json 6000.4.3f1/playing=false/dirty=false; console-final.json groundTruth 오류0·경고0 |
| r2 시트/clip/atlas 및 캡처 | 이어 받음 | unchanged-visual-assets.json: a843198 이후 해당 자산 diff0. 몸 픽셀·피벗·임포트·카메라/UI 배치 변경0. 기존 직접 본 캡처 경로/SHA는 r2/opened-images-r2.json |
| 기본 판정 경로/메뉴2회·복구 | r2 근거 유지 | a843198의8/8, 반복/복구 차이0. 이번 수정은 요청된 경계만 재실행했으며 전체 경로·전체 EditMode를 중복 실행하지 않음. 현재 소스로 전체 경로를 새로 돌렸다고 주장하지 않음 |
| 마감 수정 확인3종 | NOT_RUN | spec-reviewer·validator-client·algorithm-tester에 재요청. 세 리뷰 모두 앞 판정 FAIL이므로 재검토 대상. design-reviewer만 PASS 이어 받음 |

SceneView와 추가 카메라의 실제 렌더 보존을 확인했으며, Main의 같은 프레임 중복 콜백도 소비 수를 늘리지 않았다. 큰 Step에서 두 무기 PendingAction을 실제 가방 배치와 맞춰 만들었고 타격 기록은 한 공격 id 한 건, 다음 공격 id의 준비가 시작돼도 이전 실제 타격 장이 첫 Main 렌더에 남았다. CSV는 통합 시험 전체19행으로 ring 한도에 도달하지 않았다. 일반 게임에서 audit는 최근512행이므로 전체 판의 영구 전수 로그라고 주장하지 않는다.

## r3 놓친 점·시험 수정 이력

- 이번 필수 지적은 이전 자체 확인이 승인된 렌더/수명 반례까지 다루지 못한 데서 발생했다. 버퍼 단위 시험과 일반 전투 표본을 실제 camera boundary 시험으로 대신할 수 없었다. 승인 기준을 실행 기록으로 사용한 것도 원인이었다. 후보: 승인 파일 고정 해시와 실제 카메라/서비스 경계 fixture를 유지.
- 통합 fixture 초기4회는 EnterPlayMode 도메인 재로드가 compiler display class의 캡처 지역 변수를 복원하지 못해 bootstrap 뒤 NullReference로 실패했다. 컨테이너 캐시/속성 우회로 해결되지 않아 방식을 바꿔 Play 전환 이후에 별도 코루틴에서 캡처 변수를 생성했다. 이는 게임 코드 결함이나 실제 렌더 PASS로 기록하지 않았다.
- 다음 fixture 실행은 모든 경계 assert를 지났으나, 두 번째 무기를 Equip만 해 가방 slot이 없다는 로그로 전체 FAIL이었다. LogAssert로 허용하지 않고 실제 BagService.AddLoot/Drop으로 두 번째 무기 슬롯을 만들어 수정했다. 이후 통합 시험 PASS, SceneView 실렌더 보강 후 최종 PASS. 각 초기 실패의 status 파일도 r3에 보존한다.
- 실제 게임의 공격 주기/피해/판정 기대식/phase countdown은 변경0. 뷰의 phase 시간 입력과 상태 정리만 수정했다. 자산/임포트/배경/빛/후처리/shader/UI 수정0. 이전부터 남은 에디터 자동 직렬화와 RoundDQ2/3 미추적 meta는 계속 stage 제외.
- 시간·토큰: 04:35 리뷰 피드백 후 수정 활동은 r3 파일 시각·manifest에 기록. 정확한 Codex 토큰은 NOT_AVAILABLE, 하위 에이전트0. 앞 session_time 집계의 Claude jsonl 없음 한계 유지. 워크플로우 최종 반영은 독립 수정 확인 뒤 핸드오프 때 표시한다.

## review_open — 그대로 보존

| 종류 | 낸 리뷰 | 내용 |
|---|---|---|
| 범위 밖 | spec-reviewer·validator-client | 기존 전체 EditMode 실패5건, 원근 관문 BLOCKED·terrain N9 FAIL 보존. 이번 단계 수정 대상 아님 |
| 확인 한계 | validator-client | 앞 리뷰는 이미지를 열지 않았고 발·크기·그림자 정렬을 독립 시각 PASS로 판정하지 않음. 수정 확인에서 r2 실제 확대 시트를 직접 열도록 요청 |
| 확인 한계 | algorithm-tester | 앞 리뷰는 실제 Unity 시험·카메라 렌더·atlas packing 미실행. 이번 구현자 실행으로 그 리뷰 당시 한계를 소급 삭제하지 않음 |

배경·빛은6·7단계 몫, 효과·투사체·예고 라인 그림의 화면 연결·UI 맞춤·원근 관문은9단계 몫, 이 단계 판정 밖. 원본 그림 품질·성능도 이번 판정 밖. 기존 열린 지적을 고치거나 판정 문턱을 낮추지 않았다.

---

# r2 구현·검증 이력 (a843198, 아래 상태는 당시 기록)


상태: 마감 독립 리뷰 대기. 자체 확인을 독립 리뷰 PASS로 쓰지 않는다. 이 문서는 리뷰 입력이며 최종 핸드오프는 판정 수신 뒤 `.director/outbox/20261123-night-1to6-8-r2.md`에 쓴다. 이전 PARTIAL 기록은 `.director/outbox/20261123-night-1to6-8.md`에 남아 있다.

## 정정과 실행 대상

04:00 디렉터가 확인한 사실에 따라 이전의 “Unity 약관 미동의가 night-chars 명령을 막음”은 철회한다. 약관 창은 프로젝트 없는6000.6.7f1, 이 프로젝트6000.4.3f1의 차단은 Main 저장 확인이었다. 약관에 동의하지 않았다. 재개는 `python3 -B tools/editor_ready.py "$PWD"`만 사용했다. 다른 에디터/작업 트리 조작0. 메뉴가 Main 저장을 마치며 unrelated dirty scene은 거부한다. Play/시험 전 dirty=false를 확인했다.

실행 트리 `night-chars`, 브랜치 `claude/pack-expedition-2d-dq-chars`. 설계 기준 커밋 `1c780d0`, 설계 수정 `df10136`, 구현 초안 `d80bf94`. 최신 명령·캡처는 `output/hd2d-dq/apply/8/r2/`(이하 r2). 소스 지문/커밋은 r2 증거 manifest와 Git 기록 참조.

## 구현

- `Tools/dq_character_pack.py`: 최종 confirm 표에 선택된 9종1,406장의 원본 RGBA만9종 시트에 보존한다. 시트/클립 JSON/manifest를 재생성한다. 12개 예고 라인 r2 실사용 tile과 assembly.json만 따로 반입하며 화면 연결은9단계다. 미리보기·GIF·폐기 클립 반입0.
- `MainSceneDQCharactersMigration.Apply`, 메뉴 `DeadSurvivor/DQ/Apply characters`: PPU48·Point·무압축·발 피벗 multiple sprite,9종별 atlas. 첫 실행의 NPOT 축소 높이를 참조하던 절단 좌표 문제를 원본 텍스처 높이로 수정했다. 반복 실행과 예외 복구를 바이트 단위로 비교했다.
- 기존 `SpriteAtlasService`, `MemberView`, `MonsterView`, `WorldPresenter`를 확장한다. 정지/초상 키도 새 DQ 몸으로 해소한다. legacy clip API는 과거 자료의 호환 경로로 남으며 본 게임 DQ 몸에서는 사용하지 않는다.
- `DQClipData`, `DQMotionLibrary`: 프레임 키·장별 시간·반복·타격 장·방향/명시 대체·MonsterPhase 대응은 데이터 소유. `CharacterData.Motion`·게임 공격 주기·HitSeconds·세이브·피해 규칙과 판정 경로 기대식은 변경0.
- 실제 타격 값 기록→LateTick 우선 적용→대상 게임 카메라 렌더 후 소비. 종료 동시 통과/같은 걸음 재시작/여러 걸음은 최신 실제 타격 장을 한 번 보존한다. 비활성·쓰러짐·재시작/부활은 기록을 정리한다.
- 대원 walk24/idle12/attack7/fall+lying. 피격은 해당 장 고정+기존 흰 flash/1px 몸 오프셋. Result에서 마지막 대원이 쓰러진 뒤 fall을 마치게 하는 표시 시계만 추가했다. Initialize 재사용 시 피격 장/시계도 초기화한다.
- 몬스터 phase 진입 시계와 최종 몸 선택은 view 한 곳, 기존 예고 상태 전달은 유지. 실제 접촉/발사 통지를 받아 contact/shoot 타격 장으로 시작한다. 죽은 모델은 원래 시점에 제거하고 표시만 registry에서 분리, 마지막 장 렌더 뒤 풀 반환한다. 재사용 등장 효과도 초기화한다.
- 실제 view 캡처 observer는 기존 PlayableProbe의 partial로 분리했다. production sprite·frame·발·재질·fallback과 실제 EndOfFrame PNG를 읽는다. 게임 상태를 바꿔 장면을 연출하지 않는다. 캡처 진행 길이를 unscaledTime 종료에서 고정 렌더 tick 수로 수정했다. 그림/클립 코드는 동일하며 natural/loss 기록은 수정 전 길이 계산, quiet 기록은 수정 뒤다(`r2/character-observer-before-length-fix.cs`).

## 명령 관문

| 관문 | 결과 | 근거 |
|---|---|---|
| project-doctor | PASS | 앞 실행 configured/findings0; 설정 확인만 |
| 최종 소스 컴파일 | PASS | r2/compile-final.json errors0/warnings0 |
| DQ 데이터/방향/시간/타격 경계/실제 서비스 | PASS | r2/dq-final-status.json 22/22; 새 3개 실제 Unity 통합 시험 포함 |
| 1,406장 실제 임포트·PPU/피벗/종당1texture | PASS | DQCharacterIntegrationTests, r2/coverage-check.json 원본 전수 RGBA 일치 |
| 메뉴2회 동일·주입 실패 복구 | PASS | r2/migration-final-check.json repeatedDrift=[], failureRollbackDrift=[], dirty=false |
| 전체 EditMode 확장 확인 | FAIL | r2/editmode-final-status.json 685개/680PASS/5FAIL. 아래 기존·범위 밖 항목을 그대로 보존 |
| 기본 tools/playable.py 1회 | PASS | r2/playable-final.log, r2/playable-main/report.json 8/8단계139.8초, 기대식 변경0 |
| 실제 캐릭터/몬스터 연속 관찰 | PASS(구현자 관찰) | r2/coverage-check.json 3,546행/인접 다른 장1,287쌍, 예전 몸0, PPU48전수, 상태 fallback0 |
| 실제 공격과 렌더 타격 장 | PASS(관찰 표본) | r2/hit-render-check.json 256/256 같은 Unity frame, bounded512행의 첫 표본. 전체 타격의 전수 로그라고 주장하지 않음 |
| 로비·HUD 정지 초상 구그림 검사 | PASS(관찰) | r2/no-old-ui-sprites.json old actor sprite 목록[], 새 dq_* 키. 그림이 없는 비활성 자리까지 포함한 Image 검사 |
| 16:9·19.5:9·4:3 | 캡처·직접 관찰 | r2/aspects/{1920x1080,2340x1080,1440x1080}.png 실제 GameView 해상도 변경 뒤 캡처 |
| design-reviewer | PASS carried_from | 사용자 전달 job20261123-034539-34753d, evidence `VERDICT: PASS`. 설계 수정 확인만 |
| spec-reviewer·validator-client·algorithm-tester | NOT_RUN | Codex가 부르지 않고 이번 마감 요청에서 Claude 리뷰로 넘김 |

전체 EditMode의 열린 실패는 숨기거나 기대식을 수정하지 않았다. 아래 파일은 기준 `1c780d0`와 동일함을 `r2/preexisting-test-evidence.json`으로 확인했다. 이전 버전 전체 시험을 실행했다는 뜻은 아니다.

| 실패 | 원인/범위 |
|---|---|
| FieldLayoutTests.BakeCopiesSelectedSeedBytesAndRepeatsExactly | output/map-pcg/seed-8/export/field_layout.json 없음. 배경 자료/6단계 몫, 이 단계 판정 밖 |
| FieldModelImportTests.ImportRepeatsWithoutChangingGuidsOrSourceNormalsAndWiresAllSixteenPrefabs | output/assets/hd2d-3d/tree_v1/model.fbx 없음. 배경 소품/6단계 몫, 이 단계 판정 밖 |
| MonsterBehaviorTests.B5OutwardDashClipsFinalPositionWithoutChangingHitsForFourLegallyPlacedMembers | 기존 시험 슬롯 상한102, 기존 배치153. 관련 시험·run_rules 변경0. 배치/5단계에서 이어진 항목, 이 단계 판정 밖 |
| MonsterViewStateTests.PrefabBodyIsChildWithPivotSortPoint | 기존 시험30°, 기존 prefab18° 차이12°. 18° 채택과 기존 시험 불일치. 시험/prefab 수정0,5단계 몫, 이 단계 판정 밖 |
| TerrainDataContractTests.TV1ActualHeightArrayFitsConfiguredDepthPad | expected>=1.42467654 / actual0.907692015. 기존 terrain N9와 함께6단계 몫, 이 단계 판정 밖 |

## 본 화면과 한계

연속 기록은 `sequence-natural`(정규 전투, 승리 wave10), `sequence-line-loss`(기존 횡대형 입력과 자연 피해로 전원 쓰러짐, 패배 wave4), `sequence-feet-up`, `sequence-idle-up` 등이다. 조용한 관찰은 기존 RunSettings enemyScale0 입력만 사용했다. 이름에 down이 들어도 실제 메타데이터가 idle/다른 방향이면 walk 증거로 세지 않았다.

`inspection-sheets/index.json`의90장은 정확한4배 nearest 자르기이며 재축소 없이 sheet.py로 배치했다. 4명×walk/idle/attack 각2방향과 fall,5종 이동·고유행동·fall의 인접 frame이 다름을 메타데이터와 화면에서 확인했다. body 크기·반전·발 기준은 프레임 전환으로 튀지 않았다. 후열 대원 발은 기존 균형 진형에서 앞열에 가려져 횡대형 `hero-foot-line.png`로 보완했다. 걷기는 기존 실제 이동 거리 시계에 연결돼 정지 시 걷기 장이 멈춘다. 자연 전투의 spawn 초기/나무 뒤 캡처를 몸 노출 증거로 쓰지 않고 `monster-visible-pairs.png`의 후속 노출 장으로 보완했다.

직접 연 전체 화면·21개 기본 시트·보완 시트와 각 입력의 경로/SHA256/관찰은 `r2/opened-images-r2.json`에 적는다. 모든 원본 캡처 수천 장을 직접 열었다고 주장하지 않는다. 채택 시안 `dirs-scene/D1-battle-crop-x4.png`도 열어 몸의 모양·규모를 비교했다. 배경/빛은 현재 옛 화면 그대로이며6·7단계 몫이다. 시트의 빨간 UNVERIFIED는 런타임 캡처에 아트 생성 compare 기록이 없다는 sheet.py 표시다. 원본 그림 품질의 채택을 새로 판정하거나 그 표시를 지우지 않았다.

4:3 준비 UI 일부가 대원 가장자리와 겹친다. UI 배치/크기·예고 라인 새 그림의 화면 연결은9단계 몫, 이 단계 판정 밖. 긴 그림자/기존 색과 나무 가림/먼 배경도6·7단계 몫이다. 성능 계측0, 유료 생성0, 패키지 설치0.

## 기준 문서와 다른 점·이미 결정된 것

- 걷기24/대기12/공격7, 조각 회전 방식: 사용자 확정. 기준서6~8장 Aether와 다름.
- Bruno fall_right12장+lying: 최신 confirm표와 디렉터04:00 확정. 다른 대원10장+lying. 이전 “디렉터 확인 필요”는 해소됐다.
- Lena attack_upright는 fix2/right, attack_front는 fix1/down, up는 fix1/up. rock contact_right는 구멍 있는 r2 대신 confirm 지정 원본. boar fall_right는 모든 방향에 오른쪽 원본+반전. 정확한 클립/선택/명시 대체/약함은 source-manifest와 클립JSON에 보존한다. 명시 대체는 예상 밖 runtime fallback과 구별하며 fallback0을 속이기 위한 누락 무시가 아니다.
- r2 slime/mushroom/rock JSON 장별 시간과 원본 차이는 pack-report.json differences에 보존했다. 판독표 선택을 변경하지 않았다.
- 피격 그림 새로 생성0. 기존 UI 유지. 배경 소품/나무 수·빛·성능의 디렉터 결정은 해당 단계에서 이어 받는다.

## 놓친 점과 워크플로우 갱신 후보

- 첫 임포트의 축소 texture 높이 가정이 slice를 바꿨다. 원본 importer 크기로 고쳤고, 전수 실제 피벗/texture 검사가 결함을 잡았다. 반복 메뉴 검사만 했으면 두 번째 실행 이후만 안정돼 첫 실행 결함을 놓칠 수 있었다. 후보: 첫 임포트 직후 원본 크기·피벗 확인을 유지.
- 에디터 이름만 보고 약관 창의 프로세스를 잘못 연결했다. 디렉터 정정 수용. 후보: editor_ready 대상 path/version과 active scene dirty를 명령 직전 확인, 이행 메뉴 자체 저장.
- 한 걸음에서 공격 종료/새 시작이 실제 타격 장을 가리는 계약 누락은 설계 DR-01에서 발견됐다. 렌더까지 snapshot 유지와 경계 시험으로 수정, 설계 수정 확인 `VERDICT: PASS`.
- spawn/가림 첫 두 장만 고르면 애니메이션 확인이 불가능했다. 후보: frame index 차이와 함께 실제 몸 노출을 직접 확인하고 뒤 장/다른 방향으로 보완. 동일 품질 그림 재생성은 하지 않음.
- 전체 EditMode 확장은 배경 산출물 의존 실패를5건 드러냈다. 이후 중복 전체 실행은 하지 않고 수정한 경계/DQ 시험을 실행했다. 검사 삭제/기준 완화0. 워크플로우 문서의 최종 반영은 독립 리뷰 판정 후 핸드오프 시 표시한다.

시간 기록: 재개 기준04:00 KST. `Tools/Harness/session_time.py --markdown`은 현재 worktree에 Claude jsonl이 없어 ValueError로 집계 불가였다. Codex/Claude 토큰 수는 노출되지 않아 NOT_AVAILABLE로 기록하며0으로 쓰지 않는다. 하위 에이전트 호출0. 활동별 실제 시각은 r2 명령 결과/파일 시각과 최종 evidence manifest에 보존한다.


## 커밋 경계와 에디터 자동 직렬화

캐릭터는 기존 바인딩을 확장해 연결하므로 Main/프리팹의 authored 값 변경이 필요 없다. `SaveOpenScenes()`로 현재 Main을 저장하는 과정에서 Unity가 기존 Sun에 URP 기본 보조 컴포넌트를 직렬화하고 일부 구형 RectTransform을 정규화했다. 에디터에서 font dirty flag와 배경 두 dither 재질 기본값 추가도 관찰됐다. 이 변화는 캐릭터 구현 커밋에 포함하지 않는다. `r2/editor-serialization-excluded.diff`로 남기며 조명·배경·shader를 코드로 수정하거나 다른 세션 작업으로 복사하지 않았다. 원래 있던 RoundDQ2/3 미추적 meta도 stage하지 않는다. 현재 디스크에 남은 이러한 diff를 “깨끗한 작업 트리”로 보고하지 않는다. 9단계 합칠 때 캐릭터 메뉴만 재실행하면 된다.

최종 명령 뒤 `console-final.json` 오류0/경고0, `editor-final.json` playing=false/dirty=false/6000.4.3f1. `pack-deterministic-final.json` 재생성 바이트 차이0. 직접 본34개 전체 화면·시트와 시트를 통해 본142개 고유4배 자르기는 `opened-images-r2.json`에 절대 경로/SHA256/관찰로 기록했다. 일부 첫 장의 가림을 후속 장으로 보완한 사실도 함께 적었다. 증거 사본은 `.director/evidence/20261123-night-1to6/8/2/`에 동결한다.

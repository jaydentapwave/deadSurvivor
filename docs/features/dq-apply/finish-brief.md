# 9단계 효과·UI·마감 — 실행 단위

기준 커밋 `2dd4a63fa887cb9f1a6fc282af4ac3d10e476501`. 새 화면의 효과/예고/UI 연결은 L1이다. 게임 규칙·판정 경로·데이터 수치·세이브를 바꾸지 않는다.

## 먼저 만드는 단위

멧돼지 r2 12타일을 이행 메뉴로 4단계 원화에 조립해 실사용 Resources 자산에 굽는다. 원본 assembly의34×209, origin17/192, end17/17, radius15와 채움 단계260/240/200/100ms를 따른다. 캐시가 기존 ground warning의 화면래스터(깊이축1/2) 계약에 맞게 재래스터화하고, 길이는 dashSpeed×dashSeconds, 폭은 dashHitRadius×2, 시간은 실제 windupSeconds로 정한다. 32방향과 경계 사이 방향도 검사하며 실제 고정 돌진 각도로 래스터화하고 floor-world capsule 밖 픽셀을 만들지 않는다. 코드 임의회전·소수배율로 타일판을 늘리지 않는다. MonsterView의 기존 평지/ConformedDecal 표시를 이어 쓰며 모델/피해/phase는 바꾸지 않는다. 데이터/자산이 없는 구 버전에서는 기존 Dash fallback을 유지하되 DQ 본게임 검증은 fallback을 성공으로 보지 않는다.

메뉴 `DeadSurvivor/DQ/Apply Main finish`는 편집기 상태에서만 실사용 자산을 생성하고 저장한다. 원본12타일과 assembly를 읽고 임포터/장면을 손으로 바꾸지 않는다. 두 번 실행의 자산 해시 동일/실패복구를 확인한다. 관련 계산·시간경계·각도별 실제 범위 시험, 컴파일0, windup→dash 연속 캡처를 확인한다. 구현 뒤 마감 리뷰는 spec-reviewer·validator-client·algorithm-tester 중 이번 수정이 닿는 것만 요청한다.

## 다음 단위와 전체 마감

대원 공격의 별도 효과 그림 연결, 기존 효과/투사체 대비·발광, UI3화면비, 전체 EditMode1회, playable전체1회, 원근Main관문, 범위대조표, 직접 본 캡처만 preview에 모은다. 전체검사는 단위마다 반복하지 않는다. 성능/유료생성/패키지설치0. 외부 게시0.

## 이월 상태

실행 시작22:37:08Z. 이월 전이 검증30분 한도는23:07:08Z에 끝났다. 실제 높은 bank 수신30프레임0으로 L7-03 FAIL을 보존한다. 새 물리 receiver 설계 수정 확인은 **`VERDICT: PASS`** (design-reviewer job20261123-080542-bf4899, 문서 설계 한정). 구현/GPU 성공은 아니다. `raised-bank-design-brief.md`와 `StartTransitionMatrix(true)`가 이어 할 자리다. 새 bank pipeline 구현은 이월 검증 상한 때문에 이번에는 시작하지 않고 효과/UI를 우선한다.

기존11프레임 NOT_RUN·C4 추정FAIL/G20미실행·K55 NOT_RUN·N9 FAIL·C11 BLOCKED, 풀의 단조로운 초록과 다리/천막/깃발 도트 질감은 닫지 않는다. 기준서 예외(walk24/idle12/attack7,Bruno fall12+lying/나머지10+lying,피격추천,나무3덤불1,소품유료0,기존UI,성능별도)를 유지한다.

방향 어댑터 보완: 기존32방향 반올림은 긴 경로 끝에서 실제 고정 방향과 radius보다 크게 어긋날 수 있어 DQ 경로는 실제 float 각도를 키에 보존한다. 발동 view별 최대4장을 만들고 windup 종료/사망/풀 반환에 반환한다. 준비 표시는 기존 모델 countdown의 경과 시간을 UpdateState에 직접 전달하여 몸 프레임보다 한 프레임 늦어지지 않게 한다. 모델의 시계·상태는 바꾸지 않는다.

## 공격 효과 단위

`tools/dq_attack_fx_pack.py`는 채택 clip의 원본 경로를 현재 로컬 catalog에서 읽고, 허용된 `output/hd2d-dq`의 별도 효과만 묶는다. Bruno의 `effect_*/slash_*`만 칼 궤적으로 사용하고 몸+효과 비교용 `attack_*_effect`는 쓰지 않는다. 앞에서 두 종류를 혼동한 pack은 별도 사용 전 다시 만들었다. 공격 eventFrame에 native 효과2~4를 맞추고 비어 있는 프레임은 Sprite를 만들지 않는다. Lena는 발사 순간 한 장(뒤 프레임의 실제 투사체는 기존 경로), Mira는 무기 orb glow(실제 fireball은 기존 경로), Bolt는 별도 ring이다. 20방향 sheet/manifest와 게임에서 참조하는 Resources `AttackFx.asset`만 반입한다. PNG pack 두 번 hash 동일. 메뉴는 sprite identity를 보존하고 사용하지 않는 이전 subasset만 자기 asset 안에서 정리한다.

MemberView는 기존 `ApplySprite`와 같은 호출에서 body의 DQAction/DQFrame/flip/pivot를 공유하는 자식 cue를 갱신한다. 별도 시계·피해·발사 없음. reset/쓰러짐/비공격 상태에서 즉시 숨긴다. 무광 blade와 심만HDR인 magic 공유 재질을 사용하며 새 재질 복제0이다. 전체 투사체 그림을 새 그림으로 교체한 것으로 보고하지 않는다.

## 기존 전체 시험의 명백한 옛 fixture 보완

- MonsterBehavior B5의 balanced 슬롯 상한102는 현재 data의±45/±108(합153)과 맞지 않았다. 게임 data/행동은 그대로 두고 fixture의 현재 슬롯 수치153을 고정한다. 외곽 경로·피해 동등성 기대와 .001 허용치는 유지한다.
- FieldModelBuild의 카드 footprint 시험은 Main에서 새 DQ catalog를 읽어 원래 카드 크기와 비교했다. 새 DQ 크기를 카드에 줄이는 대신, 이 **legacy 카드 footprint 계약 시험**만 기존 로컬 legacy catalog로 격리한다. 전체 모델/개수/footprint 기대식·.001 문턱은 유지하고 끝에 Main을 다시 연다. DQ 크기의 수용 여부는 DQ 배경 기준/별도 검증이지 이 옛 카드 크기 시험으로 PASS라 하지 않는다.
- 원본 input 두 건은 허용 폴더에 없어 BLOCKED로 분류하고 기대로 지우지 않는다. 전체 결과는 마지막에 한 번 새로 실행하여 기록한다.

래스터 비교 기준은 `Tools/dq_telegraph_reference.py` **DQ-r2-raster-v1** 하나다. 반입된 r2 원본12타일과 assembly에서 직접 조립하고 floor capsule에 역매핑한다. 기준 SHA와 입력별 RGBA SHA는 `Assets/Game/Tests/EditMode/DQTelegraphReference.json`에 고정한다. 32방향+경계3방향×2치수×4단계 **280입력**, 허용차0이며 C#14시험 PASS에 포함된다. 기준 도구/원본 SHA가 달라지면 먼저 실패한다. 도구를 바꾸어 지금 결과에 자동 맞추지 않았다.

효과15키의 정적 프레임 조합(55표본)은 Main의 실제 Burst/Projectile/EnemyShot view 경로에서 캡처한다. 이것은 모든 효과의 실제 엔진 프레임 연속/발동 타이밍 성공과 다르며 그 미확인 항목을 남긴다. 최초 JSON 직렬화 실패, 다음 ROI 화면비 불일치를 고쳐 실제16:9 캡처와 같은 camera.aspect에서 상자를 선언하도록 했다.

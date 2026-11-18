# HD-2D FX F7 마감 리뷰 요청

구현·명령 관문 완료, 독립 마감 리뷰 대기. 이 문서는 리뷰 브리핑이며 최종 핸드오프가 아니다.

- 작업 루트: `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/.director/worktrees/hd2d-fx`
- 기준 커밋: `d3b01b15badf81361fcbfbe7d12f31cada741cf7`; 대상: 이 문서를 포함하는 F7 커밋(HEAD). 브랜치 `claude/hd2d-fx`.
- 단위 커밋: F0 그림2c0267e, F0 94a6e39, F1 29192b8, F2 f63448d, F3 0024a72, F4 41cd92f, F5 dd7a0e9, F6 3872fb3. F7은 HEAD.
- 원본 요구·수용 기준: `design.md` R1~R5, 2~5절 F0~F7; 사용자 지시 F0-A1~A6 및 X-1~X-4. 교체 목록 `docs/game/hd2d-전환-교체-목록.md` 8·9·21~31행.
- 리뷰 역할: validator-client(런타임·연결·명령 증거), spec-reviewer(범위·설계·교체 누락), algorithm-tester(EffectFrameMath/GroundMarkRaster/ChainBoltRaster). 별도 새 컨텍스트 Claude 시각 판독은 `output/hd2d-fx/review/request.md`를 먼저 전달한다. 눈가림 단계 전에 아래 구현자 관찰/정답을 전달하지 않는다.

## 실제 관문과 증거

파일명만 쓴 명령 증거는 `output/hd2d-fx/verification/` 아래다. 그 밖 경로는 루트 기준이다. `output/hd2d-fx/verification/stages.md`가 실패·수정 이력을 포함한다.

| 기준 | 결과 | 증거 |
|---|---|---|
| F0 그림 독립 판독 | PASS | 사용자 반환 Claude 판독 `VERDICT: PASS`, stages.md에 그대로 기록. F0-A1~A6, 눈가림7/7. f0-art-review.md의 NOT_RUN은 당시 요청 기록 |
| F0 임포트·팔레트 | PASS | F0-palette.log, F0-import-tests.json 5/5, F0-combat-tests.json 30/30 |
| F1 흰 심만 HDR | PASS | F1-tests.json, F1-gpu-pixels.txt: 실제 float RT21색, 흰2색만2.4. materials.json 7재질 GUID/세기/큐 |
| F2 프레임/근접 상태 | PASS | F2-frame-tests.json 12/12, F2-combat-tests.json 30/30, F2-play-frames.json 첫 장검1→5 |
| F3 8방향·그림자·풀 초기화 | PASS | F3-tests.json 30/30 |
| F4 Python 픽셀 동등/바닥 판 | PASS | F4-raster-tests.json 6/6, F4-monster-tests.json 13/13 |
| F5 번개3장 동등/양끝/순서 | PASS | F5-raster-tests.json 9/9, F5-combat-tests.json 30/30 |
| F6 포자·그림자 | PASS | F6-tests.json 4/4, F6-play/report.json 1/1, F6 캡처 |
| F7 컴파일 | PASS | F7-compile.json, 오류0 |
| F7 EditMode 전체1회 | FAIL 후 대상 수정 확인 PASS | F7-editmode-all.json 545/547, 옛 아틀라스 기대2건만 수정. F7-atlas-tests-fix.json 및 최종 F7-atlas-tests-final.json 5/5. 전체547/547 재실행을 주장하지 않음 |
| F7 전체 판정 경로1회 | PASS | F7-playable-full/report.json 8/8,142.8초. 성능 측정 없음 |
| F7 연속 캡처 | PASS(숫자/직접 확인) | sequence-audit.json 15종×21장,연속frame+1/time1/60/이진알파/심. final arrow·pulse만 가림 수정 재캡처. F7-sequence/report.json와 F7-visibility-fix/report.json |
| F7 기존 흐름 보존 | PASS | flow-preservation.json, 기존50개 JSON 단계 내용/효과 기대식 불변,18개 추가 |
| 이행 메뉴 멱등성 | PASS | F7-migration-replay.json, migration-idempotence.json,159자산 SHA 불변 |
| X-4 옛 그림 참조 | 본 게임0 / 보존 시안2 | legacy-reference-audit.json. 아래 범위 설명 확인 |
| 독립 마감 리뷰 | NOT_RUN | Codex가 리뷰 역할을 호출하지 않는 실행 계약에 따라 본 문서로 요청 |
| 성능·300마리·버닝 | NOT_RUN | 지정 범위 밖 |

## 구현과 검토할 경로

- `Assets/Game/02.Scripts/Features/Combat/Utils/{EffectFrameMath,GroundMarkRaster,ChainBoltRaster}.cs`, `Features/Combat/Factory/EffectSpriteCache.cs`와 해당 EditMode Tests. 기준 Python `output/assets/hd2d-fx/tools/raster.py` r003, F0 리뷰 채택본 PNG. 정수 반올림·음수·경계·큰 경과시간·뒤집기·끝점·동일 입력 픽셀을 독립 검토한다.
- `Features/Combat/{Data/CombatEffectData.cs,Presenter/CombatWorldPresenter.cs,View/BurstView.cs,View/ProjectileView.cs}`, `Features/Monster/{View/MonsterView.cs,View/EnemyShotView.cs,Factory/MonsterViewFactory.cs,Factory/EnemyShotViewFactory.cs}`, 관련 EnemyShot 모델/생성 경로. 모델 속도·피해·범위 수치 불변 여부.
- `App/PlayableProbe.cs`의 ArmEffectSequence/매프레임 observer 및 효과 키 진입점. 재질/그림자/프리팹 이행은 `Editor/HD2DFxMigration.cs`. `Tools/playable/art-insert-flows.json`은 추가만.
- `Assets/Game/03.Resources/Shaders/HD2D/HD2D_EffectGlow.shader`, Materials/HD2D의 Effect*.mat, Resources/Prefab 4종, Resources/Data/Combat/combat_effects.json 및 mushroom.json, Art/HD2D/Effects68장, EffectsSprite.preset, Characters.spriteatlasv2.
- 게임 화면 `output/hd2d-fx/battle-fx.png`; 연속 원본 `output/hd2d-fx/sequence/<key>/000.png`~`020.png`, frames.csv/state.txt; 검토 시트 review/all-01~15.png, compact-01~15.png. SHA는 fingerprints.json. 원본 캡처는 로컬 보존, 검토 시트/CSV/상태는 커밋.

## 설계 대응·범위 해석

- 기존 SpriteAtlasService/PlaceholderSpriteFactory/cache/raster 검색 후 동적 Sprite/Texture 생성·수명은 Scoped EffectSpriteCache에 두었다. 기존 아틀라스 서비스와 책임을 합치지 않았다. Dispose로 장면 끝 해제.
- 데이터 clips 표를 별도로 추가했다. 단일 item firestaff에 projectile3장12fps 반복과 burst6장24fps 단발이 공존하므로 item 하나의 frames/fps로는 표현되지 않는다. 효과 키별 GetClip으로 해석한다. 세이브 형식 변경 없음.
- ground cache는 같은 외접 정수 픽셀 크기에서도 원래 반지름 차이로 가장자리 픽셀이 달라져 원래 double 반지름도 보조 키로 둔다. 임의 회전/소수 확대 대신 실제 픽셀 재생성,32방향 키 유지.
- 그림 단계의 디렉터 지시가 설계 표보다 나중이다: 번개 흰 심1→2도트, hit16×16 실루엣, 고리3단띠/모양 구분. F0 PASS 기준 r003을 게임/시험에 동일 적용.
- `design.md` 8절의 “바닥 고리≤1.6” 개정 후보는 pulse2.2 예외가 빠져 있다. 실제 구현은3절 Pulse2.2. 기준서 수정은 하지 않고 최종 핸드오프에서 예외를 명시해 전달한다. 15종 연속 검증은 설계12개보다 넓은 실제 표시 전체(폭발 고리·예고·적탄 포함).
- X-1 워크트리 나란히, X-2 투사체 하드 바닥점 그림자, X-3 설계 세기, X-4 PNG10장 보존을 적용. 사용자 원문 대조는 f0-art-review.md 마지막 표 참조.
- X-4 감사: Main·프리팹·아틀라스·데이터·런타임 코드 옛 참조0. 전체 Assets/Game 장면까지 합치면 **2**이며 초기 시안 Lookdev_HD2D.unity의 slash/fireball이다. `docs/game/hd2d-사전조사.md:282`에서 빌드 제외 시안으로 명시, 장면·시안 보존 지시로 남겼다. 이 해석의 적합성은 spec-reviewer 검토 대상. 전체 장면0으로 보고하지 않는다.
- Main.unity/볼륨/Field/몸 그림/UI 수정0, 성능 미측정. 시작 에디터 PID57018·6000.4.3f1·projectPath 일치. 메인 폴더 쓰기0. 설계0·4절 시작 줄 번호 대조와 누락된 화면구조 핸드오프는 f0-art-review.md에 기록.

## 이행 메뉴 순서(합칠 때 재실행)

`DeadSurvivor/HD2D/FX/01 Import Frames` → `02 Wire Materials` → `03 Wire Projectile Shadow` → `04 Wire Enemy Shot` → `05 Retire Legacy References`. Play 종료 후 실행. C# 메서드도 각각 ImportFrames/WireMaterials/WireProjectileShadow/WireEnemyShot/RetireLegacyReferences. 프리팹·재질은 Unity API로 생성/연결했고 손 편집하지 않았다. 장면은 열거나 저장하지 않는다.

## 구현자가 직접 본 것(눈가림 판독 이후에만 전달)

| 키 | 연속 움직임 관찰 |
|---|---|
| slash_sword | 흰·금빛 호가 인물 옆에서 생겨 조각으로 끊김 |
| slash_axe | 두꺼운 주황 초승달이 조각으로 갈라짐 |
| hit_star | 흰 십자에서 별 정점이 커진 뒤 점 조각으로 끝남 |
| explosion | 불꽃→불덩이→불 파편→연기, 바닥 고리가 별도로 퍼짐 |
| fireball | 날아가는 불 꼬리가3장을 반복함 |
| arrow | 화살촉 달린 작은 축이 이동, 바닥 점은 아래에 분리됨 |
| crossbow_bolt | 작은 볼트 축이 이동하고 그림자가 바닥을 따름 |
| lightning | 보라/청록 가지가 양 끝에 붙어 흔들리고 조각으로 끊김 |
| heal_single | 작은 십자들이 머리 위로 올라가는 그림 후 흩어짐 |
| heal_area | 고리가 발밑에서 퍼져 호·잎 조각으로 갈라짐. 이 표본은 물에 걸친 부분이 가려짐 |
| pulse | 보라·민트 겹 고리가 지면에서 퍼지고 호 조각으로 끊김 |
| spore | 보라 포자 몸통이 맥동하며 날아감, 바닥 점 분리 |
| dash_line | 낮은 채도 붉은 갈매기들이 화살촉 쪽으로 흐름, 땅에 눕힌 길 |
| ground_blast | 속 디더가 찬 톱니 범위가 크기 고정으로 깜빡임 |
| ground_boom | 속 빈 톱니 고리가 커졌다 어두운 덩어리로 부서짐 |

전투 화면은 wave3 동시 베기·별·고리·탄·숫자를 시간 정지해 직접 보았다. 고리는 발 아래/몸 뒤, HP·숫자는 위, 칼빛은 HDR 없이 흰 심에만 작은 번짐. 물/높은 지형에서 ground y=.001의 가림은 남아 있으며 이번 단계에서 배경을 바꾸지 않았다. 시각 확정은 별도 Claude 판독 관문이다.

## 비용·회고 및 후속 문서

시간은 verification/time-usage.json. 커밋 경계별 F0~F7 실측(앞선 그림 보정/Claude 대기는 별도). Codex 토큰은 호스트 미제공으로 NOT_AVAILABLE, 하위 에이전트0; Claude 외부 리뷰 사용량 미제공. session_time.py는 Codex jsonl 부재로 FAIL, 추정하지 않음.

놓친 것/방법 전환/워크플로우 갱신 후보는 stages.md: 셰이더 GPU 오류색·console 먼저, 포트 대신 live PID+project, 지면 가림 먼저, 짧은 동시 상태 polling 대신 매프레임 observer, atlas Remove/Add 순서 변화를 SHA로 확인. 이번 도구에 반영했고 킷 원본은 고치지 않았다. 전체 검증 반복을 추가하지 않았다.

모든 리뷰 판정이 돌아온 뒤에만 지정 `.director/outbox/20261118-hd2d-fx-1.md`를 작성한다. 그 문서에 교체 목록13행의 “지금” 문안/커밋, 설계8절 개정 후보(파동2.2 예외 포함), 설계9절에서 실제 바뀐 킷 절차·raster/GPU/연속캡처 도구 후보를 적는다. 현 단계는 리뷰를 스스로 수행한 것으로 기록하지 않는다.

## r002 정정·추가 계약

이전 마감 FAIL의 수정 확인은 [r002-review-brief.md](r002-review-brief.md). 래스터 비교의 정식 절대 경로·버전·SHA는 [raster-reference.md](raster-reference.md) 하나로 고정한다. 보정 전 메인 원본은 현재 비교 기준이 아니다. 실제 clips 필드 구조와1장 복귀 소비는 [data-contract.md](data-contract.md). X-4는 반환 spec-reviewer 판정에 따라 본 게임0/보존 시안2로 명시한다. 전체 장면0이 아니다.

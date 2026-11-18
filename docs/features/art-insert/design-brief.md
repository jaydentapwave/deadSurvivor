# 캐릭터·몬스터 그림 넣기 설계 브리핑

- 상태: 설계 검토 FAIL(DR-01~03) → 반영 → 수정 확인 FAIL(DR-02-R1) → 반영 → 재확인 PASS(후속 DR-F03: 마지막 생존자 번쩍임 중 사망 사례 확인)
- 규모: L2 — 데이터 스키마 칸 추가(`Monster/*.json` `sprite`·`moveStyle`), 새 그림 찾기 서비스(`SpriteAtlasService`, DI 등록), 프로젝트 설정(Preset Manager, Sprite Packer Mode), 장면·프리팹 구조(몸 자식, Sorting Group)
- 기준: 승인 기획서 [plan.md](plan.md)(A~I절, 데이터 스키마, AC1~AC17, 기대 변경 허용 표), 시작 커밋 ba5fd08(F1~F6 합쳐진 상태). 그림은 브랜치 `claude/pack-expedition-2d-art` 13장(바이트 그대로)

## 이번에 사용할 수 있게 될 산출물·완료 조건
- 전투(V2)의 대원 4명·몬스터 5종이 아틀라스 그림으로 그려지고, 대원은 이동 방향대로 앞·뒤 + 좌우 반전, 멈추면 1px 대기, 맞으면 0.1초 흰 번쩍임 + 1px 밀림. 몬스터는 좌우 반전 + `hop`/`squash` 1px 움직임. 로비 카드 4장에 앞 그림 초상(36×36 창).
- 완료 조건: AC1~AC16 확인(AC17 사람은 not-run), 기존 EditMode 213개는 "기대 변경 허용" 표의 것 말고 기대 그대로 통과, 판정 경로 `--until G2`·F1·F2·F4 흐름·새 `art-insert-flows.json` 통과.

## 요구와 제외 범위
- 제외(기획서 "나중·남은 것"): 걷기·공격 클립, 쓰러진 그림, 돌진 예고·자폭 부풂 그림, 그림자, 타일, 효과, UI 세트, 아이콘, 하단 목록 초상, 진형 간격. 이동·충돌·피해 규칙, F6 데이터(`feedback_rules.json`), `route.json`, 다른 기능 흐름 경로, 진형·`run_rules.json`은 고치지 않는다.

## 기존 코드와 데이터 소유처 (실제 코드 확인 결과)
- 대원 그림: `MemberView`(`Party/View`) — `bodyRenderer`는 이미 자식 `Body`에 있고 Sort Point = Pivot(F1 `MainSceneBuilder`). HP 막대 자식 `HpBack`(order 1)·`HpFill`(order 2)과 F6 `HpLossBarView`. `Initialize(bodySprite, hpBarSprite)`에서 막대 높이 = `bodyDiameterPixels(14) + gap`. 초기화는 `WorldPresenter.InitializeMemberViews`(장면 등록 `IReadOnlyList<MemberView>`), 매 프레임 `UpdateMemberViews` → `UpdateView(world, hpRatio, isAlive)`.
- 몬스터 그림: `MonsterView` 프리팹 `Resources/Prefab/MonsterView.prefab` — **몸 `SpriteRenderer`가 뿌리에 있다**(F1 `CreateMonsterPrefab`, Sort Point Pivot). F4가 자식 `DashLine`·`BlastArea`를 더했다. `MonsterViewFactory.Create`가 `PoolManager.Get` + `Initialize(body, line, area, dashLength, blastRadius, unitScale)`, 몸통 마름모는 몬스터 id별 캐시.
- 로비 카드: `UI_CharacterCardView.colorImage`(LayoutElement 높이 14, 카드 VerticalLayout 첫 칸). `LobbyPresenter.Initialize`가 `UI_LobbyView.UpdateUI(dataList, startItemNameList, colorList)`.
- 데이터: `CharacterData.sprite`(F3, 지금 `""`), `MonsterData`에는 그림 칸 없음. `ResourceService.Load<T>`는 `Resources.Load`.
- 판 상태: `RunModel.Phase`·`IsPaused`·`RunCount`. F6 피드백 시계 규칙 = `Phase == Battle && !IsPaused ? Time.deltaTime : 0`(`HitReactionPresenter`).
- 장면 구조 변경 선례: F3~F6은 `MainSceneBuilder`(F1 처음 한 번 전용)를 고치지 않고 에디터 명령(`script eval_file`, 증거 폴더 `tools/`)으로 장면·프리팹을 고쳤다. 이 기능도 그렇게 한다.

## 변경할 파일과 변경 전 → 후

### 그림·설정 (에디터 명령으로, YAML 손편집 없음)
1. 브랜치 합치기(기획서 순서) → 13장 PNG가 들어온다(`.meta` 없음, 기본값으로 첫 임포트).
2. 한 장의 `TextureImporter`를 B절 값으로 맞춘 뒤 `new Preset(importer)` → `Art/Presets/AIGeneratedSprite.preset`. `Preset.SetDefaultPresetsForType(TextureImporter, [DefaultPreset(filter = AIGenerated 폴더, preset)])`(기존 목록 뒤에 붙임). 13장 모두 `Preset.ApplyTo` + `SaveAndReimport`. 새 그림 1장(임시 사본)으로 값이 붙는지 확인한 뒤 지운다.
3. `EditorSettings.spritePackerMode = SpriteAtlasV2`.
4. `SpriteAtlasAsset` 생성 → `Add([폴더 Characters, 폴더 Monsters])` → `SpriteAtlasAsset.Save` → `SpriteAtlasImporter`로 packing(회전 끔, tight 끔, padding 2), texture(Point, 밉맵 끔), 플랫폼 Default·Android·iOS(최대 2048, RGBA32·압축 없음, 덮어쓰기 켬) → 다시 임포트 → `SpriteAtlasUtility.PackAtlases`.
5. `Materials/SpriteFlashWhite.mat` = 내장 `GUI/Text Shader`.

### 겹치는 파일 (줄·칸만)
| 파일 | 변경 |
|---|---|
| `MonsterData.cs` | `[SerializeField] string sprite`, `string moveStyle` + `OnAfterDeserialize`에서 `moveStyle`을 새 열거형 `MonsterMoveStyle { Squash, Hop }`으로 한 번 푼다(behavior와 같은 방식, 모르면 `LogError`). 읽기 `Sprite`, `MoveStyle`. 새 파일 `Monster/Data/MonsterMoveStyle.cs`(기존 `MonsterBehavior.cs`와 같은 자리·모양) |
| `Character/*.json` 4개 | `sprite` = id |
| `Monster/*.json` 5개 | `sprite` = id, `moveStyle` = slime·rock `squash`, mushroom·boar·puffer `hop` |
| `MemberView.cs` | 칸: `flashRenderer`(몸 자식 `Body` 아래 자식, 재질 SpriteFlashWhite), `artHeightPixels`(48), `idlePeriodSeconds`(0.8), `idleStaggerSeconds`(0.2), `hitFlashSeconds`(0.1), `minMovePixels`(0.25), `facingAxisRatio`(0.25) + 읽기 속성. `Initialize(frontSprite, backSprite, hpBarSprite, hasArt)`: 막대 높이 = `hasArt ? artHeightPixels : bodyDiameterPixels` + gap. 새 메서드 하나 `ApplyMotion(SpriteFacing facing, Vector2Int bodyOffsetPixels, bool isFlashOn)`: 몸 스프라이트 = 앞/뒤, 몸·번쩍임 `flipX`, 번쩍임 스프라이트 = 몸 스프라이트, `bodyRenderer.transform.localPosition` = 픽셀/48, 번쩍임 `enabled`. 읽기 `HasArt`, `Facing`, `IsFlashOn`. "몸 자식 Transform" = `bodyRenderer.transform`(이미 자식이라 새 칸 없이 쓴다) |
| `MonsterView.cs` | `Initialize`에 `bool hasArt` 인자 하나(맨 앞 `bodySprite` 다음). 초기화 때 `flipX = false`, 몸 오프셋 0. 새 메서드 `ApplyMotion(bool isFlipped, int bodyOffsetYPixels)`. 읽기 `HasArt`, `IsFlipped`. 칸 `minMovePixels`·`facingAxisRatio`·`moveFrameSeconds`(0.4 주기의 절반이 아니라 주기 0.4)·`staggerSeconds`(0.37) — 몬스터 표시 값(AI4). **프리팹: 뿌리의 몸 `SpriteRenderer`를 자식 `Body`로 옮긴다**(같은 정렬 값·Sort Point Pivot, `bodyRenderer` 참조 다시 연결). 상태 표시 두 자식은 그대로 |
| `MonsterViewFactory.cs` | 생성자 인자 `SpriteAtlasService`. `GetBodySprite`: `data.Sprite`가 비면 F1 마름모, 있으면 `_spriteAtlasService.Get(data.Sprite)`(없으면 서비스가 오류 로그 → 마름모). 결과(그림/마름모, hasArt)를 id별로 캐시 |
| `WorldPresenter.cs` | 생성자 인자 `SpriteAtlasService`, `RunModel`. 대원 초기화: 키가 있으면 `<key>_front`·`<key>_back`, 둘 중 하나라도 없으면 임시 원(F1). 프레임마다(아래 "흐름") 방향·움직임·피격. 상태: 대원별 `SpriteFacing`·지난 HP·`HitFlash`, 움직임 시계, 지난 `RunCount`. 진입점 읽기 `MemberHitFlashTotal`·`MemberFlashMaxSeconds`·`IdleOffsetsSeen`(이번 판, 대원 0) |
| `UI_CharacterCardView.cs` | 칸: `portraitSlot`(LayoutElement, 높이 36), `portraitWindow`(RectMask2D, 슬롯 가운데), `portraitImage`, `portraitArea`(RectInt 14,16,36,36 위 왼쪽 원점). `UpdateUI(..., Color color, Sprite portraitSprite)`: 스프라이트가 있으면 색 칸을 끄고 슬롯을 켜며 배율 = max(1, floor(슬롯 높이 / 36)), 창 크기 = 36×배율, 이미지 크기 = 그림 크기×배율, 위치 = 창 가운데 기준 자른 영역 가운데가 맞도록. 없으면 F1 색 칸. 읽기 `HasPortrait` |
| `UI_LobbyView.cs` | `UpdateUI`에 `IReadOnlyList<Sprite> portraitSpriteList` 인자, 카드에 넘김. 읽기 `PortraitCount` |
| `LobbyPresenter.cs` | 생성자 인자 `SpriteAtlasService`. 키가 있으면 `<key>_front`, 없으면 null |
| `GameSceneLifetimeScope.cs` | `builder.Register<SpriteAtlasService>(Lifetime.Scoped);` 한 줄(Core 묶음) |
| `PlayableProbe.cs` | I절 읽기 멤버. `PlayableFrameSampler`(F6, 같은 파일)에 `UnityEditor.UnityStats.drawCalls`(**기획서는 `batches` — Unity 6.4의 `UnityStats`에는 `batches`가 없어 그리기 호출 수 `drawCalls`를 쓴다, DR-F01; 그림 전·후 모두 이 값**)·`setPassCalls` 표본을 더해 `RenderBatchesP95`·`SetPassCallsP95`(`MarkFrameStats` 뒤, 같은 백분위 식). 조작 멤버 추가 없음 |
| `Tools/check_data.py` | `--art-root`(기본 데이터 폴더 기준 `../../Art`), D절 검사·요약 줄·자체 시험 6종(임시 폴더 `tmp/Resources/Data` + `tmp/Art/AIGenerated/...` 빈 PNG 자리, 자체 시험의 하위 실행에 `--art-root` 전달) |
| `MonsterViewStateTests.cs` | SetUp의 `Initialize` 호출에 `false`(그림 없음 = F1 마름모) 한 인자. 네 시험 기대 그대로(허용 표) |
| `CombatDataLoadTests.cs` | `Sprite == ""` → `Sprite == row.id`(허용 표) |

### 새 파일
- `Core/Resource/SpriteAtlasService.cs` — `IDisposable`. 생성자 `ResourceService`. `Get(string name)`: 처음 부를 때 `Load<SpriteAtlas>("Art/Characters")` 한 번, `Dictionary<string, Sprite>` 캐시(같은 이름 = 같은 객체), 없으면 `atlas.GetSprite(name)`; null이면 `GameLogger.LogError("[SpriteAtlasService] No sprite <name>")`하고 null을 돌려준다(null은 캐시하지 않는다 — 오류는 이름마다 부를 때마다; 호출은 초기화 때뿐). `Dispose`가 캐시한 복제를 `Object.Destroy`(에디터 시험에서는 `DestroyImmediate`가 필요하면 시험이 직접 정리). 이름 규칙 상수 `FrontSuffix = "_front"`, `BackSuffix = "_back"`.
- `Effect/Facing/SpriteFacing.cs` — `readonly struct SpriteFacing(bool IsBack, bool IsFlipped)`, `static Front`, `Next(SpriteFacing last, Vector2 delta, float minMove, float axisRatio, bool canFaceBack)`(E절 규칙 그대로; 몬스터는 `canFaceBack = false`), `Label`("front-right" 등, 진입점용).
- `Effect/Motion/SpriteMotion.cs`(static 순수) — `Advance(clock, deltaTime, isRunning)`(시계), `GetCycleFrame(time, period, startOffset)`(0/1 두 프레임: `(time + startOffset) mod period < period/2` → 0), `GetIdleOffsetY(...)` = 프레임 1이면 +1, `GetMoveOffsetY(style, ...)` = `Hop` +1 / `Squash` −1, `GetHitOffsetX(isFlashOn, isFlipped)` = 켜져 있으면 반전 아님 −1·반전 +1, `ToUnits(pixels)` = pixels / 48. 그리고 작은 값 형식 `HitFlash`(남은 시간·이번 번쩍임 경과): `Start(seconds)`, `Advance(dt)`, `IsOn`, `Elapsed`.
- `Features/Monster/Data/MonsterMoveStyle.cs` — 열거형.
- 시험: `SpriteFacingTests`, `SpriteMotionTests`, `SpriteAtlasServiceTests`(시작에 `SpriteAtlasUtility.PackAtlases`), `ArtImportSettingsTests`(13장 `TextureImporter` 값 + Preset Manager 항목 + 그림 폴더 `CLAUDE.md` 프리셋 경로 줄), `MemberViewSpriteTests`(그림/임시 도형, 방향별 스프라이트·flipX, 번쩍임, 막대 높이) + `MonsterViewStateTests`에 그림 시험(그림 Initialize·반전·오프셋, 풀에서 다시 Initialize하면 초기화).
- 흐름 경로 `Tools/playable/art-insert-flows.json`(H절 표, `sleep`으로 0.5초·1초·3초. `PERF`는 `MarkFrameStats()` 뒤 `sleep 3`, expect `RenderBatchesP95 >= 0 && RenderBatchesP95 <= 50`). **기획서 H절과 다른 점(DR-01)**: `RunService.ReturnToLobby`는 `Result`에서만 된다(`RunService.cs:165`). 그래서 `STOP` 뒤에 단계 `END1`(`timeScale = 20`, `StartBot(AutoNext)`, wait `Screen == "Result"` — B1 판은 `UseRunSettings(1, 0f)` 적 0마리라 10웨이브를 이겨 끝난다)을 넣고, `MON`의 `PressToLobby()`는 그 결과 화면에서 부른다. 마지막 `LOBBY`도 앞에 `timeScale = 20` + wait `Screen == "Result"`(F4 `SHOWEND`와 같은 방식)를 둔다. 게임의 화면 전환 규칙은 바꾸지 않는다.
- `HIT` wait는 `MemberHitFlashTotal >= 1 && MemberFlashMaxSeconds > 0`(끝난 번쩍임이 생길 때까지, DR-F02), expect는 `MemberFlashMaxSeconds > 0 && MemberFlashMaxSeconds <= 0.11`(DR-03).

## 흐름 (WorldPresenter.LateTick, 기존 순서에 끼움)
1. `isRunning = Phase == Battle && !IsPaused`, 움직임 시계 = `SpriteMotion.Advance(clock, Time.deltaTime, isRunning)`.
   **시계가 멈춘 프레임(정비·결과·로비·일시정지)에는 방향·오프셋·번쩍임을 계산하지도 적용하지도 않는다**(DR-02) — 위치(`UpdateView`)만 갱신하고 `ApplyMotion`을 부르지 않으므로 몸 자식 오프셋·`flipX`·번쩍임 켜짐은 마지막 값 그대로다. 이동 중 일시정지해도 대기 +1px로 튀거나 몬스터 오프셋이 0으로 떨어지지 않는다. 예외 둘은 시계와 상관없이 늘 적용: 판 시작 리셋(2), **쓰러진 대원 정리**(번쩍임 끔·오프셋 0, 방향 유지 — 마지막 생존자가 번쩍이는 중 쓰러져 같은 프레임에 `Result`로 바뀌어도 정리된다, DR-02-R1).
2. `RunCount`가 지난 값과 다르면: 모든 대원 방향 = `Front`, 지난 HP = 지금 HP, 번쩍임 끔, 진입점 셈 0, **이 프레임 이동량은 0으로 본다**(로비 → 전투 첫 표시의 자리 점프가 방향을 바꾸지 않게).
3. 대원마다: 이동량 = 새 월드 위치 − 지난 월드 위치(`view.transform.localPosition`, `UpdateView` 전). 살아 있으면 `SpriteFacing.Next(..., canFaceBack: true)`, **먼저** 켜져 있던 번쩍임을 `Advance(dt)`(이번 프레임에 꺼지면 그 번쩍임의 경과를 최대값에 반영), **그다음** HP가 지난 프레임보다 줄었으면 번쩍임 시작(진행 중이었으면 그 경과를 최대값에 반영한 뒤 처음부터; 셈 +1). 시작한 프레임에는 `Advance`가 돌지 않으므로 새 번쩍임은 같은 프레임 `ApplyMotion`에서 반드시 한 번 켜진 채 그려진다(DR-03: `dt ≥ 0.1`이어도 셈만 오르고 안 보이는 일이 없다). `MemberFlashMaxSeconds`는 끝난(꺼진·다시 시작된) 번쩍임만 반영하고 하나도 없으면 −1. 대기 오프셋 = 이동량 < `minMove`(두 축 모두)일 때 `GetIdleOffsetY(clock, 0.8, 대원 번호 × 0.2)`, 아니면 0. 쓰러졌으면 방향·오프셋 그대로 두지 않고 오프셋 0·번쩍임 끔(방향은 쓰러질 때 값 유지). `UpdateView` 뒤 `ApplyMotion`.
4. 몬스터마다(기존 `SyncMonsterViews` 안): 이동량 = 새 위치 − `view.transform.localPosition`(새로 꺼낸 View는 이동량 0으로 본다), `Next(..., canFaceBack: false)`, 이동량 ≥ `minMove`이면 `GetMoveOffsetY(style, clock, 0.4, (id × 0.37) mod 0.4)`, 아니면 0.
- 이동량 기준(`minMove` = 0.25px)은 화면(월드) 단위로 비교: `minMovePixels / 48`.

## 재사용 후보와 신설 이유
- 그림 찾기: `ResourceService`는 경로 한 번 읽기뿐이라 이름별 캐시·복제 정리가 없다 → 기획서 C절대로 `SpriteAtlasService` 신설(`PlaceholderSpriteFactory`와 같은 `IDisposable` + Scoped).
- 방향·움직임 계산: 기존 순수 계산(`ShakeMath`, `IsoProjection`) 모양을 따라 static 순수 클래스 두 개(기획서 지정 경로). 시계 규칙은 F6과 같은 식(코드는 각자; F6 시계는 private).
- 장면 변경: `MainSceneBuilder`는 고치지 않는다(처음 한 번 전용, F3~F6 선례).

## 실패·취소·생사·전환 경계
- 키가 비면 F1 도형(전투 원·마름모, 로비 색 칸). 키가 있는데 그림이 없으면 오류 로그 + F1 도형. 데이터 검사가 미리 막는다.
- 판 시작(`RunCount` 바뀜): 방향 앞·오른쪽, 번쩍임·셈 리셋. 정비·결과·로비·일시정지: 시계 멈춤(대기·번쩍임 정지, 오프셋은 마지막 값).
- 쓰러짐: 몸 흐리게(F1), 번쩍임 끔·오프셋 0, 방향 유지. 부활: HP가 오름 → 번쩍임 없음.
- 몬스터 풀 재사용: `Initialize`가 반전·오프셋을 0으로.

## 데이터 호환성과 복구 방법
- 세이브 없음. `sprite`를 `""`로 되돌리면 F1 도형으로 돌아간다(코드 그대로). 아틀라스·프리셋·설정은 자산이라 커밋 되돌리기로 복구.

## 최소 필수 검증
- EditMode 전체(213 + 새 시험), `check_data.py`·`--self-test`, 판정 경로 `--until G2`, F1·F2·F4 흐름, `art-insert-flows.json`(4화면비 캡처), Device Simulator 노치 가로 로비·전투 캡처, AC13·AC15 측정, AC14 그림 전·후 성능 한 줄.

## 초기 실제 연결 (구현 초기에 에디터에서 먼저 확인할 추측 두 개)
- C절: `Resources.Load<SpriteAtlas>("Art/Characters")`가 `.spriteatlasv2`를 읽는가 → 안 되면 `GameSceneLifetimeScope`의 `[SerializeField] SpriteAtlas` + `RegisterInstance`, 서비스는 `SpriteAtlas`를 받는다(차이 보고).
- E절: URP 2D 렌더러가 `GUI/Text Shader` 재질의 `SpriteRenderer`를 흰색으로 그리는가(Play 캡처) → 안 되면 `Shaders/SpriteFlashWhite.shader`(꼭짓점 색 × 텍스처 알파) 추가(보고).

## 성능 기준값
- 그림 전 값은 시작 커밋 + `PlayableFrameSampler` 표본 추가만 한 상태(그리는 코드·자산 변경 없음)에서 같은 `PERF` 단계로 잰다. 시작 커밋에는 그리기 호출을 읽는 진입점이 없어서다.

## 알려진 환경 차단
- 같은 컴퓨터에 다른 프로젝트 에디터가 떠 있을 수 있다 — 모든 `unity` 명령에 `--project-path` 이 레포.

## 미정 사항
- 없음(기획서 가정 AI1~AI7대로).

## 설계 검토 결과와 해소한 지적
- 1차(Codex design-reviewer): FAIL. 필수 3건 반영:
  - DR-01 흐름 경로가 전투 중 `PressToLobby()`를 부름(`ReturnToLobby`는 `Result`에서만) → `END1` 단계와 `LOBBY` 앞 결과 대기 추가(위 "흐름 경로").
  - DR-02 일시정지에서 위치 차이 0 → 대기 오프셋 튐 → 시계가 멈춘 프레임은 `ApplyMotion`을 건너뜀(흐름 1). 확인: 흐름 경로 STOP과 algorithm-tester 케이스(이동 중 정지·일시정지).
  - DR-03 번쩍임 셈이 실제 표시 없이 오를 수 있음 → Advance 뒤 시작(시작 프레임 표시 보장), 최대값은 끝난 번쩍임만, 없으면 −1, HIT expect `> 0`.
- 후속 DR-F01 → `drawCalls`로 그림 전·후 통일, 보고에 실제 항목 명시.
- 수정 확인(1회): DR-01 해소, DR-03 해소, DR-02 일시정지 튐 해소 + 새 필수 DR-02-R1(정지 분기가 사망 정리를 건너뜀) → 쓰러진 대원 정리를 시계와 상관없이 늘 적용(흐름 1). 후속 DR-F02 → HIT wait에 `MemberFlashMaxSeconds > 0`.

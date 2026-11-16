# 캐릭터·몬스터 그림 넣기 기획서

- 상태: 초안
- 서버 필요: 아니오
- 관련 문서: [게임 기획서](../../game/gdd.md) 5절 C3(몬스터 유형 — 그림)·C4(캐릭터 — 그림), 6절 V1·V2, 판정 단계 G1·G2(이 기능에서 회귀 통과). [아트 기준서](../../art/아트-기준서.md) 3.1·3.2·3.3·3.6·3.10, K12~K14. 앞선 기능 [F1](../f1-skeleton/plan.md)(D절 "그림 키 등 칸 추가", G절 V1 "카드 그림(아트)", `PlaceholderSpriteFactory`), [F2](../f2-movement/plan.md) FA8, [F3](../f3-combat/plan.md) CA10(`sprite` 칸), [F4](../f4-monsters/plan.md) MA14·데이터 스키마("아트 갈래가 같은 파일에 그림 키 칸을 더한다")·F절 상태 표시, [F6](../f6-feedback/plan.md) F절(같은 `MemberView`에 칸을 더함). 참고 분석 문서 없음
- 그림 원본: 그림 브랜치 `claude/pack-expedition-2d-art`의 13장(아래 A절). 구현 단계가 그 브랜치를 합쳐 가져온다
- 작업 규모: L2(데이터 스키마에 칸 추가, 새 그림 찾기 서비스, 프로젝트 설정 Sprite Packer 변경) — 구현 전 설계 검토

## 목표
전투 화면의 원정대 4명과 몬스터 5종, 로비 캐릭터 카드가 임시 원·마름모 대신 아트 기준서대로 만든 도트 그림으로 보인다. 대원은 움직이는 쪽을 보고(앞·뒤 + 좌우 반전), 멈추면 숨 쉬듯 1px 오르내리고, 맞으면 0.1초 하얗게 번쩍인다. 몬스터는 움직이는 쪽으로 몸을 돌리고 통통 뛰거나 눌리며 다가온다.
게임 규칙(이동·충돌·피해)은 바꾸지 않는다. 그림이 들어간 뒤에도 판정 경로와 앞 기능의 흐름 경로가 그대로 통과하고, 전투 화면 그리기 호출이 기준서 예산 안이다.

## 참고 분석 요약
참고 게임 분석 없음. 근거는 우리 문서뿐이다: 아트 기준서(승인 2026-11-15), 그림 13장의 `source.txt`와 기준 정보 `docs/art/identities/<id>.json`(그림 브랜치, 관찰 — 이 기획서를 쓰며 읽음).

## 적용안

### A. 넣을 그림 13장 (그림 브랜치 그대로, 이름·경로·내용 바꾸지 않음)

| 무엇 | 경로(`Assets/Game/03.Resources/Art/AIGenerated/` 아래) | 캔버스 | 그림 크기(폭×키) | 피벗 |
|---|---|---|---|---|
| 브루노 앞·뒤 | `Characters/bruno/front/bruno_front.png`, `Characters/bruno/back/bruno_back.png` | 64×64 | 31×48 / 37×48 | 발 가운데 = 캔버스 아래 가운데 |
| 레나 앞·뒤 | `Characters/lena/front/lena_front.png`, `Characters/lena/back/lena_back.png` | 64×64 | 33×48 / 33×48 | 같음 |
| 미라 앞·뒤 | `Characters/mira/front/mira_front.png`, `Characters/mira/back/mira_back.png` | 64×64 | 36×48 / 36×48 | 같음 |
| 볼트 앞·뒤 | `Characters/bolt/front/bolt_front.png`, `Characters/bolt/back/bolt_back.png` | 64×64 | 30×48 / 29×48 | 같음 |
| 슬라임(일반) | `Monsters/slime/slime.png` | 64×64 | 31×24 | 바닥 가운데 |
| 버섯(사격) | `Monsters/mushroom/mushroom.png` | 64×64 | 34×34 | 바닥 가운데 |
| 멧돼지(돌진) | `Monsters/boar/boar.png` | 64×64 | 47×34(몸이 피벗 오른쪽으로 길다) | 바닥 가운데(다리 기준) |
| 가시 바위(중장갑) | `Monsters/rock/rock.png` | 64×64 | 52×51 | 바닥 가운데 |
| 가시 복어(자폭) | `Monsters/puffer/puffer.png` | 64×64 | 26×25 | 바닥 가운데 |

- 값은 각 그림의 `source.txt` "규격" 줄에서 옮겼다(모두 알파 0/255만, 앞 그림은 오른쪽 아래를 봄). 함께 오는 것: 그림마다 `source.txt`, `docs/art/identities/<id>.json` 9개, `docs/art/sheets/` 비교 시트. 그림 브랜치에는 PNG의 `.meta`가 없다 — 임포트는 B절 프리셋으로 처음 한다.
- 그림 이름은 `AIGenerated/` 전체에서 유일하다(13개 모두 다름). 코드는 이름으로 고르므로 이 유일성을 데이터 검사가 본다(D절).

### B. 임포트 값 (기준서 3.1·3.6, 그림 폴더 정책, `2d-asset-standard.md` 1절)
- 프리셋 하나 `Assets/Game/03.Resources/Art/Presets/AIGeneratedSprite.preset`(TextureImporter): Sprite(Single), PPU 48, 피벗 Bottom Center (0.5, 0), 필터 Point, 압축 없음(RGBA32), 밉맵 끔, 알파 = 투명도, Wrap Clamp. Android·iOS 블록 `overridden: 1`, 최대 크기 2048, RGBA32.
- Preset Manager에 경로 필터 `Assets/Game/03.Resources/Art/AIGenerated/`로 이 프리셋을 단다. 예외 프리셋은 없다(13장 모두 같은 값). 그림이 들어온 뒤 Preset Manager를 바꿨으면 `Preset.ApplyTo` + 다시 임포트.
- 그림 폴더 `Assets/Game/03.Resources/Art/CLAUDE.md` 정책 표의 "임포트 프리셋 (경로)" 칸을 위 프리셋 경로로 채운다. 임포트 예외 표는 빈 채로 둔다. 배율은 정책대로 1(혼자 서는 그림, 원본 크기)이다.
- 아틀라스(3.6 출발값 첫 장 "캐릭터·몬스터·효과 2048"): `Assets/Game/03.Resources/Resources/Art/Characters.spriteatlasv2` 한 장. 담는 것 = 폴더 `AIGenerated/Characters`, `AIGenerated/Monsters`(폴더로 넣어 그림 목록을 따로 적지 않는다 — `2d-asset-standard.md` 3절 "대응표를 따로 만들지 않는다"). 회전 끔, Tight Packing 끔(피벗·반전 그대로), 여백 2px, 필터 Point, 압축 없음, 최대 2048, Android·iOS 덮어쓰기 같은 값. 바닥·장식(1024)·아이콘·UI(1024) 아틀라스는 범위 밖.
- 프로젝트 설정 Editor › Sprite Packer Mode = "Sprite Atlas V2 - Enabled"(지금 Disabled). 에디터 Play에서도 아틀라스가 묶여야 그리기 호출을 잴 수 있다.
- 겹 순서(3.2): F1이 정한 Transparency Sort Axis (0,1,0)과 `sortingOrder`(원정대·적 0)를 그대로 쓴다. 그림 스프라이트의 렌더러는 **Sprite Sort Point = Pivot**(기본 Center면 키 큰 바위·캐릭터가 발이 아니라 몸 가운데로 앞뒤가 갈린다). 대원 한 명의 몸·번쩍임·HP 막대가 한 덩어리로 정렬되도록 `MemberView` 뿌리에 Sorting Group 하나를 단다(4개뿐).

### C. 그림을 키로 찾기
- 데이터의 그림 키(D절) → 스프라이트 이름: 캐릭터 `<key>_front`·`<key>_back`, 몬스터 `<key>`. 걷기·공격 클립 이름은 나중에 같은 키 아래에서 정한다(F3 CA10).
- 새 `Core/Resource/SpriteAtlasService`: `ResourceService.Load<SpriteAtlas>("Art/Characters")`로 아틀라스를 한 번 읽고 `SpriteAtlas.GetSprite(name)` 결과를 이름별로 캐시한다(`GetSprite`는 부를 때마다 복제를 만든다). `IDisposable`로 복제를 지운다(`PlaceholderSpriteFactory`와 같은 방식). 등록 `Lifetime.Scoped`(F1 규칙).
- 키가 비면(`""`) 그 대상은 F1 임시 도형을 그린다(F3 데이터 스키마 "비면 F1 임시 도형"). 키가 있는데 아틀라스에 그 이름이 없으면 `GameLogger.LogError("[SpriteAtlasService] No sprite <name>")`를 남기고 임시 도형을 그린다(조용히 넘어가지 않는다). 데이터 검사(D절)가 이 경우를 미리 막는다.
- 추측: SpriteAtlas V2 자산을 `Resources`에서 `Resources.Load<SpriteAtlas>`로 읽을 수 있다. 구현 단계에서 안 되면 `GameSceneLifetimeScope`의 `[SerializeField] SpriteAtlas` 칸 + `RegisterInstance`로 바꾸고 그 차이를 보고에 적는다(설계 검토 대상).

### D. 데이터 칸
- 캐릭터 `Character/<id>.json`: F3가 만든 `sprite` 칸에 값을 채운다 — 4명 모두 `sprite` = 자기 `id`(`bruno`·`lena`·`mira`·`bolt`). 다른 칸·값은 그대로.
- 몬스터 `Monster/<id>.json`: 같은 이름의 칸 `sprite`(그림 키)를 더하고 5종 모두 자기 `id`. 그리고 이동 움직임 칸 `moveStyle`(아래 E절 표) 하나를 더한다 — 기준서 3.10이 몬스터마다 다른 이동(슬라임 찌그러짐·버섯 통통 뛰기)을 정해서, 코드에 몬스터 id를 쓰지 않으려면 같은 파일의 칸이어야 한다(`data-driven-content.md`).
- 새 데이터 파일·폴더는 없다. 대기·피격·이동 움직임의 시간·픽셀 값은 기준서 3.10 값이고 콘텐츠마다 달라지지 않으므로 F1 `MemberView`처럼 View의 `[SerializeField]` 값으로 둔다(E절).
- 데이터 검사 `Tools/check_data.py`:
  - 캐릭터·몬스터 `sprite`: 글자, 비거나 `[a-z0-9_]+`. 비어 있지 않으면 그림 파일이 있어야 한다 — 캐릭터 `Art/AIGenerated/Characters/<key>/front/<key>_front.png`와 `.../back/<key>_back.png`, 몬스터 `Art/AIGenerated/Monsters/<key>/<key>.png`(그림 폴더는 데이터 폴더 기준 `../../Art`, `--art-root`로 바꿀 수 있다).
  - 몬스터 `sprite` 칸이 없으면 실패(모든 몬스터 파일이 칸을 가진다). `moveStyle`은 `squash`·`hop` 중 하나.
  - `AIGenerated/` 아래 PNG 파일 이름 중복이 없다.
  - 출력에 `그림 키 캐릭터 <채움>/<전체>, 몬스터 <채움>/<전체>` 한 줄(구현 뒤 4/4, 5/5).
  - `--self-test` 틀린 데이터 6종 추가: 그림 파일이 없는 캐릭터 키, 뒤 그림만 없는 캐릭터 키, 그림 파일이 없는 몬스터 키, 형식이 틀린 키(`../slime`), `sprite` 칸이 없는 몬스터, 모르는 `moveStyle`. 각각 실패해야 하고 지금 데이터는 통과해야 한다. 자체 시험은 임시 폴더에 데이터와 빈 그림 파일 자리를 같이 만든다.

### E. 전투 화면 V2의 그림과 움직임

방향 규칙(순수 함수 `Effect/Facing/SpriteFacing`, EditMode 시험):
- 입력: 지난 방향(`IsBack`, `IsFlipped`), 이번 프레임 화면(월드) 이동량 `(dx, dy)`, 최소 이동 `minMove`(0.25px = 0.25/48 유닛), 축 비 `axisRatio`(0.25).
- 앞·뒤: `|dy| ≥ minMove`이고 `|dy| ≥ axisRatio × |dx|`일 때만 정한다. `dy < 0`(화면 아래로) → 앞, `dy > 0` → 뒤. 아니면 지난 값.
- 좌우: `|dx| ≥ minMove`이고 `|dx| ≥ axisRatio × |dy|`일 때만 정한다. `dx < 0` → 반전(왼쪽을 봄), `dx > 0` → 반전 없음. 아니면 지난 값.
- 그래서: 오른쪽 아래 = 앞, 오른쪽 위 = 뒤, 왼쪽 아래 = 앞 + 반전, 왼쪽 위 = 뒤 + 반전(기준서 K14·3.1 정면 규칙). 바로 오른쪽·왼쪽으로만 가면 앞·뒤는 지난 값을 두고 좌우만 바꾼다. 바로 위·아래로만 가면 좌우는 지난 값.
- **멈췄을 때**: 두 축 모두 기준 아래이므로 마지막으로 본 방향을 그대로 둔다. 한 판이 시작될 때(`RunModel.RunCount`가 바뀜) 모든 대원은 앞 + 반전 없음(오른쪽 아래, 기준서 정면)으로 돌린다. 쓰러진 대원은 쓰러질 때 방향 그대로.
- 몬스터: 앞 그림 한 장. 같은 함수의 좌우 판정만 쓴다(앞·뒤는 늘 앞). 풀에서 꺼낼 때 반전 없음으로 초기화.
- 반전은 `SpriteRenderer.flipX`. 피벗이 가운데 아래라 발 자리는 그대로다.

움직임(모두 정수 픽셀, `2d-animation.md` 5절 — 회전·확대·소수 이동 없음. 순수 계산 `Effect/Motion/SpriteMotion`, EditMode 시험):

| 대상 | 움직임 | 값(기준서 3.10) | 언제 | 옮기는 것 |
|---|---|---|---|---|
| 대원 | 대기 | 2프레임, 0.8초 주기: 앞 0.4초 0px, 뒤 0.4초 +1px(위). 대원마다 시작을 0.2초씩 어긋냄 | 살아 있고 이번 프레임 이동이 `minMove` 아래 | 몸 그림 자식의 `localPosition.y` |
| 대원 | 피격 | 0.1초 흰 번쩍임 + 1px 밀림(보는 쪽 반대로 가로 1px). 번쩍이는 중 또 맞으면 0.1초를 처음부터 | 살아 있는 대원의 HP가 지난 프레임보다 줄었을 때(보호막이 다 막아 HP가 그대로면 없음, 회복·부활은 없음) | 몸 자식 `localPosition.x`, 번쩍임 자식 켜기 |
| 몬스터 | 이동 `hop` | 2프레임, 0.4초 주기: 0px / +1px(위, 통통 뛰기) | 이번 프레임 이동이 `minMove` 이상 | 몸 자식 `localPosition.y` |
| 몬스터 | 이동 `squash` | 2프레임, 0.4초 주기: 0px / −1px(아래로 눌림) | 같음 | 같음 |

- 몬스터별 `moveStyle`: 슬라임 `squash`, 버섯 `hop`(기준서 3.10), 멧돼지 `hop`, 가시 바위 `squash`, 가시 복어 `hop`(기준서에 없는 셋은 무게로 고름 — 가정 AI3). 몬스터마다 시작을 `(몬스터 번호 × 0.37초) mod 주기`로 어긋내 수백 마리가 한꺼번에 뛰지 않게 한다(결정적, 난수 안 씀).
- 움직임 시계: 전투 화면이고 일시정지가 아닐 때만 `Time.deltaTime`만큼 흐른다(배속과 무관, 판정 경로의 `time_scale`은 따른다 — F6 피드백 시계와 같은 규칙). 옮기는 양은 늘 `1/48` 유닛의 정수배.
- 흰 번쩍임: 몸 그림과 같은 스프라이트·같은 `flipX`를 그리는 자식 렌더러 하나(`flashRenderer`)를 0.1초 켠다. 재질은 텍스처 알파만 쓰고 색을 흰색으로 내는 공유 재질 하나 `03.Resources/Materials/SpriteFlashWhite.mat`(MaterialPropertyBlock을 쓰지 않아 묶음 그리기가 깨지지 않는다). 셰이더는 내장 `GUI/Text Shader`(꼭짓점 색 × 텍스처 알파)를 먼저 쓴다. 추측: URP 2D 렌더러가 이 셰이더를 그린다 — 안 그려지면 같은 일을 하는 작은 셰이더 `03.Resources/Shaders/SpriteFlashWhite.shader`를 더하고 보고에 적는다.
- 머리 위 HP 막대 자리: F1 `bodyDiameterPixels`(14) 대신 그림이 있으면 캐릭터 키 48px + 간격 `hpBarGapPixels` 위. 막대는 대기·피격 움직임을 따라가지 않는다(몸 자식만 움직임).
- 쓰러진 대원: F1처럼 몸을 `downedAlpha`(0.35)로 흐리게 그대로 둔다(쓰러진 그림은 범위 밖). 대기·피격 없음.
- 몬스터 그림 크기: 배율 1(원본 크기). 그림이 있으면 F1의 `radius` 기반 크기 조절을 쓰지 않는다. 충돌 반지름(`radius`)과 규칙은 그대로 — 화면 크기는 그림이, 판정 크기는 데이터가 정한다(예: 반지름 18 = 화면 지름 약 19.5px, 슬라임 그림 폭 31px).
- F4 상태 표시(돌진 방향 선, 폭발 반경 원)와 적 투사체는 그대로 둔다. 돌진 중에도 좌우 반전 규칙을 따른다.

### F. 로비 V1 캐릭터 카드 초상
- 카드의 임시 색 칸(`UI_CharacterCardView.colorImage`) 자리에 그 캐릭터의 앞 그림을 보인다. 그림 파일을 새로 만들지 않고 화면 표시 영역으로 자른다(기준서 3.3 "초상 — 기준 그림에서 자름"): `RectMask2D` 창 하나 안에 앞 그림 `Image`(Point, `preserveAspect`)를 두고, 그림 픽셀 기준 창 `portraitWindow` = x 14~50, y 16~52(위 왼쪽 원점, 36×36 — 머리부터 허리까지)만 보이게 위치·크기를 맞춘다. 창 값은 View `[SerializeField]`.
- 그림 배율은 창 높이 ÷ 36의 정수(1 이상)로 하고 남는 여백은 카드 바탕으로 둔다(도트 굵기를 고르게). 키가 비거나 그림이 없으면 F1처럼 색 칸.
- `UI_CharacterCardView.UpdateUI(...)`에 초상 스프라이트 인자를 더한다(갱신 진입점은 하나 — `code-style.md`). `UI_LobbyView.UpdateUI`와 `LobbyPresenter`는 그 목록을 넘긴다.
- 하단 대원 목록의 초상은 범위 밖(목표 5는 로비 카드만).

### G. 화면과 기능의 경계 (뒤·옆 기능과 겹치는 파일)

| 파일 | 이 기능이 더하거나 바꾸는 것 | 건드리지 않는 것 |
|---|---|---|
| `Party/View/MemberView.cs` | `[SerializeField]` 몸 자식(`bodyRenderer`를 담는 자식 Transform), `flashRenderer`, 대기·피격 값 칸. `Initialize`에 그림 스프라이트(앞·뒤) 인자, 방향·움직임 적용 메서드 하나 | F6이 더하는 `HpLossBarView` 칸과 HP 막대의 빨간 구간. 피격의 빨간 구간·카메라 흔들림·진동은 F6, 몸 그림의 흰 번쩍임·1px 밀림은 이 기능. 서로 다른 렌더러라 겹치지 않는다 |
| `Monster/View/MonsterView.cs`·`Factory/MonsterViewFactory.cs` | 몸 자식, 그림 스프라이트·반전·이동 움직임 적용. 팩토리가 `SpriteAtlasService`로 그림을 받아 넘김(키가 비면 F1 마름모) | F4 상태 표시 두 개와 `UpdateState`. F6 몬스터 HP 막대(F6은 `MonsterView`를 고치지 않는다) |
| `Field/Presenter/WorldPresenter.cs` | 대원·몬스터 그림 초기화, 프레임마다 이동량 → 방향·움직임, HP 감소 → 피격. 대원별 지난 방향·지난 HP, 움직임 시계 | 카메라·들판·생성 범위 계산 |
| `Party/View/UI_CharacterCardView.cs`·`UI_LobbyView.cs`·`Presenter/LobbyPresenter.cs` | F절 초상 | 카드 글자 칸 |
| `App/GameSceneLifetimeScope.cs` | 등록 줄 `SpriteAtlasService`(`Lifetime.Scoped`) | 다른 등록 |
| `App/PlayableProbe.cs` | 읽기 멤버만 더함(I절) | 기존 멤버·봇 |
| `Editor/MainSceneBuilder.cs`(또는 에디터 명령)·`01.Scene/Main.unity`·`Prefab/MonsterView.prefab` | 대원·몬스터의 몸 자식·번쩍임 자식·Sorting Group·Sort Point, 카드의 초상 창. F1이 장면을 만든 방식 그대로 | 다른 오브젝트 |
| `Resources/Data/Character/*.json`·`Monster/*.json`·`Tools/check_data.py` | D절 | 다른 칸·값, 다른 검사 |
| `Run/placeholder_combat.json`·진형 데이터·`run_rules.json`·`route.json`·다른 기능의 흐름 경로 | 고치지 않는다(진형 간격은 FA8대로 사람이 본 뒤 데이터로) | |

- 합치는 순서: 이 기능 구현은 F1~F6가 루트에 합쳐진 뒤 한다. F6가 `MemberView`에 칸을 먼저 더했으면 그 칸을 그대로 두고 이 기능의 칸을 더한다.

### H. 판정 경로와 흐름 경로
- 판정 경로 `route.json`은 바꾸지 않는다. 이 기능은 G1(로비 카드 4장)·G2(전투 시작)를 지나가므로 `--until G2` 회귀를 돈다.
- 새 흐름 경로 `Tools/playable/art-insert-flows.json`(화면 4종 `sizes`는 F4 흐름과 같은 1920×1080·2340×1080·2520×1080·2048×1536):

| 단계 | do | wait / expect | 캡처 |
|---|---|---|---|
| `L1` | — | `Screen == "Lobby"` / `CardPortraitCount == 4` | `all` |
| `B1` | `UseRunSettings(1, 0f)`, `PressDepart()` | `Screen == "Battle" && Wave == 1` / `MemberSpriteCount == 4 && MemberFacing(0..3) == "front-right"` | `all` |
| `DR` | `SetMove(1, -1)` 0.5초 | / 네 명 모두 `"front-right"` | 1장 |
| `DL` | `SetMove(-1, -1)` 0.5초 | / 모두 `"front-left"` | 1장 |
| `UL` | `SetMove(-1, 1)` 0.5초 | / 모두 `"back-left"` | 1장 |
| `UR` | `SetMove(1, 1)` 0.5초 | / 모두 `"back-right"` | 1장 |
| `R` | `SetMove(1, 0)` 0.5초 | / 모두 `"back-right"`(가로만 → 앞·뒤 유지) | — |
| `STOP` | `SetMove(0, 0)` 1초 | / 모두 `"back-right"`(멈춤 → 유지), `IdleOffsetsSeen == 2`(대기 두 프레임을 다 지남) | 1장 |
| `MON` | `PressToLobby()`, `UseRunSettings(13, 0.2f)`, `UseRealTimeAtWave(6)`, `timeScale = 20`, `PressDepart()`, `StartBot(AutoNext)` | `Screen == "Battle" && Wave == 6 && AliveBehaviorKinds == 5` / `MonsterSpriteCount == EnemyCount && PlaceholderBodyCount == 0` | `all` |
| `PERF` | (웨이브 6, 1배 시간) 3초 | / `RenderBatchesP95` 기록, `RenderBatchesP95 <= 50` | — |
| `HIT` | — | `MemberHitFlashTotal >= 1`(timeout 60) / `MemberFlashMaxSeconds <= 0.11` | 1장 |
| `LOBBY` | `StopBot()`, `PressToLobby()` | `Screen == "Lobby"` | — |

### I. 판정용 진입점에 더하는 읽기 (`PlayableProbe`, 에디터 전용)
`CardPortraitCount`(초상이 그림인 카드 수), `MemberSpriteCount`(그림을 그리는 산 대원 수), `MemberFacing(int)`(`"front-right"`·`"front-left"`·`"back-right"`·`"back-left"`), `IdleOffsetsSeen`(표시 뒤 대원 0이 지난 대기 오프셋 종류 수), `MonsterSpriteCount`(그림을 그리는 산 몬스터 수), `PlaceholderBodyCount`(임시 도형을 그리는 산 대원·몬스터 수), `MemberHitFlashTotal`(이번 판 번쩍임 시작 수), `MemberFlashMaxSeconds`(번쩍임 한 번이 켜져 있던 최대 시간), `RenderBatchesP95`(표시 뒤 `UnityEditor.UnityStats.batches` 95백분위, 표본 없으면 −1)와 `SetPassCallsP95`. 조작 멤버는 더하지 않는다(F2 `SetMove`·F4 설정·봇을 쓴다).

## 데이터 스키마

| 위치 | 이름 | 필드 | 타입 | 키/기본값 | 쓰는 쪽 → 읽는 쪽 |
|---|---|---|---|---|---|
| JSON `Character/<id>.json` | 캐릭터(4개, F1·F3 파일) | `sprite`(F3가 만든 칸, 값만 채움) | string, `""` 또는 `[a-z0-9_]+` | 4명 모두 = `id`. 비면 F1 임시 원 | 데이터 → `CharacterRepository` → `WorldPresenter`·`LobbyPresenter` → `SpriteAtlasService` |
| JSON `Monster/<id>.json` | 몬스터(5개, F4 파일) | `sprite`(새 칸) | string, `""` 또는 `[a-z0-9_]+`, 칸 필수 | 5종 모두 = `id`. 비면 F1 임시 마름모 | 데이터 → `MonsterRepository` → `MonsterViewFactory` → `SpriteAtlasService` |
| (같은 파일) | 이동 움직임 | `moveStyle`(새 칸) | string `squash`·`hop` | slime·rock `squash`, mushroom·boar·puffer `hop` | 데이터 → `MonsterRepository` → `WorldPresenter`(E절) |
| 자산 | 아틀라스 | `Resources/Art/Characters.spriteatlasv2` | SpriteAtlas V2 | 폴더 `AIGenerated/Characters`·`Monsters` | 에디터 → `SpriteAtlasService` |
| 자산 | 임포트 프리셋 | `Art/Presets/AIGeneratedSprite.preset` | Preset(TextureImporter) | B절 값 | Preset Manager → 13장 |
| 세이브 | 없음 | | | | |
| 서버 | 없음 | | | | |

- 실행 중 상태(저장 안 함): 대원별 지난 방향·지난 HP·번쩍임 남은 시간, 몬스터별 지난 방향, 움직임 시계, 이름별 스프라이트 캐시.

## 클라 변경

그림·설정
- 그림 브랜치 `claude/pack-expedition-2d-art`를 합쳐 A절 13장·`source.txt`·identity 9개·시트를 그대로 가져온다(그림 파일을 고치거나 새로 만들지 않는다).
- `Assets/Game/03.Resources/Art/Presets/AIGeneratedSprite.preset`, Preset Manager 필터(`ProjectSettings/PresetManager.asset`), 그림 13장의 `.meta`(프리셋이 만든 값)
- `Assets/Game/03.Resources/Art/CLAUDE.md` — 정책 표의 임포트 프리셋 경로
- `Assets/Game/03.Resources/Resources/Art/Characters.spriteatlasv2`
- `ProjectSettings/EditorSettings.asset` — Sprite Packer Mode = Sprite Atlas V2 - Enabled
- `Assets/Game/03.Resources/Materials/SpriteFlashWhite.mat`(필요하면 `03.Resources/Shaders/SpriteFlashWhite.shader`)

코드 (`Assets/Game/02.Scripts/`)
- `Core/Resource/SpriteAtlasService.cs` — C절
- `Effect/Facing/SpriteFacing.cs` — E절 방향 규칙(순수)
- `Effect/Motion/SpriteMotion.cs` — E절 대기·피격·이동 오프셋 계산(순수, 시간 → 정수 픽셀)
- `Features/Party/View/MemberView.cs`, `Features/Monster/View/MonsterView.cs`, `Features/Monster/Factory/MonsterViewFactory.cs`, `Features/Field/Presenter/WorldPresenter.cs` — E·G절
- `Features/Monster/Data/MonsterData.cs` — `sprite`·`moveStyle` 칸
- `Features/Party/View/UI_CharacterCardView.cs`, `UI_LobbyView.cs`, `Features/Party/Presenter/LobbyPresenter.cs` — F절
- `App/GameSceneLifetimeScope.cs` — 등록 줄
- `App/PlayableProbe.cs` — I절 읽기
- `Editor/MainSceneBuilder.cs`(또는 에디터 명령) — 장면·프리팹의 자식 오브젝트(G절)

데이터·도구
- `Assets/Game/03.Resources/Resources/Data/Character/*.json`(4개 `sprite` 값), `Monster/*.json`(5개 `sprite`·`moveStyle`)
- `Tools/check_data.py` — D절 검사와 자체 시험 6종
- `Tools/playable/art-insert-flows.json` — H절

시험 (`Assets/Game/Tests/EditMode/`)
- `SpriteFacingTests.cs`, `SpriteMotionTests.cs`, `SpriteAtlasServiceTests.cs`, `ArtImportSettingsTests.cs`, `MemberViewSpriteTests.cs`; `CombatDataLoadTests.cs`·`MonsterViewStateTests.cs`는 아래 "기대 변경 허용"대로

## 서버 변경과 API 계약
없음. 그림과 그리는 방식만 바뀌고 저장·재화·보상이 없다.

## 수용 기준

| 번호 | 기준 (확인할 수 있는 문장) | 확인 방법 | 확인 절차 |
|---|---|---|---|
| AC1 | 그림 13장과 `source.txt`·identity 9개·시트가 그림 브랜치와 바이트까지 같다(이름·경로·내용 그대로) | 자동 테스트 | `git diff --stat claude/pack-expedition-2d-art -- Assets/Game/03.Resources/Art/AIGenerated docs/art/identities docs/art/sheets ':(exclude)*.meta'` 출력이 비어 있음 |
| AC2 | 13장의 임포트 값이 B절과 같다: Sprite Single, PPU 48, 피벗 (0.5, 0), Point, 압축 없음, 밉맵 끔, Android·iOS `overridden` 켜짐. Preset Manager에 `AIGenerated/` 필터가 있고 그림 폴더 `CLAUDE.md` 정책 표에 프리셋 경로가 적혀 있다 | 자동 테스트 | `ArtImportSettingsTests`(13장의 `TextureImporter` 값과 Preset Manager 항목 대조) |
| AC3 | 아틀라스 한 장이 13장을 모두 담고, `SpriteAtlasService`가 `bruno_front`…`puffer` 13개 이름에 스프라이트(PPU 48, 피벗 아래 가운데)를 돌려주며 같은 이름은 같은 객체를 돌려준다. 없는 이름은 오류 로그 하나와 null | 자동 테스트 | `SpriteAtlasServiceTests`(시험 시작에 아틀라스 묶기) |
| AC4 | 캐릭터 4개 `sprite` = id, 몬스터 5개 `sprite` = id와 `moveStyle`이 D절 값이며, 데이터 검사가 통과하고 `그림 키 캐릭터 4/4, 몬스터 5/5`를 출력한다 | 자동 테스트 | `python3 -B ./tools/check_data.py`, `CombatDataLoadTests.CharactersKeepF1ValuesAndHaveSpriteField`(새 기대), `MonsterDataTests` |
| AC5 | 데이터 검사 자체 시험: D절 틀린 데이터 6종이 각각 실패하고 지금 데이터는 통과한다(기존 틀린 데이터도 계속 실패) | 자동 테스트 | `python3 -B ./tools/check_data.py --self-test` |
| AC6 | 방향 규칙: (1,−1)→앞·반전 없음, (−1,−1)→앞·반전, (−1,1)→뒤·반전, (1,1)→뒤·반전 없음. 뒤를 보던 대원이 (1,0)으로 가면 뒤 그대로·반전 없음, 앞을 보던 대원이 (0,1)로 가면 뒤·반전은 그대로. (0,0)·기준 아래 이동은 지난 방향 그대로. (1, 0.2)처럼 축 비 아래인 세로 성분은 앞·뒤를 바꾸지 않는다. 몬스터 모드는 늘 앞이고 좌우만 바뀐다. 새 판의 첫 방향은 앞·반전 없음 | 자동 테스트 | `SpriteFacingTests` |
| AC7 | 움직임 계산: 대기 0~0.4초 0px, 0.4~0.8초 +1px, 0.8초에 다시 0px. 피격 0.1초 동안 번쩍임 켜짐·가로 1px(보는 쪽 반대), 0.1초 뒤 꺼지고 0px, 번쩍임 중 또 맞으면 0.1초를 처음부터. `hop` 0/+1px, `squash` 0/−1px, 0.4초 주기. 모든 오프셋이 `1/48` 유닛의 정수배. 일시정지·정비 화면에서는 시계가 멈춘다. 이동 중 대원은 대기 오프셋 0 | 자동 테스트 | `SpriteMotionTests` |
| AC8 | `MemberView`·`MonsterView`가 그림이 있으면 그 스프라이트를 배율 1·Sort Point Pivot으로 그리고 방향대로 앞·뒤 스프라이트와 `flipX`를 바꾼다. 키가 비면 F1 임시 도형(F1 크기)을 그린다. 풀에서 다시 꺼낸 몬스터 View는 반전·오프셋이 초기화된다 | 자동 테스트 | `MemberViewSpriteTests`, `MonsterViewStateTests`(기존 기대 + 그림 시험 추가) |
| AC9 | 판정 경로 G2까지 통과 | 플레이 | `python3 -B ./tools/playable.py --until G2` |
| AC10 | 이 기능의 흐름 경로가 통과한다: 로비 카드 4장이 초상 그림, 전투 대원 4명이 그림, 네 대각선 이동마다 H절 방향, 가로 이동·멈춤에서 방향 유지, 대기 두 프레임, 웨이브 6에서 산 몬스터 모두 그림·임시 도형 0, 피격 번쩍임 1회 이상이고 한 번이 0.11초 이하 | 플레이 | `python3 -B ./tools/playable.py --route Tools/playable/art-insert-flows.json` |
| AC11 | F1·F2·F4 흐름 경로가 그대로 통과한다(그림은 규칙을 바꾸지 않는다) | 플레이 | `python3 -B ./tools/playable.py --route Tools/playable/f1-flows.json`, `--route Tools/playable/f2-movement-flows.json`, `--route Tools/playable/f4-monsters-flows.json` |
| AC12 | 노치 기기에서 가장자리 UI가 가려지지 않고, 16:9·19.5:9·21:9·4:3 가로 화면에서 판과 UI가 잘리거나 겹치지 않는다. 로비 초상이 카드 밖으로 나가지 않고 창 안에 머리~허리가 보이며, 전투의 대원·몬스터 그림이 화면 가장자리·HUD에 잘려 읽을 수 없게 되지 않는다 | 플레이 | `art-insert-flows.json`의 `L1`·`B1`·`MON` `capture: "all"` 4장씩을 열어 봄 + Device Simulator 노치 기기 1대(가로) 로비·전투 캡처 |
| AC13 | 기준서 3.9 C 판정: C1 캐릭터 키 ÷ 화면 높이 12~14%(16:9 캡처에서 대원 그림 키를 잼), C2 대원·몬스터 외곽선 픽셀 색이 #34140e에서 ΔE 8 이내·굵기 1px(16:9 캡처를 정수 배율로 줄여 잼), 그림이 흐리지 않다(Point, 반투명 가장자리 픽셀 0). 잰 값과 캡처 경로를 보고에 적는다 | 플레이 | `B1`·`MON` 16:9 캡처에서 측정 |
| AC14 | 성능: 웨이브 6(1배 시간, 5종 + 적 투사체)에서 `RenderBatchesP95`가 50 이하(기준서 3.6)이고, F4 `LOAD`의 `BurningFrameMsP95`가 16.7ms 이하(M7)다. 시작 커밋(그림 전)에서 같은 두 명령으로 잰 값과 나란히 보고에 한 줄로 적는다(그리기 호출·set-pass·버닝 95백분위·살아 있는 적 최대 수) | 플레이 | `art-insert-flows.json`의 `PERF`, `python3 -B ./tools/playable.py --route Tools/playable/f4-monsters-flows.json` 뒤 `python3 -B ./tools/wave_targets.py --metrics Logs/playable/latest/f4-metrics.json`의 M7 행 |
| AC15 | 진형 겹침 기록(F2 FA8): 균형형·쐐기형·일자형에서 이웃 대원 그림의 가로·세로 간격(px)과 겹치는 쌍 수를 캡처로 재서 보고에 적는다. 데이터는 고치지 않는다 | 플레이 | `B1`(균형형) 16:9 캡처 + 정비에서 진형을 바꾼 뒤 전투 캡처 2장(쐐기·일자)에서 측정 |
| AC16 | 컴파일 오류 0, EditMode 시험 전부 통과 | 에디터 | `python3 -B ./tools/editor_ready.py` 뒤 EditMode 전체 실행 |
| AC17 | 사람 판정: 그림이 들어간 전투·로비가 보기 좋은가 — 방향 바뀜이 어색하지 않은가, 대기·피격·몬스터 이동 움직임 세기, 로비 초상 자른 범위, 몬스터 그림 크기와 충돌 크기 차이, 진형 겹침 | 사람 | 로컬 미리보기 페이지(캡처·짧은 녹화)로 보여 주고 사용자 답을 받음 |

### 기존 테스트
건드리는 흐름의 기존 시험과 경로의 마지막 기대를 새 흐름과 대조했다.
- `CombatDataLoadTests.CharactersKeepF1ValuesAndHaveSpriteField`: 끝 기대 `character.Sprite == ""` → 이 기능이 값을 채우므로 바뀐다(아래 표). 같은 시험의 F1 값 대조는 그대로.
- `MonsterViewStateTests`: 네 시험의 끝 기대는 상태 표시 두 개의 켜짐·꺼짐뿐이고 이 기능은 그것을 바꾸지 않는다. `SetUp`의 `Initialize(...)` 호출 인자는 `MonsterView` 시그니처가 바뀌면 같이 고친다(기대는 그대로, 아래 표).
- `MonsterDataTests`·`DataLoadTests`: 칸 값 대조에 `sprite`가 없다 — 칸이 더해져도 끝 기대가 같다.
- 판정 경로 G1·G2(`CardCount == 4`, 전투·웨이브 1·생존 4·시간 감소), `f1-flows.json`·`f2-movement-flows.json`·`f4-monsters-flows.json`의 기대는 화면·규칙 수(위치, 생존, 처치, 생성 수, 상태)만 보고 몸 그림을 보지 않는다 — 그대로.

기대 변경 허용(이 목록에 있는 것만 구현 단계가 기대값을 고칠 수 있다):

| 테스트 | 지금 기대(끝 상태·값) | 새 기대 | 이유(기획서 절) |
|---|---|---|---|
| `CombatDataLoadTests.CharactersKeepF1ValuesAndHaveSpriteField` | 4명 모두 `Sprite == ""` | 4명 모두 `Sprite == id` | D절 — F3가 비워 둔 그림 키를 채움 |
| `MonsterViewStateTests`(SetUp) | `Initialize(body, line, area, 322, 90, 0.0113)` 호출 | 바뀐 `Initialize` 시그니처에 맞춘 호출(그림 없음 = F1 마름모 경로). 네 시험의 끝 기대는 그대로 | E·G절 — `MonsterView`에 그림·움직임 인자 |
| `check_data.py --self-test` 출력 | 틀린 데이터 N종 모두 실패 | N+6종 모두 실패(문서 문자열의 수도 고침) | D절 |

## 나중·남은 것 (범위 밖)
- 걷기·공격 클립(영상 프레임, 아직 없음). 지금은 움직일 때 정지 그림(대기 움직임 없음).
- 쓰러진 그림(없음 — F1 흐리게 표시 그대로), 돌진 예고·자폭 부풂 그림(없음 — F4 임시 선·원 그대로).
- 그림자 타원, 바닥·흙길·장식 타일, 효과, UI 세트, 아이템 아이콘(사람 답 대기 중), 하단 대원 목록 초상.
- 진형 간격 조정(F2 FA8): 이 기능은 AC15로 재서 기록만 한다. 사람이 보고 진형·`unitScale` 데이터로 고친다.
- F6 몬스터 HP 막대 높이는 반지름 기준이라 키 큰 그림(가시 바위 51px)과 겹칠 수 있다 — AC13·AC17 캡처로 보고 F6 데이터(`offsetPx`)로 고칠지 사람이 정한다.

## 가정

| 번호 | 가정 | 틀리면 바뀌는 것 |
|---|---|---|
| AI1 | 멈추면 마지막 방향 유지, 새 판 첫 방향은 앞·오른쪽(기준서 정면) | E절 방향 규칙 |
| AI2 | 방향 판정 문턱 0.25px/프레임, 축 비 0.25(View 값) | 대각선 근처에서 그림이 바뀌는 각도 |
| AI3 | 기준서에 이동 방식이 없는 멧돼지·복어는 `hop`, 가시 바위는 `squash`. `squash`는 정수 픽셀 규칙상 1px 아래로 눌리는 것으로 흉내 낸다 | 몬스터 `moveStyle` 데이터 값 |
| AI4 | 몬스터 이동 주기 0.4초(기준서는 2프레임만 정함), 대원 대기 시작을 0.2초씩, 몬스터를 번호 × 0.37초로 어긋냄 | View 값 |
| AI5 | 피격 밀림 방향은 보는 쪽 반대 가로 1px(공격자 방향을 쓰지 않는다 — 규칙 코드를 건드리지 않기 위해) | E절 피격 |
| AI6 | 로비 초상 창은 그림 픽셀 x 14~50, y 16~52(머리~허리 36×36) | F절 창 값(View 값) |
| AI7 | 첫 아틀라스 한 장에 캐릭터·몬스터를 담고 효과는 효과 그림이 생길 때 같은 아틀라스에 폴더로 더한다 | B절 아틀라스 |

## 열린 질문
- 없음(위 가정은 추천으로 정했고, 보기 좋음은 AC17 사람 판정에서 고친다).

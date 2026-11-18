# HD-2D 전투 효과·투사체·예고 표시 교체 — 설계 (FX)

- 작성: 2026-11-18 13:5x, Claude(디렉터 하위 작업). 상태: **초안 확정 대기(디렉터)**. 규모: **L1**(새 재질 1·새 순수 계산 2·뷰 4개 고침, 상태 소유권·DI 구조·세이브 형식 변경 없음 → design-reviewer 생략, 마감 리뷰 묶음 1회).
- 사용자 원문(11-18): "지금 현재 hd2d로 변경하는만큼 이펙트나 프로젝트타일등도 모두 교체가 되어야해, 이것도 studio-kit에서 활용할 수 있게금 정리하면서 작업을 진행해줘".
- 입력: 교체 목록 [hd2d-전환-교체-목록.md](../../game/hd2d-전환-교체-목록.md) "빠짐" 13행, 기준서 r6.1(3.1·3.4·3.5·3.10·3.12, K15·K25, 겹 순서 3.2 끝), 화면 구조 설계 [design-brief](../hd2d-screen/design-brief.md) 3.7·3.8·3.10, 4단계 핸드오프, 배경 설계 [prep](../hd2d-background/prep.md), 그림 `output/assets/hd2d-fx/`(notes.md).
- 이 설계가 하지 않는 것: Bloom·흐림 값 자체(6단계 L3), 성능 측정(7단계), HP 막대·피해 숫자 그림(U단계 U7·G3), 대원·몬스터 몸 그림.

## 0. 지금 코드에서 확인한 것 (읽은 파일과 줄)

| 무엇 | 지금 | 근거 |
|---|---|---|
| 발동 그림 재질 | `HD2D_SpriteFade`: 빛 없음, 반투명, `color × 텍스처` → 값 ≤ 1 | `BurstView.prefab` 재질 GUID, `Shaders/HD2D/HD2D_SpriteFade.shader` |
| 투사체·적 탄 재질 | `HD2D_SpriteLit`(툰 명암을 받음) | `ProjectileView.prefab`, `EnemyShotView.prefab` |
| Bloom | 문턱 2.0·세기 0.1(b31974b) | 기준서 3.12 |
| ⇒ | **어떤 효과도 Bloom 문턱을 넘지 못한다.** 기준서 3.12 "불빛·마법에만 걸림"이 지금은 아무 데도 걸리지 않음 | |
| 움직임 | 발동 0.25 s(`CombatService.BurstSeconds`), 피격 0.15 s(`CombatWorldPresenter.HitSeconds`) 동안 **한 장 + 알파 페이드**. 폭발은 1.0→1.2 소수 배율 | `CombatWorldPresenter.cs:132,174-177` |
| 투사체 방향 | 화면 각도를 45°로 반올림해 **Z 회전** + 좌우 반전 | `ProjectileView.cs:18-37` — 45°에서 텍셀이 대각선으로 놓인다 |
| 번개 | 40×9 마디를 화면 길이로 나눠 **임의 각도 회전 + 끝 마디 x 소수 배율** | `CombatWorldPresenter.cs:134-151` |
| 회복 범위·파동 | 바닥에 눕힌 뒤 **같은 배율**(지름/그림 폭)로 늘림. 회복 범위 그림은 이미 2:1 타원(64×32) → 눕히면 화면 약 4:1, 텍셀 약 2.85배(향로 반지름 168 → 1.90 m 기준, 계산·화면 미확인). 파동은 전용 그림이 없고 `explosion`을 눕혀 씀 | `CombatWorldPresenter.cs:168-173`, `combat_effects.json`(orb → explosion) |
| 폭발 크기 | 그림 26×22 도트 = 0.54 m 폭. 피해 반지름 75 → 0.85 m(지름 1.7 m) | `combat_rules.json explosionRadius 75`, `run_rules.json unitScale 0.0113` |
| 돌진 선·폭발 범위 | 코드가 만든 흰 반투명 상자(4×2)·원(64)을 눕혀 소수 배율·Y 회전 | `MonsterViewFactory.cs:50-51`, `MonsterView.cs:86-91,192-198` |
| 적 탄 | 코드가 만든 6 px 원(`shotColor #f2d3ff`), 세운 자식 없음, 몸 높이 0.5 m | `EnemyShotViewFactory.cs:33`, `EnemyShotView.cs`, `EnemyShotPresenter.cs:41` |
| 효과 확인 진입점 | `PlayableProbe.EffectVisibleCount(spriteName)`·`EffectShownCount`·`ArmEffectCapture`(처음 보인 프레임에 시간 멈추고 **한 장**) — 그림 **이름**으로 찾는다 | `PlayableProbe.cs:658-699,1202-1213`, `tools/playable/art-insert-flows.json:303-348` |
| 효과 시험 | `CombatEffectTests`(그림 존재·8방향·반전 초기화·연결 순서 등) | `Tests/EditMode/CombatEffectTests.cs` |

## 1. 원칙 (HD-2D 효과 다섯 규칙)

| # | 규칙 | 이유 |
|---|---|---|
| R1 | **도트 밀도는 캐릭터와 같다.** 세운 판 1 텍셀 = 1/48 m. 눕힌 판은 **화면 공간으로 그린 그림**을 눕히고 local y 배율 = 1/sinθ(30°에서 2) → 1 텍셀 = 가로 1/48 m × 깊이 1/24 m = 화면 1도트(바닥 질감 K6 "가로 48·깊이 24 텍셀/m"와 같다) | 정사영 30°에서 바닥 깊이는 화면에서 절반이 된다. 위에서 본 원을 눕히면 텍셀 행이 반으로 눌려 도트가 섞인다 |
| R2 | **실행 중 소수 배율·임의 각도 회전 금지.** 방향은 그린 그림(0·45·90° + 반전)으로, 크기·방향이 데이터로 바뀌는 바닥 표시와 연결선은 **실행 중 도트로 그린다**(화면 픽셀 공간 래스터) | 회전·소수 배율은 도트를 대각선·제각각 크기로 만든다(정수 배율 화면에서 가장 먼저 눈에 띄는 결함) |
| R3 | **움직임은 프레임, 사라짐은 쪼개짐.** 알파 0/255, 알파 클립. 알파 페이드·크기 키우기를 쓰지 않는다 | 반투명 도트는 흐림 띠·Bloom 뒤에서 뭉개지고, 정렬이 `sortingOrder`에 묶인다. 알파 클립이면 깊이 버퍼로 인물과 앞뒤가 맞는다 |
| R4 | **빛은 심만 낸다.** 불·마법·회복·번개의 흰 심(#ffffff·#fff9ed)만 HDR 세기 > 2.0으로 Bloom 문턱을 넘긴다. 칼빛·화살·볼트는 1.0(넘지 않음). 효과는 해·툰 명암을 받지 않는다(빛 그 자체) | 기준서 3.12 "Bloom 문턱 2.0, 불빛·마법에만" + K15 "빛 번짐은 Bloom" |
| R5 | **짧은 것은 연속으로 판정한다.** 0.15~0.25 s 효과는 정지 한 장으로 보지 않는다: 연속 캡처(프레임 번호가 1→N 차례로 다 나오는지) + 상태 숫자 | art-core-rules 1절 |

## 2. 효과별 표

겹은 기준서 3.2 끝의 ③ 그림자·눕힌 판 / ④ 세운 도트 판 / ⑤ 효과. "재질"은 3절. fps는 지속 시간 ÷ 프레임 수(기준서 3.10 추가 후보, 8절).

| 키(`combat_effects.json`) | HD-2D 꼴 | 그림(`output/assets/hd2d-fx/`) | 프레임·fps | 발광(심 세기) | 그림자 | 겹·재질 | 코드에서 바뀌는 것 |
|---|---|---|---|---|---|---|---|
| `slash_sword` | 세운 판, 몸 높이, 대상 쪽 반전(지금 규칙) | `frames/slash_sword/` 44×48 | 5장 20 fps(0.25 s) | 1.0(칼빛 — 넘지 않음) | 없음 | ⑤ M7 | 경과 시간 → 프레임(4절 `EffectFrameMath`), 알파 페이드 뺌 |
| `slash_axe` | 같음 | `frames/slash_axe/` 38×40 | 5장 20 fps | 1.0 | 없음 | ⑤ M7 | 같음 |
| `hit_star` | 세운 판, 몸 높이 | `frames/hit_star/` 20×20 | 3장 20 fps(0.15 s) | 2.4(흰 심) | 없음 | ⑤ M7 | 같음 |
| `explosion` | 세운 판(불덩이) **+ 눕힌 피해 범위 고리**(`ground_boom`, 반지름 = 데이터) | `frames/explosion/` 32×28, `raster/ground_boom_*` | 6장 24 fps + 고리 4장 16 fps | 불 2.4, 고리 1.6 | 없음 | ⑤ M7 / ③ M7g | 1.0→1.2 배율 뺌, 고리 그리기 추가 |
| `fireball`(투사체) | 세운 판, 몸 높이, **0·45·90° 그림 + 반전**(Z 회전 없음) | `frames/fireball_dirs/` | 3장 12 fps 반복 | 2.4 | 눕힌 점 그림자 8×4 | ④ M7 | `ProjectileView` 방향 그림 고르기, 프레임 반복, 그림자 자식 |
| `arrow`, `crossbow_bolt` | 같음(빛 없음) | `frames/arrow_dirs/`, `frames/crossbow_bolt_dirs/` | 1장 | 1.0 | 같음 | ④ M1(물체 — 툰 명암·그림자 받음, 지금 그대로) | 방향 그림 고르기, 그림자 자식 |
| `lightning`(연결) | 세운 판 하나, **화면 공간 꺾은선을 실행 중 그림**(흰 심 1 + 보라 테 1 도트) | `raster/chain_bolt_*`(기준 구현) | 3장 12 fps(가운데 장 청록 테 — K15 "보라·청록") | 2.4(심) | 없음 | ⑤ M7 | 마디 회전·배율 대신 `ChainBoltRaster` |
| `heal_single` | 세운 판, 머리 위 | `frames/heal_single/` 20×26 | 5장 20 fps | 2.4 | 없음 | ⑤ M7 | 4 px 오르기는 그림에 들어 있음 → 위치 오르기 뺌 |
| `heal_area` | 눕힌 고리, 실행 중 그림(반지름 = 데이터) | `raster/ground_heal_*` | 5장 20 fps | 1.6(바닥이 번지지 않게 문턱 아래) | — | ③ M7g | 같은 배율 늘리기 대신 `GroundMarkRaster.Ring(heal)` |
| `pulse`(오브, 새 키) | 눕힌 파동 고리, 실행 중 | `raster/ground_pulse_*` | 5장 20 fps | 2.2(흰 앞 테) | — | ③ M7g | `combat_effects.json` orb `burst` → `pulse`(지금 `explosion` 재사용 끊음) |
| 적 탄 `spore`(버섯) | 세운 자식을 둔 세운 판, 몸 높이 0.5 m(지금 높이) | `frames/spore/` 18×18 | 3장 8 fps 반복 | 1.6(적은 문턱 아래 — 수가 많아 번짐 방지) | 눕힌 점 그림자 | ④ M7 | `EnemyShotView`에 `upright`·그림자 자식, 팩터리가 데이터 그림 키로 아틀라스 그림 사용(색 원 폐기) |
| 돌진 예고(멧돼지) | 눕힌 "길"(양옆 점선 테 + 갈매기 흐름 + 화살촉), 방향·길이를 그림에 굽는다 | `raster/dash_line_*`(r002) | 3장 12 fps | 1.0 | — | ③ M7g | Y 회전·x 늘리기 대신 `GroundMarkRaster.DashPath` |
| 자폭 예고(복어) | 눕힌 빨간 고리 + 점선 + 디더 채움 | `raster/ground_blast_*` | 2장 8 fps 깜빡임 | 1.0 | — | ③ M7g | 흰 반투명 원 대신 `GroundMarkRaster.Ring(blast)` |
| 투사체 그림자 | 눕힌 점 8×4(#34140e) | `raster/shadow_dot.png` | 1장 | — | — | ③ `HD2D_HardShadowCard`(몬스터 그림자 판과 같은 재질) | 새 자식 1(풀 안에서 재사용) |

- **그림자 결정**: 기준서 3.12 "실제 그림자"는 대원·큰 소품·식물만이고 작은 돌·깃발은 끈다. 투사체는 몸 높이(0.5 m)에 떠 있어 바닥 점이 보이지 않으면 높이를 읽을 수 없다 → 몬스터와 같은 **눕힌 하드 판**(실제 그림자 패스 없음, 비용 = 판 1장). 발동 그림(빛)은 그림자 없음. `[방법]` 6단계 판독에서 다시 본다.
- **반지름 데이터 그대로**: 고리·길 크기는 모두 기존 데이터(`range × rangeUnit × (1+stat)`, `effectRadius`, `blastRadius`, `explosionRadius`, `dashSpeed × dashSeconds`) × `unitScale`. 새 수치 없음.
- **세운 판 크기**: 정점 프레임이 지금 채택 정지 그림과 같은 크기(장검 높이 44→48 캔버스, 도끼 36→40)라 화면 크기는 4단계와 같다. 폭발만 피해 범위를 고리로 따로 보여 준다.

## 3. 재질

| # | 재질(새/바뀜) | 쓰는 것 | 상태 |
|---|---|---|---|
| M7 | **새 `HD2D_EffectGlow`**(손으로 쓴 HLSL, 빛 없음, 알파 클립 0.5, ZWrite On, 양면, `unity_SpriteColor`·반전 지원) — 출력 = 텍스처색 × (`max(r,g,b) ≥ 0.96`이고 채도 < 0.12면 `_CoreIntensity` 아니면 `_BodyIntensity`) | 세운 효과·불 투사체·적 탄 | AlphaTest 큐. 심 세기는 재질 인스턴스 3개(칼빛 1.0/1.0, 불·마법 2.4/1.0, 적 1.6/1.0)로 나눠 SRP 배칭 유지(속성 블록 안 씀) |
| M7g | M7과 같은 셰이더, ZWrite Off, 큐 AlphaTest−10(땅 위 1 mm, 인물보다 먼저) | 눕힌 고리·길 | 회복 1.6, 파동 2.2, 폭발 고리 1.6, 예고 1.0 — 인스턴스 4개 |
| M1 | `HD2D_SpriteLit` 그대로 | 화살·볼트(물체) | 지금 그대로 |
| M2 | `HD2D_SpriteFade` | 발동 그림에서 **빠진다**(발 그림자 판 등 다른 사용처는 그대로) | |

- 심 판정 문턱(0.96·0.12)은 효과 팔레트에서 흰 두 색(#ffffff·#fff9ed)만 고른다: #ffe08a(채도 0.46)·#f8d818·#e3d7c8(max 0.89) 모두 밖 — `raster.py`와 같은 팔레트로 확인. 재질 인스턴스 이름: `HD2D_EffectGlow_Blade/Fire/Enemy`, `HD2D_EffectGround_Heal/Pulse/Boom/Warn`.
- HDR 세기 2.4 × 흰색 → 2.4 > 문턱 2.0. 세기 0.1이라 번짐은 작다. 값 다듬기는 6단계 L3 몫(이 설계는 "넘는 것 / 안 넘는 것"만 정한다).

## 4. 코드 대응 (바뀌는 것만)

| 파일 | 바뀜 | 새/고침 |
|---|---|---|
| `Features/Combat/Utils/EffectFrameMath.cs` | **새 순수 함수**: `FrameAt(elapsed, frameCount, fps, loop)` — 단발은 마지막 장에서 멈춤(지속 끝에서 뷰 반납), 반복은 나머지. `DirectionSprite(screenAngle)` → (0/45/90 인덱스, flipX, flipY) — 지금 `GetProjectileAngle` 45° 반올림과 같은 칸 | algorithm-tester 대상 |
| `Features/Combat/Utils/GroundMarkRaster.cs` | **새 순수 함수**(Unity 텍스처 없이 `Color32[]` 채우기): `Ring(kind, radiusMeters, frame)` → 크기·픽셀, `DashPath(lengthMeters, floorAngle, frame)` → 크기·피벗·픽셀. 규칙은 `output/assets/hd2d-fx/tools/raster.py`와 같다(같은 입력에 같은 픽셀 — EditMode 시험이 raster.py가 만든 PNG와 비교) | algorithm-tester 대상 |
| `Features/Combat/Utils/ChainBoltRaster.cs` | **새 순수 함수**: 시작→끝 화면 벡터(px, 정수)·시드·프레임 → 꺾은선 픽셀(흰 심 + 테) | algorithm-tester 대상 |
| `Features/Combat/Service/` 또는 `Factory/` `EffectSpriteCache` | 위 래스터 결과를 `Texture2D`(Point, 밉맵 없음)·`Sprite`(PPU 48, 피벗)로 **한 번만** 만들고 키(종류, 정수 px 반지름/길이, 각도 칸 32, 프레임)로 재사용. 장면 끝에 해제 | 기존 `SpriteAtlasService`·`PlaceholderSpriteFactory` 패턴 grep 후 위치 결정(verify-existing-code) |
| `Features/Combat/Data/CombatEffectData.cs` + `combat_effects.json` | 항목 칸 추가: `frames`(장 수), `fps`, `loop`; orb `burst` = `pulse`; 그림 키는 `<키>_<n>`(1부터) | 데이터 형식만 늘림(세이브 아님) |
| `Features/Combat/View/BurstView.cs` | `UpdateView`에서 `alpha` 인자 대신 `Sprite frame`; 크기 `scale`은 1 또는 바닥 판 (1, 1/sinθ)만 | 고침 |
| `Features/Combat/View/ProjectileView.cs` | Z 회전 뺌 → `DirectionSprite`로 그림·반전; 프레임 반복; 그림자 자식(`[SerializeField] SpriteRenderer shadowRenderer`, 눕힘 Euler(90,0,0) + 바닥 높이) | 고침 |
| `Features/Combat/Presenter/CombatWorldPresenter.cs` | `DrawBurst`: 경과 = `Seconds − RemainingSeconds` → 프레임; Chain 분기 → `ChainBoltRaster` 한 장; HealArea·Pulse → `GroundMarkRaster`; Explosion → 세운 불 + 눕힌 고리 두 개; `hit_star` 3장; `MarkShown`은 **효과 키**로 센다(`slash_sword_3`이 아니라 `slash_sword`) — 판정 경로가 키로 찾으므로 | 고침 |
| `Features/Monster/View/EnemyShotView.cs` + 팩터리·프리팹 | `upright`·`shadowRenderer` 자식, 프레임 반복; 팩터리는 몬스터 데이터의 새 칸 `shotSprite`(`spore`)로 아틀라스 그림(`shotColor`는 남겨 두되 그림에 안 씀) | 고침 |
| `Features/Monster/View/MonsterView.cs`·`Factory/MonsterViewFactory.cs` | 돌진 선·폭발 범위: 임시 상자·원 → `EffectSpriteCache`(길: 돌진 시작 때 방향 확정 → 한 번 그림, 고리: 몬스터 종류마다 한 번). Y 회전·x 늘리기 뺌. 프레임은 예고 시간으로 | 고침 |
| 프리팹 `BurstView`·`ProjectileView`·`EnemyShotView`·`MonsterView` | 재질 M7/M7g, 그림자 자식 | 이행 메뉴(에디터 스크립트)로만 — 손 편집 금지(나란히 실행 대비) |
| 그림 넣기 | `output/assets/hd2d-fx/frames/**` → `Assets/Game/03.Resources/Art/HD2D/Effects/`(새 폴더) — 임포트 PPU 48·Point·압축 없음·피벗 가운데(길만 왼쪽 아래: notes 표). `Characters.spriteatlasv2`에 폴더 추가(지금 `Effects/`처럼). 옛 `AIGenerated/Effects/` 10장은 지우지 않는다(되돌리기용, 데이터가 안 가리킴) | |
| `PlayableProbe.cs` | `EffectVisibleCount`·`EffectShownCount`가 효과 **키**로 찾음(그림 이름의 앞부분), 새 `ArmEffectSequence(key, dir, seconds)`: `Time.captureDeltaTime = 1/60`로 0.35 s 동안 매 프레임 화면 일부(효과 상자 주변) PNG + 줄마다 `frame=<n> sprite=<이름> alpha=<0/255> glowCore=<개수>` 기록 | 고침(4단계 진입점 이름·형 그대로) |
| `tools/playable/art-insert-flows.json` | 효과 흐름 8개의 기대식은 **그대로**(키 이름이 같다), 새 흐름 `FXSEQ-<키>` 추가만 | 더하기만 |

## 5. 작업 단위 F0~F7 (Codex 단계)

단위마다 그 단위의 확인만, 단위마다 커밋(지정 파일만). 판정 경로 전체·EditMode 전체는 F7에서 한 번. **성능은 재지 않는다.**

| 단위 | 하는 일 | 바꾸는 파일(약) | 확인 |
|---|---|---|---|
| **F0 그림 넣기·데이터** | 68장 넣기(임포트 값), 아틀라스, `combat_effects.json` 칸(frames·fps·loop·pulse), 몬스터 `shotSprite`, `CombatEffectData` 칸 | 그림·meta 68+, json 2, .cs 2 | 컴파일 0. `Tools/check_sprites.py --palette docs/art/아트-기준서.md`(새 폴더) PASS. `-testFilter "CombatEffectTests;ArtImportSettingsTests"`(그림 존재 시험을 프레임 키로 고침) |
| **F1 재질 M7·M7g** | 셰이더 1, 재질 인스턴스 7, 프리팹 재질 교체 메뉴 | .shader 1, .mat 7, 에디터 .cs 1 | 컴파일 0. 프리팹 재질 GUID 표를 핸드오프에. 셰이더 심 판정을 효과 부분 팔레트(notes.md 1절 색 전부) 표로 EditMode 시험(흰 두 색만 심) |
| **F2 프레임 재생(발동·피격)** | `EffectFrameMath`, `BurstView`, `CombatWorldPresenter` 근접·회복 단일·폭발(세운)·피격 | .cs 3 + 시험 1 | `-testFilter "EffectFrameMathTests;CombatEffectTests"`. 상태 숫자: 웨이브 1 근접 한 번 동안 `frame` 기록이 1,2,3,4,5 차례(빠짐 0) |
| **F3 투사체 방향·그림자** | `ProjectileView` 방향 그림·반복·그림자 자식, 프리팹 | .cs 1, 프리팹 1, 시험 고침 1(`PooledProjectileViewFacesAllEightDirectionsAndResetsFlip` → 8방향마다 그림 이름·반전 기대, 회전 0) | `-testFilter "CombatEffectTests"`. 회전 Z = 0 검사(8방향 모두) |
| **F4 바닥 표시 래스터** | `GroundMarkRaster`, `EffectSpriteCache`, 회복 범위·파동·폭발 고리, 몬스터 돌진 길·자폭 고리 | .cs 4~5 + 시험 2 | `-testFilter "GroundMarkRasterTests;MonsterViewTests"`: raster.py PNG 4종(`ground_heal_3`·`ground_blast_1`·`dash_line_1`·`ground_pulse_3`, 같은 반지름·각도)과 픽셀 같음. 눕힌 판 local 배율 y = 2·회전 Y = 0 |
| **F5 번개 연결** | `ChainBoltRaster`, Chain 분기 | .cs 2 + 시험 1 | `-testFilter "ChainBoltRasterTests;CombatEffectTests"`(연결 순서 시험 그대로 통과). 끝점 픽셀이 시작·끝 화면 점에서 1도트 안 |
| **F6 적 탄** | `EnemyShotView` 세운 자식·그림자·반복, 팩터리 아틀라스 그림 | .cs 2, 프리팹 1 | `-testFilter "EnemyShotTests"`(있는 이름 grep 후). `PlayableProbe.EnemyShotCount` 흐름 1개 |
| **F7 마감** | 판정 경로 전체 1회, EditMode 전체 1회, **연속 캡처**: 효과 키 12개마다 `ArmEffectSequence`(0.35 s, 1/60 s) → 프레임 번호가 1→N 차례로 모두 나옴, 알파 0/255만, 발광 키는 `glowCore > 0`·칼빛·화살은 0. 캡처 시트(키마다 한 줄, 시간 순서)를 **직접 열어** 움직임을 본다. 1920×1080 전투 캡처 1장(웨이브 3 이상, 효과가 여럿 보이는 순간, 시간 멈춤) | 핸드오프·기준서 개정 후보 글 | 아래 "마감" |

### 마감 (F7)
- 리뷰 묶음 1회: validator-client, spec-reviewer, algorithm-tester(`EffectFrameMath`, `GroundMarkRaster`, `ChainBoltRaster`). FAIL은 고치고 FAIL이던 리뷰만 수정 확인.
- 효과 판독(다른 계열, 읽기만): 게임 연속 캡처 시트를 섞어 번호만 → 종류 맞히기. 그림 단계 판독(r001 PASS 14/15, 돌진 길 fix PASS)과 같은 질문.
- 하지 않음: 300마리·버닝 측정, Bloom·흐림 값 바꾸기(6단계 L3), HP 막대·숫자.

## 6. 위험과 되돌리기

| # | 위험 | 보이는 것 | 대응 |
|---|---|---|---|
| 1 | 실행 중 텍스처 만들기 비용(회복 범위 반지름이 스탯마다 다름) | 첫 발동에 끊김 | 키 캐시(정수 px 반지름) — 한 판에 반지름 종류는 아이템 수 이하. 측정은 7단계. 넘으면 반지름 4 px 칸으로 묶기 |
| 2 | 알파 클립으로 바꾸면 빛 효과가 인물 뒤에 가려짐 | 베기가 몸 뒤로 숨음 | 지금 겹 규칙(깊이)대로가 맞다. 대상 앞으로 보내야 하면 세운 자식 −z 1/48 m(번쩍임과 같은 방법) |
| 3 | 셰이더 심 판정이 다른 팔레트에서 틀림 | 엉뚱한 색이 빛남 | 재질 인스턴스별 세기 + 시험(팔레트 표). 킷에서는 "심 마스크 텍스처" 대안(8절) |
| 4 | 판정 경로가 그림 이름으로 찾는 곳 | 효과 흐름 8개 FAIL | `MarkShown`·`EffectVisibleCount`를 키로(4절). 기대식 문자열은 그대로 |
| 5 | 되돌리기 | | 데이터 `combat_effects.json`의 키만 옛 그림으로 돌리면 한 장 표시로 돌아가도록 `frames` 기본 1 |

## 7. 다른 단계와 겹치는 파일 (디렉터 순서 결정용)

| 파일 | FX | 5단계 배경 | U단계 UI(워크트리) | 6단계 빛 |
|---|---|---|---|---|
| `App/PlayableProbe.cs` | 고침(키·연속 캡처) | B 단위에서 고침 | U0·U7 고침 | L 단위 진단 |
| `tools/playable/art-insert-flows.json` | 흐름 더함 | 더함 | 더함 | 더함 |
| `Resources/Prefab/MonsterView.prefab`, `MonsterView.cs` | 예고 표시 | B6 그림자 | — | — |
| `Characters.spriteatlasv2` | 폴더 더함 | B3 식물 | U0 UI 아틀라스(따로일 수 있음) | — |
| `Main.unity` | **안 바꿈**(프리팹만) | 바꿈 | 이행 메뉴 | 볼륨 |
| URP 볼륨·렌더러 | 안 바꿈(재질만) | — | — | L3 Bloom |
| `docs/art/아트-기준서.md` | 3.3·3.10 개정 후보 글만(핸드오프) | 고침 | 3.13 | 3.12 |

- 권장 순서: **FX는 5단계 B9 마감 커밋 뒤, 6단계 L3(Bloom 확인) 전.** L3이 "불빛·마법에만 걸림"을 보려면 문턱을 넘는 효과가 있어야 한다(지금은 하나도 없음). U단계와는 장면을 안 건드려 **워크트리 나란히 가능**(개선 기록 5.1 고르기 표: 서로 재지 않음, 코드 폴더 겹침은 `PlayableProbe.cs`·flows 두 곳 — 합칠 때 둘 다 "더하기"라 충돌은 줄 단위).

## 8. 기준서 개정 후보 (디렉터가 기준서에 옮김 — 이 작업은 기준서를 고치지 않았다)

- 3.3 효과 행: "10 / 변환" → "효과 **14 키**(+파동·적 탄·돌진 길·자폭 고리·폭발 고리·그림자 점) / 세운 판 프레임 + 실행 중 바닥 도트".
- 3.10 새 행: 효과 단발 = 지속 ÷ 장 수(근접·회복 5장 20 fps, 폭발 6장 24 fps, 피격 3장 20 fps), 투사체 반복 3장 12 fps, 적 탄 3장 8 fps, 예고 2~3장 8~12 fps. 사라짐은 쪼개짐, 알파 페이드 없음.
- 3.12 Bloom 행에 "넘는 것: 흰 심 × 2.4(불·마법·회복·번개·피격), 넘지 않는 것: 칼빛·화살·바닥 고리(≤ 1.6)·적 탄(1.6)".
- 3.1 세운 판 행에 "눕힌 판은 화면 공간 그림 + local y 1/sinθ".

## 9. 킷으로 옮길 것 — "효과 HD-2D 변환 절차" (2.5D 팩, 개선 기록 A24)

게임과 무관한 절차. 2D/3D 게임을 HD-2D(도트 세운 판 + 3D 배경 + Bloom)로 옮길 때 효과·투사체·예고 표시에 쓴다.

1. **세기**: 교체 목록 점검표 K5·K11·K12([교체 목록 2절](../../game/hd2d-전환-교체-목록.md))로 런타임 그림·코드 임시 그림·데이터 그림 키를 모두 센다. 한 그림을 다른 용도로 재사용한 곳을 표시.
2. **면 고르기**(효과마다 하나): 세운 판(서 있는 빛·물체) / 눕힌 판(바닥 고리·길·그림자) / 3D 배경 패스(움직이지 않는 지형 효과만) / 후처리 뒤(정보: HP·숫자).
3. **도트 밀도 계산**: 세운 판 1 텍셀 = 1/PPU m. 눕힌 판은 화면 공간으로 그리고 local y = 1/sinθ. 소수 배율·임의 회전이 남은 곳을 코드에서 grep(`localScale`, `Euler(0f, 0f, angle)`, `Quaternion.Euler(0f, -`).
4. **그림 만들기**(무료 생성 기준): 채택 정지 그림을 ×8로 키워 정점 프레임으로 주고 "N장 띠, 3×2 칸 512, 칸 = 64×64 격자 × 8 px, 사라짐은 쪼개짐"으로 생성 → 칸을 고정 크기로 잘라 같은 배율로 `pixelize` → 합집합 캔버스. 방향 그림은 원본을 굽는다(×4 최근접 회전 → ×¼ → 외톨이 지움). 크기가 데이터로 바뀌는 것은 래스터 함수(기준 구현 `raster.py`를 킷 도구로).
5. **빛**: 효과 팔레트에서 심 색만 HDR > Bloom 문턱. 수가 많은 적 효과·바닥 표시는 문턱 아래.
6. **판정**: 그림 단계 = 같은 배율 영웅 옆 프레임 띠 + 디코딩한 GIF를 직접 보고, 다른 계열 판독(번호만, 종류 맞히기). 게임 단계 = 연속 캡처(프레임 번호 1→N) + 상태 숫자(알파 0/255, 심 개수). 정지 한 장 금지.
7. **흔한 실수**(이번에 실제로 나온 것): 정점 상자에 흩어진 불티가 들어가 배율 추정이 커짐(→ 칸 도트 수를 정지 그림 크기로 고정), 외톨이 지우기가 1도트 불티 효과의 끝 장을 비움, 예고 선 하나짜리는 투사체로 읽힘(→ 길 폭·테·화살촉), 합성 미리보기는 깊이가 없어 효과가 인물·소품 위에 그려짐(게임 캡처로 다시 본다).
- 킷에 넣을 도구 후보: `raster.py`(ring·dash·chain·bake_directions·selftest) → `.ai/2d-art/tools/fxraster.py`, `cut_strips.py`(띠 칸 자르기 + 고정 배율 pixelize) → `pixelize.py strip` 하위 명령. 셰이더 `HD2D_EffectGlow`(심 판정) → 2.5D 팩 Unity 패키지.

# HD-2D 전투 효과·투사체·예고 — 그림 기록 (2026-11-18, 하위 작업)

- 사용자 원문(11-18): "지금 현재 hd2d로 변경하는만큼 이펙트나 프로젝트타일등도 모두 교체가 되어야해, 이것도 studio-kit에서 활용할 수 있게금 정리하면서 작업을 진행해줘".
- 설계: `docs/features/hd2d-fx/design.md`. 교체 목록: `docs/game/hd2d-전환-교체-목록.md`.
- 유료 생성 0. Codex 내장 이미지 생성 무료 3건(모두 1회차, exit 0). 레퍼런스 원본은 입력으로 쓰지 않음 — 입력은 **게임에 들어간 채택 정지 그림**(`Assets/Game/03.Resources/Art/AIGenerated/Effects/*.png`, ×8 확대, #ff00ff 바탕)뿐.
- **게임에 아직 넣지 않았다.** 넣기·게임 화면 확인은 Codex 단계(`.director/runs/20261118-hd2d-fx/`).
- 미리보기: `index.html`(로컬 브라우저로 연다), `preview/`.

## 1. 만든 것 (68 PNG — `_work/inventory.txt`에 크기·sha256 앞 16자 전부)

도트 밀도: 모두 **1 텍셀 = 캐릭터 도트 1칸**(세운 판 PPU 48 = 1/48 m). 눕힌 판(`raster/ground_*`, `dash_line_*`, `shadow_dot`)은 **화면 공간으로 그린 그림**이다 — 1 텍셀 = 가로 1/48 m × 깊이 1/24 m, 게임에서 눕힌 자식의 local y 배율 = 1/sin30° = 2(설계 3.2). 알파 0/255만. 외곽선 없음(기준서 3.4 효과 선 0).

| 이름 | 방식 | 프레임 | fps(제안) | 캔버스(도트) | 피벗 | 팔레트(모두 기준서 3.5 안) | 경로 |
|---|---|---|---|---|---|---|---|
| slash_sword | 생성 띠 → 칸 자르기 → pixelize | 5 | 20 (0.25 s) | 44×48 | 가운데 | #ffffff #fff9ed #ffe08a #f8d818 #f8c838 #f8b828 | `frames/slash_sword/` |
| slash_axe | 같음 | 5 | 20 | 38×40 | 가운데 | 위 + #f88808 | `frames/slash_axe/` |
| hit_star | 같음 | 3 | 20 (0.15 s) | 20×20 | 가운데 | #ffffff #fff9ed #ffe08a #f8d818 #f8c838 | `frames/hit_star/` |
| explosion | 같음 | 6 | 24 (0.25 s) | 32×28 | 가운데 | #ffffff #fff9ed #ffe08a #f8d818 #f88808 #d81808 #77211d #a89898 #786868 | `frames/explosion/` |
| heal_single | 같음 | 5 | 20 | 20×26 | 가운데 | #ffffff #fff9ed #ffe08a #a8c828 #7fa040 #7fd0c8 | `frames/heal_single/` |
| fireball | 같음 + 방향 굽기 | 3 × 3방향(오른쪽·오른쪽 위·위) | 12 반복 | 21×9 / 16×16 / 9×21 | 가운데 | #fff9ed #f8d818 #f88808 #d81808 | `frames/fireball_dirs/` (원본 오른쪽 3장 `frames/fireball/`) |
| spore(버섯 적 탄) | 생성 띠 | 3 | 8 반복 | 18×18 | 가운데 | #ffffff #b07fd0 #7a4a9e #e3d7c8 | `frames/spore/` |
| arrow | 채택 정지 그림 방향 굽기 | 1 × 3방향 | — | 30×5 / 20×22 / 5×30 | 가운데 | 채택본 그대로 | `frames/arrow_dirs/` |
| crossbow_bolt | 같음 | 1 × 3방향 | — | 21×6 / 14×16 / 6×21 | 가운데 | 채택본 그대로 | `frames/crossbow_bolt_dirs/` |
| chain_bolt(번개 연결) | **실행 중 그림**(raster.py 기준 구현) | 3 | 12 | 예시 126×44(시작→끝 110×28 px) | 시작점 | #ffffff #b07fd0 #7fd0c8 | `raster/chain_bolt_*` |
| ground_heal(회복 범위) | 실행 중 그림 | 5 | 20 | 예시 188×96(반지름 1.90 m) | 가운데 | #ffe08a #a8c828 #ffffff | `raster/ground_heal_*` |
| ground_pulse(오브 파동) | 실행 중 그림 | 5 | 20 | 예시 114×60(1.13 m) | 가운데 | #b07fd0 #ffffff #7fd0c8 | `raster/ground_pulse_*` |
| ground_boom(폭발 피해 범위) | 실행 중 그림 | 4 | 16 | 예시 86×46(0.85 m) | 가운데 | #f8d818 #f88808 #d81808 #77211d | `raster/ground_boom_*` |
| ground_blast(자폭 범위 예고) | 실행 중 그림 | 2 | 8 깜빡임 | 예시 102×54(1.02 m) | 가운데 | #d81808 #f88808 | `raster/ground_blast_*` |
| dash_line(돌진 예고 길) | 실행 중 그림(r002) | 3 | 12 | 예시 170×77(3.64 m, 바닥 35°) | 시작점(몬스터 발) | #d81808 #f88808 #f8d818 #ffe08a | `raster/dash_line_*` |
| shadow_dot(투사체 그림자) | 실행 중 그림(고정) | 1 | — | 8×4 | 가운데 | #34140e(몬스터 그림자 판과 같은 색) | `raster/shadow_dot.png` |

"예시"의 반지름은 게임 데이터 × `unitScale` 0.0113: 향로 `range 4 × rangeUnit 42 = 168`, 오브 `effectRadius 100`, 복어 `blastRadius 90`, `explosionRadius 75`, 멧돼지 돌진 `460 × 0.7`. 스탯 보너스가 붙으면 반지름이 바뀌므로 게임은 실행 중 반지름마다 같은 규칙으로 그린다(설계 4절).

## 2. 생성 기록

- 명령(3건 동시, 약 4분): `perl alarm 1200 + codex exec -m gpt-6.1-sol -c model_reasoning_effort="medium" -s workspace-write -C gen/<묶음> "$(cat prompt.txt)" -i ref_*.png < /dev/null`. 프롬프트 전문: `gen/<melee|fire|magic>/prompt.txt`(공통 꼬리 `gen/common.txt`). 로그 `gen/*/run.log`.
- 입력(채택 정지 그림 sha256 앞 16자): slash_sword 3a54b93d9d831f0a, slash_axe 2eaee39697f4a6b5, hit_star c3f19e8cf9cea60e, explosion 9e9b9e5866de6a7c, fireball 2328f1a34b60116a, heal_single 72afaa68331138d9. 만드는 법 `tools/prep_refs.py`.
- 지시 형식: 1536×1024, 3×2 칸(512), 칸 = 64×64 격자 × 8 px 블록(입력 ×8과 같은 블록), 정점 프레임 = 입력 그림 그대로, 사라짐은 투명이 아니라 쪼개짐.
- 결과(sha256 앞 16자): sword_strip 32ff922ec044dceb, axe_strip 305460f1d6fd3356, star_strip 4bf0fea3d932be55, explosion_strip af378eda441e7091, fireball_strip b589ec9fb4619000, heal_strip 7991ca01c416e6a1, spore_strip 03b2048c821ba959.
- Codex 자체 보고: 팔레트·8 px 블록을 정확히 지키지 못함(`gen/magic` "규격 미달", `gen/fire` FAIL 문구). 그래서 도트 정리로 맞춘다(아래).

## 3. 도트 정리

- `tools/cut_strips.py`: 칸을 고정 512 px로 자르고(프레임 사이 크기 관계 유지), 킷 `pixelize.pixelize`(블록 최빈색 + 팔레트 맞춤)로 내림. 칸 도트 수 = 512 ÷ (정점 프레임 그림 폭 px ÷ 채택 정지 그림 폭 도트) — 정점 프레임이 정지 그림과 같은 크기가 되게. hit_star·heal_single은 멀리 흩어진 불티가 정점 상자에 들어가 추정이 커서(첫 시도 12×12·14×16, 판독 전 직접 보고 "너무 작고 끝 장이 빔") 칸 도트 수를 34·44로 정함. 외톨이 1도트는 지움(불티가 본래 1도트인 hit_star·heal_single 제외). 모든 프레임 합집합 상자로 같은 캔버스(짝수)로 자름.
- `tools/bake_dirs.py` + `raster.bake_directions`: 오른쪽 원본을 ×4 최근접 확대 → 45°·90° 회전 → ×¼ 최근접 → 알파 0/255 → 외톨이 지움 → 같은 효과의 프레임 합집합으로 자름. **실행 중 Z 회전을 대신**한다(대각선 텍셀 없음).
- `tools/raster.py`(selftest 8건 PASS): 바닥 타원(반지름 m → 화면 반축 48r × 24r px, 테 두께 위아래도 같은 도트 수), 돌진 길, 번개 꺾은선, 그림자 점. Unity 순수 함수의 기준 구현.
- 팔레트 밖 색 0(모든 PNG가 지정 부분 팔레트만 씀 — pixelize 팔레트 맞춤의 결과). 확인 안 한 것: 킷 `Tools/check_sprites.py` 실행(넣기 단계 F0에서).

## 4. 직접 열어 본 것 (art-core-rules 6)

| 파일 | 본 것 |
|---|---|
| `output/hd2d-screen/battle-main.png` | 4단계 본 게임 캡처. 피격 별이 위 흐림 띠에서 흐린 흰 점으로만 보임(×2 잘라 봄 `_work/battle-main-crop.png`) |
| `output/hd2d-background/plates/Main1-main.png` | 최신 배경 판. 효과 없음 — 합성 바탕으로 씀 |
| `_work/strips_overview.png` | 생성 띠 7장 모두: 칸 배치 지킴, 장검 f4가 왼쪽으로 커짐, 회복 f1이 f2보다 큼 |
| `preview/frames_x4.png`(두 조각 `_work/fx4_*.png`) | 영웅 옆 같은 배율로 모든 프레임. 처음 hit_star·heal_single 너무 작고 끝 장 빔 → 고침 뒤 다시 봄 |
| `_work/gif_decoded.png` | GIF 4개(장검·폭발·회복 단일·회복 범위)를 디코딩해 순서대로: 장검 시작→2/3→정점→가늘어짐→조각, 폭발 점화→부풂→정점→불꽃→고리 조각+연기→연기. 프레임 시간 50/40 ms. 나머지 12 GIF는 킷 디코딩 검사(색 차 ≤ 1.93, 벗어난 픽셀 0%)만 |
| `raster_check`·`_work/dash_check.png` | 바닥 고리 4종·번개·돌진 길. 돌진 길 r002 첫 그림의 갈매기가 뒤를 향함 → 고침 → 앞 방향 확인 |
| `preview/composite_main1_center_x2.png`, `review/fix_dash_composite.png` | 합성(깊이 없음): 회복 고리가 대원 발밑 4명을 덮음, 장검 베기·별이 레나 위, 번개·화살·볼트·포자·자폭 범위·돌진 길이 들판에서 또렷함. 합성이라 효과가 인물·소품 위에 그려짐 — 게임에서는 깊이로 가려짐 |

## 5. 다른 계열 판독 (Codex gpt-6.1-sol, 읽기만)

- r001(`review/hd2d-fx-read-r001.md`): 15개를 섞어 번호만(`review/blind_map.json`), 영웅과 같은 배율 + 합성. **VERDICT: PASS** — 15개 모두 전투 효과로 읽힘, 종류 맞힘 14/15. 틀린 것: **#12 돌진 예고 선 → "긴 붉은 궤적의 화염 투사체"**. 그 밖 지적(필수 아님, 후속): 고리 셋(#1 회복·#10 폭발·#11 파동)이 서로 닮음, 장검/도끼·화살/볼트 구분 약함, 고리가 인물·HP 막대를 가로지름(게임에서는 바닥 겹 + 깊이 + HP 막대는 후처리 뒤라 위에 그려짐 — 게임 캡처로 다시 볼 것), 번개 시작·끝 표시 없음.
- 고침: 돌진 길 r002(양옆 점선 테 + 안쪽 갈매기 흐름 + 넓은 화살촉, 3장).
- 수정 확인 fix-r001(`review/hd2d-fx-read-fix-r001.md`): "돌진 방향·경로를 알려 주는 바닥 경고 화살표", "투사체로는 거의 읽히지 않음", **VERDICT: PASS**. 남은 말: 빨강·노랑이 주변보다 또렷함(경고로 적절), 꼬리 끝 짧은 조각이 조금 거침.

## 6. 기준서와 다른 점 (art-core-rules 6 "기준서 번호와 다른 점")

- 기준서 3.3 효과 행은 "10 / Codex + pixelize, **변환**"이다. 이번 것은 **프레임 애니메이션 + 실행 중 그림**으로 바꾼다 → 기준서 3.3·3.10 개정 후보(설계 8절). 3.10에는 효과 행이 없다.
- 새 그림: 포자(적 탄)·바닥 고리 4종·돌진 길·그림자 점·번개 연결(실행 중) — 3.3 표에 없음(개정 후보).
- 회복 단일 프레임 f1이 f2보다 크다(생성 그대로, 판독은 "작은 빛 → 큰 반짝임 → 흩어짐"으로 읽음). 사용자 판정 항목.
- 포자 18×18은 지시(12)보다 크다. 영웅 키의 약 3분의 1. 사용자 판정 항목.

# 에셋 규격(모바일)과 정식 제작 경로

게임에 들어가는 3D 에셋(캐릭터·몬스터·소품·맵 재질)에 적용한다. [핵심 규칙](core-rules.md)이 우선한다. 근거는 [050 설계](../design/asset-standard-050.md)·[050 기록](../reviews/asset-standard-050.md)이다. 2026-11-03 사용자 요청: "규격표 숫자의 경우 모바일에 최적화된 형태로 정리를 해줘, 캐릭터도 우선 팔레트를 해주면 좋을 것 같고 3번은 후자로 해줘".

- **숫자의 원본은 [`docs/art/asset-catalog.json`](../art/asset-catalog.json)의 `standards`다.** 아래 표는 `/usr/bin/python3 Tools/Art/assets.py spec --markdown`의 출력이다. 숫자를 바꾸면 JSON을 고치고 표를 다시 뽑는다.
- 같은 파일의 `assets`가 **채택 목록**이다. 빌드 장면은 목록에 등록된 폴더의 에셋만 쓴다.

## 1. 규격표

근거(2026-11-03, `Map046_Full`): 직교 카메라 크기 6, 55°. 영웅(높이 0.8)은 1080p 폰에서 약 41px, 몬스터(0.52)는 약 27px다. 텍스처 256이면 영웅 몸이 화면보다 약 3배 촘촘하고, 몬스터는 128이면 충분하다. 삼각형은 [성능 예산](performance.md)(7웨이브 20만, 적 1마리당 1,000)에 맞췄다.

| 종류 | 삼각형 목표/상한 | 텍스처 | 뼈 | 재질 | 그림자 | 색 |
|---|---|---|---|---|---|---|
| 영웅 | 1,500 / 2,000 | 256² | 70 | 1 | 실제 | 텍스처 팔레트화 (char) |
| 몬스터 | 600 / 1,000 | 128² | 0 | 1 | 바닥 원 | 텍스처 팔레트화 (char) |
| 보스(예정) | 2,500 / 4,000 | 256² | 70 | 1 | 실제 | 텍스처 팔레트화 (char) |
| 작은 소품(높이 0.5 미만) | 200 / 600 | 없음(팔레트 칸 UV) | 0 | 1 | 없음 | 면→팔레트 칸 (env) |
| 큰 소품(나무·큰 바위) | 800 / 1,500 | 없음(팔레트 칸 UV) | 0 | 1 | 실제 | 면→팔레트 칸 (env) |
| 랜드마크(맵당 3개 이하) | 2,000 / 3,000 | 256² | 0 | 1 | 실제 | 텍스처 팔레트화 (env) |
| 맵 생성 메시·지형 재질 | 3,000 / 5,000 | 없음(팔레트 칸 UV) | 0 | 1 | 실제 | 면→팔레트 칸 (env) |

- 목표는 Tripo `face_limit`의 기본값이다. 상한을 넘으면 Blender로 줄인다([성능 절차](performance.md) 3절).
- 공통 규칙:
  - **원점**은 발밑 가운데다.
  - **정면**은 +Z이고, 프리팹 안 `Visual` 아래 모델에 굽는다. 그래서 액터 세트의 `yawOffset`은 0이다. `DSMapActorViews`가 `Visual` 회전을 매 프레임 덮어쓰기 때문이다(046-15·S3-21 방향 사고 방지).
  - **프리팹 구조**: 루트(발) → `Visual` → 높이 1로 맞춘 모델.
- 텍스처 가져오기:
  - 밉맵 켬, Bilinear, Clamp.
  - 알파는 없음. 셰이더는 A=1을 "발광 없음"으로 읽는다.
  - Android·iPhone은 **ASTC 4×4**, 크기는 규격값 그대로.
  - 팔레트 PNG는 Point 필터, 밉맵 없음.
- 허용 셰이더:
  - `DeadSurvivor/LowPolyPalette`: 모든 고체·캐릭터
  - `DeadSurvivor/Water045`: 물
  - URP Unlit, Particles/Unlit: 바닥 원 그림자, 피격 깜빡임, 효과

## 2. 색: 팔레트가 유일한 색 원본

| 방식 | 언제 | 하는 일 |
|---|---|---|
| **면 → 팔레트 칸 UV**(043) | 소품·지형. 텍스처 세부가 필요 없는 것 | 면마다 팔레트 칸 하나를 UV로 가리킨다. 텍스처 없음 |
| **텍스처 팔레트화**(050) | 캐릭터·랜드마크. 눈·얼굴처럼 텍스처에만 있는 세부가 있는 것 | 규격 크기로 줄인 뒤 모든 텍셀을 팔레트의 가장 가까운 색(OKLab)으로 바꾸고, 3×3 다수결로 점 얼룩을 지운다. 재질은 공용 셰이더, `_Palette` 칸에 이 텍스처, 윗면 투영(`_TopStrength`)은 0 |

팔레트는 두 장이다.
- 환경: `palette-r001`. 043에서 만들었고 풀·흙·돌·물 4행이다.
- 캐릭터: `palette-char-r001`.
  - 0행은 영웅 정체성 6색으로, 게임 데이터 `characters[].color`에서 왔다.
  - 나머지 26색은 캐릭터 텍스처 11장에서 뽑았다.
  - 영웅 텍스처에서 자기 정체성 색 근처(OKLab 0.06 이내)인 텍셀은 항상 그 정체성 칸으로 간다.

환경 팔레트에 행을 더하면 기존 UV가 가리키는 칸이 바뀌므로, 팔레트를 바꿀 때는 새 개정(r002)을 만든다.

## 3. 정식 경로 `Tools/Art/assets.py`

Unity 편집기를 이 프로젝트로 열어 둔 채 프로젝트 루트에서 `/usr/bin/python3 Tools/Art/assets.py <명령>`으로 실행한다.

| 명령 | 하는 일 |
|---|---|
| `spec` / `list` | 규격표 / 등록 목록 |
| `gen <freeze\|dryrun\|submit\|fetch\|status\|reconcile> <자산>` / `gen anim <자산> <단계>` | Tripo 생성. `tripo_045.py`의 규칙(잠금, 재개 일지, 상한·예약, 고정 요청의 드라이런, 네트워크 오류 재전송 금지)을 그대로 옮겼다(`Tools/Art/asset_tripo.py`). **카탈로그 `budgets`에 사용자 승인 원문과 상한이 없거나, 그 예산의 `assets` 목록에 자산이 없으면 `freeze`가 거부한다**(새 에셋은 새 예산을 만든다. 옛 승인을 물려받지 않는다) |
| `palette-char [--force]` | 캐릭터 팔레트 생성. 바꾸면 모든 캐릭터 색이 바뀐다 |
| `prep <id…>\|--all` | 텍스처 팔레트화 → `output/assets/<id>/prep/` |
| `unity <id…>\|--all` | Unity(`Tools/Art/AssetBuild.cs`): 텍스처 가져오기, 공용 셰이더 재질, 몬스터 프리팹, 액터 세트 항목, 영웅 기본 프리팹의 기본 재질 |
| `check` | 등록 에셋과 빌드 장면을 재서 규격과 대조한다. 빌드 장면 의존 파일에 시험 폴더(`Scenes/Proto*`, `Art/Tests` 등)·미등록 에셋·허용 밖 셰이더·256 초과 텍스처가 있으면 FAIL |
| `capture [--tag T]` | 게임 각도·정면 줄 세우기(`output/assets/<T>-lineup_*.png`). 영웅은 편집 모드에서 묶음 자세라 옆을 보므로 짧은 Play 캡처로 본다 |
| `migrate <이름>` | 카탈로그 `migrations`의 일회성 이동(GUID 유지) |
| `selftest` | 오프라인 시험. Tripo 반례는 `Tools/Art/selftest_asset_tripo.py` |

**새 에셋 하나를 넣는 순서**
1. 카탈로그에 항목을 추가한다: `kind`, `folder`, `source` 또는 `tripo`, 필요하면 `actor`.
2. 유료 생성이면 `budgets`에 사용자 승인 원문과 상한을 적는다.
3. `gen freeze → dryrun → submit → fetch`로 생성하고, 동작이 필요하면 `gen anim`을 돌린다.
4. 삼각형이 상한을 넘으면 Blender로 줄인다.
5. `prep` → `unity` → `check`를 돌린다.
6. `capture`와 짧은 Play 1회로 확인하고, 사용자가 화면을 본다.
7. 성능 한 줄을 잰다([핵심 규칙](core-rules.md) 1절).

## 4. 위치와 소유

| 위치 | 소유(만드는 도구) |
|---|---|
| `Art/Characters/Heroes/<영웅>/`, `Art/Characters/Monsters/<몬스터>/`, `Art/Characters/ActorSet_Main.asset` 내용, 영웅 기본 프리팹의 기본 재질 | `assets.py unity` |
| `Art/Characters/Heroes/hero_base/`(모델·애니메이션·프리팹), `Art/Characters/Shared/`(바닥 원 그림자·피격), `Art/Environment/Map/`, `Art/Environment/Props/` | 050에서 옮긴 기존 에셋. 내용은 그대로 유지. 바닥 원 그림자 재질과 액터 세트의 `MonsterBlobShadow` 지정은 049 값 그대로이며 다시 만드는 도구는 없다(`build_monsters_046.cs` `ApplyPerf`는 거부) |
| `Art/Characters/Hero047/`, `Art/Fx047/` | `Tools/Map/build_fx_047.cs`(폴더를 지우고 다시 만든다) |
| `Scenes/Map046/` (`Map046_Full.unity`, 맵 생성 메시) | `Tools/Map/build_map_046.cs`. 장면 수정은 이 빌더만 한다(050 단계는 `Apply050`) |
| `Scenes/Proto*`, `Art/Tests`, `Art/Characters/Test042_r001`, `Art/Environment/{ENV_S01,TripoPair038*}` | 시험 폴더. 빌드 장면에서 참조하지 않는다 |

- `build_monsters_046.cs`는 050에서 정식 경로로 대체됐고, 실행하면 거부한다.
- `Tools/Proto/tripo_045.py`도 이제 안내만 출력한다.
- 045 시험 장면은 옮긴 에셋을 같은 GUID로 계속 쓴다. 045 빌더는 동결되어 있어 다시 돌리지 않는다.

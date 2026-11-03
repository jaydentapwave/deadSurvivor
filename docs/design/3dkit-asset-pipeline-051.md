# 051 3dkit v1 — 에셋 파이프라인(공통 뼈대 + 프로필)

- 상태: 구현 중 — Python 쪽 완료(selftest PASS), Unity 쪽(패키지·훅)과 회귀 남음. 설계 검토 반영(7절)
- 규모: **L2**. 카탈로그 스키마 v1 → v2, 에셋 소유 도구 변경(`Tools/Art/*` → `3dkit/`), Unity 쪽 코드 위치 변경(`script run` 파일 → 로컬 UPM 패키지 + 프로젝트 훅).
- 사용자 요청(2026-11-03): "응 그러면 우선 에셋도구부터해서 파이프라인을 모두 업데이트를해줘". 바로 전 대화의 합의: 뼈대(카탈로그·Tripo 생성 가드·Unity 가져오기·규격 대조·캡처)는 공통, 사양에 따라 달라지는 단계(텍스처 처리·크기·재질·감량·LOD·뼈대/동작)는 **프로필**(lowpoly / midpoly)로 갈아 끼운다.
- 해석(인수인계 [현재 상태](../현재상태.md) "다음 할 일"): 3dkit은 이 저장소에서 디벨롭한다. 이미 돌아가는 050 경로·장면·에셋을 시험대로 쓴다.

## 1. 결과와 완료 조건

| 결과 | 완료 조건 | 구분 |
|---|---|---|
| `3dkit/tools/assets.py`(새 정식 경로) | Deadsurvivor 에셋을 기존 `Tools/Art/assets.py`와 **같은 결과**로 만든다: ① `check` 등록 20·빌드 장면 2·문제 0, 측정값(삼각형·뼈·재질·텍스처 형식·셰이더) 동일 ② `prep` 11종 `albedo.png` 해시 동일 ③ `unity --all` 뒤 `Assets/DeadSurvivor/Art` git 변경 없음(또는 옛 도구도 같은 변경을 냄) ④ `capture` 이미지가 같은 조건의 옛 도구 캡처와 픽셀 동일 ⑤ Tripo 고정 요청 9건의 `planned_args`·콘셉트 해시가 카탈로그 v2 + 프로필에서 그대로 재현 ⑥ 짧은 Play 1회 PASS | 정식(Deadsurvivor) |
| 프로필 전환 | `3dkit.config.json`의 `assets.profile` 값 하나(또는 `--profile`)로 lowpoly ↔ midpoly가 바뀐다. midpoly는 **오프라인 selftest 범위**: 규격표·prep(passthrough)·Unity 요청(셰이더·텍스처 칸·노멀맵·Mesh LOD)·판정이 midpoly 값으로 나옴 | 시험(selftest) |
| `3dkit/unity/`(로컬 UPM 패키지 `com.studiokit.kit3d`) | Deadsurvivor `Packages/manifest.json`에 `file:../3dkit/unity`로 연결, 컴파일 오류 0 | 정식 |

완료 조건 밖: midpoly 실제 Tripo 생성(크레딧 승인 필요, 잔액 약 285), midpoly 규격 숫자 확정(대상 플랫폼·성능 예산 미정), 감량(Blender) 자동화, H0~H2·H4 하네스 도구(guard·probe·protect·play_check·tier·gate) 이식.

## 2. 범위

**한다**
1. `NewProjectKit/3dkit` → 저장소 루트 `3dkit/`로 옮긴다. [하네스 설계도](../../3dkit/workflows/harness-blueprint.md)의 배치(`<루트>/3dkit`, `file:../3dkit/unity`)와 같아지고, 키트 문서의 명령(`python3 3dkit/tools/...`)이 이 저장소에서 그대로 돈다. `NewProjectKit/`의 나머지(새 게임 자료)는 그대로 두고 README에 "디벨롭 중 키트는 저장소 루트 `3dkit/`"를 적는다(그 안의 `3dkit/...` 링크는 키트를 복사해 넣은 뒤에 맞는다).
2. Python 도구 `3dkit/tools/`, 프로필 `3dkit/profiles/{lowpoly,midpoly}.json`, Unity 패키지 `3dkit/unity/`, 프로젝트 설정 `3dkit.config.json`(저장소 루트), 카탈로그 v2(`docs/art/asset-catalog.json` 교체), Deadsurvivor 훅 `Assets/DeadSurvivor/Editor/Kit3DProjectHooks.cs`.
3. 회귀가 모두 맞으면 옛 도구를 은퇴: `Tools/Art/assets.py`는 새 경로 안내만 하고 거부(045의 `tripo_045.py` 방식), `AssetBuild.cs`·`asset_tripo.py`·`asset_texture.py`·`selftest_asset_tripo.py`는 지운다(원본은 git과 `3dkit/reference/`에 있다). 안내 문구가 옛 경로를 가리키는 `Tools/Proto/tripo_045.py`·`selftest_tripo_045.py`·`Tools/Map/build_monsters_046.cs`도 새 경로로 고친다.
4. 문서: 프로젝트 [에셋 규격](../workflows/asset-standard.md)(명령·숫자 원본 위치), 키트 `3dkit/workflows/asset-standard.md`(단계·프로필 일반 규칙), `3dkit/README.md`(v1 상태), 하네스 설계도 H3, [개발 워크플로우](../workflows/development.md) 에셋 줄, [개발 환경](../개발환경.md), [현재 상태](../현재상태.md).

**하지 않는다(후속)**: 감량 자동화(Blender), lowpoly 소품의 면→팔레트 칸(043 `repaint_tripo_043.py`)을 단계로 편입, 셰이더를 패키지로 옮기기(GUID·이름 위험), midpoly 실제 생성·규격 확정, 다른 하네스 도구 이식, 두 번째 프로젝트 이식 시험(v3).

## 3. 구조

```
3dkit/
├── tools/
│   ├── assets.py            CLI: spec · list · profile · gen · palette · prep · unity · check · capture · migrate · selftest
│   ├── kitlib/
│   │   ├── config.py        3dkit.config.json 찾기(현재 폴더에서 위로), 프로필 로드·병합, 경로 규칙
│   │   ├── catalog.py       카탈로그 v2 읽기·검증, 자산 고르기
│   │   ├── texture.py       텍스처 단계: palette(050 코드 그대로) | passthrough(크기 맞춤 + 추가 맵)
│   │   ├── tripo.py         생성 단계(asset_tripo.py 규칙 그대로, 경로·인자·승인은 설정·프로필·카탈로그에서)
│   │   ├── unity.py         Unity 연결: 요청 JSON 쓰기 → script eval → 결과 JSON 읽기
│   │   └── pipeline.py      build 요청 만들기, check 판정, capture 요청
│   └── selftest_tripo.py    Tripo 가드 오프라인 반례(selftest_asset_tripo.py 이식)
├── profiles/
│   ├── lowpoly.json         Deadsurvivor 050 값 그대로(모바일)
│   └── midpoly.json         시안(숫자 미확정, selftest 용)
└── unity/                   로컬 UPM 패키지 com.studiokit.kit3d (Editor 전용 asmdef Kit3D.Editor)
    ├── package.json
    └── Editor/  AssetPipeline.cs (Move·Build·Check·Capture)  IKit3DProjectHooks.cs
```

### 3.1 단계와 입출력

| 단계 | 입력 | 출력 | lowpoly | midpoly(시안) | 구현 |
|---|---|---|---|---|---|
| generate | 콘셉트 이미지, 카탈로그 `tripo` 블록, 예산(승인 원문·상한) | `<work_dir>/raw/*`, `journal.json` | `smart_low_poly=true`, `pbr=false`. 면 한도는 자산의 `tripo.face_limit`가 우선이고, 새 자산은 규격 목표를 기본으로 적는다 | `smart_low_poly=false`, `pbr=true`, `texture_quality=detailed` | `kitlib/tripo.py` |
| anim | 생성 작업 ID | 뼈대·동작 FBX | 기본 단계 check·rig·retarget(idle·run, 예산 승인으로 진행) + 프로젝트 단계(카탈로그 `anim_steps`, 예: attack — **자기 예산의 `step_approvals[단계]` 원문이 있고 자산이 그 예산 `assets`에 있을 때만**, 예산 승인으로 대신하지 않음. 기본 단계와 같은 이름은 거부) | 같음 | `kitlib/tripo.py` |
| decimate | 모델 | 감량 모델 | `none` | `blender`(**후속**, 지금은 상한 초과 시 check FAIL로 알림) | 도구 밖 |
| texture (`prep`) | `source.texture`(+`source.maps`) | `<work_root>/<id>/prep/{albedo,normal…}.png`, `prep.json`(해시) | `palette`: 규격 크기 → OKLab 최근접 팔레트 칸 → 3×3 다수결, 정체성 색 고정 | `passthrough`: 규격 크기로 줄이기(albedo sRGB, normal 선형), 맵별 해시 | `kitlib/texture.py` |
| import | prep 텍스처, 모델 | Unity 텍스처·FBX | 텍스처 Bilinear·Clamp·밉맵·알파 없음·모바일 ASTC 4×4·PC Automatic, 모델 재질 가져오기 없음·애니 없음 | + 노멀맵 형식, 노멀 가져오기 | `AssetPipeline.cs` Build |
| material | 텍스처 | `.mat` | 셰이더 역할 `palette_lit` → 설정에서 `DeadSurvivor/LowPolyPalette`, 칸 `albedo→_Palette`, 프리셋 실수값, `_FlatShading`↔`_FLAT_SHADING` | 역할 `lit` → `Universal Render Pipeline/Lit`, `albedo→_BaseMap`, `normal→_BumpMap`(+`_NORMALMAP`) | Build |
| prefab | 모델, 재질 | `.prefab` | 루트(발) → `Visual` → 높이 1 모델, 정면 +Z 굽기, 그림자 규격값, 빛 프로브 끔 | 빛 프로브 `BlendProbes` | Build |
| lod | 모델 | — | `none` | `mesh_lod`: 규격에 `lod: true`인 종류만 `ModelImporter.generateMeshLods`(Unity 6000.2+, 6000.4 DLL에서 확인) | Build |
| register | 만든 재질·프리팹 | 프로젝트 데이터(액터 세트 등) | 프로젝트 훅 | 같음 | 프로젝트 C# 훅 |
| check | 카탈로그, 빌드 장면 | `check-report.json` | 규격 숫자 + 셰이더·텍스처 형식·정규화·그림자·시험 폴더·미등록 미술·장면 텍스처 최대 256 + 훅 문제 | 같은 규칙, 장면 텍스처 최대 1024 | Check(측정) + `pipeline.py`(판정) |
| capture | 훅이 준 줄 세우기 목록 | `<work_root>/<tag>-lineup_{game,front}.png` | 050과 같은 카메라·배경 | 같음 | Capture |

### 3.2 설정 세 겹 (값의 원본은 한 곳씩)

| 파일 | 소유 | 담는 것 |
|---|---|---|
| `3dkit/profiles/<이름>.json` | 키트 | 스타일 값: 단계 방식, 종류별 규격 숫자(`standards`), 재질 프리셋·텍스처 칸, 가져오기 설정, 검사 규칙(장면 텍스처 최대, 고정 재질 속성), Tripo 모델·인자 |
| `3dkit.config.json`(프로젝트 루트) | 프로젝트 | `assets.profile`(**전환 값**), 카탈로그 경로, `content_root`·`art_root`·`work_root`, 셰이더 역할 → 실제 이름, 캡처 배경, Tripo CLI·상태 폴더·키 파일 |
| 카탈로그 v2(`docs/art/asset-catalog.json`) | 프로젝트 | 자산 목록(채택 목록), 예산·승인 원문, 팔레트 파일, 시험 폴더·허용 셰이더·이름 규칙·훅 데이터, 프로젝트 동작 단계(`anim_steps`), 이동 목록, 선택적 `standards_override` |

- 규격 숫자의 원본은 **프로필**이다. 카탈로그 `standards_override[kind]`가 있으면 그 키만 덮는다(Deadsurvivor는 비움). 그래서 프로필 값 하나를 바꾸면 숫자도 함께 바뀐다. `assets.py spec`은 최종 값과 출처를 출력한다.
- lowpoly 숫자는 050의 Deadsurvivor 모바일 측정값이다(영웅 41px, 몬스터 27px @1080p). 다른 카메라의 프로젝트는 `standards_override`로 덮는다.
- 셰이더는 프로필이 역할 이름만 갖고, 실제 이름은 설정이 정한다(`DeadSurvivor/LowPolyPalette`는 이 프로젝트 파일이다). 키트 기본값: `lit` → `Universal Render Pipeline/Lit`.

### 3.3 카탈로그 v1 → v2 대응

| v1 | v2 |
|---|---|
| `schema: ds-asset-catalog/1` | `3dkit-asset-catalog/2` |
| `standards`(종류별 숫자) | lowpoly 프로필 `standards`로 이동(값 그대로). 카탈로그에는 `standards_override: {}` |
| `presets.character/landmark` | 프로필 `material.presets`. 종류 → 프리셋은 프로필 `standards[kind].preset` |
| `common.texture_import/palette_import/mesh_import/pivot/front/prefab` | 프로필 `import`·`prefab`(설명 문자열은 키트 asset-standard 문서로) |
| `common.shader_allowed`, `trial_roots`, `scene_generated_roots` | 카탈로그 `project.shader_allowed`·`trial_roots`·`scene_generated_roots`(프로필 재질 셰이더는 자동 허용) |
| `tripo.cli`, `new_work_root` | 설정 `tripo.cli`·`tripo.new_work_root` |
| `tripo.model`, `params` | 프로필 `generate.tripo.model`·`params` |
| `budgets` | 그대로 + `budgets["045-s3"].step_approvals.attack`(옛 `asset_tripo.py` 코드에 박힌 047 승인 원문을 데이터로. 옛 코드는 이 승인을 모든 자산에 돌려줬다: 051 M2) + `legacy_scope`(045/046 고정 요청의 `scope` 값 `045_STAGE2/3/4`, 고정 요청이 지금 예산에 속하는지 대조용) |
| 코드의 `ANIM['attack']` | 카탈로그 `anim_steps.attack`(retarget, `preset:slash preset:shoot`, 예상 20) |
| `assets[].method: palette_texture` | `build`(텍스처 방식은 프로필 `standards[kind].color`가 정함) |
| `assets[].method: existing_palette_uv` | `existing_model` |
| 그 밖의 method(`existing_rig`·`style`·`fx`·`builder_owned`·`shared`·`actor_set`·`none`) | 그대로 |
| `hero_base`(기본 프리팹) | + `default_variant: "hero_bruno"`(옛 코드의 `hero_default_item`) |
| 이름 규칙(코드의 `Hero_`·`Monster_`) | 카탈로그 `project.naming`: `hero` → 재질 `Hero_{name}`; 나머지 → 재질·프리팹 `Monster_{name}`; 텍스처 `{name}_albedo` |
| 액터 세트 경로(코드) | 카탈로그 `project.hooks.actor_set: "actor_set"`(자산 ID) → 훅 요청에 경로로 전달 |
| `migrations`, `measured`, `note_050_rock`, `palettes` | 그대로(`note_050_rock` → `notes.050_rock`) |
| 경로 규칙: 접두어 없으면 `Assets/DeadSurvivor/` 아래 | 설정 `assets.content_root` 아래(값 `Assets/DeadSurvivor`) |

이행: v2는 일회성 변환 스크립트(작업 폴더)로 만든다. 회귀가 끝날 때까지는 옛 도구가 v1을 읽어야 하므로 v2를 `docs/art/asset-catalog.v2.json`에 두고, 회귀 PASS 뒤 v1 자리로 옮긴다(v1은 git 이력에 남는다). 새 도구 `selftest`가 "v1의 각 값이 v2 + 프로필에서 같은 값으로 나온다"를 한 번 대조한다(변환 직후 1회, v1 파일이 있을 때만).

### 3.4 Unity 쪽: 로컬 UPM 패키지 + 프로젝트 훅

- 패키지 `com.studiokit.kit3d`(표시 이름 3dkit, `unity: 6000.2`, 의존 `com.unity.nuget.newtonsoft-json`), Editor 전용 asmdef `Kit3D.Editor`(autoReferenced). 프로젝트의 `Assembly-CSharp-Editor`가 자동으로 참조한다(이 프로젝트에는 asmdef가 없다).
- 호출: `unity command script eval --project-path <루트> --format json --timeout 900 -- 'return Kit3D.AssetPipeline.Run("<Entry>", "<요청 절대 경로>");'`. 요청·결과는 지금처럼 `<work_root>/<Entry>-{request,result}.json`. 요청에 `project_root`가 들어가고 C#은 `Application.dataPath == project_root + "/Assets"`와 Play 아님을 확인한다(옛 `Root` 상수 대체).
- 게임 타입(`DSMapActorSet`)을 아는 일은 프로젝트 훅으로 뺀다. 패키지가 `TypeCache`로 `IKit3DProjectHooks` 구현을 찾는다(0개면 훅이 필요한 요청에서 `NO_PROJECT_HOOKS`, 2개 이상이면 거부).

```csharp
public interface IKit3DProjectHooks {
  // Build 뒤: 만든 재질·프리팹을 프로젝트 데이터에 연결하고 저장·다시 읽어 확인(046-12). 실패는 예외.
  object Register(JObject request, IReadOnlyDictionary<string,Material> materials, IReadOnlyDictionary<string,GameObject> prefabs);
  // Check: 프로젝트 측정값과 문제 목록(예: 몬스터 yawOffset != 0)
  object Measure(JObject request, List<string> problems);
  // Capture: 줄 세우기 목록(프리팹, 재질 덮기, 배율, Visual 회전)
  IEnumerable<Kit3DLineupItem> Lineup(JObject request);
}
```

- Deadsurvivor 구현 `Assets/DeadSurvivor/Editor/Kit3DProjectHooks.cs`: 옛 `AssetBuild.cs`의 액터 세트 연결·저장 후 확인(85~120행), 액터 측정(209~212행), 줄 세우기 목록(268~276행)을 옮긴다.
- 기본 프리팹 기본 재질(옛 `hero_prefab`)은 일반 규칙으로 패키지에 둔다: `variant_of` 대상 프리팹의 렌더러 재질을 그 자산의 `default_variant` 재질로 바꾼다.

### 3.5 기존 도구 대응

| 옛 | 새 | 바뀌는 점 |
|---|---|---|
| `Tools/Art/assets.py` | `3dkit/tools/assets.py` + `kitlib/pipeline.py` | 경로·셰이더·이름·훅 데이터를 설정/카탈로그에서. 명령 이름 같음. `palette-char` → `palette char`, `profile`(최종 프로필 출력)·`--profile` 추가 |
| `Tools/Art/asset_tripo.py` | `kitlib/tripo.py` | `ROOT`·카탈로그 경로·CLI·`TRIPO_HOME`·키 파일을 설정에서. 모델·인자는 프로필(+자산 `tripo.params` 덮기). attack 승인 원문은 카탈로그. 상태 기계·잠금·재전송 금지 규칙은 줄 단위로 같게 |
| `Tools/Art/asset_texture.py` | `kitlib/texture.py` | palette 코드 그대로(해시 동일 조건) + passthrough |
| `Tools/Art/AssetBuild.cs` | `3dkit/unity/Editor/AssetPipeline.cs` + 프로젝트 훅 | 설정값을 요청에서 받음(텍스처 설정·칸·키워드·가져오기·프리팹 옵션). Unity 작업 순서와 값은 같게 |
| `Tools/Art/selftest_asset_tripo.py` | `3dkit/tools/selftest_tripo.py` | 같은 반례 + attack 승인이 카탈로그에서 오는지 |
| 옛 selftest의 Deadsurvivor 단언(예산 상한 240/255/340, 레거시 9종 목록) | 일반 단언으로: 카탈로그의 모든 고정 요청이 정확히 재현, 모든 예산 사용량 ≤ 상한, 승인 원문이 있는 예산만 freeze | 프로젝트 고유 숫자는 키트 코드에서 뺀다 |

## 4. 실패·전환 경계

- 유료 생성: 상태 기계(FROZEN → DRY_RUN_OK → SUBMITTING → SUBMITTED → DOWNLOADED), 잠금, 재전송 금지, 승인 원문 키 `user_approval`은 그대로. **이식 중 새 Tripo 제출은 하지 않는다**(selftest는 네트워크 없이 가짜 CLI). 기존 작업 일지는 읽기만 한다(`status`·`selftest`).
- Unity Build는 같은 경로 덮어쓰기라 GUID가 유지된다. 실패하면 **Build가 쓰는 경로만** 되돌린다(051 M3): 요청 항목 폴더(`Art/Characters/Heroes/<영웅>/`, `Art/Characters/Monsters/<몬스터>/`), `Art/Characters/ActorSet_Main.asset`, `Art/Characters/Heroes/hero_base/Hero045v2.prefab`. 되돌리기 직전에 저장소 전체 `git status`를 다시 보고(다른 세션의 변경 확인), 그 경로 안의 추적 안 된 새 파일은 목록을 확인한 뒤 지운다. `AssetDatabase.SaveAssets()`는 Art 밖도 저장하므로 Build 전후 저장소 전체 `git status`를 비교한다.
- 패키지 추가로 편집기가 다시 컴파일한다. 컴파일 오류가 나면 `Packages/manifest.json`의 한 줄과 훅 파일을 빼서 원상복구한다.
- 장면은 건드리지 않는다(050과 같음: Build는 장면을 열지 않고 액터 세트·프리팹·재질·텍스처만). Capture는 임시 추가 장면을 저장 없이 닫는다.

## 5. 최소 필수 검증

| 변경 | 실패 가능성 | 확인 |
|---|---|---|
| 텍스처 코드 이동 | 해시 달라짐 | 새 `prep --all` 11종 해시 = **보존한 기준값** `output/3dkit-051/baseline/`(051 M1: `output/`은 git 제외라 새 도구가 같은 `work_root`에 쓰면 기준이 사라진다. 옛 `check`·`capture`(`051-old`)·`*-result.json`·`prep/*`를 해시 목록과 함께 먼저 복사했다). selftest는 임시 `work_root`만 쓴다 |
| 카탈로그 v2·프로필 | 숫자·인자 누락 | `selftest`: v1 값 ↔ v2+프로필 대조, Tripo 고정 요청 9건 재현, 반례 |
| C# 이동·훅 분리 | 재질·프리팹·액터 세트가 달라짐 | 새 `unity --all` 뒤 `git status --porcelain Assets/DeadSurvivor/Art` 빈 결과(아니면 옛 도구로 같은 조건 재실행해 같은 변경인지 비교) |
| Check 이동 | 판정 누락 | 새 `check` = 등록 20·장면 2·문제 0, 측정 숫자 옛 `check-report.json`과 항목별 동일. 판정 반례(selftest) |
| Capture 이동 | 줄 세우기 달라짐 | 같은 장면에서 옛 도구(`--tag 051-old`)와 새 도구(`--tag 051-new`) 캡처 픽셀 비교 |
| 패키지·manifest·훅(게임 동작 파일 목록에 `Packages/manifest.json`) | 컴파일·Play 회귀 | Unity 컴파일 오류 0, 짧은 Play 1회 |
| midpoly | 전환 안 됨 | selftest: 임시 설정 `profile: midpoly`로 spec·prep·Build 요청·판정 값 확인 |

- 첫 실제 연결: 패키지 + 훅 컴파일 → `check` 1회(읽기 전용, 측정만)로 Unity 연결과 판정을 먼저 맞춘다. 그다음 `prep` → `unity` → `capture`.
- 선택(시간이 되면, 완료 조건 아님): midpoly로 기존 몬스터 1종을 임시 폴더에 Build해 URP Lit·노멀맵·Mesh LOD 경로가 Unity에서 오류 없이 도는지 보고 폴더를 지운다. 안 하면 NOT_RUN으로 적는다.
- 빼는 검사: 옛 selftest의 Deadsurvivor 고정 숫자 단언(예산 상한·레거시 목록)은 일반 단언으로 대체한다(재현 단언이 더 강하다).

## 6. 미정 사항

- midpoly 숫자(대상 플랫폼·성능 예산)는 시안이다. 실제 생성 전에 사용자와 정한다.
- 패키지 이름 `com.studiokit.kit3d`는 studio-kit 반영 때 다시 볼 수 있다.
- 커밋: 사용자 요청 때만. 커밋할 때는 `3dkit/`도 함께 넣어야 새 클론에서 패키지가 풀린다(`file:../3dkit/unity`).

## 7. 설계 검토 결과

design-reviewer(Claude 독립 서브에이전트, 같은 모델 계열. Codex 검토는 하지 않음) 1회, 2026-11-03. 판정 CHANGE_REQUIRED → 필수 3건 반영.

| ID | 지적 | 반영 |
|---|---|---|
| M1 | 회귀 기준값(`output/assets/*/prep`, `check-report.json`)이 git 제외라 새 도구 실행으로 덮이면 새 값끼리 비교하게 된다 | 새 명령 전에 옛 `check`·`capture --tag 051-old` 실행, `output/3dkit-051/baseline/`에 `prep/*`·결과 JSON·캡처·`hashes.txt` 보존(11종 albedo 해시가 050 `prep.json` 기록과 일치 확인). selftest는 임시 폴더 |
| M2 | 옛 `step_auth`가 047 attack 승인을 예산·자산과 관계없이 돌려준다(새 영웅에 옛 승인으로 약 20크레딧 가능) | 카탈로그 정의 단계는 자기 예산 `step_approvals` 원문 + 예산 `assets` 포함일 때만. 반례 추가: 예산 승인만 있는 attack → `NO_AUTHORIZATION`, 공급자 호출 0회. `selftest_tripo.py` 20건 PASS |
| M3 | 복구 명령 `git checkout -- Assets/DeadSurvivor/Art`가 Build 범위보다 넓어 다른 세션 변경을 지울 수 있다 | 4절: Build 소유 경로만, 직전 전체 `git status`, 새 파일 목록 확인 |

후속 중 이번에 반영: `KIT3D_PROFILE` 환경변수 제거(전환 수단은 설정 값과 `--profile`만), 고정 요청이 지금 예산에 속하는지 대조(`scope`/`legacy_scope`), 재현 건수 출력(9건), 기본 단계 이름과 겹치는 `anim_steps` 거부, `face_limit` 우선순위 문장.

후속(사용자 결정): ① 자산·고정 요청·`prep.json`에 프로필 이름을 기록하고 다른 프로필의 prep를 Build가 거부(`prep.json`에는 이미 `profile` 기록) ② 키트 코드에 남은 종류 이름·`Monster_` 기본 이름·`Visual` 규약·`palette: char/env`를 프로젝트 데이터로(프로젝트가 종류를 정의) ③ Unity: 훅 탐색·액터 항목 사전 검증은 쓰기 전에, `TypeCache` 추상 제외·기본 생성자 요구, `Run`은 짧은 요약만, Guard는 실제 경로 비교, `--timeout` 실제 동작 확인 ④ 패키지 원상복구 목록에 `packages-lock.json`, `3dkit/unity`의 `.meta` 커밋 ⑤ 새 패키지는 `SaveAssetIfDirty` ⑥ `Tools/Proto/selftest_tripo_045.py`는 runpy 대상도 바꾼다, `docs/workflows/ai-3d-assets.md`·`scene-builders.md`도 갱신 목록에 ⑦ `work_root`와 `tripo.new_work_root` 중복.

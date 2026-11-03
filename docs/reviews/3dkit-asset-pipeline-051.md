# 051 검증 기록 — 3dkit v1 에셋 파이프라인

- 설계: [051 설계](../design/3dkit-asset-pipeline-051.md)(L2). 사용자 요청(2026-11-03): "응 그러면 우선 에셋도구부터해서 파이프라인을 모두 업데이트를해줘", "그러면 우선 3dkit에 필요한 업데이트를 모두 작업을 해줘".
- 중간 커밋 `2198c62`(Python 쪽). 이 기록의 나머지(Unity 쪽·회귀·은퇴·도구 이식·문서)는 커밋 전. **커밋 전 gate는 BLOCKED**(11-03 15:37, `gate-20261103-153723-605a7bca`): `Packages/manifest.json`·Editor 코드 변경에 대한 화면 포함 전체 회귀(`run.py all --screens`) 증거가 없음. 사용자 요청("검사 중지하고 바로 전체 커밋진행해줘")으로 시작했던 `run.py all --screens`를 중간에 멈추고(Play 모드 종료, 부작용 파일 변화 0 확인) **`git commit --no-verify`로 gate 없이 커밋했다.** 이 커밋에 대한 화면 포함 전체 회귀는 NOT_RUN이다(짧은 Play·051 회귀는 PASS). 다음 Unity 커밋 전에 `all --screens`를 먼저 돌린다. 커밋 때 추적 안 된 파일(`3dkit/unity/`의 `.meta` 포함, `Assets/DeadSurvivor/Editor/DSKit3DProjectHooks.cs(.meta)`, `3dkit/tools/` 새 파일)을 반드시 함께 넣는다(마감 검토 후속 1).

## 결과

| 완료 조건(설계 1절) | 상태 | 근거(2026-11-03, 편집기 Map046_Full) |
|---|---|---|
| ① `check` 같은 결과 | **PASS** | 새 도구 PASS 20·2·0. 옛 도구 보고서(`output/3dkit-051/baseline/check-report-old.json`)와 항목별 측정값(삼각형·뼈·렌더러·재질·셰이더·그림자·높이·발·중심·재질 경로·텍스처 형식·크기·밉맵·알파)·장면 의존 목록·장면 재질·액터 세트가 모두 같음(차이 0) |
| ② `prep` 해시 | **PASS** | 11/11 `albedo.png` 해시 = `baseline/hashes.txt`(050 `prep.json` 기록과도 일치) |
| ③ `unity --all` | **PASS** | 실행 전후 저장소 전체 `git status --porcelain` 동일(재질·텍스처·FBX·프리팹·액터 세트·기본 프리팹 바이트 변화 0) |
| ④ `capture` | **PASS** | `051-new` ↔ `051-old`(같은 장면·같은 세션) 게임 각도·정면 모두 다른 픽셀 0 |
| ⑤ Tripo 고정 요청 재현 | **PASS** | `selftest`: 9건 `planned_args`·콘셉트 해시 재현, 예산 소속(`scope`/`legacy_scope`) 대조, 사용량 ≤ 상한 |
| ⑥ 짧은 Play | **PASS** | `Tools/Proto/keyart_045.py`(short) `output/keyart-target-045/r001/run-39af30f74e7f`: 스쿼드 B·C 출전·이동·정지, 새 콘솔 오류 0, 부작용 파일 변화 0 |
| 프로필 전환(설정 값 하나) | **PASS** | `selftest` 임시 프로젝트: `assets.profile` lowpoly ↔ midpoly로 숫자·텍스처 단계·셰이더·칸·가져오기·LOD가 바뀜, `--profile` 덮기 |
| 패키지 컴파일 | **PASS** | `com.studiokit.kit3d` 해석(`packages-lock.json` local), `scriptCompilationFailed=False`, 훅 1개 발견 |
| (선택) midpoly Unity 실행 | **PASS** | 임시 폴더 `Assets/_Kit3DSmoke051`에 시험 자산 1개 Build: URP Lit, `_BaseMap`·`_BumpMap`, 노멀맵 가져오기(sRGB 끔), `_NORMALMAP` 켬, `generateMeshLods` 켬, 프리셋 적용, 규격 대조 PASS(750 삼각형, ASTC 4×4@512) → 폴더 삭제, git 흔적 0 |
| 일반 도구 이식 | **PASS(자체 시험)** | `3dkit/tools/guard.py`(옛 시험 사례 전부 + 설정 기본값), `protect.py`(실제 사용: 위 회귀 명령을 감쌈), `session_time.py`. Deadsurvivor의 hook 연결은 바꾸지 않음 |
| 옛 도구 은퇴 | 완료 | `Tools/Art/assets.py` 안내·거부, `AssetBuild.cs`·`asset_tripo.py`·`asset_texture.py`·`selftest_asset_tripo.py` 삭제(이력은 `2198c62`), `Tools/Proto/{tripo_045,selftest_tripo_045}.py`·`Tools/Map/build_monsters_046.cs` 안내 갱신. 카탈로그 v2를 `docs/art/asset-catalog.json`으로(v1은 git 이력) |

## 독립 검토

- 설계: design-reviewer 1회(Claude 서브에이전트, 같은 계열) CHANGE_REQUIRED → M1~M3 반영(설계 7절).
- 마감: validator-client 1회(Claude 서브에이전트, 같은 계열) — **PASS, 필수 수정 0건**. 셸 없이 파일로 prep 해시·check 보고서·Build 결과·짧은 Play 보고서를 직접 대조, 옛 코드와 판정 규칙·Build 값·Tripo 상태 기계가 같음을 확인. 캡처 픽셀·git 상태 비교는 메인 보고를 인정. 후속 반영: 카탈로그 git 상태(삭제+추적 안 됨 → 수정으로 stage), 낡은 문장 4곳, 복구 절차를 운영 문서([에셋 규격](../workflows/asset-standard.md) 3절)로, `--profile` 값 누락 메시지, 시험 임시 폴더 삭제. 남은 후속: `Tools/Harness/tier-policy.json`에 `3dkit/**` 분류, 프로필 변경 시 고정 요청 `FROZEN_ARGS_MISMATCH`(거부 쪽, 설계 7절 ①), midpoly 판정에 노멀맵 `type`·`srgb`와 맵별 플랫폼 형식 검사, `project_root` 누락 메시지.
- v1 ↔ v2 + 프로필 값 대조: 변환 스크립트(세션 작업 폴더, 보존 안 함)로 2026-11-03 실행, 차이 0(standards·presets·Tripo 모델/인자/CLI/작업 폴더·텍스처 가져오기·셰이더·trial/scene/shader 목록·palettes·migrations·measured·budgets·자산 전 필드·이름 규칙). 행동 회귀(① ~ ⑥)가 같은 결론을 낸다.

## 놓친 것과 워크플로우 갱신 후보

| ID | 놓친 것 | 원인 | 갱신 후보 | 반영 |
|---|---|---|---|---|
| 051-1 | 새 도구가 같은 출력 폴더에 써서 회귀 기준값이 덮일 뻔했다(설계 검토 M1) | `output/`은 git 제외인데 기준값의 추적 사본이 없었다 | 도구 교체 회귀는 "옛 도구 결과를 먼저 별도 폴더에 해시와 함께 보존"을 첫 단계로 | 3dkit 교훈 반영 |
| 051-2 | 050부터 옛 `step_auth`가 047 attack 승인을 모든 자산에 돌려줬다(M2, 실제 과금 0) | 승인 원문을 코드 상수로 두고 예산과 묶지 않았다 | 유료 단계 승인은 데이터(예산 안)로만, 프로젝트 단계는 자기 승인 필수 | 도구에 반영(`step_approvals`, 반례 시험) |
| 051-3 | 복구 명령 범위가 쓰는 범위보다 넓었다(M3) | 여러 세션 동시 작업을 고려하지 않음 | 되돌리기는 도구가 쓰는 경로만, 직전 전체 상태 확인 | 설계 4절 반영 |
| 051-4 | 내 selftest 첫 실행이 마지막 단언에서 실패했는데 출력 꼬리만 보고 지나칠 뻔했다 | 여러 명령을 `&&`로 이어 끝줄만 봄 | 시험 결과는 끝줄 "PASS"를 확인한 뒤 다음으로 | 메인 작업 습관(문서화 안 함) |
| 051-5 | Unity 편집기가 작업 중 꺼져 있었고(원인 미확인, 충돌 보고 없음), 상태 대기 명령이 10분 넘게 끝나지 않았다 | 열려 있는지 즉시 확인하지 않고 `--until-ready` 대기 | Unity 작업 전 `unity status`(대기 없이)로 먼저 확인, 없으면 사용자에게 묻기 | 3dkit 교훈 반영 |
| 051-6 | 임시 시험 스크립트를 numpy 없는 `python3`로 실행 | 도구 규칙(`/usr/bin/python3`)을 임시 스크립트에 적용 안 함 | 기존 규칙 유지 | 해당 없음 |

## 시간·토큰

`Tools/Harness/session_time.py --markdown`(이 세션, /clear 뒤): 벽시계 75.9분, 활동 40.6분, 도구 호출 112회, 출력 19.9만, 캐시 읽기 2,850만, 맥락 끝 41.5만. 마지막 요청("3dkit에 필요한 업데이트를 모두")만 23.4분·도구 40회·캐시 읽기 1,460만 — 맥락이 30만을 넘은 상태에서 큰 작업을 이어서 해 캐시 읽기가 비용의 대부분이다(사용자가 이 세션에서 계속하기를 선택). 하위 에이전트: design-reviewer 16.2만 토큰·7.3분, validator-client 18.6만 토큰·도구 55회·5.3분. Codex 0회. 시간이 든 곳: Unity 편집기 꺼짐 확인과 재시작 약 12분(대기 명령 10분 초과 포함).

## 남은 것(후속, 우선순위는 사용자)

- 감량(Blender) 자동화, lowpoly 소품의 면→팔레트 칸(043) 단계 편입.
- 키트 코드에 남은 프로젝트 성격 값(종류 이름, `Monster_` 기본 이름, `Visual` 규약, `palette: char/env`)을 프로젝트 데이터로(설계 7절 후속 ②).
- 측정 Play·Play 실행기·`tier`·`gate`(H1 일부·H2·H4)는 새 프로젝트에서 게임 루프와 함께.
- midpoly 숫자 확정(대상 플랫폼·성능 예산), Tripo 크레딧.
- Deadsurvivor hook을 `3dkit/tools/guard.py`로 바꿀지(지금은 `Tools/Hooks` 유지).

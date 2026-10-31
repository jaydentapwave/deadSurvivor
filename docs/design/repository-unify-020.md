# REPOSITORY-UNIFY-020 — 열린 Unity 프로젝트로 Git·도구 통일

2026-10-31 · L2 · 구현·최종 검증. 사용자는 현재 Unity에서 여는 `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor`를 실제 Git 작업 폴더로 삼고 기존 작업·커밋 이력을 보존하며 문서·검증 경로를 정리하도록 요청했다.

## 현재 상태와 변경

기존 Git 루트는 `/Users/hyeongwoo/Documents/ChatGPT/데드 서바이버`, HEAD는 `ee89dc8`, main/origin에는 이전3커밋이 있다. 열린 프로젝트에는 `.git`이 없다. 비무시 원본과 이전 `UnityProject` 사본 차이는 PrototypeFont.asset 및 ProjectSettings.asset 두 파일이다. 현재 실행 폴더의 자산/작업을 우선하며 Git 사본으로 덮지 않는다. 출력·도구 상태 등 무시된 파일도 보존한다. 회사 프로젝트/전역 설정에는 접근하거나 변경하지 않는다.

## 이전과 복구

1. 읽기 전용으로 Git 상태/이력/설정, 전체 추적 파일, 원본 Assets·Packages·ProjectSettings 지문, 충돌을 동결한다. 현재 Unity의 Play·미저장 상태를 확인한다. 기존 source/target baseline을 성공을 위해 갱신하지 않는다.
2. Git bundle 및 `.git` 복구용 사본을 기존 output/repository-unify-020에 만든다. 이후 해당 output도 새 루트로 함께 이동한다. 목적지 루트의 `.repo-unify-020.local.json`에 고정 journal을 남긴다. 각 rename 전에 PLANNED, 성공 뒤 DONE를 원자 기록해 output 자체가 이동해도 기록 경로는 유지한다. 완료 검증 뒤 journal을 output/repository-unify-020으로 옮긴다. 사전 파일 지문과 충돌 부재를 재검사한다.
3. 기존 루트의 최상위 항목을 목적지에 이름 충돌 없이 이동한다(.git·문서·도구·원본 아트·ignored output·로컬 상태 포함). Assets·Packages·ProjectSettings는 목적지의 기존 파일을 보존한다. 기존 `UnityProject` 사본은 output/repository-unify-020/archived-UnityProject에 원형 보관한다. 덮기/삭제/강제 reset은 하지 않는다.
4. 비어 있는 기존 루트는 호환 진입점으로만 남긴다. 실행 도구가 writable root 자체의 symlink를 거부하는 제약(D020-03)이 확인돼, 기존 루트는 물리 디렉터리로 두고 .git 텍스트 포인터와 Tools/docs/output 등 개별 링크만 둔다. 로컬 core.worktree를 정확히 새 루트로 고정해 어느 진입점에서도 Git 작업 폴더는 새 루트 하나다. 이는 과거 보고서의 절대 경로와 현재 Codex 작업 경로를 보존하는 진입점이며 파일/저장소 사본이 아니다. 새 루트 안에 추적 symlink는 만들지 않는다. gate는 실제 경로에서 실행한다.
5. 이동 직후 HEAD/브랜치/origin·Git 설정·bundle 이력, 추적 파일/Assets 지문, ignored 항목/전체 원본 파일 집합을 검증한다. 실패하면 journal의 본인 이동 항목만 원상복구한다. 사용자/외부 변경이 발견되면 보존하고 중단한다.

## 단일 소스와 경로

- 게임/콘텐츠 원본은 새 루트의 `Assets/DeadSurvivor/{Runtime,Editor,Resources}`다. 기존 UnityMigration Runtime/Editor 및 PrototypeData 복제본은 output의 이전 자료로 보관하고 추적 경로에서 제거한다. UnityMigration의 독립 실행 검증 스크립트·원본 폰트/라이선스·이미지 저작 소스는 유지한다. `sync.py --apply`는 명시적으로 폐기해 과거 사본으로 현재 코드를 덮는 경로를 막는다.
- run/local의 데이터·코드 fingerprint, map017/018의 source_hashes와 소스 전제, catalog/balance 도구를 실제 Assets 경로로 변경한다. 원본과 자기 자신을 비교해 성공을 만드는 동기화 검사는 제거하고 필수 파일/실제 프로젝트 identity를 검사한다.
- Python의 파일 기준 ROOT가 새 루트로 해석되도록 한다. Unity 실제 대상은 같은 승인된 개인 프로젝트 경로를 유지한다. C# 보고서 경로는 승인된 Project/output 하위로 정리한다. UnityAsset 임시 C# 도구도 새 루트에서 실행한다.
- Stop hook 명령은 새 루트의 gate로 바꾼다. Git hook은 상대 .githooks를 유지한다. hook/검사 강도·설치 baseline은 끄거나 재설정하지 않는다. Codex hook 정의 변경 후 앱 신뢰 여부는 별도이며 강제 신뢰 DB 편집은 하지 않는다.
- 과거 검증 JSON/asset job의 입력/경로·해시는 역사적 원자료로 보존하고 수정하지 않는다. 원래 경로는 외부 호환 링크를 통해 같은 파일에 도달한다. 새 검사 결과는 새 소스 지문으로 별도 생성한다.
- D020-01 해소: Tools/project_paths.py에 승인된 과거 루트만 재해석하는 함수를 둔다. 새 ROOT가 승인된 Desktop 프로젝트이고, 과거 루트가 실제로 해당 ROOT로 향하는 symlink이거나, D020-03의 .git regular 파일 포인터와 Tools/output 링크가 모두 정확한 ROOT 대상일 때만 oldRoot 하위 절대 경로를 같은 상대 경로의 새 ROOT로 바꾼다. `..` 이탈은 거부하고 내부 symlink 검사는 기존 그대로 적용한다. map_io.load_json의 strict 경로 검사 전에 적용하고 asset pipeline.job_dir의 BASE 검사 전에 적용한다. 원래018 receipt/JSON은 변경하지 않는다. 임의의 symlink/다른 alias를 허용하지 않는 음성 시험을 추가한다. UnityProject/중복소스의 과거 문서 링크는 현재 Assets 또는 output 보관 위치로 별도 정리한다.

## 작업 보존과 설정

PrototypeFont 등 현재 실행 폴더의 변경을 그대로 보존한다. 이전 Git 사본에서 비웠던 회사 클라우드 ID 두 값은 현재 ProjectSettings에 여전히 존재하며 cloudEnabled는0이다. 현재 로컬 작업을 먼저 완전히 보존한다. 현재 편집기는 종료돼 있으므로 실제 상태 재확인 후, 두 연결값의 원본 파일을 output에 백업하고 정확히 두 줄만 비운 뒤 같은 프로젝트를 연다. 그 외 PlayerSettings와 폰트/씬/자산은 원본 바이트를 유지한다. 자동 회사 서비스 연결/전역 Git 변경은 금지다. 다른 PlayerSettings나 미저장 사용자 편집을 덮지 않는다.

## 검증과 완료

독립 design-reviewer 선행 → 메인 이전/경로 수정 → 영향 단위 시험/로컬 → validator-client 코드 검토/수정 → 현재 Unity 컴파일/기존 all/screens와017/018 실제 경로 시험 → 독립 근거 검토/최종 gate → 기존 main에 추가 커밋/지정 origin 업로드. 새 임시 회사 서비스나 새 유료 생성은 없다. L2지만 맵 수학/전투 계산은 바꾸지 않아 별도 algorithm role은 불필요하다(실제 변경이 생기면 재분류).

문서/상태/진입점은 새 root Assets 구조로 갱신한다. 새 루트에서 git status·log·remote와 gate/검증 실행, 열린 같은 Unity 씬의 실제 프레임, 이전 경로의 동일 파일 도달을 확인해야 완료다. 이전3커밋은 rebase/amend하지 않고 새 경로 커밋으로 보존한다. 이 범위는 기존 GUID13 문제나 전체 G2/G3 인수가 아니다.

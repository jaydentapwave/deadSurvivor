# REPOSITORY-UNIFY-020 — Git·Unity 작업 위치 통일

2026-10-31 · L2 · [설계](../design/repository-unify-020.md). 사용자 확정 방향은 현재 Unity 프로젝트를 Git 루트로 삼고 작업·이력을 보존하며 문서·검증 경로를 정리하는 것이다. 실제 루트는 `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor`다.

## 보존과 변경

[사전 지문](../../output/repository-unify-020/preflight.json), [전체 기존 파일 지문](../../output/repository-unify-020/all-files-before.json), [물리 이전](../../output/repository-unify-020/physical-move.json), [호환 진입점](../../output/repository-unify-020/compatibility-portal.json)을 남겼다. Git bundle/.git 복구본·기존 ignored output(약0.86GiB)·도구 상태까지 보존했다. 실제 Git 디렉터리와 원래3커밋/main/origin을 그대로 이동했으며 rebase/reset/amend는 하지 않는다.

기존 파일11,093개와 원본 Unity파일1,741개를 물리 이전 시 대조했다. 현재 프로젝트의 PrototypeFont 변경은 이전 사본으로 덮지 않고 보존했다. 예외는 회사 연결 메타데이터 cloudProjectId/organizationId 두 값뿐이며 편집기 종료/원본 지문 검사·별도 백업 뒤 정확히 두 줄을 비웠다. cloudEnabled0과 다른 게임/사용자 설정은 보존했다.

Assets가 실제 코드·콘텐츠 원본이다. 중복 UnityMigration Runtime/Editor/PrototypeData와 UnityProject 사본은 로컬 output의 보관본으로 옮겼다. 독립 실행 검증 템플릿·저작 아트·폰트/라이선스는 유지한다. 과거 복사 --apply를 폐기하고 데이터 검사·source fingerprint·Unity 전제 검사를 실제 Assets 기준으로 바꿨다. 게임 계산은 변경하지 않았으며 활성 Editor 검증기의 출력 경로만 실제 Project/output으로 수정했다.

기존 Documents 경로는 새 저장소로 가는 호환 진입점이다. 폴더 자체의 symlink가 실행 도구의 writable-root 초기화를 막아 물리 디렉터리에 .git 포인터/개별 경로 링크를 두었다. 로컬 core.worktree는 실제 Desktop 폴더며 양쪽의 Git 루트/HEAD/refs가 같다. 이력/파일 사본은 하나이고 새 Git 루트 안에 추적 symlink는 두지 않는다. 호환 진입점에는 문서/도구/출력만 연결하고 Assets/Packages/ProjectSettings 진입점은 제거해 다른 Unity 프로젝트로 여는 혼동을 막았다. 이전 core.worktree 값과 변경 journal도 보존했다.

## 독립 검토와 검사

`/root/unify020_design` 설계 검토 PASS. D020-01: 이전 절대 경로의 receipt/job을 승인된 root에 한해 재해석하고 내부/임의 symlink 검사는 유지. D020-02: 이동 중에도 유효한 고정 목적지 journal의 PLANNED/DONE 원자 기록. D020-03: 실행 도구 제약에 맞춘 단일 gitdir/worktree 호환 진입점과 설정 rollback 보존.

경로 단위10개 PASS: 실제018의 과거 절대 receipt 읽기/동일 보고서 지문, 승인된 두 호환 형태, 임의 alias/내부 symlink/잘못된 포인터/.. 거부, 실제 Assets 요구, deprecated --apply의 무수정 거부. 과거 JSON/기준 gate-policy의 baseline은 수정하지 않는다.

전체 Verify134개·Assets38개 단위 시험과 코드/보존 독립 검토는 PASS다. 공통 경로 모듈이 추가된 최초 Assets fixture에는 해당 파일이 없어 실패했으며, 필수 도구 파일을 fixture에 포함하고 공유 정책 지문 변경 시험을 보강한 뒤38개를 통과했다. 이전 실패 로그도 보존했다. CLI의 즉시 반환 시작 방식은 Editor가 준비 전에 종료됐고, 유효 Personal 라이선스를 확인한 뒤 --wait 시작 방식으로 같은 프로젝트를 정상 연결했다. GUI 조작/계정·라이선스 변경은 하지 않았다. Unity 컴파일은 오류·경고0이며 [017 실제42사례](../../output/map-i2-017/0c347e4e679f433d964fadd64d1cb0a5/summary.json)도 새 소스 지문으로 PASS다. [018 실제5사례](../../output/map-blockout-018/04f62fb8744e40a398b0936d6697c5bc/summary.json)는35단위·250프레임·3,510sweep·64메시·콘솔/정리 PASS다. [기존 게임 전체 회귀](../../output/verification/20261031-213040-14305e38/summary.json)도 컴파일·695규칙·실제 프레임/입력·3해상도24화면·콘솔/정리·전후 지문 일치 PASS다. `all-context/restored.json`에서 원래 Map018 씬/선택/background를 복원한 결과를 확인했다. 최종 gate·커밋 마감은 별도 기록에 결합한다. 최종 실행과 지문·검토는 output/repository-unify-020/final-checks.json에 결합하며 미실행을 PASS로 표시하지 않는다. 과거 GUID13 잔존/전체G2/G3/G5·물리 기기는 이번 위치 정리의 완료 범위가 아니다.

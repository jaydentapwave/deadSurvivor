# UNITY-PROJECT-019 — Unity 프로젝트 Git 포함

2026-10-31 · L1 · 구현 기준. 사용자 요청은 실제 Unity 프로젝트의 필요한 파일을 기존 GitHub 저장소에 커밋·업로드하는 것이다. 현재 실행 대상과 소스 소유권을 바꾸지 않고 저장된 파일을 `UnityProject/`에 새로 복사한다. 원본은 `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor`이며 Git 사본은 최초 포함 시 원본과 같은 바이트여야 한다. 예외는 기존 회사 Unity 조직 연결을 이식하지 않기 위한 `ProjectSettings.asset`의 `cloudProjectId`·`organizationId` 두 값 비우기뿐이다. 원본의 `cloudEnabled: 0`과 다른 설정은 유지한다. 새 런타임 기능/자동 동기화 도입은 없다. 사본의 두 연결값 변경은 원본 SHA와 정규화 후 SHA를 따로 기록하고 정확히 두 줄만 바뀌었는지 확인한다.

## 포함과 경계

- `Assets`, `Packages`, `ProjectSettings`의 비무시 파일 전체와 원본 `.meta`를 보존한다. 게임/시험맵과 바위의 의존 자산, 폰트, 렌더 설정, input actions, 패키지 manifest/lock, Unity 버전을 포함한다.
- 이미 커밋한 `.gitignore`를 복사 경로에 적용한다. 빌드·캐시·검증 캡처/복제 참고본·로컬 상태는 제외한다. 원본과 기존 저장소 파일은 덮거나 삭제하지 않는다. 새 대상이 존재하면 덮기 전에 중단한다.
- 단일 파일 크기와 링크 파일을 확인한다. 패키지의 절대 `file:` 경로/외부 작업 폴더 의존성은 검사하고, 참조가 있으면 그대로 이식 완료로 보고하지 않는다.
- 포함 파일의 SHA-256을 복사 전/후 원본과 사본에서 대조한다. `.meta` GUID 중복·대응 파일 누락, scene/prefab/material의 GUID 참조, Build Settings의 시작 씬과 Map018·바위 참조를 검사한다. 공식 패키지 GUID는 원본 PackageCache의 같은 패키지 버전에서 읽기 전용으로 확인한다. 캐시는 커밋하지 않는다.
- 별도 원본의 현재 파일 지문이 기존 검증과 일치하는지 gate로 확인한다. 파일 이관은 새 Unity 실행/클론 첫 import PASS가 아니며 이 두 항목은 실행하지 않으면 NOT_RUN으로 남긴다. 기존 게임 검증을 신규 클론 실행 결과로 표시하지 않는다.

## 사용과 유지

Unity Hub에서 `UnityProject`를 Unity6000.4.3f1로 연다. 일반 게임은 `Assets/DeadSurvivor/Scenes/DeadSurvivor.unity`, 맵 미리보기는 `Assets/DeadSurvivor/Scenes/Map018_r001/MapBlockout018_r001.unity`를 직접 연다. 편집기 메뉴/CLI 검증의 일부는 기존 개인 실행 경로로 고정돼 있으므로 이관본에서 해당 메뉴/검증기를 바로 실행하도록 안내하지 않는다.

현재 검증/개발 대상은 기존 Desktop 프로젝트를 유지한다. 이후 변경은 원본과 Git 사본 차이를 확인해 반영한다. 저장소 안 C#은 기존 UnityMigration과 Git 사본 두 곳에 있으므로 이번 포함은 동기화 방향 변경이 아니다. 기존 gate와 hook/baseline은 변경하지 않는다.

## 검증과 독립 검토

메인이 복사·GUID/패키지/원본 불변·Git 포함 목록을 검사하고 `run.py local` 및 최종 gate를 실행한다. L1 validator-client가 결과와 미실행 범위를 읽기 전용으로 확인한다. 보고서는 [019 검증 기록](../reviews/unity-project-019-review.md)에 남긴다. 지적 수정 후 필수 검사를 통과한 파일만 커밋하고 지정 `origin/main`으로 업로드한다. 새 유료 생성/외부 서비스 연결은 없다.

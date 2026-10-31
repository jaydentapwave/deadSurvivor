# Dead Survivor Unity 프로젝트

필요 버전은 **Unity 6000.4.3f1**이다. 저장소를 내려받은 뒤 Unity Hub의 Add/Open에서 이 `UnityProject` 폴더를 선택한다. 최초 실행에서 `Packages/manifest.json`과 `packages-lock.json`에 따라 패키지/Library를 복원하므로 네트워크와 import 시간이 필요하다.

- 게임: `Assets/DeadSurvivor/Scenes/DeadSurvivor.unity`를 직접 열고 Play.
- 3D 시험맵: `Assets/DeadSurvivor/Scenes/Map018_r001/MapBlockout018_r001.unity`를 직접 열고 Play. Game 화면 클릭 후 WASD/방향키 이동, Tab 전체/근접, C 각도, V 바위/파티 초점.
- 씬/프리팹/모델/재질/폰트/설정과 원래 `.meta`를 포함한다. 캐시·빌드·검증 캡처는 제외한다.

2026-10-31 저장된 개인 실행 프로젝트의 Git 사본이다. 원본은 `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor`에서 유지한다. Git 사본의 회사 조직/클라우드 프로젝트 식별자 두 값만 비웠고, 다른 자산과 설정은 원본 지문과 일치한다. 자동 동기화나 개발 대상 변경은 수행하지 않았다.

[019 검증 기록](../docs/reviews/unity-project-019-review.md)에 파일 대조와 한계를 남겼다. 원본에 이미 있던 미해결 GUID 참조13개는 수정하지 않고 그대로 보존했다. 새 클론의 최초 import/Play는 이번 파일 이관에서 실행하지 않았다. 기존 개인 검증 메뉴/CLI 일부는 원본의 절대 경로를 사용하므로 이 사본에서는 위 씬 파일을 직접 연다.

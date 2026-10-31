# UNITY-PROJECT-019 — 실제 Unity 파일 Git 이관

2026-10-31 · L1 · [설계](../design/unity-project-019.md) · [프로젝트 열기](../../README.md). 사용자 요청은 필요한 Unity 파일을 기존 GitHub 저장소에 커밋·업로드하는 것이다. 판정 범위는 **SAVED_PROJECT_TRANSFER_ONLY**이며 새 기능/개발 대상 변경/새 클론 첫 import·Play 성공을 뜻하지 않는다.

## 포함과 보존

`UnityProject/Assets`, `Packages`, `ProjectSettings`에 저장된 원본527파일을 포함했으며, 연결 식별자 두 값을 비운 사본은61,286,467바이트다. 원본1741파일 중 이미 합의한 gitignore의 검증 캡처/메타1214개를 제외했다. 원본 바이트/파일 집합은 전후 불변이다. 캐시·빌드·로그인 상태는 포함하지 않았다. 기존 게임과 Map018의 씬/prefab·바위·텍스처/재질·폰트·input actions·렌더 설정·Unity버전·package manifest/lock과 `.meta`를 보존했다. 가장 큰 파일은14,970,908바이트로 GitHub 단일파일 제한 이하다.

사본에서 바꾼 설정은 `ProjectSettings/ProjectSettings.asset`의 `cloudProjectId`와 `organizationId` 두 값뿐이다. 원본에 남아 있던 회사 Unity 조직 연결을 사본에 이식하지 않기 위해 비웠다. 원본과 사본의 전체944줄 중929/933번 두 줄만 다르며 `cloudEnabled: 0`과 나머지 설정은 보존했다. 기존 원본 프로젝트는 변경하지 않았다. 읽기 전용 CLI로 현재 Map018 씬이 Play 정지/저장 상태임을 확인했다.

## 실행 근거와 한계

[복사 지문](../../output/unity-project-019/copied-files.json), [원본 전체 지문](../../output/unity-project-019/source-before.json), [전송 판정](../../output/unity-project-019/summary.json), [GUID 대조](../../output/unity-project-019/guid-audit.json)을 보존했다.

| 검사 | 상태 | 결과 |
|---|---|---|
| 포함 파일 집합/바이트·원본 불변 | PASS | 527개와 SHA-256 일치, 문서화한 설정2줄만 예외 |
| `.meta`·GUID 중복·제외파일 참조 | PASS | 필수 메타 누락/고아/중복0, 제외자산으로 향하는 참조0 |
| 패키지와 로컬 경로 | PASS | manifest/lock과 하위 의존성 연결, 로컬 `file:` 의존성0, GUID196개가 원본의 잠긴 버전 패키지에서 확인됨 |
| 전체 GUID 해석 | FAIL — 기존 문제 | 13개가 원본에서도 해석되지 않음. 원본/사본의 동일 참조이며 이번 복사 누락이 아님 |
| 새 클론 최초 import/Play | NOT_RUN | 파일 복사/참조 대조를 새 Unity 실행 PASS로 해석하지 않음 |

기존 미해결 참조는 `Assets/Settings/PC_Renderer.asset`의 probe debug 자원7개, `DefaultVolumeProfile.asset`의 m_Script4개, TutorialInfo Readme 아이콘1개, TMP Settings 기본 sprite1개다. 현재 원본 AssetDatabase에서도 경로가 비어 있고 PackageCache/동일 Editor의 BuiltInPackages에도 해당 메타가 없다. 원본과 동일하게 보존했으며 무해하거나 미사용이라고 단정하지 않는다. 원본 문제 정리는 별도 작업이다. 기존 전체 회귀 PASS는 원본 개인 실행 프로젝트의 기록이며 새 클론 정상 실행 증거가 아니다.

메인이 구현하고 `/root/unity019_validator`가 읽기 전용으로 파일 집합/해시·두 설정 줄·패키지 버전·기존 미해결 집합/제외자산 참조0을 독립 확인했다. 최종 local/gate·스테이징 파일 대조와 독립 마감 결과는 `output/unity-project-019/final-checks.json`에 결합한다. 최종 마감 전 해당 후속 항목을 완료로 주장하지 않는다.

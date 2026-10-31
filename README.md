# 데드 서바이버 / 배낭 원정대 프로토타입

> 3D 배치 미리보기: [018 설계·사용법](docs/design/map-blockout-018.md) · [검증 기록](docs/reviews/map-blockout-018-review.md). 007의64개 충돌 후보를 높이 있는 메시로 전개하고 기존 Tripo 바위 하나와 4인 탐색을 별도 장면에 배치했다. 최종 미술·전체 맵/G2·전투 연결·자산 채택은 후속 범위다.

> 이동 핵심 구현: [017 설계/사용법](docs/design/map-i2-017.md) · [실제 검증](docs/reviews/map-i2-017-review.md). 파티/예약 슬롯 안전 이동과 별도 Unity 프레임 시험을 구현했다. 최종 숲 맵·full map v1·I3 상태 전환·전체 G2는 후속 범위다.

> I1 기록 기반: [016 설계/명령](docs/design/map-i1-016.md) · [검증 기록](docs/reviews/map-i1-016-review.md). 로컬 실행/관측 수집과 누락·중단·변조 검출을 구현했다. 016 protocol fixture 수집의 전체 상태는 NOT_RUN이며 후속017 실제 이동 시험과 구분한다. 전체 map v1/G2 인수는 미완료다.

> 실행 효율 개선: [013 설계](docs/design/efficiency-013.md) · [검증 기록](docs/reviews/efficiency-013-review.md) · [개발 순서](docs/workflows/development.md). 상세 증거를 유지하는 출력 요약과 자산 단계별 재사용을 추가했다. 기존 검사/독립 검토/gate 기준은 유지한다.

> 최소 자산 구현(2026-10-31): [바위 전송 도구](Tools/Assets/README.md) · [012 설계](docs/design/asset-pipeline-minimum.md) · [검증 기록](docs/reviews/asset-pipeline-012-review.md). 기존 원본의 작업 기록/중단 재개, 경량화, Unity 계약 검사와 격리 화면 시험을 구현했다. 추가 유료 생성 없이 전송을 시험하며 최종 게임 자산 승인·맵 배치·전체 자산 gate는 별도다.

> 자산 설계 반영(2026-10-31): [세 공개 프로젝트의 채택 근거](docs/design/ai-3d-reference-adoption.md) · [AI 3D 자산 워크플로우](docs/workflows/ai-3d-assets.md) · [자산 하네스 설계](docs/design/ai-3d-asset-harness.md). 작업 복구·후처리·Unity 인수·시각 비교·채택 등록을 G5에 연결했다. 전체 A0~A5 설계이며 012는 그중 기존 바위 전송의 부분 구현이다. 자동 자산 gate/등록은 미구현이다.

> 구현 착수(2026-10-31): [H1 독립 기하 판정 기초](docs/design/map-harness-h1-foundation.md)를 코드로 구현했다. `local/all`에 분석 시험을 추가했으며 실제 Unity 맵 실행 하네스·G2는 아직 미완료다. 외부 3D 생성은 [CLI/API 기본 방식](docs/decisions/3d-cli-workflow.md)으로 결정했다.

> 구현 순서 원본: [첫 3D 맵 구현 착수 명세](docs/design/map-first-implementation-spec.md) — 준비 1~4 통합, 첫 시험 정책·객체별 불확실성·구현 순서. I0/016 기록/017 이동 핵심은 위 후속 결과를 따른다. 실제 숲 맵과 전체 MapRequired Unity 하네스는 미구현이다.

> 이전 준비: [근거 기반 맵 준비 1~4](docs/design/map-meadow-007-r001-package.md) — 정책·공통 도면 네 판·보정 실험·하네스 계약. 후보 자료이며 실제 충돌/게임/하네스 미검증.

> 이전 준비:  [MAP-MEADOW-006 충돌·좌표·통로 정책](docs/design/map-meadow-006-r001-package.md) — 등록부를 바탕으로 작성 기준과 변환식·인수 조건을 설계. 실제 맵/하네스 미구현.

> 이전 적용안: [우리 맵 적용 설계 04](docs/design/3D배경-우리맵-적용설계-04.md) — 03 원본 분석을 유지하며 시작 후보·검증 지점·기본 도형 제작 기준 구체화. 런타임 미구현.

> 원본 분석 자료: [Verdant Meadows 원본 구조 분석·평면도 03](docs/design/3D배경-VerdantMeadows-구조분석-03.md). 01·02안은 레퍼런스 요구 미충족으로 보존하며 현재 기준으로 사용하지 않는다.

개인 사이드 프로젝트. 문서·작업 기록은 이 저장소 안에서 관리하며 회사 허브·옵시디언·노션과 연동하지 않는다.

## 시작

- [맵 검증 도구 구현 설계](docs/design/map-harness-implementation.md): 입력·관찰·판정·기록·복구의 구체 설계. H1/016 기록/017 실제 이동 시험은 부분 구현이며 전체 MapRequired 하네스는 미구현이다.

- [검사 누락 방지 hook](docs/workflows/verification-gates.md): Git 커밋 전 자동 검사. Codex 종료 hook은 정의 작성 후 신뢰/발동 확인 필요.

- [배낭 원정대 현재 게임 사양 Word 문서](docs/specs/배낭원정대_현재게임사양_20261029.docx)

- [작업 지침](AGENTS.md)과 [개발 워크플로우](docs/workflows/development.md)
- [현재 상태](docs/현재상태.md), [할일과 미검증 범위](할일.md)
- [컨셉](docs/design/게임컨셉.md), [프로토타입 범위](docs/design/프로토타입-범위.md), [현재 장비 규칙](docs/design/장비규칙.md)
- [코드 구조](docs/구조.md), [개발 환경](docs/개발환경.md), [검증 실행](docs/검증절차.md)
- [최신 몬스터 감소·하단 표시 검증](docs/reviews/추가-몬스터감소-20261029.md)
- [하단 아이템 목록 검증](docs/reviews/하단-아이템목록-20261029.md)
- [전투 아이템 표시 검증](docs/reviews/전투-아이템표시-20261029.md)
- [병렬 아이템 검증](docs/reviews/병렬-아이템-20261029.md)
- [무작위 전리품·661회 검사](docs/reviews/무작위-전리품-20261029.md)
- [기능 구현 검증](docs/reviews/기능이행-20261029.md), [후속 구현·609회 검사](docs/reviews/후속개선-20261029.md)
- [장비·진형 자동 비교](docs/reviews/장비-자동비교-20261029.md), [원문137항목 대조](docs/reviews/요구사항-대조-20261029.md), [로컬 아이콘 시안](docs/art/README.md)

현재 구현: 32종 아이템·다중 무기 독립 발동·회전/배치/합성·회복 공유·비교/추천·10웨이브435초·진형3종. 초기 밸런스와 임시 아트이며 최종 게임 완성을 의미하지 않는다.

```sh
python3 Tools/Verify/run.py local
python3 Tools/Verify/run.py all
python3 UnityMigration/sync.py
```

이 저장소의 Git 원격은 [deadSurvivor](https://github.com/jaydentapwave/deadSurvivor)이며 기본 브랜치는 `main`이다. 사용자가 지정한 공개 저장소이며, 로그인 정보와 임시 생성물/실행 로그는 `.gitignore`의 제외 범위를 유지한다. 실제 Unity 프로젝트의 저장된 필수 파일은 [UnityProject](UnityProject/README.md)에 포함한다. Unity Hub에서 이 하위 폴더를 열며, 캐시/빌드/검증 캡처는 다시 생성한다. [파일 이관 검증과 기존 미해결 참조](docs/reviews/unity-project-019-review.md)를 확인한다. 현재 개인 실행 경로와 검증 도구 대상은 기존 Desktop 프로젝트를 유지한다.

- [3D 배경 신비한 숲 분석 준비](docs/design/3D배경-신비한숲-분석준비.md) — 구역·지도·화면 대응 정리, 제작 전.

- [꿈꾸는 섬에서 참고할 3D 배경 요소](docs/design/3D배경-꿈꾸는섬-참고범위.md) — 제작 전 적용 범위 기준안.

- [첫 3D 숲 맵 구성 기준안](docs/design/3D배경-첫맵-구성기준안.md) — 고정 공터 제안과 경계·스폰·진형 검토.

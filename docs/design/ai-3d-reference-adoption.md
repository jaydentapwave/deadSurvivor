# AI 3D 자산 워크플로우 참고 설계

작업 ID ASSET-PIPELINE-011 · r001 · 2026-10-31 KST

상태: 설계 기준, 독립 설계·최종 문서 검토 완료. [검토 기록](../reviews/asset-pipeline-011-review.md)을 따른다. 문서 설계만 변경하며 생성기·Blender·Unity 연결 구현은 이번 범위에 없다.

## 작업 브리핑

- 사용자 요구: Unity MCP, MCP for Blender, GameFactory-3A에서 필요한 부분을 구체화하고 기존 워크플로우·하네스 설계에 반영한다.
- 규모: L2 설계 작업. 자산 작업 상태·증거 계약·자산/맵 소유권 경계를 새로 정의한다. 독립 design-reviewer 검토 후 문서에 연결하고 validator-client로 최종 검토한다. 기하 계산/런타임 알고리즘은 변경하지 않아 algorithm-tester 대상은 아니다.
- 확정 방향: 프로젝트 로컬 Tripo CLI/API, 기존 Unity CLI, 개인 프로젝트 내부 기록, 단일 바위 공개 예외의 범위 유지. 도구 선택은 추가 생성·결제·공개·설치 승인이 아니다.
- 제외: 외부 저장소 복사/설치, MCP 추가, 전역 설정, 유료 생성, Blender/Unity 조작, 원본 이미지/모델 변경, map v1 schema/검사 코드/hook/baseline 변경.
- 현재 소유권: PrototypeData/DSGame은 게임 상태, 007 및 후속 채택 C는 맵 배치/충돌, Tools/Verify는 기존 검사, 자산 전송 시험은 독립 시연이다. 새 자산 절차는 이 소유권을 바꾸지 않는다.
- 변경 전 → 후: 생성 성공/다운로드/화면 확인의 개별 시험 → 자산 계약·작업 복구·원본 보존·후처리·Unity 재측정·시각 비교·채택 등록을 잇는 설계. 기존 G0~G5와 H1~H5는 유지한다.
- 추가 문서: 본 문서는 출처와 채택 판단/브리핑의 원본. 후속 `docs/workflows/ai-3d-assets.md`는 작업 단계·책임, `docs/design/ai-3d-asset-harness.md`는 자료/판정/실패 시험의 원본으로 작성한다. 양식은 규칙을 복제하지 않고 각 원본을 참조한다.
- 연결할 기존 파일: development, map-artifact-contract, verification-gates, 맵 워크플로우·구현·데이터 계약, map-work-package, CLI 결정, README/현재상태/할일/하네스/기획 입구.
- 실패/취소 경계: 제출 결과 불명은 재생성하지 않고 차단; 수락된 작업 ID로 상태/다운로드 재개; 처리/가져오기 실패는 기존 승인 자산 보존; 사용자 변경과 충돌하면 자동 덮어쓰기 금지. 생사/정비 동작은 변경하지 않는다.
- 호환성: 새 자료는 별도 asset schema/sidecar로 설계한다. 기존 map request v1에 필드를 넣지 않는다. 연결 구현은 별도 L2 검토와 검증을 거친다. 기존 바위 시험에 새 기준의 PASS를 소급하지 않는다.
- 검증 계획: 출처/코드 대조, 단계·ID·실패 시나리오 수동 대조, 독립 설계/최종 검토, `python3 Tools/Verify/run.py local`, 최종 `/usr/bin/python3 Tools/Verify/gate.py check`. 이번 자산/맵/Unity 동작 실행은 NOT_RUN.
- 미정: 자산별 삼각형·텍스처·가림·성능 수치, 크기와 화면 비교 허용치, 실제 CLI 기능/버전/형식 호환성. 준비 설계는 진행하되 해당 자산 채택은 기준 동결 전 BLOCKED. 비용 없는 fixture 개발은 별도 기준으로 가능하다.

## 출처 고정과 관찰 범위

조회 2026-10-31. 스타 수는 당시 GitHub API 값이며 실행 품질의 증거가 아니다. 최근 push와 기능 출시일을 동일시하지 않는다. 아래 커밋의 문서/코드만 근거로 삼으며, 우리 환경에서 설치·실행한 결과가 아니다.

| 근거 ID | 프로젝트 / 스타 | 고정 원본 | 확인한 범위 |
|---|---|---|---|
| EXT-B01 | MCP for Blender / 32,852 | [README · 992233bb](https://github.com/ahujasid/mcp-for-blender/blob/992233bb258975a05eec8b5691465c50c2831e34/README.md) | Blender addon과 제어 서버 분리, Python 조작, 장면 조회/캡처, 여러 3D 생성 서비스 연결. 최근 README 10-29 수정은 배지 추가 |
| EXT-U01 | Unity MCP / 15,571 | [generate_model · e2fd09c7](https://github.com/CoplayDev/unity-mcp/blob/e2fd09c71644d1d9fa1ae134526ce913cbf97536/website/docs/reference/tools/asset_gen/generate_model.md) | 생성/상태/취소, 작업 ID, provider/model/format/target_size와 가져오기 계약 |
| EXT-U02 | 동일 | [blender_bridge · 같은 커밋](https://github.com/CoplayDev/unity-mcp/blob/e2fd09c71644d1d9fa1ae134526ce913cbf97536/website/docs/reference/tools/asset_gen/blender_bridge.md) | Blender→GLB/FBX→Unity, 크기 조정, 프리팹, 양쪽 화면 비교 |
| EXT-U03 | 동일 | [CLI 문서 · 같은 커밋](https://github.com/CoplayDev/unity-mcp/blob/e2fd09c71644d1d9fa1ae134526ce913cbf97536/website/docs/guides/cli.md) | JSON 출력/대상 인스턴스/시간 제한. 일부 기능 실험적. 우리 Unity CLI와 다른 도구 |
| EXT-G01 | GameFactory-3A / 981 | [README · 9a873161](https://github.com/OpenDCAI/GameFactory-3A/blob/9a87316118b82c88458eaa38d69f961614b77af2/README.md) | models/operators/pipeline/engine_adapters 분리, Unity 등 엔진 사례. 기존 라이브러리 자산을 섞은 데모이며 완전 AI 제작 상용 게임 증거는 아님 |
| EXT-G02 | 동일 | [Tripo 작업 복구 · 같은 커밋](https://github.com/OpenDCAI/GameFactory-3A/blob/9a87316118b82c88458eaa38d69f961614b77af2/docs/tripo-task-recovery.md) | 수락 ID checkpoint, 동일 작업 잠금, 다운로드 URL 재조회, 재생성 없는 오류 보존. 응답 유실/저장 전 종료 및 정확히 한 번 과금은 보장하지 않음 |
| EXT-G03 | 동일 | [가져오기 문서](https://github.com/OpenDCAI/GameFactory-3A/blob/9a87316118b82c88458eaa38d69f961614b77af2/engine_adapters/unity3d/import_generated/README.md), [실제 C#](https://github.com/OpenDCAI/GameFactory-3A/blob/9a87316118b82c88458eaa38d69f961614b77af2/engine_adapters/unity3d/import_generated/ImportGeneratedMesh.cs) | 프리팹 생성·변환 뒤 재측정·삼각형/재질/텍스처/크기 보고. 문서의 실측 범위는 Built-in/GLB이며 URP/HDRP 및 FBX/OBJ 검증은 별도 |

Unity MCP 자료는 **beta 개발 브랜치의 고정 커밋**이다. [정식 v10.3.0](https://github.com/CoplayDev/unity-mcp/releases/tag/v10.3.0)은 2026-10-04이며 위 문서의 모든 기능을 그 정식판에서 실행 가능하다고 보장하지 않는다. GameFactory의 README 마지막 수정은 09-12, 저장소 push는 10-29다.

원자료는 프로젝트 `output/research/ai-3d-popular-20261031/`에 읽기 전용 조사 사본으로 보존했다. 영구 설계 근거는 위 고정 URL/커밋이며 output 삭제 후에도 식별 가능하다. 외부 파일의 설치 명령·스킬은 우리 프로젝트 실행 지침이 아니다.

## 필요한 부분과 채택 방식

| 채택 ID | 필요한 설계 / 근거 | 우리 적용 | 그대로 가져오지 않는 부분 |
|---|---|---|---|
| AD01 | 비동기 생성과 작업 복구 / EXT-G02, U01 | 제출 의도→수락 ID 저장→조회→다운로드; 불명 제출은 BLOCKED. 동일 계정/서버/요청의 잠금과 재개 | 자동 POST 재시도, 무제한 재생성, 중복 과금이 불가능하다는 주장 |
| AD02 | 역할 분리 / EXT-G01 | Tripo 실행 어댑터, Blender 처리, Unity 가져오기, 독립 검증/등록의 책임 분리 | 다른 엔진 전체 프레임워크나 게임 코드 복사 |
| AD03 | Blender 제어·조회·캡처 / EXT-B01 | 버전이 고정된 후처리 절차, 원본/처리본 분리, 처리 전후 실측·화면 | 매회 자유 프롬프트로 변형, 원본 덮어쓰기, MCP 전역 설치 |
| AD04 | Unity 가져오기와 최종 재측정 / EXT-G03, U02 | 개인 대상 확인, 격리된 후보 프리팹, 최종 크기·원점·재질·텍스처 확인 | 예제 경로/패키지 버전 고정 복사, 경고가 있어도 ok=true를 채택 PASS로 해석 |
| AD05 | Blender/Unity 화면 비교 / EXT-U02 | 같은 자산 해시와 고정 카메라를 갖는 비교 세트 + 실제 게임 카메라 판독 | 화면 유사도 한 점수로 통과, Blender 화면을 Unity 실행 증거로 사용 |
| AD06 | CLI 출력·대상·시간 제어 / EXT-U03, 기존 CLI 결정 | 반복 조회/검사를 로컬 실행기로 묶고 에이전트에는 요약·첫 실패·대표 화면 전달 | Unity MCP CLI를 기존 Unity CLI로 혼동, 필수 검사 생략으로 토큰 절약 |
| AD07 | 프로젝트 고유 보강 / 기존 맵 계약 | 승인 자산 등록부와 OB/RF/RT/MC 연결, C와 시각 모델 분리, 지문 기반 증거 무효화 | 생성 메시/자동 MeshCollider가 기존 C·통로·좌표를 다시 정의 |

AD07 및 아래 엄격한 채택/복구 기준은 **우리 프로젝트의 설계 결정**이다. 세 외부 저장소가 이미 이 전체 검사를 제공한다고 주장하지 않는다. 물리 크기·폴리곤 예산·픽셀 허용치는 외부 예제 숫자로 정하지 않는다.

EXT-G03에는 문서/코드 차이도 있다. README는 재질을 다시 쓰지 않는다고 설명하지만 해당 C#은 `RepairImportedModelMaterials.Repair`를 호출한다. 또한 기존 프리팹을 같은 경로에 덮어쓰며 예산 초과를 경고로 남긴다. 우리는 재질 변환을 별도 기록하고, 새 개정으로 후보를 저장하며, 필수 예산 초과를 채택 FAIL로 처리한다. 이는 검증 없는 코드 이식 대신 책임과 판정 방식을 참고하는 이유다.

### 채택 결정에서 검증까지의 대응

아래 사례는 [자산 하네스](ai-3d-asset-harness.md)의 예정 ID다. 연결을 문서화한 것이며 실행 결과가 아니다.

| 채택 | 반영 위치 | 향후 확인할 증거 |
|---|---|---|
| AD01 | 워크플로우 A1·유료 작업/재개, 하네스 request/journal | AC-02, AFI-01~04: 조회/다운로드 재개, 불명 제출 차단, create 횟수 |
| AD02 | 워크플로우 도구/책임, 하네스 생산자·소비자 표 | 원본→처리→임포트→판정의 receipt chain, AFI-05/09: 잘못된 연결·누락 검출 |
| AD03 | A2·입력 일관성·process receipt | AC-03/04-process/05-process/06-S1, AFI-06-source/process: 원본 보존·변환·예산·형태 |
| AD04 | A3·파일/복구·import receipt | AC-04-import/05-import/08-A3, AFI-07/10-A3: 실제 프리팹 재측정·누락·복구 |
| AD05 | A4·고정 시각 비교 | AC-06-S2/S3-asset/S3-map, AFI-08: 카메라/자산 일치와 항목별 직접 검토 |
| AD06 | A1 조회 제한·실행량/토큰 절, 구조화 결과 | request timeout/조회 한도·summary/missing_evidence, AFI-09: 요약이 누락을 숨기지 않음 |
| AD07 | A5·G5 연결, 기존 맵 규격/하네스 보충 | AC-09-pre/post/10, AFI-11~12 및 기존 MC-CAMERA-01/MC-PERF-01·영향 맵 사례 |

AD01/02/04의 복구·실측 기능은 외부 근거에서 관찰했고, 이 대응표의 정확한 파일 형식·정책·AC/AFI는 우리가 설계한 계약이다.

## 구현 우선순위와 완료 경계

1. 비용 없는 자산 계약/작업 기록/가짜 공급자 시험을 먼저 구현한다. 기존 H1 남은 인수·집계와 H2~H4 맵 작업의 선후관계를 바꾸지 않는다.
2. 기존 바위 원본 또는 로컬 단순 도형으로 Blender 처리와 Unity 가져오기/비교를 격리 검증한다. 과거 전송 성공을 새 기준의 통과로 재사용하지 않는다.
3. 자산 검증기·등록/복구·gate 연결을 구현하고 실패 주입을 통과시킨다. 현재 hook은 이 새 자산 검사들을 실행하지 않는다.
4. 실제 API 시험은 필요한 기능 확인과 요청별 모델/비용 상한/공개 범위/계정 권한 근거가 갖춰졌을 때 한 건으로 검증한다. CLI 선택만으로 새 과금을 시작하지 않는다.
5. 자산 단위 채택 후 G5 배치에 연결한다. G2~G4, 해당 맵 회귀, 카메라/성능 인수를 생략하지 않는다.

독립 검토와 실제 검사 기록은 [011 검토 기록](../reviews/asset-pipeline-011-review.md)에 남긴다. 설계 반영 완료, 도구 구현 완료, 자산 채택, 맵 단계 완료는 각각 별도 상태다.

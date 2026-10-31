# MAP-BLOCKOUT-018 — 3D 배치 미리보기 검증

2026-10-31 · L2 · [설계/사용법](../design/map-blockout-018.md). 완료 범위는 **PREVIEW_ONLY**다. 최종 맵·C/높이/축척/카메라 수치 채택, 전체 MapRequired/I3/G2/G3, 최종 미술/A5/G5, 물리 기기는 NOT_RUN이다. 추가 Tripo 생성/크레딧 소비는0이다.

## 구현·독립 검토

메인이 007의64개 다각형을 그대로 extrusion한 새 저장 장면, 기존 바위의 읽기 전용 배치, 4인 원기둥/키보드/공유 이동 핵심, 전용 UI/카메라와 독립 판정기를 구현했다. concave 윤곽·곡선 띠·하단 원형 조각의 빈 공간을 유지하고 bbox/hull로 바꾸지 않았다. source C는 여전히 BLOCKED 후보다. 바위 바닥과 보수적인 bounds 투영 전체를 C 내부에 검사하며 원본 prefab/material/mesh는 변경하지 않았다.

`/root/map018_design`의 설계 검토 PASS, `/root/map018_algorithm`과 `/root/map018_validator`의 코드 검토 PASS. 지적을 반영한 항목: 높이의 논리/월드 단위, top만 아니라 bottom cap/닫힌 extrusion 검사, 느린 이동을 벽 정지로 오인하지 않는 정지·반전/축소 이동 근거, 부모 wrapper 소유권, 바위 의존성 동결, 부분 저장 실패의 확인된 파일만 복구, 정상 저장 뒤 새 재질 정규화의 별도 완료 보고서 입력. 최종018 실행/원본 비율 화면 근거를 두 독립 검토자가 확인했고 PASS다. 별도 선분거리/포함 재계산으로2,250개 실제 이동 sweep의 최소 여유 .00878424 > .002와320개 world mesh 관측/64 ID·맵당1,772삼각형·체적을 확인했다. 기록된3,510은 축소 이동의 full-step 거부 질의도 포함한다. 기존 게임/최종 gate 근거 확인은 마감 기록에 결합한다. 기존 게임 검사 도구의 background 변경 순서도 Play 진입 후/종료 전 복원으로 수정해 동일한 자동 저장 부작용을 예방했다.

## 실행 근거

- [3D 장면 생성](../../output/map-blockout-018/build-e9e616ed1b0e4ebfa524f3e9b263ad59/summary.json): PASS.64장애물·바닥/외곽·메시/재질·Layout·prefab/scene의 새146파일 생성. 기존 프로젝트 파일 불변, 원래 scene setup/선택 복원. 소스와 기존 바위의 지문을 검사했다.
- [실제018 프레임 실행](../../output/map-blockout-018/bce8590619ec44ad8620197684b054e5/summary.json): **PASS**,18:11:17~18:13:02 KST. Unity 단위35개,5사례/250프레임, 독립 sweep3,510회, 각 사례의 실제 world mesh64개 검사, 콘솔/부모 정리 PASS. target-before/after의 Assets·Packages·ProjectSettings가 동일하고 기존 font도095b… 그대로다.
- [관측 판정](../../output/map-blockout-018/bce8590619ec44ad8620197684b054e5/judgments.json): s4/6/8 각각60스텝, 벽 앞34연속 정지, 전진39.375/39.07421875/41.6171875, 반전20스텝의140논리 단위 귀환. 근접 카메라40스텝, 가상 Keyboard30스텝/210논리 단위. Center/actual/reserved와 alpha 격자·초기 진형·추종을 독립 검사했다.
- [실제 메시 대조](../../output/map-blockout-018/bce8590619ec44ad8620197684b054e5/mesh-audits.json): 각 축척의64 ID/원본 vertex 투영·높이·cap 내부/면적/겹침·winding·닫힌 edge·체적·바위 접지/포함 PASS.
- [전체 화면](../../output/map-blockout-018/bce8590619ec44ad8620197684b054e5/overview.png), [45도 근접](../../output/map-blockout-018/bce8590619ec44ad8620197684b054e5/near-45.png), [55도 근접](../../output/map-blockout-018/bce8590619ec44ad8620197684b054e5/near-55.png)을 직접 열었다. 원본의 외곽·군집·띠·하단 분리 원형 구조를 확인했다. 45도에서 바위/플레이트 뒤 가까운 두 파티원의 하단이 가려지고55도에서 완화되지만 발이 완전히 노출되지는 않는다. 카메라/장애물 높이 최종 채택 PASS로 취급하지 않는다. 저장 장면의 단순 기본 메시와 최종 미술을 구분한다.

[Python 전체124단위](../../output/map-blockout-018/python-units.json)는 현재51소스 지문 안정성을 확인하며 PASS다. [017 실제 이동 회귀](../../output/map-i2-017/f750d87374e34400be6b54ac09bb7ed8/summary.json)는42사례/12재현쌍/7,491sweep·Unity63단위·콘솔/정리/기존파일불변 PASS다. 이후 바뀐018 전용 Python 두 파일과 기존 all 검사 도구는017이 호출/import하지 않아 관련 소스 지문이 동일함을 독립 검토자가 확인했다. [기존 게임 all/screens](../../output/verification/20261031-181334-4df844a3/summary.json)는 PASS다. 컴파일·695규칙·실제 프레임/입력·3해상도24화면·콘솔/정리·소스 안정성과 실행 전후 gate context가 일치했다. 수정한 background 순서로도 기존 font/PlayerSettings의 바이트가 유지된다. 최종 독립 검토/local/gate는 `output/map-blockout-018/final-checks.json`에 현재 실행 결과와 지문을 결합한다. 이 마감 파일이 생기기 전에는 해당 후속 실행 완료를 주장하지 않는다.

## 실패와 복구 이력

- [통제한3메시 저장 실패의 복구](../../output/map-blockout-018/recover-e1bcf07c264d4d58bbf3cab6c5475149/summary.json): PASS. 확인된 이번 생성 파일만 제거하고 기존 대상 전체를 복원했다.
- 최초 정상 저장의 [BLOCKED](../../output/map-blockout-018/build-8e268c19581b49f681f11834cbb490a5/summary.json)는 기존 PrototypeFont의 atlas pixel buffer 한 줄이 함께 저장된 부작용을 감지했다. 기존095b… checkpoint와 그 외1540파일/글꼴 meta·구조가 일치함을 확인하고 [해당 한 파일만 복구](../../output/map-blockout-018/font-restore-summary.json)했다. 미리보기 Play 전 발생이므로 preview 렌더링이 원인이라고 단정하지 않는다. 이후 공유 동적 캐시와 분리하고 새 Build/Play의 바이트 불변으로 확인했다.
- [완료 생성물146파일 복구](../../output/map-blockout-018/recover-488975a5296e4b8eae6053ee43a3998e/summary.json): PASS. 원 journal 보존, 동결 완료 Build와 같은 경로 집합/최종 지문 확인 뒤 이번 폴더만 제거했다.
- 첫 Play의 [FAIL/BLOCKED 정리](../../output/map-blockout-018/e0b29a99b3de4d2b9b85e468032b48cf/summary.json): URP가 첫 render에 PreviewSun의 light data를 추가해 소유 트리 검사가 차단됐다. 기존 봉인된 구성요소 손실0/추가1개를 직접 확인하고 해당 자동 요소만 제거한 같은 호출에서 소유 Play를 종료·복원했다. 이때 runInBackground0→1의 PlayerSettings 단일 변경도 검출해 원래 바이트로 복구했다. [좁은 복구 기록](../../output/map-blockout-018/tree-conflict-diagnostic/recovery.json)은 이후 명시적 자체 소스 수정 한 개의 이관도 구분한다. 검사 완화 없이 조명 선행 초기화와 Play 안에서만 background 변경/종료 전 복원을 구현했고 현재018 실행에서 정리/기존 파일 불변을 확인했다.

- V018-04: 초기 [018 실행](../../output/map-blockout-018/963865121fca4b6483e346a350439522/summary.json)의 camera3088×1440과 PNG1280×720 비등방 축소를 독립 검토가 발견했다. 기존 동작 판정과 시각 증거를 분리하고 초기 이미지는 채택 근거에서 제외했다. CLI에 native치수를 명시하고 PNG IHDR/CLI결과/camera치수 및 frozen pose의 전후 카메라 상태를 대조하는 검사를 추가했다. 중간 [b09 실행](../../output/map-blockout-018/b09b5477028c4c5cacd02681fac924e9/summary.json)은 카메라 각도의 C# double→float 호출 오류로 BLOCKED/cleanup PASS였고 정수 인수로 수정했다. 위 최종018의 세 capture JSON은 모두3088×1440·IDENTITY_NATIVE_PIXELS·전후동일·PNG지문을 기록한다.

# 바위 한 개 자산 전송 시험

[최소 설계](../../docs/design/asset-pipeline-minimum.md)의 `TRANSFER_TEST` 구현이다. 보존된 Studio 바위 파일을 사용하며 새 생성·결제·외부 업로드 명령은 없다. 전체 A0~A5 구현이나 게임용 자산 승인 도구가 아니다.

## 실행

프로젝트 루트에서 실행한다. 첫 명령이 돌려준 작업 경로를 이후 명령의 `<job>`에 넣는다. 절대 경로나 `rock-21300150/<작업 UUID>` 형식을 받는다.

```sh
python3 -m unittest discover -s Tools/Assets -p 'test_*.py' -v
python3 Tools/Assets/pipeline.py init --spec docs/design/asset-rock-test-contract.json
python3 Tools/Assets/pipeline.py resume --job <job> --stop-after-source
python3 Tools/Assets/pipeline.py status --job <job>
python3 Tools/Assets/pipeline.py resume --job <job>
python3 Tools/Assets/unity_asset.py --job <job>
```

`--stop-after-source`는 복사 완료 후 종료하여 새 프로세스의 이어하기를 시험하는 옵션이다. 생략하면 처리까지 진행한다. 이미 PROCESSED인 작업에 `resume`을 실행하면 지문과 계약 결과를 다시 확인하고 재처리하지 않는다. 원본/완료 파일의 변경은 차단한다. 처리 의존성 개정 후에는 아래 새 job 재사용을 사용하고 과거 증거를 보존한다.

## 단계별 재사용 — schema2

```sh
python3 Tools/Assets/pipeline.py init --spec docs/design/asset-rock-test-contract.json --reuse-from <기존-job>
```

새 job을 만들고 원본과 처리 결과의 전체 파일·해시·계약을 검증한 뒤 복사한다. 기존 job은 수정하지 않는다. stdout의 phase와 reuse 범위로 다음 작업을 정한다.

| 결과 | 의미·다음 작업 |
|---|---|
| PROCESSED / RAW_AND_PROCESSED | spec·원본·`pipeline.py`·`process_blender.py`·Blender 실행 파일 경로/바이트 지문 동일. Blender 재실행 없이 Unity 단계로 진행 가능 |
| SOURCE_READY / RAW_ONLY | 원본은 유효하나 처리 의존성이 달라짐. 새 job에서 resume로 처리 필요 |
| BLOCKED | 손상·누락·다른 출처·동시 실행·복사 도중 변경. 실패 기록을 확인하고 자동 덮어쓰기/성공 승격 금지 |

Unity의 `UnityAsset.cs`/`unity_asset.py`나 별도 출력 요약 코드 변경은 Blender 결과를 무효화하지 않는다. 공통 판정기가 있는 `pipeline.py` 변경은 보수적으로 처리 재실행을 요구한다. Unity 실행은 매번 새로 수행하고, 시작/종료의 Unity 도구 지문도 별도 검사한다. 이 도구에는 Unity PASS 캐시가 없다.

schema1은 기존 전체 도구 지문 규칙을 유지한다. `--reuse-from`으로 무결성을 확인한 raw만 새 schema2에 승계하고, 첫 처리는 다시 실행한다. 과거 job의 schema/도구 지문을 덮어쓰지 않는다. 처리 지문에는 Blender 실행 파일을 포함하므로 설치가 없으면 BLOCKED다. 상세 설계·한계는 [013](../../docs/design/efficiency-013.md)을 따른다.

로컬 Blender는 `/Applications/Blender.app/Contents/MacOS/Blender`를 사용한다. 개인 Unity 대상은 `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor`로 고정된다. Unity가 준비된 편집 상태이고 미저장 씬·자산, 기존 재생, reload 비활성, 기존 background override가 없어야 한다. 필요한 환경 권한이 없으면 차단 결과를 해결한 뒤 재시도하며 다른 프로젝트로 우회하지 않는다.

## 검사와 보존

- `job.json`: 기존 공급자/작업 ID, 계약·도구·원본 지문, CREATED → SOURCE_READY → PROCESSED 이벤트, 추가 생성 요청 수0.
- `raw/`: 원본 ZIP·FBX·텍스처의 검증된 복사본. 원본 위치는 수정하지 않는다.
- `process-<UUID>/`: FBX/텍스처, 전후 이미지, 처리 관측, 계약 결과, Blender 로그. 실패한 시도도 보존한다.
- `unity-<UUID>/`: 실제 측정·의존 파일 지문·프레임·카메라 캡처·콘솔·상태 복구·기존 파일 비교와 요약. 변경 전 개인 대상 파일은 `target-backup/`에 복사한다. 이 백업을 임의 자동 복원하지 않는다.
- Unity 후보는 매번 `Assets/DeadSurvivor/Art/Tests/Asset012_<UUID>/`에 추가한다. 기존 후보를 덮어쓰거나 게임 씬에 자동 배치하지 않는다.

시험 기준은 [JSON 계약](../../docs/design/asset-rock-test-contract.json)에 고정된다. Unity에서 측정한 원본의 TRIANGLE_BUDGET 실패는 검사기의 정상적인 불량 탐지다. 처리본은 양의 삼각형 수 ≤10,000, 메시/재질 각1, 2K 이하 basecolor/normal, 가로·깊이 최대2단위, 유한한 양의 크기, 바닥/가로중심 오차0.01, URP/Lit, 허용 컴포넌트만 포함해야 한다.

이미지 파일 생성과 시각 검토는 구분한다. 자동 요약의 `visual_review_status=NOT_RUN`을 덮어쓰지 않고, 직접 본 결과는 별도 검토 기록에 남긴다. 실제 게임 카메라/맵·모바일 성능·최종 스타일 검사는 이 전송 시험에 포함되지 않는다.

## 실패와 복구

로컬 작업은 원자 저장과 작업별 잠금으로 중복 실행을 막는다. 미완료 처리 시도는 보존하고 새 처리 폴더에서 재개한다. 완료 지문 불일치는 자동 고치기/기준 갱신 없이 BLOCKED다.

Unity는 변경 전에 `output/asset-pipeline/unity-pending.json`에 대상 PID·작업 소유권·원래 상태를 기록한다. 종료 시 자신이 시작한 재생을 끝내고 씬·선택·background·tick을 복구한 뒤 재조회한다. 미완료 표시는 다음 Unity 실행을 차단한다. 강제 종료 이후 자동 복구는 미구현이다. 원인/현재 소유권과 백업을 확인하여 수동 복구한 다음 별도 복구 증거와 함께 실패 기록을 보존해야 한다. 표시 파일만 지워서 재실행하지 않는다. 기존 파일 차이는 FAIL이며 무관한 사용자 편집을 덮어 복원하지 않는다.

API 작업으로 기록된 이미 수락된 ID에는 `pipeline.py query-task --job <job>` 읽기 조회가 있다. Studio 작업에는 금지되며 이번 실제 바위는 이 명령을 사용하지 않는다. 가짜 CLI 응답으로만 timeout/ID 불일치/재조회·비밀 URL 비기록을 검사했다. 실제 API 제출/다운로드·응답 유실 해결·과금 중복 방지는 미구현이다.

이 명령은 기존 `Tools/Verify/run.py local/all`과 별개다. 자산 시험 뒤 기존 게임 회귀는 `python3 Tools/Verify/run.py all --screens`, 최종 변경 gate는 `/usr/bin/python3 Tools/Verify/gate.py check`로 실행한다. 기존 gate의 PASS가 자산 시험 실행을 대신하지 않는다.

기존 게임 회귀 직후에는 dynamic font atlas의 메모리 변경 때문에 새 자산 시험이 차단될 수 있다. 이번 시험에서는 저장 파일이 같은지 확인하고 상태/파일을 백업한 뒤 개인 Editor를 정상 재시작하여 정리했다. 이를 자동 처리하거나 dirty 검사를 무효화하지 않는다. 원래 사용자 편집과 구분할 증거가 없으면 보존하고 중단한다.

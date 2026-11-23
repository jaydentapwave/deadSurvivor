# 7단계 이월 — 종별 지형 전이 검증 (9단계 첫 단위)

기준 커밋: `525231e396db6ed78e8a5ce72b8eb4d2b64acdad`. 구현 전 고정. 검증 fixture만 변경하는 L1이며 게임·DI·렌더 파이프라인 설정을 바꾸지 않는다. 독립 수정 확인은 spec-reviewer만 요청한다.

## 고정 matrix

| 행 | 평지 | 비탈 | 물가 | 턱 |
|---|---|---|---|---|
| 대원 Bruno (실제 Main view) | F | S | W | B |
| slime (실제 MonsterViewFactory) | F | S | W | B |
| mushroom (같은 풀 재사용) | F | S | W | B |
| rock (같은 풀 재사용) | F | S | W | B |
| boar (같은 풀 재사용) | F | S | W | B |
| puffer (같은 풀 재사용) | F | S | W | B |

각 칸은 5개 서로 다른 실제 프레임에서 위치가 연속 변화한다. 지점마다 같은 종이 직접 움직인다. 종을 한 위치에 세운 결과와 대원 tour를 합쳐 통과시키지 않는다.

- F: (0,0)→(1,0), 현재 Main 평지.
- S: (11,-8)→(13,-8), 현재 Main field_height의 비탈. 실제 높이·normal을 매행 기록한다.
- W: (7.5,5)→(8.5,5), pond의 서쪽 물가를 횡단한다.
- B: (7.3,8)→(8.3,8), pcg-bank의 남서쪽 경계를 횡단한다.
- 구역 사이 이동은 fixture 재배치다. 각 구역 안 5연속 프레임만 전이 증거다. 게임의 길찾기·스폰·정상 입력 경로를 증명하지 않는다.
- 모든 root 높이는 production과 같은 TerrainHeightMath.Sample이다. 턱 위를 걷는 게임 규칙을 새로 만들지 않는다. 높은 받침 위의 별도 동작은 이 fixture로 검증했다고 쓰지 않는다.

## 고정 판정

L7-03, L7-08의 미완료 수신·자원 동일성만 본다. `.04m` 그대로 유지. 행마다 종/구역/위치/높이/normal/Time.frameCount/mesh·공유 material·texture ID와 실제 scene-depth fragment 오차를 기록한다. production 출력과 그림자를 끈 같은 프레임의 차이로 실제 살아남은 픽셀을 정하고 diagnostic4의 깊이·support 값과 대응시켜 **잘못 받는 면에서 살아남은 픽셀 0, 수신 오차 .04m 이하**를 확인한다. 후보 픽셀과 실제 수신 픽셀을 구분한다. 수신 픽셀 0은 PASS가 아니라 미확인이다. 구역 분류·높이 변화가 실제로 확인되지 않은 칸도 미확인이다.

실제 Main URP·원근18° 카메라를 유지하며 카메라는 같은 상대 오프셋으로 fixture를 따라간다. 다른 actor의 표시만 임시 격리한다. mesh/재질/texture와 카메라·타임스케일·renderer enabled 상태는 finally에서 복원한다. 장면·재질 파일 저장0. 그림은 직접 열고 sha256을 남긴다.

## 이 단위에서 안 보는 것

효과/Bloom 양성/멧돼지 예고/UI/전체 테스트·한 판·관문·범위 대조표·preview는 **9단계 뒤 단위 몫, 이 단위 판정 밖**. C4 추정 문턱 FAIL·K55 NOT_RUN·N9 FAIL·C11 BLOCKED는 유지. 성능은 별도 단계, 이 단계 판정 밖. 배경의 평평한 초록과 소품의 도트 질감은 6단계 잔여이며 이 단위에서 고치지 않는다.

## 기준 문서와 다른 점·이미 결정된 것

사용자 확정 걷기24/대기12/공격7, Bruno fall12+lying·다른 대원10+lying, 피격추천안, 소품유료0, 나무3·덤불1, 기존UI 유지, 성능별도. 기준서 개정0. 기존 spec 필수 지적은 종×지형 연속 GPU 증거 부족이며 반박으로 돌려보낸 지적0. validator/algorithm의 앞 PASS는 이 fixture 변경으로 runtime 계산이 바뀌지 않는 한 재요청하지 않는다.

## 첫 회 실행 뒤 fixture 보완 (기준 변경 아님)

첫 120행은 `recheck2/matrix.json`에 보존했다. 원인: 모델에 없는 검증 몬스터를 registry에 남겨 두어 WorldPresenter가 다음 frame에 풀로 돌려보냈다. 따라서 몬스터 첫 장 이후 0표본이며 유효한 통과 증거가 아니다. factory.Create 직후 Detach하여 fixture 수명을 소유하고 ReleaseDetached로 반환하도록 고쳤다. 턱/물가의 바위 가림은 수신0으로 드러났으므로 두 번째 실행은 실제 ground/bank/water만 남긴 **지형 격리 판**으로 변경한다. production 지형 mesh·shader·원근·깊이식은 그대로고 소품·나무 표시만 임시 끈다. 최종 게임 화면 가독성 PASS와 구분한다. 출력 `recheck2/isolated/`. 모든 첫 실패 로그는 보존한다.

놓친 것과 워크플로우 갱신: fixture가 생산 registry에 등록되면 생산 정리 루프까지 따라야 한다. 데이터 모델에 넣지 않는 view fixture는 즉시 detach하고 실제 프레임 사이 active/mesh ID를 확인한다. 이 문서의 로컬 절차에 반영했으며 공통 규칙 파일은 변경하지 않았다. 새 전체 검사는 더하지 않고 기존 matrix의 프레임별0표본 검사를 활용한다.

지형 격리 실행의 대원은 비결정적 FindFirst로 Lena가 선택됐다. 숫자 결과는 삭제하지 않고 `isolated/`에 남기지만, 고정 matrix의 Bruno 증거로 쓰지 않는다. 마지막 실행은 `DQActorId == "bruno"`를 명시하고 출력도 `final/`로 분리한다. 나머지 카메라·경로·수신 문턱·샘플 수는 같다. `final/`만 최종 수정 확인 입력이다. fixture 대상은 개체 검색 순서가 아니라 데이터 ID로 고정해야 한다는 후보를 이 로컬 절차에 반영했다.

## spec 수정 확인 FAIL 뒤 받침면 보완 (2026-11-23)

수신한 판정은 **`VERDICT: FAIL`**, job `20261123-074748-37c915`다. L7-03 partial, L7-08 met. 필수 지적 둘을 모두 수용했다. 이전 `final/`은 그 리뷰가 본 고정 증거로 보존하며, 이 회차 입력은 `recheck3/`다.

- 앞 bank 여섯 칸은 전부 **미확인 — bank/물가 경계 표본만 있음, 높은 받침면 미검증**이다. Bruno 칸의 `coverage: sampled`도 높은 턱 수신 완료를 뜻하지 않는다. 다음 review-input의 칸 상태에서 바로잡는다.
- 새 `StartTransitionMatrix(true)`는 실제 Main `bank` MeshFilter의 위쪽 삼각형을 XZ barycentric으로 찾아 (18,15)→(19,15)의 5점을 각 종마다 순서대로 이동시킨다. 높이는 게임 grid가 아니라 **실제 bank mesh 삼각형의 y**다. 삼각형 번호·높이·grid와의 차·메시/재질/texture ID를 기록한다. 새 게임 이동 규칙·데이터·bank shader·material·mesh 변경0. 카메라/renderer 상태는 기존 fixture와 같이 복구한다.
- Bank 재질의 `_FlatDepth`도 읽어 기록한다. 기존 생산 설정에서 실제 받침면을 받지 못하면 실제 실패로 남긴다. fixture용으로 `_FlatDepth`를 바꿔 통과시키지 않는다.
- 침범0의 측정 범위는 **coverage(alpha×opacity) > .01이며 ZTest와 후속 투명 패스를 통과해 diagnostic 태그가 남은 후보 픽셀**에 한정한다. 더 옅은 모든 픽셀의 침범0으로 확대하지 않는다. 후속 지적을 필수 새 관문으로 승격하지 않는다.

원인과 워크플로우 반영: 구역 이름과 경계 부근 표본만으로 받침 높이를 확인한 칸처럼 분류했다. 높은 받침 표본은 실제 삼각형 높이와 root 높이를 함께 기록하고, 경계 표본/해당 면 수신을 서로 다른 상태로 표시하도록 이 로컬 절차에 반영했다. 별도 전체 검사를 추가하지 않고 기존 matrix의 위치·수신 검사에 합친다. 앞 수신0 11프레임·C4 FAIL·K55 NOT_RUN·N9 FAIL·C11 BLOCKED는 그대로 이어진다. 반박으로 돌려보낸 지적0.

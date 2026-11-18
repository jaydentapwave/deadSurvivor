VERDICT: FAIL

DR-01~DR-06의 지정 코드 근거를 다시 읽었다. 설계 수정 방향은 반영됐지만, DR-02의 위치 검증 계약과 DR-06의 기록 실행 순서에 모순이 남았다.

| 지적 | 수정 확인 결과 | 설계 근거 |
|---|---|---|
| DR-01 | 해소. 좌표 형과 소비처를 W2에서 함께 이행하고 기존 공격 방향을 유지한다 | `design-brief.md:266-286` |
| DR-02 | 부분 해소. Factory·Registry의 `Vector3` 이행은 포함됐지만 N1 기대가 오프셋 식과 충돌한다 | `design-brief.md:276-277,393` |
| DR-03 | 해소. `Game.Editor`의 명시 참조 추가가 W2에 포함됐다 | `design-brief.md:50,325` |
| DR-04 | 해소. 실제 transform으로 초점을 복원하며 입력 순서·갱신 누락 시험을 포함한다 | `design-brief.md:245-257` |
| DR-05 | 해소. 지난 방향에 따른 변경 범위와 D7, W4 재측정을 포함한다 | `design-brief.md:211-216,327,381` |
| DR-06 | 정책은 해소. 미판정 분리는 디렉터 결정에 부합하지만 기록 절차를 실행 가능한 형태로 고쳐야 한다 | `design-brief.md:333-339` |

## 필수 수정

- **DR-02-FIX — 피해 숫자의 위치 식과 시험 기대를 일치시킨다.**  
  근거: `docs/features/hd2d-screen/design-brief.md:55,276,325-326,393`.  
  `UprightOffset(h) = (0, h·cosθ, h·sinθ)`이므로 30°에서 높이·상승량만큼 z도 변한다. 그런데 N1은 시작·상승 중 숫자의 x·z가 발 위치와 같아야 한다고 요구한다. 식대로 구현하면 시험이 실패하고, z를 고정하면 세운 축 방향 상승을 위반한다.  
  **수정 방향:** 바닥 앵커와 표시 위치를 구분한다. 앵커는 발 위치를 유지하고, 표시 위치는 `anchor + UprightOffset(headHeight + rise)`인지 검사한다. W3에서 로컬 y로 옮겨도 최종 월드 위치가 이 식과 같고, 풀 재사용 때 앵커가 교체되는지 확인하도록 통일한다.

- **DR-06-FIX — 측정 시작·대기·저장을 두 단계로 나눈다.**  
  근거: `docs/features/hd2d-screen/design-brief.md:337`, `Tools/playable.py:278-295`.  
  설계는 한 `PERFREC` 단계에서 `do → sleep → do`를 요구하지만 실행기는 `do` 하나를 대기 전에 실행한다. 두 호출을 같은 `do`에 합치면 3초 표본 수집 전에 저장된다. 실제 `run_step` 함수의 모의 실행에서도 호출 후 대기 순서를 확인했다.  
  **수정 방향:** 첫 단계에 `MarkFrameStats()`와 `sleep: 3`, 다음 단계에 `WritePerfRecord(...)`를 둔다. 성능은 계속 `NOT_JUDGED`로 별도 보고한다.

## 후속 제안

- **DR-F03 — 위험 대응 문구를 최신 결정과 맞춘다.**  
  근거: `docs/features/hd2d-screen/design-brief.md:346,481`.  
  위험 표에는 범위 고정 대안과 카메라 배율 원복이 남아 있고, 13절은 데이터 조정을 확정했다. 구현자가 폐기된 대안을 선택하지 않도록 적용 여부를 명시하면 좋다.

추가 컴파일 순서 결함은 정적 대조에서 확인하지 못했다. `project-doctor`는 `configured`, findings 없음이었다. Unity 컴파일·EditMode·Play·렌더 캡처는 **NOT_RUN**이다.

읽기 전용 지시에 따라 파일을 수정하거나 `docs/features/hd2d-screen/design-review-fix-r001.md`를 저장하지 않았다.
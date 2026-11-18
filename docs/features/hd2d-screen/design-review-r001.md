VERDICT: FAIL

기준 HEAD `e45394d`와 지정된 설계·상위 문서·현재 코드를 읽기 전용으로 검토했다. 카메라 위치의 단일 소유권과 DI 방향은 타당하지만, 아래 누락 때문에 W0~W7의 컴파일·기존 판정 유지가 보장되지 않는다.

## 필수 수정

- **DR-01 — W2 좌표계 변경과 소비처 이행이 분리되어 있다.**  
  근거: `docs/features/hd2d-screen/design-brief.md:274`, `Assets/Game/02.Scripts/Features/Combat/Service/CombatService.cs:407`, `Assets/Game/02.Scripts/Features/Feedback/Presenter/DamageNumberPresenter.cs:121`, `Assets/Game/02.Scripts/Features/Feedback/Presenter/MonsterHpBarPresenter.cs:238`.  
  `FloorToWorld`를 `Vector3(x,0,z)`로 바꾸면, W4까지 남는 공격 방향 호출은 `Vector2`로 암시 변환되며 세로 성분을 잃는다. 위쪽 공격도 이전 방향에 의존하게 되어 타격 시각과 W2의 G5 측정이 오염된다. 두 표시 Presenter의 `Vector3 + Vector2` 연산도 연산자 모호성으로 컴파일을 깨뜨린다. **W2에 기존 화면 방향을 유지하는 변환과 모든 소비처의 컴파일 대응을 포함**하고, 시각 구조 완성만 W3로 미뤄야 한다.

- **DR-02 — 피해 숫자의 위치 저장 필드·팩터리 이행이 빠졌다.**  
  근거: `docs/features/hd2d-screen/design-brief.md:275`, `Assets/Game/02.Scripts/Features/Feedback/Factory/DamageNumberViewFactory.cs:35`, `Assets/Game/02.Scripts/Features/Feedback/Registry/DamageNumberViewRegistry.cs:21`.  
  W3 변경 목록에는 Presenter·View만 있고, 팩터리 인자와 `DamageNumberEntry.StartPosition`은 `Vector2`다. 새 월드 위치를 전달하면 z가 소실되어, 원점에서 떨어진 몬스터의 피해 숫자가 다른 바닥 위치에서 떠오를 수 있다. **팩터리·등록부까지 위치 계약을 이행**하고, `z ≠ 0`인 대상에서 생성·상승·풀 재사용을 확인해야 한다.

- **DR-03 — 이행 메뉴가 사용할 새 어셈블리의 Editor 참조가 누락됐다.**  
  근거: `docs/features/hd2d-screen/design-brief.md:48`, `:50`, `:70`, `Assets/Game/02.Scripts/Editor/Game.Editor.asmdef:4`.  
  설계는 `Game`·시험 어셈블리에만 `HD2D.View` 참조를 추가한다. `autoReferenced:false`인 새 컴포넌트를 이행 메뉴에서 직접 추가·조회하려면 `Game.Editor`에도 명시 참조가 필요하다. **해당 참조 추가를 W2 작업에 포함**해야 한다.

- **DR-04 — 새 `CameraError`는 실제 카메라 추적 실패를 검출하지 못한다.**  
  근거: `docs/features/hd2d-screen/design-brief.md:77`, `Assets/Game/02.Scripts/App/PlayableProbe.cs:195`, `Tools/playable/f2-movement-flows.json:45`.  
  `Rig.Focus`의 맞추기 전 입력만 원정대 중심과 비교하면, Rig가 transform 갱신을 빠뜨려도 오차가 0이다. 현재 검사가 확인하던 “카메라가 따라감”을 입력 전달 검사로 약화한다. **실제 카메라 위치에서 거리·흔들림을 제거해 초점을 복원하거나, 원정대 중심의 실제 화면 투영을 검사**해야 한다. 입력 순서 두 가지와 흔들림 종료·일시정지도 포함해야 한다.

- **DR-05 — 공격 시각 변경 범위의 사실 주장이 틀렸다.**  
  근거: `docs/features/hd2d-screen/design-brief.md:211`, `:324`, `Assets/Game/02.Scripts/Effect/Facing/SpriteFacing.cs:27`.  
  기존 구현은 축 조건을 넘지 않으면 이전 `IsBack`을 유지한다. 예를 들어 지난 방향이 뒤쪽이고 입력이 `(1,0)`이면 기존은 `hitBack`, 새 8구간 판정은 Right의 `hitFront`다. 따라서 변경 범위는 “14°~22.5°뿐”이 아니다. **D7을 이전 방향까지 포함하는 회귀 사례로 수정**하고, 이 변경까지 포함해 웨이브 목표를 재측정해야 한다.

- **DR-06 — 성능 판정을 무조건 통과하도록 완화한다.**  
  근거: `docs/features/hd2d-screen/design-brief.md:282`, `Tools/playable/art-insert-flows.json:123`, `AGENTS.md:43`.  
  `<= 50`을 지우고 `>= 0`만 남기면 성능 초과도 PASS가 된다. 이는 판정 기준을 느슨하게 통과시키지 말라는 지침과 충돌한다. **2D 기준의 적용 종료와 새 HD-2D 예산을 기준 원본에서 명시적으로 결정**해야 한다. 예산 확정 전에는 성능을 미판정으로 구분하고 기능 결과와 별도로 보고해야 한다.

## 후속 제안

- **DR-F01 — 돌진 경계 사례를 추가한다.**  
  근거: `docs/features/hd2d-screen/design-brief.md:135`, `Assets/Game/02.Scripts/Features/Monster/Service/Behavior/ChargeBehavior.cs:73`.  
  충돌은 보정 전 선분으로 계산하고 최종 위치만 보정하므로 두 경로는 일반적으로 다르다. 현재 데이터·지원 화면비에서 실제 피해 오판이 발생하는지는 확인하지 못했다. 경계를 넘는 돌진에 대해 피해 대상·횟수·최종 위치를 검사하고, 도달 불가능하다는 판단이면 그 제약을 근거로 남기는 것이 좋다.

- **DR-F02 — 렌더 대안의 동일 조건 비교를 명시한다.**  
  근거: `docs/features/hd2d-screen/design-brief.md:294`, `:295`.  
  오버레이 카메라와 바닥 메시 대안은 합리적이다. 전환 시에도 흐림 띠 속 숫자·HP 막대, 앞 몬스터와의 겹침, 실제 바닥 그림자 수신을 같은 캡처 조건으로 확인해야 한다. 타일 데이터 개수는 메시 렌더링 성공의 근거가 되지 않는다.

질문별 판단은 다음과 같다.

| 질문 | 판단 |
|---|---|
| 상태 소유권·DI | 단일 Rig 소유와 데이터 주입에 순환은 보이지 않는다. Editor 어셈블리 참조는 보완 필요 |
| 위험 (a) | 데이터 조정 경로는 타당한 시도다. 목표 전체 회복은 실행 전 보장 불가이며 DR-01·05 해소 후 측정 필요 |
| 위험 (b) | 입력마다 재계산하는 방식은 타당하다. 판정 진입점은 DR-04 수정 필요 |
| 위험 (c) | 선분 불일치 가능성 있음. 현재 조건의 실제 피해 결함은 확인 불가 |
| 위험 (d) | 대안은 적절하다. 실제 렌더 결과는 미검증 |
| 위험 (e) | `SpriteFacing8` 병존 후 일괄 교체 순서는 타당하다. 전체 단계의 녹색 유지는 별개로 DR-01~03에 막힘 |

실행한 확인은 로컬 문서·코드 조회와 `project-doctor`이며, 후자는 `configured`, findings 없음이었다. Unity 컴파일·EditMode·Play·렌더 캡처는 **NOT_RUN**이다. 파일은 수정하지 않았으며, 결과 파일 저장은 브리핑대로 중계가 수행한다.

(Codex gpt-6.1-sol, 추론 medium, read-only, 237초, job 20261118-030958-d1c182, 로그 /var/folders/4h/db8m8fxj01519w6zwv8jlpwm0000gn/T/deadsurvivor-codex-review/20261118-030958-d1c182/log.txt)

# 9단계 놓친 것과 워크플로우 보완

- 2026-11-23: 경계 부근 rooty0 표본을 높은 턱 수신으로 취급할 위험을 spec 리뷰가 잡았다. `light-transition-matrix.md`에 구역/높이 미확인 상태를 고정했고, 실제 bank mesh 받침 fixture로 30프레임 수신0을 재현했다. **반영**: 위치 분류와 실제 받침 높이를 별도로 남긴다. 수신0은 통과가 아니다.
- 높은 bank 실제 깊이만 비교하는 설계가 보이는 opaque 가림을 깨는 10/11/12 반례를 design 리뷰가 잡았다. 승인된 설계에 color-pass ownership/reference-depth와 동률 검사를 넣었다. **반영**: `raised-bank-design-brief.md` RB-02. 구현/GPU 증거는 아직 없음.
- 0.26/.5/.7초 경계는 단순 float 나눗셈의 반올림 때문에 첫 시험에서3건 실패했다. 원본800000μs를 기준으로 정규화 후 경계를 정수로 고정해 같은 기대식13/13 통과. **반영**: `DQTelegraphTests`의 정확한 경계 입력을 보존한다.
- 공격 별도 효과와 몸+효과 비교 폴더를 혼동한 pack을 발견했다. 실제 반입 전 `effect_*/slash_*`만 선택하도록 고쳤다. **반영**: `dq_attack_fx_pack.py` 출처 SHA/상대 경로와 native72×72 alpha 검사를 남긴다.
- 전체 시험에서 scene unload와 ambient Tick의 순서 창에서 파괴된 ParticleSystem 접근을 발견했다. Unity null 비교를 첫 접근 앞에 두고 실제 파괴 객체 시험 및 기존 Main lifecycle integration을 다시 실행했다. **반영**: 관련 두 시험에 final tick 포함. 판정식 완화/로그 무시 없음.
- 효과 fixture 첫 실행은 JSON Vector3.normalized 순환 직렬화로 기록만 실패했다. float[]로 바꿔 다시 캡처한다. **반영**: 증거 객체에 Unity getter 전체를 직렬화하지 않고 필요한 스칼라/배열만 쓴다.
- 비용 큰 전체 EditMode/한 판은 마감1회씩 실행했다. 뒤 수정은 실패한 통합 시험1건·Ambient 관련11건만 재실행했다. **검사 빼기 후보**: 결과가 없는 새 검사는 추가하지 않았고, 높은 비용의 전체 반복 대신 닿는 묶음을 유지한다.

시간: 시작22:37:08Z, 종×지형/턱 설계 단위22:37~23:07(상한30분), 효과·예고·메뉴/시험23:07~23:34, 전체시험/실제전투/전체경로·범위대조23:34부터. 종료 시각/나머지 활동은 최종 검증 기록에서 보완한다. 토큰 사용량은 호스트가 제공하지 않아 **미확인**이며0으로 적지 않는다. `Tools/Harness/session_time.py --markdown`은 세션jsonl이 없어 ValueError(max empty)로 실행 불가였다. 하위 에이전트0, 유료0, 성능측정0.

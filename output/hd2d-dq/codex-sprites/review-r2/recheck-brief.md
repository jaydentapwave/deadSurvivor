# C2 수정 확인 — 두 묶음만 재판독

기준 커밋 `37429ee0212dba8029702a6be160185dc2361e9c`. 수정 커밋은 `commit.json` 참조. 이 문서는 재리뷰 브리핑이며 최종 핸드오프는 판정 뒤 작성한다.

## 받은 판정

- Claude general-purpose, Opus 5.5, effort medium. 잠근 blind 답과 second-stage 대조의 결론은 같음.
- 관문 evidence 원문: **`VERDICT: FAIL`**.
- 남은 필수 수정: 이전 `group04 C/walk_right`의 C4(같은 앞다리·갈색 쐐기), 이전 `group06 C/attack_right`의 C5(얇은 팔·자루 아래 막대·약한 준비 자세).
- 통과한 일곱 묶음은 `carried-pass.json`으로 이어 간다. 원본 PNG 28장뿐 아니라 GIF·프레임 줄도 수정 전후 지문이 같다. 새 PASS의 원문 `PASS (low confidence)`와 후속 의견도 `review-status.json`에 그대로 보존했다. B 정면의 낮은 확신, A 공격 손 옆 덩어리, C 정면의 떨어져 보이는 발은 이번 필수 수정으로 승격하지 않았다.

## 방법을 바꾼 부분

앞 방식은 다리 위치를 옮기고 원본 손 사각형을 덮었다. 손 사각형에 **원본 밝은 정강이 칸(x37,y55)**까지 들어 있어, 3장째에 순서를 바꾸려 해도 같은 다리의 앞면을 다시 덮었다. C2는 그 정강이 칸을 복원 마스크에서 뺐다. 손·장갑·테두리는 함께 유지해 금색 손가락만 떠 있지 않게 했다.

- 걷기 00·01: 먼 다리 → 가까운 다리 순서로 그린다.
- 걷기 02·03: 가까운 다리 → 먼 다리로 **실제 순서를 뒤집는다**. 반대 다리는 별도 어두운 금속 면을 갖고, 가까운 다리의 밝은 세로 반사는 다리 조각과 함께 뒤로 이동한다. `leg-order.json`, `repair.py`의 `walk()` 참조.
- 뒤의 갈색 장화는 따로 붙인 쐐기가 아니라 완전한 정강이·발 조각으로 붙인다. 예전 실루엣의 갈색 조각을 다시 덧씌우지 않는다.
- 공격은 단순한 얇은 연결선을 계속 고치지 않고 **국소 팔 조각을 새로 그리는 방식**을 골랐다. 원본 팔레트로 외곽 5칸·밝은 금속 면 3칸, 손 3×3칸. 팔 끝을 방패 밖으로 돌려 방패와 팔이 서로 지우지 않게 한다. 방패 원본은 앞에 유지한다.
- 준비 자세는 칼끝 y7, 원본 머리 꼭대기 y8이다. 전체 키는 이 장만57칸으로 기존56±1 허용 안이다. 머리·눈 자체는 원본 그대로다. 회복 03은 원본 그림을 그대로 사용해 자루 아래 남던 막대가 없다. `arm-geometry.json` 참조.

**C2는 순수 조각 이동 C의 성공으로 적지 않는다.** 앞뒤 순서·반대 다리 명암·국소 팔 그리기를 포함한 방식 전환이다. 처음 C와 앞 수정본은 기존 폴더 및 `review-r2/before/`에 보존했다. 추가 이미지 생성·Aether·Tripo·Unity 호출은0회, 누적 내장 생성은17회 그대로다.

## 확인 결과와 한계

- 기존 `sheet.py anim/grid`로 C 두 묶음의 4배·8배 GIF와 4배 줄을 다시 만들었다. 완성 8배 GIF를 디코딩해 8장을 번호 순서대로 직접 열었다. 지문은 `opened-files.json`.
- `numeric-observations.json`: 36장 전체 72×72, 키56~57, 바닥선64, 반투명0, 기준 팔레트 밖0, 원본 머리 불투명 칸 변경0. 키 허용은 처음 요청의56±1을 그대로 썼다. 발 부근 중심값에는 내려간 칼이 섞일 수 있어 정확한 x36 합격이라고 주장하지 않는다.
- 기존 `frames.py holes`의 FAIL은 그대로 남긴다. C 걷기00·02는14칸, C 공격00은38칸의 둘러싸인 빈 곳을 센다. 조각 분리 수는 모두1이다. 수정 후 생긴 팔·다리 사이의 빈 곳을 독립 판독 대신 PASS로 고쳐 적지 않는다. 로그: `C/walk_right/holes.log`, `C/attack_right/holes.log`.
- reviewer의 C3은 **not-run**: `NOT ASSESSED. The reviewer did not measure size or feet; it had no Pillow, and the decoded frames are preview-page format.`
- reviewer의 C6은 **not-run**: `NOT ASSESSED; the preview page was outside this read's scope.`
- 위 둘을 reviewer PASS로 올리지 않았다. 만든 쪽의 수치·링크·디코딩 확인은 별도 근거이며 독립 판독을 대체하지 않는다. 설치된 pixelcheck/motion/dot_retouch 부재, sheet.py decode 부재와 Pillow 대체는 이전 브리핑대로다.

## review_open — 원문 유지

아래는 reviewer가 별도로 분류한 **방법 제약·구현 측 방식 선택**이다. 막음 표시는 없다. 이것만을 이유로 재리뷰를 요청하지 않는다. 구현자가 허용 범위에서 C2를 선택한 사실은 결정란에 기록하되 원문은 없애지 않는다.

1. 낸 리뷰: general-purpose review-r1 second-stage comparison / group04
   > moving the legs ±4/5 cells changes stride length but never which leg is in front. That needs a change in draw order or a drawing of the opposite leg.
   결정: 원본 정강이 복원 마스크를 제외하고 02·03의 순서와 반대 다리 표면을 바꾸는 C2를 선택했다.
2. 낸 리뷰: general-purpose review-r1 second-stage comparison / group06
   > restoring the shield covers the arm, and restoring the arm covers the shield. That trade-off may need a newly drawn arm piece.
   결정: 원본 방패를 유지하고 밖으로 돌아가는 새 국소 팔 조각을 선택했다.

틀린 지적이라고 반송한 항목은 없다. 사용자 채택·40클립 양산·Unity 적용은 이 단계 판정 밖이다. 레포의 고장난/없는 검사 도구를 새로 만들거나 기준을 낮추지 않았다. 추가 독립 확인은 사용자의 이번 반복 지시로 요청한다.

## 요청할 재리뷰

**general-purpose (Claude Opus 5.5, medium) 1종**, C4·C5 두 항목만. 이전 FAIL의 두 C 묶음만 다시 본다. PASS 일곱 묶음과 후속 목록은 재리뷰하지 않는다.

첫 판독 입력은 기존 `part-check.md`와 `review-r2/judge/group01`·`group02`의 `reference.png`, `play.gif`, `decoded/00.png`~`03.png`만 준다. 100ms/장·4장·0.4초 반복. 이 브리핑과 `judge-map.json`은 답을 고정한 뒤 준다. 앞다리·팔·손과 물건을 장별 표로 적고, 고정된 첫 답을 C4·C5의 필수 지적과 대조한다. 마지막 `VERDICT:` 줄을 그대로 반환한다.

기준 문서와 다른 점은 C2의 국소 재작성이며 미리보기에 명시했다. 저장 경계는 output 안, 기존 PASS 유지, 회사 서비스·Assets·Unity 쓰기·push 없음이다. 최종 핸드오프 `.director/outbox/20261122-dq-codex-sprites-1.md`는 판정 뒤 쓴다.

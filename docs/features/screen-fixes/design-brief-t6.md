# 설계 브리핑 — 화면 고치기 T6: 전투 그리기 호출 50 이하

- 상태: 구현 기준
- 규모: L2 — 렌더 파이프라인 설정(URP 자산의 SRP Batcher)을 바꾼다(`.ai/rules/work-levels.md`).
- 이번에 사용할 수 있게 될 산출물·완료 조건: 할 일 목록 `.director/runs/20261117-screen-fixes/tasks.md` T6 — 웨이브 6 전투(1배 시간 3초) 그리기 호출 95백분위 ≤ 50(`tools/playable/art-insert-flows.json` PERF), M7 프레임 ≤ 16.7ms(`tools/wave_targets.py --metrics`), 겹 순서(아트 기준서 3.2)·색 그대로. 정식 채택.
- 요구와 제외 범위/후속 항목: 그림·규칙·수치·판정 기준(50)은 바꾸지 않는다. 품질(해상도·몬스터 수) 낮추기 없음. 아틀라스 3장 체계(기준서 3.6)를 새로 만드는 일은 하지 않는다(그림 넣기 실행 몫). 예전 3D 장면 성능은 후속 항목으로만 적는다.
- 관련 기획 및 사용자 확정 사항: 아트 기준서 3.6(그리기 호출 50 이하 출발값, 몬스터 동시 400 이하), 기획서 M7, 그림 넣기 AC14.

## 지금 그리기 호출이 어디서 나뉘는가 (측정)

증거 `.director/evidence/20261117-screen-fixes/2/1/`.

- 카메라는 `Game2DRenderer`(URP 2D 렌더러, `PC_RPAsset`·`Mobile_RPAsset`의 렌더러 1번)로 그린다. 두 자산 모두 `m_UseSRPBatcher: 1`, `m_SupportsDynamicBatching: 0`.
- Frame Debugger(`fd-before.txt`, 웨이브 6): 2D 패스의 스프라이트 전부가 **한 개의 `SRPBatch` 이벤트**로 나온다. 즉 재질·셰이더·텍스처 때문에 끊기는 것이 아니다. 그 밖은 타일맵 2, CopyDepth 1, Blit 1, 화면 UI 4.
- 그런데 `UnityStats.drawCalls`는 SRP Batcher 안의 그리기를 렌더러 하나당 하나로 센다. SRP Batcher는 set-pass를 줄일 뿐 메시를 합치지 않는다. 렌더러 표(`perf-diag-before.txt`)와 맞춰 보면 drawCalls ≈ 보이는 렌더러 수 + 8.
- 실행 중 SRP Batcher를 껐다 켜 본 결과(`perf-srp-toggle-before.txt`, 같은 판):

  | | 켬 | 끔 |
  |---|---|---|
  | 적 12~16 | 31 → 43 (적 수와 함께 늘어남) | 22 → 22 (적 수와 무관) |
  | set-pass | 8~14 | 20 |
  | 프레임 P95 | 2.65~6.57ms | 2.40~3.87ms |

  끄면 2D 렌더러가 같은 텍스처·같은 재질·연속 정렬의 스프라이트를 하나로 묶는다(동적 묶기 설정과 무관하게). 단, 끈 표본은 적 최대 14·16에서 22였을 뿐이고 더 많은 적·피격 효과 구간은 실제 자산 값으로 PERF·M7을 재서 판단한다(검토 F1).
- 몬스터 몸은 이미 모두 아틀라스 한 장(`Characters`)과 `Sprite-Unlit-Default` 한 재질이다. 따라서 **SRP Batcher가 켜져 있는 한 아틀라스 묶기·재질 공유·정렬 순서로는 줄일 수 없다**: 몬스터가 늘면(기준서 3.6 최대 400, 지난 측정 웨이브 6에서 34~46) 몸만으로 50을 넘는다. 지난 실행 `20261116-art-insert`의 57·62도 적 34~35에서 잰 값이다.
- SRP Batcher를 끈 뒤 남는 끊김(22의 내역 추정, 렌더러 표 기준): 대원 HP 막대 바탕·채움(정렬 1·2)과 빨간 잃은 HP 막대가 **대원마다 따로 만든 흰 상자 텍스처**(`WorldPresenter` 154행, `HpLossPresenter` 43행, 같은 14×2·같은 피벗)를 써서 대원 수만큼 끊긴다(최대 8~12). 몬스터 HP 막대는 이미 한 스프라이트를 같이 쓴다(`MonsterHpBarViewFactory`).

## 고칠 방법 후보와 고른 것

| 후보 | 결과 | 판단 |
|---|---|---|
| A. 아틀라스·재질·정렬만 정리(설정 변경 없음) | SRP Batcher 아래서는 렌더러 수가 그대로 drawCalls | 단독으로는 불가(측정 근거 위) |
| B. URP 자산 둘의 SRP Batcher 끄기 | 스프라이트가 묶여 적 수와 무관하게 약 22 | **고름** |
| C. 동적 묶기만 켜기 | 지난 실행 실험에서 변화 없음(SRP Batcher가 우선) | 버림 |
| D. 몬스터를 한 메시로 직접 그리기(인스턴싱) | 뷰·풀 구조를 다시 짜야 함 | 범위 밖, 버림 |
| E. 2D 렌더러를 바꾸거나 해상도·몬스터 수 낮추기 | 품질·규칙 변경 | 금지 |

고른 것: **B + A의 작은 부분**.
1. `Assets/Settings/PC_RPAsset.asset`, `Assets/Settings/Mobile_RPAsset.asset`: `m_UseSRPBatcher: 1 → 0`. 둘 다 바꾸는 이유: 에디터는 PC 품질, 기기는 Mobile 품질을 쓴다(`QualitySettings`). 측정과 기기 동작을 같게 둔다.
2. `PlaceholderSpriteFactory.CreateBox`: 같은 (가로, 세로, 색, 피벗) 요청이면 이미 만든 스프라이트를 돌려준다. 대원 4명의 HP 막대·잃은 HP 막대가 한 텍스처를 같이 써서 정렬 묶음마다 하나로 묶인다. 상자 스프라이트는 만든 뒤 아무도 픽셀을 바꾸지 않는다(grep 확인: `texture.SetPixel`·`Apply`는 팩토리 안에서만). 지우기는 지금처럼 scope 끝에 한 번.

## 기존 코드와 데이터 소유처

- 렌더 설정: URP 자산 두 개(공유: 예전 3D 장면도 렌더러 0번으로 같은 자산을 쓴다).
- 임시 상자 스프라이트: `Effect/Placeholder/PlaceholderSpriteFactory`(Scoped). 호출처: `WorldPresenter`(대원 HP 막대), `HpLossPresenter`(잃은 HP), `MonsterHpBarViewFactory`(몬스터 HP), `MonsterViewFactory`(돌진 선). 모두 흰색 또는 고정색 상자를 만들고 바꾸지 않는다.

## 변경 전 → 후 동작

- 그리는 결과: 같다. 정렬(정렬 층·순서·Y축 정렬)과 재질·색은 그대로이고 묶기는 그리는 순서를 바꾸지 않는다. 전·후 같은 장면 캡처(`10-MON`, `13-HIT`, 정비 화면)를 나란히 본다.
- drawCalls: 적 수와 함께 늘던 값 → 지금 표본에서 22, 공유 뒤 더 낮을 것으로 예상(목표 ≤ 50, 최종 판단은 PERF·M7 측정). set-pass: 8 → 약 10~20(예상). 프레임은 M7로 확인.
- 예전 3D 장면(`Assets/DeadSurvivor`, 참고용): SRP Batcher가 꺼져 3D 메시의 CPU 비용이 늘 수 있다. 지금 만드는 게임이 아니고 사용자 결정으로 손대지 않는 작업물이라 이번 판정에 넣지 않는다. 다시 3D를 하면 자산 값을 되돌리면 된다(핸드오프에 적는다).

## 실패·전환 경계

- 런 재시작·씬 다시 열기: 팩토리 캐시는 팩토리 인스턴스(Scoped) 필드라 scope가 끝나면 인스턴스와 함께 사라진다. 새 런은 같은 scope 안이라 같은 스프라이트를 계속 쓴다(픽셀을 바꾸지 않으므로 문제 없음). Dispose는 생성 목록(최초 생성만 등록)을 지우고 캐시도 비운다. scope가 끝난 팩토리 인스턴스를 다시 쓰는 경로는 없어 'Dispose 뒤 새로 만듦'은 시험 대상이 아니다(설계 검토 D1 반영: Dispose의 `Object.Destroy`는 EditMode에서 돌릴 수 없고 PlayMode 시험 어셈블리는 아직 없어 새로 만들지 않는다. Dispose 경로는 바꾸는 줄이 `Clear()` 한 줄뿐이라 마감 리뷰로 본다).
- 풀에서 다시 꺼낸 몬스터: 스프라이트 참조만 바뀌므로 영향 없음.

## 데이터 호환성과 복구 방법

세이브·CSV·JSON 변경 없음. 되돌리기는 자산의 한 줄(`m_UseSRPBatcher`)과 팩토리 변경을 되돌리면 된다.

## 최소 필수 검증

| 변경 | 실패 가능성 | 확인 |
|---|---|---|
| SRP Batcher 끄기 | drawCalls가 여전히 50 넘음 / 프레임 악화 | `tools/playable.py --route tools/playable/art-insert-flows.json`(PERF), `perf_probe.sh 3` 전·후, `perf_srp_toggle` 대신 실제 자산 값으로 다시 잼, M7: `tools/playable.py`(route.json이 `wave-metrics.json` 기록) → `wave_targets.py --metrics <그 파일> --require M7`(미측정이면 실패, 검토 F2). Mobile 품질로 바꿔 짧게 한 번 그리기 호출·화면 확인(검토 F3, 기기 성능은 미측정) |
| 상자 스프라이트 공유 | 대원 막대 크기·색이 바뀜 / 다른 호출이 같은 스프라이트를 바꿈 | EditMode 새 시험(같은 인자 = 같은 스프라이트, 색·크기·피벗이 다르면 다른 스프라이트; 시험이 만든 것은 `SpriteAtlasServiceTests`처럼 `DestroyImmediate`로 직접 지운다), 캡처 대조 |
| 전체 | 겹 순서·색 변화 | 같은 경로 전·후 캡처(`baseline-art-flows/` ↔ 뒤) 직접 열어 대조 |

- 초기 실제 연결: 자산 값 변경 → 에디터 재컴파일 → `perf_probe.sh 3` 한 번으로 drawCalls가 내려가는지 먼저 본다.
- 검사 확대/도구 신설 조건: 없음. 기존 판정 경로·`wave_targets.py`·진단 스크립트(증거 폴더 `tools/`, 게임 파일 아님)만 쓴다.
- 알려진 환경 차단과 재개 조건: 없음(에디터 7801 준비됨, 컴파일 0, EditMode 255/255).
- 미정 사항: 실제 기기의 set-pass 증가 영향은 이 실행에서 재지 않는다(에디터 측정만). 후속 항목.

## 설계 검토 결과와 해소한 지적

- 1회(Codex design-reviewer, job 20261117-145847-62b322): VERDICT: FAIL — 필수 D1(Dispose 시험을 EditMode에 둔 계획) 1건, 후속 F1~F3.
- D1: 'Dispose 뒤 새로 만듦' EditMode 시험을 뺐다. 이유는 '실패·전환 경계' 절(쓰지 않는 경로, PlayMode 어셈블리 없음). 같은/다른 인자 시험만 EditMode에 둔다.
- F1·F2·F3: 표현을 측정 범위로 좁히고, M7 명령에 `--require M7`, Mobile 품질 짧은 확인을 검증 표에 넣었다.
- 수정 확인(job 20261117-150110-7cf41c): VERDICT: PASS. 후속 F4(시험 정리 때 스프라이트 중복 제거, 텍스처도 DestroyImmediate)는 시험에 반영.

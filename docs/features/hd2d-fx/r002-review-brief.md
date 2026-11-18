# FX 마감 리뷰 필수 수정 확인 — r002

기준: 직전 제출48f3fc0edee4ca1976a19d1df8b40c22efc3ef2d → 이 문서를 포함하는 수정 커밋HEAD. 최초 기능 기준d3b01b15badf81361fcbfbe7d12f31cada741cf7. 작업은 동일 hd2d-fx 워크트리/claude/hd2d-fx, 메인 폴더 읽기만. 마감 핸드오프는 판정 반환 뒤 작성한다.

## 반환 판정(원문 줄)

| 역할 | evidence | 상태 |
|---|---|---|
| validator-client | VERDICT: FAIL | 아래VC-1 수정 확인 요청 |
| spec-reviewer | VERDICT: FAIL | 아래SP-1/SP-2 및 X-4 해석 확인 요청 |
| algorithm-tester | VERDICT: FAIL | 아래AL-1 비교 기준 통일 확인 요청 |
| Claude 시각 판독자 | VERDICT: PASS | 사용자 지정에 따라 유지, 재판독 요청 없음 |

리뷰 자체는 실행 세션에서 호출하지 않았다. 아래 명령 증거는 직접 실행 검증이고 독립 리뷰 판정이 아니다.

## 수정 대응과 확인할 범위

- **VC-1 / F5 / 자원 수명**: EffectSpriteCache.ReleaseChain(seed)가 끝난 발동의 모든 프레임 Sprite/Texture를 Destroy하고 두 관리 목록에서도 제거한다. CombatWorldPresenter는 해당 BurstView를 풀로 반납한 다음 호출한다. 사용 중인 다른 발동은 해제하지 않는다. ChainBoltRasterTests의200개 증가 seed×3프레임 회수 검사에서 각 자원 소멸/다른 살아 있는 발동 재사용을 확인한다. 실제 전투 종료 정비에서도 Scope를 유지한 채 chainEntries=0을 확인한다. 캐시 상한으로 활성 자원을 빼앗지 않는다.
- **SP-1 / AC1(R1), AC9(F3)**: ProjectileView.UpdateView가 방향/반전·그림 크기·pivot을 이용한 유효 피벗으로 기존 CardSnapMath를 적용한다. 바닥 뿌리/Shadow 위치는 그대로 두고 Body의 월드 모서리만 native1/48 격자에 맞춘다. 매번 upright.position을 기준으로 다시 계산해 누적 보정이 없다. CombatEffectTests에서 화살·볼트·불탄 각8방향의 실제 Sprite 꼭짓점을 월드로 변환하여 x와 세운 판up축 격자를 검사하고 뿌리/그림자 불변도 검사한다.
- **SP-2 / 단일 그림 복귀 계약**: CombatEffectData의 clips가 없거나 키가 없으면1장 클립, frames/fps 기본1, 방향 없는1장은 접미사 없이 원래 그림 키를 사용한다. Presenter의 체인·바닥 래스터·폭발 추가 고리는 단일 그림 복귀 경로에서 생략한다. 기존 clips15종은 유지하고 활성 옛 그림 참조를 추가하지 않았다. 구형JSON/기본값/실제 단일 그림 해석 시험을 추가했다. 실제 데이터 구조와 소비는 data-contract.md에 명시한다.
- **AL-1 / GroundMarkRaster·ChainBoltRaster 정식 기준**: raster-reference.md의 절대 경로·SHA256 r003으로 통일한다. 최초 외부 원본은 F0 보정 전 입력이며, 후속 사용자 보정 지시와 반환 F0-A1~A6 PASS를 거친 r003을94a6e39에서 채택했다. 두 소스를 같은 기준으로 쓴 브리핑이 잘못됐다. 이번 수정은 알고리즘/아트 출력을 바꾸지 않고 소스/PNG 지문 고정·독립 Python51장 대조 시험을 추가했다. 이전 algorithm-tester도 r003에 대해서51프레임 차이0이라고 기록했다. 상세 버전/권한 근거는 raster-reference.md. 허용차0을 유지한다.

## 검증 증거

모든 새 증거는 `output/hd2d-fx/verification/r002/` 아래다. compile.json 오류0; combat-tests.json 34/34; chain-tests.json 10/10; ground-tests.json 7/7(내부51장 전부 픽셀 차이0); raster-selftest.log 22건. doctor.json configured, editor-status.json은 이 워크트리6000.4.3f1. `play-lifetime/report.json` 4/4 PASS(23.2초), `play-lifetime.txt`에 실제25회 번개 표시 후 동일 Scope 정비에서 chainEntries=0을 기록했다. 전체 Play/EditMode를 다시 돌리지 않았으며 성능/300마리/버닝 측정은 하지 않는다.

## 범위·나머지 판정

- spec-reviewer가 반환한 보존 시안 범위 판정을 그대로 채택: Main·프리팹·데이터·아틀라스·런타임 코드 **본 게임0 / 보존 시안2**. Lookdev_HD2D는 빌드 제외 초기 시안이자 수정 금지 대상. 전체 장면0이라고 보고하지 않는다.
- 시각 PASS 유지. 회복 고리의 물 가림은 여전히 미확인 부분으로 남기며 이 수정에서 그림/배경/볼륨을 바꾸지 않았다. 새로운 시각 판독은 요청하지 않는다.
- 대상 파일: Combat/Factory/EffectSpriteCache.cs, Presenter/CombatWorldPresenter.cs, View/ProjectileView.cs, Data/CombatEffectData.cs; Tests/EditMode의 CombatEffectTests/ChainBoltRasterTests/GroundMarkRasterTests; 본 계약 문서와 reference manifest/51PNG. FrameMath/Ground/Chain 런타임 래스터 소스는48f3fc0과 동일하다.
- 새 클래스를 만들지 않았다. 기존 캐시·카드 격자 함수·테스트에 수정했다. 프리팹/장면/재질/킷 도구 변경 없음. 소스 지문은 r002/fingerprints.json. 시간·사용량은 r002/time-usage.json(호스트 토큰 미제공은 NOT_AVAILABLE).

리뷰 요청은 이전 FAIL 세 역할만, 위VC-1/SP-1/SP-2/AL-1 및 관련 AC1/AC9/AC13/X-4의 수정 확인이다. 이미 PASS한 시각 판독은 재실행하지 않는다.

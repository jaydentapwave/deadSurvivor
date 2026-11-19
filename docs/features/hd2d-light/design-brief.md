# HD-2D 빛·화면 효과 L1 구현 브리핑

입력·수용 기준: `.director/runs/20261118-hd2d-light/steps/1-Deadsurvivor.md` L0~L6, 추가 L1b·A~C, `docs/art/아트-기준서.md` r8.1 K10/K21/K24/K25/K26 및3.12. 기준 커밋12e0567. 사전 design-reviewer 없는 L1. 기존 상태/DI/세이브/렌더러 구조 유지, 기존 값·셰이더·필드 표현에 한정.

- L0: 현재 값·기준 화면·측정. 기존 B8 측정 도구에 출력 경로·단위 측정 옵션만 추가. 기존 배경 흐름에 고정 웨이브6·연속 효과 없음 → 별도 light flow. 새 helper는 `App/#if UNITY_EDITOR`의 시각 촬영 fixture이며 승패/전체 플레이 시험에 사용하지 않는다. seed8, 고정30Hz, 시작1프레임; 낮은 적 배수로6까지 정상 진행·장착,6에서 내구도 높은 두 표적 배치 후 실제 전투 피드백을 기다려 정지. 멈춘 waveFrame·게임 상태를 기록한다.
- L1: 기존 해 방향/거리/해상도 유지. 실제 메시 그림자 정책 유지. 그림자 갈색과 진함을 셰이더에서 맞춘다. 실제 길이/그림자 쌍 확인.
- L1b: 높이 켠 ground 경로의 메시 기울기 법선에만4배. 해 방향 dot을2~3단으로 나눠 약한 색 띠. 높이/메시/좌표/이동 및 D-T9 OFF 계산 보존. T2/T7/Main1-hill 캡처는 디렉터 눈가림 입력.
- L2: FieldSurface 명암 계수 .58/.80/1. SpriteLit 램프는 현재 일치, 몬스터 그림자 수신Off 유지. 진단과4배 확대.
- L3: 기존Bloom2/.1/.25/Dual/Quarter·Neutral·32bits 유지. Vignette override·색0 추가, 이중 어둡게 제거. 중앙 선명 띠 보호, 밖9탭. 최종ON G09/G11으로 판단, OFF로 대체 없음. UI/WorldIndicator 기존 후처리 밖 경로 유지.
- L4: 측정값을 벗어나면 빛/재질 색만. B8 rim/top1.200109는 재증가 안 함. 풀밭 생성기는 무늬만, 풀잎 결·팔레트·배치 마스크 유지. 넓은 반복 얼룩을 없애고 실제 화면으로 확인.
- L5: 먼지36/1.4,18초 수명,반투명. 기존 PoolManager·Effect 폴더 패턴으로 생성/반환. 물 무늬·깃발은 격자/계단 프레임, 시간·프레임 선택 계산은 EditMode로. 점광 없음.
- L6: 변경 없이 Main2 선언→수집→finish→원본gate. 전체EditMode/플레이 각1회(장면/런타임 바뀐 경우),4화면비·전투캡처 직접 열람. 런타임 변경이면 validator-client/spec-reviewer/algorithm-tester를 Claude 세션에 요청. G20·K21은 디렉터, G19는7단계.

기준과 다른 점·이미 결정된 것·범위 밖: verification.md 머리 참조. 시안/Assets/DeadSurvivor/킷gate/유료생성/성능/새UI/아트 원본 모델 수정 없음. 근거 없는 검사 추가 없음. 대체할 중복 검사 후보: 같은 단위의 전체플레이 반복(이번에 실행하지 않음).

## D-L5~D-L7 구현 범위 (attempt 3)

- D-L5: 기존 Vignette의 값만 .52/.04/rounded Off/중성 곱셈색 .425. 기존 측정기에 ON/OFF 방사32구간·모서리 최저/검정 비율 옵션을 추가. 넓은 .8/.6 방식은 도트·이동 검사를 깨뜨려 폐기했다. 최종 노출0.
- D-L6: 기존 피해 숫자 Entry에 MonsterId를 전달하고 순수 계산으로 같은 대상만 글자 높이+2px 간격/좌우±3px 쌓기. 최신 시작 위치를 공유해 이동 중에도 간격 유지. 검/도끼 body만 카메라 안쪽 .6m, root/판정/진형 유지·풀 재사용 초기화.
- D-L7: 실제 걷는 땅 기준 높이 i.href와 과장 법선의 해 dot으로 넓은4단. 장식 턱 높이를 포함하지 않는다. worn/soil 혼합만 줄이며 원본 그림/높이/이동/OFF 경로는 보존. K21 최종4장 판독은 디렉터.
- 최신 지시대로 바뀐 범위 시험과 main2만 확인. validator-client/algorithm-tester는 D-L6만, spec-reviewer는 값·요구 대조. 이전 세 리뷰 PASS는 새 범위를 대신하지 않는다. 전체 아트/T4 완료는 새 G20/K21 판독 뒤다.

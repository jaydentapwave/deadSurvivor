데드 서바이버 — Unity 마이그레이션

실행
1. Unity Hub에서 Deadsurvivor 폴더를 열기 (Unity 6000.4.3f1).
2. Assets/DeadSurvivor/Scenes/DeadSurvivor.unity 씬을 열기.
3. Play 버튼 → 캐릭터 1~4명 선택 → 스쿼드 출전.
4. Mac 실행 파일: Builds/macOS/Dead Survivor.app

조작
- WASD / 방향키: 스쿼드 이동. 전투 화면 드래그: 가상 조이스틱.
- SPACE / ESC: 일시정지. 1: 현재 웨이브를 건너뛰고 정비 화면으로.
- 아이템 드래그 또는 아이템 클릭 후 시작 칸 클릭: 배치.
- 같은 종류와 등급의 장비 위에 놓기: 합성. 대상 위치 유지, 전설은 합성 불가.
- 공용 보관함 빈 곳에 놓기: 탈착. 보관함은 아이템이 많으면 스크롤 가능.
- 확장 키트: 열린 칸과 맞닿은 잠금 칸에 사용. 1×2 키트 선택 후 R 또는 회전 버튼.
- 테스트 설정: 시간, 배속, 몬스터 수, 보상 수, 웨이브와 드랍 가중치 JSON 수정.
- 결과 화면의 테스트 기록 저장: Application.persistentDataPath/dead-survivor-playtest.json.

이식 기준
MigrationReference/prototype.html의 현재 구현을 기준으로 데이터와 규칙을 이식했습니다.
옛 DOCX와 차이가 있는 항목은 HTML을 우선했습니다:
브루노 HP 400, 한스 HP 340 / 중앙 4×4 시작·최대 6×6 / 60초부터 매 웨이브 +10초 /
무기 6종(돌·벽돌 제외), 몬스터 5종 / 무료 부활 / 등급·합성 / 팀 버프와 보호막 /
6웨이브 이후 버닝 타임, 돌진 무리와 자폭 무리, 뒤처진 적 재배치.
정비 시 남은 보관함 아이템은 다음 웨이브 시작 시 폐기됩니다.

주요 경로
- Assets/DeadSurvivor/Runtime: 게임 상태, 전투, 장비/그리드 규칙, 월드 표현, uGUI.
- Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json: 수치·아이템 모양·드랍·웨이브 원본 데이터.
- Resources/DeadSurvivor/Sprites: 추출한 투명 PNG 36개. PPU 168, 중앙 피벗.
- Resources/DeadSurvivor/UI: Noto Sans KR, SIL OFL 라이선스, TMP 폰트 에셋.
- Assets/DeadSurvivor/Editor: 초기 씬 구성과 규칙 검증 도구.
- MigrationVerification: 규칙 검사 결과와 추가 검증 자료.
- Assets/MigrationVerification: 가로·세로 화면 캡처.

구현 구조
전투 좌표는 원본 HTML의 픽셀 단위를 유지하고 렌더링 때 Unity 월드 좌표로 변환합니다.
42 원본 px = Unity 1 unit. HTML Y축은 Unity 렌더링 시 반전합니다.
캐릭터/적/발사체 PNG는 SpriteRenderer, 화면은 uGUI와 TextMeshPro로 표시합니다.
UI는 DSUI가 실행 시 생성합니다. 캐릭터 등 시각 객체는 재사용 풀로 관리합니다.
정적 콘텐츠는 JSON, 실행 상태는 별도 C# 객체로 분리했습니다.
기본 패키지: URP, Input System, uGUI(TMP 포함), Test Framework.
Unity CLI 연결: com.unity.pipeline 0.9.1-exp.1.

시각적 차이와 검증 범위
캐릭터·몬스터·발사체는 추출 PNG를 사용합니다. 글꼴, UI의 모서리/여백 및 아이템 기호 표현은 Unity에 맞춰 재구성했습니다.
기존 원형 폭발·공격 예고는 Unity 도형으로 재현했습니다. 화면 비네트와 버닝 타임의 가장자리 붉은 예고도 Unity에서 재현했습니다.
가로 전용: 기본 1600×900, 모바일 좌/우 가로 회전 허용 및 세로 회전 금지. 정비 인벤토리 4개 가로 배치.
Unity Device Simulator를 90도 가로로 설정했으며, Device.Screen 기반 안전 영역을 적용했습니다.
가로 시뮬레이터에서 드래그·장착과 조이스틱 입력 검증을 통과했습니다. 실제 기기의 터치/성능은 별도 검증이 필요합니다.
규칙 테스트의 10웨이브 완주 검사는 낮은 적 수·피해 설정을 사용하며, 기본 난이도의 사람이 직접 플레이한 완주 결과가 아닙니다.
원본에도 없는 사운드, 계정, 상점, 광고/IAP, 메타 성장, 저장 후 이어하기는 추가하지 않았습니다.

조이스틱 아트 업데이트
- UI/JoystickBase.png, UI/JoystickKnob.png: 내장 이미지 생성 도구로 제작한 투명 PNG.
- 전투 화면을 누르면 해당 위치에 조이스틱 표시. 링 140, 손잡이 60 UI 단위.
- 기존 이동 감도 유지, 표시 이미지는 터치 이벤트를 가로채지 않음.
- 이미지 연결·알파 채널·드래그·해제 검사 및 컴파일/콘솔 오류 없음 확인.

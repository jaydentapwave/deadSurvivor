# FX 실행 기록

## F0 그림 관문
- 기준 d3b01b1 / 그림 커밋 2c0267e. 사용자 메시지로 반환된 Claude 시각 판독자(validator-client 시각 역할)의 판정:
VERDICT: PASS
- F0-A1~F0-A6 PASS, 눈가림 첫째 해석 7/7 일치. 깊이/Bloom/게임 가독성/fps 체감은 미확인. APNG 첫 장 한계 때문에 decoded 줄 판독.
- 후속은 게임 캡처에서 회복 대비·번개 끝·폭발/자폭 구별·돌진 바닥 깊이 확인. 300마리 검사는 범위 밖이라 실행하지 않음.
- 컴파일 첫 시도 FAIL: SpriteAtlasAsset.GetPackables API 없음. Remove 후 Add로 재실행 가능한 연결로 고침. 워크플로우 후보: 기존 HudUiImport의 V2 API만 사용, 최종에 반영 여부 기록.

## F0 완료
- 94a6e39. 컴파일 0, CombatEffectTests 30/30, ArtImportSettingsTests 5/5, 그림 68/68 PASS.
- V2 atlas는 SpriteAtlasAsset.Load가 원본을 읽는 API다. AssetDatabase.LoadAssetAtPath로 읽으면 null. Remove/Add는 같은 폴더를 중복 없이 연결한다.
- 테스트 직전 도메인 reload로 EDITOR_NOT_RUNNING 1회. 실제 ready 조회 후 port 7800 + 프로젝트 경로를 함께 지정해 정상 실행.
- 자동 생성 meta/preset 공백을 git diff --check가 잡음. 손 편집 금지 규칙에 따라 Unity 출력은 보존, 직접 작성 코드/문서에 diff --check 적용.

## F1 완료
- HDR GPU 팔레트 표 21색 PASS. ffffff=(2.4,2.4,2.4), fff9ed=(2.4,2.2734,2.0325), 나머지는 몸 세기1. 문턱·허용오차 낮추지 않음.
- 첫 카메라 검사 0.955에서 두 번 실패하여 직접 DrawRenderer, 이어 DrawMesh HDR target 방식으로 변경. 픽셀 덤프가 오류색(1,0,1)임을 확인. ShaderUtil.GetShaderMessages는 비었지만 console get에 Metal의 LinearToSRGB 미선언 오류가 있었다. Color.hlsl include 누락 수정 후 PASS. 원인은 게임 셰이더 결함이며 카메라/후처리 가설은 폐기.
- 놓친 것: C# recompile 0을 셰이더 컴파일 성공으로 혼동했다. 워크플로우 후보: 첫 GPU 실패 때 실제 RGBA와 console cursor 오류를 먼저 수집(최종에 반영 예정). 검사 자체를 완화하지 않았음.

## F2 완료
- 프레임 계산 12/12, CombatEffectTests 30/30 PASS. 실제 웨이브1 첫 장검 표시 순서 1,2,3,4,5(빠짐0), F2-play-start.json/F2-play-frames.json. captureDeltaTime 원복 후 Play 종료.
- console의 마지막 오류는 UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase에서 난 IndexOutOfRangeException. 게임 코드 호출 스택이 아닌 에디터 검색 인덱스 오류로 별도 기록. 전체 콘솔0이라고 주장하지 않음.
- 첫 인스턴스 id별 프레임만 기록하여 동시 발동 섞임과 무한 진단 목록 증가를 막음. 그림 키 진입점은 기존 이름/기대식 유지.

## F3 완료
- 0024a72, CombatEffectTests 30/30 PASS(8방향 그림·반전·Z회전0·풀 초기화·그림자 y배율2).

## F4 완료
- 41cd92f, GroundMarkRasterTests 6/6 / MonsterViewStateTests 13/13 / 컴파일0.
- 기준 Python PNG heal_3·blast_1·pulse_3·boom_2·dash_1 픽셀 차이 모두0. 캐시의 정수 외접 픽셀 반지름만 같고 원래 반지름이 다른 경우 잘못 재사용하지 않도록 원래 반지름을 보조 키로 추가. 32방향 구간은 유지.
- 기존 테스트의 물리 원 크기 대신 화면 공간 래스터의 픽셀 동등성과 local scale(1,2,1), Y회전0을 확인. 데이터 반지름 변경 없음.

## F5 완료
- 기준 PNG chain_bolt_1~3 픽셀 차이0, 6방향×3장 양 끝점 정확히 보존(0도트). ChainBoltRasterTests 9/9 / CombatEffectTests 30/30(연쇄 순서 유지) / 컴파일0.
- 오래된 port 7800 핀으로 요청이 두 번 실행 전 거절됨. 후보 목록에서 같은 프로젝트/PID 57018의 실제 port 7801을 확인. 허용된 editor_ready 복구 명령 1회 성공(재시작 없음), 이후 project path+PID로 지정. 워크플로우 후보: 포트를 세션 내 불변으로 간주하지 않고 live 후보의 PID+프로젝트 확인.

## F6 완료
- EnemyShot 관련 검사 4/4, 컴파일0. 실제 데이터 shotSprite를 탄 생성 시 함께 넘겨 색 원 대신 spore 프레임 3장/8fps 반복. 바닥 점·세운 높이 .5m·그림자 분리.
- F6 전용 흐름 FX6-SPAWN 1/1 PASS, 21.3초. wave4 실제 포자 탄·바닥 점 그림자를 1920×1080 캡처에서 직접 봄. 전체 판정 경로 미실행.
- Play 시작 실패1회는 UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase의 검색 인덱스 예외. 이 에디터의 CreateDefaultIndexAndImport API로 기본 인덱스를 다시 만든 뒤 같은 흐름 PASS. Git 변경 파일 추가0, 킷/게임 규칙/검사 문턱 변경0.
- CLI recompile 401 1회는 도메인 reload 중 인증 정보 변화. 같은 프로젝트/PID 확인 후 재실행 컴파일0. 테스트는 실제 새 프레임 뷰를 대상으로 4건 실행됨을 확인.

## F7 명령·화면 관문 완료 / 독립 마감 리뷰 대기
- 전체 판정 경로 1회: 8/8 PASS, 142.8초 (`F7-playable-full/report.json`). 성능 표본 수집 없음.
- 전체 EditMode 1회: 547건 중 545 PASS·2 FAIL. 두 실패는 아틀라스가 옛 효과10장/발 피벗을 기대한 계약이었다. 확정 F0 그림68장/중앙 피벗·옛 참조 제외를 검사하도록 SpriteAtlasServiceTests를 갱신하고 그 클래스만 5/5 PASS. 전체 재실행 없음.
- 연속 캡처: 15종×21장=315장. 프레임1→N 순서, Unity frame 연속+1, 게임 시간1/60s, 원본 알파0/255, 흰 심 HDR 대상6종만 glowCore>0 모두 PASS. 일반적 게임 캡처의 불투명 배경 알파가 아니라 실제 Sprite 텍스처·렌더러 알파와 재질 세기를 기록한 값이다.
- 최초 arrow/pulse 표본은 나무/물에 가려 시각 확인 FAIL. 두 종만 원점의 땅에서 다시 캡처, 5/5 전용 흐름 PASS(그 중 마지막은 battle). 원본 실패 표본은 occluded-attempt-1/에 보관. 배경/물 높이/장면은 안 바꿈.
- battle 첫 CLI polling은 동시 표시 90초 타임아웃 FAIL. 매 프레임 observer로 바꾸고 파티 원점에서 다시 진행한 뒤 wave3의 베기·별·고리·투사체·숫자 동시 표시를 TimeScale0에서 1920×1080으로 캡처. battle-fx.png를 직접 봄. 전체 판정 경로/전체 EditMode 반복 안 함.
- 직접 본 것: 최종 compact-01~15·all-01~15, arrow/pulse/dash 원본 000.png, battle-fx.png. 회복 고리는 발 아래에서 커져 끊기고, 파동은 겹 고리가 퍼져 호로 갈라지며, 베기/별은 세운 인물 옆에서 짧게 생겼다 조각으로 끝남. 각 키별 한 줄은 f7-review-brief.md에.
- 기존 art-insert 흐름 50개(효과 기대식 포함) 내용 불변. 새 FXSEQ 18개만 추가. 대소문자 없는 macOS 경로 tools/의 Git 원본은 Tools/다.
- 구 효과 PNG10개 보존. 실제 본 게임 Main·프리팹·아틀라스·데이터·런타임 코드 참조0. 별도 구 시안 Lookdev_HD2D.unity의 참조2는 남아 있다: 사전조사:282에서 본 게임이 아닌 1단 시안/빌드 제외로 명시했고 시안 보존 지시에 따라 손대지 않음. 전체 Assets/Game 장면까지 무조건 합치면2임을 숨기지 않는다.
- Play 뒤 DamageNumber SDF.asset의 자동 변경1줄은 세션 시작 시 깨끗했던 원본으로 복구. Main/Volume/Field/몸 그림/글꼴 변경을 커밋하지 않음.
- F7 리뷰 validator-client/spec-reviewer/algorithm-tester 및 Claude 게임 눈가림 판독은 NOT_RUN(실행 세션 금지에 따라 다음 Claude 세션으로 요청).
- 낭비·놓침 후보 반영: ShaderUtil의 빈 목록보다 actual pixel/console cursor를 먼저 확인, stable PID+project로 에디터 지정, 첫 capture부터 지면 가림·단일 표본 실제 가시성 확인, 짧은 동시 상태는 polling 대신 프레임 observer. 이번 실행의 절차/도구로 반영했고 킷 규칙 원본은 고치지 않음. 전역 워크플로우 반영 후보는 최종 핸드오프에 넘긴다. 전체 반복 검사는 추가하지 않음.

- 이행 메뉴01~05 재실행 첫 확인은159개 중 atlas1개 순서 변경으로 FAIL. Remove/Add가 swap-last 재정렬을 일으켰다. 이미 연결된 폴더를 GetPackables로 확인하여 유지하도록 바꾼 뒤159/159 SHA 불변 PASS. 최종 SpriteAtlasServiceTests만5/5 재확인, 컴파일0. 전체 Play/EditMode는 반복하지 않음.

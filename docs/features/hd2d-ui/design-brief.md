# HD-2D UI 다시 꾸미기 — 설계 브리핑 (U단계)

- **상태: 초안(2026-11-18, Claude 하위 에이전트, 디렉터 지시).** 코드는 읽기만 했다. 에디터·`Assets/`·git은 건드리지 않았다(4단계 Codex가 에디터를 쓰는 중).
- 사용자 원문(2026-11-18): "ui도 해당 3d 래퍼런스랑 동일한 디자인이면 좋을것 같아", "온전히 완성된 결과물을 내가 받아서 보고싶어 … 각 단계뼐로 계속해서 검증 및 테스트를 진행해줘야할 것 같아, 다만 지난번처럼 한개 수정했는데 전체를 검증하는 실수는 없었으면해".
- 규모: **L1**(`.ai/rules/work-levels.md`). 화면 모양·입력 영역·표시 형식이 바뀌고 View 몇 개의 표시 코드가 바뀐다. DI 구조·상태 소유권·세이브·렌더 파이프라인은 바뀌지 않는다 → design-reviewer 없음, 마감 리뷰 묶음 한 번.
- 기준 코드: 작업 트리(HEAD `da0820f` 뒤 4단계 W3 진행 중, `Main.unity` 미커밋 변경 있음). 줄 번호는 이 시점 기준이며 4단계 커밋 뒤 다시 확인한다.
- 원본(값은 여기서 복사하지 않고 가리킨다):
  - 아트 기준서 r6.1 `docs/art/아트-기준서.md` **3.13**(A 결정 줄 K16, B 색·크기 토큰, C 화면별, D 에셋 U1~U15, E 고해상 틀, F 모바일, G 다르게 가는 것), 3.3 범위 대조표 UI 행.
  - UI 관찰 `docs/art/reference/hd2d-ui-octopath-20261118/UI-관찰.md`(OU-S001~S005, OT-S005·S008·S009).
  - 만든 그림 `output/assets/hd2d-ui/` — `sprites/` 42장, `slices.json`(9-slice 경계), `notes.md`(기준서와 다른 점 7개), `preview*.png`, `make_ui.py`(재현).
  - 4단계 설계 `docs/features/hd2d-screen/design-brief.md` 3.4(화면별 n·안전 영역), **3.10**(UI 캔버스 경계), 5절 W3·W6·W7.

## 0. 확정된 결정 (입력)

| # | 결정 | 출처 |
|---|---|---|
| UE1 | UI 형태 언어 = 옥토패스 결(K16 r6): 반투명 짙은 판 + 2px 청회색 테 + 대각 꺾쇠, 마름모, 가는 산세리프, 강조만 세리프, 평평한 막대, 선택 = 파란 빛 | 기준서 3.13 A |
| UE2 | HUD·로비·정비·결과·조이스틱은 **Screen Space Overlay 캔버스 그대로**. 후처리를 받지 않는다. 대원·몬스터 HP 막대와 피해 숫자는 3D 월드 세운 판 | 4단계 설계 3.10 |
| UE3 | 틀 그림만 고해상(1기준px = 3텍셀, Bilinear). 배치 단위 640×360 기준px, `CanvasScaler` 높이 맞춤은 그대로 | 기준서 3.13 E, `slices.json` `_about` |
| UE4 | 본문 글꼴 U14 = Noto Sans KR Regular/Light. 강조 글꼴 U15(Gowun Batang + Cormorant Garamond Italic)는 **미설치·사용자 확인 필요** → 이번에는 Noto Sans KR로 두고 바꿔 끼울 자리 하나만 만든다 | 기준서 3.13 머리, 디렉터 지시 |
| UE5 | 성능은 재지 않는다(7단계 한 번) | 완성 실행 계획 3절, 사용자 11-18 |
| UE6 | 확인은 단위마다 바뀐 범위만. 화면 캡처는 바뀐 화면 1920×1080 한 장씩, 4화면비는 마감에 한 번 | 디렉터 지시(실행 계획 3절 "UI 한 화면 = 4화면비"보다 새 지시) |
| UE7 | 가죽 그림(`Art/AIGenerated/UI/`)은 지우지 않는다. 새 폴더에 넣고 바꿔 끼운다 | 기준서 3.13 D 끝 |

## 1. 목표와 완료 조건

| 항목 | 내용 |
|---|---|
| 쓸 수 있게 될 것 | 로비·전투 HUD·조이스틱·정비(진형·가방·보관함·정보)·결과 화면이 U1~U13 그림과 Noto Sans KR Regular/Light로 옥토패스 결이 된다. 누름 영역 44기준px 이상, 글자 9 이상, 판 바탕·띠는 안전 영역 밖 화면 끝까지, 도트 초상·아이콘은 화면 정수 배율 |
| 완료 조건 | 컴파일 0. 단위별 `-testFilter` 통과. 화면마다 1920×1080 캡처를 직접 열어 3.13 C표 행과 대조. 마감: 4화면비(1920×1080, 2532×1170, 2520×1080, 2048×1536) + 노치 안전 영역 캡처 1벌에서 잘림·겹침 0, EditMode 전체 1회, 판정 경로 전체(G1~G8) 1회, 장면의 가죽 UI 그림 참조 0, 마감 리뷰 묶음 |
| 제외 | 강조 글꼴 받기·넣기(U15, 사용자 확인 뒤), 피해 숫자 세리프 모양(U15에 기댐), 성능, 튜토리얼(코드 없음 — `docs/features/tutorial/`에 기획안 docx만), 일시정지 팝업(지금 게임에 없음 — 버튼 글자만 "계속"으로 바뀜) |

## 2. 지금 UI 목록과 바꿀 그림 (요소별)

- 찾은 방법: `Assets/Game/01.Scene/Main.unity`를 YAML로 읽어 `Canvas` 아래 나무를 뽑음(스크립트 GUID → 클래스 이름), View 코드 18개, `MainSceneBuilder.cs`, `UiArtSceneUtils.cs`를 읽음. UI는 **모두 `Main.unity` 장면 안**에 있다(UI 프리팹 없음). 글자는 전부 uGUI `Text`(TMP 아님, 글꼴 `Font/NotoSansKR.ttf`).
- 지금 그림 연결: 모든 `Image`에 `UI_ArtSurface`(메시 효과, 텍셀 1 = 캔버스 1, 가죽 땀 그리기)가 붙어 있고(장면에 394개), 그림 키는 `UiArtSceneUtils.GetKey`(이름으로 panel/button/bag_cell/hp_bar_*/cooldown_*/joystick_*)가 정한다. 조이스틱·HP 채움은 `SpriteAtlasService.Get/GetUi`가 Characters 아틀라스(Point)에서 런타임에 읽는다.
- "종류": 그림 = 바꿀 Image가 있는 요소 / 글자 = 글꼴·색만 바뀜 / 월드 = 4단계 세운 판(UE2).
- 그림 이름은 `output/assets/hd2d-ui/sprites/<이름>.png`. 상태 여러 장은 `{a,b}`로 쓰지 않고 모두 풀어 썼다(9절 검사가 센다).

### 2.1 로비 (`LobbyView`, `UI_LobbyView`·`UI_CharacterCardView`)

| id | 요소(장면 경로) | 종류 | 지금 | 바꿀 그림 | 코드/장면 |
|---|---|---|---|---|---|
| E01 | `LobbyView/Title` "원정대" | 글자 | Bold 16 흰색 | 제목 13~16 Regular #ececec + 밑줄 `U5_divider_line`(Image.color #707070) | 장면 |
| E02 | `Cards/Card1~4` 바탕 | 그림 | 가죽 panel, `PanelColor` | `U1_panel_menu`(Sliced, ppu 배수 3) | 장면 |
| E03 | `Card*/Color`(초상 없을 때 색 칸) | 그림 | 가죽 button에 색 | `U8_portrait_L_mask`에 캐릭터 색 곱하기 + 틀 `U8_portrait_L_normal` | 장면 |
| E04 | `Card*/Portrait/Window/Image` 초상 | 그림 | 사각 창 `RectMask2D` 36×36 | 틀 `U8_portrait_L_normal` → 마스크 `U8_portrait_L_mask`(uGUI `Mask`, 그림 안 보임) → 얼굴. 얼굴 34텍셀, 화면 정수 배율(1080에서 4px/텍셀) | **코드** `UI_CharacterCardView.UpdatePortrait`(영역·배율), 장면(RectMask2D → Mask) |
| E05 | `Card*/Name·Role` | 글자 | Bold 13 / 10 | 이름 Regular 12 #ececec, 직업 10 #bebbba. 아래 구분선 `U5_divider_line` + 가운데 `U5_divider_knot` | 장면 |
| E06 | `Card*/Hp·Atk·Def·Item` | 글자 | "HP 120" 한 줄 글 | "항목 : 값" 두 칸 — 왼쪽 고정 꼬리표 Text 새로(장면), 오른쪽 값만 코드가 씀 | **코드** `UI_CharacterCardView.UpdateUI` 형식, 장면(꼬리표 4개) |
| E07 | `DepartButton` "출발" | 그림 | 가죽 140×30 | 주 버튼: 보통 `U3_button_highlight`, 누름 `U3_button_pressed`, 꺼짐 `U3_button_disabled`(Sprite Swap). 누름 영역 44기준px | 장면 |

### 2.2 전투 HUD (`BattleHudView`, `UI_BattleHudView`·`UI_MemberStatusView`·`UI_BattleItemView`·`UI_HpLossView`·`UI_JoystickView`)

| id | 요소 | 종류 | 지금 | 바꿀 그림 | 코드/장면 |
|---|---|---|---|---|---|
| E10 | `TopBar` 바탕(높이 22) | 그림 | 가죽 panel | `U6_band_top`(위 0.75 → 아래 0), 안전 영역 밖 위 끝까지 | 장면 + `UI_SafeAreaBleed`(4절) |
| E11 | `TopBar/Wave` "웨이브 1/10" | 그림 | 글자 | 진행 줄 `U10_wave_line` + 마디 10개 `U10_wave_node_passed`·`U10_wave_node_current`·`U10_wave_node_remaining`. 작은 글자 "1/10"은 남김(제안) | **코드** `UI_BattleHudView.UpdateUI`(마디 상태), 새 순수 함수 `WaveProgressMath`(5절) |
| E12 | `TopBar/Kills·Threat` | 글자 | 10 흰색 | 10 #bebbba, 장면 위 글자 얇은 그림자(`Shadow`) | 장면 |
| E13 | `TopBar/Remaining` 남은 시간 | 글자 | Bold 13 | 13~14 Regular #ececec. 강조 글꼴 자리(숫자) | 장면(글꼴 역할 = 강조) |
| E14 | `TopBar/Burning` "버닝 타임!" | 글자 | Bold 주황 | 10 Regular #f7ea81(제안: 강조 금색) | 장면 |
| E15 | `TopBar/SpeedButton` ×1 | 그림 | 가죽 36×18 | 기호 `U12_icon_speed` + 글자 "×1", 투명 누름 영역 44×44 | 장면 |
| E16 | `TopBar/PauseButton` 일시정지/계속 | 그림 | 가죽 52×18, 글자 | 기호 `U12_icon_pause`. **멈춘 상태("계속") 기호 없음 → 7절 빈칸 G2** | 장면(+ G2 결정에 따라 코드) |
| E17 | `BottomPanel` 바탕(높이 52) | 그림 | 반투명 검정 + 가죽 | `U6_band_top`을 세로로 뒤집어(scale.y = −1) 아래에서 위로 사라짐, 화면 아래 끝까지 | 장면 + `UI_SafeAreaBleed` |
| E18 | `MemberStatus1~4` 칸 바탕 | 그림(없앰) | 가죽 panel | 틀 없음 — Image 컴포넌트를 없앤다(입력 안 받음) | 장면 |
| E19 | `MemberStatus*/PortraitWindow/Image` | 그림 | 사각 36×36, 1배 | 틀 `U8_portrait_S_normal`(살아 있음)·`U8_portrait_S_dead`(쓰러짐) → 마스크 `U8_portrait_S_mask` → 얼굴 22텍셀, 화면 정수 배율(1080에서 3px) | **코드** `UI_MemberStatusView.UpdateUI`(틀 바꾸기·영역), 장면 |
| E20 | `MemberStatus*/Name·HpText` | 글자 | 9 Bold / 8 | 이름 10 #ececec(쓰러짐 #a8acaf), HP 숫자 10 #ececec(8 → 9 이상). 넓은 자간은 uGUI `Text`에 없어 뺀다(추정 영향 작음) | 장면, 색은 기존 직렬화 칸 `aliveNameColor`·`downedNameColor` |
| E21 | `MemberStatus*/HpBar`·`Fill`·`HpLoss` | 그림 | 가죽 hp_bar_bg + 밝은 초록 | 트랙 `U7_bar_track`, 채움 `U7_bar_fill` #568757, 빨간 구간 `U7_bar_fill`(규칙 색 그대로) | 장면 |
| E22 | `Items/Item1~16` 칸 바탕 | 그림 | 가죽 button, 양피지색 | 준비·쿨·발동·지속 `U9_cell_filled`, 비활성 `U9_cell_empty` | **코드** `UI_BattleItemView`(색 → 그림 두 칸) |
| E23 | `Item*/CooldownBar`·`Fill` | 그림 | 가죽 cooldown_bg + 초록/주황 | 트랙 `U7_bar_track`, 채움 `U7_bar_fill` 쿨 #5b7984 / 준비 #f7ea81(제안) | 장면(색 직렬화 칸) |
| E24 | `Item*/Label·State` | 글자 | 6, 칸 16×8 | 글자 최소 9를 칸(16×8)에 넣을 수 없음 → **7절 결정 D3** | 결정에 따라 |
| E25 | `Item*/Icon` | 그림(도트) | 아이템 아틀라스 그림 | 그대로(Point). 화면 정수 배율만 | 코드 `UI_ItemIconView`(정수 배율, 4절) |
| E26 | `JoystickArea`(투명 입력 판) | 그림(투명) | 가죽 button α0 | `U7_bar_track`, Image.color α0(그림 없는 Image 금지 시험 유지) | 장면 |
| E27 | `JoystickArea/Ring`·`Handle` | 그림 | 가죽 joystick 56×56 / 24×24 | `U13_joystick_base`·`U13_joystick_knob`. **크기 어긋남 → 7절 G4** | **코드** `MoveInputPresenter.cs:33` 키·아틀라스 |

### 2.3 정비 (`PrepView`, `UI_PrepView`·`UI_ReviveView`·`UI_FormationView`·`UI_BagView`·`UI_BagCellView`·`UI_StashView`·`UI_StashItemView`·`UI_ItemInfoView`)

| id | 요소 | 종류 | 지금 | 바꿀 그림 | 코드/장면 |
|---|---|---|---|---|---|
| E30 | `PrepView/Dim`(화면 덮개) | 그림 | 가죽 panel α | `U7_bar_track`(검정), 지금 α 유지 | 장면 |
| E31 | `PrepView/Box`(위 머리 띠, 높이 64) | 그림 | 가죽 panel | `U6_band_bottom` 세로 뒤집기(선이 아래 = 가는 밑줄), 위 끝까지 | 장면 + `UI_SafeAreaBleed` |
| E32 | `Box/Title` "웨이브 n 완료" | 글자 | Bold 16 | 13~14, 글꼴 역할 = 강조(지금 Noto Regular) | 장면 |
| E33 | `Box/Member1~4` 대원 줄 | 그림 | 글자 "이름  HP 80/120" | 이름 + 숫자 + 평평한 막대(`U7_bar_track`·`U7_bar_fill` #568757) | **코드** `PrepPresenter.cs:47-55`·`UI_PrepView.UpdateUI`(HP 비율 목록 추가), 장면(막대 4개) |
| E34 | `Box/NextWaveButton` | 그림 | 가죽 110×22 | 주 버튼 `U3_button_highlight`·`U3_button_pressed`·`U3_button_disabled`, 누름 44 | 장면 |
| E35 | `Box/Revive1~4` "부활" 46×16 | 그림 | 가죽 button | 작은 줄 버튼 `U4_row_normal`, 누름·강조 `U4_row_selected`, 꺼짐 `U4_row_disabled`, 누름 영역 44 | 장면 |
| E36 | `FormationPanel` 바탕 | 그림 | 가죽 panel | `U1_panel_menu` | 장면 |
| E37 | `FormationButton1~3` | 그림 | 가죽 + 노랑/회색 색 곱하기 | 명령 줄: 대기 진형 `U4_row_selected` + 왼쪽 `U11_select_marker`, 나머지 `U4_row_normal`, 글자 흰색/#bebbba | **코드** `UI_FormationView.UpdateUI`(색 → 그림·표식) |
| E38 | `Preview/Member1~4` 대원 표식 30×13 + 이름 7 | 그림 | 가죽 칩 + 회색 곱하기 | 작은 마름모 `U8_portrait_S_normal`·`U8_portrait_S_dead`(+ 얼굴 `U8_portrait_S_mask`). 이름 글자는 빼거나 9로(제안: 뺌, 얼굴로 구별) | **코드** `UI_FormationView.UpdateUI`(초상 목록 받기), Presenter 한 줄 |
| E39 | `BagView0~3/Cell**/Background` 64칸 | 그림 | 가죽 bag_cell + 양피지 색 | 빈 `U9_cell_empty`, 채움 `U9_cell_filled`, 집음 `U9_cell_selected`, 놓을 수 있음 `U9_cell_droppable`, 놓을 수 없음 `U9_cell_blocked`. 쓰러진 대원은 지금처럼 `downTint` 곱하기 | **코드** `UI_BagCellView.UpdateUI`(색 → 그림), 새 순수 함수 `BagCellArt.Resolve`(5절) |
| E40 | `Cell**/Border`(등급 색 테) | 그림 | 가죽 button에 등급 색 | **등급 테 그림 없음 → 7절 G1** | 결정에 따라 |
| E41 | `Cell**/Label`(그림 없는 아이템 이름) | 글자 | 양피지 위 짙은 갈색 | #ececec | 장면 |
| E42 | `StashView` 판 + `Title` | 그림 | 가죽 panel | `U1_panel_menu`, 제목 10 #ececec + 밑줄 `U5_divider_line` | 장면 |
| E43 | `StashItem/Background` | 그림 | 가죽 + 양피지 색 | 보통 `U9_cell_filled`, 집음 `U9_cell_selected` | **코드** `UI_StashItemView.UpdateUI` |
| E44 | `StashItem/Border`(등급) | 그림 | 가죽 + 등급 색 | **G1과 같음** | 결정에 따라 |
| E45 | `StashItem/New` "새" | 그림 | 가죽 + 빨강 | `U12_icon_new`, Image.color #f7ea81 | 장면 |
| E46 | `StashItem/Shape` 4×4 작은 칸(그림 없을 때) | 그림 | 흰 칸 색 곱하기 | `U7_bar_fill`, 켬 #ececec / 끔 α0.15(제안) | 장면(색 직렬화 칸) |
| E47 | `ItemInfoView` 판 | 그림 | 가죽 panel | `U1_panel_menu` | 장면 |
| E48 | `ItemInfoView/PreviewScroll` 안쪽 칸 | 그림 | 가죽 panel | `U9_cell_empty`(9-slice 안쪽 상자) | 장면 |
| E49 | `ItemInfoView/DetailButton` 70×18 | 그림 | 가죽 button | `U4_row_normal`·`U4_row_selected`·`U4_row_disabled` | 장면 |
| E50 | `ItemInfoView/Description·Preview` | 글자 | 흰색 | 본문 10 #ececec. 3.13 C의 "머리줄 이름 + 밑줄 + 개선 줄 금색"은 Presenter 글 형식이 바뀌어 **후속 제안**(7절 D5) | 장면 |
| E51 | `RotateButton` "회전" 100×24 | 그림 | 가죽 | 보조 버튼 `U3_button_normal`·`U3_button_highlight`·`U3_button_pressed`·`U3_button_disabled` + 기호 `U12_icon_rotate` | 장면 |
| E52 | `CancelButton` "선택 취소" 100×24 | 그림 | 가죽 | 보조 버튼 `U3_button_normal`·`U3_button_highlight`·`U3_button_pressed`·`U3_button_disabled` + 기호 `U12_icon_close` | 장면 |

### 2.4 결과 (`ResultView`, `UI_ResultView`)

| id | 요소 | 종류 | 지금 | 바꿀 그림 | 코드/장면 |
|---|---|---|---|---|---|
| E60 | `ResultView/Box` 260×210 | 그림 | 가죽 panel | 가운데 세로 띠 판 `U1_panel_menu`, 폭 **300기준px 고정**, 위아래 화면 끝까지 | 장면 + `UI_SafeAreaBleed`(위·아래) |
| E61 | `Box/Outcome` 승리/패배 | 글자 | Bold 24, 금/빨강 | 강조 역할 16, 승리 #f7ea81 / 패배 #a8acaf(기준서 C "패배 회색") | 장면(직렬화 칸 `winColor`·`loseColor`) |
| E62 | 승패 아래 구분선 | 그림(새) | 없음 | `U5_divider_line` + `U5_divider_knot` | 장면 |
| E63 | `Box/Waves·Kills·Time` | 글자 | "버틴 웨이브 7/10" 한 줄 | "항목 : 값" 두 칸(꼬리표 Text 새로 3개) | **코드** `UI_ResultView.UpdateUI` 형식, 장면 |
| E64 | `Box/ToLobbyButton` | 그림 | 가죽 140×30 | 주 버튼 `U3_button_highlight`·`U3_button_pressed`·`U3_button_disabled` | 장면 |
| E65 | 아래 도움말 띠(새) | 그림(새) | 없음 | `U6_band_bottom`, 화면 아래 끝까지. 글자는 두지 않음(제안) | 장면 + `UI_SafeAreaBleed` |

### 2.5 월드 표시 (4단계 W3 뒤, UE2)

| id | 요소 | 종류 | 지금 | 바꿀 그림 | 코드/장면 |
|---|---|---|---|---|---|
| E70 | 대원 머리 위 HP 막대(`MemberView` HpBack·HpFill) | 월드 | `GetUi("hp_bar_bg")` 틀 + 채움 | `U7_bar_track`·`U7_bar_fill` #568757, 틀 없음 | **코드** `WorldPresenter.cs:228-229`, `UiArtLayout.Border`, 4단계 레이어·위치는 그대로 |
| E71 | 대원 월드 빨간 구간(`HpLossBarView`) | 월드 | `GetUi("hp_bar_fill")` | `U7_bar_fill`(규칙 색) | **코드** `HpLossPresenter.cs:43` |
| E72 | 몬스터 머리 위 HP 막대(`MonsterHpBarView` 프리팹) | 월드 | `GetUi("hp_bar_bg/fill")` | `U7_bar_track`·`U7_bar_fill` #a4534a | **코드** `MonsterHpBarViewFactory.cs:38` |
| E73 | 피해 숫자(`DamageNumberView`, 도트 숫자 그림 `digit_0~9`) | 월드 | 도트 숫자 | 세리프 기울임 + 세로 그라데이션 + 외곽은 **U15 강조 글꼴에 기댐 → 7절 G3**. 이번에는 그대로 둔다 | 없음 |

## 3. 임포트·아틀라스

- **넣을 곳**: `Assets/Game/03.Resources/Art/HD2D/UI/`(42장 + `slices.json` 사본 + `source.txt`: 원본 `output/assets/hd2d-ui/make_ui.py`, 해시, 날짜). `AIGenerated/` 밑에 두지 않는다 — 그 폴더의 프리셋(`AIGeneratedSprite.preset`: PPU 48, Point)이 붙는다(`Art/CLAUDE.md` 프로젝트 정책).
- **임포트 값**(예외): Sprite Single, **Pixels Per Unit 100**(= 캔버스 `referencePixelsPerUnit` 100, `Main.unity:57871`), **Filter Bilinear**, 밉맵 끔, 압축 없음(Android·iOS RGBA32 덮어쓰기), Mesh Type Full Rect, wrap Clamp, sRGB 켬, 알파 투명도 켬. 그러면 `Image.pixelsPerUnitMultiplier = 3`에서 3텍셀 = 1기준px, `SetNativeSize`도 기준px 크기가 된다(240×86 → 80×28.67).
- **방법**: 새 프리셋 `Art/Presets/HudUiSprite.preset` + Preset Manager 필터 `glob:"Assets/Game/03.Resources/Art/HD2D/UI/**"`(AIGenerated 항목 뒤). 그림마다 다른 `spriteBorder`는 프리셋에 담을 수 없으므로 편집기 메뉴 `DeadSurvivor/Art/Import HD2D UI`가 `slices.json`을 읽어 넣고 다시 임포트한다(두 번 돌려도 같은 결과). `.meta`를 손으로 고치지 않는다.
- `Art/CLAUDE.md` "임포트 예외" 표에 한 줄: `Art/HD2D/UI/** | 필터·PPU·border | Bilinear, PPU 100, border = slices.json | 틀 그림만 고해상(기준서 3.13 E), 도트가 아님`.
- **spriteBorder**(x=왼, y=아래, z=오른, w=위, 텍셀) — `slices.json`에서 그대로:

| 그림 | 크기(텍셀) | border | 쓰는 형 |
|---|---|---|---|
| `U1_panel_menu`, `U1_panel_battle` | 64×64 | 16,16,16,16 | Sliced, 가운데 채움 |
| `U2_glow_band` | 192×48 | 64,0,64,0 | Sliced(가로) |
| `U3_button_normal`·`U3_button_highlight`·`U3_button_pressed`·`U3_button_disabled` | 240×86 | 60,0,60,0 | Sliced(가로), **높이 86텍셀 = 28.67기준px 고정**(늘리지 않음) |
| `U4_row_normal`·`U4_row_selected`·`U4_row_disabled` | 64×40 | 12,12,12,12 | Sliced, 보이는 줄 14~16기준px |
| `U5_divider_line` | 96×12 | 24,0,24,0 | Sliced(가로) |
| `U6_band_bottom` | 8×96 | 0,0,0,3 | Sliced(세로 위만) |
| `U9_cell_empty`·`U9_cell_filled`·`U9_cell_selected`·`U9_cell_droppable`·`U9_cell_blocked` | 48×48 | 6,6,6,6 | Sliced |
| 나머지 25장(`U5_divider_knot`, `U6_band_top`, `U7_*`, `U8_*`, `U10_*`, `U11_*`, `U12_*`, `U13_*`) | `slices.json` size | 0,0,0,0 | Simple(`U6_band_top`·`U7_*`·`U10_wave_line`은 늘림) |

- **아틀라스**: 프로젝트는 SpriteAtlas v2를 쓴다(`Resources/Art/Characters.spriteatlasv2` — Point, `AIGenerated/UI` 폴더가 여기 들어 있음, `Ground`). 새 그림은 Characters에 넣지 않는다(아틀라스 필터가 Point라 Bilinear 예외가 깨진다, 추정 — 아틀라스 텍스처 설정이 그림 설정을 덮음). 새 아틀라스 `Resources/Art/HudUi.spriteatlasv2`: 폴더 `Art/HD2D/UI` 하나, Filter Bilinear, 밉맵 끔, 압축 없음, 패딩 4, 회전·tight packing 끔, Include in Build. 장면 Image는 그림을 직접 참조하고(아틀라스가 묶음), 런타임에 읽는 그림(조이스틱 E27, 월드 막대 E70~E72)만 `SpriteAtlasService.Get(name, HudUiAtlasPath)`로 읽는다(새 상수 하나, 서비스·DI 그대로).
- 도트로 남는 것(Point 그대로): 캐릭터 초상(Characters 아틀라스 `_front` 그림), 아이템 아이콘. 4단계 W4가 `_front`를 `downright` 별칭으로 바꾸면 초상 키도 별칭 표를 따른다(W4 커밋 뒤 확인).

## 4. 글꼴·배치 규칙

### 4.1 글꼴

- **확인한 사실**: `Font/NotoSansKR.ttf`는 가변 글꼴이고 굵기 축 `wght` 100~900, **기본값 100(Thin)**, OS/2 weightClass 100(파일 헤더를 직접 읽음). uGUI `Text`(동적 글꼴)는 가변 축을 고르지 못해 기본 인스턴스(Thin)로 그린다(추정 — 지금 굵은 글자는 `FontStyle.Bold` 합성). 그래서 "Regular/Light 둘 다 이 파일 하나로 된다"(`notes.md` U14 행)는 uGUI에서는 맞지 않는다(추정).
- **할 일(U1)**: `/usr/bin/python3`에 fontTools 4.60.2가 있다(확인). `fontTools.varLib.instancer`로 같은 파일에서 정적 인스턴스 2개를 만든다: `Font/NotoSansKR-Regular.ttf`(wght 400), `Font/NotoSansKR-Light.ttf`(wght 300). 새 글꼴이 아니라 이미 정한 U14의 굵기를 꺼내는 것이다. 라이선스 `Font/OFL.txt`를 함께 둔다(지금 없음). 만든 명령·해시는 `Font/source.txt`.
- **역할 표**(편집기 이행 메뉴 안 한 곳, 코드 상수): `Body`(Regular), `Sub`(Light 또는 Regular, #bebbba), `Emphasis`(지금 Regular, **바꿔 끼울 자리 = 상수 `EmphasisFontPath` 하나**). `FontStyle.Bold`는 모두 Normal로(K16 "굵게 금지"). 강조 역할: E13 남은 시간, E32 정비 제목, E61 승패, (후속) E73 피해 숫자.
- 크기: 본문 10~12, 제목 13~16, 꼬리표 최소 9(기준서 3.13 B·F). 지금 9 미만인 것: 대원 HP 8(E20), 진형 표식 7(E38), 아이템 칸 6(E24).
- 색: 주 #ececec / 보조 #bebbba / 꺼짐 #a8acaf. 장면 위(띠 위) 글자에만 uGUI `Shadow`(검정 α0.5, (1, −1)기준px, 제안). 판 안 글자는 그림자 없음.
- U15가 승인되면: `Font/` 에 Gowun Batang + Cormorant Garamond Italic + OFL.txt를 넣고 `EmphasisFontPath`만 바꾼 뒤 이행 메뉴를 다시 돈다(숫자는 Cormorant, 한글 제목은 Gowun — 두 글꼴을 한 Text에 섞을 수 없으므로 역할을 `EmphasisNumber`·`EmphasisTitle` 둘로 나눠 둔다).

### 4.2 배치

| 규칙 | 값 | 근거 |
|---|---|---|
| 캔버스 | Overlay, `ScaleWithScreenSize` 640×360, 높이 맞춤 1 — 그대로. 1080 → 3배, 1170 → 3.25배, 1536 → 4.27배 | UE2·UE3. 4단계 3.10이 넘긴 "캔버스 배율 정수화"는 **하지 않는다**: 틀이 고해상이라 정수 아닌 배율에서도 깨지지 않는다(기준서 3.13 F) |
| ppu 배수 | 새 그림을 쓰는 모든 Image `pixelsPerUnitMultiplier = 3` | `slices.json` `_about` |
| 버튼 | 보이는 판 = `U3_*` 높이 28.67기준px 고정(판 78텍셀 + 여백), 폭 ≥ 40(양끝 20+20). **누름 영역 44기준px 이상**: 버튼 루트 = 투명 Image(raycast, α0) 44 이상, 자식 `Plate`(Sliced U3, `Button.targetGraphic`, Sprite Swap) + `Label` | 3.13 F, `notes.md` 다른 점 2 |
| 주/보조 버튼 | 주(출발·다음 웨이브·로비로) = 평소 `U3_button_highlight`. 보조(회전·선택 취소) = 평소 `U3_button_normal`, 강조 `U3_button_highlight` | 3.13 C(출발·다음 웨이브 = 강조), `preview-result.png`(로비로 = 강조). 제안 |
| 작은 줄 버튼 | 부활·상세 보기·진형 = `U4_row_*`, 보이는 줄 14~16, 누름 44 | `slices.json` U4 note |
| 기호 버튼 | 배속·일시정지 = `U12_*` 16×16기준px(SetNativeSize), 누름 44×44 | `slices.json` U12 note |
| 판 투명도 | 판 바탕 알파는 그림에 들어 있다(메뉴 검정 0.55, 전투 #04182a 0.5, 띠 0.75). **Image.color는 흰색 α1 유지** — 곱하면 테도 어두워진다 | `notes.md` 다른 점 1 |
| 막대 | 두께 HUD HP 3기준px, 쿨 2기준px, 색은 Image.color로(#568757 / #5b7984 / 적 #a4534a) | 3.13 B, `notes.md` U7 |
| 안전 영역 | 글자·버튼·초상은 `SafeArea` 안. **판 바탕·띠는 화면 끝까지**: 새 컴포넌트 `UI_SafeAreaBleed`(Core 아님 — `UI/`, 늘릴 가장자리 선택)가 `SafeAreaLayout`의 같은 계산으로 안전 영역 밖까지 rect를 넓힌다 | 3.13 F 첫 줄, 4단계 3.4 안전 영역 |
| 가장자리 여백 | 안전 영역 안쪽 9~14기준px | 3.13 B |
| 좁은 화면 | 4:3 폭 480에 로비 카드 묶음(460)·가방 4개(424)·정비 가운데 버튼(보관함 4~184, 정보 296~476 사이 100폭)이 들어간다(장면 값으로 계산, 화면 미확인). 21:9에서 가운데 묶음을 늘이지 않는다. 결과 띠 폭 300 고정 | 3.13 F |
| 도트 정수 배율 | 초상 얼굴·아이템 아이콘은 화면 px 기준 정수배: 새 순수 함수 `UiDotScale.CanvasPerTexel(desiredCanvasPerTexel, canvasScale)` = `max(1, floor(desired × scale)) / scale`. 초상 S 1, L 4/3, 아이콘은 칸 맞춤 뒤 내림 | 3.13 F "도트 정수 배율", `notes.md` U8(S 22텍셀 ×3, L 34텍셀 ×4) |
| 얼굴 자르기 | 영웅마다 다른 `FACE_CENTER`(bruno 34,31 / bolt 36,32 / lena 33,31 / mira 33,35, `make_ui.py:743`) 대신 **공통 가운데 (34,32)**(제안, 최대 3텍셀 어긋남, 추정). 영웅별이 필요하면 캐릭터 데이터 칸 추가(후속) | 범위를 넓히지 않음 |

## 5. 코드가 바뀌는 곳과 장면만 바뀌는 곳

| 구분 | 파일 | 바뀌는 것 |
|---|---|---|
| 새 편집기 | `02.Scripts/Editor/HudUiImport.cs` | `slices.json` → `spriteBorder`, 다시 임포트(메뉴) |
| 새 편집기 | `02.Scripts/Editor/MainSceneHudUiMigration.cs` | 메뉴 `DeadSurvivor/Art/Apply HD2D UI to Main`. 화면별 메서드(로비·HUD·정비·결과), 글꼴 역할 표, `UI_ArtSurface` 떼기, 버튼 구조(투명 루트 + Plate), 꼬리표·막대·마디·구분선 새 오브젝트, Sprite Swap 상태. **두 번 돌려도 같은 결과**(있는 것은 건너뜀, 4단계 이행 메뉴와 같은 방식) |
| 바꿈(편집기) | `Editor/UiArtSceneUtils.cs` | 메뉴 `Apply UI Art to Main`이 가죽을 다시 입히지 않게 메뉴를 막는다("HD-2D UI로 대체됨" 예외). 파일은 둔다 |
| 바꿈(편집기) | `Editor/MainSceneBuilder.cs` | `CreateCanvas` 끝 `UiArtSceneUtils.ApplyToCanvas` → 새 이행의 `ApplyToCanvas`. 빌더 주석 "UI 도트 크기가 월드와 같다" 고침 |
| 새 런타임 | `UI/UI_SafeAreaBleed.cs`, `UI/Utils/SafeAreaLayout.cs`에 `Bleed(...)` 추가 | 4.2 안전 영역 |
| 새 런타임 | `UI/Utils/UiDotScale.cs` | 4.2 도트 정수 배율(순수) |
| 새 런타임 | `Features/Run/Utils/WaveProgressMath.cs`(이름은 grep 뒤 확정) | 마디 i의 상태(지남·지금·남음), 웨이브 0·초과 처리(순수) |
| 새 런타임 | `Features/Bag/Utils/BagCellArt.cs`(이름은 grep 뒤 확정) | `BagCellDisplay` → 칸 그림 상태(지금 색 고르기 `UI_BagCellView.cs` 조건 순서 그대로: Valid > Invalid > Picked > Item > Empty) |
| 바꿈 | `UI_CharacterCardView` | 초상 영역·배율(UiDotScale), 값 문자열 형식(꼬리표 없이 값만) |
| 바꿈 | `UI_BattleHudView` | 마디 Image 배열 + 세 그림 직렬화 칸, `UpdateUI`에서 마디 상태. 기존 `UpdateUI` 인자는 그대로 |
| 바꿈 | `UI_MemberStatusView` | 틀 Image + 살아 있음/쓰러짐 그림 칸, 영역 22, UiDotScale |
| 바꿈 | `UI_BattleItemView` | 바탕 색 → 그림 두 칸(보통·비활성), 색 칸은 막대·글자용으로 남김 |
| 바꿈 | `UI_FormationView` | 버튼 색 → 줄 그림 두 칸 + 표식 Image 배열, 표식 초상(D4에 따라) |
| 바꿈 | `UI_BagCellView`, `UI_StashItemView` | 색 → 그림(BagCellArt), 등급 테(G1에 따라) |
| 바꿈 | `UI_PrepView` + `Features/Run/Presenter/PrepPresenter.cs` | `UpdateUI(completedWave, memberLineList)`에 HP 비율 목록 추가, 막대 앵커 |
| 바꿈 | `UI_ResultView` | 값 문자열 형식 |
| 바꿈 | `UI_ItemIconView` | 그리는 rect를 UiDotScale로 내림(가운데 정렬 유지) |
| 바꿈 | `Features/Party/Presenter/MoveInputPresenter.cs:33`, `SpriteAtlasService` | 조이스틱 키 `U13_*`, 새 아틀라스 상수 |
| 바꿈(월드, U7) | `WorldPresenter.cs:228-229`, `HpLossPresenter.cs:43`, `MonsterHpBarViewFactory.cs:38`, `UiArtLayout.Border` | 막대 그림 키 `U7_*`, 틀 0 |
| 장면만 | `Main.unity` | 모든 `Image` 그림·형·ppu 배수, 글꼴·크기·색, 띠·판 크기, 직렬화 색 칸(`winColor`, `loseColor`, 막대 색, 이름 색) |
| 시험 | `Tests/EditMode/UiArtLayoutTests.cs` | `SceneArtMenuLeavesNoStaticPlaceholderImages…`: 새 이행 메뉴, 경로 접두어 `Art/HD2D/UI/`(초상·아이콘 예외 그대로). `MemberPortraitKeeps…`: 창 크기·위치 기대 바뀜. 가죽 땀 시험(`PanelMesh…`, `SmallPanelQuads…`, `Borders…`)은 `UI_ArtSurface`·가죽 그림이 남아 있으므로 그대로 통과해야 함. `WorldHpArt…`는 U7에서 바뀜 |
| 새 시험 | `HudUiImportTests`, `SafeAreaLayoutTests`(Bleed 사례 추가), `UiDotScaleTests`, `WaveProgressMathTests`, `BagCellArtTests`, `HudUiSceneTests`(화면별: 가죽 참조 0, 누름 영역 ≥ 44, 글자 ≥ 9, Bold 0, ppu 배수 3) | |

- 판정 진입점 `PlayableProbe`는 버튼을 누르지 않고 Presenter를 직접 부른다(`PressDepart` 등 `PlayableProbe.cs:797-801`). 그래서 버튼 구조를 바꿔도 판정 경로는 깨지지 않지만 **누름 영역은 판정 경로가 확인하지 않는다** → `HudUiSceneTests`가 크기를 센다.
- DI: 새 등록 없음. 새 컴포넌트는 장면 MonoBehaviour이고 아무것도 주입받지 않는다.

## 6. 작업 단위 (U0~U8)

- 순서대로 하나씩, **단위마다 커밋**(지정 파일만). 에디터 하나라 나란히 하지 않는다. 다음 단위 전에 컴파일 0.
- 화면 캡처 흐름은 U0에서 새 파일 `Tools/playable/hd2d-ui-flows.json` 하나로 만든다(기존 흐름 파일은 4단계가 고치는 중이라 건드리지 않는다). 단계 id: `L`(로비), `B`(전투 웨이브 2, 마디 1개 지남), `JOY`(조이스틱 누름 중), `DOWN`(쓰러진 대원 칸·비활성 아이템), `P`(정비, 보관함 전리품 3~5개, 쓰러진 대원 부활 버튼), `PICK`(보관함 아이템 집음 — 가방 칸 상태), `WIN`, `LOSE`, `ALL`(마감용 `capture: "all"`). 식은 `f1-flows`(WIN·LOSE), `f2-movement-flows`(JOY), `f3-combat-flows`(DOWN·PREP·부활), `f5-bag-flows`(PREP·PLACE)에서 그대로 가져온다. 흐름 파일 `sizes`: `[[1920,1080],[2532,1170],[2520,1080],[2048,1536]]`(19.5:9는 4단계 route의 2340×1080 대신 정수 아닌 3.25배를 보는 2532×1170).
- "캡처 직접 엶" = 실행 세션이 캡처를 열어 보고 3.13 C표의 그 화면 행과 대조해 핸드오프에 "본 캡처 목록"(경로·본 것)을 적는다. 디렉터도 연다.

| 단위 | 하는 일 | 바꾸는 파일 | 완료 조건 | 그 단위에서 돌릴 확인 |
|---|---|---|---|---|
| **U0 그림 넣기·흐름 파일** | 42장 + `slices.json` + `source.txt`를 `Art/HD2D/UI/`로 복사(스크립트), 프리셋·Preset Manager 필터, `HudUiImport` 메뉴, `HudUi.spriteatlasv2`, `Art/CLAUDE.md` 예외 줄, `hd2d-ui-flows.json`(아직 `capture`만, 판정 식 없음 단계는 `expect` 없음), `HudUiImportTests` | 그림 42 + 메타, json 2, 프리셋 1, ProjectSettings 프리셋 목록 1, 아틀라스 1, .cs 2, 문서 1 | 컴파일 0. 42장 모두 Bilinear·PPU 100·밉맵 끔·border = `slices.json`. 아틀라스에 42장. 기존 Point 그림 값 그대로 | 컴파일. `-testFilter "HudUiImportTests;ArtImportSettingsTests"`. `playable.py --check --route Tools/playable/hd2d-ui-flows.json`. Play 없음(화면 안 바뀜) |
| **U1 글꼴·공통 기반** | 정적 Noto Sans KR Regular/Light + OFL.txt + source.txt, `MainSceneHudUiMigration` 뼈대(글꼴 역할 표, 공통 도우미: 버튼 구조·Sliced·ppu 3·`UI_ArtSurface` 떼기), `UiArtSceneUtils` 메뉴 막기, `MainSceneBuilder` 연결, `UI_SafeAreaBleed`+`SafeAreaLayout.Bleed`, `UiDotScale`, `UiArtLayoutTests` 장면 시험 고침, 새 시험 | 글꼴 2 + txt 2, .cs 약 7, 시험 3 | 컴파일 0. 시험 통과. 이 단위는 장면을 아직 바꾸지 않는다(메뉴는 화면 메서드가 빈 채) | 컴파일. `-testFilter "SafeAreaLayoutTests;UiDotScaleTests;UiArtLayoutTests"`. Play 없음 |
| **U2 로비** | E01~E07. 이행 메뉴 로비 메서드, `UI_CharacterCardView` | .cs 2, 장면, 시험 1 | 3.13 C 로비 행대로: 반투명 판 4장 + 마름모 L 초상(얼굴 정수 배율) + 이름·직업 + 매듭 구분선 + "항목 : 값" + 강조 출발 버튼. 누름 ≥ 44, 글자 ≥ 9, Bold 0 | 컴파일. `-testFilter "HudUiSceneTests;UiDotScaleTests"`. `playable.py --route Tools/playable/hd2d-ui-flows.json --until 1`(L) 캡처 1장 1920×1080 직접 엶 |
| **U3 전투 HUD 위** | E10~E16. `UI_BattleHudView`, `WaveProgressMath`, 위 띠 bleed | .cs 2~3, 장면, 시험 1 | 위 띠 그라데이션이 화면 위 끝까지, 마디 10개(지난 웨이브 밝게, 지금 파란 큰 마디), 기호 버튼 누름 44, G2 처리대로 일시정지 상태 | 컴파일. `-testFilter "WaveProgressMathTests;HudUiSceneTests"`. 흐름 `--until 2`(B) 캡처 1장 엶 |
| **U4 전투 HUD 아래·조이스틱** | E17~E27. `UI_MemberStatusView`, `UI_BattleItemView`, `UI_ItemIconView`, `MoveInputPresenter`, `SpriteAtlasService` 상수, 아래 띠 bleed | .cs 5~6, 장면, 시험 2 | 틀 없는 대원 줄 4개(마름모 S 초상 살아 있음/쓰러짐, 이름·숫자, 회녹색 막대, 아이템 칸), 조이스틱 U13 | 컴파일. `-testFilter "UiArtLayoutTests;HudUiSceneTests;SpriteAtlasServiceTests;HpLossTrailTests"`. 흐름 `JOY`·`DOWN` 캡처 2장 엶(`--until`은 그 단계 번호) |
| **U5 정비 머리·진형·버튼** | E30~E38, E51·E52. `UI_PrepView`, `PrepPresenter`, `UI_FormationView`, 머리 띠 bleed | .cs 3~4, 장면 | 머리 띠 + 밑줄, 대원 줄 막대, 강조 다음 웨이브, 부활 줄 버튼, 진형 명령 줄(선택 = 파란 빛 + 마름모 표식), 회전·선택 취소 | 컴파일. `-testFilter "HudUiSceneTests"`(UI View·`PrepPresenter`를 다루는 기존 EditMode 시험은 `UiArtLayoutTests`뿐 — grep 확인). 흐름 `P` 캡처 1장 엶 |
| **U6 정비 가방·보관함·정보** | E39~E50. `UI_BagCellView`, `UI_StashItemView`, `BagCellArt`, G1 처리 | .cs 3~4, 장면(+ G1에 따라 그림 1) | 칸 5상태 그림, 보관함·정보 반투명 판, "새" 금색, 등급 표시 | 컴파일. `-testFilter "BagCellArtTests;HudUiSceneTests;BagPreviewTests"`. 흐름 `P`·`PICK` 캡처 2장 엶 |
| **U7 결과·월드 표시** | E60~E65(`UI_ResultView`, 띠 판 bleed), E70~E72(4단계 W3·W4 커밋 뒤에만) | .cs 4~5, 장면, 시험 1~2 | 결과 = 가운데 세로 띠 300 + 금색 승리/회색 패배 + 항목:값 + 도움말 띠. 머리 위 막대 틀 없음·색 토큰 | 컴파일. `-testFilter "UiArtLayoutTests;MonsterHpBarRuleTests;HpLossTrailTests;HudUiSceneTests"`. 흐름 `WIN`·`LOSE` 캡처 2장 엶. `Tools/playable/f6-feedback-flows.json` `MHP`까지 1회(머리 위 막대) 캡처 엶 |
| **U8 마감** | 고치지 않는다. 마감 확인·리뷰만 | 0(문서: 기준서 3.3 UI 행 지금 값, `docs/현재상태.md`) | 1절 완료 조건 전부 | 흐름 `ALL`(`capture: "all"`, 4화면비) 로비·전투·정비·결과 4장면 × 4 = 16장 + 노치 1벌(G5) 엶. **EditMode 전체 1회. `playable.py` route 전체(G1~G8) 1회.** 장면의 가죽 그림 GUID 참조 수 = 0(grep). 마감 리뷰 묶음(병렬): validator-client, spec-reviewer(이 문서 + 기준서 3.13), algorithm-tester(`WaveProgressMath`, `SafeAreaLayout.Bleed`, `UiDotScale`, `BagCellArt`). FAIL만 고쳐 그 리뷰만 수정 확인 |

- 판정에 못 미치면 고쳐 같은 확인을 다시 돈다. 같은 방법 2번 실패면 방법을 바꾼다(AGENTS.md). 기준값(누름 44, 글자 9, 색 토큰)은 낮추지 않는다.

## 7. 빈칸(그림 없음)과 디렉터 결정

**디렉터 확정(2026-11-18 10:2x)**: 아래 제안을 모두 그대로 채택한다(G1·G2·G4 그림은 디렉터가 4단계와 나란히 미리 만든다). 화면비는 5종(4단계 route의 2340×1080 더함). 글꼴은 설치된 가변 `NotoSansKR.ttf`에서 fontTools로 Regular 400·Light 300 정적 판을 꺼내고 `OFL.txt`를 함께 둔다(새로 내려받지 않음).

| # | 무엇 | 왜 | 제안(디렉터가 정함) |
|---|---|---|---|
| G1 | 등급 색 테(E40 가방 칸 `Border`, E44 보관함 `Border`) | U9는 테가 그림에 박혀 있어 등급 색을 곱하면 바탕까지 물든다(곱하기라 짙은 바탕은 거의 안 바뀌지만 테 #8fa0aa × 등급색이 탁해짐, 추정) | `make_ui.py`에 `U9_cell_grade`(1px 흰 테만, 안 투명, 48×48, border 6) 한 장을 숫자로 더한다(유료 0). 안 되면 등급을 칸 왼쪽 위 작은 점(`U7_bar_fill` 2×2 등급색)으로 |
| G2 | 일시정지 상태 "계속" 기호(E16) | U12에 재생 기호 없음 | `U12_icon_resume`(오른쪽 삼각) 한 장을 숫자로 더한다. 대안: 일시정지 기호 + 작은 글자 "계속" |
| G3 | 피해 숫자 세리프 기울임(E73) | U15 강조 글꼴 미설치(사용자 확인 필요) | 이번에는 도트 숫자 그대로, 색만 토큰 맞춤도 하지 않음. U15 승인 뒤 TMP SDF로 바꾸는 후속 단위(U9) |
| G4 | 조이스틱 크기(E27) | 장면 `Ring` 56×56·`Handle` 24×24(장면 값 확인). `notes.md`는 "120px = 40기준px(지금 판과 같은 크기)"라 적었지만 **지금 판은 56**. 120텍셀을 56기준px(168px)로 늘리면 Bilinear로 흐려진다 | `make_ui.py`에서 U13을 168×168 / 72×72텍셀로 다시 그린다(조작 느낌 유지). 대안: 판을 40·16으로 줄이고 `handleTravel` 14 → 12 |
| G5 | 노치 캡처 | `UI_SafeAreaPanel`은 `Screen.safeArea`를 읽고 `playable.py`는 노치를 흉내 내지 못한다 | 편집기 전용 덮어쓰기(`#if UNITY_EDITOR` 정적 Rect)를 `UI_SafeAreaPanel`에 두고 흐름 단계 `do`로 iPhone 가로 안전 영역(2532×1170에서 좌우 각 약 141px·아래 63px, 추정 값)을 넣어 캡처. 대안: Device Simulator 손 확인(사람 몫) |
| D1 | 대원 상태 위치(3.13 C·H 남은 결정) | 조이스틱이 누른 자리에 뜨는 방식(`UI_JoystickView` — 고정 자리 없음)이라 손가락 자리 충돌이 화면 어디서나 같다 | **아래 띠 유지**(`preview-hud.png`와 같음, 지금 배치·시험 그대로). 오른쪽 위 세로 쌓기는 쓰지 않음 |
| D2 | 주 버튼의 평소 상태 | 3.13 C는 "평소 어두움, 누름·강조 때 파란 빛"과 "다음 웨이브 = 강조 상태"를 함께 적음 | 출발·다음 웨이브·로비로는 평소 강조, 나머지는 평소 보통(4.2) |
| D3 | 전투 아이템 칸 글자(E24) | 칸 16×8에 최소 글자 9가 안 들어감 | 칸에는 아이콘 + 쿨 막대만, 상태 글(준비·n초·발동!)은 뺀다(상태는 막대·칸 그림으로). 그림 없는 아이템의 이름 글자만 9로 잘라 둠. 판정 경로의 `ItemState`는 Presenter 값이라 영향 없음(`PlayableProbe.cs:95`) |
| D4 | 진형 미리보기 대원 표식(E38) | 마름모 초상이면 Presenter가 초상 그림을 넘겨야 함 | 마름모 S 틀 + 얼굴, 이름 글자 뺌 |
| D5 | 아이템 정보 판 구조(E50) | 3.13 C의 머리줄 이름·항목:값·개선 금색은 BagPresenter 글 형식 변경 | 이번에는 판·글자 모양만. 형식은 후속 |
| D6 | 쓰지 않는 그림 | `U1_panel_battle`(전투 중 판넬 없음), `U2_glow_band`(선택 빛이 U3·U4·U9 그림 안에 이미 있음), `U8_portrait_L_active`·`U8_portrait_L_dead`·`U8_portrait_S_active`(게임에 "행동 중"·로비 쓰러짐 상태 없음), `U12_icon_lock`(잠긴 것 없음) | 넣기는 42장 모두(아틀라스·시험), 장면 참조는 없음. 3.3 대조표에 "넣음·안 씀"으로 |
| D7 | 세로 띠·띠 판이 안전 영역 밖으로 | `SafeArea` 아래에 있는 View 루트는 노치·홈 표시줄 자리를 덮지 못함 | `UI_SafeAreaBleed`(5절) |

## 8. 4단계·5단계와 겹치는 곳

| 대상 | 겹침 | 처리 |
|---|---|---|
| `Main.unity` | 4단계 W2~W3 이행 메뉴가 같은 장면을 바꾸는 중(작업 트리 미커밋) | **U0은 4단계와 상관없이 할 수 있으나(장면 안 건드림), U2부터는 4단계 W7 커밋 뒤**. 5단계(배경)도 `Main.unity`를 바꾸면 U와 번갈아 하지 않고 한 단계씩 끝낸다. UI 이행은 `Canvas` 아래만 고친다 |
| 4단계 W6(화면비·안전 영역) | W6은 3D 화면 n·size와 HUD가 3D와 겹쳐 읽히는지를 본다(코드 0 목표) | U8의 4화면비 캡처가 같은 화면비를 다시 찍는다. W6 결과 캡처를 U 시작 전 기준으로 열어 둔다. 화면비 목록 차이: 4단계 route는 2340×1080, U는 2532×1170(정수 아닌 UI 배율 3.25 확인용) — 둘 다 판정에 쓰면 U8 `sizes`에 2340×1080을 더한다(디렉터 결정) |
| 4단계 W3 | `MemberView` HP 막대 레이어, `MonsterHpBarView` 프리팹, `DamageNumberView`, `UiArtLayoutTests.WorldHpArt…`를 바꿈 | U7 월드 표시는 W3·W4 커밋 뒤, **그림 키·색·틀 두께만** 바꾼다(레이어·위치·세운 자식은 그대로) |
| 4단계 W4 | `SpriteAtlasService`(별칭 표), `PlayableProbe`, `art-insert-flows.json`, 초상 키 `_front` | U4의 `SpriteAtlasService` 상수 추가는 W4 뒤 다시 확인. U는 `PlayableProbe`·기존 흐름 파일을 고치지 않는다(G5를 고르면 `UI_SafeAreaPanel`만) |
| 4단계 W7 | EditMode 전체·route 전체 | U8에서 UI 변경 뒤 다시 한 번(게임 동작 파일 = 장면·View가 바뀌었으므로) |
| 6단계(빛·효과) | 볼륨 값 | Overlay라 영향 없음. 단 띠 그라데이션이 밝은 풀밭 위에서 옅게 보일 수 있음(`notes.md` 아쉬운 점) — 6단계 뒤 밝기가 바뀌면 띠 곡선을 다시 볼 수 있음(후속) |
| 7단계(성능) | UI 그리기 수 | U에서 재지 않는다. 새 아틀라스 1장 + 도트 그림(Characters)으로 캔버스 그리기는 2~3묶음(추정) |
| 3.3 범위 대조표 | UI 행(U1~U13 13행, U14·U15) | U8에서 지금 값으로 고친다: U14 = 정적 인스턴스 2(차이 0), U15 = 0(차이 −2, 사용자 확인 대기) |

## 9. 그림 확인(이 문서가 부른 그림이 모두 있는가)

- 검사: 이 문서의 1~8절에서 `U\d+_[A-Za-z_]+` 이름을 모두 뽑아 `output/assets/hd2d-ui/sprites/<이름>.png`가 있는지 본다(`python3`, 2026-11-18). 결과는 아래 표에 적는다(디렉터 보고와 같은 숫자).

| 항목 | 값 |
|---|---|
| 이 문서가 부른 그림 이름(1~8절, 새 후보 `U9_cell_grade`·`U12_icon_resume`과 `U4_row_*` 같은 묶음 표기 제외) | 42종 — `sprites/`에 42 / 42 있음, 없는 이름 0 |
| `sprites/` 전체 | 42장(`slices.json` 42항목과 이름이 같음) |
| 2절 요소 표에 실제로 배치한 그림 | 36종 / 42 |
| 넣지만 장면에서 쓰지 않는 그림 | 6장: `U1_panel_battle`, `U2_glow_band`, `U8_portrait_L_active`, `U8_portrait_L_dead`, `U8_portrait_S_active`, `U12_icon_lock`(D6) |
| 2절 요소 | 58개 = 글자만 13 + 그림·월드 45 |
| 그중 U 그림이 필요 없는 것 | 2개 — E18(바탕을 없앰), E25(아이템 도트 아이콘 그대로) |
| **U 그림이 필요한 요소** | **43개** |
| 다 덮인 요소 | 39 / 43 (E27 조이스틱은 그림은 있으나 크기 어긋남 — G4) |
| 일부만 덮인 요소 | 1 — E16 일시정지(멈춤 상태 "계속" 기호 없음, G2) |
| 그림이 없는 요소 | 3 — E40 가방 칸 등급 테, E44 보관함 등급 테(G1), E73 피해 숫자 세리프(G3, 그림이 아니라 U15 글꼴) |

## 10. 추측으로 남긴 것 (구현 단위에서 확인)

- uGUI `Text`가 가변 글꼴의 기본 인스턴스(Thin)로 그린다(U1에서 정적 Regular로 바꾼 전후 캡처로 확인).
- SpriteAtlas 필터가 그림 임포트 필터를 덮는다(그래서 Characters에 넣지 않음). U0에서 아틀라스 미리보기 필터로 확인.
- 4:3에서 정비 가운데 버튼이 보관함·정보 판 사이에 들어간다(장면 값 계산, 누름 영역 44 폭까지 넣으면 좁음). U8 캡처로 확인.
- 노치 안전 영역 값(G5), 얼굴 공통 가운데의 어긋남(최대 3텍셀).

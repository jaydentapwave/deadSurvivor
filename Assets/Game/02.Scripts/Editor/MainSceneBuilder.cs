using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Main.unity와 MonsterView 프리팹을 처음 한 번 만든다(설계 브리핑 "장면·자산을 만드는 방법").
// 둘 중 하나라도 있으면 아무것도 쓰지 않는다 — 처음 만든 뒤에는 장면·프리팹이 원본이다.
// 크기·위치 수치는 기준 해상도 640×360(아트 기준서 K2) 기준 UI 픽셀이다.
public static class MainSceneBuilder
{
    private const string ScenePath = "Assets/Game/01.Scene/Main.unity";
    private const string PrefabFolder = "Assets/Game/03.Resources/Resources/Prefab";
    private const string MonsterPrefabPath = PrefabFolder + "/MonsterView.prefab";
    private const string FontPath = "Assets/Game/03.Resources/Font/NotoSansKR.ttf";
    // 두 RP 자산의 렌더러 목록 끝에 덧붙인 2D 렌더러(기획서 F절)
    private const int Renderer2DIndex = 1;
    private const int ReferenceWidth = 640;
    private const int ReferenceHeight = 360;
    private const int MemberCount = 4;
    private const int ItemSlotCount = 16;
    private const int ItemColumns = 4;

    private static readonly Color PanelColor = new(0.16f, 0.11f, 0.08f, 0.94f);
    private static readonly Color BarColor = new(0f, 0f, 0f, 0.55f);
    private static readonly Color ButtonColor = new(0.47f, 0.31f, 0.16f, 1f);
    private static readonly Color SlotColor = new(0.83f, 0.74f, 0.55f, 0.92f);
    private static readonly Color SlotTextColor = new(0.2f, 0.12f, 0.06f, 1f);
    private static readonly Color MutedTextColor = new(0.85f, 0.8f, 0.7f, 1f);

    private static Font _font;

    [MenuItem("Game/Build Main Scene (first time only)")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(ScenePath) != null || AssetDatabase.LoadAssetAtPath<Object>(MonsterPrefabPath) != null)
        {
            GameLogger.LogWarning($"[MainSceneBuilder] {ScenePath} or {MonsterPrefabPath} already exists. Nothing written.");
            return;
        }

        if (SceneManager.GetActiveScene().isDirty)
        {
            GameLogger.LogWarning("[MainSceneBuilder] The open scene has unsaved changes. Save or discard them first.");
            return;
        }

        var createdPathList = new List<string>();
        try
        {
            CreateMonsterPrefab();
            createdPathList.Add(MonsterPrefabPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 새 장면을 연 뒤에 읽는다(NewScene이 앞서 읽은 에셋 참조를 끊는다)
            _font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            BuildScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            createdPathList.Add(ScenePath);
            GameLogger.Log($"[MainSceneBuilder] Built {ScenePath} and {MonsterPrefabPath}");
        }
        catch
        {
            foreach (var path in createdPathList)
            {
                AssetDatabase.DeleteAsset(path);
            }

            throw;
        }
    }

    private static void CreateMonsterPrefab()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Game/03.Resources/Resources", "Prefab");
        }

        var root = new GameObject("MonsterView");
        var body = root.AddComponent<SpriteRenderer>();
        body.sortingOrder = 0;
        body.spriteSortPoint = SpriteSortPoint.Pivot;
        var view = root.AddComponent<MonsterView>();
        SetReference(view, "bodyRenderer", body);
        PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void BuildScene()
    {
        CreateCamera();
        var fieldView = CreateField();
        var memberViews = CreateParty();
        var scope = new GameObject("GameSceneLifetimeScope").AddComponent<GameSceneLifetimeScope>();
        SetReferenceArray(scope, "memberViews", memberViews);
        CreateEventSystem();
        CreateCanvas();
        Selection.activeObject = fieldView.gameObject;
    }

    private static void CreateCamera()
    {
        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ReferenceHeight * 0.5f / Const.PixelsPerUnit;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.17f, 0.18f, 0.14f, 1f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(Renderer2DIndex);
        var pixelPerfect = cameraObject.AddComponent<PixelPerfectCamera>();
        pixelPerfect.assetsPPU = Const.PixelsPerUnit;
        pixelPerfect.refResolutionX = ReferenceWidth;
        pixelPerfect.refResolutionY = ReferenceHeight;
        pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.None;
        pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
    }

    private static FieldView CreateField()
    {
        var gridObject = new GameObject("Grid");
        var grid = gridObject.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize = new Vector3(1f, 0.5f, 1f);
        var fieldObject = new GameObject("Field");
        fieldObject.transform.SetParent(gridObject.transform, false);
        var tilemap = fieldObject.AddComponent<Tilemap>();
        // 바닥 그림 피벗 아래 가운데(임포트 프리셋) → 앵커는 마름모 아래 꼭짓점
        tilemap.tileAnchor = Vector3.zero;
        var tilemapRenderer = fieldObject.AddComponent<TilemapRenderer>();
        tilemapRenderer.sortingOrder = -100;
        var fieldView = fieldObject.AddComponent<FieldView>();
        SetReference(fieldView, "tilemap", tilemap);
        return fieldView;
    }

    private static MemberView[] CreateParty()
    {
        var partyObject = new GameObject("Party");
        var memberViews = new MemberView[MemberCount];
        for (var i = 0; i < MemberCount; i++)
        {
            var memberObject = new GameObject($"Member{i + 1}");
            memberObject.transform.SetParent(partyObject.transform, false);
            var body = CreateSpriteRenderer("Body", memberObject.transform, 0);
            body.spriteSortPoint = SpriteSortPoint.Pivot;
            var hpBack = CreateSpriteRenderer("HpBack", memberObject.transform, 1);
            var hpFill = CreateSpriteRenderer("HpFill", memberObject.transform, 2);
            var view = memberObject.AddComponent<MemberView>();
            SetReference(view, "bodyRenderer", body);
            SetReference(view, "hpBackRenderer", hpBack);
            SetReference(view, "hpFillRenderer", hpFill);
            memberViews[i] = view;
        }

        return memberViews;
    }

    private static SpriteRenderer CreateSpriteRenderer(string name, Transform parent, int sortingOrder)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        var spriteRenderer = child.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = sortingOrder;
        return spriteRenderer;
    }

    private static void CreateEventSystem()
    {
        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private static void CreateCanvas()
    {
        var canvasObject = new GameObject("Canvas", typeof(RectTransform));
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        // 가상 해상도와 같은 기준이라 UI 도트 크기가 월드와 같다. 가로 게임이라 높이를 맞추고 긴 화면은 옆으로 넓힌다
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        canvasObject.AddComponent<GraphicRaycaster>();

        var safeArea = CreateStretch("SafeArea", canvasObject.transform);
        SetReference(safeArea.gameObject.AddComponent<UI_SafeAreaPanel>(), "panel", safeArea);

        CreateLobbyView(safeArea);
        CreateBattleHudView(safeArea);
        CreatePrepView(safeArea);
        CreateResultView(safeArea);
    }

    private static void CreateLobbyView(Transform parent)
    {
        var root = CreateStretch("LobbyView", parent);
        CreateText("Title", root, "원정대", 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold,
            new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(300f, 22f));

        var cards = CreateRect("Cards", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 8f), new Vector2(460f, 200f));
        var cardsLayout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
        cardsLayout.spacing = 6f;
        cardsLayout.childControlWidth = true;
        cardsLayout.childControlHeight = true;
        cardsLayout.childForceExpandWidth = true;
        cardsLayout.childForceExpandHeight = true;

        var cardViews = new UI_CharacterCardView[MemberCount];
        for (var i = 0; i < MemberCount; i++)
        {
            var card = CreateRect($"Card{i + 1}", cards, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.gameObject.AddComponent<Image>().color = PanelColor;
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var colorImage = CreateLayoutImage("Color", card, 14f);
            var view = card.gameObject.AddComponent<UI_CharacterCardView>();
            SetReference(view, "colorImage", colorImage);
            SetReference(view, "nameText", CreateLayoutText("Name", card, 13, FontStyle.Bold, Color.white, 18f));
            SetReference(view, "roleText", CreateLayoutText("Role", card, 10, FontStyle.Normal, MutedTextColor, 14f));
            SetReference(view, "hpText", CreateLayoutText("Hp", card, 10, FontStyle.Normal, Color.white, 14f));
            SetReference(view, "atkText", CreateLayoutText("Atk", card, 10, FontStyle.Normal, Color.white, 14f));
            SetReference(view, "defText", CreateLayoutText("Def", card, 10, FontStyle.Normal, Color.white, 14f));
            SetReference(view, "itemText", CreateLayoutText("Item", card, 10, FontStyle.Normal, MutedTextColor, 14f));
            cardViews[i] = view;
        }

        var departButton = CreateButton("DepartButton", root, "출발", 14, new Vector2(0.5f, 0f), new Vector2(0f, 10f),
            new Vector2(140f, 30f));
        var lobbyView = root.gameObject.AddComponent<UI_LobbyView>();
        SetReferenceArray(lobbyView, "cardViews", cardViews);
        SetReference(lobbyView, "departButton", departButton);
    }

    private static void CreateBattleHudView(Transform parent)
    {
        var root = CreateStretch("BattleHudView", parent);

        var topBar = CreateRect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero,
            new Vector2(0f, 22f));
        topBar.gameObject.AddComponent<Image>().color = BarColor;
        var left = new Vector2(0f, 0.5f);
        var center = new Vector2(0.5f, 0.5f);
        var right = new Vector2(1f, 0.5f);
        var waveText = CreateText("Wave", topBar, "웨이브 1/10", 10, TextAnchor.MiddleLeft, Color.white, FontStyle.Normal,
            left, new Vector2(6f, 0f), new Vector2(72f, 20f));
        var killsText = CreateText("Kills", topBar, "처치 0", 10, TextAnchor.MiddleLeft, Color.white, FontStyle.Normal,
            left, new Vector2(80f, 0f), new Vector2(62f, 20f));
        var threatText = CreateText("Threat", topBar, "위협 0", 10, TextAnchor.MiddleLeft, Color.white, FontStyle.Normal,
            left, new Vector2(144f, 0f), new Vector2(62f, 20f));
        var remainingText = CreateText("Remaining", topBar, "0:30", 13, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold,
            center, Vector2.zero, new Vector2(52f, 20f));
        var burningText = CreateText("Burning", topBar, "버닝 타임!", 10, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.15f, 1f),
            FontStyle.Bold, center, new Vector2(30f, 0f), new Vector2(72f, 20f));
        burningText.rectTransform.pivot = new Vector2(0f, 0.5f);
        var speedButton = CreateButton("SpeedButton", topBar, "×1", 10, right, new Vector2(-62f, 0f), new Vector2(36f, 18f));
        var pauseButton = CreateButton("PauseButton", topBar, "일시정지", 9, right, new Vector2(-6f, 0f), new Vector2(52f, 18f));

        var bottomPanel = CreateRect("BottomPanel", root, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero,
            new Vector2(0f, 62f));
        bottomPanel.gameObject.AddComponent<Image>().color = BarColor;
        var bottomLayout = bottomPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        bottomLayout.padding = new RectOffset(4, 4, 3, 3);
        bottomLayout.spacing = 4f;
        bottomLayout.childControlWidth = true;
        bottomLayout.childControlHeight = true;
        bottomLayout.childForceExpandWidth = true;
        bottomLayout.childForceExpandHeight = true;
        var statusViews = new UI_MemberStatusView[MemberCount];
        for (var i = 0; i < MemberCount; i++)
        {
            statusViews[i] = CreateMemberStatus($"MemberStatus{i + 1}", bottomPanel);
        }

        var hudView = root.gameObject.AddComponent<UI_BattleHudView>();
        SetReference(hudView, "waveText", waveText);
        SetReference(hudView, "killsText", killsText);
        SetReference(hudView, "threatText", threatText);
        SetReference(hudView, "remainingText", remainingText);
        SetReference(hudView, "burningLabel", burningText.gameObject);
        SetReference(hudView, "speedButton", speedButton);
        SetReference(hudView, "speedText", speedButton.GetComponentInChildren<Text>());
        SetReference(hudView, "pauseButton", pauseButton);
        SetReference(hudView, "pauseText", pauseButton.GetComponentInChildren<Text>());
        SetReferenceArray(hudView, "memberStatusViews", statusViews);
    }

    private static UI_MemberStatusView CreateMemberStatus(string name, Transform parent)
    {
        var root = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        root.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.07f, 0.05f, 0.75f);
        var nameText = CreateText("Name", root, "이름", 9, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(3f, -1f), new Vector2(60f, 11f));
        nameText.rectTransform.pivot = new Vector2(0f, 1f);
        var hpText = CreateText("HpText", root, "0/0", 8, TextAnchor.MiddleRight, MutedTextColor, FontStyle.Normal,
            new Vector2(1f, 1f), new Vector2(-3f, -1f), new Vector2(50f, 11f));
        hpText.rectTransform.pivot = new Vector2(1f, 1f);

        var hpBack = CreateRect("HpBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -13f),
            new Vector2(-6f, 4f));
        hpBack.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.08f, 0.06f, 1f);
        var hpFill = CreateRect("Fill", hpBack, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        hpFill.gameObject.AddComponent<Image>().color = new Color(0.36f, 0.82f, 0.36f, 1f);

        var items = CreateRect("Items", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -19f),
            new Vector2(-6f, 35f));
        var grid = items.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(26f, 8f);
        grid.spacing = new Vector2(1f, 1f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = ItemColumns;
        var slotTexts = new Text[ItemSlotCount];
        for (var i = 0; i < ItemSlotCount; i++)
        {
            var slot = CreateRect($"Slot{i + 1}", items, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            slot.gameObject.AddComponent<Image>().color = SlotColor;
            var slotText = CreateText("Text", slot, "", 6, TextAnchor.MiddleCenter, SlotTextColor, FontStyle.Normal,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(slotText.rectTransform);
            // 최적 크기 맞추기는 줄바꿈 모드에서만 동작한다
            slotText.horizontalOverflow = HorizontalWrapMode.Wrap;
            slotText.verticalOverflow = VerticalWrapMode.Truncate;
            slotText.resizeTextForBestFit = true;
            slotText.resizeTextMinSize = 3;
            slotText.resizeTextMaxSize = 6;
            slotTexts[i] = slotText;
        }

        var view = root.gameObject.AddComponent<UI_MemberStatusView>();
        SetReference(view, "nameText", nameText);
        SetReference(view, "hpText", hpText);
        SetReference(view, "hpFill", hpFill);
        SetReferenceArray(view, "itemSlotTexts", slotTexts);
        return view;
    }

    private static void CreatePrepView(Transform parent)
    {
        var root = CreateStretch("PrepView", parent);
        var box = CreateRect("Box", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(280f, 210f));
        box.gameObject.AddComponent<Image>().color = PanelColor;
        var top = new Vector2(0.5f, 1f);
        var title = CreateText("Title", box, "웨이브 1 완료", 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold,
            top, new Vector2(0f, -10f), new Vector2(260f, 24f));
        var memberTexts = new Text[MemberCount];
        for (var i = 0; i < MemberCount; i++)
        {
            memberTexts[i] = CreateText($"Member{i + 1}", box, "", 11, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal,
                top, new Vector2(0f, -44f - i * 20f), new Vector2(240f, 18f));
        }

        var nextWaveButton = CreateButton("NextWaveButton", box, "다음 웨이브", 13, new Vector2(0.5f, 0f), new Vector2(0f, 12f),
            new Vector2(150f, 30f));
        var view = root.gameObject.AddComponent<UI_PrepView>();
        SetReference(view, "titleText", title);
        SetReferenceArray(view, "memberTexts", memberTexts);
        SetReference(view, "nextWaveButton", nextWaveButton);
    }

    private static void CreateResultView(Transform parent)
    {
        var root = CreateStretch("ResultView", parent);
        var box = CreateRect("Box", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(260f, 210f));
        box.gameObject.AddComponent<Image>().color = PanelColor;
        var top = new Vector2(0.5f, 1f);
        var outcome = CreateText("Outcome", box, "승리", 24, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold,
            top, new Vector2(0f, -10f), new Vector2(240f, 32f));
        var waves = CreateText("Waves", box, "", 12, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal,
            top, new Vector2(0f, -54f), new Vector2(240f, 18f));
        var kills = CreateText("Kills", box, "", 12, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal,
            top, new Vector2(0f, -76f), new Vector2(240f, 18f));
        var time = CreateText("Time", box, "", 12, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal,
            top, new Vector2(0f, -98f), new Vector2(240f, 18f));
        var toLobbyButton = CreateButton("ToLobbyButton", box, "로비로", 13, new Vector2(0.5f, 0f), new Vector2(0f, 12f),
            new Vector2(140f, 30f));
        var view = root.gameObject.AddComponent<UI_ResultView>();
        SetReference(view, "outcomeText", outcome);
        SetReference(view, "wavesText", waves);
        SetReference(view, "killsText", kills);
        SetReference(view, "timeText", time);
        SetReference(view, "toLobbyButton", toLobbyButton);
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    private static RectTransform CreateStretch(string name, Transform parent)
    {
        var rect = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // anchor 한 점에 pivot을 같이 둔다
    private static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment, Color color,
        FontStyle fontStyle, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        var rect = CreateRect(name, parent, anchor, anchor, anchor, anchoredPosition, size);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = _font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Text CreateLayoutText(string name, Transform parent, int fontSize, FontStyle fontStyle, Color color, float height)
    {
        var text = CreateText(name, parent, "", fontSize, TextAnchor.MiddleCenter, color, fontStyle, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private static Image CreateLayoutImage(string name, Transform parent, float height)
    {
        var rect = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return rect.gameObject.AddComponent<Image>();
    }

    private static Button CreateButton(string name, Transform parent, string label, int fontSize, Vector2 anchor,
        Vector2 anchoredPosition, Vector2 size)
    {
        var rect = CreateRect(name, parent, anchor, anchor, anchor, anchoredPosition, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = ButtonColor;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var text = CreateText("Label", rect, label, fontSize, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(text.rectTransform);
        return button;
    }

    private static void SetReference(Object target, string field, Object value)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(field).objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReferenceArray(Object target, string field, IReadOnlyList<Object> values)
    {
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(field);
        property.arraySize = values.Count;
        for (var i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}

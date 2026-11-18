using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Replay after background migration; touches Canvas descendants only.
public static class MainSceneHudUiMigration
{
    public const string EmphasisFontPath = "Assets/Game/03.Resources/Font/NotoSansKR-Regular.ttf";
    public const string BodyFontPath = "Assets/Game/03.Resources/Font/NotoSansKR-Regular.ttf";
    private const string LightFontPath = "Assets/Game/03.Resources/Font/NotoSansKR-Light.ttf";
    private const string ScenePath = "Assets/Game/01.Scene/Main.unity";
    private static readonly Color White = Hex("ececec");
    private static readonly Color Muted = Hex("bebbba");

    [MenuItem("DeadSurvivor/Art/Apply HD2D UI to Main")]
    public static void Apply()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("HD2D UI migration requires Edit Mode");
        }
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                ApplyToCanvas(canvas);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ApplyToCanvas(Canvas canvas)
    {
        ApplyLobby(canvas.transform);
        ApplyHudTop(canvas.transform);
        ApplyHudBottom(canvas.transform);
        ApplyPrep(canvas.transform);
        ApplyBag(canvas.transform);
        ApplyResult(canvas.transform);
    }

    private static void ApplyLobby(Transform canvas)
    {
        var root = Find(canvas, "LobbyView");
        TextStyle(root);
        var title = root.Find("Title").GetComponent<Text>();
        title.fontSize = 16;
        Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(300, 24));
        Picture("TitleLine", root, "U5_divider_line", new Vector2(0.5f, 1), new Vector2(0, -38), new Vector2(300, 2)).color = Hex("707070");
        var cards = root.Find("Cards");
        Place((RectTransform)cards, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(460, 220));
        foreach (var view in root.GetComponentsInChildren<UI_CharacterCardView>(true))
        {
            var card = view.transform;
            Art(card.GetComponent<Image>(), "U1_panel_menu");
            Art(card.Find("Color").GetComponent<Image>(), "U8_portrait_L_mask");
            var so = new SerializedObject(view);
            var slot = (LayoutElement)so.FindProperty("portraitSlot").objectReferenceValue;
            slot.preferredHeight = 55;
            var window = (RectTransform)so.FindProperty("portraitWindow").objectReferenceValue;
            Portrait(window, "L", 52);
            var area = so.FindProperty("portraitArea");
            area.rectIntValue = new RectInt(17, 15, 34, 34);
            so.ApplyModifiedPropertiesWithoutUndo();
            card.Find("Name").GetComponent<Text>().fontSize = 12;
            card.Find("Role").GetComponent<Text>().color = Muted;
            var line = Picture("Divider", card, "U5_divider_line", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 3));
            var layout = line.GetComponent<LayoutElement>() ?? line.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 3;
            line.transform.SetSiblingIndex(card.Find("Role").GetSiblingIndex() + 1);
            Picture("Knot", line.transform, "U5_divider_knot", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
            RowLabel(card.Find("Hp").GetComponent<Text>(), "HP");
            RowLabel(card.Find("Atk").GetComponent<Text>(), "공격");
            RowLabel(card.Find("Def").GetComponent<Text>(), "방어");
            RowLabel(card.Find("Item").GetComponent<Text>(), "장비");
        }
        var depart = root.Find("DepartButton").GetComponent<Button>();
        ButtonArt(depart, true);
        Place((RectTransform)depart.transform, new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(140, 44));
    }
    private static void ApplyHudTop(Transform canvas)
    {
        var root = Find(canvas, "BattleHudView");
        var top = root.Find("TopBar");
        TextStyle(top);
        Art(top.GetComponent<Image>(), "U7_bar_fill");
        top.GetComponent<Image>().color = Color.clear;
        ((RectTransform)top).sizeDelta = new Vector2(0, 52);
        Band(top, "U6_band_top", true, false, false);
        Place((RectTransform)top.Find("Wave"), new Vector2(0, 1), new Vector2(28, -35), new Vector2(36, 14));
        Picture("WaveLine", top, "U10_wave_line", new Vector2(0, 1), new Vector2(95, -18), new Vector2(162, 4));
        var nodes = new Image[10];
        for (var i = 0; i < nodes.Length; i++)
        {
            nodes[i] = Picture("Node" + i, top, "U10_wave_node_remaining", new Vector2(0, 1), new Vector2(14 + i * 18, -18), new Vector2(8, 8));
        }
        var view = root.GetComponent<UI_BattleHudView>();
        Refs(view, "waveNodes", nodes);
        Ref(view, "passedNode", Sprite("U10_wave_node_passed"));
        Ref(view, "currentNode", Sprite("U10_wave_node_current"));
        Ref(view, "remainingNode", Sprite("U10_wave_node_remaining"));
        Place((RectTransform)top.Find("Kills"), new Vector2(0, 1), new Vector2(225, -17), new Vector2(70, 20));
        Place((RectTransform)top.Find("Threat"), new Vector2(0, 1), new Vector2(225, -33), new Vector2(70, 16));
        top.Find("Kills").GetComponent<Text>().color = Muted;
        top.Find("Threat").GetComponent<Text>().color = Muted;
        Place((RectTransform)top.Find("Remaining"), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(60, 24));
        top.Find("Remaining").GetComponent<Text>().fontSize = 14;
        var burning = top.Find("Burning");
        Place((RectTransform)burning, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(80, 16));
        burning.GetComponent<Text>().fontSize = 10;
        burning.GetComponent<Text>().color = Hex("f7ea81");
        var speed = top.Find("SpeedButton").GetComponent<Button>();
        IconButton(speed, "U12_icon_speed", new Vector2(-78, -26));
        var label = speed.GetComponentInChildren<Text>();
        Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(12, 0), new Vector2(24, 20));
        var pause = top.Find("PauseButton").GetComponent<Button>();
        var icon = IconButton(pause, "U12_icon_pause", new Vector2(-26, -26));
        pause.GetComponentInChildren<Text>().text = "";
        Ref(view, "pauseIcon", icon);
        Ref(view, "pauseSprite", Sprite("U12_icon_pause"));
        Ref(view, "resumeSprite", Sprite("U12_icon_resume"));
    }
    private static void ApplyHudBottom(Transform canvas)
    {
        var root = Find(canvas, "BattleHudView");
        var bottom = root.Find("BottomPanel");
        TextStyle(bottom);
        Art(bottom.GetComponent<Image>(), "U7_bar_fill");
        bottom.GetComponent<Image>().color = Color.clear;
        ((RectTransform)bottom).sizeDelta = new Vector2(0, 85);
        var band = Band(bottom, "U6_band_top", false, true, true);
        var ignore = band.GetComponent<LayoutElement>() ?? band.gameObject.AddComponent<LayoutElement>();
        ignore.ignoreLayout = true;
        foreach (var member in bottom.GetComponentsInChildren<UI_MemberStatusView>(true))
        {
            var tr = member.transform;
            var old = tr.GetComponent<UI_ArtSurface>();
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }
            var background = tr.GetComponent<Image>();
            if (background != null)
            {
                Object.DestroyImmediate(background);
            }
            var window = (RectTransform)tr.Find("PortraitWindow");
            Place(window, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(22, 22));
            Portrait(window, "S", 34);
            Ref(member, "portraitFrame", tr.Find("Frame").GetComponent<Image>());
            Ref(member, "alivePortraitFrame", Sprite("U8_portrait_S_normal"));
            Ref(member, "downedPortraitFrame", Sprite("U8_portrait_S_dead"));
            var so = new SerializedObject(member);
            so.FindProperty("portraitArea").rectIntValue = new RectInt(23, 21, 22, 22);
            so.FindProperty("aliveNameColor").colorValue = White;
            so.FindProperty("downedNameColor").colorValue = Hex("a8acaf");
            so.ApplyModifiedPropertiesWithoutUndo();
            var name = tr.Find("Name").GetComponent<Text>();
            StretchRow(name.rectTransform, 44, -3, 3, 12);
            name.fontSize = 10;
            var hp = tr.Find("HpText").GetComponent<Text>();
            StretchRow(hp.rectTransform, 44, -17, 3, 12);
            hp.alignment = TextAnchor.MiddleLeft;
            hp.fontSize = 10;
            var bar = (RectTransform)tr.Find("HpBar");
            StretchRow(bar, 44, -31, 3, 3);
            Art(bar.GetComponent<Image>(), "U7_bar_track");
            foreach (var image in bar.GetComponentsInChildren<Image>(true))
            {
                if (image.transform == bar)
                {
                    continue;
                }
                Art(image, "U7_bar_fill");
                image.color = image.name == "HpLoss" ? Hex("a4534a") : Hex("568757");
            }
            var items = (RectTransform)tr.Find("Items");
            StretchRow(items, 44, -37, 3, 48);
            var grid = items.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(16, 10);
            grid.spacing = Vector2.one;
            foreach (var item in member.GetComponentsInChildren<UI_BattleItemView>(true))
            {
                var itemSo = new SerializedObject(item);
                var bg = (Image)itemSo.FindProperty("background").objectReferenceValue;
                Art(bg, "U9_cell_filled");
                Ref(item, "normalBackground", Sprite("U9_cell_filled"));
                Ref(item, "disabledBackground", Sprite("U9_cell_empty"));
                itemSo.Update();
                itemSo.FindProperty("readyBarColor").colorValue = Hex("f7ea81");
                itemSo.FindProperty("coolingBarColor").colorValue = Hex("5b7984");
                itemSo.ApplyModifiedPropertiesWithoutUndo();
                foreach (var image in item.GetComponentsInChildren<Image>(true))
                {
                    if (image == bg || image.name == "Icon")
                    {
                        continue;
                    }
                    Art(image, image.name == "Fill" ? "U7_bar_fill" : "U7_bar_track");
                }
                var cooldown = (RectTransform)((GameObject)itemSo.FindProperty("cooldownBar").objectReferenceValue).transform;
                cooldown.sizeDelta = new Vector2(cooldown.sizeDelta.x, 2);
                var icon = (Image)itemSo.FindProperty("iconImage").objectReferenceValue;
                Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(8, 8));
                var label = (Text)itemSo.FindProperty("labelText").objectReferenceValue;
                var state = (Text)itemSo.FindProperty("stateText").objectReferenceValue;
                label.gameObject.SetActive(false);
                state.gameObject.SetActive(false);
            }
        }
        var joy = Find(canvas, "JoystickArea");
        Art(joy.GetComponent<Image>(), "U7_bar_track");
        joy.GetComponent<Image>().color = Color.clear;
        Art(joy.Find("Ring").GetComponent<Image>(), "U13_joystick_base");
        Art(joy.Find("Handle").GetComponent<Image>(), "U13_joystick_knob");
    }

    private static void StretchRow(RectTransform rect, float left, float y, float right, float height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1);
        rect.offsetMin = new Vector2(left, y - height);
        rect.offsetMax = new Vector2(-right, y);
    }
    private static void ApplyPrep(Transform canvas)
    {
        var root = Find(canvas, "PrepView");
        TextStyle(root);
        var dim = root.Find("Dim").GetComponent<Image>();
        var alpha = dim.color.a;
        Art(dim, "U7_bar_track");
        dim.color = new Color(1, 1, 1, alpha);
        var box = root.Find("Box");
        Art(box.GetComponent<Image>(), "U7_bar_fill");
        box.GetComponent<Image>().color = Color.clear;
        ((RectTransform)box).sizeDelta = new Vector2(0, 90);
        Band(box, "U6_band_bottom", true, false, true);
        var title = box.Find("Title").GetComponent<Text>();
        title.fontSize = 14;
        Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(210, 22));
        var next = box.Find("NextWaveButton").GetComponent<Button>();
        ButtonArt(next, true);
        Place((RectTransform)next.transform, new Vector2(1, 1), new Vector2(-64, -24), new Vector2(110, 44));
        var fills = new RectTransform[4];
        for (var i = 0; i < 4; i++)
        {
            var x = -168 + i * 112;
            var member = box.Find("Member" + (i + 1)).GetComponent<Text>();
            member.fontSize = 9;
            Place(member.rectTransform, new Vector2(0.5f, 1), new Vector2(x - 22, -58), new Vector2(68, 28));
            var track = Picture("MemberHp" + i, box, "U7_bar_track", new Vector2(0.5f, 1), new Vector2(x - 22, -79), new Vector2(68, 3));
            var fill = Picture("Fill", track.transform, "U7_bar_fill", Vector2.zero, Vector2.zero, Vector2.zero);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            fill.color = Hex("568757");
            fills[i] = fill.rectTransform;
            var revive = box.Find("Revive" + (i + 1)).GetComponent<Button>();
            ButtonArt(revive, false, true);
            Place((RectTransform)revive.transform, new Vector2(0.5f, 1), new Vector2(x + 36, -68), new Vector2(44, 44));
        }
        Refs(root.GetComponent<UI_PrepView>(), "memberHpFills", fills);
        for (var i = 0; i < 4; i++)
        {
            var bag = (RectTransform)Find(root, "BagView" + i);
            bag.anchoredPosition = new Vector2(-168 + i * 112, -96);
        }
        foreach (var key in new[] { "RotateButton", "CancelButton" })
        {
            var button = Find(root, key).GetComponent<Button>();
            ButtonArt(button);
            Place((RectTransform)button.transform, new Vector2(0.5f, 0), new Vector2(0, key == "RotateButton" ? 166 : 116), new Vector2(100, 44));
            var icon = Picture("Symbol", button.transform, key == "RotateButton" ? "U12_icon_rotate" : "U12_icon_close", new Vector2(0.5f, 0.5f), new Vector2(-35, 0), new Vector2(12, 12));
            button.GetComponentInChildren<Text>().fontSize = 10;
        }
        var formation = root.GetComponentInChildren<UI_FormationView>(true);
        var panel = (RectTransform)formation.transform;
        Place(panel, new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(460, 60));
        Art(panel.GetComponent<Image>(), "U1_panel_menu");
        var markers = new Image[3];
        for (var i = 0; i < formation.FormationButtons.Count; i++)
        {
            var button = formation.FormationButtons[i];
            ButtonArt(button, false, true);
            Place((RectTransform)button.transform, new Vector2(0, 0.5f), new Vector2(48 + i * 88, 0), new Vector2(82, 44));
            markers[i] = Picture("Selection", button.transform, "U11_select_marker", new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(6, 6));
            button.GetComponentInChildren<Text>().fontSize = 10;
        }
        Refs(formation, "selectionMarkers", markers);
        Ref(formation, "normalRow", Sprite("U4_row_normal"));
        Ref(formation, "selectedRow", Sprite("U4_row_selected"));
        Ref(formation, "aliveFrame", Sprite("U8_portrait_S_normal"));
        Ref(formation, "deadFrame", Sprite("U8_portrait_S_dead"));
        var preview = panel.Find("Preview");
        Place((RectTransform)preview, new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(160, 56));
        var so = new SerializedObject(formation);
        so.FindProperty("previewScale").floatValue = 0.4f;
        so.ApplyModifiedPropertiesWithoutUndo();
        var memberMarkers = so.FindProperty("memberMarkers");
        var faces = new Image[memberMarkers.arraySize];
        for (var i = 0; i < faces.Length; i++)
        {
            var marker = (RectTransform)memberMarkers.GetArrayElementAtIndex(i).objectReferenceValue;
            marker.sizeDelta = new Vector2(28, 28);
            Art(marker.GetComponent<Image>(), "U8_portrait_S_normal");
            foreach (var text in marker.GetComponentsInChildren<Text>(true))
            {
                text.text = "";
                text.gameObject.SetActive(false);
            }
            var mask = Picture("PortraitMask", marker, "U8_portrait_S_mask", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
            (mask.GetComponent<Mask>() ?? mask.gameObject.AddComponent<Mask>()).showMaskGraphic = false;
            faces[i] = Picture("Face", mask.transform, "U7_bar_fill", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 96));
        }
        Refs(formation, "portraitFaces", faces);
    }
    private static void ApplyBag(Transform canvas)
    {
        var root = Find(canvas, "PrepView");
        foreach (var bag in root.GetComponentsInChildren<UI_BagView>(true))
        {
            foreach (var image in bag.GetComponentsInChildren<Image>(true))
            {
                Art(image, image.name == "Border" ? "U9_cell_grade" : "U9_cell_empty");
            }
            foreach (var cell in bag.GetComponentsInChildren<UI_BagCellView>(true))
            {
                Refs(cell, "cellArt", new Object[] { Sprite("U9_cell_empty"), Sprite("U9_cell_filled"), Sprite("U9_cell_selected"), Sprite("U9_cell_droppable"), Sprite("U9_cell_blocked") });
            }
        }
        var stash = root.GetComponentInChildren<UI_StashView>(true);
        Art(stash.GetComponent<Image>(), "U1_panel_menu");
        var stashGrid = stash.Content.GetComponent<GridLayoutGroup>();
        stashGrid.cellSize = new Vector2(56, 44);
        stashGrid.spacing = new Vector2(2, 2);
        ((RectTransform)stash.transform).sizeDelta = new Vector2(180, 110);
        Picture("TitleLine", stash.transform, "U5_divider_line", new Vector2(0.5f, 1), new Vector2(0, -19), new Vector2(164, 2));
        foreach (var item in stash.GetComponentsInChildren<UI_StashItemView>(true))
        {
            Ref(item, "normalArt", Sprite("U9_cell_filled"));
            Ref(item, "pickedArt", Sprite("U9_cell_selected"));
            foreach (var image in item.GetComponentsInChildren<Image>(true))
            {
                Art(image, image.name == "Border" ? "U9_cell_grade" : image.name == "New" ? "U12_icon_new" : image.name == "Background" ? "U9_cell_filled" : "U7_bar_fill");
                if (image.name == "New")
                {
                    image.color = Hex("f7ea81");
                    foreach (var text in image.GetComponentsInChildren<Text>(true))
                    {
                        text.gameObject.SetActive(false);
                    }
                }
            }
            var so = new SerializedObject(item);
            so.FindProperty("shapeOnColor").colorValue = White;
            so.FindProperty("shapeOffColor").colorValue = new Color(White.r, White.g, White.b, 0.15f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var info = root.GetComponentInChildren<UI_ItemInfoView>(true);
        foreach (var image in info.GetComponentsInChildren<Image>(true))
        {
            Art(image, image.name == "PreviewScroll" ? "U9_cell_empty" : "U1_panel_menu");
        }
        ((RectTransform)info.transform).sizeDelta = new Vector2(180, 110);
        foreach (var button in info.GetComponentsInChildren<Button>(true))
        {
            ButtonArt(button, false, true);
            Place((RectTransform)button.transform, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(80, 44));
        }
        TextStyle(root);
        // Reapply token exceptions after the shared font pass.
        root.Find("Box/Title").GetComponent<Text>().fontSize = 14;
    }
    private static void ApplyResult(Transform canvas) { }

    private static void Refs(Object owner, string field, Object[] values)
    {
        var so = new SerializedObject(owner);
        var property = so.FindProperty(field);
        property.arraySize = values.Length;
        for (var i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Image IconButton(Button button, string key, Vector2 pos)
    {
        Art(button.GetComponent<Image>(), "U7_bar_fill");
        button.GetComponent<Image>().color = Color.clear;
        Place((RectTransform)button.transform, new Vector2(1, 1), pos, new Vector2(44, 44));
        var icon = Picture("Symbol", button.transform, key, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
        button.targetGraphic = icon;
        button.transition = Selectable.Transition.ColorTint;
        return icon;
    }

    private static Image Band(Transform parent, string key, bool top, bool bottom, bool flip)
    {
        var band = Picture("Backdrop", parent, key, Vector2.zero, Vector2.zero, Vector2.zero);
        band.rectTransform.anchorMin = Vector2.zero;
        band.rectTransform.anchorMax = Vector2.one;
        band.rectTransform.offsetMin = band.rectTransform.offsetMax = Vector2.zero;
        band.transform.SetAsFirstSibling();
        band.transform.localScale = new Vector3(1, flip ? -1 : 1, 1);
        var bleed = band.GetComponent<UI_SafeAreaBleed>() ?? band.gameObject.AddComponent<UI_SafeAreaBleed>();
        var so = new SerializedObject(bleed);
        so.FindProperty("left").boolValue = true;
        so.FindProperty("right").boolValue = true;
        so.FindProperty("top").boolValue = top;
        so.FindProperty("bottom").boolValue = bottom;
        so.ApplyModifiedPropertiesWithoutUndo();
        return band;
    }

    private static void Portrait(RectTransform window, string size, float dimension)
    {
        var rectMask = window.GetComponent<RectMask2D>();
        if (rectMask != null)
        {
            Object.DestroyImmediate(rectMask);
        }
        var image = window.GetComponent<Image>() ?? window.gameObject.AddComponent<Image>();
        Art(image, "U8_portrait_" + size + "_mask");
        image.raycastTarget = false;
        var mask = window.GetComponent<Mask>() ?? window.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        var frame = Picture("Frame", window.parent, "U8_portrait_" + size + "_normal", window.anchorMin, window.anchoredPosition, new Vector2(dimension, dimension));
        frame.rectTransform.anchorMax = window.anchorMax;
        frame.rectTransform.pivot = window.pivot;
        var layout = frame.GetComponent<LayoutElement>() ?? frame.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
    }

    private static void RowLabel(Text value, string label)
    {
        value.alignment = TextAnchor.MiddleRight;
        var rect = Rect("Caption", value.transform);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = rect.GetComponent<Text>() ?? rect.gameObject.AddComponent<Text>();
        text.font = AssetDatabase.LoadAssetAtPath<Font>(LightFontPath);
        text.fontStyle = FontStyle.Normal;
        text.fontSize = 9;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Muted;
        text.text = label + " :";
        text.raycastTarget = false;
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }
        throw new InvalidOperationException("Missing UI object: " + name);
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color);
        return color;
    }

    private static Sprite Sprite(string key) => AssetDatabase.LoadAssetAtPath<Sprite>(HudUiImport.Folder + "/" + key + ".png");

    private static void Art(Image image, string key)
    {
        var old = image.GetComponent<UI_ArtSurface>();
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }
        image.sprite = Sprite(key);
        image.type = image.sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 3;
        image.color = Color.white;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var existing = parent.Find(name);
        if (existing != null)
        {
            return (RectTransform)existing;
        }
        var created = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        created.SetParent(parent, false);
        return created;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Image Picture(string name, Transform parent, string key, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rect = Rect(name, parent);
        Place(rect, anchor, pos, size);
        var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
        Art(image, key);
        image.raycastTarget = false;
        return image;
    }

    private static void TextStyle(Transform root)
    {
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            text.font = AssetDatabase.LoadAssetAtPath<Font>(text.name == "Remaining" || text.name == "Outcome" || text.name == "Title" ? EmphasisFontPath : BodyFontPath);
            text.resizeTextForBestFit = false;
            text.fontStyle = FontStyle.Normal;
            text.fontSize = Mathf.Max(9, text.fontSize);
            text.color = White;
        }
    }

    private static void Ref(Object owner, string field, Object value)
    {
        var so = new SerializedObject(owner);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ButtonArt(Button button, bool primary = false, bool row = false)
    {
        var root = (RectTransform)button.transform;
        root.sizeDelta = new Vector2(Mathf.Max(44, root.sizeDelta.x), Mathf.Max(44, root.sizeDelta.y));
        var hit = button.GetComponent<Image>();
        Art(hit, "U7_bar_fill");
        hit.color = Color.clear;
        var normal = row ? "U4_row_normal" : primary ? "U3_button_highlight" : "U3_button_normal";
        var plate = Picture("Plate", root, normal, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(root.sizeDelta.x, row ? 16 : 86f / 3));
        plate.transform.SetAsFirstSibling();
        button.targetGraphic = plate;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = Sprite(row ? "U4_row_selected" : "U3_button_highlight"),
            pressedSprite = Sprite(row ? "U4_row_selected" : "U3_button_pressed"),
            selectedSprite = Sprite(row ? "U4_row_selected" : "U3_button_highlight"),
            disabledSprite = Sprite(row ? "U4_row_disabled" : "U3_button_disabled")
        };
    }
}

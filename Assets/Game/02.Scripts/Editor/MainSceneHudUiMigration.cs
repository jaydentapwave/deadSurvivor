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
    private static void ApplyHudTop(Transform canvas) { }
    private static void ApplyHudBottom(Transform canvas) { }
    private static void ApplyPrep(Transform canvas) { }
    private static void ApplyBag(Transform canvas) { }
    private static void ApplyResult(Transform canvas) { }

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

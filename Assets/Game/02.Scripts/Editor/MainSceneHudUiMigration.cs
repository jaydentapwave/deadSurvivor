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

    private static void ApplyLobby(Transform canvas) { }
    private static void ApplyHudTop(Transform canvas) { }
    private static void ApplyHudBottom(Transform canvas) { }
    private static void ApplyPrep(Transform canvas) { }
    private static void ApplyBag(Transform canvas) { }
    private static void ApplyResult(Transform canvas) { }

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

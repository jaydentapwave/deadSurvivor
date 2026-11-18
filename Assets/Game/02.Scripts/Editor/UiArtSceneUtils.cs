using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 기존 Main 장면에만 그림을 연결한다. 장면 빌더 재실행으로 다른 갈래의 변경을 지우지 않는다.
public static class UiArtSceneUtils
{
    private const string ArtPath = "Assets/Game/03.Resources/Art/AIGenerated/UI/";

    [MenuItem("DeadSurvivor/Art/Apply UI Art to Main")]
    public static void ApplyToMain()
    {
        throw new InvalidOperationException("HD-2D UI로 대체됨: DeadSurvivor/Art/Apply HD2D UI to Main을 사용하세요.");
    }

    public static void ApplyToCanvas(Canvas canvas)
    {
        foreach (var image in canvas.GetComponentsInChildren<Image>(true))
        {
            var key = GetKey(image);
            if (key == null)
            {
                continue;
            }

            Undo.RecordObject(image, "Connect UI art");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + key + ".png");
            if (sprite == null)
            {
                throw new InvalidOperationException($"UI 그림 없음: {key}");
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            var isTinted = image.name == "Fill" || image.name == "HpLoss" || image.name == "Border";
            if (!isTinted)
            {
                // 입력·딤의 alpha는 유지한다. 가죽에 임시 갈색을 곱하지 않는다.
                image.color = new Color(1f, 1f, 1f, image.name == "TopBar" ? 1f : image.color.a);
            }

            var surface = image.GetComponent<UI_ArtSurface>();
            if (surface == null)
            {
                surface = Undo.AddComponent<UI_ArtSurface>(image.gameObject);
            }

            Undo.RecordObject(surface, "Connect UI border");
            surface.Initialize(key, image.name == "Border");
            EditorUtility.SetDirty(surface);
            EditorUtility.SetDirty(image);
        }

        foreach (var member in canvas.GetComponentsInChildren<UI_MemberStatusView>(true))
        {
            ApplyMemberPortrait(member);
        }

        // 양피지는 가방 칸만. 나머지 가죽 위 글자는 흰색이다.
        foreach (var text in canvas.GetComponentsInChildren<Text>(true))
        {
            Undo.RecordObject(text, "UI art text contrast");
            text.color = text.GetComponentInParent<UI_BagCellView>() != null
                ? new Color(0.2f, 0.08f, 0.04f, 1f) : Color.white;
            EditorUtility.SetDirty(text);
        }

        foreach (var item in canvas.GetComponentsInChildren<UI_BattleItemView>(true))
        {
            SetColor(item, "backgroundColor", Color.white);
            SetColor(item, "textColor", Color.white);
            SetColor(item, "firedTextColor", new Color(1f, 0.75f, 0.3f, 1f));
        }

        foreach (var stash in canvas.GetComponentsInChildren<UI_StashView>(true))
        {
            // 180px 판넬의 양쪽 8px 테두리 안에 3열(52×3 + 간격4×2)을 넣는다.
            var viewport = (RectTransform)stash.Content.parent;
            Undo.RecordObject(viewport, "Stash art inset");
            viewport.offsetMin = new Vector2(8f, viewport.offsetMin.y);
            viewport.offsetMax = new Vector2(-8f, viewport.offsetMax.y);
            var grid = stash.Content.GetComponent<GridLayoutGroup>();
            Undo.RecordObject(grid, "Stash card width");
            grid.cellSize = new Vector2(52f, grid.cellSize.y);
        }

        foreach (var item in canvas.GetComponentsInChildren<UI_StashItemView>(true))
        {
            SetColor(item, "itemColor", Color.white);
            var name = (RectTransform)item.transform.Find("Name");
            Undo.RecordObject(name, "Stash name width");
            name.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 46f);
            var mark = (RectTransform)item.transform.Find("New");
            Undo.RecordObject(mark, "New label padding");
            mark.sizeDelta = new Vector2(18f, 16f);
        }

        foreach (var formation in canvas.GetComponentsInChildren<UI_FormationView>(true))
        {
            SetColor(formation, "normalButtonColor", Color.white);
            foreach (var text in formation.transform.Find("Preview").GetComponentsInChildren<Text>(true))
            {
                // 30px 칩의 양쪽 4px 가죽 테두리 안에 한글 세 글자를 넣는다.
                Undo.RecordObject(text, "Formation marker text inset");
                text.fontSize = 7;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }
    }

    private static void ApplyMemberPortrait(UI_MemberStatusView member)
    {
        var window = member.transform.Find("PortraitWindow") as RectTransform;
        if (window == null)
        {
            var windowObject = new GameObject("PortraitWindow", typeof(RectTransform), typeof(RectMask2D));
            Undo.RegisterCreatedObjectUndo(windowObject, "Member portrait window");
            window = (RectTransform)windowObject.transform;
            window.SetParent(member.transform, false);
            window.anchorMin = window.anchorMax = new Vector2(0f, 0.5f);
            window.pivot = new Vector2(0f, 0.5f);
            window.anchoredPosition = new Vector2(5f, 0f);
            window.sizeDelta = new Vector2(36f, 36f);
            var imageObject = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(imageObject, "Member portrait image");
            imageObject.transform.SetParent(window, false);
            var image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            var serialized = new SerializedObject(member);
            serialized.FindProperty("portraitWindow").objectReferenceValue = window;
            serialized.FindProperty("portraitImage").objectReferenceValue = image;
            serialized.ApplyModifiedProperties();
        }

        // 36px 초상과 3px 간격. 이름·HP는 오른쪽 칸을 나누고 16개 칩은 4열을 유지한다.
        var name = (RectTransform)member.transform.Find("Name");
        var hp = (RectTransform)member.transform.Find("HpText");
        Undo.RecordObjects(new UnityEngine.Object[] { name, hp }, "Member portrait text inset");
        name.anchorMax = new Vector2(0.5f, 1f);
        name.offsetMin = new Vector2(44f, name.offsetMin.y);
        name.offsetMax = new Vector2(20f, name.offsetMax.y);
        hp.anchorMin = new Vector2(0.5f, 1f);
        hp.offsetMin = new Vector2(20f, hp.offsetMin.y);
        hp.offsetMax = new Vector2(-3f, hp.offsetMax.y);
        foreach (var childName in new[] { "HpBar", "Items" })
        {
            var rect = (RectTransform)member.transform.Find(childName);
            Undo.RecordObject(rect, "Member portrait content inset");
            rect.offsetMin = new Vector2(44f, rect.offsetMin.y);
        }

        var grid = member.transform.Find("Items").GetComponent<GridLayoutGroup>();
        Undo.RecordObject(grid, "Member portrait chip width");
        grid.cellSize = new Vector2(16f, grid.cellSize.y);
    }

    private static string GetKey(Image image)
    {
        var name = image.name;
        // 캐릭터 초상과 아이템은 기존 Presenter가 그림을 결정한다.
        if (name == "Icon" || name == "Image" && (image.GetComponentInParent<UI_CharacterCardView>() != null
            || image.GetComponentInParent<UI_MemberStatusView>() != null))
        {
            return null;
        }

        if (image.GetComponent<Button>() != null)
        {
            return "button";
        }

        if (name == "Ring")
        {
            return "joystick_base";
        }

        if (name == "Handle")
        {
            return "joystick_knob";
        }

        if (name == "HpBar")
        {
            return "hp_bar_bg";
        }

        if (name == "CooldownBar")
        {
            return "cooldown_bg";
        }

        if (name == "Fill" || name == "HpLoss")
        {
            return image.transform.parent.name == "CooldownBar" ? "cooldown_fill" : "hp_bar_fill";
        }

        if (image.GetComponentInParent<UI_BagCellView>() != null)
        {
            return "bag_cell";
        }

        // 소형 표지·등급 테두리·입력용 투명 Image에는 버튼 그림. 패널 8px 모서리를 찌그러뜨리지 않는다.
        if (name == "Border" || name == "Color" || name == "New" || name == "JoystickArea"
            || image.GetComponentInParent<UI_BattleItemView>() != null
            || image.GetComponentInParent<UI_StashItemView>() != null
            || image.transform.parent.name == "Preview")
        {
            return "button";
        }

        return "panel";
    }

    private static void SetColor(UnityEngine.Object target, string name, Color color)
    {
        Undo.RecordObject(target, "UI art tint");
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).colorValue = color;
        serialized.ApplyModifiedProperties();
    }
}

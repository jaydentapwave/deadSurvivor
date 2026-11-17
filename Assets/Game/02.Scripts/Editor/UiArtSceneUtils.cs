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
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Game/01.Scene/Main.unity")
        {
            throw new InvalidOperationException("Main.unity를 연 뒤 UI 그림 연결 메뉴를 실행하세요.");
        }

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
                image.color = new Color(1f, 1f, 1f, image.color.a);
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

        foreach (var item in canvas.GetComponentsInChildren<UI_StashItemView>(true))
        {
            SetColor(item, "itemColor", Color.white);
        }

        foreach (var formation in canvas.GetComponentsInChildren<UI_FormationView>(true))
        {
            SetColor(formation, "normalButtonColor", Color.white);
        }
    }

    private static string GetKey(Image image)
    {
        var name = image.name;
        // 캐릭터 초상과 아이템은 기존 Presenter가 그림을 결정한다.
        if (name == "Icon" || name == "Image" && image.GetComponentInParent<UI_CharacterCardView>() != null)
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

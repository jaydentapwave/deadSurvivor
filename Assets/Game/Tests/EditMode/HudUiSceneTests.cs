using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class HudUiSceneTests
{
    [Test]
    public void LobbyMeetsTextTouchAndArtContract()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var lobby = Array.Find(scene.GetRootGameObjects(), r => r.GetComponentInChildren<UI_LobbyView>(true) != null).GetComponentInChildren<UI_LobbyView>(true);
        AssertScreen(lobby.transform);
        foreach (var card in lobby.GetComponentsInChildren<UI_CharacterCardView>(true))
        {
            Assert.IsNotNull(card.GetComponentInChildren<Mask>(true));
        }
    }

    [Test]
    public void TopHudMeetsTouchAndArtContract()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var hud = Array.Find(scene.GetRootGameObjects(), r => r.GetComponentInChildren<UI_BattleHudView>(true) != null).GetComponentInChildren<UI_BattleHudView>(true);
        AssertScreen(hud.transform.Find("TopBar"));
    }

    [Test]
    public void BottomHudMeetsArtContract()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var hud = Array.Find(scene.GetRootGameObjects(), r => r.GetComponentInChildren<UI_BattleHudView>(true) != null).GetComponentInChildren<UI_BattleHudView>(true);
        AssertScreen(hud.transform.Find("BottomPanel"));
    }

    [Test]
    public void PrepHeaderAndFormationMeetTouchAndArtContract()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var prep = Array.Find(scene.GetRootGameObjects(), r => r.GetComponentInChildren<UI_PrepView>(true) != null).GetComponentInChildren<UI_PrepView>(true);
        AssertScreen(prep.transform.Find("Box"));
        AssertScreen(prep.GetComponentInChildren<UI_FormationView>(true).transform);
    }

    [Test]
    public void EntirePrepMeetsArtTextAndTouchContract()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var prep = Array.Find(scene.GetRootGameObjects(), r => r.GetComponentInChildren<UI_PrepView>(true) != null).GetComponentInChildren<UI_PrepView>(true);
        AssertScreen(prep.transform);
    }

    [Test]
    public void ResultMeetsArtTextAndTouchContract()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var result = Array.Find(scene.GetRootGameObjects(), r => r.GetComponentInChildren<UI_ResultView>(true) != null).GetComponentInChildren<UI_ResultView>(true);
        AssertScreen(result.transform);
    }

    [Test]
    public void TabletTopLabelsDoNotOverlapTimer()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var hud = Array.Find(scene.GetRootGameObjects(), r => r.GetComponentInChildren<UI_BattleHudView>(true) != null).GetComponentInChildren<UI_BattleHudView>(true);
        var clone = UnityEngine.Object.Instantiate(hud.transform.Find("TopBar").gameObject);
        var rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.sizeDelta = new Vector2(480, 52);
        var timer = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, clone.transform.Find("Remaining"));
        foreach (var name in new[] { "Wave", "Kills", "Threat" })
        {
            var label = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, clone.transform.Find(name));
            Assert.Less(label.max.x, timer.min.x, name);
        }
        UnityEngine.Object.DestroyImmediate(clone);
    }

    [Test]
    public void WorldBarLegacyAliasesResolveToFramelessHighResolutionArt()
    {
        var service = new SpriteAtlasService(new ResourceService());
        foreach (var key in new[] { "hp_bar_bg", "hp_bar_fill", "U7_bar_track", "U7_bar_fill" })
        {
            var sprite = service.GetUi(key);
            Assert.AreEqual(Vector4.zero, sprite.border, key);
            Assert.AreEqual(100, sprite.pixelsPerUnit, key);
            StringAssert.StartsWith("U7_", sprite.name);
            UnityEngine.Object.DestroyImmediate(sprite);
        }
    }

    [TestCase(3f)]
    [TestCase(3.25f)]
    [TestCase(4.2666667f)]
    public void BattleIconsUseIntegralScreenTexelsAndFitTheirCells(float scale)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var hud = Array.Find(scene.GetRootGameObjects(), r => r.GetComponentInChildren<UI_BattleHudView>(true) != null).GetComponentInChildren<UI_BattleHudView>(true);
        var canvasObject = new GameObject("IconTest", typeof(Canvas));
        canvasObject.GetComponent<Canvas>().scaleFactor = scale;
        var item = UnityEngine.Object.Instantiate(hud.MemberStatusViews[0].BattleItemViews[0], canvasObject.transform);
        ((RectTransform)item.transform).sizeDelta = new Vector2(16, 18);
        var icon = (Image)new SerializedObject(item).FindProperty("iconImage").objectReferenceValue;
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Game/03.Resources/Art/AIGenerated/Items" }))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                item.UpdateUI("item", sprite, BattleItemState.Ready, 0, 0);
                var pixels = icon.rectTransform.sizeDelta * scale;
                var factor = pixels.x / sprite.rect.width;
                Assert.AreEqual(Mathf.Round(factor), factor, 0.0001f, sprite.name);
                Assert.GreaterOrEqual(factor, 1, sprite.name);
                Assert.LessOrEqual(icon.rectTransform.sizeDelta.x, 16.0001f, sprite.name);
                Assert.LessOrEqual(icon.rectTransform.sizeDelta.y, 16.0001f, sprite.name);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    [TestCase(BattleItemState.Ready)]
    [TestCase(BattleItemState.Disabled)]
    public void BattleItemWithoutArtShowsClippedName(BattleItemState state)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var source = Array.Find(scene.GetRootGameObjects(), r => r.GetComponentInChildren<UI_BattleHudView>(true) != null).GetComponentInChildren<UI_BattleHudView>(true).MemberStatusViews[0].BattleItemViews[0];
        var canvas = new GameObject("FallbackNameTest", typeof(Canvas));
        var clone = UnityEngine.Object.Instantiate(source, canvas.transform);
        try
        {
            clone.UpdateUI("그림 없는 긴 아이템 이름", null, state, 0, 0);
            var serialized = new SerializedObject(clone);
            var text = (Text)serialized.FindProperty("labelText").objectReferenceValue;
            var icon = (Image)serialized.FindProperty("iconImage").objectReferenceValue;
            Assert.IsTrue(text.gameObject.activeSelf);
            Assert.IsFalse(icon.gameObject.activeSelf);
            Assert.AreEqual("그림 없는 긴 아이템 이름", text.text);
            Assert.AreEqual(9, text.fontSize);
            Assert.AreEqual(FontStyle.Normal, text.fontStyle);
            Assert.AreEqual(HorizontalWrapMode.Wrap, text.horizontalOverflow);
            Assert.AreEqual(VerticalWrapMode.Truncate, text.verticalOverflow);
            Assert.AreEqual(new Vector2(16, 16), text.rectTransform.sizeDelta);
            Assert.AreEqual(state == BattleItemState.Disabled ? new Color(168f / 255, 172f / 255, 175f / 255) : new Color(236f / 255, 236f / 255, 236f / 255), text.color);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/HD2D/UI/U7_bar_fill.png");
            clone.UpdateUI("item", sprite, state, 0, 0);
            Assert.IsFalse(text.gameObject.activeSelf);
            Assert.IsTrue(icon.gameObject.activeSelf);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvas);
        }
    }

    [TestCase(480f, 360f)]
    [TestCase(698f, 340.6f)]
    [TestCase(697.846f, 354f)]
    [TestCase(697.846f, 354.4615f)]
    [TestCase(697.846f, 360f)]
    public void PrepReflowsWithoutOverlappingBagAndStash(float width, float height)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var source = Array.Find(scene.GetRootGameObjects(), r => r.GetComponentInChildren<UI_PrepView>(true) != null).GetComponentInChildren<UI_PrepView>(true);
        var clone = UnityEngine.Object.Instantiate(source);
        var rect = (RectTransform)clone.transform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        try
        {
            clone.RotateButton.interactable = false;
            clone.CancelButton.interactable = false;
            typeof(UI_PrepView).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(clone, null);
            Assert.AreEqual(new Color(168f / 255, 172f / 255, 175f / 255), clone.RotateButton.GetComponentInChildren<Text>(true).color);
            var bag = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, clone.BagViews[0].transform);
            var stash = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, clone.StashView.transform);
            Assert.GreaterOrEqual(bag.min.y + 0.001f, stash.max.y);
            var serialized = new SerializedObject(clone);
            for (var i = 0; i < clone.BagViews.Count; i++)
            {
                var column = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, clone.BagViews[i].transform);
                var member = (Text)serialized.FindProperty("memberTexts").GetArrayElementAtIndex(i).objectReferenceValue;
                var fill = (RectTransform)serialized.FindProperty("memberHpFills").GetArrayElementAtIndex(i).objectReferenceValue;
                var nameBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, member.transform);
                var hpBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, fill.parent);
                var revive = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, clone.ReviveViews[i].transform);
                Assert.AreEqual(column.center.x, nameBounds.center.x + 22, 0.001f, "name column " + i);
                Assert.AreEqual(column.center.x, hpBounds.center.x + 22, 0.001f, "HP column " + i);
                Assert.AreEqual(column.center.x, revive.center.x - 36, 0.001f, "revive column " + i);
                Assert.GreaterOrEqual(column.min.y + 0.001f, stash.max.y, "bag column " + i);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone.gameObject);
        }
    }

    [Test]
    public void LongItemDescriptionRemainsCompleteAndScrolls()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var source = Array.Find(scene.GetRootGameObjects(), r => r.GetComponentInChildren<UI_PrepView>(true) != null).GetComponentInChildren<UI_PrepView>(true).ItemInfoView;
        var canvasObject = new GameObject("InfoTest", typeof(Canvas));
        var clone = UnityEngine.Object.Instantiate(source, canvasObject.transform);
        clone.gameObject.SetActive(true);
        var description = string.Join("\n", new string[30]).Replace("\n", "긴 아이템 설명 원문을 그대로 유지한다\n");
        try
        {
            clone.UpdateUI(description, "", false, false);
            var so = new SerializedObject(clone);
            var scroll = (ScrollRect)so.FindProperty("descriptionScroll").objectReferenceValue;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Canvas.ForceUpdateCanvases();
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
            Assert.AreEqual(description, ((Text)so.FindProperty("descriptionText").objectReferenceValue).text);
            scroll.OnScroll(new UnityEngine.EventSystems.PointerEventData(null) { scrollDelta = Vector2.down * 4 });
            Assert.Greater(scroll.content.anchoredPosition.y, 0);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    public static void AssertScreen(Transform root)
    {
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            Assert.GreaterOrEqual(text.fontSize, 9, text.name);
            Assert.AreEqual(FontStyle.Normal, text.fontStyle, text.name);
        }
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            var rect = ((RectTransform)button.transform).rect;
            Assert.GreaterOrEqual(rect.width, 44, button.name);
            Assert.GreaterOrEqual(rect.height, 44, button.name);
        }
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null)
            {
                continue;
            }
            StringAssert.DoesNotContain("AIGenerated/UI/", AssetDatabase.GetAssetPath(image.sprite), image.name);
            if (AssetDatabase.GetAssetPath(image.sprite).Contains("HD2D/UI/"))
            {
                Assert.AreEqual(3, image.pixelsPerUnitMultiplier, image.name);
            }
            Assert.IsNull(image.GetComponent<UI_ArtSurface>(), image.name);
        }
    }
}

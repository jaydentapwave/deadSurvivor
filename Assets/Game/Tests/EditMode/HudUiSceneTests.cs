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

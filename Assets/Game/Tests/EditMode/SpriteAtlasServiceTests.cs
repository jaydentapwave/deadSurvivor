using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.U2D;

// 그림 넣기 AC3 — 아틀라스 한 장이 캐릭터·몬스터 19장과 전투 효과 10장을 담고, 이름별로 같은 스프라이트 객체(PPU 48, 피벗 아래 가운데)
public class SpriteAtlasServiceTests
{
    private const string AtlasAssetPath = "Assets/Game/03.Resources/Resources/Art/Characters.spriteatlasv2";

    private static readonly string[] SpriteNames =
    {
        "bruno_front", "bruno_back", "lena_front", "lena_back", "mira_front", "mira_back", "bolt_front", "bolt_back",
        "slime", "mushroom", "boar", "rock", "puffer",
        "bruno_downed", "lena_downed", "mira_downed", "bolt_downed", "boar_telegraph", "puffer_inflate",
        "slash_sword", "slash_axe", "hit_star", "arrow", "crossbow_bolt", "fireball", "explosion",
        "lightning", "heal_single", "heal_area",
    };

    [OneTimeSetUp]
    public void PackAtlas()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasAssetPath);
        SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
    }

    [Test]
    public void AtlasHoldsCharactersMonstersAndTenEffects()
    {
        var itemCount = new ItemRepository(new ResourceService()).ItemDataList.Count();
        Assert.AreEqual(SpriteNames.Length + itemCount, Resources.Load<SpriteAtlas>("Art/Characters").spriteCount);
    }

    [Test]
    public void ReturnsEverySpriteWithPpuAndFootPivot()
    {
        var service = new SpriteAtlasService(new ResourceService());
        foreach (var spriteName in SpriteNames)
        {
            var sprite = service.Get(spriteName);

            Assert.IsNotNull(sprite, spriteName);
            Assert.AreEqual(Const.PixelsPerUnit, sprite.pixelsPerUnit, spriteName);
            Assert.AreEqual(new Vector2(sprite.rect.width * 0.5f, 0f), sprite.pivot, spriteName);
            Assert.AreSame(sprite, service.Get(spriteName), spriteName);
        }

        DestroyAll(service);
    }

    [Test]
    public void MissingNameLogsOneErrorAndReturnsNull()
    {
        var service = new SpriteAtlasService(new ResourceService());
        LogAssert.Expect(LogType.Error, "[SpriteAtlasService] No sprite nobody_front");

        Assert.IsNull(service.Get("nobody_front"));
        LogAssert.NoUnexpectedReceived();
    }

    // Dispose의 Object.Destroy는 에디터 시험에서 쓸 수 없어 시험이 직접 지운다
    private static void DestroyAll(SpriteAtlasService service)
    {
        foreach (var spriteName in SpriteNames)
        {
            Object.DestroyImmediate(service.Get(spriteName));
        }
    }
}

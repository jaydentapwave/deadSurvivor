using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.U2D;

// 그림 넣기 AC3 — 아틀라스 한 장이 캐릭터·몬스터·동작 프레임·그림자와 전투 효과을 담고, 이름별로 같은 스프라이트 객체(PPU 48, 피벗 아래 가운데)
public class SpriteAtlasServiceTests
{
    private const string AtlasAssetPath = "Assets/Game/03.Resources/Resources/Art/Characters.spriteatlasv2";

    private static readonly string[] SpriteNames =
    {
        "bruno_front", "bruno_back", "lena_front", "lena_back", "mira_front", "mira_back", "bolt_front", "bolt_back",
        "slime", "mushroom", "boar", "rock", "puffer",
        "bruno_downed", "lena_downed", "mira_downed", "bolt_downed", "boar_telegraph", "puffer_inflate",
        "slash_sword", "slash_axe", "hit_star", "arrow", "crossbow_bolt", "fireball", "explosion",
        "lightning", "heal_single", "heal_area", "shadow",
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
        // UI 그림은 캐릭터 PPU/피벗 계약과 다르므로 별도로 센다.
        var uiNames = new[] { "panel", "button", "bag_cell", "hp_bar_bg", "hp_bar_fill",
            "cooldown_bg", "cooldown_fill", "joystick_base", "joystick_knob",
            "digit_0", "digit_1", "digit_2", "digit_3", "digit_4", "digit_5", "digit_6", "digit_7", "digit_8", "digit_9" };
        var atlas = Resources.Load<SpriteAtlas>("Art/Characters");
        foreach (var key in uiNames)
        {
            var sprite = atlas.GetSprite(key);
            Assert.IsNotNull(sprite, key);
            Object.DestroyImmediate(sprite);
        }

        var expected = SpriteNames.Length + uiNames.Length + new ItemRepository(new ResourceService()).ItemDataList.Count();
        foreach (var character in new CharacterRepository(new ResourceService()).CharacterDataList)
        {
            expected += 2 * (character.Motion.WalkFrames + character.Motion.AttackFrames);
        }

        Assert.AreEqual(expected, Resources.Load<SpriteAtlas>("Art/Characters").spriteCount);
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
    public void AnimationFrameNamesResolveFromCharacterMetadata()
    {
        var service = new SpriteAtlasService(new ResourceService());
        foreach (var character in new CharacterRepository(new ResourceService()).CharacterDataList)
        {
            foreach (var clip in new[] { "walk_front", "walk_back", "attack_front", "attack_back" })
            {
                var count = clip.StartsWith("walk") ? character.Motion.WalkFrames : character.Motion.AttackFrames;
                var frames = service.GetMemberFrames(character.Sprite, clip, count);
                Assert.AreEqual(count, frames.Length);
                foreach (var frame in frames)
                {
                    Assert.IsNotNull(frame, character.Id + " " + clip);
                    Assert.AreEqual(Const.PixelsPerUnit, frame.pixelsPerUnit);
                    Assert.AreEqual(new Vector2(32f, 0f), frame.pivot);
                    Object.DestroyImmediate(frame);
                }
            }
        }
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

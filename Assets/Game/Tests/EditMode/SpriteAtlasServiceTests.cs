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
        "boar_down", "boar_right", "boar_telegraph_down", "boar_telegraph_right", "boar_telegraph_up", "boar_telegraph_upright", "boar_up", "boar_upright", "bolt_down", "bolt_right", "bolt_up", "bruno_down", "bruno_right", "bruno_up", "lena_down", "lena_right", "lena_up", "mira_down", "mira_right", "mira_up", "mushroom_down", "mushroom_right", "mushroom_up", "mushroom_upright", "puffer_down", "puffer_inflate_down", "puffer_inflate_right", "puffer_inflate_up", "puffer_inflate_upright", "puffer_right", "puffer_up", "puffer_upright", "rock_down", "rock_right", "rock_up", "rock_upright", "slime_down", "slime_right", "slime_up", "slime_upright",
        "bruno_front", "bruno_back", "lena_front", "lena_back", "mira_front", "mira_back", "bolt_front", "bolt_back",
        "slime", "mushroom", "boar", "rock", "puffer",
        "bruno_downed", "lena_downed", "mira_downed", "bolt_downed", "boar_telegraph", "puffer_inflate",
        "shadow",
    };

    private static string[] EffectNames => AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/03.Resources/Art/HD2D/Effects" })
        .Select(guid => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid))).ToArray();

    [OneTimeSetUp]
    public void PackAtlas()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasAssetPath);
        SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
    }

    [Test]
    public void AtlasHoldsCharactersMonstersAndReviewedEffectFrames()
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

        Assert.AreEqual(68, EffectNames.Length);
        var expected = SpriteNames.Length + EffectNames.Length + uiNames.Length + new ItemRepository(new ResourceService()).ItemDataList.Count();
        foreach (var character in new CharacterRepository(new ResourceService()).CharacterDataList)
        {
            expected += 5 * (character.Motion.WalkFrames + character.Motion.AttackFrames);
        }

        expected -= 7; // mira_up_walk: approved pending clip.
        Assert.AreEqual(expected, Resources.Load<SpriteAtlas>("Art/Characters").spriteCount);
    }

    [Test]
    public void ReturnsEverySpriteWithPpuAndItsApprovedPivot()
    {
        var service = new SpriteAtlasService(new ResourceService());
        foreach (var spriteName in SpriteNames.Concat(EffectNames))
        {
            var sprite = service.Get(spriteName);

            Assert.IsNotNull(sprite, spriteName);
            Assert.AreEqual(Const.PixelsPerUnit, sprite.pixelsPerUnit, spriteName);
            Assert.AreEqual(new Vector2(sprite.rect.width * .5f, EffectNames.Contains(spriteName) ? sprite.rect.height * .5f : 0f), sprite.pivot, spriteName);
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
            foreach (var clip in new[] { "walk_downright", "walk_upright", "attack_downright", "attack_upright" })
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
    public void SuppliedClipsResolveAllBrunoDirectionsAndOnlyMiraUpWalkIsMissing()
    {
        var service = new SpriteAtlasService(new ResourceService());
        var table = service.GetDirectionFrames("bruno", "walk", 4);
        Assert.IsNotNull(table[(int)FacingDirection.Down]);
        Assert.IsNotNull(table[(int)FacingDirection.Right]);
        Assert.IsNotNull(table[(int)FacingDirection.Up]);
        Assert.IsNull(service.GetDirectionFrames("mira", "walk", 7)[(int)FacingDirection.Up]);
        Assert.AreSame(service.Get("bruno_walk_front_00"), table[(int)FacingDirection.DownRight][0]);
        Assert.AreSame(service.Get("bruno_walk_back_00"), table[(int)FacingDirection.UpRight][0]);
        Assert.AreSame(service.Get("bruno_front"), service.Get("bruno_downright"));
        foreach (var sprite in service.GetDirections("bruno"))
        {
            Assert.IsNotNull(sprite);
        }
        Assert.IsFalse(service.TryGet("missing_optional_direction", out _));
        LogAssert.NoUnexpectedReceived();
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
        foreach (var spriteName in SpriteNames.Concat(EffectNames))
        {
            Object.DestroyImmediate(service.Get(spriteName));
        }
    }
}

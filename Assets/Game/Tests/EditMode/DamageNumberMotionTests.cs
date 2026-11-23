using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// F6 AC7 — 피해 숫자 합치기·반올림·움직임·상한 교체(기획서 B절). 값은 실제 feedback_rules.json
[Category("DQScreen")]
public class DamageNumberMotionTests
{
    private static DamageNumberRulesData Rules => new FeedbackRulesRepository(new ResourceService()).Rules.DamageNumber;

    [Test]
    public void SameMonsterThreeHitsInOneFrameMergeIntoOneAtFirstPosition()
    {
        var recordList = new List<MonsterHitRecord>
        {
            new(7, 1.2f, new Vector2(10f, 20f), null),
            new(8, 4f, new Vector2(50f, 60f), null),
            new(7, 2.3f, new Vector2(11f, 21f), null),
            new(7, 0.6f, new Vector2(12f, 22f), null),
        };
        var resultList = new List<MonsterHitRecord>();

        DamageNumberMotion.Merge(recordList, resultList, new Dictionary<int, int>());

        Assert.AreEqual(2, resultList.Count);
        Assert.AreEqual(7, resultList[0].MonsterId);
        Assert.AreEqual(4.1f, resultList[0].Amount, 1e-4f);
        Assert.AreEqual(new Vector2(10f, 20f), resultList[0].Position);
        Assert.AreEqual(8, resultList[1].MonsterId);
        Assert.AreEqual(4, DamageNumberMotion.GetValue(resultList[0].Amount));
    }

    [Test]
    public void ValueIsRoundedHalfUpAndAtLeastOne()
    {
        Assert.AreEqual(1, DamageNumberMotion.GetValue(0.2f));
        Assert.AreEqual(2, DamageNumberMotion.GetValue(2.49f));
        Assert.AreEqual(3, DamageNumberMotion.GetValue(2.5f));
        Assert.AreEqual(4, DamageNumberMotion.GetValue(3.5f));
    }

    [Test]
    public void RiseAndAlphaFollowAge()
    {
        var rules = Rules;
        var fadeStart = rules.Lifetime * rules.FadeStartRatio;

        Assert.AreEqual(0f, DamageNumberMotion.GetRisePx(0f, rules), 1e-5f);
        Assert.AreEqual(1f, DamageNumberMotion.GetAlpha(0f, rules), 1e-5f);
        Assert.AreEqual(1f, DamageNumberMotion.GetAlpha(fadeStart - 0.001f, rules), 1e-5f);
        Assert.Less(DamageNumberMotion.GetAlpha(fadeStart + 0.05f, rules), 1f);
        Assert.AreEqual(rules.RisePx, DamageNumberMotion.GetRisePx(rules.Lifetime, rules), 1e-4f);
        Assert.AreEqual(0f, DamageNumberMotion.GetAlpha(rules.Lifetime, rules), 1e-5f);
    }

    [Test]
    public void FullRegistryEvictsOldestForNewNumber()
    {
        var maxActive = Rules.MaxActive;
        Assert.AreEqual(0, DamageNumberMotion.GetEvictCount(maxActive - 1, maxActive));
        Assert.AreEqual(1, DamageNumberMotion.GetEvictCount(maxActive, maxActive));

        var registry = new DamageNumberViewRegistry();
        var objectList = new List<GameObject>();
        for (var i = 0; i < 3; i++)
        {
            var go = new GameObject($"number{i}");
            objectList.Add(go);
            registry.Register(new DamageNumberEntry(go.AddComponent<DamageNumberView>(), new Vector3(i, 0f, 0f), Vector3.zero));
        }

        // 상한 3에서 새 숫자 하나: 가장 오래된 것(앞)을 빼고 끝에 넣는다
        var evictCount = DamageNumberMotion.GetEvictCount(registry.Count, 3);
        for (var i = 0; i < evictCount; i++)
        {
            registry.Unregister(0);
        }

        var newObject = new GameObject("number3");
        objectList.Add(newObject);
        registry.Register(new DamageNumberEntry(newObject.AddComponent<DamageNumberView>(), new Vector3(3f, 0f, 0f), Vector3.zero));

        Assert.AreEqual(3, registry.Count);
        Assert.AreEqual(new Vector3(1f, 0f, 0f), registry.EntryList[0].StartPosition);
        Assert.AreEqual(new Vector3(3f, 0f, 0f), registry.EntryList[2].StartPosition);
        foreach (var go in objectList)
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SameTargetStacksApartAcrossFramesAndMovementWhileOtherTargetsStayIndependent()
    {
        var entryList = new List<DamageNumberEntry>
        {
            new(null, Vector3.zero, Vector3.zero, 7) { Age = .3f },
            new(null, Vector3.one * 20, Vector3.zero, 8),
            new(null, Vector3.one * 2, Vector3.zero, 7) { Age = .1f },
            new(null, Vector3.one * 3, Vector3.zero, 7)
        };
        var rules = Rules;
        var old = DamageNumberMotion.GetStackOffsetPx(entryList, 0, rules.FontSizePx);
        var middle = DamageNumberMotion.GetStackOffsetPx(entryList, 2, rules.FontSizePx);
        Assert.AreEqual(-3, old.x);
        Assert.AreEqual(3, middle.x);
        Assert.AreEqual(Vector2.zero, DamageNumberMotion.GetStackOffsetPx(entryList, 3, rules.FontSizePx));
        Assert.AreEqual(Vector2.zero, DamageNumberMotion.GetStackOffsetPx(entryList, 1, rules.FontSizePx));
        var gap = Mathf.Ceil(rules.FontSizePx) + 2;
        Assert.AreEqual(gap * 2, old.y);
        Assert.AreEqual(gap, middle.y);
        Assert.GreaterOrEqual(old.y + DamageNumberMotion.GetRisePx(.3f, rules)
            - middle.y - DamageNumberMotion.GetRisePx(.1f, rules), gap);
        foreach (var index in new[] { 0, 2, 3 })
        {
            Assert.AreEqual(Vector3.one * 3, DamageNumberMotion.GetStackOrigin(entryList, index));
        }
        Assert.AreEqual(Vector3.one * 20, DamageNumberMotion.GetStackOrigin(entryList, 1));
        entryList.RemoveAt(0);
        Assert.AreEqual(middle, DamageNumberMotion.GetStackOffsetPx(entryList, 1, rules.FontSizePx));
    }

    [Test]
    public void UnassignedTargetsAndNewRegistryDoNotInheritStackSlots()
    {
        var entryList = new List<DamageNumberEntry>
        {
            new(null, Vector3.one, Vector3.zero),
            new(null, Vector3.zero, Vector3.zero)
        };
        Assert.AreEqual(Vector2.zero, DamageNumberMotion.GetStackOffsetPx(entryList, 0, Rules.FontSizePx));
        Assert.AreEqual(Vector3.one, DamageNumberMotion.GetStackOrigin(entryList, 0));
        entryList.Clear();
        entryList.Add(new DamageNumberEntry(null, Vector3.zero, Vector3.zero, 7));
        Assert.AreEqual(Vector2.zero, DamageNumberMotion.GetStackOffsetPx(entryList, 0, Rules.FontSizePx));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void N1N2NonzeroDepthAndTerrainSurviveRiseAndPoolReuse(bool hasTerrain)
    {
        var context = new TestRunContext();
        var resources = context.ResourceService;
        var atlas = new SpriteAtlasService(resources);
        var placeholder = new PlaceholderSpriteFactory();
        var parent = new GameObject("N1 numbers");
        var registry = new DamageNumberViewRegistry();
        var pool = new PoolManager();
        var rules = new FeedbackRulesRepository(resources);
        var factory = new DamageNumberViewFactory(pool, placeholder, atlas, rules, registry,
            resources.Load<DamageNumberView>("Prefab/DamageNumberView"), parent.transform);
        var monsters = new MonsterViewFactory(pool, resources, new MonsterViewRegistry(), placeholder, atlas, context.RunRules, new EffectSpriteCache(resources));
        var heights = new int[73 * 73];
        for (var j = 0; j < 73; j++)
        {
            for (var i = 0; i < 73; i++)
            {
                heights[j * 73 + i] = i >= 36 ? 250 : -100;
            }
        }
        var terrain = hasTerrain ? TerrainHeightGrid.FromMillimetres(1, -36, -36, 73, 73, heights) : TerrainHeightGrid.Flat;
        using var presenter = new DamageNumberPresenter(context.RunModel, context.CombatFeedbackModel, rules,
            context.RunRules, factory, registry, monsters, context.ViewRules, terrain);
        context.RunModel.SetPhase(RunPhase.Battle);
        context.RunModel.SetPaused(true);
        DamageNumberView previous = null;
        try
        {
            foreach (var floor in new[] {new Vector2(1000,1500),new Vector2(-800,-400)})
            {
                var monster = context.MonsterRepository.MonsterDataList[0];
                context.CombatFeedbackModel.AddMonsterHit(1,12,floor,monster);
                presenter.LateTick();
                var entry = registry.EntryList[0];
                var anchor = IsoProjection.FloorToWorldSnapped(floor,context.RunRules.UnitScale,context.ViewRules.SinTilt, terrain);
                Assert.That(Vector3.Distance(anchor,entry.AnchorPosition),Is.LessThan(1e-5));
                if (hasTerrain)
                {
                    Assert.AreEqual(floor.x > 0 ? .25f : -.1f, anchor.y, .000001f);
                }
                entry.Age = .3f;
                presenter.LateTick();
                var h = monsters.GetHeadHeight(monster)+rules.Rules.DamageNumber.OffsetPx/Const.PixelsPerUnit
                    +DamageNumberMotion.GetRisePx(.3f,rules.Rules.DamageNumber)/Const.PixelsPerUnit;
                Assert.That(Vector3.Distance(anchor+TiltedViewMath.UprightOffset(h,context.ViewRules.TiltDegrees),entry.View.transform.position),Is.LessThan(1e-4));
                if(previous != null) Assert.AreSame(previous,entry.View);
                previous=entry.View;
                factory.Release(0);
            }
        }
        finally
        {
            Object.DestroyImmediate(parent);
            // Runtime services use deferred Destroy. EditMode owns these test clones directly.
            var sprites = (Dictionary<string, Sprite>)typeof(SpriteAtlasService).GetField("_spriteDict",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(atlas);
            foreach (var sprite in sprites.Values) Object.DestroyImmediate(sprite);
        }
    }
}

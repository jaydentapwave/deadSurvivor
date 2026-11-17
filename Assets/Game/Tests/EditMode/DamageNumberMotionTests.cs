using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// F6 AC7 — 피해 숫자 합치기·반올림·움직임·상한 교체(기획서 B절). 값은 실제 feedback_rules.json
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
    public void N1NonzeroDepthSurvivesRiseAndPoolReuse()
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
        var monsters = new MonsterViewFactory(pool, resources, new MonsterViewRegistry(), placeholder, atlas, context.RunRules);
        using var presenter = new DamageNumberPresenter(context.RunModel, context.CombatFeedbackModel, rules,
            context.RunRules, factory, registry, monsters, context.ViewRules);
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
                var anchor = IsoProjection.FloorToWorldSnapped(floor,context.RunRules.UnitScale,context.ViewRules.SinTilt);
                Assert.That(Vector3.Distance(anchor,entry.AnchorPosition),Is.LessThan(1e-5));
                entry.Age = .3f;
                presenter.LateTick();
                var h = monsters.GetHeadHeight(monster)+rules.Rules.DamageNumber.OffsetPx/Const.PixelsPerUnit
                    +DamageNumberMotion.GetRisePx(.3f,rules.Rules.DamageNumber)/Const.PixelsPerUnit;
                Assert.That(Vector3.Distance(anchor+TiltedViewMath.UprightOffset(h,30),entry.View.transform.position),Is.LessThan(1e-4));
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

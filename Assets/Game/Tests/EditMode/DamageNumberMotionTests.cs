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
            new(7, 1.2f, new Vector2(10f, 20f), 18f),
            new(8, 4f, new Vector2(50f, 60f), 18f),
            new(7, 2.3f, new Vector2(11f, 21f), 18f),
            new(7, 0.6f, new Vector2(12f, 22f), 18f),
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
            registry.Register(new DamageNumberEntry(go.AddComponent<DamageNumberView>(), new Vector2(i, 0f)));
        }

        // 상한 3에서 새 숫자 하나: 가장 오래된 것(앞)을 빼고 끝에 넣는다
        var evictCount = DamageNumberMotion.GetEvictCount(registry.Count, 3);
        for (var i = 0; i < evictCount; i++)
        {
            registry.Unregister(0);
        }

        var newObject = new GameObject("number3");
        objectList.Add(newObject);
        registry.Register(new DamageNumberEntry(newObject.AddComponent<DamageNumberView>(), new Vector2(3f, 0f)));

        Assert.AreEqual(3, registry.Count);
        Assert.AreEqual(new Vector2(1f, 0f), registry.EntryList[0].StartPosition);
        Assert.AreEqual(new Vector2(3f, 0f), registry.EntryList[2].StartPosition);
        foreach (var go in objectList)
        {
            Object.DestroyImmediate(go);
        }
    }
}

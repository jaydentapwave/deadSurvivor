using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// AC17 — Core 기반
public class CoreTests
{
    private struct TestEvent
    {
        public int Value;
    }

    [Test]
    public void EventBusSubscribePublishUnsubscribe()
    {
        var eventBus = new EventBus();
        var received = 0;
        var subscription = eventBus.Subscribe((in TestEvent e) => received += e.Value);

        eventBus.Publish(new TestEvent { Value = 3 });
        Assert.AreEqual(3, received);

        subscription.Dispose();
        eventBus.Publish(new TestEvent { Value = 5 });
        Assert.AreEqual(3, received);
    }

    [Test]
    public void EventBusDeliversToEverySubscriber()
    {
        var eventBus = new EventBus();
        var first = 0;
        var second = 0;
        using (eventBus.Subscribe((in TestEvent e) => first += e.Value))
        using (eventBus.Subscribe((in TestEvent e) => second += e.Value))
        {
            eventBus.Publish(new TestEvent { Value = 2 });
        }

        Assert.AreEqual(2, first);
        Assert.AreEqual(2, second);
    }

    [Test]
    public void PoolManagerReusesReleasedInstanceAndResetsOnGet()
    {
        var poolManager = new PoolManager();
        var prefab = new GameObject("PoolTestPrefab").transform;
        var parent = new GameObject("PoolTestParent").transform;
        try
        {
            var first = poolManager.Get("test", prefab, parent);
            first.localPosition = new Vector3(5f, 6f, 0f);
            first.localScale = new Vector3(2f, 2f, 2f);
            poolManager.Release("test", first);
            Assert.IsFalse(first.gameObject.activeSelf);

            var second = poolManager.Get("test", prefab, parent);

            Assert.AreSame(first, second);
            Assert.IsTrue(second.gameObject.activeSelf);
            Assert.AreSame(parent, second.parent);
            Assert.AreEqual(Vector3.zero, second.localPosition);
            Assert.AreEqual(Vector3.one, second.localScale);

            var third = poolManager.Get("test", prefab, parent);
            Assert.AreNotSame(second, third);
        }
        finally
        {
            Object.DestroyImmediate(parent.gameObject);
            Object.DestroyImmediate(prefab.gameObject);
        }
    }

    [Test]
    public void GameLoggerWritesNothingWhenDisabled()
    {
        GameLogger.Write(false, LogType.Error, "[CoreTests] disabled error");
        GameLogger.Write(false, LogType.Log, "[CoreTests] disabled log");
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void GameLoggerWritesWhenEnabled()
    {
        GameLogger.Write(true, LogType.Warning, "[CoreTests] enabled warning");
        LogAssert.Expect(LogType.Warning, new Regex(@"\[CoreTests\] enabled warning"));
    }
}

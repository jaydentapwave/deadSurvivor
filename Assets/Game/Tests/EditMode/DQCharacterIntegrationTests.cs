using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class DQCharacterIntegrationTests
{
    [Test]
    public void AllAdoptedSpritesHaveFootPivotAndOnePointTexturePerSpecies()
    {
        var resources = new ResourceService();
        var atlas = new SpriteAtlasService(resources);
        var catalog = resources.LoadJson<DQClipCatalog>("Data/DQ/dq_character_clips");
        var clones = new HashSet<Sprite>();
        try
        {
            var count = 0;
            foreach (var actor in catalog.actors)
            {
                var textures = new HashSet<Texture2D>();
                foreach (var source in actor.sprites)
                {
                    var sprite = atlas.Get(source.key, actor.atlasPath);
                    clones.Add(sprite);
                    Assert.IsNotNull(sprite, source.key);
                    Assert.AreEqual(48, sprite.pixelsPerUnit, source.key);
                    Assert.AreEqual(source.size, sprite.rect.width, source.key);
                    Assert.AreEqual(source.size, sprite.rect.height, source.key);
                    Assert.AreEqual(source.pivotX, sprite.pivot.x, .001, source.key);
                    Assert.AreEqual(source.size - source.pivotY, sprite.pivot.y, .001, source.key);
                    Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, source.key);
                    textures.Add(sprite.texture);
                    count++;
                }
                Assert.AreEqual(1, textures.Count, actor.actorId + " must fit one atlas texture");
            }
            Assert.AreEqual(1406, count);
        }
        finally
        {
            foreach (var clone in clones)
            {
                Object.DestroyImmediate(clone);
            }
        }
    }

    [Test]
    public void NewMonsterViewFirstFramePhaseAndRepeatedDeliveryShareOneClock()
    {
        var resources = new ResourceService();
        var atlas = new SpriteAtlasService(resources);
        var registry = new MonsterViewRegistry();
        var pool = new PoolManager();
        var rules = resources.LoadJson<RunRulesData>("Data/Run/run_rules");
        var factory = new MonsterViewFactory(pool, resources, registry, new PlaceholderSpriteFactory(), atlas, rules, new EffectSpriteCache(resources));
        var data = new MonsterRepository(resources).Get("boar");
        var monster = new Monster(808, data, Vector2.zero, 18, 1, 0);
        var view = factory.Create(monster, null);
        try
        {
            // Simulate a view created after MonsterStatePresenter.Tick, then LateTick's explicit sync.
            view.InitializeDQ(atlas.DQ.Get("boar"));
            view.UpdateState(MonsterPhase.Windup, Vector2.right);
            view.ApplyDQAnimation(.1f, false);
            Assert.AreEqual("windup", view.DQAction);
            Assert.IsTrue(view.IsDashLineVisible);
            Assert.AreEqual(.1f, view.DQPhaseClock, .0001);
            var sprite = view.BodySpriteName;
            view.UpdateState(MonsterPhase.Windup, Vector2.right);
            Assert.AreEqual(.1f, view.DQPhaseClock, .0001, "same phase must not reset clock");
            view.ApplyDQAnimation(0, false);
            Assert.AreEqual(.1f, view.DQPhaseClock, .0001, "paused delta must not advance clock");
            Assert.AreEqual(sprite, view.BodySpriteName);
            view.UpdateState(MonsterPhase.Dash, Vector2.right);
            Assert.AreEqual(0, view.DQPhaseClock);
        }
        finally
        {
            factory.Release(monster.Id);
            Object.DestroyImmediate(view.gameObject);
        }
    }
    [Test]
    public void DetachedDeathLeavesRegistryAndPoolReuseClearsAppearanceAndPhase()
    {
        var resources = new ResourceService();
        var atlas = new SpriteAtlasService(resources);
        var registry = new MonsterViewRegistry();
        var factory = new MonsterViewFactory(new PoolManager(), resources, registry,
            new PlaceholderSpriteFactory(), atlas, resources.LoadJson<RunRulesData>("Data/Run/run_rules"), new EffectSpriteCache(resources));
        var repository = new MonsterRepository(resources);
        var boar = new Monster(901, repository.Get("boar"), Vector2.zero, 18, 1, 0);
        var view = factory.Create(boar, null);
        try
        {
            view.UpdateState(MonsterPhase.Windup, Vector2.left);
            view.BeginAppearance(true);
            Assert.AreEqual(0, view.Appearance);
            Assert.AreSame(view, factory.Detach(boar.Id));
            Assert.IsFalse(registry.TryGet(boar.Id, out _));
            view.BeginDQFall();
            factory.ReleaseDetached(view);
            var puffer = new Monster(902, repository.Get("puffer"), Vector2.zero, 18, 1, 0);
            var reused = factory.Create(puffer, null);
            Assert.AreSame(view, reused);
            Assert.AreEqual(1, reused.Appearance);
            Assert.AreEqual("puffer", reused.DQActorId);
            Assert.AreEqual(0, reused.DQPhaseClock);
            Assert.IsFalse(reused.IsDashLineVisible);
            factory.Release(puffer.Id);
        }
        finally
        {
            Object.DestroyImmediate(view.gameObject);
        }
    }

}

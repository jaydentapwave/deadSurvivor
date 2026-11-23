using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

// Controlled fixtures inside the actual Main scope. Model writes are confined to this
// integration test; the regular playable route and gameplay rules are unchanged.
public class DQRenderBoundaryIntegrationTests
{
    private const string Output = "output/hd2d-dq/apply/8/r3";
    private static IObjectResolver _container;
    private static T Get<T>()
    {
        try
        {
            var result = _container.Resolve<T>();
            Record("resolve", typeof(T).Name + "=" + (result == null ? "null" : "present"));
            return result;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Main resolve " + typeof(T).FullName, exception);
        }
    }
    private static void Record(string name, string detail)
    {
        Directory.CreateDirectory(Output);
        File.AppendAllText(Output + "/render-boundaries.txt", $"PASS {name}: frame={Time.frameCount} {detail}\n");
    }

    [UnityTest]
    public IEnumerator MainCameraHitAndPresenterLifecycleBoundaries()
    {
        Assert.IsFalse(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty);
        EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/render-boundaries.txt", "Actual Main scope / URP camera consumption integration\n");
        yield return new EnterPlayMode();
        yield return RunBoundaries();
        yield return new ExitPlayMode();
    }

    // Create captured locals only after EnterPlayMode's domain reload. NUnit's
    // iterator persistence cannot restore a compiler display class across that reload.
    private IEnumerator RunBoundaries()
    {
        yield return null;
        var scope = Object.FindAnyObjectByType<GameSceneLifetimeScope>();
        Assert.IsNotNull(scope, "Main scene scope");
        _container = scope.Container;
        Assert.IsNotNull(_container, "Main scope built");
        Record("bootstrap", scope.gameObject.name);
        var run = Get<RunModel>();
        var service = Get<RunService>();
        var world = Get<WorldPresenter>();
        var members = Get<IReadOnlyList<MemberView>>();
        var party = Get<PartyModel>();
        var monsters = Get<MonsterModel>();
        var combat = Get<CombatService>();
        var combatModel = Get<CombatModel>();
        var atlas = Get<SpriteAtlasService>();
        var bus = Get<EventBus>();
        var camera = Get<Camera>();
        var registry = Get<MonsterViewRegistry>();
        var repository = Get<MonsterRepository>();
        Assert.IsNotNull(world);
        Assert.IsNotNull(members);
        Assert.AreEqual(4, members.Count);
        service.StartRun(731, 0);
        run.SetPaused(true);
        world.LateTick();
        // Equip the second weapon through the real bag contract; a bare Equip would
        // leave BagPlacementInfo without its corresponding slot.
        var bags = Get<BagService>();
        var bag = Get<BagModel>();
        run.SetPhase(RunPhase.Prep);
        bags.AddLoot(new[] { "sword" });
        Assert.IsTrue(bags.Drop(bag.LastGrantedUidList[0], 0, 0, 1, 0, out var bagReason), bagReason.ToString());
        run.SetPhase(RunPhase.Battle);
        combat.ResetRun();
        combat.PrepareWave();
        var member = party.MemberList[0];
        var target = new Monster(88001, repository.Get("slime"), member.Position + Vector2.right * 10, 100000, 0, 1000);
        monsters.Add(target);
        var observedHits = new List<DQHitRecord>();
        using var subscription = bus.Subscribe<GameEvent.MemberAttackHitEvent>((in GameEvent.MemberAttackHitEvent e) =>
        {
            if (e.MemberIndex == 0) observedHits.Add(e.Hit);
        });
        var renderedIds = new List<long>();
        var renderedFrames = new List<int>();
        var extraRenders = 0;
        var sceneRenders = 0;
        var duplicateStable = true;
        var callback = typeof(WorldPresenter).GetMethod("OnEndCameraRendering", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(callback);
        var extra = new GameObject("DQ test extra camera").AddComponent<Camera>();
        extra.CopyFrom(camera);
        extra.tag = "Untagged";
        extra.depth = camera.depth + 1;
        extra.targetTexture = new RenderTexture(64, 64, 16);
        void Observe(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering == extra) extraRenders++;
            if (rendering.cameraType == CameraType.SceneView) sceneRenders++;
            if (rendering == camera && members[0].DQAction == "attack-hit")
            {
                renderedIds.Add(members[0].DQHitAttackId);
                renderedFrames.Add(members[0].DQFrame);
                var count = world.DQHitsRendered;
                callback.Invoke(world, new object[] { context, camera });
                duplicateStable &= world.DQHitsRendered == count;
            }
        }
        RenderPipelineManager.endCameraRendering += Observe;
        try
        {
            camera.enabled = false;
            combat.Step(.033f);
            var motion = combatModel.MemberStateList[0].AttackMotion;
            var firstId = motion.AttackId;
            Assert.GreaterOrEqual(combatModel.MemberStateList[0].PendingActionList.Count, 2);
            combat.Step(2);
            Assert.Greater(motion.AttackId, firstId, "same step restarted after damage");
            Assert.AreEqual(1, observedHits.Count, "simultaneous weapons share one actual hit record");
            Assert.Less(target.Hp, target.MaxHp);
            world.LateTick();
            Assert.AreEqual(firstId, members[0].DQHitAttackId);
            var pending = world.DQHitsPending;
            Assert.Greater(pending, 0);
            // Render the SceneView as well as the extra game camera. Neither may
            // acknowledge the target Main camera's applied batch.
            var sceneView = SceneView.GetWindow<SceneView>();
            Assert.IsNotNull(sceneView.camera);
            Assert.AreEqual(CameraType.SceneView, sceneView.camera.cameraType);
            sceneView.Focus();
            SceneView.RepaintAll();
            yield return null;
            yield return null;
            Assert.Greater(sceneRenders, 0, "SceneView must really render through URP");
            Assert.AreEqual(pending, world.DQHitsPending);
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView, UnityEditor")).Focus();
            yield return null;
            yield return null;
            Assert.Greater(extraRenders, 0, "the additional camera must really render");
            Assert.AreEqual(pending, world.DQHitsPending, "no Main render: retain through multiple frames and other camera");
            Assert.AreEqual(firstId, members[0].DQHitAttackId);
            Record("scene-extra-unrendered-retention", $"pending={pending} sceneRenders={sceneRenders} extraRenders={extraRenders}");
            camera.enabled = true;
            yield return null;
            yield return null;
            Assert.Contains(firstId, renderedIds);
            var hitClip = atlas.DQ.Get("bruno").Clip("attack", observedHits[0].Facing.Direction);
            Assert.AreEqual(hitClip.eventFrame, renderedFrames[renderedIds.IndexOf(firstId)]);
            Assert.AreEqual(0, world.DQHitsPending);
            Assert.IsTrue(duplicateStable, "same-frame duplicate callback cannot consume twice");
            var rendered = world.DQHitsRendered;
            callback.Invoke(world, new object[] { default(ScriptableRenderContext), camera });
            Assert.AreEqual(rendered, world.DQHitsRendered, "duplicate camera notification consumes nothing twice");
            Record("large-step-end-restart-first-main-render", $"attack={firstId} actualHitFrame={hitClip.eventFrame} records={observedHits.Count}");

            camera.enabled = false;
            target.Position = member.Position + Vector2.left * 10;
            var batchStart = observedHits.Count;
            combat.Step(2); // previous right-facing attack hits, next starts left
            combat.Step(2); // left-facing attack hits before any Main render
            var batch = observedHits.Skip(batchStart).ToArray();
            Assert.AreEqual(2, batch.Length);
            Assert.AreNotEqual(batch[0].Facing.Label, batch[1].Facing.Label);
            Assert.AreNotEqual(batch[0].Step, batch[1].Step);
            world.LateTick();
            Assert.AreEqual(batch[1].AttackId, members[0].DQHitAttackId);
            yield return null;
            Assert.GreaterOrEqual(world.DQHitsPending, 2);
            camera.enabled = true;
            yield return null;
            yield return null;
            Assert.Contains(batch[1].AttackId, renderedIds);
            Assert.That(world.DQHitAudit, Does.Contain($",{batch[1].AttackId},coalesced"));
            Assert.That(world.DQHitAudit, Does.Contain($",{batch[1].AttackId},rendered"));
            Record("different-steps-directions-coalesced", string.Join(";", batch.Select(h => $"{h.AttackId}/{h.Step}/{h.Facing.Label}")));

            // Suppression must keep the removed id and its cause, never draw attack on a corpse.
            camera.enabled = false;
            void Enqueue(long id) => bus.Publish(new GameEvent.MemberAttackHitEvent { MemberIndex = 0, Hit = new DQHitRecord(id, id, SpriteFacing.Front) });
            Enqueue(90001);
            member.Hp = 0;
            world.LateTick();
            Assert.AreEqual(0, world.DQHitsPending);
            Assert.AreEqual("fall", members[0].DQAction);
            Assert.That(world.DQHitAudit, Does.Contain("90001").And.Contain("suppressed-by-death"));
            member.Hp = member.MaxHp;
            world.LateTick();
            foreach (var reason in new[] { "revive", "wave-start", "run-start" })
            {
                Enqueue(90002);
                if (reason == "revive") combat.ReviveMember(0);
                else if (reason == "wave-start") combat.PrepareWave();
                else combat.ResetRun();
                Assert.AreEqual(0, world.DQHitsPending);
                Assert.That(world.DQHitAudit, Does.Contain(reason));
            }
            members[0].AdvanceDQClock(.55f);
            motion.Stop();
            members[0].ApplyAnimation(false, 0, motion);
            Assert.AreEqual(6, members[0].DQFrame, "reproduce reviewer idle-clock input");
            Enqueue(90005);
            party.Clear();
            world.LateTick();
            Assert.AreEqual(0, world.DQHitsPending);
            Assert.That(world.DQHitAudit, Does.Contain("90005").And.Contain("inactive"));
            run.SetPhase(RunPhase.Lobby);
            service.StartRun(732, 0);
            run.SetPaused(true);
            world.LateTick();
            Assert.AreEqual("bruno", members[0].DQActorId);
            Assert.AreEqual("idle", members[0].DQAction);
            Assert.AreEqual(0, members[0].DQFrame);
            Record("death-revive-wave-run-suppression-and-same-character-idle-reset", "all causes recorded; idle frame6 -> frame0");

            monsters.Clear();
            world.LateTick();
            var boar = new Monster(89001, repository.Get("boar"), member.Position + Vector2.right * 50, 100000, 0, 100);
            var puffer = new Monster(89002, repository.Get("puffer"), member.Position + Vector2.left * 50, 100000, 0, 100);
            monsters.Add(boar); monsters.Add(puffer);
            boar.Phase = MonsterPhase.Windup; boar.PhaseClock = boar.Data.Charge.WindupSeconds;
            puffer.Phase = MonsterPhase.Fuse; puffer.PhaseClock = puffer.Data.Bomb.FuseSeconds;
            run.SetSpeedIndex(1);
            run.SetPaused(false);
            for (var step = 0; step < 23; step++)
            {
                service.AdvanceFrame(1f / 60);
                world.LateTick();
            }
            Assert.IsTrue(registry.TryGet(boar.Id, out var boarView));
            Assert.IsTrue(registry.TryGet(puffer.Id, out var pufferView));
            Assert.AreEqual(23f / 30, boarView.DQPhaseClock, .00001);
            Assert.AreEqual(5, boarView.DQFrame);
            Record("speed-two-windup-23-real-simulation-steps", $"frame={boarView.DQFrame},clock={boarView.DQPhaseClock}");
            for (var step = 23; step < 35; step++)
            {
                service.AdvanceFrame(1f / 60);
                world.LateTick();
            }
            Assert.AreEqual(35f / 30, pufferView.DQPhaseClock, .00001);
            Assert.AreEqual(6, pufferView.DQFrame);
            run.SetPaused(true);
            var boarFrame = boarView.DQFrame;
            var pufferFrame = pufferView.DQFrame;
            yield return null;
            world.LateTick();
            Assert.AreEqual(boarFrame, boarView.DQFrame, "pause retains authoritative phase clock");
            Assert.AreEqual(pufferFrame, pufferView.DQFrame);
            Record("speed-two-fuse-35-real-simulation-steps-and-pause", $"puffer={pufferFrame}@{pufferView.DQPhaseClock}");

            run.SetPaused(false);
            boar.Hp = 0;
            monsters.RemoveDead();
            world.LateTick();
            Assert.IsFalse(registry.TryGet(boar.Id, out _));
            Assert.AreEqual(1, world.DQDeathCount);
            world.LateTick();
            Assert.AreEqual(1, world.DQDeathCount, "no duplicate detach on second LateTick");
            run.SetPaused(true);
            run.SetPhase(RunPhase.Prep);
            monsters.Clear();
            world.LateTick(); world.LateTick();
            Assert.AreEqual(0, world.DQDeathCount);
            Assert.AreEqual(0, registry.Views.Count());
            Assert.IsFalse(boarView.gameObject.activeSelf);
            Assert.IsFalse(pufferView.gameObject.activeSelf);
            Record("presenter-clear-detached-and-live-views", "registry0 deathList0 repeated clear no duplicate return");

            run.SetPhase(RunPhase.Battle);
            var a = new Monster(89003, repository.Get("boar"), member.Position, 100, 0, 100);
            var b = new Monster(89004, repository.Get("puffer"), member.Position, 100, 0, 100);
            monsters.Add(a); monsters.Add(b); world.LateTick();
            registry.TryGet(a.Id, out var av); registry.TryGet(b.Id, out var bv);
            Assert.AreNotSame(av, bv, "pool must not hand out one returned view twice");
            run.SetPaused(false); a.Hp = b.Hp = 0; monsters.RemoveDead(); world.LateTick();
            Assert.AreEqual(2, world.DQDeathCount);
            run.SetPhase(RunPhase.Lobby);
            service.StartRun(733, 0); run.SetPaused(true);
            world.LateTick(); world.LateTick();
            Assert.AreEqual(0, world.DQDeathCount);
            Assert.AreEqual(0, registry.Views.Count());
            Record("presenter-restart-clears-detached-once", "two detached views released, repeated LateTick empty");
            Enqueue(90003); world.LateTick();
            Assert.Greater(world.DQHitsPending, 0);
            world.Dispose(); world.Dispose();
            Assert.AreEqual(0, world.DQHitsPending);
            Assert.AreEqual(0, world.DQDeathCount);
            Assert.That(world.DQHitAudit, Does.Contain("90003").And.Contain("dispose"));
            Enqueue(90004);
            world.LateTick();
            Assert.AreEqual(0, world.DQHitsPending, "Dispose unsubscribes hit events");
            callback.Invoke(world, new object[] { default(ScriptableRenderContext), camera });
            Assert.AreEqual(0, world.DQHitsPending);
            Record("dispose-clears-ids-unsubscribes-and-is-idempotent", "no residual hits/deaths or duplicate return");
            File.WriteAllText(Output + "/hit-audit.csv", "frame,member,attack,step,facing,batch,applied,reason\n" + world.DQHitAudit);
        }
        finally
        {
            camera.enabled = true;
            RenderPipelineManager.endCameraRendering -= Observe;
            Object.Destroy(extra.targetTexture);
            Object.Destroy(extra.gameObject);
        }
    }

    [UnityTearDown]
    public IEnumerator ExitIfInterrupted()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}

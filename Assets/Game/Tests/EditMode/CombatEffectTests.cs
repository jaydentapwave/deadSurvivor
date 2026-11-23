using System.Linq;
using NUnit.Framework;
using UnityEngine;

[Category("DQScreen")]
public class CombatEffectTests
{
    [TestCase("sword", "", "slash_sword")]
    [TestCase("axe", "", "slash_axe")]
    [TestCase("shortbow", "arrow", "")]
    [TestCase("crossbow", "crossbow_bolt", "")]
    [TestCase("firestaff", "fireball", "explosion")]
    [TestCase("wand", "", "lightning")]
    [TestCase("relic", "", "heal_single")]
    [TestCase("censer", "", "heal_area")]
    [TestCase("orb", "", "pulse")]
    public void MappingMatchesApprovedItemKinds(string itemId, string projectile, string burst)
    {
        var data = new ResourceService().LoadJson<CombatEffectData>("Data/Combat/combat_effects");
        Assert.AreEqual(projectile, data.Get(itemId).Projectile);
        Assert.AreEqual(burst, data.Get(itemId).Burst);
        Assert.AreEqual(data.Items.Count, data.Items.Select(item => item.ItemId).Distinct().Count());
    }

    [Test]
    public void EveryActionAndPulseHasAnExistingSprite()
    {
        var resources = new ResourceService();
        var data = resources.LoadJson<CombatEffectData>("Data/Combat/combat_effects");
        var items = new ItemRepository(resources);
        foreach (var item in items.ItemDataList)
        {
            if (item.Mode == ItemMode.Passive && item.BonusEffect != ItemEffectKind.Pulse)
            {
                continue;
            }

            var effect = data.Get(item.Id);
            foreach (var key in new[] { effect.Projectile, effect.Burst }.Where(key => !string.IsNullOrEmpty(key)))
            {
                var clip = data.GetClip(key);
                for (var frame = 0; frame < clip.Frames; frame++)
                {
                    var path = $"Assets/Game/03.Resources/Art/HD2D/Effects/{clip.FrameKey(frame)}.png";
                    Assert.IsNotNull(UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path), path);
                    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
                    Assert.AreEqual(48f, importer.spritePixelsPerUnit);
                    Assert.AreEqual(FilterMode.Point, importer.filterMode);
                    Assert.AreEqual(UnityEditor.TextureImporterCompression.Uncompressed, importer.textureCompression);
                }
            }
        }
    }

    [TestCase(1f, 0f, 0f)]
    [TestCase(0f, 1f, 90f)]
    [TestCase(-1f, 0f, 180f)]
    [TestCase(0f, -1f, -90f)]
    [TestCase(1f, 0.2f, 0f)]
    [TestCase(1f, 1f, 45f)]
    [TestCase(-1f, 1f, 135f)]
    [TestCase(-1f, -1f, -135f)]
    [TestCase(1f, -1f, -45f)]
    public void ProjectileRotationSelectsEightDirections(float x, float y, float expected)
    {
        Assert.AreEqual(expected, CombatEffectMotion.GetProjectileAngle(new Vector2(x, y)));
    }

    [TestCase(0f, 0)]
    [TestCase(0.3f, 1)]
    [TestCase(2f, 2)]
    [TestCase(2.2f, 3)]
    public void LightningSegmentsCoverTheWholeLine(float length, int expectedCount)
    {
        var from = new Vector2(1f, 2f);
        var to = from + new Vector2(0.6f, -0.8f) * length;
        var count = CombatEffectMotion.GetSegmentCount(length, 1f);
        Assert.AreEqual(expectedCount, count);
        var end = from;
        for (var i = 0; i < count; i++)
        {
            CombatEffectMotion.GetSegment(from, to, 1f, i, out var center, out var scale);
            var half = (to - from).normalized * scale * 0.5f;
            Assert.Less(Vector2.Distance(end, center - half), 0.00001f);
            end = center + half;
        }

        Assert.Less(Vector2.Distance(to, end), 0.00001f);
    }

    [Test]
    public void PooledProjectileViewFacesAllEightDirectionsAndResetsFlip()
    {
        var pool = new PoolManager();
        var parent = new GameObject("ProjectileDirectionTest");
        var prefab = Resources.Load<ProjectileView>("Prefab/ProjectileView");
        var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Game/03.Resources/Art/HD2D/Effects/arrow_right_1.png");
        ProjectileView previous = null;
        try
        {
            // 부호가 바뀌는 순서로 같은 View를 재사용하여 반전 잔류도 검사한다.
            foreach (var degrees in new[] { 135f, 45f, -135f, -45f, 180f, 0f, -90f, 90f })
            {
                var direction = (Vector2)(Quaternion.Euler(0f, 0f, degrees) * Vector3.right);
                var view = pool.Get("ProjectileDirectionTest", prefab, parent.transform);
                view.Initialize(sprite);
                view.UpdateView(new Vector2(2f, 3f), direction);
                var facing = EffectFrameMath.DirectionSprite(degrees);
                var expectedDirection = EffectFrameMath.DirectionName(facing.index);
                var directionSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"Assets/Game/03.Resources/Art/HD2D/Effects/arrow_{expectedDirection}_1.png");
                view.SetFrame(directionSprite, "arrow", 1, false);
                view.UpdateView(new Vector3(2f, 0f, 3f), direction);
                Assert.AreEqual($"arrow_{expectedDirection}_1", view.BodyRenderer.sprite.name);
                Assert.AreEqual(degrees > 90f || degrees <= -135f || degrees == 180f, view.BodyRenderer.flipX, degrees.ToString());
                Assert.AreEqual(degrees < 0f, view.BodyRenderer.flipY, degrees.ToString());
                Assert.Less(Quaternion.Angle(Quaternion.identity, view.BodyRenderer.transform.localRotation), .001f);
                var shadow = view.transform.Find("Shadow");
                Assert.AreEqual(new Vector3(1, 2, 1), shadow.localScale);
                Assert.AreEqual(0f, shadow.localEulerAngles.y);
                var position = view.transform.localPosition;
                Assert.That(Vector3.Distance(position, new Vector3(2f, 0f, 3f)), Is.LessThan(.0001f),
                    "Physical root stays on the floor; only the visual card is pixel-snapped.");
                if (previous != null)
                {
                    Assert.AreSame(previous, view);
                }

                pool.Release("ProjectileDirectionTest", view);
                previous = view;
            }
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }

    [TestCase(81f, 0f)]
    [TestCase(81f, 33f)]
    [TestCase(-81f, 33f)]
    [TestCase(1f, 1f)]
    public void LightningViewBoundariesRemainJoinedAfterPlacement(float xPixels, float yPixels)
    {
        var image = ChainBoltRaster.Draw((int)xPixels, (int)yPixels, 7, 0);
        var ox = xPixels >= 0 ? 10 : image.Width - 10;
        var oy = yPixels >= 0 ? 10 : image.Height - 10;
        Assert.AreEqual(255, image.Pixels[oy * image.Width + ox].a);
        Assert.AreEqual(255, image.Pixels[(oy + (int)yPixels) * image.Width + ox + (int)xPixels].a);
    }

    [Test]
    public void ChainRecordsActualHopOrderAndStopsWithoutAnotherTarget()
    {
        var context = new TestRunContext();
        context.SetupParty(new[] { "wand" });
        var origin = context.PartyModel.MemberList[0].Position;
        var first = context.AddMonster(1, origin + new Vector2(60f, 0f));
        var second = context.AddMonster(2, origin + new Vector2(180f, 0f));
        context.CombatService.Step(0f);
        context.StepCombat(6, context.RunRules.SimStep); // 브루노 앞 hit_frame 2 / 10fps
        var bursts = context.CombatModel.BurstList;
        Assert.AreEqual(2, bursts.Count);
        Assert.AreEqual(origin, bursts[0].Position);
        Assert.AreEqual(first.Position, bursts[0].EndPosition);
        Assert.AreEqual(first.Position, bursts[1].Position);
        Assert.AreEqual(second.Position, bursts[1].EndPosition);
        Assert.IsTrue(bursts.All(burst => burst.ItemId == "wand" && burst.Kind == BurstEffectKind.Chain));
    }

    [Test]
    public void HitNotificationDoesNotConsumeDamageNumberRecords()
    {
        var context = new TestRunContext();
        context.SetupParty(new[] { "sword" });
        var monster = context.AddMonster(1, Vector2.zero);
        var notifications = 0;
        context.CombatFeedbackModel.MonsterHit += _ => notifications++;
        context.MonsterService.ApplyDamage(monster, 10f);
        Assert.AreEqual(1, notifications);
        Assert.AreEqual(1, context.CombatFeedbackModel.MonsterHitList.Count);
        Assert.AreEqual(10f, context.CombatFeedbackModel.MonsterHitList[0].Amount);
    }
    [Test]
    public void LegacySingleSpriteDataDefaultsToOneUnsuffixedFrameWithoutActiveLegacyReferences()
    {
        var data = JsonUtility.FromJson<CombatEffectData>("{\"items\":[{\"itemId\":\"sword\",\"burst\":\"shadow\"}]}");
        var clip = data.GetClip(data.Get("sword").Burst);
        Assert.AreEqual(1, clip.Frames);
        Assert.AreEqual("shadow", clip.FrameKey(0));
        Assert.AreEqual(0, EffectFrameMath.FrameAt(100, clip.Frames, clip.Fps, clip.Loop));
        var resolved = Resources.Load<UnityEngine.U2D.SpriteAtlas>("Art/Characters").GetSprite(clip.FrameKey(0));
        Assert.IsNotNull(resolved);
        Object.DestroyImmediate(resolved);
        var omitted = JsonUtility.FromJson<CombatEffectClipData>("{\"key\":\"still\",\"sprite\":\"shadow\"}");
        Assert.AreEqual(1, omitted.Frames);
        Assert.AreEqual("shadow", omitted.FrameKey(0));
    }

    [TestCase("arrow")]
    [TestCase("crossbow_bolt")]
    [TestCase("fireball")]
    public void ProjectileCornersStayOnNativeGridForEveryBakedDirectionAndFlip(string key)
    {
        var view = Object.Instantiate(Resources.Load<ProjectileView>("Prefab/ProjectileView"));
        var rotation = Quaternion.Euler(18, 0, 0);
        var projection = new PerspectiveFrame(new Vector3(0, .95f, 0) - rotation * Vector3.forward * TiltedCameraRig.DQDistance,
            rotation, 20, 16f / 9f, 1080);
        try
        {
            var shadow = view.transform.Find("Shadow");
            var shadowLocal = shadow.localPosition;
            foreach (var degrees in new[] { 0f, 45f, 90f, 135f, 180f, -135f, -90f, -45f })
            {
                var facing = EffectFrameMath.DirectionSprite(degrees);
                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"Assets/Game/03.Resources/Art/HD2D/Effects/{key}_{EffectFrameMath.DirectionName(facing.index)}_1.png");
                view.Initialize(sprite);
                view.SetFrame(sprite, key, 1, key == "fireball");
                var direction = (Vector2)(Quaternion.Euler(0, 0, degrees) * Vector3.right);
                var position = new Vector3(2.017f, 0, -3.013f);
                view.UpdateView(position, direction, projection);
                Assert.AreEqual(position, view.transform.localPosition);
                Assert.AreEqual(shadowLocal, shadow.localPosition);
                foreach (var vertex in sprite.vertices)
                {
                    var local = new Vector3(facing.flipX ? -vertex.x : vertex.x, facing.flipY ? -vertex.y : vertex.y, 0);
                    var world = view.BodyRenderer.transform.TransformPoint(local);
                    var projected = projection.Project(world);
                    var pitch = projection.FocalPixels / (projected.z * sprite.pixelsPerUnit);
                    var pixels = (Vector2)projected / pitch;
                    Assert.Less(Vector2.Distance(pixels, new Vector2(Mathf.Round(pixels.x), Mathf.Round(pixels.y))), .0001f,
                        $"{sprite.name} angle={degrees} corner={pixels}");
                }
                var first = view.BodyRenderer.transform.position;
                view.UpdateView(position, direction, projection);
                Assert.AreEqual(first, view.BodyRenderer.transform.position, "snap must not accumulate");
            }
        }
        finally
        {
            Object.DestroyImmediate(view.gameObject);
        }
    }
    [Test]
    public void P1_FirstFrameAndReusedProjectileUseTheLocalTerrainTangent()
    {
        var grid = TerrainHeightMathTests.Plane(0, .2f);
        var floor = new Vector2(.2f, .3f);
        var direction = Vector2.one.normalized;
        var a = IsoProjection.FloorToWorld(floor, 1, grid);
        var b = IsoProjection.FloorToWorld(floor + direction * .25f, 1, grid);
        var tangent = TiltedViewMath.WorldToScreenPlane(b - a, .5f);
        Assert.AreEqual(34f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg, .1f);
        var facing = EffectFrameMath.DirectionSprite(Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
        var view = Object.Instantiate(Resources.Load<ProjectileView>("Prefab/ProjectileView"));
        try
        {
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/Game/03.Resources/Art/HD2D/Effects/arrow_{EffectFrameMath.DirectionName(facing.index)}_1.png");
            foreach (var position in new[] { floor, -floor })
            {
                view.Initialize(sprite);
                view.SetFrame(sprite, "arrow", 1, false);
                var foot = IsoProjection.FloorToWorldSnapped(position, 1, .5f, grid);
                view.UpdateView(foot, tangent);
                Assert.AreEqual(facing.flipX, view.BodyRenderer.flipX);
                Assert.AreEqual(facing.flipY, view.BodyRenderer.flipY);
                Assert.Less(Vector3.Distance(foot, view.transform.position), 1e-6f);
            }
        }
        finally
        {
            Object.DestroyImmediate(view.gameObject);
        }
    }
    [TestCase("slash_sword")]
    [TestCase("slash_axe")]
    public void SlashMovesOnlyItsPictureBehindBodiesAndPoolReuseClearsTheDepth(string key)
    {
        var view = Object.Instantiate(Resources.Load<BurstView>("Prefab/BurstView"));
        var frame = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/HD2D/Effects/ground_boom_1.png");
        var position = new Vector3(1, 0, 2);
        try
        {
            view.Initialize(frame);
            view.UpdateView(position, frame, key, 1, 0);
            var root = view.transform.position;
            Assert.AreEqual(new Vector3(0, 0, .6f), view.BodyRenderer.transform.localPosition);
            var displacement = view.BodyRenderer.transform.position - root;
            var cameraForward = Quaternion.Euler(18, 0, 0) * Vector3.forward;
            Assert.Less(Vector3.Cross(cameraForward, displacement).magnitude, 1e-5f);
            Assert.Greater(Vector3.Dot(cameraForward, displacement), .59f);
            Assert.IsTrue(view.IsShown);
            Assert.AreEqual(1, view.Alpha);
            view.UpdateView(position, frame, "explosion", 1, 0);
            Assert.AreEqual(root, view.transform.position);
            Assert.AreEqual(Vector3.zero, view.BodyRenderer.transform.localPosition);
            view.UpdateView(position, frame, key, 1, 0);
            view.Initialize(frame);
            Assert.AreEqual(Vector3.zero, view.BodyRenderer.transform.localPosition);
        }
        finally
        {
            Object.DestroyImmediate(view.gameObject);
        }
    }

    [Test]
    public void E1_GroundBurstReportsTheActiveConformedRendererAndReuseRestoresSprite()
    {
        var view = Object.Instantiate(Resources.Load<BurstView>("Prefab/BurstView"));
        var frame = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/HD2D/Effects/ground_boom_1.png");
        try
        {
            view.Initialize(frame);
            view.SetTerrain(TerrainHeightMathTests.Plane(.2f, 0));
            view.UpdateView(new Vector3(.2f,.04f,.3f), frame, "ground_boom", 1, 5, false, true);
            var source = (IEffectFrameSource)view;
            Assert.IsTrue(source.IsShown);
            Assert.AreSame(frame, source.SourceSprite);
            Assert.IsInstanceOf<MeshRenderer>(source.ActiveRenderer);
            Assert.IsFalse(view.BodyRenderer.enabled);
            Assert.AreEqual(1996, source.ActiveRenderer.sharedMaterial.renderQueue);
            view.UpdateView(new Vector3(.2f,.04f,.3f), frame, "explosion", 2, 1);
            Assert.AreSame(view.BodyRenderer, source.ActiveRenderer);
            Assert.IsTrue(view.BodyRenderer.enabled);
            Assert.AreEqual(2, source.FrameNumber);
        }
        finally
        {
            Object.DestroyImmediate(view.gameObject);
        }
    }
}

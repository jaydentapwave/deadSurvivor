using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

// Presentation fixture only: invokes the production view/cache/atlas contracts on Main.
// Representative 1m rings and 4m chain are not proof of every item's damage radius.
public static class DQFinishEffectsVerification
{
    public static void Capture()
    {
        if (!Application.isPlaying || PlayableProbe.Screen != "Battle")
        {
            throw new InvalidOperationException("Effect fixture requires Main Battle.");
        }
        const string directory = "output/hd2d-dq/apply/9/effects";
        var container = Object.FindAnyObjectByType<GameSceneLifetimeScope>().Container;
        var atlas = (SpriteAtlasService)container.Resolve(typeof(SpriteAtlasService));
        var effects = (CombatEffectData)container.Resolve(typeof(CombatEffectData));
        var cache = (EffectSpriteCache)container.Resolve(typeof(EffectSpriteCache));
        var terrain = (TerrainHeightGrid)container.Resolve(typeof(TerrainHeightGrid));
        var viewRules = (ViewRulesData)container.Resolve(typeof(ViewRulesData));
        var rules = (RunRulesData)container.Resolve(typeof(RunRulesData));
        var previousOutput = DQBackgroundVerification.Output;
        var previousTime = Time.timeScale;
        var root = new GameObject("DQ finish effects fixture");
        var rows = new List<object>();
        var party = new Vector2(PlayableProbe.PartyX, PlayableProbe.PartyY) * rules.UnitScale;
        var views = new List<(CombatEffectClipData clip, BurstView burst, ProjectileView projectile, EnemyShotView enemy, Vector3 position)>();
        try
        {
            Time.timeScale = 0;
            DQBackgroundVerification.Output = directory + "/frames";
            for (var i = 0; i < effects.Clips.Count; i++)
            {
                var clip = effects.Clips[i];
                var floor = party + new Vector2((i % 5 - 2) * 2.2f, (i / 5 + 1) * 3f);
                var position = new Vector3(floor.x, TerrainHeightMath.Sample(terrain, floor.x, floor.y), floor.y);
                var sprite = atlas.Get(clip.FrameKey(0));
                var projectile = clip.Key == "arrow" || clip.Key == "crossbow_bolt" || clip.Key == "fireball";
                if (clip.Key == "spore")
                {
                    var view = Object.Instantiate(Resources.Load<EnemyShotView>("Prefab/EnemyShotView"), root.transform);
                    view.Initialize(Enumerable.Range(0, clip.Frames).Select(f => atlas.Get(clip.FrameKey(f))).ToArray(), clip.Fps);
                    view.SetTerrain(terrain); view.gameObject.SetActive(false);
                    views.Add((clip, null, null, view, position));
                }
                else if (projectile)
                {
                    var view = Object.Instantiate(Resources.Load<ProjectileView>("Prefab/ProjectileView"), root.transform);
                    view.Initialize(sprite); view.SetTerrain(terrain); view.gameObject.SetActive(false);
                    views.Add((clip, null, view, null, position));
                }
                else
                {
                    var view = Object.Instantiate(Resources.Load<BurstView>("Prefab/BurstView"), root.transform);
                    view.Initialize(sprite); view.SetTerrain(terrain); view.gameObject.SetActive(false);
                    views.Add((clip, view, null, null, position));
                }
            }
            DQBackgroundVerification.Capture("effects-off", 1920, 1080);
            for (var frame = 0; frame < effects.Clips.Max(c => c.Frames); frame++)
            {
                foreach (var item in views)
                {
                    var clip = item.clip;
                    var count = clip.Key == "dash_line" ? 4 : clip.Frames;
                    var visible = frame < count;
                    var go = item.burst != null ? item.burst.gameObject : item.projectile != null ? item.projectile.gameObject : item.enemy.gameObject;
                    go.SetActive(visible);
                    if (!visible) continue;
                    var sprite = atlas.Get(clip.FrameKey(Math.Min(frame, clip.Frames - 1)));
                    var ground = clip.Key.StartsWith("ground_", StringComparison.Ordinal) || clip.Key == "dash_line" || clip.Key == "heal_area" || clip.Key == "pulse";
                    if (ground)
                    {
                        sprite = clip.Key == "dash_line" ? cache.DQDash(3.6386, .3164, 0, frame, -99999)
                            : cache.Ring(clip.Key == "heal_area" ? "heal" : clip.Key == "pulse" ? "pulse" : clip.Key.Substring(7), 1, frame);
                    }
                    else if (clip.Key == "lightning")
                    {
                        sprite = cache.Chain(192, 0, 99999, frame);
                    }
                    if (item.burst != null)
                    {
                        item.burst.UpdateView(item.position, sprite, clip.Key, frame + 1, clip.MaterialIndex,
                            false, ground, viewRules.SinTilt, viewRules.Frame);
                    }
                    else if (item.projectile != null)
                    {
                        item.projectile.SetFrame(sprite, clip.Key, frame + 1, clip.MaterialIndex == 1);
                        item.projectile.UpdateView(item.position, Vector2.right, viewRules.Frame);
                    }
                    if (item.enemy != null) item.enemy.UpdateView(item.position, (frame + .1f) / clip.Fps);
                    var renderer = item.burst != null ? item.burst.ActiveRenderer : item.projectile != null ? item.projectile.ActiveRenderer : item.enemy.ActiveRenderer;
                    var bounds = renderer.bounds;
                    var camera = Camera.main;
                    var corners = (from x in new[] { bounds.min.x, bounds.max.x }
                        from y in new[] { bounds.min.y, bounds.max.y }
                        from z in new[] { bounds.min.z, bounds.max.z }
                        select camera.WorldToViewportPoint(new Vector3(x, y, z))).ToArray();
                    rows.Add(new { key = clip.Key, frame = frame + 1, plane = ground ? "terrain" : "standing18",
                        sprite = sprite.name, material = renderer.sharedMaterial.name,
                        position = new[] { item.position.x, item.position.y, item.position.z },
                        box = new[] { corners.Min(p => p.x) * 1920, (1 - corners.Max(p => p.y)) * 1080,
                            corners.Max(p => p.x) * 1920, (1 - corners.Min(p => p.y)) * 1080 },
                        spriteId = sprite.GetInstanceID(), meshRenderer = renderer.GetInstanceID() });
                }
                DQBackgroundVerification.Capture("effects-" + frame, 1920, 1080);
            }
            File.WriteAllText(directory + "/manifest.json", JsonConvert.SerializeObject(new
            {
                utc = DateTime.UtcNow.ToString("O"), engineFrame = Time.frameCount, timeScale = Time.timeScale,
                fixture = "Main production view paths. Grid positions at three depths; 1m representative rings, actual boar dimensions, 4m representative chain. Not damage/spawn/progression evidence.",
                clips = effects.Clips.Count, rows
            }, Formatting.Indented));
        }
        finally
        {
            foreach (var item in views)
            {
                if (item.burst != null) item.burst.gameObject.SetActive(false);
                if (item.projectile != null) item.projectile.gameObject.SetActive(false);
                if (item.enemy != null) item.enemy.gameObject.SetActive(false);
            }
            Object.Destroy(root);
            cache.ReleaseDQDash(-99999); cache.ReleaseChain(99999);
            Time.timeScale = previousTime;
            DQBackgroundVerification.Output = previousOutput;
        }
    }
}

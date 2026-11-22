#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

// Character observation option of the existing PlayableProbe capture workflow.
// It observes production views; it never sets victory, damage, phase or cooldowns.
public static partial class PlayableProbe
{
    public static int DQHitsRendered => Resolve<WorldPresenter>().DQHitsRendered;
    public static int DQHitsPending => Resolve<WorldPresenter>().DQHitsPending;
    public static int DQDeathsPending => Resolve<WorldPresenter>().DQDeathCount;
    public static string DQHitObservations => Resolve<WorldPresenter>().DQHitObservations;
    public static bool IsDQSequenceReady => Object.FindAnyObjectByType<PlayableDQCharacterSampler>()?.IsCaptured == true;

    public static IEnumerable<DQActorObservation> DQActors()
    {
        foreach (var view in Resolve<IReadOnlyList<MemberView>>())
        {
            if (view.gameObject.activeInHierarchy)
            {
                yield return new DQActorObservation(view.DQActorId, "hero", view.DQAction,
                    view.Facing.Label, view.DQFrame, view.BodyRenderer, view.transform.position);
            }
        }
        foreach (var view in Resolve<MonsterViewRegistry>().Views.Concat(Resolve<WorldPresenter>().DQDeathViews))
        {
            if (view.gameObject.activeInHierarchy)
            {
                yield return new DQActorObservation(view.DQActorId, "monster", view.DQAction,
                    view.Facing.Label, view.DQFrame, view.BodyRenderer, view.transform.position);
            }
        }
    }

    public static string DQCharacterState => JsonUtility.ToJson(new DQCharacterSnapshot
    {
        unityFrame = Time.frameCount, gameTime = Time.time, phase = Screen, fallback = FacingFallbackCount,
        hitsRendered = DQHitsRendered, hitsPending = DQHitsPending, deathsPending = DQDeathsPending,
        actors = DQActors().ToArray()
    }, true);

    public static void ArmDQCharacterSequence(string directory, float seconds = 40)
    {
        var sampler = Object.FindAnyObjectByType<PlayableDQCharacterSampler>();
        if (sampler == null)
        {
            sampler = new GameObject(nameof(PlayableDQCharacterSampler)).AddComponent<PlayableDQCharacterSampler>();
        }
        sampler.Arm(directory, seconds);
    }
}

[Serializable]
public class DQCharacterSnapshot
{
    public int unityFrame;
    public float gameTime;
    public string phase;
    public int fallback;
    public int hitsRendered;
    public int hitsPending;
    public int deathsPending;
    public DQActorObservation[] actors;
}

[Serializable]
public class DQActorObservation
{
    public string actor;
    public string kind;
    public string action;
    public string facing;
    public int frame;
    public string sprite;
    public Vector3 root;
    public Vector3 body;
    public float ppu;
    public Vector2 pivot;
    public string material;
    [NonSerialized] public SpriteRenderer Renderer;

    public DQActorObservation(string actorId, string type, string clip, string direction, int index, SpriteRenderer renderer, Vector3 foot)
    {
        actor = actorId;
        kind = type;
        action = clip == "attack-hit" ? "attack" : clip;
        facing = direction;
        frame = index;
        sprite = renderer.sprite.name.Replace("(Clone)", "");
        root = foot;
        body = renderer.transform.position;
        ppu = renderer.sprite.pixelsPerUnit;
        pivot = renderer.sprite.pivot;
        material = renderer.sharedMaterial.name;
        Renderer = renderer;
    }
}

[DefaultExecutionOrder(11000)]
public class PlayableDQCharacterSampler : MonoBehaviour
{
    public bool IsCaptured { get; private set; }
    private float _oldCaptureDelta;
    private float _oldScale;
    private bool _oldBackground;
    private bool _armed;
    private string _directory;
    private readonly Dictionary<string, (SpriteRenderer renderer, int count)> _caseDict = new();
    private readonly List<string> _rows = new();

    public void Arm(string directory, float seconds)
    {
        StopAllCoroutines();
        Restore();
        _directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
        _oldCaptureDelta = Time.captureDeltaTime;
        _oldScale = Time.timeScale;
        _oldBackground = Application.runInBackground;
        _armed = true;
        IsCaptured = false;
        _caseDict.Clear();
        _rows.Clear();
        Time.captureDeltaTime = .04f;
        Time.timeScale = 1;
        Application.runInBackground = true;
        StartCoroutine(Capture(seconds));
    }

    private IEnumerator Capture(float seconds)
    {
        var ticks = Mathf.CeilToInt(seconds / .04f);
        var camera = PlayableProbe.EffectCamera;
        for (var tick = 0; _armed && tick < ticks; tick++)
        {
            yield return new WaitForEndOfFrame();
            var selected = new List<(DQActorObservation actor, string key, int index, RectInt rect)>();
            foreach (var actor in PlayableProbe.DQActors())
            {
                if (actor.actor == null || actor.action == null || actor.action == "static")
                {
                    continue;
                }
                var key = actor.actor + "-" + actor.action + "-" + (actor.action == "fall" ? "shared" : actor.facing);
                _caseDict.TryGetValue(key, out var current);
                var max = actor.action == "fall" ? 70 : 12;
                if (current.count >= max || (current.renderer != null && current.renderer != actor.Renderer))
                {
                    continue;
                }
                var rect = CanvasRect(actor.Renderer, camera);
                if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > Screen.width || rect.yMax > Screen.height)
                {
                    continue;
                }
                selected.Add((actor, key, current.count, rect));
                _caseDict[key] = (actor.Renderer, current.count + 1);
            }
            if (selected.Count == 0)
            {
                continue;
            }
            var full = ScreenCapture.CaptureScreenshotAsTexture();
            foreach (var item in selected)
            {
                var folder = Path.Combine(_directory, item.key);
                Directory.CreateDirectory(folder);
                var rect = item.rect;
                var crop = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
                crop.SetPixels(full.GetPixels(rect.x, rect.y, rect.width, rect.height));
                crop.Apply();
                var name = item.index.ToString("D3");
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), crop.EncodeToPNG());
                // Exact integer nearest-neighbour crop, not a second camera rendering.
                var enlarged = new Texture2D(rect.width * 4, rect.height * 4, TextureFormat.RGBA32, false);
                var original = crop.GetPixels32();
                var pixels = new Color32[enlarged.width * enlarged.height];
                for (var y = 0; y < enlarged.height; y++)
                {
                    for (var x = 0; x < enlarged.width; x++)
                    {
                        pixels[y * enlarged.width + x] = original[(y / 4) * rect.width + x / 4];
                    }
                }
                enlarged.SetPixels32(pixels);
                enlarged.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + "-4x.png"), enlarged.EncodeToPNG());
                var foot = camera.WorldToScreenPoint(item.actor.root);
                _rows.Add(JsonUtility.ToJson(new DQSampleRecord
                {
                    unityFrame = Time.frameCount, time = Time.time, sample = item.index, key = item.key,
                    crop = new[] { rect.x, rect.y, rect.width, rect.height }, screen = new[] { Screen.width, Screen.height },
                    foot = foot, observation = item.actor
                }));
                Object.Destroy(enlarged);
                Object.Destroy(crop);
            }
            Object.Destroy(full);
        }
        File.WriteAllLines(Path.Combine(_directory, "frames.jsonl"), _rows);
        File.WriteAllText(Path.Combine(_directory, "state.json"), PlayableProbe.DQCharacterState);
        File.WriteAllText(Path.Combine(_directory, "hits.csv"), PlayableProbe.DQHitObservations);
        Restore();
        Time.timeScale = 0;
        IsCaptured = true;
    }

    private static RectInt CanvasRect(SpriteRenderer body, Camera camera)
    {
        var rect = body.sprite.rect;
        var pivot = body.sprite.pivot;
        var corners = new[] { Vector2.zero, rect.size, new Vector2(0, rect.height), new Vector2(rect.width, 0) }
            .Select(p => (p - pivot) / body.sprite.pixelsPerUnit)
            .Select(p => body.transform.TransformPoint(new Vector3(body.flipX ? -p.x : p.x, p.y, 0)))
            .Select(camera.WorldToScreenPoint).ToArray();
        var left = Mathf.FloorToInt(corners.Min(p => p.x)) - 8;
        var bottom = Mathf.FloorToInt(corners.Min(p => p.y)) - 8;
        return new RectInt(left, bottom, Mathf.CeilToInt(corners.Max(p => p.x)) + 8 - left,
            Mathf.CeilToInt(corners.Max(p => p.y)) + 8 - bottom);
    }

    private void Restore()
    {
        if (!_armed)
        {
            return;
        }
        Time.captureDeltaTime = _oldCaptureDelta;
        Time.timeScale = _oldScale;
        Application.runInBackground = _oldBackground;
        _armed = false;
    }
    private void OnDestroy() => Restore();
}

[Serializable]
public class DQSampleRecord
{
    public int unityFrame;
    public float time;
    public int sample;
    public string key;
    public int[] crop;
    public int[] screen;
    public Vector3 foot;
    public DQActorObservation observation;
}
#endif

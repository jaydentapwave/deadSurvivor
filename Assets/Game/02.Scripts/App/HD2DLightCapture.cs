#if UNITY_EDITOR
using System;
using UnityEngine;

// Capture fixture only; no game rule depends on this editor-only component.
[DefaultExecutionOrder(32000)]
public sealed class HD2DLightCapture : MonoBehaviour
{
    public static bool IsFrozen { get; private set; }
    public static int WaveFrame { get; private set; }
    private int _targetWave;
    private int _targetFrame;
    private float _oldCaptureDelta;

    public static void Arm(int wave, int frame)
    {
        if (!Application.isPlaying || wave < 1 || frame < 1)
        {
            throw new InvalidOperationException("Owned Main Play and positive wave/frame required.");
        }
        var sampler = FindAnyObjectByType<HD2DLightCapture>();
        if (sampler == null)
        {
            sampler = new GameObject("Light exact-frame capture fixture").AddComponent<HD2DLightCapture>();
            sampler._oldCaptureDelta = Time.captureDeltaTime;
        }
        sampler._targetWave = wave;
        sampler._targetFrame = frame;
        WaveFrame = 0;
        IsFrozen = false;
        Time.captureDeltaTime = 1f / 30f;
        Time.timeScale = 1f;
    }

    private void LateUpdate()
    {
        if (IsFrozen || PlayableProbe.Screen != "Battle" || PlayableProbe.Wave != _targetWave)
        {
            return;
        }
        WaveFrame++;
        if (_targetWave > 1)
        {
            Time.timeScale = 1f;
            if (WaveFrame == 1)
            {
                // Visual fixture, not a progression test: use real combat/presenters
                // against durable nearby targets so numbers and HP coexist.
                var monsters = PlayableProbe.Resolve<MonsterModel>();
                var data = PlayableProbe.Resolve<MonsterRepository>().Get("slime");
                var center = PlayableProbe.Resolve<PartyModel>().Center;
                monsters.Add(new Monster(9000001, data, center + new Vector2(-100, -30), 5000, 0, 0));
                monsters.Add(new Monster(9000002, data, center + new Vector2(100, -30), 5000, 0, 0));
            }
        }
        var hasFeedback = _targetWave == 1 || (PlayableProbe.DamageNumberCount > 0 && PlayableProbe.MonsterHpBarCount > 0);
        if (WaveFrame >= _targetFrame && hasFeedback)
        {
            PlayableProbe.StopBot();
            PlayableProbe.SetMove(0, 0);
            Time.timeScale = 0;
            IsFrozen = true;
        }
    }

    private void OnDestroy()
    {
        Time.captureDeltaTime = _oldCaptureDelta;
        IsFrozen = false;
    }
}

#endif

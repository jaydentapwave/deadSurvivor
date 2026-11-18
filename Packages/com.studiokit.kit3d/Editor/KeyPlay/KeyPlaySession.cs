using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Kit3D
{
    // Real key input for an automatic play-through: a virtual keyboard takes the same input path as a player, so the probe never moves
    // the character or calls game methods. No project values here: the scenario (route, target, pass condition) stays in the project's
    // probe file, run by `play_check.py short <Class.Method> <result file> --probe-file <file> --probe-dir <dir> --pass '^fails=none'`.
    // This assembly compiles only when com.unity.inputsystem is installed (see the asmdef).
    //
    //   KeyPlaySession session = null;
    //   session = KeyPlaySession.Start(() =>
    //   {
    //       var keys = new List<Key>(KeyPlaySession.PickMoveKeys(target - player, camera.transform.eulerAngles.y));
    //       session.Send(keys);
    //   }, () => session.Stop());
    public sealed class KeyPlaySession
    {
        private const string DeviceName = "ProbeKeyboard";
        // The first key event right after Play can be lost (seen right after an Editor recompile), so the same keys are sent again now and then.
        private const int KeyResendFrames = 15;

        private static readonly (Vector2 input, Key[] keys)[] MoveKeyList =
        {
            (new Vector2(0f, 1f), new[] { Key.W }),
            (new Vector2(1f, 1f).normalized, new[] { Key.W, Key.D }),
            (new Vector2(1f, 0f), new[] { Key.D }),
            (new Vector2(1f, -1f).normalized, new[] { Key.S, Key.D }),
            (new Vector2(0f, -1f), new[] { Key.S }),
            (new Vector2(-1f, -1f).normalized, new[] { Key.S, Key.A }),
            (new Vector2(-1f, 0f), new[] { Key.A }),
            (new Vector2(-1f, 1f).normalized, new[] { Key.W, Key.A }),
        };

        private readonly InputSettings _settings;
        private readonly InputSettings.BackgroundBehavior _oldBackground;
        private readonly InputSettings.EditorInputBehaviorInPlayMode _oldEditorInput;
        private readonly Keyboard _keyboard;
        private readonly Action _onFrame;
        private readonly Action _onPlayEnded;
        private Key[] _sentKeys;
        private bool _isStopped;

        private KeyPlaySession(Action onFrame, Action onPlayEnded)
        {
            _onFrame = onFrame;
            _onPlayEnded = onPlayEnded;
            _settings = InputSystem.settings;
            _oldBackground = _settings.backgroundBehavior;
            _oldEditorInput = _settings.editorInputBehaviorInPlayMode;
            _settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>(DeviceName);
            InputSystem.onBeforeUpdate += OnBeforeUpdate;
        }

        private void OnBeforeUpdate()
        {
            if (_isStopped || InputState.currentUpdateType != InputUpdateType.Dynamic)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                _onPlayEnded();
                return;
            }

            _onFrame();
        }

        // onFrame runs once per game frame, before the game reads input. onPlayEnded runs if Play stops first; it must call Stop.
        public static KeyPlaySession Start(Action onFrame, Action onPlayEnded) => new KeyPlaySession(onFrame, onPlayEnded);

        // Holds exactly these keys until the next Send.
        public void Send(IEnumerable<Key> keys)
        {
            var keyArray = keys.ToArray();
            if (_sentKeys == null || !_sentKeys.SequenceEqual(keyArray) || Time.frameCount % KeyResendFrames == 0)
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keyArray));
                _sentKeys = keyArray;
            }
        }

        // Removes the keyboard and restores the input settings. Safe to call twice.
        public void Stop()
        {
            if (_isStopped)
            {
                return;
            }

            _isStopped = true;
            InputSystem.onBeforeUpdate -= OnBeforeUpdate;
            InputSystem.RemoveDevice(_keyboard);
            _settings.backgroundBehavior = _oldBackground;
            _settings.editorInputBehaviorInPlayMode = _oldEditorInput;
        }

        public static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);

        // The WASD keys of the eight directions nearest to a world direction, for a game that moves relative to the camera's yaw.
        public static Key[] PickMoveKeys(Vector3 direction, float cameraYaw)
        {
            var best = MoveKeyList[0].keys;
            var bestDot = float.MinValue;
            var rotation = Quaternion.Euler(0f, cameraYaw, 0f);
            foreach (var (input, keys) in MoveKeyList)
            {
                var world = rotation * new Vector3(input.x, 0f, input.y);
                var dot = Vector3.Dot(world, direction.normalized);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = keys;
                }
            }

            return best;
        }

        // Renders the camera to a JPG, whether or not the Game view has focus.
        public static void Capture(Camera camera, string path, int width = 1280, int height = 720)
        {
            var renderTexture = RenderTexture.GetTemporary(width, height, 24);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(path, texture.EncodeToJPG(85));
            UnityEngine.Object.DestroyImmediate(texture);
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }
}

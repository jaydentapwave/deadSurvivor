using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace DeadSurvivor.Editor
{
    // Temporary GameView sizes only; never save editor preferences to disk.
    public static class DSScreenVerification
    {
        static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static EditorWindow view, previousFocus, previousPlayView;
        static EditorWindow[] simulators;
        static string[] simulatorStates;
        static int resizeFrame;
        static string SimulatorState(EditorWindow window) => window.GetType().GetField("m_State", Flags).GetValue(window).ToString();
        static void UpdateSimulators() { foreach (var window in simulators) window.GetType().GetMethod("Update", Flags).Invoke(window, null); }
        static object group;
        static int originalIndex, customIndex = -1;
        static PropertyInfo selected;
        static string[] originalLabels;
        static void Select(int index) => view.GetType().GetMethod("SizeSelectionCallback", Flags).Invoke(view, new object[] { index, null });
        static string[] Labels() => (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
        public static string Resize(int width, int height)
        {
            if (view == null)
            {
                previousFocus = EditorWindow.focusedWindow;
                var playView = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PlayModeView");
                previousPlayView = (EditorWindow)playView.GetMethod("GetMainPlayModeView", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, null);
                simulators = Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.GetType().Name == "SimulatorWindow").ToArray();
                if (simulators.Length > 1) throw new Exception("Multiple simulators: exact shim restoration unsupported");
                simulatorStates = simulators.Select(SimulatorState).ToArray();
                var assembly = typeof(UnityEditor.Editor).Assembly;
                var type = assembly.GetType("UnityEditor.GameView");
                view = Resources.FindObjectsOfTypeAll(type).Cast<EditorWindow>().First();
                selected = type.GetProperty("selectedSizeIndex", Flags);
                originalIndex = (int)selected.GetValue(view);
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                group = sizesType.GetProperty("currentGroup", Flags).GetValue(sizes);
                originalLabels = Labels();
            }
            RemoveTemporary();
            var a = typeof(UnityEditor.Editor).Assembly;
            var kind = Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
            var size = Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"), kind, width, height, "DS temporary verification");
            int builtIn = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            customIndex = builtIn + (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            Select(customIndex);
            view.Focus();
            UpdateSimulators();
            resizeFrame = Time.frameCount;
            view.Repaint();
            return "RESIZED";
        }
        static void RemoveTemporary()
        {
            if (customIndex < 0) return;
            Select(originalIndex);
            group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { customIndex });
            customIndex = -1;
        }
        public static string Restore()
        {
            var errors = new System.Collections.Generic.List<string>();
            var game = UnityEngine.Object.FindAnyObjectByType<DSGame>();
            if (game != null) game.UI.SafeAreaPreview = null;
            try
            {
                if (view != null) { RemoveTemporary(); Select(originalIndex); view.Repaint(); }
                if (group != null && !Labels().SequenceEqual(originalLabels)) errors.Add("GameView size list not restored");
                if (view != null && (int)selected.GetValue(view) != originalIndex) errors.Add("GameView selection not restored");
            }
            catch (Exception ex) { errors.Add("sizes: " + ex.Message); }
            try { if (previousPlayView != null) previousPlayView.Focus(); }
            catch (Exception ex) { errors.Add("play view: " + ex.Message); }
            try { if (previousFocus != null) previousFocus.Focus(); }
            catch (Exception ex) { errors.Add("focus: " + ex.Message); }
            if (simulators != null && simulatorStates != null)
                for (int n = 0; n < simulators.Length; n++)
                try
                {
                    var window = simulators[n];
                    var state = window.GetType().GetField("m_State", Flags);
                    var main = window.GetType().GetField("m_Main", Flags).GetValue(window);
                    main.GetType().GetMethod(simulatorStates[n] == "Enabled" ? "Enable" : "Disable", Flags).Invoke(main, null);
                    state.SetValue(window, Enum.Parse(state.FieldType, simulatorStates[n]));
                    if (SimulatorState(window) != simulatorStates[n]) errors.Add("Simulator mode not restored");
                }
                catch (Exception ex) { errors.Add("simulator: " + ex.Message); }
            view = null; group = null; previousFocus = null; previousPlayView = null; simulators = null; simulatorStates = null;
            if (errors.Count > 0) throw new Exception(string.Join("; ", errors));
            return "RESTORED";
        }
        public static string Check(int width, int height)
        {
            var texture = (RenderTexture)view.GetType().GetField("m_RenderTexture", Flags).GetValue(view);
            var game = UnityEngine.Object.FindAnyObjectByType<DSGame>();
            var layout = (Vector2)typeof(DSUI).GetField("lastScreen", Flags).GetValue(game.UI);
            var canvas = (Canvas)typeof(DSUI).GetField("canvas", Flags).GetValue(game.UI);
            if (texture == null || !texture.IsCreated() || texture.width != width || texture.height != height || Time.frameCount <= resizeFrame || layout != new Vector2(width, height) || canvas.pixelRect.size != new Vector2(width, height))
                throw new Exception($"GameView mismatch RT={texture?.width}x{texture?.height} layout={layout} canvas={canvas.pixelRect} frames={resizeFrame}->{Time.frameCount} native={Screen.width}x{Screen.height} device={UnityEngine.Device.Screen.width}x{UnityEngine.Device.Screen.height}");
            Canvas.ForceUpdateCanvases();
            Rect safe = game.UI.SafeAreaPreview ?? new Rect(0, 0, width, height);
            int count = 0;
            foreach (var button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().Where(b => b.gameObject.activeInHierarchy))
            {
                var points = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(points);
                if (points.Any(p => p.x < safe.xMin - 1 || p.y < safe.yMin - 1 || p.x > safe.xMax + 1 || p.y > safe.yMax + 1)) throw new Exception("Button outside screen: " + button.name);
                count++;
            }
            return $"PASS {width}x{height}: {count} buttons within screen";
        }
    }
}

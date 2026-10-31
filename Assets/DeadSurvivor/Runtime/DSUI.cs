using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
namespace DeadSurvivor
{
    public partial class DSUI : MonoBehaviour
    {
#if UNITY_EDITOR
        public Rect? SafeAreaPreview { get; set; }
#endif
        DSGame g; Canvas canvas; RectTransform root, page, modal, toastRoot, tooltip, ghost; TMP_FontAsset font; TextMeshProUGUI toastText, hudWave, hudTimer, hudInfo, banner; List<TextMeshProUGUI> teamTexts = new(); float toastUntil, nextHud; public int Held = -1; bool dragged; public bool JoystickActive; public Vector2 Joystick; Vector2 joystickStart; RectTransform joystickRing, joystickKnob; UnityEngine.UI.Image burningBorder; Sprite borderSprite, joystickBaseSprite, joystickKnobSprite; public float W = 1600, H = 900; UnityEngine.UI.CanvasScaler scaler; Rect lastSafeArea; Vector2 lastScreen;
        public void Init(DSGame game)
        {
            g = game;
            W = 1600;
            H = 900;
            font = Resources.Load<TMP_FontAsset>("DeadSurvivor/UI/PrototypeFont");
            g.World.Font = font;
            joystickBaseSprite = Resources.Load<Sprite>("DeadSurvivor/UI/JoystickBase");
            joystickKnobSprite = Resources.Load<Sprite>("DeadSurvivor/UI/JoystickKnob");
            var go = new GameObject("DeadSurvivorUI", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var safeObject = new GameObject("SafeArea", typeof(RectTransform));
            root = safeObject.GetComponent<RectTransform>();
            root.SetParent(go.transform, false);
            root.pivot = Vector2.one * .5f;
            UpdateSafeArea();
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Lobby();
        }
        void UpdateSafeArea()
        {
            int width = Mathf.Max(1, UnityEngine.Device.Screen.width), height = Mathf.Max(1, UnityEngine.Device.Screen.height);
            Rect safe = UnityEngine.Device.Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0)
                safe = new Rect(0, 0, width, height);
#if UNITY_EDITOR
            if (SafeAreaPreview.HasValue) safe = SafeAreaPreview.Value;
#endif
            Vector2 screen = new Vector2(width, height);
            if (screen == lastScreen && safe == lastSafeArea)
                return;
            lastScreen = screen;
            lastSafeArea = safe;
            root.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            root.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            root.offsetMin = root.offsetMax = Vector2.zero;
            scaler.referenceResolution = new Vector2(W * width / safe.width, H * height / safe.height);
            if (page != null && g.State == "battle") { bool wasModal = modal != null; Battle(); if (g.Paused && wasModal) ShowPause(); }
        }
        Color C(string h) => DSWorld.ColorOf(h);
        RectTransform Box(string name, Transform parent, float x, float y, float w, float h, string color = null, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(.5f, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x - W / 2, -y);
            r.sizeDelta = new Vector2(w, h);
            if (parent != root)
            {
                r.anchorMin = r.anchorMax = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(x, -y);
            }
            if (color != null)
            {
                var im = go.AddComponent<UnityEngine.UI.Image>();
                im.color = C(color);
                im.raycastTarget = raycast;
            }
            return r;
        }
        TextMeshProUGUI Text(Transform p, string text, float x, float y, float w, float h, float size = 16, string color = "#ecf0e8", bool bold = false)
        {
            var r = Box("Text", p, x, y, w, h);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = C(color);
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }
        UnityEngine.UI.Button Button(Transform p, string label, float x, float y, float w, float h, Action action, bool primary = false)
        {
            var r = Box(label, p, x, y, w, h, primary ? "#c7ed83" : "#253238", true);
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = r.GetComponent<UnityEngine.UI.Image>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f);
            colors.pressedColor = new Color(.8f, .8f, .8f);
            b.colors = colors;
            b.onClick.AddListener(() => action());
            var t = Text(r, label, 8, 0, w - 16, h, 16, primary ? "#172017" : "#ecf0e8", primary);
            t.alignment = TextAlignmentOptions.Midline;
            return b;
        }
        void Image(Transform p, Sprite sprite, float x, float y, float w, float h)
        {
            var r = Box("Artwork", p, x, y, w, h);
            var im = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            im.sprite = sprite;
            im.preserveAspect = true;
            im.raycastTarget = false;
        }
        void Dispose(ref RectTransform t)
        {
            if (t != null)
            {
                t.gameObject.SetActive(false);
                Destroy(t.gameObject);
                t = null;
            }
        }
        void Clear()
        {
            battleItems.Clear();
            Dispose(ref page);
            Dispose(ref modal);
            Dispose(ref tooltip);
            Dispose(ref ghost);
            Held = -1;
            HeldRotation = 0;
            JoystickActive = false;
            Joystick = Vector2.zero;
            page = Box("Page_" + g.State, root, 0, 0, W, H, g.State == "battle" ? null : "#0d1317");
            Header();
        }
        void Header()
        {
            Box("Header", page, 0, 0, W, 64, "#0d1317");
            Text(page, "✚  DEAD SURVIVOR", 32, 16, 400, 32, 22, "#c7ed83", true);
            Button(page, "테스트 설정", W - 160, 12, 128, 40, Settings).interactable = g.State != "battle";
        }
        public void Lobby()
        {
            Clear();
            Text(page, "SQUAD SELECTION / 01", 32, 90, 500, 24, 13, "#c7ed83", true);
            Text(page, "끝까지 살아남을 당신의 스쿼드.", 32, 122, W - 64, 60, 40, "#ecf0e8", true);
            Text(page, "생존자 최대 4명을 선발하고, 10번의 밤을 버티세요.", 32, 186, W - 64, 30, 17, "#93a5a5");
            float slotW = (W - 94) / 4;
            for (int i = 0; i < 4; i++)
            {
                int n = i;
                Button(page, i < g.Selected.Count ? g.Data.characters[g.Selected[i]].name + "  ×" : "＋ 생존자 " + (i + 1), 32 + i * (slotW + 10), 228, slotW, 52, () => { if (n < g.Selected.Count) g.ToggleSelection(g.Selected[n]); }, i < g.Selected.Count);
            }
            int cols = 3;
            float cardW = (W - 64 - (cols - 1) * 16) / cols, cardH = 198;
            for (int i = 0; i < 6; i++)
            {
                int id = i;
                var c = g.Data.characters[i];
                float x = 32 + i % cols * (cardW + 16), y = 300 + i / cols * (cardH + 14);
                var card = Box("Card_" + c.id, page, x, y, cardW, cardH, g.Selected.Contains(i) ? "#354535" : "#182125", true);
                var b = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                b.targetGraphic = card.GetComponent<UnityEngine.UI.Image>();
                b.onClick.AddListener(() => g.ToggleSelection(id));
                Image(card, g.World.Load("Portraits/" + c.id), cardW - 138, 3, 132, 122);
                Text(card, c.name, 18, 20, cardW - 150, 48, 25, "#ecf0e8", true);
                Text(card, "시작: " + g.Items[c.weapon].name, 18, 58, 130, 25, 14, "#93a5a5");
                Text(card, $"HP {c.hp}   공격 ×{c.atk:0.0}   방어 {c.def * 100:0}%", 18, 128, cardW - 36, 30, 14);
                Text(card, $"{g.Items[c.weapon].name}  ·  {g.Items[c.weapon].shape.Length}칸", 18, 164, cardW - 36, 28, 14, "#c7ed83");
            }
            float end = 300 + Mathf.Ceil(6f / cols) * (cardH + 14) + 8;
            Text(page, "WASD / 방향키 이동 · 화면 드래그 이동\n공격은 자동입니다. 1: 웨이브 종료", 32, end, W - 340, 54, 14, "#93a5a5");
            Button(page, "스쿼드 출전 →", W - 280, end, 248, 54, g.StartRun, true).interactable = g.Selected.Count > 0;
        }
        public void Battle()
        {
            Clear();
            teamTexts.Clear();
            Canvas.ForceUpdateCanvases();
            float battleBottom = Mathf.Max(H, root.rect.height);
            var touch = Box("MovementInput", page, 0, 64, W, battleBottom - 64, "#ffffff00", true);
            var pointer = touch.gameObject.AddComponent<DSPointer>();
            pointer.ui = this;
            pointer.joystick = true;
            if (borderSprite == null)
            {
                int n = 128;
                var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var pixels = new Color[n * n];
                for (int yy = 0; yy < n; yy++)
                for (int xx = 0; xx < n; xx++)
                {
                    float edge = Mathf.Min(xx, yy, n - 1 - xx, n - 1 - yy) / (float)n;
                    pixels[yy * n + xx] = new Color(1, 1, 1, Mathf.Clamp01(1 - edge / .06f));
                }
                texture.SetPixels(pixels);
                texture.Apply();
                borderSprite = Sprite.Create(texture, new Rect(0, 0, n, n), Vector2.one * .5f, n);
            }
            var border = Box("BurningBorder", page, 0, 64, W, H - 64);
            burningBorder = border.gameObject.AddComponent<UnityEngine.UI.Image>();
            burningBorder.sprite = borderSprite;
            burningBorder.raycastTarget = false;
            burningBorder.color = Color.clear;
            Box("WaveHUD", page, 24, 84, 230, 104, "#10191de8");
            hudWave = Text(page, "", 40, 96, 210, 42, 28, "#c7ed83", true);
            hudInfo = Text(page, "", 40, 140, 210, 40, 13);
            Box("TimerHUD", page, W / 2 - 110, 84, 220, 104, "#10191de8");
            hudTimer = Text(page, "", W / 2 - 95, 100, 190, 72, 27);
            hudTimer.alignment = TextAlignmentOptions.Center;
            Button(page, "Ⅱ", W - 85, 84, 60, 48, () => { g.Paused = true; ShowPause(); });
            banner = Text(page, "", 60, H * .25f, W - 120, 100, 30, "#c7ed83", true);
            banner.alignment = TextAlignmentOptions.Center;
            int itemRows = Mathf.Max(1, Mathf.CeilToInt(g.Team.Max(c => c.items.Count) / 4f));
            float teamHeight = 78 + itemRows * 46;
            for (int i = 0; i < g.Team.Count; i++)
            {
                float width = (W - 64) / 4;
                var box = Box("TeamHUD", page, 24 + i * width, battleBottom - 28 - teamHeight, width - 8, teamHeight, "#10191de8");
                teamTexts.Add(Text(box, "", 12, 8, width - 32, 60, 13));
                BuildBattleItems(box, g.Team[i], width - 8);
            }
            Text(page, "WASD 이동  /  SPACE 일시정지  /  1 정비 화면", 24, battleBottom - 25, W - 48, 22, 11, "#93a5a5");
            RefreshBattleItems();
            nextHud = 0;
        }
        public void ShowPause()
        {
            Dispose(ref modal);
            if (!g.Paused)
                return;
            modal = Box("Pause", root, 0, 0, W, H, "#060b10dd", true);
            var d = Box("Dialog", modal, W / 2 - 230, H / 2 - 140, 460, 280, "#182125");
            Text(d, "작전 일시정지", 28, 28, 400, 50, 28, "#ecf0e8", true);
            Text(d, "전투 시간과 모든 행동이 멈췄습니다.", 28, 92, 400, 50, 17, "#93a5a5");
            Button(d, "선발 화면으로", 28, 190, 190, 52, g.ReturnLobby);
            Button(d, "계속하기 →", 240, 190, 192, 52, () => { g.Paused = false; ShowPause(); }, true);
        }
        public void Prep()
        {
            Clear();
            // Keep the four backpacks at the corners, leaving the centre for wave loot.
            const float margin = 28, gap = 4, cell = 70;
            float gridW = 4 * cell + 3 * gap, packW = gridW + 24, packH = gridW + 72;
            float top = 84, bottom = H - 28 - packH;
            for (int ci = 0; ci < g.Team.Count; ci++)
            {
                int idx = ci;
                var c = g.Team[ci];
                float x = ci % 2 == 0 ? margin : W - margin - packW, y = ci / 2 == 0 ? top : bottom;
                var p = Box("Inventory_" + g.Def(c).id, page, x, y, packW, packH, c.hp > 0 ? "#182125" : "#302527");
                if (c.hp <= 0)
                {
                    var cover = Box("ReviveOverlay", p, 0, 0, packW, packH, "#000000", true);
                    var deadName = Text(cover, g.Def(c).name, 12, 4, gridW, 22, 18, "#ecf0e8", true);
                    deadName.alignment = TextAlignmentOptions.Center;
                    var revive = Button(cover, "부활하기", (packW - 180) / 2, (packH - 56) / 2, 180, 56, () => g.Revive(idx), true);
                    revive.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
                    continue;
                }
                var name = Text(p, g.Def(c).name, 12, 4, gridW, 22, 18, "#ecf0e8", true);
                name.alignment = TextAlignmentOptions.Center;
                for (int yy = 1; yy <= 4; yy++)
                for (int xx = 1; xx <= 4; xx++)
                {
                    var it = c.items.FirstOrDefault(i => g.Shape(i).Any(q => i.x + q.x == xx && i.y + q.y == yy));
                    string color = it == null ? "#223035" : g.Items[it.type].color;
                    var r = Box("Cell_" + xx + "_" + yy, p, 12 + (xx - 1) * (cell + gap), 60 + (yy - 1) * (cell + gap), cell, cell, color, true);
                    var target = r.gameObject.AddComponent<DSPointer>();
                    target.ui = this;
                    target.character = ci;
                    target.x = xx;
                    target.y = yy;
                    target.item = it?.id ?? -1;
                    if (it != null)
                    {
                        bool origin = xx == it.x + g.Shape(it)[0].x && yy == it.y + g.Shape(it)[0].y;
                        var label = Text(r, origin ? ShortName(g.Items[it.type].name) : "", 3, 2, cell - 6, 23, 14);
                        if (origin) Image(r, g.World.ItemIcon(it.type), 16, 25, 38, 38);
                        label.alignment = TextAlignmentOptions.Center;
                        Box("Grade", r, 0, cell - 3, cell, 3, g.Data.grades[it.grade].color);
                    }
                }
            }
            float stashX = margin + packW + 28, stashW = W - 2 * stashX, stashY = top, stashH = H - top - 120;
            var stash = Box("SharedStash", page, stashX, stashY, stashW, stashH, "#111b20", true);
            var st = stash.gameObject.AddComponent<DSPointer>();
            st.ui = this;
            st.stash = true;
            var title = Text(stash, "전리품", 16, 18, stashW - 32, 34, 24, "#ecf0e8", true);
            title.alignment = TextAlignmentOptions.Center;
            var hint = Text(stash, "아이템을 좌우 인벤토리로 드래그하세요", 16, 58, stashW - 32, 30, 16, "#93a5a5");
            hint.alignment = TextAlignmentOptions.Center;
            int cols = 3;
            float iw = (stashW - 48) / cols, viewH = stashH - 330;
            var scrollRoot = Box("StashScroll", stash, 16, 104, stashW - 32, viewH);
            var scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = Box("Viewport", scrollRoot, 0, 0, stashW - 32, viewH, "#ffffff", true);
            var mask = viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = false;
            var content = Box("Content", viewport, 0, 0, stashW - 32, 1);
            var grid = content.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(iw, 132);
            grid.spacing = new Vector2(8, 12);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 28;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            foreach (var i in g.Stash)
            {
                var r = Box("StashItem_" + i.id, content, 0, 0, iw, 132, "#253238", true);
                var t = r.gameObject.AddComponent<DSPointer>();
                t.ui = this;
                t.item = i.id;
                var name = Text(r, g.Items[i.type].name, 8, 8, iw - 16, 28, 17);
                name.alignment = TextAlignmentOptions.Center;
                Image(r, g.World.ItemIcon(i.type), 12, 42, 48, 48);
                var shape = g.Shape(i);
                int sw = shape.Max(q => q.x) + 1, sh = shape.Max(q => q.y) + 1;
                float unit = Mathf.Min(18, 48f / sh), sx = (iw - sw * (unit + 2)) / 2;
                foreach (var q in shape)
                    Box("Shape", r, sx + q.x * (unit + 2), 42 + q.y * (unit + 2), unit, unit, g.Items[i.type].color);
                var grade = Text(r, Grade(i) + " · " + shape.Length + "칸", 8, 103, iw - 16, 23, 13, GradeColor(i));
                grade.alignment = TextAlignmentOptions.Center;
            }
            var warning = Text(stash, "다음 웨이브 시작 시 남은 아이템은 폐기됩니다.", 16, stashH - 84, stashW - 32, 30, 13, "#ffa86b");
            warning.alignment = TextAlignmentOptions.Center;
            FormationControls();
            Button(page, "회전", W / 2 - 270, H - 96, 120, 52, RotateHeld);
            Button(page, "선택 취소", W / 2 + 150, H - 96, 120, 52, () => { Held = -1; Prep(); });
            Button(page, g.Wave == 10 ? "결과 확인 →" : "다음 웨이브 →", W / 2 - 130, H - 96, 260, 52, g.NextWave, true);
            var help = Text(page, "같은 장비는 합성 · 중앙 빈 곳에 놓으면 탈착", stashX, H - 38, stashW, 24, 12, "#93a5a5");
            help.alignment = TextAlignmentOptions.Center;
        }
        string ShortName(string s) => s.Length > 4 ? s.Substring(0, 4) : s; string Grade(Gear i) => g.Items[i.type].expand > 0 ? "소모품" : g.Data.grades[i.grade].name; string GradeColor(Gear i) => g.Data.grades[i.grade].color;
        public void Result()
        {
            Clear();
            var d = Box("Result", page, 80, H / 2 - 220, W - 160, 440, "#182125");
            Text(d, g.Win ? "MISSION COMPLETE" : "SQUAD LOST", 32, 28, W - 224, 28, 15, "#c7ed83");
            Text(d, g.Win ? "새벽이 밝았습니다." : "다음 구조대를 기다리며.", 32, 80, W - 224, 60, 36, "#ecf0e8", true);
            Text(d, $"클리어 웨이브  {(g.Win ? 10 : g.Wave - 1)} / 10\n처치한 몬스터  {g.Kills}\n전투 시간  {Mathf.FloorToInt(g.TotalTime / 60)}:{Mathf.FloorToInt(g.TotalTime % 60):00}", 32, 160, W - 224, 135, 23);
            Button(d, "테스트 기록 저장", 32, 340, 230, 54, () => g.Export());
            Button(d, "스쿼드 재편성 →", W - 160 - 300, 340, 268, 54, g.ReturnLobby, true);
        }
        public void Tick()
        {
            UpdateSafeArea();
            PulseRecommendations();
            if (g.State == "battle") RefreshBattleItems();
            if (toastRoot != null && Time.unscaledTime > toastUntil)
                Dispose(ref toastRoot);
            if (g.State != "battle" || Time.unscaledTime < nextHud)
                return;
            nextHud = Time.unscaledTime + .1f;
            if (hudWave == null)
                return;
            hudWave.text = $"WAVE {g.Wave:00} / 10";
            hudInfo.text = $"누적 처치 {g.Kills} · 위협 {g.Enemies.Count}";
            int n = Mathf.CeilToInt(Mathf.Max(0, g.Duration - g.Elapsed));
            hudTimer.text = $"{n / 60:00}:{n % 60:00}\n<size=13>{g.Config.speed}× 속도</size>";
            float start = g.Duration / 2;
            if (burningBorder != null)
                burningBorder.color = g.Wave >= 6 && g.Elapsed >= start - 3 && g.Elapsed < start + 10 ? new Color(1, .38f, .23f, .35f + .15f * Mathf.Sin(g.Elapsed * 4)) : Color.clear;
            banner.text = g.Wave >= 6 && g.Elapsed >= start - 3 && g.Elapsed < start ? $"{Mathf.CeilToInt(start - g.Elapsed)}초 후 버닝 타임" : g.Wave >= 6 && g.Elapsed >= start && g.Elapsed < start + 10 ? $"BURNING TIME · {Mathf.CeilToInt(start + 10 - g.Elapsed)}초\n<size=16>양방향 대규모 습격 · 스폰 약 3배</size>" : g.Elapsed < 2 ? $"STAY TOGETHER. STAY ALIVE.\nWAVE {g.Wave:00}" : "";
            for (int i = 0; i < teamTexts.Count; i++)
            {
                var c = g.Team[i];
                var s = g.Stats(c);
                teamTexts[i].text = $"<b>{g.Def(c).name}</b>  {(c.hp > 0 ? Mathf.CeilToInt(c.hp).ToString() : "사망")}\n<size=11>방어 {s.def * 100:0}% · 회복 {s.heal:0.#}/3초\n공격 +{s.attack * 100:0}% · 보호막 {c.shield:0}</size>";
            }
        }
        public void Toast(string message)
        {
            Dispose(ref toastRoot);
            toastRoot = Box("Toast", root, W / 2 - 300, 8, 600, 48, "#e4eccc");
            toastText = Text(toastRoot, message, 12, 4, 576, 40, 14, "#16201b");
            toastUntil = Time.unscaledTime + 3;
        }
        public void StartJoystick(Vector2 screen)
        {
            JoystickActive = true;
            joystickStart = screen;
            Joystick = Vector2.zero;
            var p = Local(screen);
            joystickRing = Box("Joystick", page, p.x - 70, p.y - 70, 140, 140);
            var ringImage = joystickRing.gameObject.AddComponent<UnityEngine.UI.Image>();
            ringImage.sprite = joystickBaseSprite;
            ringImage.preserveAspect = true;
            ringImage.raycastTarget = false;
            ringImage.color = new Color(1, 1, 1, .8f);
            joystickKnob = Box("Knob", joystickRing, 40, 40, 60, 60);
            var knobImage = joystickKnob.gameObject.AddComponent<UnityEngine.UI.Image>();
            knobImage.sprite = joystickKnobSprite;
            knobImage.preserveAspect = true;
            knobImage.raycastTarget = false;
        }
        public void MoveJoystick(Vector2 screen)
        {
            if (!JoystickActive)
                return;
            var d = (screen - joystickStart) / canvas.scaleFactor;
            Joystick = Vector2.ClampMagnitude(new Vector2(d.x, -d.y) / 45, 1);
            if (joystickKnob != null)
                joystickKnob.anchoredPosition = new Vector2(40 + Joystick.x * 35, -40 - Joystick.y * 35);
        }
        public void StopJoystick()
        {
            JoystickActive = false;
            Joystick = Vector2.zero;
            Dispose(ref joystickRing);
        }
        Vector2 Local(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var p);
            return new Vector2(p.x + W / 2, root.rect.height / 2 - p.y);
        }
        public void ClickTarget(DSPointer t)
        {
            if (g.State != "prep")
                return;
            if (Held >= 0)
            {
                if ((t.character >= 0 || t.stash || (t.item >= 0 && t.item != Held)) && confirmedTapTarget != t)
                {
                    ShowEquipmentPreview(t);
                    confirmedTapTarget = t;
                    return;
                }
                ApplyDrop(t);
                return;
            }
            if (t.item >= 0)
            {
                var i = g.Find(t.item);
                if (g.Owner(i)?.hp <= 0)
                { Toast("사망한 캐릭터는 인벤토리의 부활하기 버튼을 누른 뒤 장비를 변경하세요."); return; }
                Held = t.item;
                HeldRotation = i.rotation;
                ShowEquipmentSelection();
                Toast(g.Items[i.type].name + " 선택 · 배치할 칸 또는 합성 대상을 클릭하세요.");
            }
        }
        public void BeginItem(int id, Vector2 p)
        {
            var i = g.Find(id);
            if (i == null) return;
            if (g.Owner(i)?.hp <= 0)
            { Toast("사망한 캐릭터는 인벤토리의 부활하기 버튼을 누른 뒤 장비를 변경하세요."); return; }
            Held = id;
            HeldRotation = i.rotation;
            dragged = true;
            HideTooltip();
            Dispose(ref ghost);
            ghost = Box("DragGhost", root, 0, 0, 200, 44, "#283e36");
            Text(ghost, g.Items[i.type].name, 8, 7, 184, 30, 16, "#c7ed83");
            ShowEquipmentSelection();
            DragItem(p);
        }
        public void DragItem(Vector2 screen)
        {
            if (Held < 0)
                return;
            if (ghost != null)
            {
                var p = Local(screen);
                ghost.anchoredPosition = new Vector2(p.x - W / 2 + 12, -p.y - 12);
            }
            var t = TargetAt(screen);
            if (t != null)
                Highlight(t);
            else
            {
                ClearPreviewCells();
                ShowEquipmentSelection();
            }
        }
        DSPointer TargetAt(Vector2 screen)
        {
            var e = new PointerEventData(EventSystem.current) { position = screen };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(e, hits);
            foreach (var h in hits)
            {
                var p = h.gameObject.GetComponentInParent<DSPointer>();
                if (p != null && !p.joystick)
                    return p;
            }
            return null;
        }
        public void EndItem(Vector2 screen)
        {
            if (!dragged)
                return;
            dragged = false;
            var t = TargetAt(screen);
            Dispose(ref ghost);
            if (t != null)
                ApplyDrop(t);
            else
            {
                Held = -1;
                Prep();
                Toast(" 배치 취소: 원래 위치를 유지합니다.");
            }
        }
        bool IsUnequipTarget(DSPointer target)
        {
            if (target.character >= 0) return false;
            if (target.stash) return true;
            var held = g.Find(Held);
            var other = g.Find(target.item);
            return held != null && g.Owner(held) != null && other != null
                && g.Stash.Contains(other) && !g.CanMerge(held, other);
        }
        void ApplyDrop(DSPointer t)
        {
            int id = Held;
            if (id < 0)
                return;
            var item = g.Find(id);
            bool ok = false;
            if (item != null)
            {
                if (t.character >= 0)
                    ok = g.PlaceRotated(id, t.character, t.x, t.y, HeldRotation);
                else if (IsUnequipTarget(t))
                    ok = g.Unequip(id);
                else if (t.item >= 0 && t.item != id)
                    ok = g.Merge(id, t.item);
            }
            int flashed = g.Find(id) == null ? t.item : id;
            Held = -1;
            Prep();
            if (ok)
                FlashItem(flashed);
            Toast(ok ? "장비 배치를 적용했습니다." : "배치 또는 합성 불가: 원래 위치를 유지합니다.");
        }
        public void RotateHeld()
        {
            if (g.State != "prep" || g.Find(Held) == null)
                return;
            HeldRotation = (HeldRotation + 1) % 4;
            ShowEquipmentSelection();
            Toast("회전 " + HeldRotation * 90 + "° · 배치 성공 시 적용");
        }
        public void HoverTarget(DSPointer t)
        {
            if (Held >= 0)
            {
                Highlight(t);
                return;
            }
            if (t.item < 0)
                return;
            var i = g.Find(t.item);
            if (i == null)
                return;
            ShowTooltip(i);
        }
        void Highlight(DSPointer t)
        {
            ShowEquipmentPreview(t);
        }
        void ShowTooltip(Gear i)
        {
            ShowEquipmentText(ItemDescription(i), false);
        }
        public void HideTooltip()
        {
            Dispose(ref tooltip);
            if (page != null && Held < 0)
            foreach (Transform child in page)
            if (child.name == "FormationPreview" || DSGame.FormationNames.Contains(child.name))
                child.gameObject.SetActive(true);
        }
        TMP_InputField Field(Transform p, string label, string value, float x, float y, float w, float h = 48, bool multiline = false)
        {
            Text(p, label, x, y, w, 25, 14, "#93a5a5");
            var r = Box("Input", p, x, y + 28, w, h, "#0f171b", true);
            var input = r.gameObject.AddComponent<TMP_InputField>();
            var viewport = Box("Viewport", r, 10, 5, w - 20, h - 10);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text = Text(viewport, "", 0, 0, w - 20, h - 10, 15);
            input.textViewport = viewport;
            input.textComponent = text;
            input.fontAsset = font;
            input.text = value;
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            return input;
        }
        public void Settings()
        {
            Dispose(ref modal);
            modal = Box("Settings", root, 0, 0, W, H, "#060b10ee", true);
            float dw = Mathf.Min(860, W - 40), x = (W - dw) / 2;
            var d = Box("SettingsDialog", modal, x, 60, dw, H - 100, "#182125");
            Text(d, "플레이테스트 설정", 24, 20, dw - 150, 44, 27, "#ecf0e8", true);
            Button(d, "닫기", dw - 110, 18, 86, 42, () => Dispose(ref modal));
            Text(d, "변경값은 다음 웨이브부터 적용됩니다. 기본 총 전투 7분 15초. 시간 변경은 전체에 비례 적용됩니다.", 24, 74, dw - 48, 42, 14, "#93a5a5");
            float cw = (dw - 64) / 2;
            var duration = Field(d, "1웨이브 시간 (기본30초 / 5~60)", g.Config.duration.ToString(), 24, 122, cw);
            var speed = Field(d, "진행 속도 (1 / 2 / 3)", g.Config.speed.ToString(), 40 + cw, 122, cw);
            var count = Field(d, "몬스터 수 배율 (0.1~3)", g.Config.countScale.ToString(), 24, 216, cw);
            var drops = Field(d, "보상 (0=자동 / 1~8 고정)", g.Config.drops.ToString(), 40 + cw, 216, cw);
            float jsonH = H - 510;
            var json = Field(d, "고급 설정 JSON · weights / waves", JsonUtility.ToJson(g.Config, true), 24, 310, dw - 48, jsonH, true);
            Button(d, "빠른 테스트", 24, H - 170, 160, 46, () => { duration.text = "15"; speed.text = "2"; });
            Button(d, "기본값 복원", 196, H - 170, 160, 46, () => { g.Config = JsonUtility.FromJson<RunSettings>(JsonUtility.ToJson(g.Data.settings)); Settings(); });
            Button(d, "설정 적용", dw - 210, H - 170, 186, 46, () => { try { var c = JsonUtility.FromJson<RunSettings>(json.text); c.duration = float.Parse(duration.text); c.speed = float.Parse(speed.text); c.countScale = float.Parse(count.text); c.drops = int.Parse(drops.text); if (!ValidSettings(c)) throw new Exception("시간·배속·적 수·10개 웨이브·드랍 가중치를 확인하세요."); g.Config = c; g.TestControlsUsed = true; Dispose(ref modal); Toast("테스트 설정을 적용했습니다."); } catch (Exception ex) { Toast("설정 오류: " + ex.Message); } }, true);
        }
        public bool ValidSettings(RunSettings c) => c != null && c.duration >= 5 && c.duration <= 60 && (c.speed == 1 || c.speed == 2 || c.speed == 3) && c.countScale >= .1f && c.countScale <= 3 && c.drops >= 0 && c.drops <= 8 && c.weights != null && c.weights.Length > 0 && c.weights.All(w => g.Items.ContainsKey(w.id) && float.IsFinite(w.weight) && w.weight >= 0) && c.weights.Where(w => g.Items[w.id].expand == 0).Sum(w => w.weight) > 0 && c.waves != null && c.waves.Length == 10 && c.waves.All(w => w != null && float.IsFinite(w.duration) && w.duration > 0 && w.duration <= 180 && w.drops >= 0 && w.drops <= 8 && w.count >= 1 && w.count <= 3000 && w.hp > 0 && w.hp <= 20 && w.damage > 0 && w.damage <= 20 && w.types != null && w.types.Length > 0 && w.types.All(t => g.Monsters.ContainsKey(t))) && c.waves[9].drops == 0;
    }
}

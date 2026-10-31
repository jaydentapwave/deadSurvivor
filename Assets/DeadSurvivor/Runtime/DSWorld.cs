using System.Collections.Generic;
using UnityEngine;
using TMPro;
namespace DeadSurvivor
{
    public partial class DSWorld : MonoBehaviour
    {
        bool mapOwned; DSGame g; Camera cam; Transform root; List<SpriteRenderer> pool = new(); int cursor; Dictionary<string, Sprite> sprites = new(); Sprite square, disc, ring, vignette; float vignetteAspect; List<TextMeshPro> texts = new(); int textCursor; public TMP_FontAsset Font;
        public static Color ColorOf(string h)
        {
            ColorUtility.TryParseHtmlString(h, out var c);
            return c;
        }
        public void Init(DSGame game)
        {
            g = game;
            mapOwned=game.MapSession!=null;
            cam = Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = 748f / 84;
            cam.backgroundColor = ColorOf("#192326");
            cam.clearFlags = CameraClearFlags.SolidColor;
            root = new GameObject("BattleWorld").transform;
            if(g.MapSession)root.SetParent(transform,false);
            square = MakeShape(0);
            disc = MakeShape(1);
            ring = MakeShape(2);
        }
        Sprite Vignette(float aspect)
        {
            int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            var colors = new Color[n * n];
            float outer = Mathf.Max(aspect, 1) * .7f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = ((x + .5f) / n - .5f) * aspect, dy = (y + .5f) / n - .5f;
                float alpha = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - .1f) / (outer - .1f)) * .5333f;
                colors[y * n + x] = new Color(0, 0, 0, alpha);
            }
            t.SetPixels(colors);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * .5f, n);
        }
        Sprite MakeShape(int kind)
        {
            int n = kind == 0 ? 2 : 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * n / 2) / (n / 2);
                float a = kind == 0 ? 1 : kind == 1 ? Mathf.Clamp01((1 - d) * n / 2) : Mathf.Clamp01((1 - d) * n / 2) * Mathf.Clamp01((d - .95f) * n / 2);
                px[y * n + x] = new Color(1, 1, 1, a);
            }
            t.SetPixels(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * .5f, n);
        }
        void OnDestroy()
        {
            if(mapOwned)
                foreach(var shape in new[]{square,disc,ring,vignette})if(shape){Destroy(shape.texture);Destroy(shape);}
            foreach (var pair in sprites)
                if (pair.Key.StartsWith("prototype-icon:") && pair.Value != null)
                { Destroy(pair.Value.texture); Destroy(pair.Value); }
        }
        public Sprite ItemIcon(string id)
        {
            string key = "prototype-icon:" + id;
            if (!sprites.TryGetValue(key, out var icon))
            {
                icon = DSIconArt.Create(id, Color.Lerp(ColorOf(g.Items[id].color), Color.white, .6f));
                sprites[key] = icon;
            }
            return icon;
        }
        public Sprite Load(string p)
        {
            if (!sprites.TryGetValue(p, out var s))
            {
                s = Resources.Load<Sprite>("DeadSurvivor/Sprites/" + p);
                sprites[p] = s;
            }
            return s;
        }
        Vector3 Pos(Vector2 p) => new(p.x / 42, -p.y / 42, 0);
        SpriteRenderer Draw(Sprite s, Vector2 p, Vector2 size, Color color, int order, float angle = 0, bool native = false)
        {
            if (cursor == pool.Count)
            {
                var go = new GameObject("Visual " + cursor, typeof(SpriteRenderer));
                go.transform.SetParent(root);
                pool.Add(go.GetComponent<SpriteRenderer>());
            }
            var r = pool[cursor++];
            r.gameObject.SetActive(true);
            r.sprite = s;
            r.color = color;
            r.sortingOrder = order;
            r.transform.position = Pos(p);
            r.transform.rotation = Quaternion.Euler(0, 0, -angle * Mathf.Rad2Deg);
            r.transform.localScale = native ? Vector3.one : new Vector3(size.x / 42, size.y / 42, 1);
            return r;
        }
        void Rect(Vector2 p, float w, float h, string color, int order)
        {
            Draw(square, p, new Vector2(w, h), ColorOf(color), order);
        }
        void Circle(Vector2 p, float r, Color c, int order, bool outline = false)
        {
            Draw(outline ? ring : disc, p, Vector2.one * r * 2, c, order);
        }
        void Text(string content, Vector2 p, float size, Color c, int order = 1000)
        {
            if (Font == null)
                return;
            if (textCursor == texts.Count)
            {
                var go = new GameObject("WorldLabel", typeof(TextMeshPro));
                go.transform.SetParent(root);
                var t = go.GetComponent<TextMeshPro>();
                t.alignment = TextAlignmentOptions.Center;
                t.rectTransform.sizeDelta = new Vector2(8, 1);
                t.GetComponent<MeshRenderer>().sortingOrder = 1000;
                texts.Add(t);
            }
            var label = texts[textCursor++];
            label.gameObject.SetActive(true);
            label.GetComponent<MeshRenderer>().sortingOrder = order;
            label.font = Font;
            label.text = content;
            label.fontSize = size / 4.2f;
            label.color = c;
            label.transform.position = Pos(p);
        }
        public void Render()
        {
            if(g.MapSession){RenderMap();return;}
            cursor = textCursor = 0;
            bool visible = g.State == "battle" || g.State == "result";
            root.gameObject.SetActive(visible);
            cam.backgroundColor = ColorOf(visible ? "#192326" : "#0d1317");
            if (!visible)
                return;
            cam.transform.position = new Vector3(g.Center.x / 42, -g.Center.y / 42, -10);
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            g.ViewHeight = 748;
            g.ViewWidth = g.ViewHeight * aspect;
            cam.orthographicSize = g.ViewHeight / 84;
            float left = g.Center.x - g.ViewWidth / 2, top = g.Center.y - g.ViewHeight / 2;
            for (float x = Mathf.Floor(left / 64) * 64; x < left + g.ViewWidth + 64; x += 64)
                Rect(new Vector2(x, g.Center.y), 1, g.ViewHeight + 128, "#263336", -20);
            for (float y = Mathf.Floor(top / 64) * 64; y < top + g.ViewHeight + 64; y += 64)
                Rect(new Vector2(g.Center.x, y), g.ViewWidth + 128, 1, "#263336", -20);
            for (float x = Mathf.Floor(left / 300) * 300; x < left + g.ViewWidth + 300; x += 300)
            for (float y = Mathf.Floor(top / 300) * 300; y < top + g.ViewHeight + 300; y += 300)
            {
                Rect(new Vector2(x + 47, y + 56.5f), 30, 5, "#2b3735", -19);
                Rect(new Vector2(x + 78.5f, y + 69), 5, 12, "#2b3735", -19);
                Circle(new Vector2(x + 150, y + 220), 18, ColorOf("#202d2c"), -19);
            }
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                Vector2 p = g.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 168;
                Draw(square, p, new Vector2(4, 1), ColorOf("#c7ed8310"), -15, a + Mathf.PI / 2);
            }
            foreach (var e in g.Enemies)
            {
                if (e.phase == "windup")
                {
                    float a = Mathf.Atan2(e.charge.y, e.charge.x);
                    Draw(square, e.pos + e.charge * 161, new Vector2(322, 40), ColorOf("#eea04c33"), -5, a);
                    Draw(square, e.pos + e.charge * 161, new Vector2(322, 2), ColorOf("#ffc779"), -4, a);
                }
                if (e.phase == "fuse")
                {
                    Circle(e.pos, 90, ColorOf("#ff595933"), -5);
                    Circle(e.pos, 90, ColorOf("#ff7777"), -4, true);
                    Circle(e.pos, 90 * (1 - e.phaseTime / 1.2f), ColorOf("#ff7777"), -4, true);
                }
                if (e.phase == "swarm")
                    Draw(square, e.pos - e.flock * 21.5f, new Vector2(13, 3), ColorOf("#ff9b88"), 0, Mathf.Atan2(e.flock.y, e.flock.x));
                Draw(Load("Monsters/" + e.type + (e.flash > 0 ? "_hit" : "")), e.pos, Vector2.one, Color.white, 10, 0, true);
                if (e.hp < e.maxhp)
                {
                    Vector2 p = e.pos + new Vector2(0, -27 * (e.type == "armored" ? 1.4f : 1));
                    float w = 26 * (e.type == "armored" ? 1.4f : 1);
                    Rect(p, w, 3, "#39413b", 20);
                    float fill = w * Mathf.Max(0, e.hp / e.maxhp);
                    Rect(p + new Vector2((fill - w) / 2, 0), fill, 3, "#dc9b78", 21);
                }
            }
            foreach (var c in g.Team)
            {
                Draw(Load(c.hp > 0 ? "Characters/" + g.Def(c).id + (c.flash > 0 ? "_hit" : "") : "Markers/dead"), c.pos, Vector2.one, Color.white, 30, 0, true);
                if (c.hp <= 0)
                    continue;
                var p = c.pos + new Vector2(0, -31);
                Rect(p, 36, 4, "#3c4847", 40);
                float w = 36 * c.hp / c.maxhp;
                Rect(p + new Vector2((w - 36) / 2, 0), w, 4, "#c7ed83", 41);
                Text(g.Def(c).name, c.pos + new Vector2(0, 40), 10, ColorOf("#dce4d9"));
            }
            foreach (var b in g.Shots)
            {
                float a = Mathf.Atan2(b.dir.y, b.dir.x);
                Draw(Load("Projectiles/" + (b.type == "enemy" ? "enemy" : g.Items[b.type].spriteKey)), b.pos, Vector2.one, Color.white, 50, a, true);
            }
            foreach (var e in g.Effects)
            {
                var col = ColorOf(e.color);
                col.a = Mathf.Min(1, e.life / e.max);
                if (e.text != null)
                    Text(e.text, e.pos, 12, col);
                else
                {
                    float r = e.r * (1 - e.life / e.max * .6f);
                    Circle(e.pos, r, col, 60, true);
                    col.a *= .2f;
                    Circle(e.pos, r, col, 59);
                }
            }
            if (vignette == null || Mathf.Abs(aspect - vignetteAspect) > .01f)
            {
                if (vignette != null)
                {
                    Destroy(vignette.texture);
                    Destroy(vignette);
                }
                vignette = Vignette(aspect);
                vignetteAspect = aspect;
            }
            Draw(vignette, g.Center, new Vector2(g.ViewWidth, g.ViewHeight), Color.white, 4000);
            for (int i = cursor; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);
            for (int i = textCursor; i < texts.Count; i++)
                texts[i].gameObject.SetActive(false);
        }
    }
}

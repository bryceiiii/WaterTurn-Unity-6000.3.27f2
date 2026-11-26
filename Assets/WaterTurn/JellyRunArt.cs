using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterTurn
{
    /// <summary>
    /// Runtime procedural art for the jelly run.  The HTML prototype painted the
    /// whole cave with canvas gradients, so the Unity port bakes the same shapes
    /// into sprites once and caches them; nothing here is imported from disk.
    /// The fish is deliberately NOT generated: it uses the project's FishArt.
    /// </summary>
    public static class JellyRunArt
    {
        static Sprite solid, rounded, circle, dot, glow, ring, ellipse, pearl, diamond;
        static Sprite vignette, shaft, beam, saw, redVignette, background, bottomGlow;
        static Sprite[] floors, platforms, spikes, caves, stalactites;

        // Everything generated here so the cache can be released between play
        // sessions: the project runs with domain reload disabled, so these
        // statics outlive the objects Unity destroys when play mode stops.
        static readonly List<Sprite> madeSprites = new List<Sprite>();
        static readonly List<Texture2D> madeTextures = new List<Texture2D>();

        const int CaveWidth = 1536;

        // ---------------------------------------------------------------- basics

        public static Sprite Solid => solid != null ? solid : solid = MakeSolid();
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded();
        public static Sprite Circle => circle != null ? circle : circle = MakeCircle(32);
        public static Sprite Dot => dot != null ? dot : dot = MakeDot();
        public static Sprite Glow => glow != null ? glow : glow = MakeGlow(128);
        public static Sprite Ring => ring != null ? ring : ring = MakeRing(128, .055f);
        public static Sprite Ellipse => ellipse != null ? ellipse : ellipse = MakeEllipse(96, 40);
        public static Sprite Pearl => pearl != null ? pearl : pearl = MakePearl();
        public static Sprite Diamond => diamond != null ? diamond : diamond = MakeDiamond();
        public static Sprite Vignette => vignette != null ? vignette : vignette = MakeVignette();
        public static Sprite RedVignette => redVignette != null ? redVignette : redVignette = MakeRedVignette();
        public static Sprite Shaft => shaft != null ? shaft : shaft = MakeShaft();
        public static Sprite Beam => beam != null ? beam : beam = MakeBeam();
        public static Sprite Background => background != null ? background : background = MakeBackground();
        public static Sprite BottomGlow => bottomGlow != null ? bottomGlow : bottomGlow = MakeBottomGlow();
        public static Sprite Saw => saw != null ? saw : saw = MakeSaw(0f);
        public static Sprite SawSmall => MakeSaw(1f);

        public static Sprite Arc(float startDeg, float sweepDeg) => MakeArc(128, startDeg, sweepDeg, .06f);

        public static Sprite Cave(int layer)
        {
            if (caves == null) caves = new Sprite[2];
            if (caves[layer] == null) caves[layer] = MakeCave(layer);
            return caves[layer];
        }

        public static Sprite Stalactites(int layer)
        {
            if (stalactites == null) stalactites = new Sprite[2];
            if (stalactites[layer] == null) stalactites[layer] = MakeStalactites(layer);
            return stalactites[layer];
        }

        public static Sprite Spike(int index)
        {
            if (spikes == null) spikes = new Sprite[JellyRunData.Spikes.Length];
            if (index < 0 || index >= spikes.Length) return Solid;
            if (spikes[index] == null) spikes[index] = MakeSpike(JellyRunData.Spikes[index]);
            return spikes[index];
        }

        public static Sprite Floor(int index)
        {
            if (floors == null) floors = new Sprite[JellyRunData.Floors.Length];
            if (index < 0 || index >= floors.Length) return Solid;
            if (floors[index] == null) floors[index] = MakeFloor(JellyRunData.Floors[index]);
            return floors[index];
        }

        public static Sprite Platform(int index)
        {
            if (platforms == null) platforms = new Sprite[JellyRunData.EntrancePlatforms.Length];
            if (index < 0 || index >= platforms.Length) return Rounded;
            if (platforms[index] == null) platforms[index] = MakePlatform(JellyRunData.EntrancePlatforms[index]);
            return platforms[index];
        }

        static readonly Dictionary<string, Sprite> dynamicSprites = new Dictionary<string, Sprite>();

        /// <summary>
        /// Size-keyed floor sprite for hand-placed floor prefabs.  Unlike
        /// <see cref="Floor(int)"/> this is not tied to the HTML table, so an
        /// authored floor can be resized in the Scene view and still look right.
        /// </summary>
        public static Sprite FloorSprite(int width, int height, bool ceiling)
        {
            width = Mathf.Max(4, width); height = Mathf.Max(4, height);
            string key = "F" + width + "x" + height + (ceiling ? "c" : "f");
            if (dynamicSprites.TryGetValue(key, out var cached) && cached != null) return cached;
            var sprite = MakeFloor(new JellyRunData.RectF(0, ceiling ? 0 : 2, width, height));
            dynamicSprites[key] = sprite;
            return sprite;
        }

        public static Sprite LedgeSprite(int width)
        {
            width = Mathf.Max(16, width);
            string key = "L" + width;
            if (dynamicSprites.TryGetValue(key, out var cached) && cached != null) return cached;
            var sprite = MakePlatform(new JellyRunData.RectF(0, 0, width, 16));
            dynamicSprites[key] = sprite;
            return sprite;
        }

        public static Sprite SpikeSprite(int width, int height, bool up)
        {
            width = Mathf.Max(4, width); height = Mathf.Max(4, height);
            string key = "S" + width + "x" + height + (up ? "u" : "d");
            if (dynamicSprites.TryGetValue(key, out var cached) && cached != null) return cached;
            var sprite = MakeSpike(new JellyRunData.SpikeF(0, 0, width, height, up));
            dynamicSprites[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Drops the cache and releases the generated objects.  Called from the
        /// play-mode entry point so a second Play press gets fresh, live sprites
        /// instead of the destroyed leftovers from the previous run.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlayMode() => ClearCache();

        public static void ClearCache()
        {
            for (int i = 0; i < madeSprites.Count; i++) Release(madeSprites[i]);
            for (int i = 0; i < madeTextures.Count; i++) Release(madeTextures[i]);
            madeSprites.Clear();
            madeTextures.Clear();

            floors = null; platforms = null; spikes = null; caves = null; stalactites = null;
            solid = rounded = circle = dot = glow = ring = ellipse = pearl = diamond = null;
            vignette = shaft = beam = saw = redVignette = background = bottomGlow = null;
            dynamicSprites.Clear();
        }

        // "== null" is Unity's overload, so objects destroyed at the end of the
        // previous play session are skipped instead of throwing.
        static void Release(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        // -------------------------------------------------------------- textures

        static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }

        static Texture2D NewRepeatTex(int w, int h)
        {
            var tex = NewTex(w, h);
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        static Sprite SpriteFrom(Texture2D tex, Vector4 border = default)
        {
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.DontSave;
            madeTextures.Add(tex);
            madeSprites.Add(sprite);
            return sprite;
        }

        static Color32 C(byte r, byte g, byte b, byte a = 255) => new Color32(r, g, b, a);
        static Color32 Clear => new Color32(0, 0, 0, 0);

        static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));

        static float RoundRectAlpha(float px, float py, float w, float h, float r)
        {
            float dx = Mathf.Min(px, w - 1 - px);
            float dy = Mathf.Min(py, h - 1 - py);
            if (dx >= r || dy >= r) return 1f;
            float cx = r - dx, cy = r - dy;
            float d = Mathf.Sqrt(cx * cx + cy * cy);
            return Mathf.Clamp01(r - d + .5f);
        }

        static float Noise(float t) =>
            (Mathf.Sin(t * 1.7f) * .5f + Mathf.Sin(t * 2.9f + 1.3f) * .3f + Mathf.Sin(t * 5.1f + 2.7f) * .2f) * .5f + .5f;

        // ------------------------------------------------------------ flat tiles

        static Sprite MakeSolid()
        {
            var tex = NewTex(4, 4);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeRounded()
        {
            const int size = 64, r = 18;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = RoundRectAlpha(x, y, size, size, r);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex, new Vector4(20, 20, 20, 20));
        }

        static Sprite MakeCircle(int size)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float c = (size - 1) * .5f, rad = size * .5f - .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = Mathf.Clamp01(rad - d + .5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeDot()
        {
            const int size = 16;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float c = (size - 1) * .5f, rad = size * .42f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = Mathf.Clamp01(1f - d / rad);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Pow(a, 1.4f) * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeGlow(int size)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float c = (size - 1) * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeRing(int size, float thickness)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float c = (size - 1) * .5f, rad = size * .5f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / rad;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - .82f) / thickness);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeArc(int size, float startDeg, float sweepDeg, float thickness)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float c = (size - 1) * .5f, rad = size * .5f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / rad;
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                float diff = Mathf.Repeat(ang - startDeg + 180f, 360f) - 180f;
                if (Mathf.Abs(diff) > sweepDeg * .5f) { px[y * size + x] = Clear; continue; }
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - .82f) / thickness);
                float ends = Mathf.Clamp01((sweepDeg * .5f - Mathf.Abs(diff)) / 10f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * ends * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeEllipse(int w, int h)
        {
            var tex = NewTex(w, h);
            var px = new Color32[w * h];
            float cx = (w - 1) * .5f, cy = (h - 1) * .5f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / (w * .5f);
                float dy = (y - cy) / (h * .5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakePearl()
        {
            const int size = 64;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float cx = size * .5f, cy = size * .5f, rad = size * .46f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / rad;
                if (d > 1f) { px[y * size + x] = Clear; continue; }
                // Offset highlight towards the upper-left, like the HTML pearl.
                float hl = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(cx - 7, cy + 7)) / (rad * .85f));
                Color col = Color.Lerp(new Color(0.85f, .77f, .60f), Color.white, Mathf.Pow(1f - d, .7f));
                col = Color.Lerp(col, new Color(1f, .98f, .92f), hl * .8f);
                col.a = 1f;
                px[y * size + x] = col;
            }
            // A little cold iridescence at the lower-right.
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / rad;
                if (d > 1f) continue;
                float iri = Mathf.Clamp01((dx / rad) * .5f - (dy / rad) * .5f);
                if (iri <= 0f) continue;
                var col = (Color)px[y * size + x];
                col = Color.Lerp(col, new Color(.70f, .86f, 1f), iri * .22f);
                px[y * size + x] = col;
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeDiamond()
        {
            const int size = 64;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float c = (size - 1) * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - c) / c, dy = Mathf.Abs(y - c) / c;
                float d = dx + dy;
                if (d > 1f) { px[y * size + x] = Clear; continue; }
                float t = Mathf.Clamp01(1f - d);
                Color col = Color.Lerp(new Color(.20f, .55f, .58f), new Color(.80f, 1f, .96f), Mathf.Pow(t, .8f));
                px[y * size + x] = col;
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeVignette() => MakeVignetteSprite(new Color(0f, 0f, 0f, .62f));
        static Sprite MakeRedVignette() => MakeVignetteSprite(new Color(1f, .22f, .22f, 1f));

        static Sprite MakeVignetteSprite(Color edge)
        {
            const int size = 256;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float cx = (size - 1) * .5f, cy = (size - 1) * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - cx) / cx, dy = (y - cy) / cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.Clamp01((d - .34f) / .66f);
                a = a * a;
                px[y * size + x] = new Color32((byte)(edge.r * 255), (byte)(edge.g * 255), (byte)(edge.b * 255),
                    (byte)(a * edge.a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeShaft()
        {
            const int w = 128, h = 512;
            var tex = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + .5f) / w;
                float hz = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.1f);
                float vy = Mathf.Clamp01((h - y) / (float)h * .5f + .5f);
                float a = hz * .11f * vy;
                px[y * w + x] = new Color32(96, 220, 220, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeBeam()
        {
            const int w = 64, h = 256;
            var tex = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + .5f) / w;
                float hz = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.4f);
                float v = (y + .5f) / h;
                float a = hz * v * .55f;
                px[y * w + x] = new Color32(255, 235, 175, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeBackground()
        {
            const int h = 512;
            var tex = NewTex(2, h);
            var px = new Color32[2 * h];
            Color top = new Color(.027f, .078f, .114f);
            Color mid = new Color(.039f, .125f, .161f);
            Color bottom = new Color(.016f, .071f, .102f);
            for (int y = 0; y < h; y++)
            {
                float v = (y + .5f) / h; // 0 at the bottom of the screen
                Color col = v < .45f ? Color.Lerp(bottom, mid, v / .45f) : Color.Lerp(mid, top, (v - .45f) / .55f);
                for (int x = 0; x < 2; x++) px[y * 2 + x] = col;
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeBottomGlow()
        {
            const int h = 128;
            var tex = NewTex(2, h);
            var px = new Color32[2 * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + .5f) / h;
                float a = Mathf.Pow(Mathf.Clamp01(v), 1.4f) * .16f;
                float r = Mathf.Lerp(18 / 255f, 128 / 255f, v);
                float g = Mathf.Lerp(10 / 255f, 32 / 255f, v);
                float b = Mathf.Lerp(12 / 255f, 38 / 255f, v);
                for (int x = 0; x < 2; x++)
                    px[y * 2 + x] = new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        // ------------------------------------------------------------- terrain

        static Sprite MakeFloor(JellyRunData.RectF r)
        {
            int w = Mathf.Max(2, Mathf.RoundToInt(r.w));
            int h = Mathf.Max(2, Mathf.RoundToInt(r.h));
            bool ceiling = r.y <= 1f;
            var tex = NewTex(w, h);
            var px = new Color32[w * h];

            Color top = ceiling ? new Color(.098f, .196f, .239f) : new Color(.280f, .458f, .510f);
            Color band = ceiling ? new Color(.075f, .153f, .192f) : new Color(.230f, .400f, .458f);
            Color mid = new Color(.140f, .280f, .330f);
            Color deep = new Color(.055f, .110f, .150f);
            Color teal = new Color(.494f, .839f, .839f);

            for (int py = 0; py < h; py++)
            {
                float v = 1f - (py + .5f) / h;
                float fromTop = h - 1 - py;
                for (int x = 0; x < w; x++)
                {
                    float alpha = RoundRectAlpha(x, py, w, h, 7f);
                    if (alpha <= .002f) { px[py * w + x] = Clear; continue; }
                    Color col = v > .93f ? Color.Lerp(band, top, (v - .93f) / .07f)
                        : v > .55f ? Color.Lerp(mid, band, (v - .55f) / .38f)
                        : Color.Lerp(deep, mid, v / .55f);

                    // Layered sediment: a wavy horizontal line every 30 px.
                    float sy = fromTop + Mathf.Sin((x + r.x) * .021f + fromTop * .3f) * 3.5f;
                    float line = Mathf.Repeat(sy, 30f);
                    if (line < 1.15f) col = Color.Lerp(col, teal, .11f);

                    // Top lip catches the cave light, bottom sinks into the dark.
                    if (!ceiling && fromTop < 26f)
                        col = Color.Lerp(col, new Color(.627f, .941f, .894f), (1f - fromTop / 26f) * .26f);
                    if (v < .45f)
                        col = Color.Lerp(col, new Color(.016f, .047f, .071f), (0.45f - v) / .45f * .55f);

                    // Moss flecks along the top surface.
                    if (!ceiling && fromTop < 4f && (x + (int)r.x) % 36 < 13)
                        col = Color.Lerp(col, new Color(.431f, .922f, .804f), .13f);

                    // Edge highlight.
                    if (!ceiling && fromTop < 2f)
                        col = Color.Lerp(col, new Color(.698f, .980f, .941f), .5f);
                    if (ceiling && py < 2)
                        col = Color.Lerp(col, new Color(.549f, .843f, .863f), .18f);

                    col.a = 1f;
                    px[py * w + x] = col;
                }
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakePlatform(JellyRunData.RectF r)
        {
            int w = Mathf.Max(2, Mathf.RoundToInt(r.w));
            const int h = 16;
            var tex = NewTex(w, h);
            var px = new Color32[w * h];
            for (int py = 0; py < h; py++)
            {
                float v = 1f - (py + .5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float alpha = RoundRectAlpha(x, py, w, h, 5f);
                    if (alpha <= .002f) { px[py * w + x] = Clear; continue; }
                    Color col = Color.Lerp(new Color(.055f, .133f, .173f), new Color(.173f, .322f, .365f), v);
                    float fromTop = h - 1 - py;
                    if (fromTop < 2f) col = Color.Lerp(col, new Color(.471f, .922f, .843f), .85f);
                    if (fromTop >= 3f && fromTop < 5f && x % 26 < 9 && x > 8)
                        col = Color.Lerp(col, new Color(.345f, .780f, .745f), .75f);
                    col.a = 1f;
                    px[py * w + x] = col;
                }
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeSpike(JellyRunData.SpikeF s)
        {
            int w = Mathf.Max(2, Mathf.RoundToInt(s.w));
            int h = Mathf.Max(2, Mathf.RoundToInt(s.h));
            var tex = NewTex(w, h);
            var px = new Color32[w * h];
            int teeth = Mathf.Max(2, Mathf.RoundToInt(w / 22f));
            float step = w / (float)teeth;
            for (int py = 0; py < h; py++)
            for (int x = 0; x < w; x++)
            {
                float local = Mathf.Repeat(x, step) / step;
                float half = 1f - Mathf.Abs(local * 2f - 1f); // 0 at edges, 1 at centre
                float hh = s.up ? half * h : (1f - half) * h;
                bool inside = s.up ? py < hh : py >= h - hh;
                if (!inside) { px[py * w + x] = Clear; continue; }
                float t = s.up ? py / (float)h : 1f - py / (float)h;
                Color col = Color.Lerp(new Color(.486f, .545f, .588f), new Color(.812f, .847f, .871f), t);
                if ((s.up && py > h - 5) || (!s.up && py < 5)) col = Color.Lerp(col, new Color(.04f, .08f, .10f), .55f);
                col.a = 1f;
                px[py * w + x] = col;
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeSaw(float small)
        {
            int size = small > 0f ? 74 : 92;
            float radius = size * .5f - 3f;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float c = (size - 1) * .5f;
            const int teeth = 12;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float sector = Mathf.Repeat(ang / (Mathf.PI * 2f) * teeth, 1f);
                float edge = radius * (sector < .5f ? 1f : .78f);
                if (d > edge) { px[y * size + x] = Clear; continue; }
                float t = Mathf.Clamp01(d / radius);
                Color col = Color.Lerp(new Color(.416f, .490f, .533f), new Color(.788f, .839f, .867f), 1f - t);
                if (d < radius * .3f) col = new Color(.137f, .231f, .275f);
                col.a = 1f;
                px[y * size + x] = col;
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        // ----------------------------------------------------------- background

        static Sprite MakeCave(int layer)
        {
            const int h = 900;
            var tex = NewRepeatTex(CaveWidth, h);
            var px = new Color32[CaveWidth * h];
            float seed = layer == 0 ? 1.1f : 4.7f;
            Color32 col = layer == 0 ? new Color32(12, 37, 48, 255) : new Color32(10, 27, 36, 255);
            for (int x = 0; x < CaveWidth; x++)
            {
                float u = x / (float)CaveWidth * Mathf.PI * 2f;
                float n = .5f + .22f * Mathf.Sin(u * 3f + seed) + .16f * Mathf.Sin(u * 7f + seed * 2f)
                          + .12f * Mathf.Sin(u * 13f + seed * 3f);
                float hgt = (0.09f + n * .16f) * h;
                int top = Mathf.Clamp(Mathf.RoundToInt(hgt), 4, h - 1);
                for (int y = 0; y < h; y++)
                    px[y * CaveWidth + x] = y <= top ? col : Clear;
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }

        static Sprite MakeStalactites(int layer)
        {
            const int w = CaveWidth, h = 300;
            var tex = NewRepeatTex(w, h);
            var px = new Color32[w * h];
            float seed = layer == 0 ? 2.2f : 5.4f;
            Color col = layer == 0 ? new Color(.043f, .122f, .161f) : new Color(.035f, .094f, .129f);
            int count = 16;
            for (int i = 0; i < count; i++)
            {
                float cx = (i + .5f) * (w / (float)count);
                float n = Noise(i * 1.7f + seed);
                float half = (26f + n * 50f) * .5f;
                float len = (60f + n * 140f);
                for (int x = Mathf.Max(0, (int)(cx - half)); x < Mathf.Min(w, (int)(cx + half)); x++)
                {
                    float t = 1f - Mathf.Abs(x - cx) / half;
                    int tip = Mathf.RoundToInt(len * t);
                    for (int y = 0; y < tip && y < h; y++)
                    {
                        int row = h - 1 - y;
                        if (row < 0) break;
                        px[row * w + x] = col;
                    }
                }
            }
            tex.SetPixels32(px); tex.Apply();
            return SpriteFrom(tex);
        }
    }
}

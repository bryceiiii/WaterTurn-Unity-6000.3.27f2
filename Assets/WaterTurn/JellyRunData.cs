using System;
using UnityEngine;

namespace WaterTurn
{
    /// <summary>
    /// Everything that describes the "果冻潮池" side-scrolling demo, copied from
    /// Docs/waterturn-fish-run.html. Coordinates are HTML pixels (y grows down),
    /// so one Unity UI unit is one source pixel.
    ///
    /// The only intentional gameplay change is the goal: the twenty pearls of the
    /// HTML prototype became five chapter entrances (four levels each). Their
    /// ledges live in <see cref="EntrancePlatforms"/> and are additional solids,
    /// so the fish can stand on them and press E; nothing is picked up on contact.
    /// </summary>
    public static class JellyRunData
    {
        public const float ViewW = 960f;
        public const float ViewH = 540f;
        public const float WorldW = 7720f;
        public const float WorldH = 900f;
        public const float FishRadius = 13f;

        public readonly struct RectF
        {
            public readonly float x, y, w, h;
            public RectF(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
            public float Right => x + w;
            public float Bottom => y + h;
            public bool Contains(float px, float py, float margin = 0f) =>
                px >= x - margin && px <= Right + margin && py >= y - margin && py <= Bottom + margin;
            public bool Overlaps(RectF o) => x < o.Right && Right > o.x && y < o.Bottom && Bottom > o.y;
        }

        public readonly struct JellyF
        {
            public readonly float x, y, w, h;
            public readonly bool blob;
            public readonly float floodTo;
            public readonly Vector2 valve;
            public JellyF(float x, float y, float w, float h, bool blob = false, float floodTo = -1f, Vector2 valve = default)
            {
                this.x = x; this.y = y; this.w = w; this.h = h;
                this.blob = blob; this.floodTo = floodTo; this.valve = valve;
            }
        }

        public readonly struct SpikeF
        {
            public readonly float x, y, w, h;
            public readonly bool up;
            public SpikeF(float x, float y, float w, float h, bool up) { this.x = x; this.y = y; this.w = w; this.h = h; this.up = up; }
        }

        public readonly struct SawF
        {
            public readonly float ax, ay, bx, by, r, speed, phase;
            public SawF(float ax, float ay, float bx, float by, float r, float speed, float phase)
            { this.ax = ax; this.ay = ay; this.bx = bx; this.by = by; this.r = r; this.speed = speed; this.phase = phase; }
        }

        public readonly struct SwitchF
        {
            public readonly Vector2 position;
            public readonly float radius;
            public SwitchF(Vector2 position, float radius) { this.position = position; this.radius = radius; }
        }

        public readonly struct ZoneF
        {
            public readonly float x;
            public readonly string name, tip;
            public ZoneF(float x, string name, string tip) { this.x = x; this.name = name; this.tip = tip; }
        }

        public sealed class Entrance
        {
            public int chapter;
            public string zone;
            public float entryX, top;
            public float platformX, platformW;
            public Color32 accent;
            public string Title => "第" + ChineseNumber(chapter + 1) + "章 · " + zone;
            public int FirstLevel => chapter * 4;
            public int LastLevel => chapter * 4 + 3;
            public string LevelRange => (FirstLevel + 1).ToString("00") + "–" + (LastLevel + 1).ToString("00");
            public RectF Platform => new RectF(platformX, top, platformW, 16f);
        }

        static string ChineseNumber(int n) => n == 1 ? "一" : n == 2 ? "二" : n == 3 ? "三" : n == 4 ? "四" : "五";

        // ---------------------------------------------------------------- level

        public static readonly RectF[] Floors =
        {
            new RectF(-260, 720, 780, 180),
            new RectF(740, 720, 460, 180),
            new RectF(1440, 720, 460, 180),
            new RectF(2190, 700, 330, 200),
            new RectF(2740, 690, 420, 210),
            new RectF(3400, 660, 320, 240),
            new RectF(3700, 660, 560, 240),
            new RectF(3960, 0, 140, 300),
            new RectF(4260, 860, 200, 40),
            new RectF(4220, 300, 40, 600),
            new RectF(4460, 380, 240, 520),
            new RectF(4700, 700, 350, 200),
            new RectF(4770, 0, 260, 300),
            new RectF(5300, 700, 260, 200),
            new RectF(5560, 660, 220, 40),
            new RectF(5780, 640, 280, 260),
            new RectF(6320, 640, 330, 260),
            new RectF(6650, 680, 250, 220),
            new RectF(7180, 190, 540, 710),
        };

        public static readonly JellyF[] Jellies =
        {
            new JellyF(120, 600, 380, 120),
            new JellyF(760, 580, 420, 140),
            new JellyF(1460, 590, 420, 130),
            new JellyF(1990, 460, 160, 95, true),
            new JellyF(2210, 580, 290, 120),
            new JellyF(2570, 400, 150, 95, true),
            new JellyF(2780, 560, 340, 130),
            new JellyF(3420, 520, 280, 140),
            new JellyF(4110, 520, 140, 140),
            new JellyF(4268, 780, 184, 80, false, 300f, new Vector2(4360, 822)),
            new JellyF(4702, 580, 300, 120),
            new JellyF(5310, 590, 220, 110),
            new JellyF(5820, 500, 220, 140),
            new JellyF(6340, 520, 300, 120),
            new JellyF(6660, 560, 220, 120),
            new JellyF(6880, 450, 200, 90, true),
            new JellyF(7060, 300, 200, 90, true),
            new JellyF(6860, 160, 260, 90, true),
        };

        public static readonly SpikeF[] Spikes =
        {
            new SpikeF(5560, 624, 220, 36, true),
            new SpikeF(4770, 300, 260, 52, false),
        };

        public static readonly SawF[] Saws =
        {
            new SawF(5175, 548, 5175, 716, 34, 1.05f, 0f),
            new SawF(6112, 576, 6276, 576, 30, 1.55f, 1.7f),
        };

        public static readonly RectF Door = new RectF(4020, 300, 80, 360);
        public static readonly SwitchF Switch = new SwitchF(new Vector2(3830, 380), 38f);
        public static readonly RectF Portal = new RectF(7360, 60, 110, 130);

        public static readonly Vector2 FishStart = new Vector2(172, 662);

        public static readonly ZoneF[] Zones =
        {
            new ZoneF(0, "第一段 · 潮池", "按 S 扎进果冻蓄力，空格弹出。本段潮心在起点左侧石台。"),
            new ZoneF(1900, "第二段 · 断桥", "空中的果冻块也能落脚。本段潮心在断桥右侧石台。"),
            new ZoneF(3600, "第三段 · 水闸", "跳起来撞亮机关，闸门会开；井底阀门会把潮水抽上来。"),
            new ZoneF(4700, "第四段 · 尖刺走廊", "头顶有垂棘，脚下有棘床，锯片会来回扫。"),
            new ZoneF(6650, "第五段 · 终章", "最后一段攀升。五处潮心全亮，终点才认你。"),
        };

        // ------------------------------------------------------------ entrances

        public static readonly Entrance[] Entrances =
        {
            new Entrance { chapter = 0, zone = "潮池", entryX = 600f, top = 580f, platformX = 540f, platformW = 120f,
                accent = new Color32(108, 245, 220, 255) },
            new Entrance { chapter = 1, zone = "断桥", entryX = 2575f, top = 684f, platformX = 2480f, platformW = 190f,
                accent = new Color32(138, 220, 255, 255) },
            new Entrance { chapter = 2, zone = "水闸", entryX = 3905f, top = 644f, platformX = 3820f, platformW = 170f,
                accent = new Color32(255, 205, 120, 255) },
            new Entrance { chapter = 3, zone = "尖刺走廊", entryX = 5055f, top = 684f, platformX = 4990f, platformW = 130f,
                accent = new Color32(130, 235, 170, 255) },
            new Entrance { chapter = 4, zone = "终章", entryX = 7265f, top = 174f, platformX = 7190f, platformW = 150f,
                accent = new Color32(255, 220, 140, 255) },
        };

        public static readonly RectF[] EntrancePlatforms = BuildPlatforms();

        static RectF[] BuildPlatforms()
        {
            var list = new RectF[Entrances.Length];
            for (int i = 0; i < Entrances.Length; i++)
                list[i] = Entrances[i].Platform;
            return list;
        }

        // -------------------------------------------------------------- helpers

        public static JellyF Jelly(int index) => Jellies[index];

        public static int ChapterOfLevel(int level) => Mathf.Clamp(level / 4, 0, 4);

        public static string LevelName(int level)
        {
            if (level < 0 || level >= Campaign.Levels.Length) return "";
            return Campaign.Levels[level].Name;
        }

        // ------------------------------------------------------------- validate

        /// <summary>
        /// Checks the fifteen-second rule as data, not as pixels: exactly five
        /// entrances, four valid levels each, every entrance standing on its own
        /// ledge and clear of every hazard, and no two entrances close together.
        /// </summary>
        public static bool Validate(out string report)
        {
            var problems = new System.Collections.Generic.List<string>();
            if (Entrances.Length != 5) problems.Add("expected 5 entrances, found " + Entrances.Length);
            if (EntrancePlatforms.Length != Entrances.Length) problems.Add("platform count mismatch");
            if (Floors.Length != 19) problems.Add("expected 19 floors, found " + Floors.Length);
            if (Jellies.Length != 18) problems.Add("expected 18 jellies, found " + Jellies.Length);

            var seenLevels = new bool[20];
            for (int i = 0; i < Entrances.Length; i++)
            {
                var e = Entrances[i];
                if (e.chapter != i) problems.Add("entrance " + i + ": chapter out of order");
                if (e.platformW < 120f) problems.Add("entrance " + i + ": platform too narrow");
                if (e.entryX < e.platformX + 12f || e.entryX > e.platformX + e.platformW - 12f)
                    problems.Add("entrance " + i + ": entry point off its platform");
                for (int level = e.FirstLevel; level <= e.LastLevel; level++)
                {
                    if (level < 0 || level >= 20) { problems.Add("entrance " + i + ": level out of range"); continue; }
                    if (seenLevels[level]) problems.Add("entrance " + i + ": duplicate level " + level);
                    seenLevels[level] = true;
                }

                // The platform must not be buried inside a floor; a 2px overlap
                // with the floor it sits on is expected, so only flag deeper ones.
                foreach (var f in Floors)
                {
                    var p = e.Platform;
                    float overlap = Mathf.Min(p.Bottom, f.Bottom) - Mathf.Max(p.y, f.y);
                    if (overlap > 6f && p.x < f.Right && p.Right > f.x)
                        problems.Add("entrance " + i + ": platform buried in a floor");
                }

                // The standing point (fish centre on the ledge) must be free.
                var feet = new Vector2(e.entryX, e.top - FishRadius);
                foreach (var j in Jellies)
                {
                    var r = new RectF(j.x, j.y, j.w, j.h);
                    if (r.Contains(feet.x, feet.y, FishRadius * .5f))
                        problems.Add("entrance " + i + ": standing point inside jelly");
                }
                foreach (var s in Spikes)
                {
                    var r = new RectF(s.x, s.y, s.w, s.h);
                    if (r.Contains(feet.x, feet.y, FishRadius)) problems.Add("entrance " + i + ": standing point inside spikes");
                }
                foreach (var saw in Saws)
                {
                    if (Vector2.Distance(feet, new Vector2(saw.ax, saw.ay)) < saw.r + 30f &&
                        Vector2.Distance(feet, new Vector2(saw.bx, saw.by)) < saw.r + 30f)
                        problems.Add("entrance " + i + ": standing point too close to a saw");
                }
                if (Door.Contains(feet.x, feet.y, FishRadius)) problems.Add("entrance " + i + ": standing point inside the door");
                if (Vector2.Distance(feet, Switch.position) < Switch.radius + 20f)
                    problems.Add("entrance " + i + ": standing point on the switch");
            }

            for (int i = 0; i < Entrances.Length; i++)
                for (int j = i + 1; j < Entrances.Length; j++)
                    if (Mathf.Abs(Entrances[i].entryX - Entrances[j].entryX) < 800f)
                        problems.Add("entrances " + i + "/" + j + " too close");

            report = problems.Count == 0
                ? "OK · 5 处入口 · 20 关 · 最近入口间距 " + MinEntranceGap().ToString("0") + "px"
                : string.Join("\n", problems);
            return problems.Count == 0;
        }

        public static float MinEntranceGap()
        {
            float min = float.MaxValue;
            for (int i = 0; i < Entrances.Length; i++)
                for (int j = i + 1; j < Entrances.Length; j++)
                    min = Mathf.Min(min, Vector2.Distance(
                        new Vector2(Entrances[i].entryX, Entrances[i].top),
                        new Vector2(Entrances[j].entryX, Entrances[j].top)));
            return min == float.MaxValue ? 0f : min;
        }
    }
}

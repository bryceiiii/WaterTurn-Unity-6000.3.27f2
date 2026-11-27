using System.Collections.Generic;
using UnityEngine;

namespace WaterTurn
{
    /// <summary>
    /// The hand-made 16 px underwater sheet that ships under
    /// Resources/ManualArt/Pixel-16px Underwater.  The sheet is a single 208x48
    /// image, not a sliced sprite sheet, so this class does the cutting itself:
    /// every piece is a <see cref="Sprite.Create"/> window onto the texture, made
    /// once and cached.
    ///
    /// Two kinds of window are exported:
    /// <list type="bullet">
    /// <item>the ground block - a sand crust band plus a dirt body, tiled to fill
    /// any platform;</item>
    /// <item>the seabed decorations - coral, kelp, shells, bones, rocks.</item>
    /// </list>
    ///
    /// Pixels are drawn at <see cref="Scale"/>x, an exact integer so nothing
    /// blurs, and every window keeps the source pixel grid.
    /// </summary>
    public static class JellyRunTiles
    {
        const string Sheet =
            "ManualArt/Pixel-16px Underwater/Pixel-16px Underwater/tileset/tileset";

        /// <summary>Native tile pixels are drawn two canvas units each.</summary>
        public const int Scale = 2;

        // The ground block occupies x 0..47, y 0..47 of the sheet.  Only the
        // flat middle is used: the block's own corners are rounded, so sampling
        // them would stamp a notch every time the band repeats.
        public const int CrustX = 5, CrustY = 17, CrustW = 38, CrustH = 11;
        public const int DirtX = 6, DirtY = 28, DirtW = 36, DirtH = 20;

        static Texture2D sheet;
        static bool scanned;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly List<Object> made = new List<Object>();

        public static bool Ready
        {
            get
            {
                Load();
                return sheet != null;
            }
        }

        static void Load()
        {
            if (scanned) return;
            scanned = true;
            sheet = Resources.Load<Texture2D>(Sheet);
            if (sheet == null)
                Debug.LogWarning("WaterTurn: 海底素材缺失（" + Sheet + "），平台退回程序生成贴图。");
        }

        /// <summary>
        /// Drops every sprite this class built.  They are created at runtime and
        /// carry <see cref="HideFlags.DontSave"/>, so with domain reload turned
        /// off they would outlive the play session and come back as dead
        /// references on the next Play.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlayMode()
        {
            for (int i = 0; i < made.Count; i++)
                if (made[i] != null) Object.Destroy(made[i]);
            made.Clear();
            cache.Clear();
            sheet = null;
            scanned = false;
        }

        // ------------------------------------------------------------- ground

        /// <summary>
        /// A piece of the sand crust, <paramref name="cut"/> source pixels wide.
        /// Narrower cuts are slid sideways by <paramref name="phase"/> so a long
        /// floor does not end on the same fragment it started with.
        /// </summary>
        public static Sprite Crust(int cut, int phase)
        {
            cut = Mathf.Clamp(cut, 1, CrustW);
            return Window(CrustX, CrustY, CrustW, CrustH, cut, CrustH, phase, TopLeft);
        }

        /// <summary>A piece of the dirt body, cut from the top-left of the block.</summary>
        public static Sprite Dirt(int cut, int height, int phase)
        {
            cut = Mathf.Clamp(cut, 1, DirtW);
            height = Mathf.Clamp(height, 1, DirtH);
            return Window(DirtX, DirtY, DirtW, DirtH, cut, height, phase, TopLeft);
        }

        // -------------------------------------------------------- decorations

        public enum Deco
        {
            Shell,
            CoralPink,
            KelpTall,
            CoralRed,
            CoralGreen,
            WeedBush,
            TwigPurple,
            WeedGreen,
            WeedPurple,
            FishBone,
            TwigOrange,
            Bone,
            RockDark,
            Crystal,
            ShellSmall,
            RockWide,
            Snail,
        }

        // x, y (from the top), width, height - measured off the sheet.
        static readonly int[,] DecoRects =
        {
            {  50,  2, 13, 14 }, // Shell
            {  65,  2, 14, 14 }, // CoralPink
            {  81,  1, 14, 47 }, // KelpTall
            {  96,  2, 15, 14 }, // CoralRed
            { 112,  2, 15, 14 }, // CoralGreen
            {  48, 21, 39, 27 }, // WeedBush
            { 129,  4, 14, 12 }, // TwigPurple
            {  97, 21, 14, 27 }, // WeedGreen
            { 113, 21, 14, 27 }, // WeedPurple
            { 128, 16, 32, 15 }, // FishBone
            { 145,  4, 14, 12 }, // TwigOrange
            { 161,  1, 14, 14 }, // Bone
            { 160, 17, 32, 15 }, // RockDark
            { 177,  6, 14, 10 }, // Crystal
            { 193,  6, 14, 10 }, // ShellSmall
            { 128, 33, 64, 15 }, // RockWide
            { 192, 17, 16, 15 }, // Snail
        };

        /// <summary>A decoration standing on its bottom edge, pivot at bottom centre.</summary>
        public static Sprite Deco_Standing(Deco kind)
        {
            int i = (int)kind;
            return Window(DecoRects[i, 0], DecoRects[i, 1], DecoRects[i, 2], DecoRects[i, 3],
                DecoRects[i, 2], DecoRects[i, 3], 0, BottomCentre);
        }

        /// <summary>The same sprite measured from its top edge, for hanging growth.</summary>
        public static Sprite Deco_Hanging(Deco kind)
        {
            int i = (int)kind;
            return Window(DecoRects[i, 0], DecoRects[i, 1], DecoRects[i, 2], DecoRects[i, 3],
                DecoRects[i, 2], DecoRects[i, 3], 0, TopCentre);
        }

        public static Vector2 DecoSize(Deco kind)
        {
            int i = (int)kind;
            return new Vector2(DecoRects[i, 2], DecoRects[i, 3]);
        }

        // ------------------------------------------------------------- shared

        /// <summary>
        /// How many source pixels this segment takes.  A fixed tile width makes
        /// a long floor show one obvious period, so each segment gets 2/3 to the
        /// full tile - <paramref name="seed"/> keeps it stable per caller.
        /// </summary>
        public static int Span(int tileSource, int column, int row, int seed, int availableCanvas)
        {
            int min = Mathf.Max(8, tileSource * 2 / 3);
            int spread = Mathf.Max(1, tileSource - min + 1);
            int pick = min + Mathf.Abs((column * 73 + row * 37 + seed * 29) % spread);
            int cap = Mathf.Max(1, Mathf.CeilToInt(availableCanvas / (float)Scale));
            return Mathf.Min(pick, cap);
        }

        /// <summary>Which window of the tile this segment samples (0..49).</summary>
        public static int Phase(int column, int row, int seed) =>
            Mathf.Abs((column * 41 + row * 67 + seed * 17) % 50);

        static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        static readonly Vector2 TopCentre = new Vector2(.5f, 1f);
        static readonly Vector2 BottomCentre = new Vector2(.5f, 0f);

        /// <summary>
        /// Cuts a window out of a source rect.  <paramref name="cut"/>/<paramref name="height"/>
        /// is how much of the rect to keep, taken from its top-left corner;
        /// <paramref name="phase"/> (0..1, quantised to 1/50) slides that window
        /// around inside the rect so repeats can differ.
        /// </summary>
        static Sprite Window(int x, int yTop, int w, int h, int cut, int height, int phase, Vector2 pivot)
        {
            Load();
            if (sheet == null) return null;

            cut = Mathf.Clamp(cut, 1, w);
            height = Mathf.Clamp(height, 1, h);
            int step = Mathf.RoundToInt(Mathf.Repeat(phase / 50f, 1f) * 50f);
            float slide = (w - cut) * (step / 50f);
            float drop = (h - height) * (step / 50f);

            string key = x + "|" + yTop + "|" + w + "|" + h + "|" + cut + "|" + height + "|" + step + "|" + pivot.x + "," + pivot.y;
            Sprite hit;
            if (cache.TryGetValue(key, out hit) && hit != null) return hit;

            var sprite = Sprite.Create(sheet,
                new Rect(x + slide, sheet.height - yTop - height - drop, cut, height),
                pivot, 100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            cache[key] = sprite;
            made.Add(sprite);
            return sprite;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// 预览用外挂皮肤：把 PUNY DUNGEON 的砖石贴到当前关卡盘面上，并拼出俯视 2.5D 的块体。
    ///
    /// 块体模型：一格墙 = 一块有厚度的砖。俯视看到的是它的「上表面」，铺满整格；
    /// 只有朝屏幕下方那一侧才露出一条窄的「前立面」，表示高度。连续的一列墙里，
    /// 只有最下面那格画前立面，整堆墙才读成一个整体，而不是一格格贴片。
    ///
    /// 它只改运行时的 Image，不碰预制体、不碰场景资产 —— 关掉 applyOnStart 或摘掉
    /// 这个组件，关卡就回到原来的样子，适合先看效果再决定采不采。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonSkinPreview : MonoBehaviour
    {
        public const string AtlasPath = "PunyDungeon/punyworld-dungeon-tileset";

        [Header("图集瓦片（列, 行），行从贴图顶部数")]
        [Tooltip("上表面：俯视看到的平面，要能无缝平铺")] public Vector2Int topTile = new Vector2Int(6, 3);
        [Tooltip("前立面：竖直的砖墙面")] public Vector2Int frontTile = new Vector2Int(5, 2);
        [Tooltip("地面")] public Vector2Int floorTile = new Vector2Int(16, 14);
        [Tooltip("木箱")] public Vector2Int crateTile = new Vector2Int(21, 19);

        [Header("块体比例")]
        [Tooltip("前立面高度占一格的多少，越大块体越厚")] [Range(.05f, .40f)] public float frontRatio = .34f;
        [Tooltip("左右侧面宽度占一格的多少")] [Range(0f, .30f)] public float sideRatio = .16f;
        [Tooltip("墙堆外沿的暗边宽度（像素）")] public float outline = 2f;

        [Header("光照：上表面最亮，地面次之，立面最暗")]
        public Color topTint = new Color(.94f, .94f, .96f, 1f);
        public Color sideTint = new Color(.42f, .43f, .48f, 1f);
        public Color frontTint = new Color(.30f, .31f, .35f, 1f);
        public Color floorTint = new Color(.70f, .71f, .75f, 1f);
        public Color silhouette = new Color(.045f, .05f, .065f, 1f);
        public Color shadowTint = new Color(0f, 0f, 0f, .34f);

        public bool dropShadow = true;
        public bool applyOnStart = true;

        const int TileSize = 16;

        Texture2D atlas;
        Sprite topSprite, frontSprite, sideSprite, floorSprite, crateSprite, flat;
        Sprite aoTop, aoBottom, aoLeft, aoRight;
        bool applied;
        readonly List<Original> originals = new List<Original>();
        readonly List<GameObject> created = new List<GameObject>();

        struct Original
        {
            public Image image;
            public Sprite sprite;
            public Color color;
            public bool enabled;
        }

        void Start()
        {
            if (applyOnStart) Apply();
        }

        [ContextMenu("应用 2.5D 皮肤")]
        public void Apply()
        {
            if (applied) Clear();
            if (!LoadArt()) return;

            var board = FindFirstObjectByType<LevelBoard>();
            if (board == null || board.room == null)
            {
                Debug.LogWarning("DungeonSkinPreview: 当前场景没有 LevelBoard。");
                return;
            }
            var definition = FindFirstObjectByType<LevelDefinition>();
            var room = board.room;
            float cell = board.cell;

            var wallCells = new HashSet<int>();
            var wallViews = new List<KeyValuePair<int, RectTransform>>();
            var floorViews = new List<KeyValuePair<int, RectTransform>>();
            var crateViews = new List<RectTransform>();

            for (int i = 0; i < room.childCount; i++)
            {
                var child = room.GetChild(i) as RectTransform;
                if (child == null) continue;

                if (child.name.StartsWith("Wall "))
                {
                    int p;
                    if (!int.TryParse(child.name.Substring(5), out p)) continue;
                    wallCells.Add(p);
                    wallViews.Add(new KeyValuePair<int, RectTransform>(p, child));
                }
                else if (child.name.StartsWith("Floor "))
                {
                    int p;
                    int.TryParse(child.name.Substring(6), out p);
                    floorViews.Add(new KeyValuePair<int, RectTransform>(p, child));
                }
                else if (child.name.StartsWith("Crate"))
                {
                    crateViews.Add(child);
                }
            }

            int width = definition != null && definition.rows != null && definition.rows.Length > 0
                ? definition.rows[0].Length
                : 0;

            foreach (var pair in floorViews)
            {
                var image = pair.Value.GetComponent<Image>();
                if (image == null) continue;
                Remember(image);
                image.sprite = floorSprite;
                image.color = floorTint;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;

                // 四面各按相邻是否墙来决定压不压暗：走廊被墙围住的那几边最暗，
                // 这圈「墙根暗部」是走廊看起来低于台面的主要依据。
                if (dropShadow)
                {
                    int p = pair.Key;
                    bool left = width > 0 && p % width > 0 && wallCells.Contains(p - 1);
                    bool right = width > 0 && p % width < width - 1 && wallCells.Contains(p + 1);
                    bool above = width > 0 && p - width >= 0 && wallCells.Contains(p - width);
                    bool below = width > 0 && wallCells.Contains(p + width);
                    AddContactShadows(pair.Value, cell, left, right, above, below);
                }
            }

            foreach (var pair in wallViews)
            {
                int p = pair.Key;
                bool hasLeft = width > 0 && p % width > 0 && wallCells.Contains(p - 1);
                bool hasRight = width > 0 && p % width < width - 1 && wallCells.Contains(p + 1);
                bool hasAbove = width > 0 && p - width >= 0 && wallCells.Contains(p - width);
                bool hasBelow = width > 0 && wallCells.Contains(p + width);
                SkinWall(pair.Value, cell, hasLeft, hasRight, hasAbove, hasBelow);
            }

            foreach (var crate in crateViews)
            {
                var image = crate.GetComponent<Image>();
                if (image == null) continue;
                Remember(image);
                image.sprite = crateSprite;
                image.color = Color.white;
                image.preserveAspect = true;
            }

            applied = true;
        }

        void SkinWall(RectTransform wall, float cell,
            bool hasLeft, bool hasRight, bool hasAbove, bool hasBelow)
        {
            // 原来的图形整体让位，下面的块体全部由我们自己拼。
            foreach (var image in wall.GetComponentsInChildren<Image>(true))
                if (!image.name.StartsWith("Puny"))
                {
                    Remember(image);
                    image.enabled = false;
                }

            // 前立面只在「下方不是走廊」的时候出现：一列墙里只有最下面那格露出立面，
            // 整堆墙才读成一个厚块，而不是一格格贴片。左右侧面同理。
            float front = hasBelow ? 0f : Mathf.Max(2f, cell * frontRatio);
            float side = cell * sideRatio;
            float sideLeft = hasLeft ? 0f : side;
            float sideRight = hasRight ? 0f : side;
            float padLeft = hasLeft ? 0f : outline;
            float padRight = hasRight ? 0f : outline;
            float padTop = hasAbove ? 0f : outline;

            var block = NewGroup(wall, "Puny Block");

            // 剪影：整格铺一层近黑，各个面往内缩，露在外的部分就成了暗边。
            var rim = NewImage(block, "Puny Rim", flat, silhouette);
            Stretch(rim.rectTransform, 0f, 1f, 0f, 0f, 0f, 0f);

            // 左右侧面：朝内的墙不画，走廊两侧才立得起墙。
            if (sideLeft > 0f)
            {
                var left = NewImage(block, "Puny Side L", sideSprite, sideTint);
                var rect = left.rectTransform;
                rect.pivot = new Vector2(0f, .5f);
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.offsetMin = new Vector2(padLeft, front);
                rect.offsetMax = new Vector2(padLeft + sideLeft, -padTop);
            }
            if (sideRight > 0f)
            {
                var right = NewImage(block, "Puny Side R", sideSprite, sideTint);
                var rect = right.rectTransform;
                rect.pivot = new Vector2(1f, .5f);
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.offsetMin = new Vector2(-(padRight + sideRight), front);
                rect.offsetMax = new Vector2(-padRight, -padTop);
            }

            // 上表面：俯视看到的那一面，最后画，压住侧面和立面的接缝。
            var top = NewImage(block, "Puny Top", topSprite, topTint);
            Stretch(top.rectTransform, 0f, 1f, sideLeft, front, sideRight, padTop);

            if (front <= 0f) return;

            var face = NewImage(block, "Puny Front", frontSprite, frontTint);
            Stretch(face.rectTransform, 0f, 0f, padLeft, 0f, padRight, -front);

            // 上表面和立面之间压一条暗线，转折才立得住。
            var seam = NewImage(face.transform, "Puny Seam", flat,
                new Color(.03f, .04f, .06f, .5f));
            Place(seam.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(.5f, 1f), Vector2.zero, new Vector2(0f, Mathf.Max(1.5f, cell * .035f)));
        }

        /// <summary>走廊贴着墙的那几条边压暗：靠墙的一边最黑，往格心渐隐。</summary>
        void AddContactShadows(RectTransform cellRect, float cell,
            bool left, bool right, bool above, bool below)
        {
            float depth = cell * .20f;
            if (above) Edge(cellRect, "Puny AO Top", aoTop, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -depth), Vector2.zero);
            if (below) Edge(cellRect, "Puny AO Bottom", aoBottom, new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, depth));
            if (left) Edge(cellRect, "Puny AO Left", aoLeft, new Vector2(0f, 0f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(depth, 0f));
            if (right) Edge(cellRect, "Puny AO Right", aoRight, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-depth, 0f), Vector2.zero);
        }

        void Edge(RectTransform parent, string name, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var image = NewImage(parent, name, sprite, shadowTint);
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = (anchorMin + anchorMax) * .5f;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        /// <summary>
        /// 横向拉满、纵向以 <paramref name="anchorYBottom"/> / <paramref name="anchorYTop"/>
        /// 为基线，用 offsetMin/offsetMax 逐边控制内缩——块体之间的接缝只在最外沿留出来。
        /// </summary>
        static void Stretch(RectTransform rect, float anchorYBottom, float anchorYTop,
            float left, float bottom, float right, float top)
        {
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchorMin = new Vector2(0f, anchorYBottom);
            rect.anchorMax = new Vector2(1f, anchorYTop);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        RectTransform NewGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform) go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .5f);
            created.Add(go);
            return rect;
        }

        Image NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform) go.transform;
            rect.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            created.Add(go);
            return image;
        }

        void Remember(Image image)
        {
            foreach (var o in originals)
                if (o.image == image) return;
            originals.Add(new Original
            {
                image = image,
                sprite = image.sprite,
                color = image.color,
                enabled = image.enabled,
            });
        }

        [ContextMenu("还原原始皮肤")]
        public void Clear()
        {
            foreach (var o in originals)
            {
                if (o.image == null) continue;
                o.image.sprite = o.sprite;
                o.image.color = o.color;
                o.image.enabled = o.enabled;
            }
            originals.Clear();

            foreach (var go in created)
                if (go != null) Destroy(go);
            created.Clear();

            applied = false;
        }

        bool LoadArt()
        {
            atlas = Resources.Load<Texture2D>(AtlasPath);
            if (atlas == null)
            {
                var asset = Resources.Load<Sprite>(AtlasPath);
                if (asset != null) atlas = asset.texture;
            }
            if (atlas == null)
            {
                Debug.LogWarning("DungeonSkinPreview: 找不到图集 " + AtlasPath);
                return false;
            }

            topSprite = Tile(topTile);
            frontSprite = Tile(frontTile) ?? topSprite;
            sideSprite = frontSprite;
            floorSprite = Tile(floorTile) ?? topSprite;
            crateSprite = Tile(crateTile);
            if (flat == null) flat = MakeFlat();
            if (aoTop == null)
            {
                aoTop = MakeEdgeGradient(0);
                aoBottom = MakeEdgeGradient(1);
                aoLeft = MakeEdgeGradient(2);
                aoRight = MakeEdgeGradient(3);
            }
            return topSprite != null;
        }

        Sprite Tile(Vector2Int tile)
        {
            int x = tile.x * TileSize;
            int y = atlas.height - (tile.y + 1) * TileSize;
            if (x < 0 || y < 0 || x + TileSize > atlas.width || y + TileSize > atlas.height)
                return null;
            return Sprite.Create(atlas, new Rect(x, y, TileSize, TileSize),
                new Vector2(.5f, .5f), TileSize, 0, SpriteMeshType.FullRect);
        }

        /// <summary>纯白 1x1，做剪影和细线用。</summary>
        static Sprite MakeFlat()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 100f);
        }

        /// <summary>黑色单向渐变：贴住的那条边不透明，向另一侧渐隐。0=上 1=下 2=左 3=右。</summary>
        static Sprite MakeEdgeGradient(int side)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float) y / (size - 1);   // 0 = 贴图底部
                    float v = (float) x / (size - 1);   // 0 = 贴图左侧
                    float t = side == 0 ? u : side == 1 ? 1f - u : side == 2 ? 1f - v : v;
                    byte alpha = (byte) (Mathf.Clamp01(t * t) * 255f);
                    pixels[y * size + x] = new Color32(0, 0, 0, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        }
    }
}

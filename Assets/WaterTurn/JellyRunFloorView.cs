using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// One piece of static level geometry (floor, ledge or ceiling).
    ///
    /// The body keeps the generated sprite, which is a flat dark mass and reads
    /// as solid rock.  The face the fish actually walks on is dressed with the
    /// hand-made pixel pack instead: a sand crust band straight across, and a
    /// few rows of dirt under it, cut from the pack's ground block and laid at
    /// an exact 2x pixel scale.
    /// </summary>
    [ExecuteAlways]
    public sealed class JellyRunFloorView : MonoBehaviour
    {
        /// <summary>
        /// How many 20 px dirt rows hang under the sand crust.  One, so the band
        /// stays a readable edge instead of turning the whole cliff brown.  It
        /// is a constant rather than an inspector field because these views are
        /// baked at runtime and never serialised - a tweak would not survive.
        /// </summary>
        const int BandRows = 1;

        public bool ceiling;
        public Image image;

        readonly List<Image> pool = new List<Image>();
        int used;
        int laidWidth = int.MinValue;
        int laidHeight = int.MinValue;
        bool laidCeiling;
        bool booted;
        bool queued;

        public RectTransform Rect => (RectTransform)transform;

        void OnEnable() => Refresh();
        void OnValidate() => Refresh();
        void OnRectTransformDimensionsChange() => Refresh();

        // Play mode builds the band a frame later.  The game binds the scene
        // during Awake, and creating children from there makes Unity skip the
        // layout callbacks it would normally send ("SendMessage cannot be called
        // during Awake") - the band then stays unsized until something else
        // dirties the canvas.
        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (!booted)
            {
                booted = true;
                queued = true;
            }
            if (!queued) return;
            queued = false;
            Refresh();
        }

        public void EnsureVisuals()
        {
            if (image == null) image = GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
                image.raycastTarget = false;
            }
        }

        public void Refresh()
        {
            EnsureVisuals();
            var rect = Rect.rect;
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);

            image.sprite = JellyRunArt.FloorSprite(width, height, ceiling);
            image.type = Image.Type.Simple;
            image.color = Color.white;

            // Play mode owns the tileset face.  In the editor - and whenever the
            // pack is missing - the generated body stands alone, so an authored
            // instance never holds runtime sprites it would want to save.
            if (!Application.isPlaying || !JellyRunTiles.Ready)
            {
                laidWidth = int.MinValue;
                laidHeight = int.MinValue;
                ClearSurface();
                return;
            }

            if (!booted)
            {
                queued = true;
                return;
            }

            if (width == laidWidth && height == laidHeight && ceiling == laidCeiling && used > 0) return;
            laidWidth = width;
            laidHeight = height;
            laidCeiling = ceiling;

            LaySurface(width, height);
        }

        void LaySurface(int width, int height)
        {
            if (width < 8)
            {
                ClearSurface();
                return;
            }

            const int scale = JellyRunTiles.Scale;
            int crustH = JellyRunTiles.CrustH * scale;
            int dirtH = JellyRunTiles.DirtH * scale;

            used = 0;

            // Sand crust first, then dirt rows while they still fit inside the
            // piece, so a 40 px shelf gets the crust only and a tall cliff gets
            // the full body.
            LayBand(width, 0, crustH, true, 0);

            int y = crustH;
            int rows = Mathf.Clamp(Mathf.Min(BandRows, (height - crustH) / dirtH), 0, 8);
            for (int row = 0; row < rows; row++)
            {
                LayBand(width, y, dirtH, false, row + 1);
                y += dirtH;
            }

            for (int i = used; i < pool.Count; i++)
                if (pool[i] != null) pool[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// Lays one horizontal band of the block across <paramref name="width"/>,
        /// starting <paramref name="y"/> pixels away from the walkable edge.
        /// Segments alternate mirrored so the speckles do not stamp the same
        /// pattern along the whole floor.
        /// </summary>
        void LayBand(int width, int y, int height, bool crust, int row)
        {
            const int scale = JellyRunTiles.Scale;
            int tileW = (crust ? JellyRunTiles.CrustW : JellyRunTiles.DirtW) * scale;
            int sourceH = Mathf.Max(1, Mathf.RoundToInt(height / (float)scale));

            int x = 0;
            int column = 0;
            int seed = transform.GetSiblingIndex();
            while (x < width)
            {
                int remaining = width - x;
                int cut = JellyRunTiles.Span(tileW / scale, column, row, seed, remaining);
                int drawW = Mathf.Min(cut * scale, remaining);

                var img = Take();
                var rt = img.rectTransform;
                bool flip = (column & 1) == 1;

                if (ceiling)
                {
                    // The walkable face is the bottom edge, so the band is
                    // flipped and grows upward into the rock above it.
                    rt.anchorMin = rt.anchorMax = Vector2.zero;
                    rt.pivot = Vector2.zero;
                    rt.anchoredPosition = new Vector2(flip ? x + drawW : x, y + height);
                    rt.localScale = new Vector3(flip ? -1f : 1f, -1f, 1f);
                }
                else
                {
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(flip ? x + drawW : x, -y);
                    rt.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
                }

                rt.sizeDelta = new Vector2(drawW, height);

                int phase = JellyRunTiles.Phase(column, row, seed);
                img.sprite = crust
                    ? JellyRunTiles.Crust(cut, phase)
                    : JellyRunTiles.Dirt(cut, sourceH, phase);
                img.enabled = img.sprite != null;

                x += drawW;
                column++;
            }
        }

        Image Take()
        {
            if (used < pool.Count && pool[used] != null)
            {
                var reused = pool[used];
                used++;
                reused.gameObject.SetActive(true);
                return reused;
            }

            var go = new GameObject("Face " + used, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;

            if (used < pool.Count) pool[used] = img;
            else pool.Add(img);
            used++;
            return img;
        }

        /// <summary>
        /// Drops the runtime faces.  They carry <see cref="HideFlags.DontSave"/>,
        /// so clear the flags before destroying or the object merely goes
        /// invisible and stacks up on the next build.
        /// </summary>
        void ClearSurface()
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null) Discard(pool[i].gameObject);
            pool.Clear();
            used = 0;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Face")) Discard(child.gameObject);
            }
        }

        static void Discard(GameObject go)
        {
            if (go == null) return;
            go.hideFlags = HideFlags.None;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go, true);
        }
    }
}

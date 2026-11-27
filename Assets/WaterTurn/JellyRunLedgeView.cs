using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// One floating ledge in front of a chapter shrine.
    ///
    /// The RectTransform is only the 16 px collider (see
    /// <see cref="JellyRunData.EntrancePlatforms"/>), so the art hangs from
    /// children: a sand crust along the walkable line, cut from the pixel pack's
    /// ground block.  The crust is the whole platform - the shrine's halo starts
    /// three pixels under the walkable line, so a dirt body under it would bury
    /// the pearl the player is supposed to aim at.  Everything stays inside the
    /// ledge width, so nothing looks standable that is not.
    /// </summary>
    [ExecuteAlways]
    public sealed class JellyRunLedgeView : MonoBehaviour
    {
        public Image image;

        readonly List<Image> pool = new List<Image>();
        int used;
        int laidWidth = int.MinValue;
        bool booted;
        bool queued;

        public RectTransform Rect => (RectTransform)transform;

        void OnEnable() => Refresh();
        void OnValidate() => Refresh();
        void OnRectTransformDimensionsChange() => Refresh();

        // Play mode builds the art a frame later.  The game binds the scene
        // during Awake, and creating children from there makes Unity skip the
        // layout callbacks it would normally send ("SendMessage cannot be called
        // during Awake") - the sprite then stays unsized until the canvas is
        // dirtied again.
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

            int width = Mathf.RoundToInt(Rect.rect.width);

            // Play mode owns the tileset art.  In the editor - and whenever the
            // pack is missing - the generated bar stays, so an authored prefab
            // instance never holds a runtime sprite it would want to save.
            if (!Application.isPlaying || !JellyRunTiles.Ready)
            {
                if (laidWidth == int.MinValue) return;
                laidWidth = int.MinValue;
                ClearArt();
                image.enabled = true;
                image.type = Image.Type.Simple;
                image.color = Color.white;
                image.sprite = JellyRunArt.LedgeSprite(width);
                return;
            }

            if (!booted)
            {
                queued = true;
                return;
            }

            if (width == laidWidth && used > 0) return;
            laidWidth = width;

            image.enabled = false;
            LayPlatform(width);
        }

        void LayPlatform(int width)
        {
            if (width < 8)
            {
                ClearArt();
                return;
            }

            used = 0;
            LayCrust(width);

            for (int i = used; i < pool.Count; i++)
                if (pool[i] != null) pool[i].gameObject.SetActive(false);
        }

        void LayCrust(int width)
        {
            const int scale = JellyRunTiles.Scale;
            const int height = JellyRunTiles.CrustH * scale;

            int x = 0;
            int column = 0;
            int seed = transform.GetSiblingIndex() + 5;
            while (x < width)
            {
                int remaining = width - x;
                int cut = JellyRunTiles.Span(JellyRunTiles.CrustW, column, 0, seed, remaining);
                int drawW = Mathf.Min(cut * scale, remaining);

                var img = Take();
                var rt = img.rectTransform;
                bool flip = (column & 1) == 1;

                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(flip ? x + drawW : x, 0f);
                rt.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
                rt.sizeDelta = new Vector2(drawW, height);

                img.sprite = JellyRunTiles.Crust(cut, JellyRunTiles.Phase(column, 0, seed));
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

            var go = new GameObject("Art " + used, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;

            if (used < pool.Count) pool[used] = img;
            else pool.Add(img);
            used++;
            return img;
        }

        /// <summary>
        /// Removes children this view never wants to keep: the tileset faces
        /// outside play mode, and the "Moss"/"Art" slabs built by earlier
        /// designs.  Anything made at runtime carries
        /// <see cref="HideFlags.DontSave"/>, so clear the flags before
        /// destroying or the object merely goes invisible.
        /// </summary>
        void ClearArt()
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null) Discard(pool[i].gameObject);
            pool.Clear();
            used = 0;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name == "Moss") { Discard(child.gameObject); continue; }
                if (child.name == "Art" && !Application.isPlaying) Discard(child.gameObject);
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

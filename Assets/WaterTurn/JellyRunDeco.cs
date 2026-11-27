using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// Scatters the pixel pack's seabed props - kelp, coral, shells, bones and
    /// rocks - along the level.
    ///
    /// Everything is built at runtime under a container called "Deco" that sits
    /// right above the floors in the draw order, so props read as lying on the
    /// rock while the fish and the jellies still pass in front of them.  The
    /// container and its children carry <see cref="HideFlags.DontSave"/>, so the
    /// authored scene never stores them; they are rebuilt on every Play.
    /// </summary>
    public static class JellyRunDeco
    {
        const string Container = "Deco";

        /// <summary>Props kept short, so they never reach the fish's swimming line.</summary>
        static readonly JellyRunTiles.Deco[] Litter =
        {
            JellyRunTiles.Deco.Shell,
            JellyRunTiles.Deco.ShellSmall,
            JellyRunTiles.Deco.Snail,
            JellyRunTiles.Deco.Bone,
            JellyRunTiles.Deco.Crystal,
            JellyRunTiles.Deco.FishBone,
        };

        /// <summary>One-tile plants.</summary>
        static readonly JellyRunTiles.Deco[] Sprigs =
        {
            JellyRunTiles.Deco.CoralPink,
            JellyRunTiles.Deco.CoralRed,
            JellyRunTiles.Deco.CoralGreen,
            JellyRunTiles.Deco.TwigPurple,
            JellyRunTiles.Deco.TwigOrange,
        };

        /// <summary>Tall growth and rock piles, for the gaps between ledges.</summary>
        static readonly JellyRunTiles.Deco[] Growth =
        {
            JellyRunTiles.Deco.KelpTall,
            JellyRunTiles.Deco.WeedGreen,
            JellyRunTiles.Deco.WeedPurple,
            JellyRunTiles.Deco.WeedBush,
        };

        static readonly JellyRunTiles.Deco[] Mass =
        {
            JellyRunTiles.Deco.RockDark,
            JellyRunTiles.Deco.RockWide,
        };

        public static void Build(RectTransform world)
        {
            if (world == null || !JellyRunTiles.Ready) return;

            var root = ContainerFor(world);

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.hideFlags = HideFlags.None;
                Object.Destroy(child);
            }

            var floors = world.GetComponentsInChildren<JellyRunFloorView>(true);
            var busy = BusySpots(world, floors.Length);

            int planted = 0;
            for (int i = 0; i < floors.Length; i++)
                planted += Dress(root, world, floors[i], busy, i);

            root.SetSiblingIndex(Mathf.Min(floors.Length, world.childCount - 1));
            if (planted > 0) root.gameObject.SetActive(true);
        }

        static RectTransform ContainerFor(RectTransform world)
        {
            var found = world.Find(Container) as RectTransform;
            if (found != null) return found;

            var go = new GameObject(Container, typeof(RectTransform));
            go.hideFlags = HideFlags.DontSave;
            var rt = (RectTransform)go.transform;
            rt.SetParent(world, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>
        /// X positions props must keep clear of: the shrine ledges, the spawn and
        /// the exit portal.  Nothing looks worse than kelp growing through the
        /// prompt the player has to read.
        /// </summary>
        static List<float> BusySpots(RectTransform world, int reserve)
        {
            var busy = new List<float>(reserve);
            foreach (var ledge in world.GetComponentsInChildren<JellyRunLedgeView>(true))
                busy.Add(LocalX(world, ledge.Rect));

            foreach (var name in new[] { "Spawn", "Portal", "Well Valve", "Switch" })
            {
                var t = world.Find(name);
                if (t != null) busy.Add(LocalX(world, (RectTransform)t));
            }
            return busy;
        }

        static int Dress(RectTransform root, RectTransform world, JellyRunFloorView floor,
            List<float> busy, int index)
        {
            var rt = floor.Rect;
            Vector3 min = world.InverseTransformPoint(rt.TransformPoint(new Vector3(rt.rect.xMin, rt.rect.yMin)));
            Vector3 max = world.InverseTransformPoint(rt.TransformPoint(new Vector3(rt.rect.xMax, rt.rect.yMax)));

            float left = min.x, right = max.x;
            float width = right - left;
            float height = max.y - min.y;
            if (width < 110f) return 0;

            // Vertical shafts are much taller than they are wide; their "top" is
            // not a surface anything stands on.
            bool wall = width < height * .5f;
            if (wall) return 0;

            var rng = new System.Random(17_000 + index * 7919);
            int count = Mathf.Clamp(Mathf.RoundToInt(width / 140f), 1, 6);
            float span = width / count;

            int planted = 0;
            for (int slot = 0; slot < count; slot++)
            {
                float x = left + span * (slot + .5f) + (float)(rng.NextDouble() - .5) * span * .45f;
                x = Mathf.Clamp(x, left + 16f, right - 16f);

                // Keep clear of the shrines and the spawn, but try to slide out
                // of the way instead of dropping the slot outright.
                for (int nudge = 0; nudge < 3 && Blocked(x, busy); nudge++)
                    x = Mathf.Clamp(x + (nudge % 2 == 0 ? span * .35f : -span * .7f), left + 16f, right - 16f);
                if (Blocked(x, busy)) continue;

                if (floor.ceiling)
                {
                    if (rng.NextDouble() < .55) continue;
                    Add(root, x, min.y, Pick(rng, Growth), true);
                }
                else
                {
                    double roll = rng.NextDouble();
                    JellyRunTiles.Deco kind = roll < .28 ? Pick(rng, Growth)
                        : roll < .62 ? Pick(rng, Sprigs)
                        : roll < .86 ? Pick(rng, Litter)
                        : Pick(rng, Mass);
                    Add(root, x, max.y - 2f, kind, false);
                }
                planted++;

                // Half of the props get a neighbour, which reads as a patch
                // rather than a single sticker on the rock.
                if (rng.NextDouble() < .55)
                {
                    float side = rng.NextDouble() < .5 ? -1f : 1f;
                    float nx = x + side * (18f + (float)rng.NextDouble() * 22f);
                    nx = Mathf.Clamp(nx, left + 10f, right - 10f);
                    if (!Blocked(nx, busy))
                        Add(root, nx, floor.ceiling ? min.y : max.y - 2f, Pick(rng, Sprigs), floor.ceiling);
                }
            }
            return planted;
        }

        static bool Blocked(float x, List<float> busy)
        {
            for (int i = 0; i < busy.Count; i++)
                if (Mathf.Abs(busy[i] - x) < 118f) return true;
            return false;
        }

        static JellyRunTiles.Deco Pick(System.Random rng, JellyRunTiles.Deco[] from) =>
            from[rng.Next(from.Length)];

        /// <summary>
        /// Places one prop.  Standing props are pivoted at their bottom edge on
        /// the walkable line; hanging ones are flipped and grown downward from
        /// the ceiling's underside.
        /// </summary>
        static void Add(RectTransform root, float x, float y, JellyRunTiles.Deco kind, bool hanging)
        {
            var size = JellyRunTiles.DecoSize(kind) * JellyRunTiles.Scale;
            var sprite = hanging ? JellyRunTiles.Deco_Hanging(kind) : JellyRunTiles.Deco_Standing(kind);
            if (sprite == null) return;

            var go = new GameObject(kind.ToString(), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(root, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.sizeDelta = size;

            if (hanging)
            {
                rt.pivot = new Vector2(.5f, 1f);
                rt.anchoredPosition = new Vector2(x, y - size.y);
                rt.localScale = new Vector3(1f, -1f, 1f);
            }
            else
            {
                rt.pivot = new Vector2(.5f, 0f);
                rt.anchoredPosition = new Vector2(x, y);
                rt.localScale = Vector3.one;
            }

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
        }

        static float LocalX(RectTransform world, RectTransform rt) =>
            world.InverseTransformPoint(rt.TransformPoint(new Vector3(rt.rect.center.x, rt.rect.center.y, 0f))).x;
    }
}

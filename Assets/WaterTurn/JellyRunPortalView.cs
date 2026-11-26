using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunPortalView : MonoBehaviour
    {
        public RectTransform trigger;
        public Image glow, core;
        public Image[] rings = new Image[2];
        public Image[] arcs = new Image[3];
        public Text count, state;

        void OnEnable() => Refresh();
        void OnValidate() => Refresh();

        public void EnsureVisuals()
        {
            if (trigger == null)
            {
                var t = JellyRunUi.FindRect(transform, "Trigger");
                if (t == null) t = JellyRunUi.NewRect(transform, "Trigger", Vector2.zero, new Vector2(110f, 130f));
                trigger = t;
            }
            if (glow == null)
            {
                glow = JellyRunUi.NewImage(transform, "Glow", Vector2.zero, new Vector2(280f, 280f),
                    JellyRunArt.Glow, new Color(.47f, .59f, .67f, .26f));
            }
            for (int i = 0; i < arcs.Length; i++)
            {
                if (arcs[i] == null)
                {
                    arcs[i] = JellyRunUi.NewImage(transform, "Arc " + i, Vector2.zero,
                        Vector2.one * (110f + i * 26f), JellyRunArt.Arc(i * 120f, 100f),
                        new Color(.47f, .59f, .67f, .45f));
                }
            }
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] == null)
                {
                    rings[i] = JellyRunUi.NewImage(transform, i == 0 ? "OuterRing" : "InnerRing",
                        Vector2.zero, Vector2.one * (i == 0 ? 116f : 100f), JellyRunArt.Ring,
                        new Color(.47f, .59f, .67f, i == 0 ? .55f : .28f));
                }
            }
            if (core == null)
            {
                core = JellyRunUi.NewImage(transform, "Core", Vector2.zero, new Vector2(80f, 80f),
                    JellyRunArt.Glow, new Color(.59f, .70f, .76f, .35f));
            }
            if (count == null)
            {
                count = JellyRunUi.NewText(transform, "Count", "0 / 20", 15,
                    new Color(.74f, .84f, .87f, .75f), TextAnchor.MiddleCenter,
                    new Vector2(0f, 76f), new Vector2(220f, 22f));
                count.fontStyle = FontStyle.Bold;
            }
            if (state == null)
            {
                state = JellyRunUi.NewText(transform, "State", "封印中", 11,
                    new Color(.63f, .75f, .78f, .65f), TextAnchor.MiddleCenter,
                    new Vector2(0f, 58f), new Vector2(220f, 18f));
            }
        }

        public void Refresh()
        {
            EnsureVisuals();
            glow.sprite = JellyRunArt.Glow;
            core.sprite = JellyRunArt.Glow;
            count.font = JellyRunUi.Font;
            state.font = JellyRunUi.Font;
            for (int i = 0; i < rings.Length; i++)
            {
                rings[i].sprite = JellyRunArt.Ring;
            }
            for (int i = 0; i < arcs.Length; i++)
            {
                arcs[i].sprite = JellyRunArt.Arc(i * 120f, 100f);
            }
        }
    }
}

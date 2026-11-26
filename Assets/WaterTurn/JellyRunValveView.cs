using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunValveView : MonoBehaviour
    {
        public float radius = 70f;
        public JellyRunJellyView target;
        public Image glow, ring;
        public Text label;

        void OnEnable() => Refresh();
        void OnValidate() => Refresh();

        public void EnsureVisuals()
        {
            if (glow == null)
            {
                glow = JellyRunUi.NewImage(transform, "Glow", Vector2.zero, new Vector2(92f, 92f),
                    JellyRunArt.Glow, new Color(1f, .82f, .45f, .22f));
            }
            if (ring == null)
            {
                ring = JellyRunUi.NewImage(transform, "Ring", Vector2.zero, new Vector2(42f, 42f),
                    JellyRunArt.Ring, new Color(1f, .82f, .45f, .75f));
            }
            if (transform.Find("Hub") == null)
            {
                JellyRunUi.NewImage(transform, "Hub", Vector2.zero, new Vector2(13f, 13f),
                    JellyRunArt.Circle, new Color(1f, .90f, .62f, .95f));
            }
            if (label == null)
            {
                label = JellyRunUi.NewText(transform, "Label", "阀门", 11,
                    new Color(1f, .88f, .58f, .95f), TextAnchor.MiddleCenter,
                    new Vector2(0f, -28f), new Vector2(100f, 18f));
                label.fontStyle = FontStyle.Bold;
            }
        }

        public void Refresh()
        {
            EnsureVisuals();
            glow.sprite = JellyRunArt.Glow;
            glow.color = new Color(1f, .82f, .45f, .22f);
            glow.rectTransform.sizeDelta = new Vector2(92f, 92f);
            ring.sprite = JellyRunArt.Ring;
            ring.color = new Color(1f, .82f, .45f, .75f);
            ring.rectTransform.sizeDelta = new Vector2(42f, 42f);
            label.font = JellyRunUi.Font;
        }
    }
}

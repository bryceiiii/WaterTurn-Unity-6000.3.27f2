using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunDoorView : MonoBehaviour
    {
        public Image frame;
        public RectTransform bars;

        public RectTransform Rect => (RectTransform)transform;

        void OnEnable() => Refresh();
        void OnValidate() => Refresh();

        public void EnsureVisuals()
        {
            if (frame == null)
            {
                frame = GetComponent<Image>();
                if (frame == null) frame = gameObject.AddComponent<Image>();
            }
            frame.raycastTarget = false;
            if (bars == null)
            {
                var b = JellyRunUi.FindRect(transform, "Bars");
                if (b == null) b = JellyRunUi.NewRect(transform, "Bars", Vector2.zero, new Vector2(70f, 390f));
                bars = b;
            }
            if (bars.childCount == 0) BuildBars();
        }

        void BuildBars()
        {
            var rect = Rect.rect;
            float w = rect.width > 10f ? rect.width : 80f;
            float h = rect.height > 10f ? rect.height : 360f;
            bars.sizeDelta = new Vector2(w - 10f, h + 34f);
            for (int i = 0; i < 4; i++)
            {
                var bar = JellyRunUi.NewImage(bars, "Bar " + i,
                    new Vector2(-w * .5f + 8f + i * (w - 16f) / 3.2f, 0f), new Vector2(8f, h + 20f),
                    JellyRunArt.Rounded, new Color32(45, 74, 87, 255));
                bar.type = Image.Type.Sliced;
                var shine = JellyRunUi.NewImage(bar.rectTransform, "Shine", Vector2.zero,
                    new Vector2(2f, h + 20f), JellyRunArt.Rounded, new Color32(160, 240, 230, 46));
                shine.type = Image.Type.Sliced;
            }
            for (int i = 0; i < 3; i++)
            {
                var cross = JellyRunUi.NewImage(bars, "Cross " + i,
                    new Vector2(0f, 40f + i * (h / 3.2f) - h * .5f), new Vector2(w - 8f, 7f),
                    JellyRunArt.Rounded, new Color32(40, 68, 79, 255));
                cross.type = Image.Type.Sliced;
            }
        }

        public void Refresh()
        {
            EnsureVisuals();
            frame.sprite = JellyRunArt.Rounded;
            frame.type = Image.Type.Sliced;
            frame.color = new Color32(19, 40, 51, 255);
        }
    }
}

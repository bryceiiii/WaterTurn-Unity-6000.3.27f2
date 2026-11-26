using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunJellyView : MonoBehaviour
    {
        public bool blob;
        public float floodTo = -1f;
        public JellyGraphic body;
        public Image glow;
        public Image shadow;

        public RectTransform Rect => (RectTransform)transform;

        void OnEnable() => Refresh();
        void OnValidate() => Refresh();
        void OnRectTransformDimensionsChange() => Refresh();

        public void EnsureVisuals()
        {
            var glowRect = JellyRunUi.FindRect(transform, "Glow");
            if (glowRect == null) glowRect = JellyRunUi.NewRect(transform, "Glow", Vector2.zero, new Vector2(100, 100));
            glow = glowRect.GetComponent<Image>() ?? glowRect.gameObject.AddComponent<Image>();
            glow.raycastTarget = false;

            var shadowRect = JellyRunUi.FindRect(transform, "Shadow");
            if (shadowRect == null) shadowRect = JellyRunUi.NewRect(transform, "Shadow", Vector2.zero, new Vector2(100, 18));
            shadow = shadowRect.GetComponent<Image>() ?? shadowRect.gameObject.AddComponent<Image>();
            shadow.raycastTarget = false;

            var bodyRect = JellyRunUi.FindRect(transform, "Body");
            if (bodyRect == null)
            {
                bodyRect = JellyRunUi.NewRect(transform, "Body", Vector2.zero, Vector2.zero);
                bodyRect.anchorMin = Vector2.zero;
                bodyRect.anchorMax = Vector2.one;
                bodyRect.offsetMin = bodyRect.offsetMax = Vector2.zero;
            }
            body = bodyRect.GetComponent<JellyGraphic>();
            if (body == null) body = bodyRect.gameObject.AddComponent<JellyGraphic>();
            if (bodyRect.GetComponent<CanvasRenderer>() == null)
                bodyRect.gameObject.AddComponent<CanvasRenderer>();
            body.raycastTarget = false;
        }

        public void Refresh()
        {
            EnsureVisuals();
            var rect = Rect.rect;
            if (body != null)
            {
                body.blob = blob;
                body.seed = 1.7f;
                body.time = 0f;
                body.squash = 0f;
                body.SetVerticesDirty();
            }
            if (glow != null)
            {
                glow.sprite = JellyRunArt.Glow;
                glow.color = new Color(.41f, .95f, .84f, .10f);
                glow.rectTransform.sizeDelta = new Vector2(rect.width * 1.12f, rect.height * 1.6f);
            }
            if (shadow != null)
            {
                shadow.sprite = JellyRunArt.Ellipse;
                shadow.color = new Color(0f, 0f, 0f, .28f);
                shadow.rectTransform.anchoredPosition = new Vector2(0f, rect.height * .5f);
                shadow.rectTransform.sizeDelta = new Vector2(rect.width * .92f, 18f);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunLedgeView : MonoBehaviour
    {
        public Image image;

        public RectTransform Rect => (RectTransform)transform;

        void OnEnable() => Refresh();
        void OnValidate() => Refresh();
        void OnRectTransformDimensionsChange() => Refresh();

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
            image.sprite = JellyRunArt.LedgeSprite(Mathf.RoundToInt(Rect.rect.width));
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }
    }
}

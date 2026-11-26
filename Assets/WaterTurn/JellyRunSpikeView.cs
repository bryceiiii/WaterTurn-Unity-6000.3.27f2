using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunSpikeView : MonoBehaviour
    {
        public bool up = true;
        public Image image;

        public RectTransform Rect => (RectTransform)transform;

        void OnEnable() => Refresh();
        void OnValidate() => Refresh();
        void OnRectTransformDimensionsChange() => Refresh();

        public void EnsureVisuals()
        {
            if (image == null) image = GetComponent<Image>();
            if (image == null) image = gameObject.AddComponent<Image>();
            if (image != null) image.raycastTarget = false;
        }

        public void Refresh()
        {
            EnsureVisuals();
            var rect = Rect.rect;
            image.sprite = JellyRunArt.SpikeSprite(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), up);
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }
    }
}

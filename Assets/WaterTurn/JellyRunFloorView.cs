using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunFloorView : MonoBehaviour
    {
        public bool ceiling;
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
            var rect = Rect.rect;
            image.sprite = JellyRunArt.FloorSprite(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), ceiling);
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }
    }
}

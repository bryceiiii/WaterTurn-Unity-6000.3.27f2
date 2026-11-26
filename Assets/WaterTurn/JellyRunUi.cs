using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    public static class JellyRunUi
    {
        static Font font;

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                font = Font.CreateDynamicFontFromOSFont(
                    new[] {"Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial"}, 28);
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform NewRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image NewImage(Transform parent, string name, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text NewText(Transform parent, string name, string content, int size, Color color,
            TextAnchor align, Vector2 position, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = box;
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static RectTransform FindRect(Transform parent, string name)
        {
            var child = parent.Find(name);
            return child as RectTransform;
        }

        public static T FindOrAdd<T>(Transform parent, string name) where T : Component
        {
            var child = parent.Find(name);
            if (child != null)
            {
                var existing = child.GetComponent<T>();
                if (existing != null) return existing;
            }
            var rect = NewRect(parent, name, Vector2.zero, new Vector2(10f, 10f));
            return rect.gameObject.AddComponent<T>();
        }

        /// <summary>World-space (canvas-local) rect of any UI element under the world root.</summary>
        public static JellyRunData.RectF GameRect(RectTransform world, RectTransform target)
        {
            if (world == null || target == null) return new JellyRunData.RectF(0, 0, 1, 1);
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 a = GamePoint(world, corners[0]);
            Vector2 b = GamePoint(world, corners[2]);
            return new JellyRunData.RectF(
                Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        public static Vector2 GamePoint(RectTransform world, Vector3 worldPosition)
        {
            if (world == null) return worldPosition;
            Vector3 local = world.InverseTransformPoint(worldPosition);
            return new Vector2(local.x, -local.y);
        }
    }
}

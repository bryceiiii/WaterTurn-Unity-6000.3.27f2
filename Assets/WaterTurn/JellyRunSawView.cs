using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunSawView : MonoBehaviour
    {
        public float radius = 34f;
        public float speed = 1.05f;
        public float phase;
        public Transform pathA, pathB;
        public Image blade, glow;

        public RectTransform Rect => (RectTransform)transform;

        void OnEnable() => Refresh();
        void OnValidate() => Refresh();

        public void EnsureVisuals()
        {
            if (pathA == null)
            {
                var a = JellyRunUi.FindRect(transform, "Path A");
                if (a == null) a = JellyRunUi.NewRect(transform, "Path A", Vector2.zero, new Vector2(8, 8));
                pathA = a;
            }
            if (pathB == null)
            {
                var b = JellyRunUi.FindRect(transform, "Path B");
                if (b == null) b = JellyRunUi.NewRect(transform, "Path B", Vector2.zero, new Vector2(8, 8));
                pathB = b;
            }
            if (glow == null)
            {
                glow = JellyRunUi.NewImage(transform, "Glow", Vector2.zero,
                    new Vector2(radius * 3.2f, radius * 3.2f), JellyRunArt.Glow,
                    new Color(1f, .35f, .35f, .18f));
            }
            if (blade == null)
            {
                blade = JellyRunUi.NewImage(transform, "Blade", Vector2.zero,
                    new Vector2(radius * 2f + 8f, radius * 2f + 8f), JellyRunArt.Saw, Color.white);
                blade.preserveAspect = true;
            }
        }

        public void Refresh()
        {
            EnsureVisuals();
            radius = Mathf.Max(4f, radius);
            blade.sprite = JellyRunArt.Saw;
            blade.color = Color.white;
            blade.rectTransform.sizeDelta = new Vector2(radius * 2f + 8f, radius * 2f + 8f);
            glow.sprite = JellyRunArt.Glow;
            glow.color = new Color(1f, .35f, .35f, .18f);
            glow.rectTransform.sizeDelta = new Vector2(radius * 3.2f, radius * 3.2f);
        }

        void OnDrawGizmos()
        {
            if (pathA == null || pathB == null) return;
            Gizmos.color = new Color(1f, .5f, .5f, .8f);
            Gizmos.DrawLine(pathA.position, pathB.position);
            Gizmos.DrawWireSphere(pathA.position, 6f);
            Gizmos.DrawWireSphere(pathB.position, 6f);
        }
    }
}

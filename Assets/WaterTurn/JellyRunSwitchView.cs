using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunSwitchView : MonoBehaviour
    {
        public float radius = 38f;
        public Image glow, crystal;
        public Text label;

        void OnEnable() => Refresh();
        void OnValidate() => Refresh();

        public void EnsureVisuals()
        {
            if (glow == null)
            {
                glow = JellyRunUi.NewImage(transform, "Glow", Vector2.zero,
                    new Vector2(radius * 4.2f, radius * 4.2f), JellyRunArt.Glow,
                    new Color(.51f, .92f, 1f, .26f));
            }
            if (crystal == null)
            {
                crystal = JellyRunUi.NewImage(transform, "Crystal", Vector2.zero,
                    new Vector2(radius * 1.1f, radius * 1.1f), JellyRunArt.Diamond,
                    new Color(.55f, .96f, .92f, .9f));
                crystal.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            if (transform.Find("Label") == null)
            {
                label = JellyRunUi.NewText(transform, "Label", "机关", 12,
                    new Color(.82f, 1f, .97f, .75f), TextAnchor.MiddleCenter,
                    new Vector2(0f, -radius - 12f), new Vector2(120f, 20f));
                label.fontStyle = FontStyle.Bold;
            }
            else if (label == null)
            {
                label = transform.Find("Label").GetComponent<Text>();
            }
        }

        public void Refresh()
        {
            EnsureVisuals();
            glow.sprite = JellyRunArt.Glow;
            glow.color = new Color(.51f, .92f, 1f, .26f);
            glow.rectTransform.sizeDelta = new Vector2(radius * 4.2f, radius * 4.2f);
            crystal.sprite = JellyRunArt.Diamond;
            crystal.color = new Color(.55f, .96f, .92f, .9f);
            crystal.rectTransform.sizeDelta = new Vector2(radius * 1.1f, radius * 1.1f);
            if (label != null) label.font = JellyRunUi.Font;
        }
    }
}

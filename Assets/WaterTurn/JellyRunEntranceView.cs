using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    [ExecuteAlways]
    public sealed class JellyRunEntranceView : MonoBehaviour
    {
        public int chapter;
        public RectTransform platform;
        public JellyRunLedgeView platformView;
        public Image beam, glow, ring, pearl;
        public Image[] pips = new Image[4];
        public Text title, range;

        public RectTransform Rect => (RectTransform)transform;

        void OnEnable() => Refresh();
        void OnValidate() { chapter = Mathf.Clamp(chapter, 0, 4); Refresh(); }

        public void EnsureVisuals()
        {
            if (platform == null)
            {
                var p = JellyRunUi.FindRect(transform, "Platform");
                if (p == null) p = JellyRunUi.NewRect(transform, "Platform", new Vector2(0f, -8f), new Vector2(120f, 16f));
                platform = p;
            }
            platform.anchorMin = platform.anchorMax = new Vector2(.5f, .5f);
            platform.pivot = new Vector2(.5f, .5f);
            platformView = platform.GetComponent<JellyRunLedgeView>();
            if (platformView == null) platformView = platform.gameObject.AddComponent<JellyRunLedgeView>();
            platformView.EnsureVisuals();

            if (beam == null)
            {
                beam = JellyRunUi.NewImage(transform, "Beam", new Vector2(0f, -142f), new Vector2(72f, 260f),
                    JellyRunArt.Beam, new Color(1f, 1f, 1f, .38f));
            }
            if (glow == null)
            {
                glow = JellyRunUi.NewImage(transform, "Glow", new Vector2(0f, -72f), new Vector2(150f, 150f),
                    JellyRunArt.Glow, new Color(1f, 1f, 1f, .30f));
            }
            if (ring == null)
            {
                ring = JellyRunUi.NewImage(transform, "Ring", new Vector2(0f, -72f), new Vector2(64f, 64f),
                    JellyRunArt.Ring, Color.white);
            }
            if (pearl == null)
            {
                pearl = JellyRunUi.NewImage(transform, "Pearl", new Vector2(0f, -72f), new Vector2(32f, 32f),
                    JellyRunArt.Pearl, Color.white);
                pearl.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null)
                {
                    pips[i] = JellyRunUi.NewImage(transform, "Pip " + i,
                        new Vector2(-30f + i * 20f, -110f), new Vector2(9f, 9f), JellyRunArt.Circle,
                        new Color(1f, 1f, 1f, .22f));
                }
            }
            if (title == null)
            {
                title = JellyRunUi.NewText(transform, "Title", "", 14, Color.white, TextAnchor.MiddleCenter,
                    new Vector2(0f, -156f), new Vector2(280f, 22f));
                title.fontStyle = FontStyle.Bold;
            }
            if (range == null)
            {
                range = JellyRunUi.NewText(transform, "Range", "", 11, new Color(.75f, .86f, .86f, .8f),
                    TextAnchor.MiddleCenter, new Vector2(0f, -134f), new Vector2(280f, 20f));
            }
        }

        public void Refresh()
        {
            EnsureVisuals();
            chapter = Mathf.Clamp(chapter, 0, 4);
            var data = JellyRunData.Entrances[chapter];
            var accent = new Color(data.accent.r / 255f, data.accent.g / 255f, data.accent.b / 255f, 1f);
            title.font = JellyRunUi.Font;
            title.text = data.Title;
            title.color = new Color(accent.r, accent.g, accent.b, .95f);
            range.font = JellyRunUi.Font;
            range.text = "关卡 " + data.LevelRange;
            beam.sprite = JellyRunArt.Beam;
            beam.color = new Color(accent.r, accent.g, accent.b, .38f);
            glow.sprite = JellyRunArt.Glow;
            glow.color = new Color(accent.r, accent.g, accent.b, .30f);
            ring.sprite = JellyRunArt.Ring;
            ring.color = new Color(accent.r, accent.g, accent.b, .85f);
            pearl.sprite = JellyRunArt.Pearl;
            pearl.color = Color.white;
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].sprite = JellyRunArt.Circle;
                pips[i].color = new Color(1f, 1f, 1f, .22f);
            }
            platformView.Refresh();
        }
    }
}

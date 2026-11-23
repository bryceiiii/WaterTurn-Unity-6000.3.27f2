using UnityEngine;

namespace WaterTurn
{
    /// <summary>
    /// A hand-placed level entrance for the hub. Move this object in the Scene
    /// view to move the corresponding seaweed shrine; the position is in the
    /// same pixel space as the generated cave map (1260 x 756, centre = 0,0).
    /// </summary>
    public sealed class HubPortalAnchor : MonoBehaviour
    {
        [Tooltip("0-based index into Campaign.Levels. 20 is the hidden level.")]
        public int level;

        [Tooltip("Short label shown on the shrine; leave empty for 01..20 / 隐藏关.")]
        public string title;

        [Tooltip("Level name shown in the interaction prompt; leave empty to read Campaign.Levels.")]
        public string subtitle;

        public Color activeColor = new Color(0.35f, 0.85f, 0.62f, 1f);
        public Color haloColor = new Color(0.35f, 0.85f, 0.62f, 1f);
        public bool compact = true;

        public string ResolvedTitle =>
            string.IsNullOrEmpty(title)
                ? (level >= Campaign.VisibleLevels ? "隐藏关" : (level + 1).ToString("00"))
                : title;

        public string ResolvedSubtitle
        {
            get
            {
                if (!string.IsNullOrEmpty(subtitle)) return subtitle;
                int index = Mathf.Clamp(level, 0, Campaign.Levels.Length - 1);
                return Campaign.Levels[index].Name;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var p = transform.position;
            Gizmos.color = new Color(activeColor.r, activeColor.g, activeColor.b, 0.85f);
            Gizmos.DrawWireSphere(p, 12f);
            Gizmos.DrawLine(p, p + Vector3.up * 22f);
            UnityEditor.Handles.color = activeColor;
            UnityEditor.Handles.Label(p + Vector3.up * 28f, ResolvedTitle + "  " + ResolvedSubtitle);
        }
#endif
    }
}

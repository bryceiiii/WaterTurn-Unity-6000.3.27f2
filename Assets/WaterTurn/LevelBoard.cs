using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// Binds the runtime puzzle state to the prefab instances that already sit in
    /// the level scene. <see cref="WaterGame"/> moves these; it never creates them.
    /// The arrays are ordered exactly like <see cref="Position.Water"/>.
    /// </summary>
    public sealed class LevelBoard : MonoBehaviour
    {
        [Tooltip("The rotating room. Its children are the prefab instances.")]
        public RectTransform room;

        [Tooltip("Pixels per grid cell used to convert a Position index into a local point.")]
        public float cell = 100f;

        public RectTransform[] water = new RectTransform[0];
        public RectTransform[] foam = new RectTransform[0];

        public RectTransform player;
        public RectTransform crate;
        public Image gate;
        public Image sensor;
        public Image pearl;
    }
}

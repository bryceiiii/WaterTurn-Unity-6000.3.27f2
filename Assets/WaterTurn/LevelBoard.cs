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

        [Tooltip("传送门画面，按盘面从左到右、从上到下排列。")]
        public Image[] portals = new Image[0];

        public RectTransform player;
        [Tooltip("旧关卡只有一个箱子时用这个字段；新关卡请填 crates。")]
        public RectTransform crate;
        [Tooltip("箱子，顺序和 Position.Boxes 一致（升序）。")]
        public RectTransform[] crates = new RectTransform[0];
        public Image gate;
        public Image sensor;
        public Image pearl;
    }
}

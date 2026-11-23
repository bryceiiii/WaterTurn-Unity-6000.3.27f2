using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// References into the GameCanvas prefab instance. The scene owns the layout;
    /// <see cref="WaterGame"/> only reads these and updates their content.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        [Header("Containers")]
        public RectTransform boardArea;

        [Header("Text")]
        public Text headline;
        public Text instructions;
        public Text mechanism;
        public Text status;
        public Text readout;

        [Header("Buttons")]
        public Button home;
        public Button next;
        public Button undo;
        public Button restart;
        public Button hint;
        public Button up;
        public Button down;
        public Button left;
        public Button right;
        public Button turn;

        [Header("Level picker (20)")]
        public Button[] levelButtons = new Button[0];

        /// <summary>
        /// The rounded panel sprite and the CJK-capable font are created at runtime,
        /// so the prefab stores layout only and gets its skin here.
        /// </summary>
        public void ApplySkin(Font font, Sprite rounded)
        {
            if (font != null)
                foreach (var text in GetComponentsInChildren<Text>(true))
                    text.font = font;

            if (rounded == null) return;
            foreach (var image in GetComponentsInChildren<Image>(true))
                if (image.type == Image.Type.Sliced)
                    image.sprite = rounded;
        }
    }
}

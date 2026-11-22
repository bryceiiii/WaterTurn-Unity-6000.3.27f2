using UnityEngine;

namespace WaterTurn
{
    /// <summary>Which way the fish art is drawn to face.</summary>
    public enum FishFacing
    {
        Right = 0,
        Left = 1,
        Up = 2,
        Down = 3,
    }

    /// <summary>
    /// The hand-drawn pixel fish, imported from the Aseprite project under
    /// Assets/Resources/Art/Fish.  Swim frames are 48x48, four per direction at
    /// 110 ms each; the jump sets are imported too but not played yet.
    /// </summary>
    public static class FishArt
    {
        public const int FrameCount = 4;
        public const float FrameSeconds = .110f;
        public const float FramePixels = 48f;

        static readonly string[] Names = {"right", "left", "up", "down"};
        static readonly Sprite[][] swim = new Sprite[4][];
        static readonly Sprite[][] jump = new Sprite[4][];
        static bool swimReady;

        /// <summary>True once every swim frame has been found in Resources.</summary>
        public static bool Ready => swimReady;

        public static Sprite[] Swim(FishFacing facing)
        {
            if (!swimReady) swimReady = LoadSet(swim, "swim");
            return swim[(int)facing];
        }

        public static Sprite[] Jump(FishFacing facing)
        {
            var set = jump[(int)facing];
            if (set == null || set.Length == 0 || set[0] == null) LoadSet(jump, "jump");
            return jump[(int)facing];
        }

        /// <summary>Current animation frame for a running clock, in seconds.</summary>
        public static int FrameAt(float seconds) =>
            Mathf.FloorToInt(seconds / FrameSeconds) % FrameCount;

        public static FishFacing FacingOf(Vector2 velocity, FishFacing fallback)
        {
            if (velocity.sqrMagnitude < 1f) return fallback;
            if (Mathf.Abs(velocity.x) >= Mathf.Abs(velocity.y))
                return velocity.x >= 0 ? FishFacing.Right : FishFacing.Left;
            return velocity.y >= 0 ? FishFacing.Up : FishFacing.Down;
        }

        static bool LoadSet(Sprite[][] sets, string kind)
        {
            bool complete = true;
            for (int f = 0; f < sets.Length; f++)
            {
                var frames = new Sprite[FrameCount];
                for (int i = 0; i < FrameCount; i++)
                {
                    frames[i] = Resources.Load<Sprite>("Art/Fish/" + kind + "_" + Names[f] + "_" + i);
                    if (frames[i] == null) complete = false;
                }
                sets[f] = frames;
            }
            if (!complete)
                Debug.LogWarning("WaterTurn: 小鱼素材缺失（" + kind + "）· 检查 Assets/Resources/Art/Fish 是否已导入。");
            return complete;
        }
    }
}

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

        /// <summary>The single death set: hit, twitch, belly-up, drift, sink, vanish.</summary>
        static Sprite[] death;
        static bool deathReady;

        /// <summary>Per-frame durations of the death set, straight from the Aseprite tags.</summary>
        public static readonly float[] DeathSeconds = {.09f, .12f, .15f, .18f, .22f, .32f};

        public const int DeathFrameCount = 6;

        /// <summary>Whole death animation, about 1.08 s.</summary>
        public static float DeathTotalSeconds
        {
            get
            {
                float total = 0f;
                foreach (float seconds in DeathSeconds) total += seconds;
                return total;
            }
        }

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

        /// <summary>
        /// The death frames. They are drawn facing right; mirror the Image for a
        /// left-facing death (the art pack has one arrow for this set by design).
        /// </summary>
        public static Sprite[] Death()
        {
            if (!deathReady)
            {
                var frames = new Sprite[DeathFrameCount];
                bool complete = true;
                for (int i = 0; i < DeathFrameCount; i++)
                {
                    frames[i] = Resources.Load<Sprite>("Art/Fish/death_" + i);
                    if (frames[i] == null) complete = false;
                }
                death = frames;
                deathReady = complete;
                if (!complete)
                    Debug.LogWarning("WaterTurn: 小鱼死亡动画缺失（Art/Fish/death_0..5）。");
            }
            return death;
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

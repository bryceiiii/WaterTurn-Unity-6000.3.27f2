using UnityEditor;

namespace WaterTurn.EditorTools
{
    /// <summary>
    /// 第三章（09–12「隔栅」）的场景重建与盘面校验。这一章换了三种材料：
    /// 薄膜 M（只过鱼）、逆闸 d（常开，水压感应格才关）、泉眼 o（永不滑动的水格）。
    /// 它们全是运行时白模，所以场景里只有地板、墙、水、鱼、出口和感应格。
    /// </summary>
    public static class Chapter03SceneBuilder
    {
        static readonly int[] Chapter = {8, 9, 10, 11};

        [MenuItem("WaterTurn/第三章/重建 09-12 场景")]
        public static void Build() => BoardSceneBuilder.Build(Chapter);

        [MenuItem("WaterTurn/第三章/校验 09-12 盘面")]
        public static void Verify() => BoardSceneBuilder.Verify(Chapter);
    }
}

using UnityEditor;

namespace WaterTurn.EditorTools
{
    /// <summary>
    /// 第二章（05–08「塞与阀」）的场景重建与盘面校验，实际工作交给
    /// <see cref="BoardSceneBuilder"/>，这样第三章不用再抄一遍。
    /// </summary>
    public static class Chapter02SceneBuilder
    {
        static readonly int[] Chapter = {4, 5, 6, 7};

        [MenuItem("WaterTurn/第二章/重建 05-08 场景")]
        public static void Build() => BoardSceneBuilder.Build(Chapter);

        [MenuItem("WaterTurn/第二章/校验 05-08 盘面")]
        public static void Verify() => BoardSceneBuilder.Verify(Chapter);
    }
}

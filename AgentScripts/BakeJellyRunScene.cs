namespace WaterTurn.Agent
{
    /// <summary>Entry point used by the unityMCP "script run" pipeline.</summary>
    public static class BakeJellyRunScene
    {
        public static string Run()
        {
            return WaterTurn.EditorTools.JellyRunSceneBaker.BakeSilent();
        }

        public static string Validate()
        {
            return WaterTurn.EditorTools.JellyRunSceneBaker.ValidateScene();
        }
    }
}

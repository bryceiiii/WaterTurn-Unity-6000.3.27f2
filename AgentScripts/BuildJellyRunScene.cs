namespace WaterTurn.Agent
{
    /// <summary>Entry point used by the unityMCP "script run" pipeline.</summary>
    public static class BuildJellyRunScene
    {
        public static void Run()
        {
            WaterTurn.EditorTools.JellyRunSceneTools.BuildAndSave();
        }

        public static void Validate()
        {
            WaterTurn.JellyRunData.Validate(out string report);
            UnityEngine.Debug.Log("[JellyRun] " + report);
        }
    }
}

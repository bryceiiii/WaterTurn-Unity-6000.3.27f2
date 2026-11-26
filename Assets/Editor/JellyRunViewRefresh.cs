using UnityEditor;
using UnityEngine;

namespace WaterTurn.EditorTools
{
    /// <summary>
    /// The view art is generated at edit time (runtime-created sprites are not
    /// persisted in the scene).  When Play stops, Unity destroys those sprites,
    /// so refresh the authored views once the editor is back in edit mode.
    /// </summary>
    [InitializeOnLoad]
    public static class JellyRunViewRefresh
    {
        static JellyRunViewRefresh()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode ||
                    state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += RefreshAll;
            };
        }

        [MenuItem("WaterTurn/JellyRun/刷新场景美术", false, 22)]
        public static void RefreshAll()
        {
            int count = 0;
            foreach (var view in Object.FindObjectsByType<JellyRunFloorView>(FindObjectsSortMode.None))
            { view.Refresh(); count++; }
            foreach (var view in Object.FindObjectsByType<JellyRunLedgeView>(FindObjectsSortMode.None))
            { view.Refresh(); count++; }
            foreach (var view in Object.FindObjectsByType<JellyRunSpikeView>(FindObjectsSortMode.None))
            { view.Refresh(); count++; }
            foreach (var view in Object.FindObjectsByType<JellyRunJellyView>(FindObjectsSortMode.None))
            { view.Refresh(); count++; }
            foreach (var view in Object.FindObjectsByType<JellyRunEntranceView>(FindObjectsSortMode.None))
            { view.Refresh(); count++; }
            foreach (var view in Object.FindObjectsByType<JellyRunSawView>(FindObjectsSortMode.None))
            { view.Refresh(); count++; }
            foreach (var view in Object.FindObjectsByType<JellyRunDoorView>(FindObjectsSortMode.None))
            { view.Refresh(); count++; }
            foreach (var view in Object.FindObjectsByType<JellyRunSwitchView>(FindObjectsSortMode.None))
            { view.Refresh(); count++; }
            foreach (var view in Object.FindObjectsByType<JellyRunPortalView>(FindObjectsSortMode.None))
            { view.Refresh(); count++; }
            foreach (var view in Object.FindObjectsByType<JellyRunValveView>(FindObjectsSortMode.None))
            { view.Refresh(); count++; }
            SceneView.RepaintAll();
            if (count > 0) Debug.Log("[JellyRun] 已刷新 " + count + " 个场景视图。");
        }
    }
}

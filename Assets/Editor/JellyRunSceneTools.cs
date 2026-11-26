using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterTurn;

namespace WaterTurn.EditorTools
{
    /// <summary>
    /// Scene-level helpers for the authored JellyRun scene.  The heavy lifting
    /// now lives in <see cref="JellyRunSceneBaker"/>: the scene contains real
    /// prefab instances, so "rebuild" means re-baking from the HTML tables.
    /// </summary>
    public static class JellyRunSceneTools
    {
        public const string ScenePath = "Assets/Scenes/JellyRun.unity";
        const string Menu = "WaterTurn/JellyRun/";

        [MenuItem(Menu + "重建场景（覆盖手动修改）", false, 10)]
        public static void BuildAndSave()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("果冻潮池", "请先退出 Play 模式。", "好");
                return;
            }
            if (!EditorUtility.DisplayDialog("果冻潮池",
                    "会用 JellyRunData 重新烘焙预制体和场景，覆盖场景里的手动修改。\n确定继续？",
                    "重建", "取消"))
                return;

            if (!File.Exists(ScenePath)) CreateBareScene();
            JellyRunSceneBaker.BakeSilent();
            AddToBuildSettings();
        }

        static void CreateBareScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.027f, .078f, .114f, 1f);
            camera.orthographic = true;
            camera.cullingMask = 0;
            var root = new GameObject("JellyRun");
            root.AddComponent<JellyRunGame>();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        [MenuItem(Menu + "校验入口布局与场景", false, 11)]
        public static void ValidateLayout()
        {
            bool ok = JellyRunData.Validate(out string dataReport);
            string sceneReport = JellyRunSceneBaker.ValidateScene();
            string report = (ok ? "入口布局 OK\n" : "入口布局失败\n") + dataReport + "\n\n" + sceneReport;
            if (ok) Debug.Log("[JellyRun] " + report);
            else Debug.LogError("[JellyRun] " + report);
            EditorUtility.DisplayDialog("果冻潮池 · 校验", report, "好");
        }

        [MenuItem(Menu + "清空入口进度", false, 12)]
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(GameSession.JellyRunClearedKey);
            PlayerPrefs.Save();
            Debug.Log("WaterTurn: 已清空果冻潮池 20 关完成度。");
        }

        [MenuItem(Menu + "打开场景", false, 13)]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else BuildAndSave();
        }

        public static void AddToBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            int hub = scenes.FindIndex(s => s.path.EndsWith("/WaterTurnHub.unity"));
            var entry = new EditorBuildSettingsScene(ScenePath, true);
            if (hub >= 0) scenes.Insert(hub + 1, entry);
            else scenes.Insert(0, entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

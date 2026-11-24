using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterTurn;

namespace WaterTurn.EditorTools
{
    /// <summary>
    /// Builds the standalone "果冻潮池" scene and registers it in Build Settings.
    /// The scene itself is deliberately tiny: the camera plus one JellyRunGame,
    /// because the whole cave, its five chapter entrances and the HUD are
    /// generated at runtime exactly like the HTML canvas prototype.
    /// </summary>
    public static class JellyRunSceneTools
    {
        public const string ScenePath = "Assets/Scenes/JellyRun.unity";
        const string Menu = "WaterTurn/JellyRun/";

        [MenuItem(Menu + "重建场景", false, 10)]
        public static void BuildAndSave()
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

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("WaterTurn: 已重建 " + ScenePath + " 并写入 Build Settings。");
        }

        [MenuItem(Menu + "校验入口布局", false, 11)]
        public static void ValidateLayout()
        {
            bool ok = JellyRunData.Validate(out string report);
            if (ok) Debug.Log("[JellyRun] " + report);
            else Debug.LogError("[JellyRun] 入口布局校验失败：\n" + report);
            EditorUtility.DisplayDialog("果冻潮池 · 入口校验", report, "好");
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
            if (System.IO.File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else BuildAndSave();
        }

        static void AddToBuildSettings()
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

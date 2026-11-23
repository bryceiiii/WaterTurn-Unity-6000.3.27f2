using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterTurn;

namespace AgentBuild
{
    public static class SceneValidator
    {
        public static string Validate()
        {
            var lines = new List<string>();
            int failures = 0;
            for (int i = 0; i < Campaign.Levels.Length; i++)
            {
                var level = Campaign.Levels[i];
                var expected = Puzzle.Begin(level);
                var path = "Assets/Scenes/Level_" + (i + 1).ToString("00") + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    lines.Add(path + " MISSING");
                    failures++;
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var def = UnityEngine.Object.FindObjectsByType<LevelDefinition>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).FirstOrDefault();
                var board = UnityEngine.Object.FindObjectsByType<LevelBoard>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).FirstOrDefault();
                var manager = UnityEngine.Object.FindObjectsByType<LevelManager>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).FirstOrDefault();
                var game = UnityEngine.Object.FindObjectsByType<WaterGame>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).FirstOrDefault();
                var hud = UnityEngine.Object.FindObjectsByType<GameHud>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).FirstOrDefault();
                var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).FirstOrDefault();

                int prefabInstances = 0;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.GetPrefabInstanceStatus(t.gameObject) == PrefabInstanceStatus.Connected)
                        prefabInstances++;

                var problems = new List<string>();
                if (def == null) problems.Add("no LevelDefinition");
                else
                {
                    if (def.levelIndex != i) problems.Add("levelIndex " + def.levelIndex);
                    if (def.levelName != level.Name) problems.Add("name");
                    if (def.rows == null || string.Concat(def.rows) != level.Tiles) problems.Add("rows mismatch");
                }
                if (board == null) problems.Add("no LevelBoard");
                else
                {
                    if (board.water.Length != expected.Water.Length)
                        problems.Add("water " + board.water.Length + "/" + expected.Water.Length);
                    if (board.foam.Length != expected.Water.Length)
                        problems.Add("foam " + board.foam.Length + "/" + expected.Water.Length);
                    if (board.player == null) problems.Add("no player");
                    if (board.crate != null && expected.Box < 0) problems.Add("unexpected crate");
                    if (Mathf.Abs(board.cell - Mathf.Min(100f,
                            510f / Mathf.Sqrt(level.Width * level.Width + level.Height * level.Height))) > .01f)
                        problems.Add("cell " + board.cell);
                }
                if (manager == null) problems.Add("no LevelManager");
                else
                {
                    if (manager.levelIndex != i) problems.Add("manager index");
                    if (manager.game == null) problems.Add("manager game null");
                }
                if (game == null) problems.Add("no WaterGame");
                if (hud == null) problems.Add("no GameHud");
                else if (hud.levelButtons.Length != Campaign.VisibleLevels) problems.Add("level buttons");
                if (camera == null) problems.Add("no camera");

                int tiles = board != null && board.room != null ? board.room.childCount : 0;
                if (problems.Count > 0) failures++;
                lines.Add((problems.Count == 0 ? "OK   " : "FAIL ") + "Level_" + (i + 1).ToString("00") +
                          "  tiles=" + tiles + " water=" + (board != null ? board.water.Length : -1) +
                          " prefabs=" + prefabInstances +
                          (problems.Count > 0 ? "  -> " + string.Join(", ", problems) : ""));
            }

            EditorSceneManager.OpenScene("Assets/Scenes/WaterTurnHub.unity", OpenSceneMode.Single);
            lines.Add("failures: " + failures);
            return string.Join("\n", lines);
        }
    }
}

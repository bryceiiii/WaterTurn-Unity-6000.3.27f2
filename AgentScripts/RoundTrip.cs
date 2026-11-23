using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterTurn;
using WaterTurn.EditorTools;

namespace AgentBuild
{
    public static class RoundTrip
    {
        public static string Run()
        {
            var lines = new List<string>();
            int bad = 0;
            for (int i = 0; i < Campaign.Levels.Length; i++)
            {
                var path = "Assets/Scenes/Level_" + (i + 1).ToString("00") + ".unity";
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var def = UnityEngine.Object.FindFirstObjectByType<LevelDefinition>();
                var board = UnityEngine.Object.FindFirstObjectByType<LevelBoard>();
                var rows = LevelSceneTools.ReadRows(board, def, out string error);
                var expected = Enumerable.Range(0, Campaign.Levels[i].Height)
                    .Select(y => Campaign.Levels[i].Tiles.Substring(y * Campaign.Levels[i].Width,
                        Campaign.Levels[i].Width)).ToArray();
                bool ok = error == null && rows != null && rows.SequenceEqual(expected);
                if (!ok) bad++;
                lines.Add((ok ? "OK   " : "FAIL ") + scene.name +
                          (ok ? "" : "  " + (error ?? string.Join("/", rows ?? new string[0]))));
            }

            EditorSceneManager.OpenScene("Assets/Scenes/WaterTurnHub.unity", OpenSceneMode.Single);
            lines.Add("round-trip failures: " + bad);
            return string.Join("\n", lines);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterTurn;

namespace WaterTurn.EditorTools
{
    /// <summary>
    /// The scene is the visual projection of LevelDefinition.rows. After moving
    /// tiles around by hand, run this to read the board back into the ASCII data
    /// so the puzzle logic follows the layout you just edited.
    /// </summary>
    public static class LevelSceneTools
    {
        const string Menu = "WaterTurn/";

        [MenuItem(Menu + "场景布局 → 关卡数据 (Scene to Rows)", false, 1)]
        static void SyncCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();
            var definition = UnityEngine.Object.FindFirstObjectByType<LevelDefinition>();
            var board = UnityEngine.Object.FindFirstObjectByType<LevelBoard>();
            if (definition == null || board == null)
            {
                EditorUtility.DisplayDialog("WaterTurn", "当前场景没有 LevelDefinition / LevelBoard。", "好");
                return;
            }

            var rows = ReadRows(board, definition, out string error);
            if (error != null)
            {
                EditorUtility.DisplayDialog("WaterTurn", error, "好");
                return;
            }

            definition.rows = rows;
            EditorUtility.SetDirty(definition);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("WaterTurn: 已把 " + scene.name + " 的棋盘同步到关卡数据。");
        }

        /// <summary>Rebuild the ASCII grid from the prefab instances in the scene.</summary>
        public static string[] ReadRows(LevelBoard board, LevelDefinition definition, out string error)
        {
            error = null;
            if (definition.rows == null || definition.rows.Length == 0)
            {
                error = "关卡数据为空，无法确定棋盘尺寸。";
                return null;
            }

            int width = definition.rows[0].Length;
            int height = definition.rows.Length;
            if (board.room == null || board.cell <= 0f)
            {
                error = "LevelBoard 缺少 room 或 cell。";
                return null;
            }

            var grid = new char[width * height];
            for (int i = 0; i < grid.Length; i++) grid[i] = '.';

            int CellOf(Transform t)
            {
                var rt = (RectTransform) t;
                int x = Mathf.RoundToInt(rt.anchoredPosition.x / board.cell + (width - 1) * .5f);
                int y = Mathf.RoundToInt((height - 1) * .5f - rt.anchoredPosition.y / board.cell);
                return x < 0 || x >= width || y < 0 || y >= height ? -1 : x + y * width;
            }

            foreach (Transform child in board.room)
            {
                char symbol = SymbolOf(child.name);
                if (symbol == '\0') continue;
                int cell = CellOf(child);
                if (cell < 0)
                {
                    error = "物体 " + child.name + " 落在棋盘网格之外。";
                    return null;
                }
                grid[cell] = symbol;
            }

            foreach (var water in board.water)
            {
                if (water == null) continue;
                int cell = CellOf(water);
                if (cell < 0)
                {
                    error = "水格 " + water.name + " 落在棋盘网格之外。";
                    return null;
                }
                grid[cell] = '~';
            }

            if (board.player != null)
            {
                int cell = CellOf(board.player);
                if (cell < 0)
                {
                    error = "玩家不在棋盘网格内。";
                    return null;
                }
                grid[cell] = 'P';
            }

            if (board.crate != null)
            {
                int cell = CellOf(board.crate);
                if (cell < 0)
                {
                    error = "木箱不在棋盘网格内。";
                    return null;
                }
                grid[cell] = 'B';
            }

            var rows = new string[height];
            for (int y = 0; y < height; y++) rows[y] = new string(grid, y * width, width);

            try
            {
                var level = new Level(definition.levelName, definition.hint, rows);
                if (definition.levelIndex >= 0 && definition.levelIndex < Campaign.Levels.Length &&
                    level.Width != Campaign.Levels[definition.levelIndex].Width)
                    error = "棋盘宽度和 Campaign 中的关卡不一致。";
            }
            catch (Exception exception)
            {
                error = "同步后的棋盘无效：" + exception.Message;
            }

            return error == null ? rows : null;
        }

        static char SymbolOf(string name)
        {
            if (name.StartsWith("Wall")) return '#';
            if (name.StartsWith("Floor")) return '.';
            if (name.StartsWith("Gate")) return 'D';
            if (name.StartsWith("Sensor")) return 'S';
            if (name.StartsWith("Pearl")) return 'K';
            if (name.StartsWith("Exit")) return 'E';
            if (name.StartsWith("Grate")) return 'G';
            if (name.StartsWith("Crate")) return 'B';
            return '\0';
        }
    }
}

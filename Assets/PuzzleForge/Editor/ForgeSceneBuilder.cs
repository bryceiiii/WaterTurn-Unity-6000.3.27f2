#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WaterTurn.Forge.EditorTools
{
    /// <summary>
    /// 把 PuzzleForge 生成的 ASCII 盘面落成"正式关卡场景"：与 Level_01..Level_21 完全相同的
    /// Hierarchy —— GameRoot(WaterGame + LevelManager + LevelDefinition) / GameCanvas(UI_GameHud
    /// 预制体) → BoardCenter → Room(LevelBoard) / EventSystem / Camera_Level，Room 里是
    /// Tile_Wall、Tile_Floor、Effect_Water、Effect_Foam、Prop_Exit、Player_Fish 的真实预制体实例。
    ///
    /// Play 模式下没法建场景，所以运行时只写一个请求文件（Assets/PuzzleForge/.forge-scene-request.json，
    /// 点号开头 Unity 不会导入），退出播放后本类自动接单、生成场景并 ping 出来。
    /// </summary>
    [InitializeOnLoad]
    public static class ForgeSceneBuilder
    {
        public const string RequestFile = "Assets/PuzzleForge/.forge-scene-request.json";

        const string Board = "Assets/Prefabs/Board/";
        const string Level = "Assets/Prefabs/Level/";
        const string Ui = "Assets/Prefabs/UI/";

        static double nextTick;

        static ForgeSceneBuilder()
        {
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + .5;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists(RequestFile)) return;

            string json;
            try { json = File.ReadAllText(RequestFile, Encoding.UTF8); }
            catch (Exception e)
            {
                Debug.LogWarning("PuzzleForge: 读不到场景请求 —— " + e.Message);
                return;
            }

            // 先删请求，失败也不会每半秒重试一次。
            File.Delete(RequestFile);
            var pack = JsonUtility.FromJson<ForgePack>(json);
            if (pack == null || pack.levels == null || pack.levels.Count == 0)
            {
                Debug.LogWarning("PuzzleForge: 场景请求里没有关卡。");
                return;
            }

            try
            {
                var paths = Build(pack);
                Debug.Log("PuzzleForge: 已生成 " + paths.Count + " 个正式关卡场景 → " + Path.GetDirectoryName(paths[0]));
                var first = AssetDatabase.LoadAssetAtPath<SceneAsset>(paths[0]);
                if (first != null) EditorGUIUtility.PingObject(first);
            }
            catch (Exception e)
            {
                Debug.LogError("PuzzleForge: 生成关卡场景失败 —— " + e);
            }
        }

        // ------------------------------------------------------------------ 菜单

        [MenuItem("Tools/PuzzleForge/用最近工程生成正式关卡场景")]
        static void BuildFromLastPack()
        {
            if (!ForgeIO.TryLoadLast(out var pack, out string file))
            {
                EditorUtility.DisplayDialog("PuzzleForge", "还没有保存过工程。先在 PuzzleForge 场景里生成并保存一次。", "好");
                return;
            }
            var paths = Build(pack);
            EditorUtility.DisplayDialog("PuzzleForge",
                "已生成 " + paths.Count + " 个场景：" + Path.GetDirectoryName(paths[0]), "好");
        }

        [MenuItem("Tools/PuzzleForge/选择工程 JSON 生成正式关卡场景")]
        static void BuildFromPickedPack()
        {
            string file = EditorUtility.OpenFilePanel("选择 PuzzleForge 工程 JSON", "Assets/PuzzleForge/Exports", "json");
            if (string.IsNullOrEmpty(file)) return;
            if (!ForgeIO.TryLoad(file, out var pack))
            {
                EditorUtility.DisplayDialog("PuzzleForge", "这个 JSON 读不出来。", "好");
                return;
            }
            var paths = Build(pack);
            EditorUtility.DisplayDialog("PuzzleForge",
                "已生成 " + paths.Count + " 个场景：" + Path.GetDirectoryName(paths[0]), "好");
        }

        // ------------------------------------------------------------------ 生成

        public static List<string> Build(ForgePack pack)
        {
            var tiles = new Tiles();
            tiles.Load();

            string baseName = SafeName(pack.packName);
            // Application.dataPath 在 Windows 上带反斜杠，AssetDatabase 只吃正斜杠。
            string folder = (ForgeIO.Root + "/" + baseName + "/Scenes").Replace('\\', '/');
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            var paths = new List<string>();
            for (int i = 0; i < pack.levels.Count; i++)
            {
                string path = folder + "/" + baseName + "_" + (i + 1).ToString("00") + ".unity";
                BuildOne(pack, pack.levels[i], i, path, tiles);
                paths.Add(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return paths;
        }

        sealed class Tiles
        {
            public GameObject wall, floor, water, foam, exit, fish, crate, pearl, gate, grate;
            public GameObject hud, eventSystem, camera;

            public void Load()
            {
                wall = Prefab(Board + "Tile_Wall.prefab");
                floor = Prefab(Board + "Tile_Floor.prefab");
                water = Prefab(Board + "Effect_Water.prefab");
                foam = Prefab(Board + "Effect_Foam.prefab");
                exit = Prefab(Board + "Prop_Exit.prefab");
                fish = Prefab(Board + "Player_Fish.prefab");
                crate = Prefab(Board + "Obstacle_Crate.prefab");
                pearl = Prefab(Board + "Prop_Pearl.prefab");
                gate = Prefab(Board + "Mechanism_Gate.prefab");
                grate = Prefab(Board + "Obstacle_Grate.prefab");
                hud = Prefab(Ui + "UI_GameHud.prefab");
                eventSystem = Prefab(Level + "EventSystem.prefab");
                camera = Prefab(Level + "Camera_Level.prefab");

                var missing = new List<string>();
                if (wall == null) missing.Add("Tile_Wall");
                if (floor == null) missing.Add("Tile_Floor");
                if (water == null) missing.Add("Effect_Water");
                if (exit == null) missing.Add("Prop_Exit");
                if (fish == null) missing.Add("Player_Fish");
                if (gate == null) missing.Add("Mechanism_Gate");
                if (grate == null) missing.Add("Obstacle_Grate");
                if (hud == null) missing.Add("UI_GameHud");
                if (eventSystem == null) missing.Add("EventSystem");
                if (camera == null) missing.Add("Camera_Level");
                if (missing.Count > 0)
                    throw new FileNotFoundException("关卡预制体缺失：" + string.Join(" / ", missing.ToArray()));
            }

            static GameObject Prefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void BuildOne(ForgePack pack, ForgeLevel level, int index, string path, Tiles tiles)        {
            string[] rows = level.rows ?? new string[0];
            if (rows.Length == 0) return;

            int width = rows[0].Length;
            int height = rows.Length;
            float cell = Mathf.Round(398f / Mathf.Max(width, height) * 1000f) / 1000f;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                PrefabUtility.InstantiatePrefab(tiles.camera, scene);
                PrefabUtility.InstantiatePrefab(tiles.eventSystem, scene);

                // GameRoot：组件顺序与正式关卡一致（WaterGame → LevelManager → LevelDefinition）。
                var root = new GameObject("GameRoot");
                SceneManager.MoveGameObjectToScene(root, scene);
                var game = root.AddComponent<WaterGame>();
                var manager = root.AddComponent<LevelManager>();
                manager.levelIndex = index;
                manager.game = game;

                var definition = root.AddComponent<LevelDefinition>();
                definition.levelIndex = index;
                definition.levelName = level.name;
                definition.hint = level.hint ?? "";
                definition.rows = (string[])rows.Clone();

                // GameCanvas：直接用 UI_GameHud 预制体，Room 挂到它的 BoardCenter 下（和正式关卡同位置）。
                var hud = (GameObject)PrefabUtility.InstantiatePrefab(tiles.hud, scene);
                hud.name = "GameCanvas";
                var boardCenter = FindDeep(hud.transform, "BoardCenter");
                if (boardCenter == null)
                    throw new InvalidOperationException("UI_GameHud 预制体里找不到 BoardCenter。");

                var roomGo = new GameObject("Room", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(roomGo, scene);
                var room = (RectTransform)roomGo.transform;
                room.SetParent(boardCenter, false);
                room.anchorMin = room.anchorMax = new Vector2(.5f, .5f);
                room.pivot = new Vector2(.5f, .5f);
                room.anchoredPosition = Vector2.zero;
                room.sizeDelta = Vector2.zero;
                room.localRotation = Quaternion.identity;
                room.localScale = Vector3.one;

                var board = roomGo.AddComponent<LevelBoard>();
                board.room = room;
                board.cell = cell;

                // 地形：每格一块地砖，墙用 Tile_Wall，其余（地面/水/出口/鱼）都先铺 Tile_Floor。
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        char tile = rows[y][x];
                        bool wall = tile == '#';
                        Add(board, wall ? tiles.wall : tiles.floor, wall ? "Wall " + i : "Floor " + i,
                            Point(i, width, height, cell), wall ? cell + 1f : cell - 2f);
                    }
                }

                // 水：顺序必须和 Position.Water 一致（扫描序），鱼脚下那格、箱子格、泉眼都算水。
                var waters = new List<RectTransform>();
                var foams = new List<RectTransform>();
                var crates = new List<RectTransform>();
                RectTransform fish = null, exit = null, pearl = null, gate = null;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        char tile = rows[y][x];
                        Vector2 point = Point(i, width, height, cell);

                        if (tile == 'P' || tile == '~' || tile == 'B' || tile == 'b' || tile == 'o')
                        {
                            waters.Add(Add(board, tiles.water, "Water " + waters.Count, point, cell + 2f));
                            foams.Add(Add(board, tiles.foam, "Foam " + foams.Count, point, cell * 1.55f));
                        }

                        if (tile == 'E') exit = AddScaled(board, tiles.exit, "Exit", point, cell * .78f, 78f);
                        if (tile == 'K') pearl = Add(board, tiles.pearl, "Pearl", point, cell * .32f);
                        if (tile == 'B' || tile == 'b')
                            crates.Add(Add(board, tiles.crate, "Crate " + crates.Count, point, cell * .84f));
                        if (tile == 'P') fish = Add(board, tiles.fish, "Player", point, cell * .68f);
                        if (tile == 'G' && tiles.grate != null)
                            Add(board, tiles.grate, "Grate " + i, point, cell * .75f);
                        if (tile == 'D' && tiles.gate != null && gate == null)
                            gate = Add(board, tiles.gate, "Gate", point, cell * .70f);
                    }
                }

                if (exit == null)
                    throw new InvalidOperationException(level.name + "：盘面里没有出口 E。");

                board.water = waters.ToArray();
                board.foam = foams.ToArray();
                board.player = fish;
                board.crates = crates.ToArray();
                board.crate = crates.Count > 0 ? crates[0] : null;
                board.gate = gate != null ? gate.GetComponent<Image>() : null;
                board.sensor = null;
                board.pearl = pearl != null ? pearl.GetComponent<Image>() : null;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, path);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>和 WaterGame.Point 完全一致的格子坐标（Room 中心为原点）。</summary>
        static Vector2 Point(int index, int width, int height, float cell) =>
            new Vector2((index % width - (width - 1) * .5f) * cell,
                ((height - 1) * .5f - index / width) * cell);

        /// <summary>
        /// 出口预制体里有个 40px 的固定"洞"，缩 sizeDelta 会让洞盖住整块金框（大棋盘上看着就是一块黑格）。
        /// 所以出口保持预制体原尺寸，用 localScale 整体缩放，洞和金框的比例才不会走样。
        /// </summary>
        static RectTransform AddScaled(LevelBoard board, GameObject prefab, string name, Vector2 point,
            float targetSize, float nativeSize)
        {
            var rect = Add(board, prefab, name, point, nativeSize);
            float k = targetSize / nativeSize;
            rect.localScale = new Vector3(k, k, 1f);
            return rect;
        }

        static RectTransform Add(LevelBoard board, GameObject prefab, string name, Vector2 point, float size)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, board.room);
            go.name = name;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = point;
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            return rect;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "ForgePack";
            var sb = new StringBuilder();
            foreach (var c in value.Trim())
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            return sb.ToString();
        }
    }
}
#endif

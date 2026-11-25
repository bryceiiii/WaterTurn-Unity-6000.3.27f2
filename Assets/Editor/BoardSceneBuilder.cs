using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WaterTurn;

namespace WaterTurn.EditorTools
{
    /// <summary>
    /// 按 <see cref="Campaign"/> 重建一批关卡场景：盘面取自关卡数据，预制体沿用
    /// Assets/Prefabs 里现成的那套。传送门、重压格、阀门、时效感应格、薄膜、泉眼、
    /// 逆闸、感应格都是运行时白模，由 <see cref="WaterGame"/> 现场生成，
    /// 所以这里只落地地板、墙、水、鱼、箱子、出口和石门。
    /// </summary>
    public static class BoardSceneBuilder
    {
        public static void Build(int[] indices)
        {
            var camera = Load("Assets/Prefabs/Level/Camera_Level.prefab");
            var events = Load("Assets/Prefabs/Level/EventSystem.prefab");
            var hud = Load("Assets/Prefabs/UI/UI_GameHud.prefab");
            var floor = Load("Assets/Prefabs/Board/Tile_Floor.prefab");
            var wall = Load("Assets/Prefabs/Board/Tile_Wall.prefab");
            var water = Load("Assets/Prefabs/Board/Effect_Water.prefab");
            var foam = Load("Assets/Prefabs/Board/Effect_Foam.prefab");
            var player = Load("Assets/Prefabs/Board/Player_Fish.prefab");
            var crate = Load("Assets/Prefabs/Board/Obstacle_Crate.prefab");
            var gate = Load("Assets/Prefabs/Board/Mechanism_Gate.prefab");
            var exit = Load("Assets/Prefabs/Board/Prop_Exit.prefab");

            foreach (int index in indices)
            {
                var level = Campaign.Levels[index];
                var begin = Puzzle.Begin(level);
                if (begin == null) throw new System.InvalidOperationException("盘面无法落地: " + level.Name);

                float cell = Mathf.Min(100f,
                    510f / Mathf.Sqrt(level.Width * level.Width + level.Height * level.Height));
                Vector2 Point(int p) => new Vector2(
                    (p % level.Width - (level.Width - 1) * .5f) * cell,
                    ((level.Height - 1) * .5f - p / level.Width) * cell);

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Object.Instantiate(camera);
                Object.Instantiate(events);

                var root = new GameObject("GameRoot");
                var definition = root.AddComponent<LevelDefinition>();
                definition.levelIndex = index;
                definition.levelName = level.Name;
                definition.hint = level.Hint;
                definition.rows = Enumerable.Range(0, level.Height)
                    .Select(y => level.Tiles.Substring(y * level.Width, level.Width)).ToArray();

                var manager = root.AddComponent<LevelManager>();
                manager.levelIndex = index;
                manager.game = root.AddComponent<WaterGame>();

                var hudGo = (GameObject) PrefabUtility.InstantiatePrefab(hud, root.transform);
                hudGo.name = "GameCanvas";
                var hudView = hudGo.GetComponent<GameHud>();

                var room = NewUI("Room", hudView.boardArea);
                room.anchoredPosition = Vector2.zero;
                room.sizeDelta = Vector2.zero;
                var board = room.gameObject.AddComponent<LevelBoard>();
                board.room = room;
                board.cell = cell;

                RectTransform gateView = null, exitView = null;
                for (int p = 0; p < level.Tiles.Length; p++)
                {
                    char tile = level.Tiles[p];
                    if (tile == '#')
                        Instance(wall, room, Point(p), new Vector2(cell + 1, cell + 1), "Wall " + p);
                    else
                        Instance(floor, room, Point(p), new Vector2(cell - 2, cell - 2), "Floor " + p);

                    if (tile == 'E')
                    {
                        exitView = Instance(exit, room, Point(p), new Vector2(cell * .78f, cell * .78f), "Exit");
                        var hole = exitView.Find("Exit Hole") as RectTransform;
                        if (hole != null) hole.sizeDelta = new Vector2(cell * .40f, cell * .40f);
                    }
                    if (tile == 'D')
                        gateView = Instance(gate, room, Point(p), new Vector2(cell - 12, cell - 12), "Gate");
                }

                var waters = new RectTransform[begin.Water.Length];
                var foams = new RectTransform[begin.Water.Length];
                float foamSize = (cell + 2) * 1.5f;
                for (int i = 0; i < begin.Water.Length; i++)
                {
                    foams[i] = Instance(foam, room, Point(begin.Water[i]),
                        new Vector2(foamSize, foamSize), "Foam " + i);
                    waters[i] = Instance(water, room, Point(begin.Water[i]),
                        new Vector2(cell + 2, cell + 2), "Water " + i);
                }

                var crates = new RectTransform[begin.Boxes.Length];
                for (int i = 0; i < begin.Boxes.Length; i++)
                    crates[i] = Instance(crate, room, Point(begin.Boxes[i]),
                        new Vector2(cell * .84f, cell * .84f),
                        begin.Boxes.Length == 1 ? "Crate" : "Crate " + i);

                var playerView = Instance(player, room, Point(begin.Fish),
                    new Vector2(cell * .68f, cell * .68f), "Player");

                board.player = playerView;
                board.crate = crates.Length > 0 ? crates[0] : null;
                board.crates = crates;
                board.gate = gateView != null ? gateView.GetComponent<Image>() : null;
                board.water = waters;
                board.foam = foams;

                var path = "Assets/Scenes/Level_" + (index + 1).ToString("00") + ".unity";
                EditorSceneManager.SaveScene(scene, path);
                Debug.Log($"WaterTurn: 重建 {path}  {level.Name}  {level.Width}x{level.Height}  " +
                          $"水 {begin.Water.Length}  箱 {begin.Boxes.Length}  cell {cell:0.#}");
            }

            EnsureBuildSettings(indices);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // 建完不要停在空场景上：切回潮汐洞口，玩家从这里进关。
            const string hub = "Assets/Scenes/WaterTurnHub.unity";
            if (System.IO.File.Exists(hub))
                EditorSceneManager.OpenScene(hub, OpenSceneMode.Single);
            Verify(indices);
        }

        public static void Verify(int[] indices)
        {
            foreach (int index in indices)
            {
                var level = Campaign.Levels[index];
                var begin = Puzzle.Begin(level);
                if (begin == null)
                {
                    Debug.LogWarning($"{(index + 1):00} {level.Name}  起点即死");
                    continue;
                }
                string route = Puzzle.Solve(level, begin);
                if (route == null)
                {
                    Debug.LogWarning($"{(index + 1):00} {level.Name}  无解");
                    continue;
                }
                int turns = route.Count(c => c == '4' || c == '5');
                int steps = route.Length - turns;
                string pretty = string.Join(" ", route.Select(c => "↑→↓←RQ"[c - '0'].ToString()));
                Debug.Log($"{(index + 1):00} {level.Name}  {turns} 转 / {steps} 步   路线 {pretty}");
            }
        }

        static GameObject Load(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new System.IO.FileNotFoundException("缺少预制体: " + path);
            return asset;
        }

        static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }

        static RectTransform Instance(GameObject prefab, Transform parent, Vector2 position,
            Vector2 size, string name)
        {
            var go = (GameObject) PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static void EnsureBuildSettings(int[] indices)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (int index in indices)
            {
                string path = "Assets/Scenes/Level_" + (index + 1).ToString("00") + ".unity";
                int existing = scenes.FindIndex(s => s.path == path);
                if (existing >= 0)
                    scenes[existing] = new EditorBuildSettingsScene(path, true);
                else
                    scenes.Insert(Mathf.Min(index + 1, scenes.Count), new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

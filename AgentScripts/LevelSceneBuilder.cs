using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using WaterTurn;

namespace AgentBuild
{
    /// <summary>
    /// One-shot authoring pass: builds the shared prefabs, then one scene per
    /// Campaign level with the board materialised from prefab instances.
    /// Run through the Unity pipeline (script run) while the editor is idle.
    /// </summary>
    public static class LevelSceneBuilder
    {
        const string HubPath = "Assets/Scenes/WaterTurnHub.unity";
        const string OldLevelScene = "Assets/Scenes/WaterTurn.unity";

        static readonly Color Ink = new Color32(14, 23, 30, 255);
        static readonly Color Panel = new Color32(25, 39, 48, 255);
        static readonly Color Slate = new Color32(39, 59, 69, 255);
        static readonly Color White = new Color32(232, 248, 241, 255);
        static readonly Color Quiet = new Color32(156, 189, 193, 255);
        static readonly Color Teal = new Color32(120, 239, 234, 255);
        static readonly Color Amber = new Color32(255, 193, 124, 255);
        static readonly Color Gold = new Color32(247, 212, 134, 255);

        static Sprite Rounded;
        static Font PreviewFont;
        static readonly List<string> Report = new List<string>();

        sealed class BoardPrefabs
        {
            public GameObject floor, wall, water, foam, player, crate, grate, gate, sensor, pearl, exit;
        }

        public static string Build()
        {
            Rounded = EnsureRoundedSprite();
            PreviewFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var camera = BuildCameraPrefab();
            var eventSystem = BuildEventSystemPrefab();
            var board = BuildBoardPrefabs();
            var hud = BuildHudPrefab();

            var scenePaths = new List<string>();
            for (int i = 0; i < Campaign.Levels.Length; i++)
                scenePaths.Add(BuildLevelScene(i, camera, eventSystem, hud, board));

            var settings = new List<EditorBuildSettingsScene> {new EditorBuildSettingsScene(HubPath, true)};
            settings.AddRange(scenePaths.Select(path => new EditorBuildSettingsScene(path, true)));
            EditorBuildSettings.scenes = settings.ToArray();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OldLevelScene) != null)
                AssetDatabase.DeleteAsset(OldLevelScene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(HubPath, OpenSceneMode.Single);

            Report.Add("prefabs: Assets/Prefabs");
            Report.Add("scenes: " + scenePaths.Count);
            Report.Add("build scenes: " + EditorBuildSettings.scenes.Length);
            return string.Join("\n", Report);
        }

        // ---------------------------------------------------------------- folders

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (path.Length == 0 || AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static GameObject SavePrefab(GameObject go, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------------------------------------------------------------- UI helpers

        static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            if (parent != null) rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            return go;
        }

        static RectTransform Place(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = NewUI(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Image ImageOn(Transform parent, string name, Color color, Sprite sprite, Image.Type type)
        {
            var go = NewUI(name, parent);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = type;
            image.raycastTarget = false;
            return image;
        }

        static Text Label(Transform parent, string content, float x, float y, float w, float h, int size,
            Color color, FontStyle style = FontStyle.Normal)
        {
            var rt = Place(parent, content == "" ? "Text" : content, x, y, w, h);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = PreviewFont;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.raycastTarget = false;
            return text;
        }

        static void PanelAt(Transform parent, string name, float x, float y, float w, float h)
        {
            var image = Place(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = Panel;
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
        }

        static Button Control(Transform parent, string title, float x, float y, float w, float h,
            bool accent = false, int size = 17)
        {
            var rt = Place(parent, title, x, y, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = accent ? Teal : Slate;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            var label = Label(rt, title, 0, 0, w, h, size, accent ? Ink : White, FontStyle.Bold);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        // ---------------------------------------------------------------- rounded sprite

        static Sprite EnsureRoundedSprite()
        {
            const string folder = "Assets/Art/UI";
            const string path = folder + "/rounded.png";
            EnsureFolder(folder);
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cornerX = Mathf.Max(0, Mathf.Abs(x - 31.5f) - 24);
                float cornerY = Mathf.Max(0, Mathf.Abs(y - 31.5f) - 24);
                if (cornerX * cornerX + cornerY * cornerY < 59) colors[y * size + x] = new Color32(255, 255, 255, 255);
            }
            texture.SetPixels32(colors);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter) AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = new Vector4(9, 9, 9, 9);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------------------------------------------------------------- prefabs

        static GameObject BuildCameraPrefab()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.19215687f, 0.3019608f, 0.4745098f, 0f);
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 1000f;
            camera.depth = -1;
            camera.allowMSAA = false;
            camera.allowHDR = true;
            go.transform.position = new Vector3(0, 0, -10);
            return SavePrefab(go, "Assets/Prefabs/Level/Camera_Level.prefab");
        }

        static GameObject BuildEventSystemPrefab()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return SavePrefab(go, "Assets/Prefabs/Level/EventSystem.prefab");
        }

        static GameObject BuildHudPrefab()
        {
            var root = new GameObject("UI_GameHud", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.matchWidthOrHeight = .5f;
            root.AddComponent<GraphicRaycaster>();
            var hud = root.AddComponent<GameHud>();

            var backdrop = ImageOn(root.transform, "Backdrop", Ink, null, Image.Type.Simple);
            backdrop.rectTransform.anchorMin = Vector2.zero;
            backdrop.rectTransform.anchorMax = Vector2.one;
            backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;

            var stage = NewUI("Layout", root.transform).GetComponent<RectTransform>();
            stage.sizeDelta = new Vector2(1280, 800);

            Label(stage, "借水过河", 58, -49, 545, 67, 49, White, FontStyle.Bold);
            Label(stage, "GRID PUZZLE  /  TTURN", 58, -113, 545, 30, 17, Teal, FontStyle.Bold);
            hud.home = Control(stage, "回洞穴  Esc", 1108, -45, 120, 38, false, 15);
            PanelAt(stage, "BoardFrame", 57, -157, 694, 594);
            PanelAt(stage, "ControlPanel", 774, -157, 450, 594);

            hud.boardArea = Place(stage, "BoardCenter", 404, -452, 0, 0);
            hud.boardArea.name = "BoardCenter";

            Label(stage, "重力  ↓", 635, -171, 104, 27, 17, Quiet);

            hud.headline = Label(stage, "", 800, -181, 400, 45, 28, White, FontStyle.Bold);
            hud.headline.gameObject.name = "Headline";
            hud.instructions = Label(stage, "", 800, -230, 391, 61, 18, Quiet);
            hud.instructions.gameObject.name = "Instructions";
            hud.mechanism = Label(stage, "", 800, -295, 394, 71, 16, Teal);
            hud.mechanism.gameObject.name = "Mechanism";
            hud.status = Label(stage, "", 800, -371, 389, 63, 19, Amber);
            hud.status.gameObject.name = "Status";
            hud.readout = Label(stage, "", 800, -437, 380, 32, 16, Quiet);
            hud.readout.gameObject.name = "Readout";

            hud.up = Control(stage, "↑", 868, -480, 52, 42);
            hud.left = Control(stage, "←", 812, -526, 52, 42);
            hud.down = Control(stage, "↓", 868, -526, 52, 42);
            hud.right = Control(stage, "→", 924, -526, 52, 42);
            hud.turn = Control(stage, "转房间  /  空格", 992, -493, 205, 62, true);
            hud.undo = Control(stage, "撤销 Z", 800, -586, 96, 40);
            hud.restart = Control(stage, "重来 R", 902, -586, 96, 40);
            hud.hint = Control(stage, "提示 H", 1004, -586, 96, 40);
            hud.next = Control(stage, "下一关 ↵", 1105, -586, 99, 40, true);
            hud.next.gameObject.SetActive(false);
            Label(stage, "关卡  ·  通关后解锁下一关", 800, -638, 390, 25, 16, Quiet);

            hud.levelButtons = new Button[Campaign.VisibleLevels];
            for (int i = 0; i < Campaign.VisibleLevels; i++)
                hud.levelButtons[i] = Control(stage, (i + 1).ToString("00"),
                    800 + (i % 10) * 40, -665 - (i / 10) * 34, 36, 30, false, 16);

            return SavePrefab(root, "Assets/Prefabs/UI/UI_GameHud.prefab");
        }

        static BoardPrefabs BuildBoardPrefabs()
        {
            var rock = Resources.Load<Sprite>("Art/Water/rock");
            var wood = Resources.Load<Sprite>("Art/Water/wood");
            var water = Resources.Load<Sprite>("Art/Water/water_0");
            var foam = Resources.Load<Sprite>("Art/Water/foam_0");
            var fish = Resources.Load<Sprite>("Art/Fish/swim_right_0");

            var prefabs = new BoardPrefabs();

            // floor
            var floor = NewUI("Tile_Floor", null);
            var floorRect = floor.GetComponent<RectTransform>();
            floorRect.sizeDelta = new Vector2(98, 98);
            var floorImage = floor.AddComponent<Image>();
            floorImage.color = new Color32(16, 29, 38, 255);
            floorImage.sprite = Rounded;
            floorImage.raycastTarget = false;
            prefabs.floor = SavePrefab(floor, "Assets/Prefabs/Board/Tile_Floor.prefab");

            // wall
            var wall = NewUI("Tile_Wall", null);
            wall.GetComponent<RectTransform>().sizeDelta = new Vector2(101, 101);
            var wallImage = wall.AddComponent<Image>();
            wallImage.color = new Color32(32, 42, 52, 255);
            wallImage.sprite = Rounded;
            wallImage.raycastTarget = false;
            var rockImage = ImageOn(wall.transform, "Rock", new Color32(188, 202, 232, 255), rock, Image.Type.Simple);
            rockImage.rectTransform.sizeDelta = new Vector2(100, 100);
            prefabs.wall = SavePrefab(wall, "Assets/Prefabs/Board/Tile_Wall.prefab");

            // water
            var waterGo = NewUI("Tile_Water", null);
            waterGo.GetComponent<RectTransform>().sizeDelta = new Vector2(102, 102);
            var waterImage = waterGo.AddComponent<Image>();
            waterImage.color = Color.white;
            waterImage.sprite = water;
            waterImage.raycastTarget = false;
            prefabs.water = SavePrefab(waterGo, "Assets/Prefabs/Board/Effect_Water.prefab");

            // foam
            var foamGo = NewUI("Effect_Foam", null);
            foamGo.GetComponent<RectTransform>().sizeDelta = new Vector2(153, 153);
            var foamImage = foamGo.AddComponent<Image>();
            foamImage.color = Color.white;
            foamImage.sprite = foam;
            foamImage.raycastTarget = false;
            prefabs.foam = SavePrefab(foamGo, "Assets/Prefabs/Board/Effect_Foam.prefab");

            // player
            var player = NewUI("Player_Fish", null);
            player.GetComponent<RectTransform>().sizeDelta = new Vector2(68, 68);
            var playerImage = player.AddComponent<Image>();
            playerImage.color = White;
            playerImage.sprite = fish;
            playerImage.preserveAspect = true;
            playerImage.raycastTarget = false;
            prefabs.player = SavePrefab(player, "Assets/Prefabs/Board/Player_Fish.prefab");

            // crate
            var crate = NewUI("Obstacle_Crate", null);
            crate.GetComponent<RectTransform>().sizeDelta = new Vector2(84, 84);
            var crateImage = crate.AddComponent<Image>();
            crateImage.color = Color.white;
            crateImage.sprite = wood;
            crateImage.raycastTarget = false;
            prefabs.crate = SavePrefab(crate, "Assets/Prefabs/Board/Obstacle_Crate.prefab");

            // grate
            var grate = NewUI("Obstacle_Grate", null);
            grate.GetComponent<RectTransform>().sizeDelta = new Vector2(75, 75);
            var grateImage = grate.AddComponent<Image>();
            grateImage.color = new Color32(101, 122, 140, 160);
            grateImage.sprite = Rounded;
            grateImage.raycastTarget = false;
            for (int i = -1; i <= 1; i++)
            {
                var bar = ImageOn(grate.transform, "Bar" + (i + 1), Quiet, Rounded, Image.Type.Simple);
                bar.rectTransform.sizeDelta = new Vector2(4, 65);
                bar.rectTransform.anchoredPosition = new Vector2(i * 18, 0);
            }
            prefabs.grate = SavePrefab(grate, "Assets/Prefabs/Board/Obstacle_Grate.prefab");

            // gate
            var gate = NewUI("Mechanism_Gate", null);
            gate.GetComponent<RectTransform>().sizeDelta = new Vector2(88, 88);
            var gateImage = gate.AddComponent<Image>();
            gateImage.color = Amber;
            gateImage.sprite = Rounded;
            gateImage.raycastTarget = false;
            prefabs.gate = SavePrefab(gate, "Assets/Prefabs/Board/Mechanism_Gate.prefab");

            // sensor
            var sensor = NewUI("Mechanism_Sensor", null);
            sensor.GetComponent<RectTransform>().sizeDelta = new Vector2(54, 54);
            var sensorImage = sensor.AddComponent<Image>();
            sensorImage.color = Teal;
            sensorImage.sprite = Rounded;
            sensorImage.raycastTarget = false;
            prefabs.sensor = SavePrefab(sensor, "Assets/Prefabs/Board/Mechanism_Sensor.prefab");

            // pearl
            var pearl = NewUI("Prop_Pearl", null);
            pearl.GetComponent<RectTransform>().sizeDelta = new Vector2(32, 32);
            var pearlImage = pearl.AddComponent<Image>();
            pearlImage.color = Gold;
            pearlImage.sprite = Rounded;
            pearlImage.raycastTarget = false;
            prefabs.pearl = SavePrefab(pearl, "Assets/Prefabs/Board/Prop_Pearl.prefab");

            // exit
            var exit = NewUI("Prop_Exit", null);
            exit.GetComponent<RectTransform>().sizeDelta = new Vector2(78, 78);
            var exitImage = exit.AddComponent<Image>();
            exitImage.color = new Color32(196, 148, 82, 255);
            exitImage.sprite = Rounded;
            exitImage.raycastTarget = false;
            var hole = ImageOn(exit.transform, "Exit Hole", new Color32(26, 34, 44, 255), Rounded, Image.Type.Simple);
            hole.rectTransform.sizeDelta = new Vector2(40, 40);
            prefabs.exit = SavePrefab(exit, "Assets/Prefabs/Board/Prop_Exit.prefab");

            return prefabs;
        }

        // ---------------------------------------------------------------- scenes

        static RectTransform Instance(GameObject prefab, Transform parent, Vector2 position, Vector2 size, string name)
        {
            var go = (GameObject) PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        static string BuildLevelScene(int index, GameObject cameraPrefab, GameObject eventSystemPrefab,
            GameObject hudPrefab, BoardPrefabs prefabs)
        {
            var level = Campaign.Levels[index];
            var begin = Puzzle.Begin(level);
            if (begin == null) throw new InvalidOperationException("关卡数据无法落地: " + level.Name);

            float cell = Mathf.Min(100f, 510f / Mathf.Sqrt(level.Width * level.Width + level.Height * level.Height));
            Vector2 Point(int p) => new Vector2(
                (p % level.Width - (level.Width - 1) * .5f) * cell,
                ((level.Height - 1) * .5f - p / level.Width) * cell);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(cameraPrefab);
            PrefabUtility.InstantiatePrefab(eventSystemPrefab);

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

            var hudGo = (GameObject) PrefabUtility.InstantiatePrefab(hudPrefab, root.transform);
            hudGo.name = "GameCanvas";
            var hud = hudGo.GetComponent<GameHud>();

            var room = NewUI("Room", hud.boardArea).GetComponent<RectTransform>();
            room.anchoredPosition = Vector2.zero;
            room.sizeDelta = Vector2.zero;
            var board = room.gameObject.AddComponent<LevelBoard>();
            board.room = room;
            board.cell = cell;

            RectTransform gate = null, sensor = null, pearl = null;
            for (int p = 0; p < level.Tiles.Length; p++)
            {
                char tile = level.Tiles[p];
                if (tile == '#')
                {
                    var wall = Instance(prefabs.wall, room, Point(p), new Vector2(cell + 1, cell + 1), "Wall " + p);
                    var rock = wall.Find("Rock") as RectTransform;
                    if (rock != null) rock.sizeDelta = new Vector2(cell, cell);
                }
                else
                    Instance(prefabs.floor, room, Point(p), new Vector2(cell - 2, cell - 2), "Floor " + p);

                if (tile == 'E')
                {
                    var exit = Instance(prefabs.exit, room, Point(p), new Vector2(cell * .78f, cell * .78f), "Exit");
                    var hole = exit.Find("Exit Hole") as RectTransform;
                    if (hole != null) hole.sizeDelta = new Vector2(cell * .40f, cell * .40f);
                }
                if (tile == 'D') gate = Instance(prefabs.gate, room, Point(p), new Vector2(cell - 12, cell - 12), "Gate");
                if (tile == 'S') sensor = Instance(prefabs.sensor, room, Point(p), new Vector2(cell * .54f, cell * .54f), "Sensor");
                if (tile == 'K') pearl = Instance(prefabs.pearl, room, Point(p), new Vector2(cell * .32f, cell * .32f), "Pearl");
            }

            var water = new RectTransform[begin.Water.Length];
            var foam = new RectTransform[begin.Water.Length];
            float foamSize = (cell + 2) * 1.5f;
            for (int i = 0; i < begin.Water.Length; i++)
            {
                foam[i] = Instance(prefabs.foam, room, Point(begin.Water[i]), new Vector2(foamSize, foamSize), "Foam " + i);
                water[i] = Instance(prefabs.water, room, Point(begin.Water[i]), new Vector2(cell + 2, cell + 2), "Water " + i);
            }

            for (int p = 0; p < level.Tiles.Length; p++)
            {
                if (level.Tiles[p] != 'G') continue;
                var grate = Instance(prefabs.grate, room, Point(p), new Vector2(cell * .75f, cell * .75f), "Grate " + p);
                for (int i = -1; i <= 1; i++)
                {
                    var bar = grate.Find("Bar" + (i + 1)) as RectTransform;
                    if (bar == null) continue;
                    bar.sizeDelta = new Vector2(4, cell * .65f);
                    bar.anchoredPosition = new Vector2(i * cell * .18f, 0);
                }
            }

            RectTransform crate = null;
            if (begin.Box >= 0)
                crate = Instance(prefabs.crate, room, Point(begin.Box),
                    new Vector2(cell * .84f, cell * .84f), "Crate");

            var player = Instance(prefabs.player, room, Point(begin.Fish),
                new Vector2(cell * .68f, cell * .68f), "Player");

            board.player = player;
            board.crate = crate;
            board.gate = gate != null ? gate.GetComponent<Image>() : null;
            board.sensor = sensor != null ? sensor.GetComponent<Image>() : null;
            board.pearl = pearl != null ? pearl.GetComponent<Image>() : null;
            board.water = water;
            board.foam = foam;

            var path = "Assets/Scenes/Level_" + (index + 1).ToString("00") + ".unity";
            EditorSceneManager.SaveScene(scene, path);
            Report.Add("Level_" + (index + 1).ToString("00") + "  <-  " + level.Name +
                       "  (" + level.Width + "x" + level.Height + ", " + begin.Water.Length + " water, cell " +
                       cell.ToString("0.#") + ")");
            return path;
        }
    }
}

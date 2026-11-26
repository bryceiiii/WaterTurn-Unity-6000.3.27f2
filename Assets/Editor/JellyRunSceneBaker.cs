using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WaterTurn;

namespace WaterTurn.EditorTools
{
    /// <summary>
    /// One-time authoring bake: turns the code-generated JellyRun world into real
    /// prefab instances in the scene.  After baking, the scene owns positions and
    /// sizes and JellyRunGame binds its physics to those objects.
    /// </summary>
    public static class JellyRunSceneBaker
    {
        public const string ScenePath = "Assets/Scenes/JellyRun.unity";
        const string PrefabFolder = "Assets/Prefabs/JellyRun";
        const string Menu = "WaterTurn/JellyRun/";

        [MenuItem(Menu + "烘焙场景与预制体", false, 20)]
        public static void BakeMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("果冻潮池", "请先退出 Play 模式再烘焙。", "好");
                return;
            }
            if (!EditorUtility.DisplayDialog("果冻潮池",
                    "这会用当前 JellyRunData 重建 JellyRun 场景中的 Canvas / World / 预制体实例。\n场景里的手动修改会被覆盖，确定继续？",
                    "烘焙", "取消"))
                return;
            BakeSilent();
        }

        [MenuItem(Menu + "校验场景", false, 21)]
        public static void ValidateMenu()
        {
            string report = ValidateScene();
            Debug.Log("[JellyRun 场景] " + report);
            EditorUtility.DisplayDialog("果冻潮池 · 场景校验", report, "好");
        }

        /// <summary>Entry point for the Unity pipeline.</summary>
        public static string BakeSilent()
        {
            if (EditorApplication.isPlaying)
                return "Play 模式中不能烘焙。";

            var active = SceneManager.GetActiveScene();
            if (active.path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // The view scripts must be imported before the prefab baker resolves
            // their MonoScript GUIDs; otherwise a prefab can be written with
            // m_Script: 0 and the instances lose their components on load.
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            EnsureFolders();
            var prefabs = BuildPrefabs();
            if (prefabs == null) return "预制体创建失败，查看 Console。";

            var scene = SceneManager.GetActiveScene();
            var root = GameObject.Find("JellyRun");
            if (root == null)
            {
                root = new GameObject("JellyRun");
                root.AddComponent<JellyRunGame>();
            }

            BuildCanvas(root.transform, out var world, out var board);
            PopulateWorld(world, board, prefabs);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            JellyRunSceneTools.AddToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report = "烘焙完成 · " + PrefabFolder + " · " + ScenePath + "\n" + ValidateScene();
            Debug.Log("[JellyRun] " + report);
            return report;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "JellyRun");
        }

        static void BuildCanvas(Transform root, out RectTransform world, out JellyRunLevelBoard board)
        {
            var oldCanvas = root.Find("JellyRunCanvas");
            if (oldCanvas != null) UnityEngine.Object.DestroyImmediate(oldCanvas.gameObject);

            var canvasGo = new GameObject("JellyRunCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(root, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            var camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraGo.tag = "MainCamera";
                camera = cameraGo.GetComponent<Camera>();
            }
            camera.orthographic = true;
            camera.orthographicSize = JellyRunData.ViewH * .5f;
            camera.cullingMask = -1;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.027f, .078f, .114f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 100;
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(.5f, .5f);
            canvasRect.pivot = new Vector2(.5f, .5f);
            canvasRect.sizeDelta = new Vector2(JellyRunData.ViewW, JellyRunData.ViewH);
            canvasRect.localScale = Vector3.one;
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.localPosition = Vector3.zero;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referenceResolution = new Vector2(JellyRunData.ViewW, JellyRunData.ViewH);

            var worldGo = new GameObject("World", typeof(RectTransform));
            worldGo.transform.SetParent(canvasGo.transform, false);
            world = (RectTransform)worldGo.transform;
            world.anchorMin = world.anchorMax = new Vector2(0f, 1f);
            world.pivot = new Vector2(0f, 1f);
            world.sizeDelta = new Vector2(JellyRunData.WorldW, JellyRunData.WorldH);
            world.anchoredPosition = Vector2.zero;

            board = worldGo.AddComponent<JellyRunLevelBoard>();
            board.worldRect = world;
        }

        static void PopulateWorld(RectTransform world, JellyRunLevelBoard board, Prefabs prefabs)
        {
            for (int i = 0; i < JellyRunData.Floors.Length; i++)
            {
                var f = JellyRunData.Floors[i];
                var go = Instantiate(prefabs.floor, world, "Floor " + i);
                SetRect(go, f.x, f.y, f.w, f.h);
                var view = go.GetComponent<JellyRunFloorView>();
                view.ceiling = f.y <= 1f;
                view.Refresh();
            }

            for (int i = 0; i < JellyRunData.Spikes.Length; i++)
            {
                var s = JellyRunData.Spikes[i];
                var go = Instantiate(prefabs.spike, world, "Spike " + i);
                SetRect(go, s.x, s.y, s.w, s.h);
                var view = go.GetComponent<JellyRunSpikeView>();
                view.up = s.up;
                view.Refresh();
            }

            var doorGo = Instantiate(prefabs.door, world, "Gate");
            var d = JellyRunData.Door;
            SetRect(doorGo, d.x, d.y, d.w, d.h);
            doorGo.GetComponent<JellyRunDoorView>().Refresh();

            var wellJelly = (JellyRunJellyView)null;
            for (int i = 0; i < JellyRunData.Jellies.Length; i++)
            {
                var j = JellyRunData.Jellies[i];
                var go = Instantiate(prefabs.jelly, world, "Jelly " + i);
                SetRect(go, j.x, j.y, j.w, j.h);
                var view = go.GetComponent<JellyRunJellyView>();
                view.blob = j.blob;
                view.floodTo = j.floodTo;
                view.Refresh();
                if (j.floodTo >= 0f) wellJelly = view;
            }

            if (wellJelly != null)
            {
                var valveGo = Instantiate(prefabs.valve, world, "Well Valve");
                SetPoint(valveGo, JellyRunData.Jellies[9].valve.x, JellyRunData.Jellies[9].valve.y, 60f, 60f);
                var valve = valveGo.GetComponent<JellyRunValveView>();
                valve.target = wellJelly;
                valve.Refresh();
            }

            var switchGo = Instantiate(prefabs.switchStone, world, "Switch");
            SetPoint(switchGo, JellyRunData.Switch.position.x, JellyRunData.Switch.position.y, 120f, 120f);
            var switchView = switchGo.GetComponent<JellyRunSwitchView>();
            switchView.radius = JellyRunData.Switch.radius;
            switchView.Refresh();

            for (int i = 0; i < JellyRunData.Entrances.Length; i++)
            {
                var e = JellyRunData.Entrances[i];
                var go = Instantiate(prefabs.entrance, world, "Entrance " + (i + 1).ToString("00"));
                SetPoint(go, e.entryX, e.top, 280f, 260f);
                var view = go.GetComponent<JellyRunEntranceView>();
                view.chapter = i;
                if (view.platform != null)
                    view.platform.sizeDelta = new Vector2(e.platformW, 16f);
                view.Refresh();
            }

            var portalGo = Instantiate(prefabs.portal, world, "Portal");
            var p = JellyRunData.Portal;
            SetPoint(portalGo, p.x + p.w * .5f, p.y + p.h * .5f, 240f, 240f);
            portalGo.GetComponent<JellyRunPortalView>().Refresh();

            for (int i = 0; i < JellyRunData.Saws.Length; i++)
            {
                var s = JellyRunData.Saws[i];
                var go = Instantiate(prefabs.saw, world, "Saw " + i);
                SetPoint(go, s.ax, s.ay, s.r * 2f + 12f, s.r * 2f + 12f);
                var view = go.GetComponent<JellyRunSawView>();
                view.radius = s.r;
                view.speed = s.speed;
                view.phase = s.phase;
                var a = (RectTransform)view.pathA;
                var b = (RectTransform)view.pathB;
                a.anchoredPosition = Vector2.zero;
                b.anchoredPosition = new Vector2(s.bx - s.ax, -(s.by - s.ay));
                view.Refresh();
            }

            var spawnGo = Instantiate(prefabs.spawn, world, "Spawn");
            SetPoint(spawnGo, JellyRunData.FishStart.x, JellyRunData.FishStart.y, 20f, 20f);
        }

        static GameObject Instantiate(GameObject prefab, RectTransform parent, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            return go;
        }

        static void SetRect(GameObject go, float x, float y, float w, float h)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x + w * .5f, -(y + h * .5f));
        }

        static void SetPoint(GameObject go, float cx, float cy, float w, float h)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(cx, -cy);
        }

        sealed class Prefabs
        {
            public GameObject floor, ledge, jelly, entrance, spike, saw, door, switchStone, portal, valve, spawn;
        }

        static Prefabs BuildPrefabs()
        {
            var result = new Prefabs();
            result.floor = SavePrefab("Floor", new Vector2(200f, 100f), rt =>
            {
                var image = rt.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                var view = rt.gameObject.AddComponent<JellyRunFloorView>();
                view.image = image;
                view.Refresh();
            });
            result.ledge = SavePrefab("Ledge", new Vector2(120f, 16f), rt =>
            {
                var image = rt.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                var view = rt.gameObject.AddComponent<JellyRunLedgeView>();
                view.image = image;
                view.Refresh();
            });
            result.jelly = SavePrefab("Jelly", new Vector2(380f, 120f), rt =>
            {
                var view = rt.gameObject.AddComponent<JellyRunJellyView>();
                view.EnsureVisuals();
                view.Refresh();
            });
            result.entrance = SavePrefab("Entrance", new Vector2(280f, 260f), rt =>
            {
                var view = rt.gameObject.AddComponent<JellyRunEntranceView>();
                view.chapter = 0;
                view.EnsureVisuals();
                view.Refresh();
            });
            result.spike = SavePrefab("Spike", new Vector2(220f, 36f), rt =>
            {
                var image = rt.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                var view = rt.gameObject.AddComponent<JellyRunSpikeView>();
                view.image = image;
                view.up = true;
                view.Refresh();
            });
            result.saw = SavePrefab("Saw", new Vector2(80f, 80f), rt =>
            {
                var view = rt.gameObject.AddComponent<JellyRunSawView>();
                view.radius = 34f;
                view.EnsureVisuals();
                view.Refresh();
            });
            result.door = SavePrefab("Door", new Vector2(80f, 360f), rt =>
            {
                var view = rt.gameObject.AddComponent<JellyRunDoorView>();
                view.EnsureVisuals();
                view.Refresh();
            });
            result.switchStone = SavePrefab("Switch", new Vector2(120f, 120f), rt =>
            {
                var view = rt.gameObject.AddComponent<JellyRunSwitchView>();
                view.radius = 38f;
                view.EnsureVisuals();
                view.Refresh();
            });
            result.portal = SavePrefab("Portal", new Vector2(240f, 240f), rt =>
            {
                var view = rt.gameObject.AddComponent<JellyRunPortalView>();
                view.EnsureVisuals();
                view.Refresh();
            });
            result.valve = SavePrefab("Valve", new Vector2(60f, 60f), rt =>
            {
                var view = rt.gameObject.AddComponent<JellyRunValveView>();
                view.EnsureVisuals();
                view.Refresh();
            });
            result.spawn = SavePrefab("Spawn", new Vector2(20f, 20f), rt =>
            {
                rt.gameObject.AddComponent<JellyRunSpawn>();
            });

            if (result.floor == null || result.jelly == null || result.entrance == null)
                return null;
            return result;
        }

        static GameObject SavePrefab(string name, Vector2 size, Action<RectTransform> build)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = size;
            build(rt);
            string path = PrefabFolder + "/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out bool success);
            UnityEngine.Object.DestroyImmediate(go);
            if (!success) Debug.LogError("[JellyRun] 预制体保存失败: " + path);
            return prefab;
        }

        public static string ValidateScene()
        {
            var board = UnityEngine.Object.FindFirstObjectByType<JellyRunLevelBoard>();
            if (board == null) return "失败：场景中没有 JellyRunLevelBoard。";

            var floors = board.GetComponentsInChildren<JellyRunFloorView>(true);
            var ledges = board.GetComponentsInChildren<JellyRunLedgeView>(true);
            var jellies = board.GetComponentsInChildren<JellyRunJellyView>(true);
            var spikes = board.GetComponentsInChildren<JellyRunSpikeView>(true);
            var saws = board.GetComponentsInChildren<JellyRunSawView>(true);
            var entrances = board.GetComponentsInChildren<JellyRunEntranceView>(true);
            var portals = board.GetComponentsInChildren<JellyRunPortalView>(true);
            var doors = board.GetComponentsInChildren<JellyRunDoorView>(true);
            var switches = board.GetComponentsInChildren<JellyRunSwitchView>(true);
            var valves = board.GetComponentsInChildren<JellyRunValveView>(true);
            var spawns = board.GetComponentsInChildren<JellyRunSpawn>(true);

            var problems = new List<string>();
            if (floors.Length == 0) problems.Add("没有 Floor");
            if (jellies.Length == 0) problems.Add("没有 Jelly");
            if (ledges.Length != entrances.Length) problems.Add("Ledge 数量与 Entrance 不一致");
            if (entrances.Length != 5) problems.Add("Entrance 应为 5 个，实际 " + entrances.Length);
            if (portals.Length != 1) problems.Add("Portal 应为 1 个");
            if (doors.Length != 1) problems.Add("Door 应为 1 个");
            if (switches.Length != 1) problems.Add("Switch 应为 1 个");
            if (spawns.Length != 1) problems.Add("Spawn 应为 1 个");

            var chapters = new HashSet<int>();
            foreach (var entrance in entrances)
            {
                if (entrance.chapter < 0 || entrance.chapter > 4)
                    problems.Add(entrance.name + " chapter 越界");
                else if (!chapters.Add(entrance.chapter))
                    problems.Add("重复章节 " + entrance.chapter);
                if (entrance.platform == null)
                    problems.Add(entrance.name + " 缺少 Platform");
            }
            if (chapters.Count != 5) problems.Add("章节覆盖不足 5 个");

            foreach (var saw in saws)
                if (saw.pathA == null || saw.pathB == null)
                    problems.Add(saw.name + " 缺少 Path A/B");

            string report = problems.Count == 0
                ? "OK · floors " + floors.Length + " · ledges " + ledges.Length + " · jellies " + jellies.Length +
                  " · spikes " + spikes.Length + " · saws " + saws.Length + " · entrances " + entrances.Length +
                  " · portal " + portals.Length + " · valve " + valves.Length + "\n场景对象就是玩法数据；拖动/缩放后直接 Play 即可。"
                : string.Join("\n", problems);
            return report;
        }
    }
}

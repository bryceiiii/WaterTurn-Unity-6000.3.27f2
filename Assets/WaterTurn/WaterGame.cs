using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WaterTurn
{
    public sealed class WaterGame : MonoBehaviour
    {
        const string Progress = "WaterTurn.ChapterProgress.v1";
        static readonly Color Ink = new Color32(14, 23, 30, 255);
        static readonly Color Panel = new Color32(25, 39, 48, 255);
        static readonly Color Slate = new Color32(39, 59, 69, 255);
        static readonly Color White = new Color32(232, 248, 241, 255);
        static readonly Color Quiet = new Color32(156, 189, 193, 255);
        static readonly Color Teal = new Color32(120, 239, 234, 255);
        static readonly Color Amber = new Color32(255, 193, 124, 255);
        static readonly Color Gold = new Color32(247, 212, 134, 255);

        RectTransform geometry, board;
        Font font;
        Sprite rounded, fishSprite;
        Sprite[] waterFrames, foamFrames;
        Sprite rockSprite, woodSprite;
        Text headline, instructions, readout, status, mechanism;
        Button next, undo, home;
        readonly List<Button> levels = new List<Button>();
        readonly Stack<Position> history = new Stack<Position>();
        RectTransform[] waters, foams;
        Image[] waterImages, foamImages;
        RectTransform fish, crate;
        Image gate, pearl, sensor;
        int index, unlocked, waterFrame = -1, foamFrame = -1;
        float cell;
        bool busy, won;
        Position position;
        Level Current => Campaign.Levels[index];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SceneManager.GetActiveScene().name == GameSession.HubScene) return;
            if (FindFirstObjectByType<WaterGame>() == null)
                new GameObject("WaterTurn").AddComponent<WaterGame>();
        }

        void Awake()
        {
            unlocked = Mathf.Clamp(PlayerPrefs.GetInt(Progress, 0), 0, Campaign.VisibleLevels - 1);
            font = Font.CreateDynamicFontFromOSFont(new[] {"Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial"}, 28);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rounded = MakeSprite(false);
            fishSprite = MakeSprite(true);
            LoadArt();
            BuildInterface();

            int start = GameSession.StartLevel >= 0
                ? Mathf.Clamp(GameSession.StartLevel, 0, Campaign.Levels.Length - 1)
                : unlocked;
            GameSession.StartLevel = -1;
            Load(start);
        }

        void LoadArt()
        {
            var frames = new List<Sprite>();
            for (int i = 0; i < 5; i++)
            {
                var frame = Resources.Load<Sprite>("Art/Water/water_" + i);
                if (frame != null) frames.Add(frame);
            }
            if (frames.Count == 0)
                Debug.LogWarning("WaterTurn: 水流动画帧缺失，回退到纯色水格。");
            waterFrames = frames.ToArray();
            var foam = new List<Sprite>();
            for (int i = 0; i < 8; i++)
            {
                var frame = Resources.Load<Sprite>("Art/Water/foam_" + i);
                if (frame != null) foam.Add(frame);
            }
            foamFrames = foam.ToArray();
            rockSprite = Resources.Load<Sprite>("Art/Water/rock");
            woodSprite = Resources.Load<Sprite>("Art/Water/wood");
        }

        void AnimateWater()
        {
            int tick = Mathf.FloorToInt(Time.unscaledTime * 7f);
            if (waterFrames.Length > 0 && waterImages != null && tick % waterFrames.Length != waterFrame)
            {
                waterFrame = tick % waterFrames.Length;
                for (int i = 0; i < waterImages.Length; i++)
                    if (waterImages[i] != null) waterImages[i].sprite = waterFrames[waterFrame];
            }
            int foamTick = Mathf.FloorToInt(Time.unscaledTime * 4f);
            if (foamFrames.Length > 0 && foamImages != null && foamTick % foamFrames.Length != foamFrame)
            {
                foamFrame = foamTick % foamFrames.Length;
                for (int i = 0; i < foamImages.Length; i++)
                    if (foamImages[i] != null) foamImages[i].sprite = foamFrames[foamFrame];
            }
        }

        void Update()
        {
            AnimateWater();
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { ReturnToHub(); return; }
            if (keyboard.rKey.wasPressedThisFrame) { Load(index); return; }
            if (keyboard.zKey.wasPressedThisFrame) { Undo(); return; }
            if (keyboard.hKey.wasPressedThisFrame) { Hint(); return; }
            if (won && keyboard.enterKey.wasPressedThisFrame)
            {
                if (index >= Campaign.VisibleLevels) ReturnToHub();
                else Advance();
                return;
            }
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame) Play(Move.Turn);
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) Play(Move.Up);
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) Play(Move.Right);
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) Play(Move.Down);
            else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) Play(Move.Left);
        }

        void BuildInterface()
        {
            var root = NewRect("GameCanvas", null);
            root.SetParent(transform, false);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = root.gameObject.AddComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1280, 800);
            scale.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var bk = ImageOn(root, "Backdrop", Ink, null);
            bk.rectTransform.anchorMin = Vector2.zero;
            bk.rectTransform.anchorMax = Vector2.one;
            bk.rectTransform.offsetMin = bk.rectTransform.offsetMax = Vector2.zero;
            var stage = Place(root, "Layout", 0, 0, 1280, 800);
            Label(stage, "借水过河", 58, -49, 545, 67, 49, White, FontStyle.Bold);
            Label(stage, "GRID PUZZLE  /  TTURN", 58, -113, 545, 30, 17, Teal, FontStyle.Bold);
            home = Control(stage, "回洞穴  Esc", 1108, -45, 120, 38, ReturnToHub, false, 15);
            PanelAt(stage, "BoardFrame", 57, -157, 694, 594);
            PanelAt(stage, "ControlPanel", 774, -157, 450, 594);
            board = Place(stage, "BoardCenter", 404, -452, 0, 0);
            Label(stage, "重力  ↓", 635, -171, 104, 27, 17, Quiet);

            headline = Label(stage, "", 800, -181, 400, 45, 28, White, FontStyle.Bold);
            instructions = Label(stage, "", 800, -230, 391, 61, 18, Quiet);
            mechanism = Label(stage, "", 800, -295, 394, 71, 16, Teal);
            status = Label(stage, "", 800, -371, 389, 63, 19, Amber);
            readout = Label(stage, "", 800, -437, 380, 32, 16, Quiet);

            Control(stage, "↑", 868, -480, 52, 42, () => Play(Move.Up));
            Control(stage, "←", 812, -526, 52, 42, () => Play(Move.Left));
            Control(stage, "↓", 868, -526, 52, 42, () => Play(Move.Down));
            Control(stage, "→", 924, -526, 52, 42, () => Play(Move.Right));
            Control(stage, "转房间  /  空格", 992, -493, 205, 62, () => Play(Move.Turn), true);
            undo = Control(stage, "撤销 Z", 800, -586, 96, 40, Undo);
            Control(stage, "重来 R", 902, -586, 96, 40, () => Load(index));
            Control(stage, "提示 H", 1004, -586, 96, 40, Hint);
            next = Control(stage, "下一关 ↵", 1105, -586, 99, 40, Advance, true);
            Label(stage, "关卡  ·  通关后解锁下一关", 800, -638, 390, 25, 16, Quiet);
            for (int i = 0; i < Campaign.VisibleLevels; i++)
            {
                int target = i;
                levels.Add(Control(stage, (i + 1).ToString("00"), 800 + (i % 10) * 40, -665 - (i / 10) * 34, 36, 30,
                    () => Load(target), false, 16));
            }
        }

        void Load(int target)
        {
            if (index != target) GameSession.LastCompletedLevel = -1;
            StopAllCoroutines();
            index = target;
            history.Clear();
            busy = won = false;
            position = Puzzle.Begin(Current);
            headline.text = index >= Campaign.VisibleLevels
                ? "EX  " + Current.Name
                : (index + 1).ToString("00") + "  " + Current.Name;
            instructions.text = Current.Hint;
            if (geometry != null)
            {
                geometry.gameObject.SetActive(false);
                Destroy(geometry.gameObject);
            }
            geometry = NewRect("Room", board);
            cell = Mathf.Min(100, 510 / Mathf.Sqrt(Current.Width * Current.Width + Current.Height * Current.Height));
            gate = pearl = sensor = null;
            for (int p = 0; p < Current.Tiles.Length; p++)
            {
                char tile = Current.Tiles[p];
                if (tile == '#')
                {
                    Cell("Tile " + p, p, cell + 1, new Color32(32, 42, 52, 255));
                    if (rockSprite != null) Cell("Rock " + p, p, cell, new Color32(188, 202, 232, 255), rockSprite);
                }
                else
                    Cell("Tile " + p, p, cell - 2, new Color32(16, 29, 38, 255));
                if (tile == 'E')
                {
                    var exitTile = Cell("Exit", p, cell * .78f, new Color32(196, 148, 82, 255));
                    var hole = ImageOn(exitTile, "Exit Hole", new Color32(26, 34, 44, 255), rounded).rectTransform;
                    hole.sizeDelta = new Vector2(cell * .40f, cell * .40f);
                }
                if (tile == 'D') gate = Cell("Gate", p, cell - 12, Amber).GetComponent<Image>();
                if (tile == 'S') sensor = Cell("Sensor", p, cell * .54f, Teal).GetComponent<Image>();
                if (tile == 'K') pearl = Cell("Pearl", p, cell * .32f, Gold).GetComponent<Image>();
            }
            waterFrame = foamFrame = -1;
            if (foamFrames.Length > 0)
            {
                float foamSize = (cell + 2) * 1.5f;
                foams = new RectTransform[position.Water.Length];
                foamImages = new Image[foams.Length];
                for (int i = 0; i < foams.Length; i++)
                {
                    var rt = ImageOn(geometry, "Foam " + i, Color.white, foamFrames[0]).rectTransform;
                    rt.sizeDelta = new Vector2(foamSize, foamSize);
                    rt.anchoredPosition = Point(position.Water[i]);
                    foams[i] = rt;
                    foamImages[i] = rt.GetComponent<Image>();
                }
            }
            else
            {
                foams = new RectTransform[0];
                foamImages = new Image[0];
            }
            waters = new RectTransform[position.Water.Length];
            waterImages = new Image[waters.Length];
            waterFrame = -1;
            bool artWater = waterFrames.Length > 0;
            for (int i = 0; i < waters.Length; i++)
            {
                waters[i] = artWater
                    ? Cell("Water " + i, position.Water[i], cell + 2, Color.white, waterFrames[0])
                    : Cell("Water " + i, position.Water[i], cell - 11, new Color32(48, 166, 180, 220));
                waterImages[i] = waters[i].GetComponent<Image>();
            }
            for (int p = 0; p < Current.Tiles.Length; p++)
            {
                if (Current.Tiles[p] != 'G') continue;
                var grate = Cell("Fish grate", p, cell * .75f, new Color32(101, 122, 140, 160));
                for (int i = -1; i <= 1; i++)
                {
                    var bar = ImageOn(grate, "Grate Bar", Quiet, rounded).rectTransform;
                    bar.sizeDelta = new Vector2(4, cell * .65f);
                    bar.anchoredPosition = new Vector2(i * cell * .18f, 0);
                }
            }
            if (position.Box >= 0)
            {
                crate = woodSprite == null
                    ? Cell("Crate", position.Box, cell * .61f, new Color32(166, 108, 67, 255))
                    : Cell("Crate", position.Box, cell * .84f, Color.white, woodSprite);
                if (woodSprite == null)
                {
                    var brace = ImageOn(crate, "Crate Brace", Amber, rounded).rectTransform;
                    brace.sizeDelta = new Vector2(cell * .57f, 5);
                    brace.localRotation = Quaternion.Euler(0, 0, 45);
                }
            }
            else crate = null;
            fish = Cell("Fish", position.Fish, cell * .72f, White, fishSprite);
            Paint(position, position, position.Turn * 90f, 1);
            status.text = "把鱼送到金色出口。";
            Refresh();
        }

        void Play(Move move)
        {
            if (won || busy) return;
            Position result = Puzzle.Apply(Current, position, move, out Block block);
            if (result == null)
            {
                status.text = block == Block.Grate ? "这一步会让鱼穿过栅栏。换一格水再试。" :
                    block == Block.Crate ? "箱子前面没有可以推入的位置。" : "鱼只能进入相邻的水格。";
                return;
            }
            StartCoroutine(Animate(move, result));
        }

        IEnumerator Animate(Move move, Position result)
        {
            busy = true;
            Position source = position;
            float angle = source.Turn * 90;
            if (move == Move.Turn)
            {
                for (float t = 0; t < .19f; t += Time.unscaledDeltaTime)
                {
                    Paint(source, source, angle + 90 * Mathf.SmoothStep(0, 1, t / .19f), 0);
                    yield return null;
                }
                angle += 90;
            }
            for (float t = 0; t < .16f; t += Time.unscaledDeltaTime)
            {
                Paint(source, result, angle, Mathf.SmoothStep(0, 1, t / .16f));
                yield return null;
            }
            history.Push(source);
            position = result;
            won = Puzzle.Won(Current, result);
            busy = false;
            if (won)
            {
                GameSession.LastCompletedLevel = index;
                if (index == 0)
                {
                    PlayerPrefs.SetInt(GameSession.HiddenUnlockedKey, 1);
                    PlayerPrefs.SetInt(GameSession.HubPearlKey, 1);
                }
                if (index >= Campaign.VisibleLevels)
                    PlayerPrefs.SetInt(GameSession.HiddenClearedKey, 1);
                PlayerPrefs.Save();
                status.text = index >= Campaign.VisibleLevels
                    ? "隐藏关完成！回声沉入水底。按 Esc 回到潮汐洞穴。"
                    : index == Campaign.VisibleLevels - 1
                        ? "通关！全部二十关已完成。按 Esc 回到潮汐洞穴。"
                        : "到达出口，已解锁下一关。按 Esc 回到潮汐洞穴。";
            }
            else
                status.text =
                    !source.Pearl && result.Pearl ? "已收集珍珠。现在可以交付了。" :
                    !source.Gate && result.Gate ? "感应格进水，闸门已打开。" :
                    result.Fish == Current.Exit ? "先收集珍珠，出口才会开启。" :
                    source.Box != result.Box ? "箱子已移位，留意新的水路。" : "水已落定。";
            if (won && index == unlocked && index < Campaign.VisibleLevels - 1)
            {
                unlocked++;
                PlayerPrefs.SetInt(Progress, unlocked);
                PlayerPrefs.Save();
            }
            Paint(result, result, result.Turn * 90f, 1);
            Refresh();
        }

        void Paint(Position source, Position target, float angle, float fraction)
        {
            geometry.localRotation = Quaternion.Euler(0, 0, -angle);
            for (int i = 0; i < waters.Length; i++)
            {
                waters[i].anchoredPosition = Vector2.Lerp(Point(source.Water[i]), Point(target.Water[i]), fraction);
                if (i < foams.Length) foams[i].anchoredPosition = waters[i].anchoredPosition;
            }
            if (crate != null)
                crate.anchoredPosition = Vector2.Lerp(Point(source.Box), Point(target.Box), fraction);
            fish.anchoredPosition = Vector2.Lerp(Point(source.Fish), Point(target.Fish), fraction);
            fish.localRotation = Quaternion.Euler(0, 0, angle);
            if (gate != null) gate.color = target.Gate ? new Color32(255, 193, 124, 53) : Amber;
            if (sensor != null) sensor.color = target.Gate ? Teal : new Color32(55, 120, 124, 255);
            if (pearl != null) pearl.enabled = !target.Pearl;
        }

        void Refresh()
        {
            string gateState = Current.Sensor < 0 ? "" : "   ·   闸门" + (position.Gate ? "开" : "关");
            string pearlState = Current.Pearl < 0 ? "" : "   ·   珍珠 " + (position.Pearl ? "1 / 1" : "0 / 1");
            readout.text = "步数 " + history.Count + gateState + pearlState;
            mechanism.text = Campaign.Chapters[Campaign.Chapter(index)] +
                (Current.Sensor < 0 ? "" : "   ·   青色为感应格") +
                (Current.Pearl < 0 ? "" : "   ·   金色小格为珍珠");
            undo.interactable = history.Count > 0;
            next.gameObject.SetActive(won && index + 1 < Campaign.VisibleLevels);
            for (int i = 0; i < levels.Count; i++)
            {
                levels[i].interactable = i <= unlocked;
                levels[i].targetGraphic.color = i == index ? new Color32(60, 118, 121, 255) : Slate;
            }
        }

        void Advance()
        {
            if (won && index + 1 < Campaign.VisibleLevels) Load(index + 1);
        }

        void ReturnToHub()
        {
            GameSession.EnterHub();
        }

#if UNITY_EDITOR
        public void DebugPlayRoute(string route)
        {
            StartCoroutine(DebugRoute(route));
        }

        IEnumerator DebugRoute(string route)
        {
            foreach (char c in route)
            {
                Play((Move)(c - '0'));
                yield return new WaitForSeconds(.5f);
            }
        }
#endif

        void Undo()
        {
            if (busy || history.Count == 0) return;
            position = history.Pop();
            won = false;
            status.text = "已恢复到上一步。";
            Paint(position, position, position.Turn * 90f, 1);
            Refresh();
        }

        void Hint()
        {
            if (busy || won) return;
            string route = Puzzle.Solve(Current, position);
            status.text = route == null ? "这个局面走不到出口。撤销一步，试试另一条路线。" :
                "下一步可以" + new[] {"向上游。","向右游。","向下游。","向左游。","转动房间。"}[route[0] - '0'];
        }

        Vector2 Point(int p) => new Vector2((p % Current.Width - (Current.Width - 1) * .5f) * cell,
            ((Current.Height - 1) * .5f - p / Current.Width) * cell);

        RectTransform Cell(string name, int p, float size, Color color, Sprite sprite = null)
        {
            var rt = ImageOn(geometry, name, color, sprite == null ? rounded : sprite).rectTransform;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Point(p);
            return rt;
        }

        static RectTransform NewRect(string name, RectTransform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        static RectTransform Place(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var rt = NewRect(name, parent);
            if (name == "Layout")
            {
                rt.sizeDelta = new Vector2(w, h);
                return rt;
            }
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Image ImageOn(RectTransform parent, string name, Color color, Sprite sprite)
        {
            var image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        void PanelAt(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var image = Place(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = Panel;
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
        }

        Text Label(RectTransform parent, string content, float x, float y, float w, float h, int size, Color color,
            FontStyle style = FontStyle.Normal)
        {
            var text = Place(parent, content == "" ? "Text" : content, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.raycastTarget = false;
            return text;
        }

        Button Control(RectTransform parent, string title, float x, float y, float w, float h,
            UnityEngine.Events.UnityAction action, bool accent = false, int size = 17)
        {
            var rect = Place(parent, title, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = accent ? Teal : Slate;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            var label = Label(rect, title, 0, 0, w, h, size, accent ? Ink : White, FontStyle.Bold);
            label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(action);
            return button;
        }

        static Sprite MakeSprite(bool fish)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (fish)
                {
                    float u = (x - 35) / 21f, v = (y - 31) / 13f;
                    bool body = u * u + v * v <= 1;
                    bool tail = x >= 8 && x <= 19 && Mathf.Abs(y - 31) < (21 - x) * .84f;
                    if (body || tail) colors[y * size + x] = y >= 30 ? new Color32(255, 207, 146, 255) : new Color32(243, 154, 102, 255);
                    if ((x - 46) * (x - 46) + (y - 34) * (y - 34) <= 5)
                        colors[y * size + x] = new Color32(19, 33, 40, 255);
                    continue;
                }
                float cornerX = Mathf.Max(0, Mathf.Abs(x - 31.5f) - 24);
                float cornerY = Mathf.Max(0, Mathf.Abs(y - 31.5f) - 24);
                if (cornerX * cornerX + cornerY * cornerY < 59) colors[y * size + x] = new Color32(255,255,255,255);
            }
            texture.SetPixels32(colors);
            texture.Apply();
            var border = fish ? Vector4.zero : new Vector4(9, 9, 9, 9);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
        }
    }
}

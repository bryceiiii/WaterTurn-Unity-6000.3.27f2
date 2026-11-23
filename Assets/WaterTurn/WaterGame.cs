using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// Runs one chamber. The board it plays on is already built in the scene from
    /// prefab instances (see <see cref="LevelBoard"/>); this component only moves
    /// them and drives the HUD, so the puzzle rules in <see cref="Puzzle"/> stay
    /// untouched.
    /// </summary>
    public sealed class WaterGame : MonoBehaviour
    {
        const string Progress = "WaterTurn.ChapterProgress.v1";
        static readonly Color Slate = new Color32(39, 59, 69, 255);
        static readonly Color Teal = new Color32(120, 239, 234, 255);
        static readonly Color Amber = new Color32(255, 193, 124, 255);

        GameHud hud;
        LevelDefinition definition;
        LevelBoard boardView;
        Level current;
        Sprite[] fishSwim;
        Image fishImage;
        Sprite waterGlowSprite;
        RectTransform waterGlowLayer;
        Image[] waterGlows;
        float fishAnim;
        int fishFrame = -1;
        Sprite[] waterFrames, foamFrames;
        Sprite[][] waterVariants;
        int[] waterMasks;
        Button next, undo, home;
        readonly List<Button> levels = new List<Button>();
        readonly Stack<Position> history = new Stack<Position>();
        RectTransform[] waters, foams;
        Image[] waterImages, foamImages;
        RectTransform fish, crate, geometry;
        Image gate, pearl, sensor;
        int index, unlocked, waterFrame = -1, foamFrame = -1;
        float cell;
        bool busy, won;
        Position position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.StartsWith("Level_")) return;
            if (FindFirstObjectByType<WaterGame>() == null)
                new GameObject("WaterTurn").AddComponent<WaterGame>();
        }

        void Awake()
        {
            definition = FindFirstObjectByType<LevelDefinition>();
            boardView = FindFirstObjectByType<LevelBoard>();
            hud = FindFirstObjectByType<GameHud>();
            if (definition == null || boardView == null || hud == null)
            {
                Debug.LogError("WaterTurn: 关卡场景缺少 LevelDefinition / LevelBoard / GameHud，无法启动。");
                enabled = false;
                return;
            }

            unlocked = Mathf.Clamp(PlayerPrefs.GetInt(Progress, 0), 0, Campaign.VisibleLevels - 1);
            var font = Font.CreateDynamicFontFromOSFont(
                new[] {"Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial"}, 28);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud.ApplySkin(font, MakeRounded());
            fishSwim = FishArt.Swim(FishFacing.Right);
            LoadArt();
            WireHud();
            Load();
        }

        void WireHud()
        {
            home = hud.home;
            next = hud.next;
            undo = hud.undo;

            home.onClick.AddListener(ReturnToHub);
            next.onClick.AddListener(Advance);
            undo.onClick.AddListener(Undo);
            hud.restart.onClick.AddListener(Restart);
            hud.hint.onClick.AddListener(Hint);
            hud.up.onClick.AddListener(() => Play(Move.Up));
            hud.down.onClick.AddListener(() => Play(Move.Down));
            hud.left.onClick.AddListener(() => Play(Move.Left));
            hud.right.onClick.AddListener(() => Play(Move.Right));
            hud.turn.onClick.AddListener(() => Play(Move.Turn));

            if (hud.levelButtons == null) return;
            for (int i = 0; i < hud.levelButtons.Length; i++)
            {
                int target = i;
                var button = hud.levelButtons[i];
                if (button == null) continue;
                levels.Add(button);
                button.onClick.AddListener(() => LevelManager.Instance.LoadLevel(target));
            }
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
            BuildWaterVariants();
            waterGlowSprite = MakeWaterGlow();
            var foam = new List<Sprite>();
            for (int i = 0; i < 8; i++)
            {
                var frame = Resources.Load<Sprite>("Art/Water/foam_" + i);
                if (frame != null) foam.Add(frame);
            }
            foamFrames = foam.ToArray();
        }

        void BuildWaterVariants()
        {
            waterVariants = new Sprite[waterFrames.Length][];
            for (int frame = 0; frame < waterFrames.Length; frame++)
            {
                var texture = waterFrames[frame] != null ? waterFrames[frame].texture : null;
                if (texture == null) continue;
                Color32[] source = texture.GetPixels32();
                int width = texture.width;
                int height = texture.height;
                waterVariants[frame] = new Sprite[16];
                for (int mask = 0; mask < 16; mask++)
                {
                    var edgeTexture = MakeWaterEdgeTexture(source, width, height, mask);
                    waterVariants[frame][mask] = Sprite.Create(edgeTexture,
                        new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 100f);
                }
            }
        }

        static Texture2D MakeWaterEdgeTexture(Color32[] source, int sourceWidth, int sourceHeight, int open)
        {
            const int size = 64;
            const float edge = .16f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size;
                    float v = (y + .5f) / size;
                    float alpha = 1f;
                    if ((open & 1) != 0) alpha *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - v) / edge));
                    if ((open & 2) != 0) alpha *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / edge));
                    if ((open & 4) != 0) alpha *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / edge));
                    if ((open & 8) != 0) alpha *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - u) / edge));

                    int sx = Mathf.Clamp(Mathf.FloorToInt(u * sourceWidth), 0, sourceWidth - 1);
                    int sy = Mathf.Clamp(Mathf.FloorToInt(v * sourceHeight), 0, sourceHeight - 1);
                    Color32 color = source[sy * sourceWidth + sx];
                    float sourceAlpha = color.a / 255f;
                    pixels[y * size + x] = new Color32(color.r, color.g, color.b,
                        (byte)(Mathf.Clamp01(alpha * sourceAlpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        int WaterMask(int index, int[] water)
        {
            int width = current.Width;
            int count = current.Width * current.Height;
            bool Has(int cell) => cell >= 0 && cell < count && System.Array.IndexOf(water, cell) >= 0;

            int mask = 0;
            if (!Has(index - width)) mask |= 1;   // open top
            if (!Has(index + width)) mask |= 2;   // open bottom
            if (!Has(index - 1) || index % width == 0) mask |= 4;    // open left
            if (!Has(index + 1) || index % width == width - 1) mask |= 8; // open right
            return mask;
        }

        void CreateWaterGlowLayer()
        {
            if (geometry == null || waters == null || waters.Length == 0 || waterGlowSprite == null)
                return;

            var layerObject = new GameObject("WaterGlow", typeof(RectTransform));
            waterGlowLayer = layerObject.GetComponent<RectTransform>();
            waterGlowLayer.SetParent(geometry, false);
            waterGlowLayer.anchorMin = waterGlowLayer.anchorMax = new Vector2(.5f, .5f);
            waterGlowLayer.pivot = new Vector2(.5f, .5f);
            waterGlowLayer.sizeDelta = new Vector2(current.Width * cell, current.Height * cell);
            waterGlowLayer.anchoredPosition = Vector2.zero;

            int firstWater = int.MaxValue;
            foreach (var water in waters)
                if (water != null) firstWater = Mathf.Min(firstWater, water.GetSiblingIndex());
            if (firstWater != int.MaxValue)
                waterGlowLayer.SetSiblingIndex(firstWater);

            waterGlows = new Image[waters.Length];
            for (int i = 0; i < waters.Length; i++)
            {
                if (waters[i] == null) continue;
                var glowObject = new GameObject("Glow " + i,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = glowObject.GetComponent<RectTransform>();
                rect.SetParent(waterGlowLayer, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(cell * 1.55f, cell * 1.55f);
                rect.anchoredPosition = waters[i].anchoredPosition;

                var image = glowObject.GetComponent<Image>();
                image.sprite = waterGlowSprite;
                image.color = new Color(0.10f, 0.82f, 0.88f, 0.10f);
                image.raycastTarget = false;
                waterGlows[i] = image;
            }
        }

        void AnimateWater()
        {
            int tick = Mathf.FloorToInt(Time.unscaledTime * 7f);
            if (waterVariants != null && waterVariants.Length > 0 && waterMasks != null &&
                waterImages != null)
            {
                waterFrame = tick % waterVariants.Length;
                var variants = waterVariants[waterFrame];
                for (int i = 0; i < waterImages.Length; i++)
                {
                    if (waterImages[i] == null) continue;
                    int mask = i < waterMasks.Length ? waterMasks[i] : 0;
                    var sprite = variants != null && mask >= 0 && mask < variants.Length
                        ? variants[mask] : null;
                    if (sprite != null) waterImages[i].sprite = sprite;
                }
            }
            else if (waterFrames.Length > 0 && waterImages != null && tick % waterFrames.Length != waterFrame)
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

        void AnimateFish()
        {
            if (fishImage == null) return;
            fishAnim += Time.unscaledDeltaTime;
            int frame = FishArt.FrameAt(fishAnim);
            if (frame == fishFrame) return;
            fishFrame = frame;
            if (fishSwim != null && fishSwim.Length > 0)
                fishImage.sprite = fishSwim[Mathf.Clamp(frame, 0, fishSwim.Length - 1)];
        }

        void Update()
        {
            AnimateWater();
            AnimateFish();
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { ReturnToHub(); return; }
            if (keyboard.rKey.wasPressedThisFrame) { Restart(); return; }
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

        /// <summary>Bind the puzzle to the board that is already in the scene.</summary>
        void Load()
        {
            current = definition.ToLevel();
            index = definition.levelIndex;
            history.Clear();
            busy = won = false;
            position = Puzzle.Begin(current);

            SetHeader();
            if (position == null)
            {
                Debug.LogError("WaterTurn: 关卡数据无效 —— " + definition.levelName);
                return;
            }

            geometry = boardView.room;
            cell = boardView.cell;
            waters = boardView.water ?? new RectTransform[0];
            foams = boardView.foam ?? new RectTransform[0];
            waterMasks = new int[waters.Length];
            waterImages = new Image[waters.Length];
            for (int i = 0; i < waters.Length; i++)
            {
                waterImages[i] = waters[i].GetComponent<Image>();
                if (waterImages[i] != null)
                    waterImages[i].color = new Color(0.88f, 0.98f, 0.99f, 0.94f);
            }
            foamImages = new Image[foams.Length];
            for (int i = 0; i < foams.Length; i++)
            {
                foamImages[i] = foams[i].GetComponent<Image>();
                // The old foam frame was a full 153px rounded square around
                // every single water cell, so adjacent cells showed a white
                // lattice and each splash overlapped its neighbours. The water
                // itself is a seamless tile; keep the surface clean and let the
                // animated water texture carry the motion.
                if (foams[i] != null) foams[i].gameObject.SetActive(false);
            }
            CreateWaterGlowLayer();

            fish = boardView.player;
            fishImage = fish != null ? fish.GetComponent<Image>() : null;
            crate = boardView.crate;
            gate = boardView.gate;
            pearl = boardView.pearl;
            sensor = boardView.sensor;
            if (fishImage != null) fishImage.preserveAspect = true;

            waterFrame = foamFrame = -1;
            Paint(position, position, position.Turn * 90f, 1);
            hud.status.text = "把鱼送到金色出口。";
            Refresh();
        }

        void SetHeader()
        {
            hud.headline.text = index >= Campaign.VisibleLevels
                ? "EX  " + current.Name
                : (index + 1).ToString("00") + "  " + current.Name;
            hud.instructions.text = current.Hint;
        }

        void Play(Move move)
        {
            if (won || busy || position == null) return;
            Position result = Puzzle.Apply(current, position, move, out Block block);
            if (result == null)
            {
                hud.status.text = block == Block.Grate ? "这一步会让鱼穿过栅栏。换一格水再试。" :
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
            won = Puzzle.Won(current, result);
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
                hud.status.text = index >= Campaign.VisibleLevels
                    ? "隐藏关完成！回声沉入水底。按 Esc 回到潮汐洞穴。"
                    : index == Campaign.VisibleLevels - 1
                        ? "通关！全部二十关已完成。按 Esc 回到潮汐洞穴。"
                        : "到达出口，已解锁下一关。按 Esc 回到潮汐洞穴。";
            }
            else
                hud.status.text =
                    !source.Pearl && result.Pearl ? "已收集珍珠。现在可以交付了。" :
                    !source.Gate && result.Gate ? "感应格进水，闸门已打开。" :
                    result.Fish == current.Exit ? "先收集珍珠，出口才会开启。" :
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
            if (geometry != null) geometry.localRotation = Quaternion.Euler(0, 0, -angle);
            for (int i = 0; i < waters.Length; i++)
            {
                waters[i].anchoredPosition = Vector2.Lerp(Point(source.Water[i]), Point(target.Water[i]), fraction);
                if (waterMasks != null && i < waterMasks.Length)
                    waterMasks[i] = WaterMask(target.Water[i], target.Water);
                if (i < foams.Length) foams[i].anchoredPosition = waters[i].anchoredPosition;
                if (waterGlows != null && i < waterGlows.Length && waterGlows[i] != null)
                    waterGlows[i].rectTransform.anchoredPosition = waters[i].anchoredPosition;
            }
            if (crate != null && source.Box >= 0 && target.Box >= 0)
                crate.anchoredPosition = Vector2.Lerp(Point(source.Box), Point(target.Box), fraction);
            if (fish != null)
            {
                fish.anchoredPosition = Vector2.Lerp(Point(source.Fish), Point(target.Fish), fraction);
                fish.localRotation = Quaternion.Euler(0, 0, angle);
            }
            if (gate != null) gate.color = target.Gate ? new Color32(255, 193, 124, 53) : Amber;
            if (sensor != null) sensor.color = target.Gate ? Teal : new Color32(55, 120, 124, 255);
            if (pearl != null) pearl.enabled = !target.Pearl;
        }

        void Refresh()
        {
            string gateState = current.Sensor < 0 ? "" : "   ·   闸门" + (position.Gate ? "开" : "关");
            string pearlState = current.Pearl < 0 ? "" : "   ·   珍珠 " + (position.Pearl ? "1 / 1" : "0 / 1");
            hud.readout.text = "步数 " + history.Count + gateState + pearlState;
            hud.mechanism.text = Campaign.Chapters[Campaign.Chapter(index)] +
                (current.Sensor < 0 ? "" : "   ·   青色为感应格") +
                (current.Pearl < 0 ? "" : "   ·   金色小格为珍珠");
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
            if (!won || index + 1 >= Campaign.VisibleLevels) return;
            LevelManager.Instance.LoadLevel(index + 1);
        }

        void Restart()
        {
            if (LevelManager.Instance != null) LevelManager.Instance.RestartLevel();
            else Load();
        }

        void ReturnToHub()
        {
            GameSession.EnterHub(index);
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
            hud.status.text = "已恢复到上一步。";
            Paint(position, position, position.Turn * 90f, 1);
            Refresh();
        }

        void Hint()
        {
            if (busy || won) return;
            string route = Puzzle.Solve(current, position);
            hud.status.text = route == null ? "这个局面走不到出口。撤销一步，试试另一条路线。" :
                "下一步可以" + new[] {"向上游。","向右游。","向下游。","向左游。","转动房间。"}[route[0] - '0'];
        }

        Vector2 Point(int p) => new Vector2((p % current.Width - (current.Width - 1) * .5f) * cell,
            ((current.Height - 1) * .5f - p / current.Width) * cell);

        static Sprite MakeWaterGlow()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + .5f) / size * 2f - 1f;
                    float ny = (y + .5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    colors[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(colors);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        }

        static Sprite MakeRounded()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cornerX = Mathf.Max(0, Mathf.Abs(x - 31.5f) - 24);
                float cornerY = Mathf.Max(0, Mathf.Abs(y - 31.5f) - 24);
                if (cornerX * cornerX + cornerY * cornerY < 59) colors[y * size + x] = new Color32(255,255,255,255);
            }
            texture.SetPixels32(colors);
            texture.Apply();
            var border = new Vector4(9, 9, 9, 9);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
        }
    }
}

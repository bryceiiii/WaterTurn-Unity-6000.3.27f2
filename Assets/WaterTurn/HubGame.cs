using System;
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
    /// <summary>
    /// The tidal cave that sits in front of the campaign.  It is a real scene
    /// (WaterTurnHub) built from a hand-authored network of flooded mine
    /// tunnels: five chambers joined by winding passages, two sealed stone
    /// doors, dry side pockets, and three shrine nodes.  The fish swims inside
    /// the water tunnels; the dry pockets and the surrounding rock are scenery.
    /// Pressing E at a shrine hands the chosen level index to GameSession.
    /// </summary>
    public sealed class HubGame : MonoBehaviour
    {
        const int W = 45, H = 27;
        const float Cell = 28f;
        const float FishRadius = 9.5f;
        const float SwimSpeed = 148f;
        const float SwimAcceleration = 900f;
        const float SwimDrag = 700f;
        [Header("Camera Authoring")]
        [Tooltip("Default zoom when no saved value exists.")]
        public float defaultZoom = 1.2f;
        public float minZoom = 1.06f;
        public float maxZoom = 2.2f;
        [Tooltip("Positive moves the fish down on screen; use it to leave room for the HUD.")]
        public float cameraBiasY = -80f;
        public float followSpeed = 7f;
        public float zoomSpeed = 12f;
        public Vector2 viewSize = new Vector2(1280f, 800f);

        [Header("Scene Authoring")]
        [Tooltip("Use HubPortalAnchor objects in the scene instead of the hard-coded 20 entrances.")]
        public bool useSceneAnchors = true;

        const string ZoomKey = "WaterTurn.HubZoom.v1";
        float cameraZoom, zoomTarget;

        HubPuzzle puzzle;

        static readonly Color Ink = new Color32(20, 12, 13, 255);
        static readonly Color Panel = new Color32(30, 20, 19, 238);
        static readonly Color Slate = new Color32(72, 49, 43, 255);
        static readonly Color White = new Color32(246, 232, 211, 255);
        static readonly Color Quiet = new Color32(190, 161, 134, 255);
        static readonly Color Teal = new Color32(118, 226, 232, 255);
        static readonly Color Amber = new Color32(255, 196, 120, 255);
        static readonly Color Gold = new Color32(250, 209, 130, 255);
        static readonly Color Kelp = new Color32(112, 220, 156, 255);
        static readonly Color ColdKelp = new Color32(108, 182, 255, 255);
        static readonly Color HideKelp = new Color32(255, 199, 104, 255);
        static readonly Color Dormant = new Color32(92, 105, 84, 255);

        Font font;
        Sprite rounded, solid, ellipse, leafSprite, glowSprite, ringSprite;
        Sprite pearlSprite, orbSprite, bubbleSprite, vignetteSprite, shaftSprite;
        Sprite[] fishSwim;
        Sprite[] waterFrames;
        Sprite floorSprite, shrineSprite, rockPropSprite, potSprite, statueSprite, crystalFormationSprite;
        Texture2D rockTexture, propsTexture, grafxTexture, crystalsTexture;

        RectTransform canvasRoot, mapRoot, fishRoot, fishImage, pearlRoot, promptRoot, toastRoot;
        Image fade, pearlGlow, fishGlow, tunnelImage, fishSpriteImage;
        Sprite[][] waterTileVariants;
        int waterTileFrame = -1;
        Text objective, progress, promptText, promptKey, toastText, zoomReadout, statusText;
        CanvasGroup promptGroup, toastGroup;

        RectTransform waterRoot, valveRoot;
        readonly List<WaterTile> waterTiles = new List<WaterTile>();
        readonly List<ValveView> valveViews = new List<ValveView>();
        readonly bool[,] reachable = new bool[W, H];
        ValveView hoveredValve, keyboardValve, focusedValve, hintValve;
        float hintTimer;
        int turnCount;
        readonly Stack<HubSnapshot> undoStack = new Stack<HubSnapshot>();
        float valveClock;
        float valveRotateDuration = .30f;
        bool rotatingValve;
        readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
        PointerEventData pointerEvent;

        readonly List<Portal> portals = new List<Portal>();
        readonly List<Gate> gates = new List<Gate>();
        readonly List<Bubble> bubbles = new List<Bubble>();

        Vector2 fishPos, fishVel;
        float fishBob, bubbleClock, fishAnim;
        FishFacing fishFacing = FishFacing.Right, fishShown = (FishFacing)(-1);
        int fishFrame = -1;
        bool pearlTaken, level1Cleared, hiddenUnlocked, hiddenCleared, traveling;
        bool justCompletedFirst, gatesStartOpen;
        float toastTimer, promptAlpha;
        Portal nearest;
        float swayClock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SceneManager.GetActiveScene().name != GameSession.HubScene) return;
            if (FindFirstObjectByType<HubGame>() == null)
                new GameObject("TidalHollow").AddComponent<HubGame>();
        }

        void Awake()
        {
            cameraZoom = zoomTarget = Mathf.Clamp(
                PlayerPrefs.GetFloat(ZoomKey, defaultZoom), minZoom, maxZoom);
            level1Cleared = GameSession.LevelsCleared >= 1;
            hiddenUnlocked = GameSession.HiddenUnlocked;
            hiddenCleared = GameSession.HiddenCleared;
            pearlTaken = GameSession.HubPearlTaken;
            justCompletedFirst = GameSession.LastCompletedLevel == 0;
            bool justCompletedHidden = GameSession.LastCompletedLevel >= Campaign.VisibleLevels;
            GameSession.LastCompletedLevel = -1;
            int returnLevel = GameSession.ReturnLevel;
            GameSession.ReturnLevel = -1;
            gatesStartOpen = level1Cleared && !justCompletedFirst;
            puzzle = new HubPuzzle();
            BuildArt();
            BuildInterface();
            BuildMap();
            ResetFish(returnLevel);
            RefreshWaterTiles();
            SnapCamera();
            RefreshHud();
            // A level→cave trip arrives behind the shared iris, so the hub must
            // not also fade from black on top of it.
            if (!WaterFx.PendingIrisOpen) StartCoroutine(FadeInRoutine());
            if (justCompletedFirst) StartCoroutine(RevealDoorsOnReturn());
            else if (justCompletedHidden) StartCoroutine(ShowHiddenReturn());
            else StartCoroutine(ShowPuzzleIntro());
        }

        IEnumerator ShowPuzzleIntro()
        {
            yield return new WaitForSeconds(1.0f);
            ShowToast("鼠标指向水块按 Space（或左键）转 90°   ·   Z 撤销   ·   H 提示   ·   R 重开", 5.2f);
        }

        // ------------------------------------------------------------------ art

        void BuildArt()
        {
            font = Font.CreateDynamicFontFromOSFont(
                new[] {"Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial"}, 28);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rounded = MakeRounded();
            solid = MakeSolid();
            ellipse = MakeEllipse(64);
            fishSwim = FishArt.Swim(FishFacing.Right);
            leafSprite = MakeLeaf();
            glowSprite = MakeGlow(128);
            ringSprite = MakeRing(128, 0.045f);
            bubbleSprite = MakeRing(64, 0.10f);
            pearlSprite = MakePearl();
            orbSprite = MakeOrb();
            vignetteSprite = MakeVignette();
            shaftSprite = MakeShaft();

            rockTexture = Resources.Load<Texture2D>("Art/Cave/rock_bg");
            propsTexture = Resources.Load<Texture2D>("Art/Cave/props");
            grafxTexture = Resources.Load<Texture2D>("Art/Cave/grafx_cave");
            crystalsTexture = Resources.Load<Texture2D>("Art/Cave/crystals");
            shrineSprite = CropSprite(propsTexture, 352, 256, 96, 84);
            rockPropSprite = CropSprite(propsTexture, 0, 416, 72, 50);
            potSprite = CropSprite(propsTexture, 160, 192, 28, 40);
            statueSprite = CropSprite(propsTexture, 440, 32, 40, 64);
            floorSprite = CropSprite(grafxTexture, 80, 640, 16, 16);
            crystalFormationSprite = CropSprite(crystalsTexture, 108, 0, 54, 72);

            var water = new List<Sprite>();
            for (int i = 0; i < 5; i++)
            {
                var frame = Resources.Load<Sprite>("Art/Water/water_" + i);
                if (frame != null) water.Add(frame);
            }
            waterFrames = water.ToArray();
            BuildWaterTileVariants();
        }

        /// <summary>
        /// One sprite per animation frame and per open-edge mask.  The water is
        /// drawn as separate tiles now, so a water block can turn without the
        /// whole cave texture being rebuilt.
        /// </summary>
        void BuildWaterTileVariants()
        {
            waterTileVariants = new Sprite[waterFrames.Length][];
            for (int frame = 0; frame < waterFrames.Length; frame++)
            {
                var sprite = waterFrames[frame];
                var texture = sprite != null ? sprite.texture : null;
                if (texture == null) continue;
                Color32[] source = texture.GetPixels32();
                int width = texture.width;
                int height = texture.height;
                var variants = new Sprite[16];
                for (int mask = 0; mask < 16; mask++)
                    variants[mask] = MakeWaterTile(source, width, height, mask);
                waterTileVariants[frame] = variants;
            }
        }

        static Sprite MakeWaterTile(Color32[] source, int sourceWidth, int sourceHeight, int open)
        {
            const int size = 64;
            const float edge = .20f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
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
                    pixels[y * size + x] = new Color32(color.r, color.g, color.b,
                        (byte)(Mathf.Clamp01(alpha * (color.a / 255f)) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        }

        // ------------------------------------------------------------- interface

        void BuildInterface()
        {
            canvasRoot = NewRect("HubCanvas", transform);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.matchWidthOrHeight = .5f;
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var backdrop = Stretch(canvasRoot, "Backdrop");
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color32(10, 6, 5, 255);
            backdropImage.raycastTarget = false;

            var frame = Box(canvasRoot, "CaveFrame", Vector2.zero, new Vector2(W * Cell + 12, H * Cell + 12), Center);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = rounded;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = new Color32(92, 60, 48, 255);

            var inner = Box(frame, "CaveFrameInner", Vector2.zero, new Vector2(W * Cell + 4, H * Cell + 4), Center);
            var innerImage = inner.gameObject.AddComponent<Image>();
            innerImage.sprite = rounded;
            innerImage.type = Image.Type.Sliced;
            innerImage.color = new Color32(22, 14, 13, 255);
            // The hub uses a scrolling map viewport now; a fixed frame would not
            // move with the camera.
            frame.gameObject.SetActive(false);

            mapRoot = Box(canvasRoot, "Cave", Vector2.zero, new Vector2(W * Cell, H * Cell), Center);

            // Ambient light and vignette, above the map but below the HUD.
            var ambient = Stretch(canvasRoot, "Ambient");
            for (int i = 0; i < 3; i++)
            {
                var shaft = Img(ambient, "Shaft " + i, shaftSprite, new Color(1f, 0.72f, 0.44f, 0.045f),
                    new Vector2(-330 + i * 330, 160), new Vector2(250, 700));
                shaft.rectTransform.localRotation = Quaternion.Euler(0, 0, -12 + i * 5);
            }
            var tint = Stretch(ambient, "WaterTint");
            var tintImage = tint.gameObject.AddComponent<Image>();
            tintImage.color = new Color(0.28f, 0.12f, 0.05f, 0.06f);
            // This layer sits above the map and covers the whole screen, so it
            // must not eat raycasts: a full-screen graphic makes
            // IsPointerOverGameObject() true everywhere and no water block can
            // ever be clicked.
            tintImage.raycastTarget = false;
            var vignette = Stretch(ambient, "Vignette");
            var vignetteImage = vignette.gameObject.AddComponent<Image>();
            vignetteImage.sprite = vignetteSprite;
            vignetteImage.color = new Color(0, 0, 0, 0.72f);
            vignetteImage.raycastTarget = false;

            var hud = Stretch(canvasRoot, "Hud");
            var topScrim = Box(hud, "TopScrim", Vector2.zero, new Vector2(1280, 220), new Vector2(0, 1));
            var topScrimImage = topScrim.gameObject.AddComponent<Image>();
            topScrimImage.sprite = MakeScrim(true);
            topScrimImage.color = new Color(0.035f, 0.018f, 0.012f, 0.95f);
            topScrimImage.raycastTarget = false;
            var bottomScrim = Box(hud, "BottomScrim", Vector2.zero, new Vector2(1280, 150), new Vector2(0, 0));
            var bottomScrimImage = bottomScrim.gameObject.AddComponent<Image>();
            bottomScrimImage.sprite = MakeScrim(false);
            bottomScrimImage.color = new Color(0.035f, 0.018f, 0.012f, 0.95f);
            bottomScrimImage.raycastTarget = false;

            var title = Label(hud, "Title", "潮汐洞穴 · 引水", 46, White, TextAnchor.MiddleLeft,
                new Vector2(42, -24), new Vector2(760, 58), FontStyle.Bold);
            title.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.65f);
            Label(hud, "Subtitle", "TIDAL HOLLOW  ·  序章 · 水块谜题", 15, Teal, TextAnchor.MiddleLeft,
                new Vector2(44, -82), new Vector2(620, 24), FontStyle.Bold);
            Label(hud, "Deck", "亮色的水与小鱼相通，暗色的水被石壁截断。转动水块，把水路接起来。", 14, Quiet,
                TextAnchor.MiddleLeft, new Vector2(44, -106), new Vector2(920, 22));

            var objectivePanel = PanelImage(hud, "ObjectivePanel", new Vector2(42, -138), new Vector2(650, 58),
                new Color32(38, 24, 20, 225));
            var accent = Box(objectivePanel.transform, "Accent", new Vector2(0, 0), new Vector2(4, 58), LeftCenter);
            accent.gameObject.AddComponent<Image>().color = Teal;
            objective = Label(objectivePanel.transform, "Objective", "", 18, White, TextAnchor.MiddleLeft,
                new Vector2(20, -7), new Vector2(612, 44));

            var progressPanel = PanelImage(hud, "ProgressPanel", new Vector2(714, -138), new Vector2(524, 58),
                new Color32(38, 24, 20, 225));
            progress = Label(progressPanel.transform, "Progress", "", 15, Quiet, TextAnchor.MiddleRight,
                new Vector2(18, -9), new Vector2(488, 40));
            progress.supportRichText = true;

            Label(hud, "Controls", "WASD / 方向键 游动   ·   Space / 左键 旋转水块   ·   Z 撤销   ·   H 提示   ·   R 重开   ·   E 海草",
                13, Quiet, TextAnchor.MiddleLeft, new Vector2(42, -734), new Vector2(880, 24));
            statusText = Label(hud, "StatusLine", "", 13, Gold, TextAnchor.MiddleLeft,
                new Vector2(42, -708), new Vector2(870, 20));
            Label(hud, "Footer", "亮色水与小鱼相通  ·  暗色水被石壁截断", 12,
                new Color32(166, 128, 105, 255), TextAnchor.MiddleRight,
                new Vector2(930, -734), new Vector2(308, 24));
            zoomReadout = Label(hud, "ZoomReadout", "", 12, new Color32(196, 158, 126, 255),
                TextAnchor.MiddleRight, new Vector2(930, -710), new Vector2(308, 20));

            promptRoot = Box(hud, "Prompt", new Vector2(385, -664), new Vector2(510, 64), new Vector2(0, 1));
            var promptBack = promptRoot.gameObject.AddComponent<Image>();
            promptBack.sprite = rounded;
            promptBack.type = Image.Type.Sliced;
            promptBack.color = new Color32(46, 29, 24, 245);
            promptGroup = promptRoot.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0;
            promptGroup.interactable = false;
            promptGroup.blocksRaycasts = false;
            var keyCap = Box(promptRoot, "KeyCap", new Vector2(20, -13), new Vector2(38, 38), new Vector2(0, 1));
            var keyImage = keyCap.gameObject.AddComponent<Image>();
            keyImage.sprite = rounded;
            keyImage.type = Image.Type.Sliced;
            keyImage.color = Teal;
            promptKey = CenterLabel(keyCap, "Key", "E", 20, Ink, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(38, 38), FontStyle.Bold);
            promptText = Label(promptRoot, "PromptText", "", 17, White, TextAnchor.MiddleLeft,
                new Vector2(72, -8), new Vector2(418, 48));

            toastRoot = Box(hud, "Toast", new Vector2(315, -208), new Vector2(650, 48), new Vector2(0, 1));
            var toastBack = toastRoot.gameObject.AddComponent<Image>();
            toastBack.sprite = rounded;
            toastBack.type = Image.Type.Sliced;
            toastBack.color = new Color32(44, 27, 21, 238);
            toastGroup = toastRoot.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0;
            toastGroup.interactable = false;
            toastGroup.blocksRaycasts = false;
            toastText = CenterLabel(toastRoot, "ToastText", "", 16, Gold, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(610, 40));

            var fadeRect = Stretch(canvasRoot, "Fade");
            fade = fadeRect.gameObject.AddComponent<Image>();
            fade.color = new Color32(10, 5, 4, 0);
            fade.raycastTarget = false;
            fadeRect.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ map

        void BuildMap()
        {
            AddBackground();
            AddTunnelCanvas();
            AddProps();
            AddStartMarker();
            AddValveViews();

            BuildPortals();

            AddGates();
            AddPearl();

            fishRoot = Box(mapRoot, "Fish", CellPosition(22, 16), new Vector2(40, 40), Center);
            fishGlow = Img(fishRoot, "FishGlow", glowSprite, new Color(0.42f, 0.95f, 0.90f, 0.20f),
                Vector2.zero, new Vector2(64, 64));
            fishImage = Box(fishRoot, "FishImage", Vector2.zero, new Vector2(34, 34), Center);
            var fishRectImage = fishImage.gameObject.AddComponent<Image>();
            fishRectImage.sprite = fishSwim != null && fishSwim.Length > 0 ? fishSwim[0] : null;
            fishRectImage.preserveAspect = true;
            fishRectImage.raycastTarget = false;
            fishSpriteImage = fishRectImage;

            AddBubbleVents();
        }

        void AddBackground()
        {
            var bgGo = new GameObject("RockBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            bgGo.transform.SetParent(mapRoot, false);
            var bg = (RectTransform)bgGo.transform;
            bg.anchorMin = bg.anchorMax = Center;
            bg.pivot = Center;
            bg.sizeDelta = new Vector2(W * Cell, H * Cell);
            var raw = bgGo.GetComponent<RawImage>();
            raw.texture = rockTexture;
            raw.uvRect = new Rect(0, 0, W * Cell / 240f, H * Cell / 160f);
            raw.color = new Color(1.18f, 0.94f, 0.82f, 1f);
            raw.raycastTarget = false;

            var strataGo = new GameObject("Strata", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            strataGo.transform.SetParent(mapRoot, false);
            var strata = (RectTransform)strataGo.transform;
            strata.anchorMin = strata.anchorMax = Center;
            strata.pivot = Center;
            strata.sizeDelta = new Vector2(W * Cell, H * Cell);
            var strataRaw = strataGo.GetComponent<RawImage>();
            strataRaw.texture = MakeStrata();
            strataRaw.uvRect = new Rect(0, 0, W * Cell / 256f, H * Cell / 256f);
            strataRaw.color = new Color(1f, 0.58f, 0.32f, 0.18f);
            strataRaw.raycastTarget = false;

            Img(mapRoot, "WarmLight", glowSprite, new Color(1f, 0.55f, 0.25f, 0.055f),
                new Vector2(-220, 40), new Vector2(1000, 720));
        }

        void AddTunnelCanvas()
        {
            int tw = W * (int)Cell, th = H * (int)Cell;
            tunnelImage = Img(mapRoot, "CaveFloor", BuildFloorSprite(tw, th), Color.white,
                Vector2.zero, new Vector2(tw, th));
            AddWaterTiles();
        }

        /// <summary>
        /// Bake the static cave floor once.  Water is no longer part of this
        /// texture: it is drawn as separate tiles so a block can turn without
        /// rebuilding a 1260x756 image.
        /// </summary>
        Sprite BuildFloorSprite(int tw, int th)
        {
            var rockPixels = rockTexture != null ? rockTexture.GetPixels32() : null;
            var floorPixels = floorSprite != null && floorSprite.texture != null
                ? floorSprite.texture.GetPixels32()
                : null;
            var floorRect = floorSprite != null ? floorSprite.textureRect : new Rect(0, 0, 1, 1);
            var pixels = new Color32[tw * th];

            for (int sy = 0; sy < th; sy++)
            {
                int mapY = th - 1 - sy;
                float fy = (mapY + .5f) / Cell;
                for (int x = 0; x < tw; x++)
                {
                    float fx = (x + .5f) / Cell;
                    int cx = (int)fx, cy = (int)fy;
                    if (!puzzle.IsWalkableCell(cx, cy)) continue;

                    float dist = DistanceToRock(fx, fy)
                                 + Mathf.Sin(fx * 2.9f + fy * 1.7f) * .035f
                                 + Mathf.Sin(fx * 1.1f - fy * 3.3f) * .025f;
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dist / .38f));
                    if (edge <= .01f) continue;

                    char baseTile = puzzle.BaseTile(cx, cy);
                    bool wet = baseTile == '~' || HubPuzzle.IsGate(baseTile);
                    bool niche = puzzle.IsValveCell(cx, cy);

                    Color col;
                    if (rockPixels != null && rockPixels.Length > 0)
                    {
                        int sw = rockTexture.width;
                        int sh = rockTexture.height;
                        var p = rockPixels[((sy / 2) % sh) * sw + ((x / 2) % sw)];
                        col = p;
                    }
                    else col = new Color(.38f, .27f, .22f, 1f);

                    if (floorPixels != null && floorPixels.Length > 0)
                    {
                        int sw = floorSprite.texture.width;
                        int sh = floorSprite.texture.height;
                        int sx = (int)floorRect.x + ((x / 2) % Mathf.Max(1, (int)floorRect.width));
                        int yy = (int)floorRect.y + ((sy / 2) % Mathf.Max(1, (int)floorRect.height));
                        Color cave = floorPixels[(yy % sh) * sw + (sx % sw)];
                        col = Color.Lerp(col, cave, .38f);
                    }

                    col = new Color(
                        Mathf.Clamp01(col.r * 1.14f + .060f),
                        Mathf.Clamp01(col.g * .86f + .038f),
                        Mathf.Clamp01(col.b * .70f + .026f), 1f);

                    if (wet)
                        col = Color.Lerp(col, new Color(.045f, .125f, .155f), .58f);
                    else if (niche)
                        col = Color.Lerp(col, new Color(.070f, .052f, .045f), .55f);

                    float grain = ((x * 13 + sy * 7) % 11 - 5) * .006f;
                    col += new Color(grain, grain * .7f, grain * .5f, 0f);
                    if (niche) col += new Color(.014f, .010f, .008f, 0f);

                    float rim = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dist - .20f) / .17f));
                    col = Color.Lerp(col, new Color(.055f, .033f, .025f), rim * .78f);
                    if (rim > .35f && rim < .62f)
                        col += new Color(.055f, .032f, .020f, 0f);
                    col.a = edge;
                    pixels[sy * tw + x] = col;
                }
            }

            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(.5f, .5f), 100);
        }

        void AddWaterTiles()
        {
            waterRoot = Box(mapRoot, "WaterTiles", Vector2.zero, new Vector2(W * Cell, H * Cell), Center);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    char b = puzzle.BaseTile(x, y);
                    if (b != '~' && b != '.' && !HubPuzzle.IsGate(b)) continue;
                    var go = new GameObject("WaterTile",
                        typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(waterRoot, false);
                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = rt.anchorMax = Center;
                    rt.pivot = Center;
                    rt.anchoredPosition = CellPosition(x, y);
                    rt.sizeDelta = new Vector2(Cell, Cell);
                    var image = go.GetComponent<Image>();
                    image.raycastTarget = false;
                    image.preserveAspect = false;
                    image.color = new Color(0.46f, 0.73f, 0.77f, 1f);
                    waterTiles.Add(new WaterTile { X = x, Y = y, Image = image });
                }
            }
            RefreshWaterTiles();
        }

        void RefreshWaterTiles()
        {
            for (int i = 0; i < waterTiles.Count; i++)
            {
                var tile = waterTiles[i];
                if (tile.Image == null) continue;
                var rt = tile.Image.rectTransform;
                rt.anchoredPosition = CellPosition(tile.X, tile.Y);
                rt.localRotation = Quaternion.identity;
                bool water = puzzle.IsWaterCell(tile.X, tile.Y, level1Cleared);
                tile.Image.enabled = water;
                if (!water) continue;
                tile.Mask = WaterMask(tile.X, tile.Y);
            }

            RecomputeReachability();
            for (int i = 0; i < waterTiles.Count; i++)
            {
                var tile = waterTiles[i];
                if (tile.Image == null || !tile.Image.enabled) continue;
                tile.Image.color = reachable[tile.X, tile.Y]
                    ? new Color(0.58f, 0.88f, 0.92f, 1f)
                    : new Color(0.26f, 0.44f, 0.48f, .92f);
            }
            ApplyWaterFrame(true);
        }

        void RecomputeReachability()
        {
            System.Array.Clear(reachable, 0, reachable.Length);
            int sx = GridX(fishPos.x), sy = GridY(fishPos.y);
            if (!puzzle.IsWaterCell(sx, sy, level1Cleared)) return;
            var queue = new Queue<Vector2Int>();
            reachable[sx, sy] = true;
            queue.Enqueue(new Vector2Int(sx, sy));
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                TryFlow(p.x + 1, p.y, queue);
                TryFlow(p.x - 1, p.y, queue);
                TryFlow(p.x, p.y + 1, queue);
                TryFlow(p.x, p.y - 1, queue);
            }
        }

        void TryFlow(int x, int y, Queue<Vector2Int> queue)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            if (reachable[x, y]) return;
            if (!puzzle.IsWaterCell(x, y, level1Cleared)) return;
            reachable[x, y] = true;
            queue.Enqueue(new Vector2Int(x, y));
        }

        int WaterMask(int x, int y)
        {
            int mask = 0;
            if (!puzzle.IsWaterCell(x, y - 1, level1Cleared)) mask |= 1;
            if (!puzzle.IsWaterCell(x, y + 1, level1Cleared)) mask |= 2;
            if (!puzzle.IsWaterCell(x - 1, y, level1Cleared)) mask |= 4;
            if (!puzzle.IsWaterCell(x + 1, y, level1Cleared)) mask |= 8;
            return mask;
        }

        void ApplyWaterFrame(bool force)
        {
            if (waterTileVariants == null || waterTileVariants.Length == 0) return;
            int tick = Mathf.FloorToInt(Time.unscaledTime * 6f);
            int frame = tick % waterTileVariants.Length;
            if (!force && frame == waterTileFrame) return;
            waterTileFrame = frame;
            var variants = waterTileVariants[frame];
            if (variants == null) return;
            for (int i = 0; i < waterTiles.Count; i++)
            {
                var tile = waterTiles[i];
                if (tile.Image == null || !tile.Image.enabled) continue;
                var sprite = variants[Mathf.Clamp(tile.Mask, 0, variants.Length - 1)];
                if (sprite != null) tile.Image.sprite = sprite;
            }
        }

        float DistanceToRock(float fx, float fy)
        {
            float best = 9f;
            int cx = (int)fx, cy = (int)fy;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (puzzle.IsWalkableCell(x, y)) continue;
                float ddx = Mathf.Max(x - fx, fx - (x + 1), 0f);
                float ddy = Mathf.Max(y - fy, fy - (y + 1), 0f);
                best = Mathf.Min(best, Mathf.Sqrt(ddx * ddx + ddy * ddy));
            }
            return best;
        }

        void AddProps()
        {
            var rocks = new[]
            {
                new Vector2Int(2, 6), new Vector2Int(43, 7), new Vector2Int(12, 23), new Vector2Int(33, 5),
                new Vector2Int(18, 25), new Vector2Int(42, 2), new Vector2Int(26, 22), new Vector2Int(3, 16),
                new Vector2Int(41, 16), new Vector2Int(23, 4),
            };
            for (int i = 0; i < rocks.Length; i++)
                if (IsRockCell(rocks[i].x, rocks[i].y))
                    AddProp(rockPropSprite, rocks[i].x, rocks[i].y, i, 0.75f, 0.75f, 0.55f);

            var pots = new[] { new Vector2Int(2, 13), new Vector2Int(43, 21), new Vector2Int(24, 2) };
            for (int i = 0; i < pots.Length; i++)
                if (IsRockCell(pots[i].x, pots[i].y))
                    AddProp(potSprite, pots[i].x, pots[i].y, i + 10, 0.80f, 0.80f, 0.62f);

            if (IsRockCell(23, 3))
                AddProp(statueSprite, 23, 3, 20, 0.70f, 0.70f, 0.58f);

            var dryRocks = new[]
            {
                new Vector2Int(4, 7), new Vector2Int(9, 7), new Vector2Int(4, 11), new Vector2Int(9, 11),
                new Vector2Int(34, 7), new Vector2Int(40, 7), new Vector2Int(34, 11), new Vector2Int(40, 11),
                new Vector2Int(19, 17), new Vector2Int(25, 17), new Vector2Int(4, 17), new Vector2Int(9, 17),
                new Vector2Int(34, 17), new Vector2Int(40, 17), new Vector2Int(4, 21), new Vector2Int(9, 21),
                new Vector2Int(3, 9), new Vector2Int(10, 9), new Vector2Int(35, 9), new Vector2Int(39, 9),
                new Vector2Int(20, 14), new Vector2Int(24, 14), new Vector2Int(3, 20), new Vector2Int(10, 20),
                new Vector2Int(35, 20), new Vector2Int(39, 20), new Vector2Int(13, 7), new Vector2Int(31, 7),
            };
            for (int i = 0; i < dryRocks.Length; i++)
                if (IsFreeDryCell(dryRocks[i].x, dryRocks[i].y))
                    AddProp(i % 3 == 0 ? potSprite : rockPropSprite, dryRocks[i].x, dryRocks[i].y,
                        i + 30, i % 3 == 0 ? 0.65f : 0.55f, 0.62f, 0.48f);

            for (int i = 0; i < 5; i++)
            {
                int x = 3 + i * 9, y = 2 + (i % 2) * 20;
                if (!IsRockCell(x, y)) continue;
                Img(mapRoot, "CaveMotif", glowSprite, new Color(1f, 0.55f, 0.25f, 0.08f),
                    CellPosition(x, y), new Vector2(90, 90));
            }

            var mushrooms = new[]
            {
                new Vector2Int(4, 7), new Vector2Int(9, 11), new Vector2Int(34, 11), new Vector2Int(40, 7),
                new Vector2Int(19, 17), new Vector2Int(25, 17), new Vector2Int(4, 21), new Vector2Int(34, 21),
                new Vector2Int(12, 6), new Vector2Int(42, 12), new Vector2Int(42, 22), new Vector2Int(33, 22),
            };
            for (int i = 0; i < mushrooms.Length; i++)
                if (IsFreeDryCell(mushrooms[i].x, mushrooms[i].y))
                    AddMushroomPatch(mushrooms[i].x, mushrooms[i].y, i);

            var crystals = new[]
            {
                new Vector2Int(3, 11), new Vector2Int(10, 7), new Vector2Int(34, 7), new Vector2Int(40, 11),
                new Vector2Int(20, 17), new Vector2Int(24, 17), new Vector2Int(3, 18), new Vector2Int(10, 21),
                new Vector2Int(34, 21), new Vector2Int(40, 18),
            };
            for (int i = 0; i < crystals.Length; i++)
                if (IsFreeDryCell(crystals[i].x, crystals[i].y))
                    AddCrystalPatch(crystals[i].x, crystals[i].y, i);
        }

        void AddCrystalPatch(int x, int y, int seed)
        {
            var root = Box(mapRoot, "Crystals", CellPosition(x, y) + new Vector2(0, -4),
                new Vector2(50, 56), Center);
            var rng = new System.Random(900 + seed);
            Img(root, "CrystalGlow", glowSprite, new Color(0.34f, 0.82f, 1f, 0.14f),
                new Vector2(0, 2), new Vector2(66, 60));
            if (crystalFormationSprite != null)
            {
                Img(root, "CrystalFormation", crystalFormationSprite, new Color(0.82f, 0.94f, 1f, 0.95f),
                    new Vector2(0, -5), new Vector2(42, 56));
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var shard = Img(root, "Shard", rounded, new Color(0.55f - i * .06f, 0.86f, 0.98f, 0.92f),
                        new Vector2(-10 + i * 9 + rng.Next(-3, 4), rng.Next(-4, 3)),
                        new Vector2(5 + i, 16 + rng.Next(-5, 10)));
                    shard.rectTransform.localRotation = Quaternion.Euler(0, 0, -22 + i * 21 + rng.Next(-6, 7));
                }
            }
            Img(root, "CrystalBase", ellipse, new Color(0.10f, 0.20f, 0.20f, 0.55f),
                new Vector2(0, -18), new Vector2(30, 9));
        }

        void AddMushroomPatch(int x, int y, int seed)
        {
            var root = Box(mapRoot, "CaveLights", CellPosition(x, y) + new Vector2(0, -3),
                new Vector2(54, 44), Center);
            var rng = new System.Random(700 + seed);
            Img(root, "PatchGlow", glowSprite, new Color(0.42f, 0.95f, 0.86f, 0.10f),
                new Vector2(0, 4), new Vector2(64, 54));
            for (int i = 0; i < 3; i++)
            {
                float ox = -14 + i * 13 + rng.Next(-3, 4);
                float oy = rng.Next(-4, 4);
                Img(root, "Stem", rounded, new Color(0.62f, 0.72f, 0.62f, 0.9f),
                    new Vector2(ox, oy - 5), new Vector2(3, 9));
                Img(root, "Cap", ellipse, new Color(0.55f, 0.94f, 0.82f, 0.95f),
                    new Vector2(ox, oy + 1), new Vector2(10 - i, 5));
                Img(root, "CapGlow", glowSprite, new Color(0.45f, 1f, 0.90f, 0.12f),
                    new Vector2(ox, oy + 1), new Vector2(28, 24));
            }
        }

        void AddProp(Sprite sprite, int x, int y, int seed, float scale, float r, float g)
        {
            if (sprite == null) return;
            var root = Box(mapRoot, "CaveProp", CellPosition(x, y) + new Vector2(0, -3),
                new Vector2(sprite.rect.width * scale, sprite.rect.height * scale), Center);
            var im = root.gameObject.AddComponent<Image>();
            im.sprite = sprite;
            im.color = new Color(r, g, 0.42f, 0.92f);
            im.raycastTarget = false;
            root.localRotation = Quaternion.Euler(0, 0, (seed % 5 - 2) * 2f);
        }

        void AddStartMarker()
        {
            var pos = CellPosition(22, 16);
            Img(mapRoot, "StartRing", ringSprite, new Color(0.55f, 0.95f, 1f, 0.22f), pos, new Vector2(46, 46));
            var label = CenterLabel(mapRoot, "StartLabel", "起点", 11, new Color(0.70f, 0.90f, 0.90f, 0.70f),
                TextAnchor.MiddleCenter, pos + new Vector2(0, 30), new Vector2(60, 18));
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
        }

        void AddPortal(int x, int y, int level, string title, string subtitle,
            Color active, Color halo, bool compact = true)
        {
            AddPortalAt(CellPosition(x, y), level, title, subtitle, active, halo, compact);
        }

        void AddPortalAt(Vector2 position, int level, string title, string subtitle,
            Color active, Color halo, bool compact = true)
        {
            bool hidden = level >= Campaign.VisibleLevels;
            float rootSize = hidden ? 78f : compact ? 66f : 96f;
            var root = Box(mapRoot, "Portal " + title, position,
                new Vector2(rootSize, rootSize), Center);
            var p = new Portal
            {
                Level = level, Position = position, Title = title, Subtitle = subtitle,
                Active = active, HaloColor = halo, Root = root, Awake = 0,
                Phase = (position.x * 0.011f + position.y * 0.008f) % 6.28f
            };

            Img(root, "Beam", glowSprite, new Color(halo.r, halo.g, halo.b, compact ? .07f : .10f),
                new Vector2(0, compact ? 5f : 8f), new Vector2(compact ? 58f : 84f, compact ? 92f : 150f));
            p.Halo = Img(root, "Halo", glowSprite, new Color(halo.r, halo.g, halo.b, 0.22f),
                Vector2.zero, new Vector2(hidden ? 96f : compact ? 78f : 124f,
                    hidden ? 96f : compact ? 78f : 124f));
            Img(root, "Shrine", shrineSprite != null ? shrineSprite : ellipse,
                new Color(0.94f, 0.70f, 0.50f, 0.96f), new Vector2(0, compact ? -6f : -10f),
                compact ? new Vector2(44, 38) : new Vector2(64, 56));
            p.Ring = Img(root, "Ring", ringSprite, new Color(halo.r, halo.g, halo.b, 0.35f),
                new Vector2(0, -2), new Vector2(compact ? 38f : 64f, compact ? 38f : 64f));

            var leaves = Box(root, "Leaves", new Vector2(0, compact ? -4f : -6f),
                new Vector2(72, 72), Center);
            var rng = new System.Random(40 + level);
            int outer = compact ? 4 : 7;
            for (int i = 0; i < outer; i++)
            {
                float angle = i * 360f / outer + rng.Next(-9, 10);
                var leaf = Box(leaves, "Leaf", Vector2.zero,
                    new Vector2(compact ? 8f : 11f, compact ? 20f + rng.Next(-4, 8) : 30f + rng.Next(-8, 16)),
                    new Vector2(.5f, 0f));
                var im = leaf.gameObject.AddComponent<Image>();
                im.sprite = leafSprite;
                im.raycastTarget = false;
                im.color = Dormant;
                leaf.localRotation = Quaternion.Euler(0, 0, angle);
                p.Leaves.Add(im);
                p.LeafAngles.Add(angle);
            }
            int inner = compact ? 2 : 4;
            for (int i = 0; i < inner; i++)
            {
                float angle = i * (360f / inner) + 30f;
                var leaf = Box(leaves, "LeafInner", Vector2.zero,
                    new Vector2(compact ? 7f : 9f, compact ? 12f : 20f), new Vector2(.5f, 0f));
                var im = leaf.gameObject.AddComponent<Image>();
                im.sprite = leafSprite;
                im.raycastTarget = false;
                im.color = Dormant;
                leaf.localRotation = Quaternion.Euler(0, 0, angle);
                p.Leaves.Add(im);
                p.LeafAngles.Add(angle);
            }

            p.Core = Img(root, "Core", orbSprite, Color.white, new Vector2(0, compact ? 3f : 5f),
                new Vector2(compact ? 18f : 30f, compact ? 18f : 30f));
            int beads = compact ? 2 : 3;
            for (int i = 0; i < beads; i++)
            {
                float a = (i * 137f + level * 13f) * Mathf.Deg2Rad;
                Img(root, "Bead", bubbleSprite, new Color(0.85f, 1f, 0.95f, 0.30f),
                    new Vector2(Mathf.Cos(a) * (compact ? 12f : 18f), Mathf.Sin(a) * (compact ? 9f : 14f) + 5f),
                    new Vector2(compact ? 3f : 4f + i, compact ? 3f : 4f + i));
            }

            float labelY = hidden ? -44f : compact ? -31f : -50f;
            float labelW = hidden ? 104f : compact ? 54f : 186f;
            float labelH = hidden ? 24f : compact ? 20f : 25f;
            var labelBack = Box(root, "LabelBack", new Vector2(0, labelY),
                new Vector2(labelW, labelH), Center);
            var labelImage = labelBack.gameObject.AddComponent<Image>();
            labelImage.sprite = rounded;
            labelImage.type = Image.Type.Sliced;
            labelImage.color = new Color32(28, 17, 14, 218);
            labelImage.raycastTarget = false;
            p.Label = CenterLabel(root, "PortalLabel", title, compact ? 12 : 14, active,
                TextAnchor.MiddleCenter, new Vector2(0, labelY),
                new Vector2(labelW - 8f, labelH - 4f), FontStyle.Bold);
            p.Label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);

            portals.Add(p);
        }

        void BuildPortals()
        {
            // Scene-authored entrances take priority. The anchors are ordinary
            // GameObjects under TidalHollow, so they can be dragged in the Scene
            // view and their labels/colours edited in the Inspector.
            if (useSceneAnchors)
            {
                var anchors = FindObjectsByType<HubPortalAnchor>(FindObjectsSortMode.None);
                if (anchors.Length > 0)
                {
                    System.Array.Sort(anchors, (a, b) => a.level.CompareTo(b.level));
                    foreach (var anchor in anchors)
                    {
                        if (anchor == null) continue;
                        AddPortalAt(anchor.transform.localPosition, anchor.level,
                            anchor.ResolvedTitle, anchor.ResolvedSubtitle,
                            anchor.activeColor, anchor.haloColor, anchor.compact);
                    }
                    return;
                }
            }

            // Fallback: the original generated layout. This keeps the hub usable
            // if every anchor has been deleted.
            AddChapterPortals(0, 7, 9, Kelp, new Color(0.35f, 0.85f, 0.62f, 1f));
            AddChapterPortals(4, 37, 9, ColdKelp, new Color(0.38f, 0.62f, 1f, 1f));
            AddChapterPortals(8, 22, 16, Teal, new Color(0.30f, 0.80f, 0.88f, 1f));
            AddChapterPortals(12, 7, 19, Amber, new Color(0.95f, 0.58f, 0.28f, 1f));
            AddChapterPortals(16, 37, 19, new Color(0.82f, 0.62f, 1f, 1f),
                new Color(0.66f, 0.45f, 1f, 1f));
            AddPortal(7, 19, Campaign.VisibleLevels, "隐藏关", "静水回声",
                HideKelp, new Color(1f, 0.72f, 0.30f, 1f));
        }

        void AddChapterPortals(int first, int cx, int cy, Color active, Color halo)
        {
            AddPortal(cx - 2, cy - 1, first, (first + 1).ToString("00"),
                Campaign.Levels[first].Name, active, halo);
            AddPortal(cx + 2, cy - 1, first + 1, (first + 2).ToString("00"),
                Campaign.Levels[first + 1].Name, active, halo);
            AddPortal(cx - 2, cy + 1, first + 2, (first + 3).ToString("00"),
                Campaign.Levels[first + 2].Name, active, halo);
            AddPortal(cx + 2, cy + 1, first + 3, (first + 4).ToString("00"),
                Campaign.Levels[first + 3].Name, active, halo);
        }

        void AddGates()
        {
            AddGate(13, 21, 'g');
            AddGate(31, 8, 'x');
        }

        void AddGate(int x, int y, char tile)
        {
            var center = CellPosition(x, y) + new Vector2(Cell * .5f, -Cell * .5f);
            var root = Box(mapRoot, "StoneDoor", center, new Vector2(Cell * 2, Cell * 2), Center);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var gate = new Gate { X = x, Y = y, Root = root, Group = group, Base = center };
            gates.Add(gate);

            var shadow = Img(root, "DoorShadow", ellipse, new Color(0, 0, 0, 0.45f),
                new Vector2(0, -Cell * .82f), new Vector2(Cell * 2.0f, 14));
            var slab = Box(root, "Slab", Vector2.zero, new Vector2(Cell * 1.88f, Cell * 1.88f), Center);
            var slabImage = slab.gameObject.AddComponent<Image>();
            slabImage.sprite = rounded;
            slabImage.type = Image.Type.Sliced;
            slabImage.color = new Color(0.34f, 0.23f, 0.19f, 1f);
            slabImage.raycastTarget = false;
            gate.Slabs.Add(slabImage);

            var inset = Img(root, "Inset", rounded, new Color(0.20f, 0.15f, 0.13f, 1f),
                Vector2.zero, new Vector2(Cell * 1.50f, Cell * 1.50f));
            inset.type = Image.Type.Sliced;
            gate.Slabs.Add(inset);
            var rune = Img(root, "Rune", ringSprite, new Color(0.98f, 0.76f, 0.36f, 0.85f),
                Vector2.zero, new Vector2(Cell * .92f, Cell * .92f));
            gate.Slabs.Add(rune);
            var runeText = CenterLabel(root, "RuneGlyph", "潮", 24, new Color32(247, 212, 134, 255),
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(40, 40), FontStyle.Bold);
            runeText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            gate.Texts.Add(runeText);

            for (int i = 0; i < 4; i++)
            {
                var rivet = Img(root, "Rivet", ellipse, new Color(0.12f, 0.17f, 0.20f, 0.8f),
                    new Vector2((i % 2 == 0 ? -1 : 1) * Cell * .72f, (i < 2 ? 1 : -1) * Cell * .72f),
                    new Vector2(7, 7));
            }

            if (gatesStartOpen) OpenGateImmediate(gate);
            else SetGateClosed(gate);
        }

        void AddPearl()
        {
            pearlRoot = Box(mapRoot, "TidePearl", CellPosition(37, 19), new Vector2(80, 80), Center);
            pearlGlow = Img(pearlRoot, "PearlGlow", glowSprite, new Color(1f, 0.82f, 0.42f, 0.42f),
                Vector2.zero, new Vector2(118, 118));
            var halo = Img(pearlRoot, "PearlHalo", ringSprite, new Color(1f, 0.80f, 0.40f, 0.38f),
                Vector2.zero, new Vector2(48, 48));
            var pearl = Img(pearlRoot, "Pearl", pearlSprite, Color.white, Vector2.zero, new Vector2(30, 30));
            var labelBack = Box(pearlRoot, "PearlLabelBack", new Vector2(0, 40), new Vector2(104, 23), Center);
            var labelImage = labelBack.gameObject.AddComponent<Image>();
            labelImage.sprite = rounded;
            labelImage.type = Image.Type.Sliced;
            labelImage.color = new Color32(28, 17, 14, 218);
            labelImage.raycastTarget = false;
            var label = CenterLabel(pearlRoot, "PearlLabel", "潮汐珍珠", 13, Gold, TextAnchor.MiddleCenter,
                new Vector2(0, 40), new Vector2(98, 19), FontStyle.Bold);
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            pearlRoot.gameObject.SetActive(!pearlTaken);
        }

        void AddBubbleVents()
        {
            var vents = new[]
            {
                new Vector2Int(22, 16), new Vector2Int(16, 13), new Vector2Int(10, 8),
                new Vector2Int(34, 9), new Vector2Int(37, 19), new Vector2Int(7, 19),
            };
            for (int i = 0; i < 30; i++)
            {
                var vent = vents[i % vents.Length];
                var rt = Box(mapRoot, "Bubble", CellPosition(vent.x, vent.y) + new Vector2(
                    ((i * 37) % 22) - 11, ((i * 53) % 18) - 9), new Vector2(7, 7), Center);
                var im = rt.gameObject.AddComponent<Image>();
                im.sprite = bubbleSprite;
                im.color = new Color(0.82f, 1f, 0.96f, 0.32f);
                im.raycastTarget = false;
                bubbles.Add(new Bubble
                {
                    Rect = rt, Image = im, Origin = rt.anchoredPosition,
                    Age = (i * 0.37f) % 4f, Life = 3.2f + (i % 5) * 0.3f,
                    Speed = 14f + (i % 4) * 4f, Phase = i * 1.7f,
                });
            }
        }

        // --------------------------------------------------------------- update

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            ApplyWaterFrame(false);
            AnimateValves(dt);
            AnimatePortals(dt);
            AnimateBubbles(dt);
            AnimatePearl();
            UpdateToast(dt);
            if (traveling) return;
            HandleMouse();
            UpdateKeyboardValve();
            HandleInput(dt);
            if (!rotatingValve) UpdateFish(dt);
            UpdateCamera(dt);
            CheckPearlPickup();
            UpdatePrompt(dt);
        }

        /// <summary>
        /// The hub is built on a Screen Space - Overlay canvas, so the "camera"
        /// is the map viewport: zoom in a little, follow the fish smoothly, and
        /// clamp at the map edges so no rock-free gutter is ever visible.
        /// </summary>
        void UpdateCamera(float dt)
        {
            if (mapRoot == null) return;
            HandleZoomInput();

            cameraZoom = Mathf.Lerp(cameraZoom, zoomTarget, 1f - Mathf.Exp(-zoomSpeed * dt));
            mapRoot.localScale = Vector3.one * cameraZoom;

            Vector2 target = CameraTarget();
            float t = 1f - Mathf.Exp(-followSpeed * dt);
            mapRoot.anchoredPosition = Vector2.Lerp(mapRoot.anchoredPosition, target, t);

            if (zoomReadout != null)
                zoomReadout.text = "镜头 " + cameraZoom.ToString("0.00") + "×   滚轮 / + - 缩放";
        }

        void HandleZoomInput()
        {
            float delta = 0f;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f)
                {
                    float amount = Mathf.Abs(scroll) > 10f
                        ? scroll / 120f
                        : Mathf.Sign(scroll);
                    delta += amount * .10f;
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.equalsKey.wasPressedThisFrame) delta += .10f;
                if (keyboard.minusKey.wasPressedThisFrame) delta -= .10f;
            }

            if (Mathf.Abs(delta) <= .0001f) return;
            zoomTarget = Mathf.Clamp(zoomTarget + delta, minZoom, maxZoom);
            PlayerPrefs.SetFloat(ZoomKey, zoomTarget);
            PlayerPrefs.Save();
        }

        void SnapCamera()
        {
            if (mapRoot == null) return;
            cameraZoom = zoomTarget;
            mapRoot.localScale = Vector3.one * cameraZoom;
            mapRoot.anchoredPosition = CameraTarget();
        }

        Vector2 CameraTarget()
        {
            Vector2 target = -fishPos * cameraZoom + new Vector2(0f, cameraBiasY);
            float halfW = W * Cell * cameraZoom * .5f;
            float halfH = H * Cell * cameraZoom * .5f;
            float maxX = Mathf.Max(0f, halfW - viewSize.x * .5f);
            float maxY = Mathf.Max(0f, halfH - viewSize.y * .5f);
            target.x = Mathf.Clamp(target.x, -maxX, maxX);
            target.y = Mathf.Clamp(target.y, -maxY, maxY);
            return target;
        }

        void OnDisable()
        {
            if (PlayerPrefs.GetFloat(ZoomKey, defaultZoom) != zoomTarget)
            {
                PlayerPrefs.SetFloat(ZoomKey, zoomTarget);
                PlayerPrefs.Save();
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            // The hub map's canvas space maps 1:1 to scene units. Draw its
            // bounds and a coarse grid so entrance anchors are easy to place.
            Vector3 center = transform.position;
            Gizmos.color = new Color(0.42f, 0.85f, 0.72f, 0.55f);
            Gizmos.DrawWireCube(center, new Vector3(W * Cell, H * Cell, 0f));
            Gizmos.color = new Color(0.42f, 0.85f, 0.72f, 0.12f);
            for (int x = 0; x <= W; x += 5)
            {
                float px = center.x + (x - W * .5f) * Cell;
                Gizmos.DrawLine(new Vector3(px, center.y - H * Cell * .5f, 0f),
                    new Vector3(px, center.y + H * Cell * .5f, 0f));
            }
            for (int y = 0; y <= H; y += 5)
            {
                float py = center.y + (y - H * .5f) * Cell;
                Gizmos.DrawLine(new Vector3(center.x - W * Cell * .5f, py, 0f),
                    new Vector3(center.x + W * Cell * .5f, py, 0f));
            }
            UnityEditor.Handles.Label(center + new Vector3(-W * Cell * .5f, H * Cell * .5f + 20f, 0f),
                "潮汐洞穴 · 手动入口锚点区域");
        }
#endif

        void HandleInput(float dt)
        {
            Vector2 input = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (!rotatingValve)
                {
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1;
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1;
                }
                if (keyboard.spaceKey.wasPressedThisFrame) RotateFocusedValve();
                if (keyboard.eKey.wasPressedThisFrame) Interact();
                if (keyboard.zKey.wasPressedThisFrame) UndoValve();
                if (keyboard.hKey.wasPressedThisFrame) HintValve();
                if (keyboard.rKey.wasPressedThisFrame) RestartValvePuzzle();
            }

            Vector2 desired = input.sqrMagnitude > .01f ? input.normalized * SwimSpeed : Vector2.zero;
            float rate = input.sqrMagnitude > .01f ? SwimAcceleration : SwimDrag;
            fishVel = Vector2.MoveTowards(fishVel, desired, rate * dt);
        }

        void UpdateFish(float dt)
        {
            Vector2 delta = fishVel * dt;
            int steps = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / 3f), 1, 10);
            Vector2 step = delta / steps;
            for (int i = 0; i < steps; i++)
            {
                if (CanFit(fishPos + new Vector2(step.x, 0))) fishPos.x += step.x;
                else fishVel.x = 0;
                if (CanFit(fishPos + new Vector2(0, step.y))) fishPos.y += step.y;
                else fishVel.y = 0;
            }

            float speed = fishVel.magnitude;
            if (speed > 8f) fishFacing = FishArt.FacingOf(fishVel, fishFacing);
            fishBob = Mathf.Sin(Time.unscaledTime * 2.4f) * 1.2f;
            fishRoot.anchoredPosition = fishPos + new Vector2(0, fishBob);

            fishAnim += dt;
            int frame = FishArt.FrameAt(fishAnim);
            if (fishSpriteImage != null && (frame != fishFrame || fishFacing != fishShown))
            {
                fishFrame = frame;
                fishShown = fishFacing;
                var frames = FishArt.Swim(fishFacing);
                if (frames != null && frames.Length > 0)
                    fishSpriteImage.sprite = frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
            }

            bubbleClock -= dt;
            if (speed > 40f && bubbleClock <= 0)
            {
                bubbleClock = 0.18f;
                SpawnTrailBubble();
            }
        }

        void AnimateValves(float dt)
        {
            valveClock += dt;
            if (hintTimer > 0)
            {
                hintTimer -= dt;
                if (hintTimer <= 0) hintValve = null;
            }

            ValveView focus = hoveredValve != null
                ? hoveredValve
                : hintTimer > 0 && hintValve != null ? hintValve : keyboardValve;
            if (focus != focusedValve)
            {
                focusedValve = focus;
                UpdateStatusLine();
            }

            float pulse = .55f + Mathf.Sin(valveClock * 1.9f) * .18f;
            for (int i = 0; i < valveViews.Count; i++)
            {
                var view = valveViews[i];
                if (view.Root == null) continue;
                bool hover = view == hoveredValve;
                bool focused = view == focusedValve;
                bool hinted = view == hintValve && hintTimer > 0;
                bool open = puzzle.IsBlockOpen(view.Valve, level1Cleared);
                Color tint = hinted ? Gold : open ? Teal : new Color(0.62f, 0.82f, 0.80f, 1f);
                float alpha = hover || focused ? 1f : open ? .72f + pulse * .22f : .38f + pulse * .16f;
                for (int b = 0; b < view.Brackets.Count; b++)
                {
                    var image = view.Brackets[b];
                    if (image == null) continue;
                    image.color = new Color(tint.r, tint.g, tint.b, alpha);
                }
                if (view.Glow != null)
                    view.Glow.color = new Color(tint.r, tint.g, tint.b,
                        hover ? .22f : focused ? .16f : hinted ? .20f : open ? .10f : .04f);
                if (view.Glyph != null)
                {
                    view.Glyph.color = new Color(tint.r, tint.g, tint.b,
                        hover || focused || hinted ? .98f : .55f);
                    view.Glyph.rectTransform.localRotation =
                        Quaternion.Euler(0, 0, Mathf.Sin(valveClock * .8f + i) * 6f);
                }
                int sides = puzzle.WetSides(view.Valve, level1Cleared);
                for (int s = 0; s < view.SideDots.Count; s++)
                {
                    var dot = view.SideDots[s];
                    if (dot == null) continue;
                    bool lit = (sides & (1 << s)) != 0;
                    dot.color = lit
                        ? new Color(1f, 0.86f, 0.46f, hover || focused ? 1f : .95f)
                        : new Color(0.44f, 0.62f, 0.61f, .34f);
                }

                // Ghost of where the two water cells will land after one turn.
                bool showPreview = focused || hinted;
                int preview = 0;
                for (int p = 0; p < view.Preview.Count; p++)
                    if (view.Preview[p] != null) view.Preview[p].enabled = false;
                if (showPreview)
                {
                    int next = (view.Valve.State + 1) & 3;
                    for (int dy = 0; dy < 2 && preview < view.Preview.Count; dy++)
                    {
                        for (int dx = 0; dx < 2 && preview < view.Preview.Count; dx++)
                        {
                            if (!HubPuzzle.WaterOffset(dx, dy, next)) continue;
                            var ghost = view.Preview[preview++];
                            if (ghost == null) continue;
                            ghost.enabled = true;
                            ghost.rectTransform.anchoredPosition =
                                CellPosition(view.Valve.X + dx, view.Valve.Y + dy) - ValveCenter(view.Valve);
                            float a = (hinted ? .38f : .22f) *
                                      (.75f + Mathf.Sin(valveClock * 3.2f) * .25f);
                            ghost.color = new Color(0.58f, 0.95f, 1f, a);
                        }
                    }
                }

                float target = hover ? 1.10f : hinted ? 1.08f : focused ? 1.04f : 1f;
                float scale = Mathf.Lerp(view.Root.localScale.x, target, 1f - Mathf.Exp(-12f * dt));
                view.Root.localScale = Vector3.one * scale;
            }
        }

        void UpdateStatusLine()
        {
            if (statusText == null || puzzle == null) return;
            string text = "步数 " + turnCount + "   ·   水道 " + puzzle.OpenCount(level1Cleared) +
                          " / " + puzzle.Valves.Length;
            if (focusedValve != null)
            {
                int index = System.Array.IndexOf(puzzle.Valves, focusedValve.Valve) + 1;
                int next = (focusedValve.Valve.State + 1) & 3;
                text += "   ·   水块 " + index + "  " + HubPuzzle.SidePair(focusedValve.Valve.State) +
                        " → " + HubPuzzle.SidePair(next);
            }
            statusText.text = text;
        }

        void UpdateKeyboardValve()
        {
            if (hoveredValve != null)
            {
                keyboardValve = null;
                return;
            }

            // A block right next to the fish wins, so swimming up to a block
            // and pressing Space always turns the one you are looking at.
            ValveView near = null;
            float nearDistance = Cell * 2.6f;
            for (int i = 0; i < valveViews.Count; i++)
            {
                var view = valveViews[i];
                if (view == null) continue;
                float distance = Vector2.Distance(fishPos, ValveCenter(view.Valve));
                if (distance < nearDistance)
                {
                    nearDistance = distance;
                    near = view;
                }
            }
            if (near != null)
            {
                keyboardValve = near;
                return;
            }

            // Otherwise Space aims at the block the current objective needs.
            keyboardValve = FindObjectiveValve();
        }

        ValveView FindObjectiveValve()
        {
            int[] order = HintOrder();
            for (int i = 0; i < order.Length; i++)
            {
                int index = order[i];
                if (index < 0 || index >= valveViews.Count) continue;
                if (valveViews[index].Valve.State != valveViews[index].Valve.Solution)
                    return valveViews[index];
            }
            return null;
        }

        void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();

            // Only real widgets capture the pointer. Tints, scrims, labels and
            // the toast / prompt panels are pure decoration, so a valve that
            // happens to sit behind them can still be clicked.
            if (PointerOverWidget())
            {
                hoveredValve = null;
                return;
            }

            hoveredValve = null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mapRoot, screen, null, out Vector2 local))
            {
                int gx = GridX(local.x), gy = GridY(local.y);
                var valve = puzzle.ValveAt(gx, gy);
                if (valve != null) hoveredValve = ValveViewFor(valve);
            }

            if (mouse.leftButton.wasPressedThisFrame && hoveredValve != null)
            {
                TryRotateValve(hoveredValve);
            }
        }

        /// <summary>
        /// True only when the pointer sits on something the player can press.
        /// <c>IsPointerOverGameObject()</c> is not usable here: it reports any
        /// graphic under the cursor, so a decorative full-screen tint froze the
        /// valve clicks for the whole hub.
        /// </summary>
        bool PointerOverWidget()
        {
            var system = EventSystem.current;
            var mouse = Mouse.current;
            if (system == null || mouse == null) return false;
            if (pointerEvent == null) pointerEvent = new PointerEventData(system);
            pointerEvent.position = mouse.position.ReadValue();
            pointerHits.Clear();
            system.RaycastAll(pointerEvent, pointerHits);
            for (int i = 0; i < pointerHits.Count; i++)
                if (pointerHits[i].gameObject.GetComponentInParent<Selectable>() != null)
                    return true;
            return false;
        }

        ValveView ValveViewFor(HubPuzzle.Valve valve)
        {
            for (int i = 0; i < valveViews.Count; i++)
                if (valveViews[i].Valve == valve) return valveViews[i];
            return null;
        }

        void TryRotateValve(ValveView view)
        {
            if (view == null || traveling || rotatingValve) return;
            var valve = view.Valve;
            int nextState = (valve.State + 1) & 3;

            bool fishRides = FishInValve(valve);
            int newDx = 0, newDy = 0;
            if (fishRides)
            {
                int gx = GridX(fishPos.x), gy = GridY(fishPos.y);
                int dx = Mathf.Clamp(gx - valve.X, 0, 1);
                int dy = Mathf.Clamp(gy - valve.Y, 0, 1);
                newDx = 1 - dy;
                newDy = dx;
                int nx = valve.X + newDx, ny = valve.Y + newDy;
                if (!CanFitNext(CellPosition(nx, ny), valve, nextState))
                {
                    ShowToast("这一步会把小鱼挤进石壁。先游出去，再转这块水块。", 3.2f);
                    return;
                }
            }

            undoStack.Push(Snapshot());
            turnCount++;
            HintClear();
            StartCoroutine(RotateValveRoutine(view, fishRides, newDx, newDy));
        }

        bool FishInValve(HubPuzzle.Valve valve)
        {
            int gx = GridX(fishPos.x), gy = GridY(fishPos.y);
            return gx >= valve.X && gx <= valve.X + 1 && gy >= valve.Y && gy <= valve.Y + 1;
        }

        bool ValveCellWater(HubPuzzle.Valve valve, int state, int x, int y)
        {
            if (x < valve.X || x > valve.X + 1 || y < valve.Y || y > valve.Y + 1) return false;
            return HubPuzzle.WaterOffset(x - valve.X, y - valve.Y, state);
        }

        /// <summary>
        /// Same clearance test as <see cref="CanFit"/>, but a valve is evaluated
        /// in the orientation it is about to have, so the fish can ride the
        /// turning block exactly like it rides the room in a level.
        /// </summary>
        bool CanFitNext(Vector2 local, HubPuzzle.Valve valve, int state)
        {
            for (int i = -1; i < 8; i++)
            {
                Vector2 sample = i < 0
                    ? local
                    : local + new Vector2(Mathf.Cos(i * Mathf.PI * .25f),
                        Mathf.Sin(i * Mathf.PI * .25f)) * FishRadius;
                int x = GridX(sample.x), y = GridY(sample.y);
                bool water = valve != null && x >= valve.X && x <= valve.X + 1 &&
                             y >= valve.Y && y <= valve.Y + 1
                    ? ValveCellWater(valve, state, x, y)
                    : puzzle.IsWaterCell(x, y, level1Cleared);
                if (!water) return false;
            }
            return true;
        }

        HubSnapshot Snapshot()
        {
            var snapshot = new HubSnapshot
            {
                States = new int[puzzle.Valves.Length],
                Fish = fishPos,
                Turns = turnCount
            };
            for (int i = 0; i < puzzle.Valves.Length; i++)
                snapshot.States[i] = puzzle.Valves[i].State;
            return snapshot;
        }

        void RotateFocusedValve()
        {
            if (rotatingValve || traveling) return;
            ValveView target = hoveredValve != null
                ? hoveredValve
                : hintTimer > 0 && hintValve != null ? hintValve : keyboardValve;
            if (target == null)
            {
                if (FindObjectiveValve() == null)
                    ShowToast("当前目标的水路已经接好了。去海草那里进入关卡吧。", 2.8f);
                else
                    ShowToast("用鼠标指向一块发光的水块，或靠近它再按 Space。", 2.8f);
                return;
            }
            TryRotateValve(target);
        }

        void UndoValve()
        {
            if (rotatingValve || traveling) return;
            if (undoStack.Count == 0)
            {
                ShowToast("没有可以撤销的转动。", 2f);
                return;
            }
            var snapshot = undoStack.Pop();
            for (int i = 0; i < puzzle.Valves.Length && i < snapshot.States.Length; i++)
                puzzle.Valves[i].State = snapshot.States[i];
            fishPos = snapshot.Fish;
            fishVel = Vector2.zero;
            turnCount = snapshot.Turns;
            if (fishRoot != null)
            {
                fishRoot.anchoredPosition = fishPos;
                fishRoot.localRotation = Quaternion.identity;
            }
            HintClear();
            RefreshWaterTiles();
            RefreshHud();
            ShowToast("已撤销上一次转动。", 2f);
        }

        void RestartValvePuzzle()
        {
            if (rotatingValve || traveling) return;
            puzzle.Reset();
            undoStack.Clear();
            turnCount = 0;
            fishPos = CellPosition(22, 16);
            fishVel = Vector2.zero;
            if (fishRoot != null)
            {
                fishRoot.anchoredPosition = fishPos;
                fishRoot.localRotation = Quaternion.identity;
            }
            HintClear();
            RefreshWaterTiles();
            RefreshHud();
            ShowToast("水块已复位，小鱼回到起点。", 2.6f);
        }

        void HintValve()
        {
            if (rotatingValve || traveling || puzzle == null) return;
            if (hiddenCleared)
            {
                ShowToast("潮汐洞穴已经完全苏醒了，没有还需要转动的水块。", 3.2f);
                return;
            }
            if (pearlTaken)
            {
                int nextLevel = Mathf.Clamp(GameSession.LevelsCleared, 0, Campaign.VisibleLevels - 1);
                if (nextLevel >= 8 && nextLevel < 12)
                {
                    ShowToast("中央石室就在起点旁边，直接去海草那里进入第 " +
                              (nextLevel + 1).ToString("00") + " 关。", 3.6f);
                    return;
                }
            }

            ValveView target = FindObjectiveValve();
            if (target == null)
            {
                ShowToast("当前目标的水路已经接好了。去海草那里进入关卡吧。", 3.2f);
                return;
            }

            hintValve = target;
            hintTimer = 6f;
            focusedValve = target;
            UpdateStatusLine();
            ShowToast("提示：把这块水块转到 " + HubPuzzle.SidePair(target.Valve.Solution) +
                      "（Space / 左键）。", 4.2f);
        }

        int[] HintOrder()
        {
            if (!pearlTaken) return new[] { 4, 5 };
            if (GameSession.LevelsCleared >= Campaign.VisibleLevels) return new[] { 3 };
            int nextLevel = Mathf.Clamp(GameSession.LevelsCleared, 0, Campaign.VisibleLevels - 1);
            if (nextLevel < 4) return new[] { 0, 1 };
            if (nextLevel < 8) return new[] { 2 };
            if (nextLevel < 12) return new int[0];
            if (nextLevel < 16) return new[] { 3 };
            return new[] { 4, 5 };
        }

        void HintClear()
        {
            hintTimer = 0f;
            hintValve = null;
        }

        WaterTile TileAt(int x, int y)
        {
            for (int i = 0; i < waterTiles.Count; i++)
                if (waterTiles[i].X == x && waterTiles[i].Y == y) return waterTiles[i];
            return null;
        }

        IEnumerator RotateValveRoutine(ValveView view, bool fishRides, int newDx, int newDy)
        {
            rotatingValve = true;
            fishVel = Vector2.zero;
            var valve = view.Valve;
            var center = ValveCenter(valve);
            var fishOffset = fishPos - center;
            var moving = new List<WaterTile>(2);
            var offsets = new List<Vector2>(2);
            for (int dy = 0; dy < 2; dy++)
            {
                for (int dx = 0; dx < 2; dx++)
                {
                    if (!HubPuzzle.WaterOffset(dx, dy, valve.State)) continue;
                    var tile = TileAt(valve.X + dx, valve.Y + dy);
                    if (tile == null || tile.Image == null) continue;
                    var rt = tile.Image.rectTransform;
                    rt.anchoredPosition = CellPosition(tile.X, tile.Y);
                    rt.localRotation = Quaternion.identity;
                    tile.Image.enabled = true;
                    moving.Add(tile);
                    offsets.Add(rt.anchoredPosition - center);
                }
            }

            float t = 0;
            float duration = Mathf.Max(.05f, valveRotateDuration);
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                var rotation = Quaternion.Euler(0, 0, -90f * k);
                for (int i = 0; i < moving.Count; i++)
                {
                    var rt = moving[i].Image.rectTransform;
                    rt.anchoredPosition = center + (Vector2)(rotation * offsets[i]);
                    rt.localRotation = rotation;
                }
                if (fishRides)
                {
                    fishPos = center + (Vector2)(rotation * fishOffset);
                    if (fishRoot != null)
                    {
                        fishRoot.anchoredPosition = fishPos + new Vector2(0, fishBob);
                        fishRoot.localRotation = rotation;
                    }
                }
                yield return null;
            }

            puzzle.Rotate(valve);
            if (fishRides)
            {
                fishPos = CellPosition(valve.X + newDx, valve.Y + newDy);
                fishVel = Vector2.zero;
                if (fishRoot != null)
                {
                    fishRoot.anchoredPosition = fishPos + new Vector2(0, fishBob);
                    fishRoot.localRotation = Quaternion.identity;
                }
            }
            RefreshWaterTiles();
            Burst(center, new Color(0.55f, 0.95f, 0.96f, 1f));
            ShowToast("水块转向：" + HubPuzzle.SidePair(valve.State) +
                      (puzzle.IsBlockOpen(valve, level1Cleared) ? " · 水道接通" : " · 这一侧没有水"), 2.4f);
            RefreshHud();
            rotatingValve = false;
        }

        Vector2 ValveCenter(HubPuzzle.Valve valve)
        {
            return CellPosition(valve.X, valve.Y) + new Vector2(Cell * .5f, -Cell * .5f);
        }

        void AddValveViews()
        {
            valveRoot = Box(mapRoot, "WaterBlocks", Vector2.zero,
                new Vector2(W * Cell, H * Cell), Center);
            for (int i = 0; i < puzzle.Valves.Length; i++)
            {
                var valve = puzzle.Valves[i];
                var root = Box(valveRoot, "WaterBlock " + (i + 1), ValveCenter(valve),
                    new Vector2(Cell * 2f + 10f, Cell * 2f + 10f), Center);
                var view = new ValveView { Valve = valve, Root = root };

                view.Glow = Img(root, "ValveGlow", glowSprite,
                    new Color(0.45f, 0.95f, 0.92f, .06f), Vector2.zero,
                    new Vector2(Cell * 2.9f, Cell * 2.9f));

                for (int p = 0; p < 2; p++)
                {
                    var ghost = Img(root, "TurnPreview", rounded,
                        new Color(0.58f, 0.95f, 1f, .18f), Vector2.zero,
                        new Vector2(Cell - 2f, Cell - 2f));
                    ghost.enabled = false;
                    ghost.rectTransform.localRotation = Quaternion.identity;
                    view.Preview.Add(ghost);
                }

                float half = Cell + 3f;
                float arm = 10f;
                float thick = 2.2f;
                for (int corner = 0; corner < 4; corner++)
                {
                    float sx = (corner == 0 || corner == 3) ? -1f : 1f;
                    float sy = (corner < 2) ? 1f : -1f;
                    var h = Img(root, "CornerH", rounded, Color.white,
                        new Vector2(sx * (half - arm * .5f), sy * half), new Vector2(arm, thick));
                    var v = Img(root, "CornerV", rounded, Color.white,
                        new Vector2(sx * half, sy * (half - arm * .5f)), new Vector2(thick, arm));
                    h.rectTransform.localRotation = Quaternion.identity;
                    view.Brackets.Add(h);
                    view.Brackets.Add(v);
                }

                view.Glyph = CenterLabel(root, "ValveGlyph", "↻", 20,
                    new Color(0.62f, 0.95f, 0.94f, .6f), TextAnchor.MiddleCenter,
                    Vector2.zero, new Vector2(34, 34), FontStyle.Bold);
                view.Glyph.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .7f);
                view.Glyph.raycastTarget = false;

                for (int side = 0; side < 4; side++)
                {
                    Vector2 dotPos;
                    switch (side)
                    {
                        case 0: dotPos = new Vector2(0, half + 2f); break;   // north
                        case 1: dotPos = new Vector2(half + 2f, 0); break;   // east
                        case 2: dotPos = new Vector2(0, -half - 2f); break;  // south
                        default: dotPos = new Vector2(-half - 2f, 0); break; // west
                    }
                    var dot = Img(root, "SideDot", ellipse, new Color(0.44f, 0.62f, 0.61f, .34f),
                        dotPos, new Vector2(6.5f, 6.5f));
                    dot.rectTransform.localRotation = Quaternion.identity;
                    view.SideDots.Add(dot);
                }
                valveViews.Add(view);
            }
        }

        void AnimatePortals(float dt)
        {
            swayClock += dt;
            foreach (var p in portals)
            {
                bool ready = PortalReady(p);
                p.Awake = Mathf.MoveTowards(p.Awake, ready ? 1f : 0f, dt * 2.2f);
                float sway = Mathf.Sin(swayClock * 1.15f + p.Phase) * (2.5f + p.Awake * 4.5f);
                Color leaf = Color.Lerp(Dormant, p.Active, p.Awake);
                for (int i = 0; i < p.Leaves.Count; i++)
                {
                    var im = p.Leaves[i];
                    im.color = new Color(leaf.r, leaf.g, leaf.b, 0.70f + p.Awake * 0.30f);
                    float wobble = Mathf.Sin(swayClock * 1.9f + p.Phase + i * 0.9f) * 3.2f;
                    im.rectTransform.localRotation = Quaternion.Euler(0, 0, p.LeafAngles[i] + sway + wobble);
                }
                float pulse = 0.72f + Mathf.Sin(swayClock * 2.1f + p.Phase) * 0.18f;
                p.Halo.color = new Color(p.HaloColor.r, p.HaloColor.g, p.HaloColor.b, 0.22f + p.Awake * 0.22f * pulse);
                p.Ring.color = new Color(p.HaloColor.r, p.HaloColor.g, p.HaloColor.b, 0.18f + p.Awake * 0.34f * pulse);
                Color orb = Color.Lerp(p.Active, Color.white, .34f);
                p.Core.color = new Color(orb.r, orb.g, orb.b, 0.62f + p.Awake * 0.38f);
                p.Root.localScale = Vector3.one * (1f + p.Awake * 0.02f * Mathf.Sin(swayClock * 1.6f + p.Phase));
                if (p.Label != null)
                {
                    Color c = ready ? p.Active : (Color)new Color32(140, 172, 164, 255);
                    p.Label.color = c;
                    if (p.Level >= Campaign.VisibleLevels)
                        p.Label.text = hiddenUnlocked ? "隐藏关" : "???";
                    else
                        p.Label.text = p.Title;
                }
            }
        }

        void AnimatePearl()
        {
            if (pearlRoot == null || !pearlRoot.gameObject.activeSelf || pearlTaken) return;
            pearlRoot.anchoredPosition = CellPosition(37, 19) + new Vector2(0, Mathf.Sin(Time.unscaledTime * 1.8f) * 3.5f);
            float pulse = 0.72f + Mathf.Sin(Time.unscaledTime * 2.3f) * 0.22f;
            pearlGlow.color = new Color(1f, 0.82f, 0.42f, 0.25f * pulse);
            pearlRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 0.7f) * 8f);
        }

        void CheckPearlPickup()
        {
            if (pearlTaken || traveling || pearlRoot == null || !pearlRoot.gameObject.activeSelf) return;
            if (Vector2.Distance(fishPos, CellPosition(37, 19)) <= 36f)
                StartCoroutine(CollectPearl());
        }

        void AnimateBubbles(float dt)
        {
            for (int i = 0; i < bubbles.Count; i++)
            {
                var b = bubbles[i];
                b.Age += dt;
                if (b.Age > b.Life)
                {
                    b.Age = 0;
                    b.Life = 3.2f + (i % 5) * 0.35f;
                    b.Rect.anchoredPosition = b.Origin;
                    b.Image.color = new Color(0.82f, 1f, 0.96f, 0.30f);
                }
                var pos = b.Rect.anchoredPosition;
                pos.y += b.Speed * dt;
                pos.x = b.Origin.x + Mathf.Sin(b.Age * 2.2f + b.Phase) * 4f;
                b.Rect.anchoredPosition = pos;
                float a = Mathf.Clamp01(1f - b.Age / b.Life) * 0.34f;
                b.Image.color = new Color(0.82f, 1f, 0.96f, a);
            }
        }

        void SpawnTrailBubble()
        {
            for (int i = 0; i < bubbles.Count; i++)
            {
                if (bubbles[i].Age > bubbles[i].Life - 0.2f) continue;
                var b = bubbles[i];
                if (Vector2.Distance(b.Rect.anchoredPosition, fishPos) > 55f) continue;
                b.Age = b.Life - 0.9f;
                b.Origin = fishPos + new Vector2(-fishVel.x * 0.06f, -fishVel.y * 0.06f);
                b.Rect.anchoredPosition = b.Origin;
                break;
            }
        }

        // ------------------------------------------------------------- collision

        static Vector2 Center => new Vector2(.5f, .5f);
        static Vector2 LeftCenter => new Vector2(0, .5f);
        static Vector2 CenterBottom => new Vector2(.5f, 0);
        static Vector2 CenterTop => new Vector2(.5f, 1);

        Vector2 CellPosition(int x, int y)
        {
            return new Vector2((x - (W - 1) * .5f) * Cell, ((H - 1) * .5f - y) * Cell);
        }

        int GridX(float localX) => Mathf.FloorToInt(localX / Cell + W * .5f);
        int GridY(float localY) => Mathf.FloorToInt(H * .5f - localY / Cell);

        char Tile(int x, int y) => puzzle.Tile(x, y, level1Cleared);

        char BaseTile(int x, int y) => puzzle.BaseTile(x, y);

        bool IsRockCell(int x, int y) => puzzle.BaseTile(x, y) == '#';

        bool IsFreeDryCell(int x, int y) =>
            puzzle.BaseTile(x, y) == '.' && !puzzle.IsValveCell(x, y);

        bool IsDry(int x, int y) => BaseTile(x, y) == '.';

        bool IsWaterAt(Vector2 local)
        {
            return puzzle.IsWaterCell(GridX(local.x), GridY(local.y), level1Cleared);
        }

        bool CanFit(Vector2 local)
        {
            if (!IsWaterAt(local)) return false;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * .25f;
                if (!IsWaterAt(local + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * FishRadius)) return false;
            }
            return true;
        }

        void ResetFish(int returnLevel = -1)
        {
            fishPos = CellPosition(22, 16);

            // Coming out of a level should put the fish beside that level's
            // shrine, not back in the central start pool. The shrine itself is
            // on dry stone, so find the nearest swimmable cell to it.
            if (returnLevel >= 0)
            {
                Portal from = null;
                foreach (var p in portals)
                {
                    if (p.Level != returnLevel) continue;
                    from = p;
                    break;
                }
                if (from != null) fishPos = FindWaterSpawn(from.Position);
            }

            fishVel = Vector2.zero;
        }

        Vector2 FindWaterSpawn(Vector2 around)
        {
            int cx = GridX(around.x);
            int cy = GridY(around.y);
            for (int radius = 0; radius <= 6; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (radius > 0 && Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                            continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || x >= W || y < 0 || y >= H) continue;
                        Vector2 candidate = CellPosition(x, y);
                        if (CanFit(candidate)) return candidate;
                    }
                }
            }
            return around;
        }

        // ------------------------------------------------------------ interaction

        bool PortalReady(Portal p)
        {
            if (p.Level >= Campaign.VisibleLevels) return hiddenUnlocked;
            if (!pearlTaken) return false;
            return p.Level == 0 || GameSession.LevelsCleared >= p.Level;
        }

        void UpdatePrompt(float dt)
        {
            nearest = null;
            float best = 82f;
            foreach (var p in portals)
            {
                float d = Vector2.Distance(fishPos, p.Position);
                if (d < best) { best = d; nearest = p; }
            }
            bool show = nearest != null;
            promptAlpha = Mathf.MoveTowards(promptAlpha, show ? 1f : 0f, dt * 7f);
            promptGroup.alpha = promptAlpha;
            if (nearest != null)
            {
                bool ready = PortalReady(nearest);
                promptKey.text = ready ? "E" : "·";
                promptKey.color = ready ? Ink : Quiet;
                if (ready)
                    promptText.text = "与海草共鸣 · 进入「" + nearest.Title + " · " + nearest.Subtitle + "」";
                else if (!pearlTaken)
                    promptText.text = "沉睡的海草 · 先找到东南石室里的潮汐珍珠";
                else if (nearest.Level >= Campaign.VisibleLevels)
                    promptText.text = "石门紧闭 · 通关第一关后才会打开";
                else
                    promptText.text = "潮心尚未回应 · 先通关第 " + nearest.Level.ToString("00") + " 关";
            }
        }

        void Interact()
        {
            if (rotatingValve || nearest == null) return;
            if (!PortalReady(nearest))
            {
                if (!pearlTaken)
                    ShowToast("海草还没有回应。东南石室深处有一颗潮汐珍珠。");
                else if (nearest.Level >= Campaign.VisibleLevels)
                    ShowToast("石门纹丝不动。先通关第一关，让洞穴记住你的水路。");
                else
                    ShowToast("潮心还没回应。先通关第 " + nearest.Level.ToString("00") + " 关。");
                return;
            }
            StartCoroutine(Travel(nearest));
        }

        IEnumerator Travel(Portal portal)
        {
            traveling = true;
            fishVel = Vector2.zero;
            ShowToast("海草缠住小鱼，气泡把它送向「" + portal.Title + "」…", 2.4f);
            float t = 0;
            while (t < .78f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / .78f);
                fishRoot.localRotation = Quaternion.Euler(0, 0, k * 460f);
                fishRoot.localScale = Vector3.one * (1f - k * .7f);
                fishRoot.anchoredPosition = Vector2.Lerp(fishPos, portal.Position, k);
                yield return null;
            }
            yield return WaterFx.Close(.42f);
            WaterFx.GoToLevelAfterClose(portal.Level);
        }

        IEnumerator RevealDoorsOnReturn()
        {
            ShowToast("第一关通过 · 洞穴深处传来石门开启的轰鸣。", 4.4f);
            yield return new WaitForSeconds(1.2f);
            foreach (var gate in gates) StartCoroutine(OpenGate(gate));
        }

        IEnumerator ShowHiddenReturn()
        {
            ShowToast("隐藏关的回声留在水底。洞穴里每一株海草都记住了你。", 4.4f);
            yield return null;
        }

        IEnumerator FadeInRoutine()
        {
            fade.color = new Color(0.04f, 0.022f, 0.016f, 1f);
            float t = 0;
            while (t < .7f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .7f));
                fade.color = new Color(0.04f, 0.022f, 0.016f, 1f - k);
                yield return null;
            }
            fade.color = new Color(0.04f, 0.022f, 0.016f, 0f);
        }

        // ---------------------------------------------------------------- gates

        void SetGateClosed(Gate gate)
        {
            gate.Open = false;
            gate.Group.alpha = 1f;
            gate.Root.localScale = Vector3.one;
        }

        void OpenGateImmediate(Gate gate)
        {
            gate.Open = true;
            gate.Group.alpha = 0f;
            gate.Root.localScale = new Vector3(1, .05f, 1);
        }

        IEnumerator OpenGate(Gate gate)
        {
            gate.Open = true;
            float t = 0;
            while (t < .9f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .9f));
                gate.Group.alpha = 1f - k;
                gate.Root.localScale = new Vector3(1f, Mathf.Lerp(1f, .06f, k), 1f);
                gate.Root.anchoredPosition = gate.Base + new Vector2(0, k * 8f);
                if (t > .25f && t < .3f) Burst(gate.Base, new Color(0.60f, 0.92f, 0.86f, 1f));
                yield return null;
            }
            gate.Group.alpha = 0f;
            level1Cleared = true;
            RefreshWaterTiles();
            RefreshHud();
        }

        // ---------------------------------------------------------------- pearl

        IEnumerator CollectPearl()
        {
            pearlTaken = true;
            PlayerPrefs.SetInt(GameSession.HubPearlKey, 1);
            PlayerPrefs.Save();
            RefreshHud();
            ShowToast("潮汐珍珠回应了你。西北石室的海草醒了。", 4f);
            Vector2 from = pearlRoot.anchoredPosition;
            Vector2 to = CellPosition(7, 9);
            for (float t = 0; t < .9f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / .9f);
                pearlRoot.anchoredPosition = Vector2.Lerp(from, to, k);
                pearlRoot.localScale = Vector3.one * (1f - k * .75f);
                yield return null;
            }
            pearlRoot.gameObject.SetActive(false);
            Burst(to, new Color(0.55f, 1f, 0.76f, 1f));
        }

        void Burst(Vector2 at, Color color)
        {
            for (int i = 0; i < 7; i++)
            {
                var rt = Box(mapRoot, "Burst", at, new Vector2(10, 10), Center);
                var im = rt.gameObject.AddComponent<Image>();
                im.sprite = i % 2 == 0 ? bubbleSprite : ellipse;
                im.color = new Color(color.r, color.g, color.b, .8f);
                im.raycastTarget = false;
                StartCoroutine(FlyBubble(rt, im, at, new Vector2(
                    Mathf.Cos(i * 0.9f) * (24 + i * 5), Mathf.Sin(i * 0.9f) * (24 + i * 5)), .7f));
            }
        }

        IEnumerator FlyBubble(RectTransform rt, Image im, Vector2 from, Vector2 offset, float life)
        {
            float t = 0;
            while (t < life)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / life);
                rt.anchoredPosition = from + offset * k + new Vector2(0, k * 18f);
                im.color = new Color(im.color.r, im.color.g, im.color.b, (1f - k) * .8f);
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        // ------------------------------------------------------------------ hud

        void RefreshHud()
        {
            if (objective == null) return;
            if (hiddenCleared)
                objective.text = "隐藏关的回声已经归位。潮汐洞穴完全苏醒了。";
            else if (!pearlTaken)
                objective.text = "转动东南方向的水块，把水路接到潮汐珍珠所在的石室。";
            else if (GameSession.LevelsCleared >= Campaign.VisibleLevels)
                objective.text = "二十关已经全部完成。西南石室的隐藏关还在等你。";
            else
            {
                int next = Mathf.Clamp(GameSession.LevelsCleared, 0, Campaign.VisibleLevels - 1);
                string room = next < 4 ? "西北" : next < 8 ? "东北" : next < 12 ? "中央" :
                    next < 16 ? "西南" : "东南";
                objective.text = next >= 8 && next < 12
                    ? "中央石室的海草已经醒了。前往第 " + (next + 1).ToString("00") + " 关。"
                    : "转动水块，把水路引向" + room + "石室，前往第 " + (next + 1).ToString("00") + " 关。";
                if (hiddenUnlocked)
                    objective.text += "  隐藏关在西南。";
            }

            int reachable = pearlTaken
                ? Mathf.Clamp(GameSession.LevelsCleared + 1, 1, Campaign.VisibleLevels)
                : 0;
            int open = puzzle != null ? puzzle.OpenCount(level1Cleared) : 0;
            progress.text = "<color=#7cf0dc>水道 " + open + " / " + (puzzle != null ? puzzle.Valves.Length : 0) +
                            "</color>    " + Chip("潮汐珍珠", pearlTaken) + "    " +
                            "<color=#7cf0dc>可达 " + reachable + " / " + Campaign.VisibleLevels + "</color>" + "    " +
                            Chip("隐藏关", hiddenCleared);
            UpdateStatusLine();
        }

        static string Chip(string name, bool on)
        {
            return on
                ? "<color=#7cf0dc>◆ " + name + "</color>"
                : "<color=#547077>◇ " + name + "</color>";
        }

        void ShowToast(string message, float seconds = 3.4f)
        {
            if (toastText == null) return;
            toastText.text = message;
            toastTimer = seconds;
            toastGroup.alpha = 1f;
        }

        void UpdateToast(float dt)
        {
            if (toastTimer <= 0) return;
            toastTimer -= dt;
            if (toastTimer < .6f) toastGroup.alpha = Mathf.Clamp01(toastTimer / .6f);
        }

        // -------------------------------------------------------------- helpers

        RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        RectTransform Stretch(Transform parent, string name)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Center;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        static Image Img(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        Image PanelImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rt = Box(parent, name, position, size, new Vector2(0, 1));
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        Text Label(Transform parent, string name, string content, int size, Color color,
            TextAnchor align, Vector2 position, Vector2 box, FontStyle style = FontStyle.Normal)
        {
            return TextIn(parent, name, content, size, color, align,
                new Vector2(0, 1), new Vector2(0, 1), position, box, style);
        }

        Text CenterLabel(Transform parent, string name, string content, int size, Color color,
            TextAnchor align, Vector2 position, Vector2 box, FontStyle style = FontStyle.Normal)
        {
            return TextIn(parent, name, content, size, color, align,
                Center, Center, position, box, style);
        }

        Text TextIn(Transform parent, string name, string content, int size, Color color,
            TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 box,
            FontStyle style)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = box;
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // ------------------------------------------------------------ sprite art

        static bool IsWaterChar(char c) => c == '~' || c == '1' || c == '2' || c == 'H' || c == 'K' || c == '@' || IsGateChar(c);
        static bool IsGateChar(char c) => c == 'g' || c == 'x';
        static bool IsWalkable(char c) => IsWaterChar(c) || c == '.';

        static Sprite MakeSolid()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < 16; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
        }

        static Sprite CropSprite(Texture2D tex, int x, int yTop, int w, int h)
        {
            if (tex == null) return null;
            int y = tex.height - yTop - h;
            if (x < 0 || y < 0 || x + w > tex.width || y + h > tex.height) return null;
            return Sprite.Create(tex, new Rect(x, y, w, h), new Vector2(.5f, .5f), 16f);
        }

        static Sprite MakeRoundedWater(Sprite frame)
        {
            if (frame == null) return null;
            var src = frame.texture;
            if (src == null || !src.isReadable) return frame;
            int sw = src.width, sh = src.height;
            const int scale = 2;
            int w = sw * scale, h = sh * scale;
            var source = src.GetPixels32();
            var pixels = new Color32[w * h];
            float r = 6f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Max(r - x, x - (w - r), 0f);
                float dy = Mathf.Max(r - y, y - (h - r), 0f);
                if (dx * dx + dy * dy > r * r) continue;
                pixels[y * w + x] = source[(y / scale) * sw + (x / scale)];
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 16f);
        }

        static Texture2D MakeStrata()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float band = 0f;
                for (int i = 0; i < 7; i++)
                {
                    float baseY = 18 + i * 34;
                    float line = baseY + Mathf.Sin(x * 0.035f + i * 1.7f) * 8f + Mathf.Sin(x * 0.011f + i) * 5f;
                    float d = Mathf.Abs(y - line);
                    if (d < 3f) band = Mathf.Max(band, 1f - d / 3f);
                }
                if (band <= 0f) continue;
                pixels[y * size + x] = new Color32(255, 220, 180, (byte)(band * 90f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static Sprite MakeOrb()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - (size - 1) * .5f) / (size * .5f);
                float dy = (y - (size - 1) * .5f) / (size * .5f);
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - d, 1.8f);
                if (d < .24f) a = 1f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }

        static Sprite MakeRounded()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Max(0, Mathf.Abs(x - 31.5f) - 24);
                float cy = Mathf.Max(0, Mathf.Abs(y - 31.5f) - 24);
                if (cx * cx + cy * cy < 59) pixels[y * size + x] = new Color32(255, 255, 255, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(9, 9, 9, 9));
        }

        static Sprite MakeEllipse(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float r = size * .5f - 1;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - (size - 1) * .5f) / r;
                float dy = (y - (size - 1) * .5f) / r;
                if (dx * dx + dy * dy <= 1f) pixels[y * size + x] = new Color32(255, 255, 255, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }

        static Sprite MakeGlow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float r = size * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - r) / r, dy = (y - r) / r;
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                byte a = (byte)(Mathf.Pow(d, 2.4f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }

        static Sprite MakeRing(int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float r = size * .5f;
            float radius = size * .38f;
            float band = size * thickness;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - r, dy = y - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / band);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }

        static Sprite MakeLeaf()
        {
            const int w = 32, h = 86;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);
                float center = w * .5f + Mathf.Sin(t * Mathf.PI * 2.3f) * 4.2f;
                float half = Mathf.Lerp(5.8f, 1.2f, t);
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(x - center);
                    if (d > half) continue;
                    byte a = (byte)(255 * Mathf.Clamp01((half - d + 1f) / 2.2f));
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, 0f), 100);
        }

        static Sprite MakePearl()
        {
            const int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float r = size * .5f - 2;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - (size - 1) * .5f) / r;
                float dy = (y - (size - 1) * .5f) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) continue;
                float k = Mathf.Clamp01(1f - d);
                Color c = Color.Lerp(new Color32(255, 186, 88, 255), new Color32(255, 250, 226, 255), k);
                if (d > .82f) c = new Color32(236, 176, 92, 255);
                pixels[y * size + x] = c;
            }
            for (int y = -3; y <= 2; y++)
            for (int x = -4; x <= 2; x++)
                if (x * x + y * y < 9) pixels[(26 + y) * size + (20 + x)] = new Color32(255, 255, 246, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }

        static Sprite MakeVignette()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - size * .5f) / (size * .5f);
                float dy = (y - size * .5f) / (size * .5f);
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.15f);
                byte a = (byte)(Mathf.SmoothStep(0, 1, Mathf.Clamp01((d - .35f) / .62f)) * 220f);
                pixels[y * size + x] = new Color32(0, 0, 0, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }

        static Sprite MakeScrim(bool top)
        {
            const int w = 4, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = top ? y / (h - 1f) : 1f - y / (h - 1f);
                byte a = (byte)(Mathf.Pow(Mathf.Clamp01(t), 1.7f) * 255f);
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100);
        }

        static Sprite MakeShaft()
        {
            const int w = 64, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float edge = 1f - Mathf.Abs(x - (w - 1) * .5f) / (w * .5f);
                float top = Mathf.Clamp01(1f - y / (h * .92f));
                byte a = (byte)(Mathf.Clamp01(edge * edge * top * 1.4f) * 255f);
                pixels[y * w + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), w);
        }

        static Texture2D MakeSand()
        {
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;
            var rng = new System.Random(9182);
            var baseColor = new Color32(44, 55, 61, 255);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int n = rng.Next(-8, 9);
                var c = new Color32(
                    (byte)Mathf.Clamp(baseColor.r + n, 0, 255),
                    (byte)Mathf.Clamp(baseColor.g + n, 0, 255),
                    (byte)Mathf.Clamp(baseColor.b + n, 0, 255), 255);
                if (rng.NextDouble() < .04) c = new Color32(73, 70, 57, 255);
                if (rng.NextDouble() < .025) c = new Color32(31, 42, 48, 255);
                pixels[y * size + x] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ---------------------------------------------------------------- models

        sealed class Portal
        {
            public int Level;
            public Vector2 Position;
            public string Title, Subtitle;
            public RectTransform Root;
            public Image Halo, Ring, Core;
            public Text Label;
            public Color Active, HaloColor;
            public readonly List<Image> Leaves = new List<Image>();
            public readonly List<float> LeafAngles = new List<float>();
            public float Phase, Awake;
        }

        sealed class Gate
        {
            public int X, Y;
            public RectTransform Root;
            public CanvasGroup Group;
            public Vector2 Base;
            public bool Open;
            public readonly List<Image> Slabs = new List<Image>();
            public readonly List<Text> Texts = new List<Text>();
        }

        sealed class Bubble
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Origin;
            public float Age, Life, Speed, Phase;
        }

        sealed class WaterTile
        {
            public int X, Y;
            public Image Image;
            public int Mask;
        }

        sealed class ValveView
        {
            public HubPuzzle.Valve Valve;
            public RectTransform Root;
            public Image Glow;
            public readonly List<Image> Brackets = new List<Image>();
            public readonly List<Image> SideDots = new List<Image>();
            public readonly List<Image> Preview = new List<Image>();
            public Text Glyph;
        }

        struct HubSnapshot
        {
            public int[] States;
            public Vector2 Fish;
            public int Turns;
        }

#if UNITY_EDITOR
        // Test hooks used by the pipeline during play-mode screenshots.
        public void DebugGotoPortal(int index)
        {
            if (index < 0 || index >= portals.Count) return;
            var p = portals[index];
            fishPos = p.Position - new Vector2(Cell * 2.2f, 0);
            if (!CanFit(fishPos)) fishPos = p.Position;
            fishVel = Vector2.zero;
        }

        public void DebugGotoPearl()
        {
            fishPos = CellPosition(37, 19) - new Vector2(Cell * 1.8f, 0);
            if (!CanFit(fishPos)) fishPos = CellPosition(37, 19);
            fishVel = Vector2.zero;
            if (!pearlTaken) StartCoroutine(CollectPearl());
        }

        public void DebugOpenDoors()
        {
            level1Cleared = true;
            hiddenUnlocked = true;
            foreach (var gate in gates)
            {
                gate.Open = true;
                OpenGateImmediate(gate);
            }
            RefreshWaterTiles();
            RefreshHud();
        }

        public void DebugTravelTo(int level)
        {
            GameSession.EnterLevel(level);
        }

        public void DebugSetRotateDuration(float seconds)
        {
            valveRotateDuration = Mathf.Clamp(seconds, .05f, 20f);
        }

        public void DebugRotateValve(int index)
        {
            if (index < 0 || index >= puzzle.Valves.Length) return;
            puzzle.Rotate(puzzle.Valves[index]);
            RefreshWaterTiles();
            RefreshHud();
        }

        public void DebugSetValve(int index, int state)
        {
            if (index < 0 || index >= puzzle.Valves.Length) return;
            puzzle.Valves[index].State = ((state % 4) + 4) % 4;
            RefreshWaterTiles();
            RefreshHud();
        }

        public void DebugSolveValves()
        {
            puzzle.Solve();
            RefreshWaterTiles();
            RefreshHud();
        }

        public void DebugResetValves()
        {
            puzzle.Reset();
            RefreshWaterTiles();
            RefreshHud();
        }

        public void DebugGotoCell(int x, int y)
        {
            fishPos = CellPosition(x, y);
            fishVel = Vector2.zero;
            RefreshWaterTiles();
            UpdateKeyboardValve();
            focusedValve = keyboardValve;
            UpdateStatusLine();
        }

        public int DebugValveCount => puzzle != null ? puzzle.Valves.Length : 0;

        public int DebugTurnCount => turnCount;

        public int DebugUndoCount => undoStack.Count;

        public void DebugRotateFocused()
        {
            RotateFocusedValve();
        }

        public void DebugUndo()
        {
            UndoValve();
        }

        public void DebugHint()
        {
            HintValve();
        }

        public void DebugRestart()
        {
            RestartValvePuzzle();
        }

        /// <summary>Same hit test the mouse uses, but fed a screen point.</summary>
        public bool DebugClickScreen(float x, float y)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mapRoot, new Vector2(x, y), null, out Vector2 local)) return false;
            var valve = puzzle.ValveAt(GridX(local.x), GridY(local.y));
            if (valve == null) return false;
            TryRotateValve(ValveViewFor(valve));
            return true;
        }

        public string DebugValveAtScreen(float x, float y)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mapRoot, new Vector2(x, y), null, out Vector2 local)) return "none";
            var valve = puzzle.ValveAt(GridX(local.x), GridY(local.y));
            if (valve == null) return "none";
            return "valve " + System.Array.IndexOf(puzzle.Valves, valve) +
                   " state " + valve.State + " link " + valve.LinkLabel;
        }

        public string DebugValveScreenPoints()
        {
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < puzzle.Valves.Length; i++)
            {
                var p = RectTransformUtility.WorldToScreenPoint(null,
                    mapRoot.TransformPoint(ValveCenter(puzzle.Valves[i])));
                builder.Append(i).Append(':').Append(Mathf.RoundToInt(p.x))
                    .Append(',').Append(Mathf.RoundToInt(p.y)).Append(' ');
            }
            return builder.ToString();
        }

        public string DebugReachReport()
        {
            var start = new Vector2Int(GridX(fishPos.x), GridY(fishPos.y));
            var seen = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            if (!puzzle.IsWaterCell(start.x, start.y, level1Cleared)) return "fish not in water";
            seen.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in new[]
                         {
                             new Vector2Int(1, 0), new Vector2Int(-1, 0),
                             new Vector2Int(0, 1), new Vector2Int(0, -1)
                         })
                {
                    var n = p + d;
                    if (seen.Contains(n)) continue;
                    if (!puzzle.IsWaterCell(n.x, n.y, level1Cleared)) continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            return "reachable " + seen.Count + " cells from " + start +
                   "; NW " + (seen.Contains(new Vector2Int(6, 8)) || seen.Contains(new Vector2Int(7, 8)) ||
                              seen.Contains(new Vector2Int(6, 9)) || seen.Contains(new Vector2Int(7, 9))) +
                   "; NE " + (seen.Contains(new Vector2Int(36, 8)) || seen.Contains(new Vector2Int(37, 8)) ||
                              seen.Contains(new Vector2Int(36, 9)) || seen.Contains(new Vector2Int(37, 9))) +
                   "; SW " + (seen.Contains(new Vector2Int(6, 18)) || seen.Contains(new Vector2Int(7, 18)) ||
                              seen.Contains(new Vector2Int(6, 19)) || seen.Contains(new Vector2Int(7, 19))) +
                   "; SE " + (seen.Contains(new Vector2Int(36, 18)) || seen.Contains(new Vector2Int(37, 18)) ||
                              seen.Contains(new Vector2Int(36, 19)) || seen.Contains(new Vector2Int(37, 19)));
        }

        public string DebugFishCell()
        {
            return GridX(fishPos.x) + "," + GridY(fishPos.y);
        }

        public string DebugValveState(int index)
        {
            if (puzzle == null || index < 0 || index >= puzzle.Valves.Length) return "?";
            var valve = puzzle.Valves[index];
            return valve.State + " (" + HubPuzzle.SidePair(valve.State) + ")";
        }
#endif
    }
}

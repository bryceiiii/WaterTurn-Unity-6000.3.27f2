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
        const float FishRadius = 10.5f;
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

        static readonly string[] Map =
        {
            "#############################################",
            "#############################################",
            "#############################################",
            "#############################################",
            "#############################################",
            "#############################################",
            "#########.......................#############",
            "####............................##.......####",
            "####..#~~..#######..##############..#~~..####",
            "####..~1~..#######..##############..~2~..####",
            "####..~~#~~~~~~~~#..##############..~~#..#..#",
            "####...~~~~~~~~~~#......########~~~~~~...#..#",
            "####..#########~~#......########gg~~~~##....#",
            "####..#######..~~~~~~~~~########gg######....#",
            "####...######..~~~~~~~~~..##~~~~~~########..#",
            "####...######..####..#~~..##~~~~~#########..#",
            "#############..####..~@~~~~~~~############..#",
            "####.......########..~~#~~~~~#####..........#",
            "####..#~~..####~~~~~~~~~~~~~~~~~##..#~~....##",
            "####..~H~..####xx~~~~~~~~~~~~~~~....~K~..####",
            "####..~~~..####xx#############~~....~~~..####",
            "####...~~~~~~~~~~#############~~~~~~~~...####",
            "#######~~~~~~~~~##############~~~~~~~~#######",
            "#######################################.....#",
            "#######################################.....#",
            "#############..###############..............#",
            "#############..............................##",
        };

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
        Texture2D[] tunnelTextures;
        Sprite[] tunnelSprites;
        Text objective, progress, promptText, promptKey, toastText, zoomReadout;
        CanvasGroup promptGroup, toastGroup;

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
        int waterFrame = -1;
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
            BuildArt();
            BuildInterface();
            BuildMap();
            ResetFish(returnLevel);
            SnapCamera();
            RefreshHud();
            StartCoroutine(FadeInRoutine());
            if (justCompletedFirst) StartCoroutine(RevealDoorsOnReturn());
            else if (justCompletedHidden) StartCoroutine(ShowHiddenReturn());
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
            backdrop.gameObject.AddComponent<Image>().color = new Color32(10, 6, 5, 255);

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
            tint.gameObject.AddComponent<Image>().color = new Color(0.28f, 0.12f, 0.05f, 0.06f);
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

            var title = Label(hud, "Title", "潮汐洞穴", 46, White, TextAnchor.MiddleLeft,
                new Vector2(42, -24), new Vector2(620, 58), FontStyle.Bold);
            title.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.65f);
            Label(hud, "Subtitle", "TIDAL HOLLOW  ·  序章", 15, Teal, TextAnchor.MiddleLeft,
                new Vector2(44, -82), new Vector2(620, 24), FontStyle.Bold);
            Label(hud, "Deck", "被水淹没的旧矿道，五座石室藏着二十处潮心。", 14, Quiet,
                TextAnchor.MiddleLeft, new Vector2(44, -106), new Vector2(720, 22));

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

            Label(hud, "Controls", "WASD / 方向键  游动        E / 空格  与海草共鸣", 14, Quiet,
                TextAnchor.MiddleLeft, new Vector2(42, -734), new Vector2(640, 24));
            Label(hud, "Footer", "小鱼只能在水里游  ·  被水淹没的矿道通向关卡", 12, new Color32(166, 128, 105, 255),
                TextAnchor.MiddleRight, new Vector2(930, -734), new Vector2(308, 24));
            zoomReadout = Label(hud, "ZoomReadout", "", 12, new Color32(196, 158, 126, 255),
                TextAnchor.MiddleRight, new Vector2(930, -710), new Vector2(308, 20));

            promptRoot = Box(hud, "Prompt", new Vector2(385, -664), new Vector2(510, 64), new Vector2(0, 1));
            var promptBack = promptRoot.gameObject.AddComponent<Image>();
            promptBack.sprite = rounded;
            promptBack.type = Image.Type.Sliced;
            promptBack.color = new Color32(46, 29, 24, 245);
            promptGroup = promptRoot.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0;
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
            if (tunnelSprites == null)
            {
                BuildTunnelFrames(tw, th);
            }
            tunnelImage = Img(mapRoot, "TunnelCanvas", tunnelSprites[0], Color.white,
                Vector2.zero, new Vector2(tw, th));
            waterFrame = 0;
        }

        void BuildTunnelFrames(int tw, int th)
        {
            var rockPixels = rockTexture != null ? rockTexture.GetPixels32() : null;
            var floorPixels = floorSprite != null && floorSprite.texture != null
                ? floorSprite.texture.GetPixels32()
                : null;
            var floorRect = floorSprite != null ? floorSprite.textureRect : new Rect(0, 0, 1, 1);
            var waterPixels = new Color32[waterFrames.Length][];
            for (int i = 0; i < waterFrames.Length; i++)
                waterPixels[i] = waterFrames[i].texture != null
                    ? waterFrames[i].texture.GetPixels32()
                    : null;

            int frameCount = waterPixels.Length == 0 ? 1 : Mathf.Clamp(waterPixels.Length, 1, 4);
            tunnelTextures = new Texture2D[frameCount];
            tunnelSprites = new Sprite[frameCount];
            for (int f = 0; f < frameCount; f++)
            {
                var pixels = new Color32[tw * th];
                for (int sy = 0; sy < th; sy++)
                {
                    int mapY = th - 1 - sy;
                    float fy = (mapY + .5f) / Cell;
                    for (int x = 0; x < tw; x++)
                    {
                        float fx = (x + .5f) / Cell;
                        int cx = (int)fx, cy = (int)fy;
                        char c = Tile(cx, cy);
                        if (!IsWalkable(c)) continue;

                        float dist = DistanceToRock(fx, fy)
                                     + Mathf.Sin(fx * 2.9f + fy * 1.7f) * .035f
                                     + Mathf.Sin(fx * 1.1f - fy * 3.3f) * .025f;
                        float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dist / .38f));
                        if (edge <= .01f) continue;

                        bool water = IsWaterChar(c);
                        Color col;
                        if (water)
                        {
                            var src = waterPixels.Length > 0 ? waterPixels[f % waterPixels.Length] : null;
                            if (src != null && src.Length > 0)
                            {
                                int sw = waterFrames[f % waterFrames.Length].texture.width;
                                int sh = waterFrames[f % waterFrames.Length].texture.height;
                                var p = src[((sy / 2) % sh) * sw + ((x / 2) % sw)];
                                col = p;
                            }
                            else col = new Color(0.18f, 0.52f, 0.60f, 1f);
                            col = new Color(col.r * .26f, col.g * .58f, col.b * .68f, 1f);
                            float wave = Mathf.Sin(x * .038f + sy * .031f + f * 1.7f)
                                         + Mathf.Sin(x * .011f - sy * .027f + f * .8f) * .5f;
                            if (wave > .62f) col += new Color(.045f, .075f, .075f, 0f);
                            float depth = Mathf.Clamp01((dist - .20f) / .45f);
                            col = Color.Lerp(new Color(.035f, .11f, .14f), col, .55f + depth * .45f);

                            float dryDist = DistanceToDry(fx, fy);
                            if (dryDist < .14f)
                            {
                                float shore = Mathf.Clamp01(dryDist / .14f);
                                col = Color.Lerp(new Color(.04f, .13f, .15f), col, shore);
                                if (dryDist > .07f && dryDist < .11f)
                                    col += new Color(.04f, .10f, .09f, 0f);
                            }
                        }
                        else
                        {
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
                                col = Color.Lerp(col, cave, .34f);
                            }
                            col = new Color(
                                Mathf.Clamp01(col.r * 1.16f + .065f),
                                Mathf.Clamp01(col.g * .84f + .040f),
                                Mathf.Clamp01(col.b * .67f + .028f), 1f);
                            float grain = ((x * 13 + sy * 7) % 11 - 5) * .006f;
                            col += new Color(grain, grain * .7f, grain * .5f, 0f);
                        }

                        float rim = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dist - .20f) / .17f));
                        col = Color.Lerp(col, new Color(.055f, .033f, .025f), rim * .78f);
                        if (rim > .35f && rim < .62f)
                            col += new Color(.055f, .032f, .020f, 0f);
                        col.a = edge;
                        pixels[sy * tw + x] = col;
                    }
                }

                var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
                tex.SetPixels32(pixels);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tunnelTextures[f] = tex;
                tunnelSprites[f] = Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(.5f, .5f), 100);
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
                if (IsWalkable(Tile(x, y))) continue;
                float ddx = Mathf.Max(x - fx, fx - (x + 1), 0f);
                float ddy = Mathf.Max(y - fy, fy - (y + 1), 0f);
                best = Mathf.Min(best, Mathf.Sqrt(ddx * ddx + ddy * ddy));
            }
            return best;
        }

        float DistanceToDry(float fx, float fy)
        {
            float best = 9f;
            int cx = (int)fx, cy = (int)fy;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (IsWaterChar(Tile(x, y))) continue;
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
                if (Tile(rocks[i].x, rocks[i].y) == '#')
                    AddProp(rockPropSprite, rocks[i].x, rocks[i].y, i, 0.75f, 0.75f, 0.55f);

            var pots = new[] { new Vector2Int(2, 13), new Vector2Int(43, 21), new Vector2Int(24, 2) };
            for (int i = 0; i < pots.Length; i++)
                if (Tile(pots[i].x, pots[i].y) == '#')
                    AddProp(potSprite, pots[i].x, pots[i].y, i + 10, 0.80f, 0.80f, 0.62f);

            if (Tile(23, 3) == '#')
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
                if (Tile(dryRocks[i].x, dryRocks[i].y) == '.')
                    AddProp(i % 3 == 0 ? potSprite : rockPropSprite, dryRocks[i].x, dryRocks[i].y,
                        i + 30, i % 3 == 0 ? 0.65f : 0.55f, 0.62f, 0.48f);

            for (int i = 0; i < 5; i++)
            {
                int x = 3 + i * 9, y = 2 + (i % 2) * 20;
                if (Tile(x, y) != '#') continue;
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
                if (Tile(mushrooms[i].x, mushrooms[i].y) == '.')
                    AddMushroomPatch(mushrooms[i].x, mushrooms[i].y, i);

            var crystals = new[]
            {
                new Vector2Int(3, 11), new Vector2Int(10, 7), new Vector2Int(34, 7), new Vector2Int(40, 11),
                new Vector2Int(20, 17), new Vector2Int(24, 17), new Vector2Int(3, 18), new Vector2Int(10, 21),
                new Vector2Int(34, 21), new Vector2Int(40, 18),
            };
            for (int i = 0; i < crystals.Length; i++)
                if (Tile(crystals[i].x, crystals[i].y) == '.')
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
            AddGate(32, 12, 'g');
            AddGate(15, 19, 'x');
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
                new Vector2Int(22, 16), new Vector2Int(15, 13), new Vector2Int(8, 10),
                new Vector2Int(37, 9), new Vector2Int(37, 19), new Vector2Int(10, 20),
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
            AnimateWater();
            AnimatePortals(dt);
            AnimateBubbles(dt);
            AnimatePearl();
            UpdateToast(dt);
            if (traveling) return;
            HandleInput(dt);
            UpdateFish(dt);
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
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1;
                if (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) Interact();
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

        void AnimateWater()
        {
            if (tunnelImage == null || tunnelSprites == null || tunnelSprites.Length == 0) return;
            int tick = Mathf.FloorToInt(Time.unscaledTime * 5f);
            if (tick % tunnelSprites.Length != waterFrame)
            {
                waterFrame = tick % tunnelSprites.Length;
                tunnelImage.sprite = tunnelSprites[waterFrame];
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

        char Tile(int x, int y)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return '#';
            return Map[y][x];
        }

        bool IsDry(int x, int y) => Tile(x, y) == '.';

        bool IsWaterAt(Vector2 local)
        {
            char c = Tile(GridX(local.x), GridY(local.y));
            if (IsGateChar(c)) return level1Cleared;
            return c == '~' || c == '1' || c == '2' || c == 'H' || c == 'K' || c == '@';
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
            if (nearest == null) return;
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
                fade.color = new Color(0.04f, 0.022f, 0.016f, k);
                fishRoot.localRotation = Quaternion.Euler(0, 0, k * 460f);
                fishRoot.localScale = Vector3.one * (1f - k * .7f);
                fishRoot.anchoredPosition = Vector2.Lerp(fishPos, portal.Position, k);
                yield return null;
            }
            GameSession.EnterLevel(portal.Level);
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
            else if (hiddenUnlocked)
                objective.text = "石门已经打开。按顺序挑战潮心，或进入隐藏关。";
            else if (pearlTaken)
                objective.text = "海草醒了。去西北石室，从第一关开始。";
            else
                objective.text = "找到沉在东南石室里的潮汐珍珠。";

            int reachable = pearlTaken
                ? Mathf.Clamp(GameSession.LevelsCleared + 1, 1, Campaign.VisibleLevels)
                : 0;
            progress.text = Chip("潮汐珍珠", pearlTaken) + "    " +
                             "<color=#7cf0dc>可达 " + reachable + " / " + Campaign.VisibleLevels + "</color>" + "    " +
                             Chip("隐藏关", hiddenCleared);
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
            RefreshHud();
        }

        public void DebugTravelTo(int level)
        {
            GameSession.EnterLevel(level);
        }
#endif
    }
}

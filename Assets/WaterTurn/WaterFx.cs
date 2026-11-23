using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// The presentation layer between chambers. It lives on a DontDestroyOnLoad
    /// canvas, so it survives the scene load each level owns, and it draws:
    ///
    /// · the hard-edged iris that carries every level change (circle anchored a
    ///   third of the way down the screen),
    /// · the death blast (cold flash + shock ring + diamond-grid dissolve + pixel
    ///   debris), and
    /// · the clear card (teal cloud frame, backdrop darkened and softly blurred).
    ///
    /// Everything is a runtime Mesh or a generated sprite: no prefab, no extra
    /// art, no post-processing volume.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class WaterFx : MonoBehaviour
    {
        const int RefWidth = 1280, RefHeight = 800;
        public const float IrisSeconds = .6f;
        public const float BlackHoldSeconds = .3f;

        static WaterFx instance;

        public static WaterFx Instance
        {
            get
            {
                if (instance == null) Create();
                return instance;
            }
        }

        /// <summary>True when a level/scene change will open the iris on load.</summary>
        public static bool PendingIrisOpen => instance != null && instance.pendingOpen;

        static WaterFx Create()
        {
            if (instance != null) return instance;
            var go = new GameObject("WaterFx");
            DontDestroyOnLoad(go);
            go.AddComponent<WaterFx>();
            return instance;
        }

        Canvas canvas;
        RectTransform canvasRect;
        IrisGraphic iris;
        Image blackout, flash, ring;
        DiamondGraphic dissolve;
        RectTransform debrisLayer;
        readonly List<Debris> debris = new List<Debris>();

        Image dimmer;
        RawImage blurImage;
        Texture2D blurTexture;
        RectTransform cardRoot;
        CanvasGroup cardGroup;
        Text cardTitle, cardSubtitle;
        readonly List<Text> cardLines = new List<Text>();
        bool confirmed;
        Button cardButton;

        bool transitioning;
        bool pendingOpen;

        /// <summary>
        /// Presentation-only speed knob (1 = real time). Lower it to inspect a
        /// transition frame by frame from the editor; gameplay never reads it.
        /// </summary>
        public static float DebugSpeed = 1f;

        static float Dt => Time.unscaledDeltaTime * Mathf.Max(.01f, DebugSpeed);

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Build();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        // ------------------------------------------------------------- building

        void Build()
        {
            var canvasObject = new GameObject("WaterFxCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.matchWidthOrHeight = .5f;
            canvasRect = (RectTransform)canvasObject.transform;

            // Win backdrop: blurred snapshot, then a dark wash over it.
            var blurGo = new GameObject("ClearBlur", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage));
            var blurRect = (RectTransform)blurGo.transform;
            blurRect.SetParent(canvasRect, false);
            blurRect.anchorMin = Vector2.zero;
            blurRect.anchorMax = Vector2.one;
            blurRect.offsetMin = Vector2.zero;
            blurRect.offsetMax = Vector2.zero;
            blurImage = blurGo.GetComponent<RawImage>();
            blurImage.raycastTarget = false;
            blurImage.enabled = false;
            blurImage.color = new Color(1f, 1f, 1f, 0f);
            dimmer = FullImage("ClearDim", new Color(.03f, .07f, .08f, 0f), false).GetComponent<Image>();
            dimmer.raycastTarget = false;

            dissolve = FullGraphic<DiamondGraphic>("DiamondDissolve");
            dissolve.raycastTarget = false;
            dissolve.enabled = false;

            iris = FullGraphic<IrisGraphic>("Iris");
            iris.raycastTarget = false;
            iris.enabled = false;
            iris.color = new Color32(5, 8, 10, 255);

            blackout = FullImage("Blackout", new Color32(5, 8, 10, 255), false).GetComponent<Image>();
            blackout.raycastTarget = false;
            blackout.enabled = false;

            BuildCard();

            flash = FullImage("Flash", new Color(0.82f, 0.95f, 1f, 0f), false).GetComponent<Image>();
            flash.raycastTarget = false;
            flash.enabled = false;

            ring = FullImage("ShockRing", Color.white, false).GetComponent<Image>();
            ring.sprite = MakeRing(128, .055f);
            var ringRect = ring.rectTransform;
            ringRect.anchorMin = ringRect.anchorMax = new Vector2(.5f, .5f);
            ringRect.pivot = new Vector2(.5f, .5f);
            ringRect.sizeDelta = new Vector2(260, 260);
            ring.raycastTarget = false;
            ring.enabled = false;

            debrisLayer = NewLayer("Debris");
        }

        Image FullImage(string name, Color color, bool block)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRect, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = block;
            return image;
        }

        T FullGraphic<T>(string name) where T : MaskableGraphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRect, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go.GetComponent<T>();
        }

        RectTransform NewLayer(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRect, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        void BuildCard()
        {
            cardRoot = NewLayer("ClearCard");
            cardRoot.anchorMin = cardRoot.anchorMax = new Vector2(.5f, .5f);
            cardRoot.pivot = new Vector2(.5f, .5f);
            cardRoot.sizeDelta = new Vector2(560, 380);
            cardRoot.anchoredPosition = Vector2.zero;
            cardGroup = cardRoot.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;

            var cloudGo = new GameObject("Cloud", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var cloud = (RectTransform)cloudGo.transform;
            cloud.SetParent(cardRoot, false);
            cloud.anchorMin = cloud.anchorMax = new Vector2(.5f, .5f);
            cloud.pivot = new Vector2(.5f, .5f);
            cloud.sizeDelta = new Vector2(560, 380);
            var cloudImage = cloudGo.GetComponent<Image>();
            cloudImage.sprite = MakeCloudSprite(560, 380, new Color32(118, 226, 232, 255),
                new Color32(32, 96, 104, 255));
            cloudImage.raycastTarget = true;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.SetParent(cardRoot, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
            panelRect.pivot = new Vector2(.5f, .5f);
            panelRect.sizeDelta = new Vector2(500, 320);
            panelRect.anchoredPosition = Vector2.zero;
            var panel = panelGo.GetComponent<Image>();
            panel.color = new Color32(18, 30, 35, 246);
            panel.sprite = MakeRoundedSprite();
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;

            var font = Font.CreateDynamicFontFromOSFont(
                new[] {"Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial"}, 28);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            cardTitle = NewText(cardRoot, "Title", "", 34, new Color32(240, 253, 252, 255),
                new Vector2(0, 112), new Vector2(460, 52), TextAnchor.MiddleCenter, FontStyle.Bold, font);
            cardSubtitle = NewText(cardRoot, "Subtitle", "", 16, new Color32(118, 226, 232, 255),
                new Vector2(0, 78), new Vector2(460, 26), TextAnchor.MiddleCenter, FontStyle.Bold, font);

            for (int i = 0; i < 3; i++)
            {
                var line = NewText(cardRoot, "Stat " + i, "", 18, new Color32(214, 238, 235, 255),
                    new Vector2(-10, 30 - i * 44), new Vector2(400, 30), TextAnchor.MiddleLeft,
                    FontStyle.Normal, font);
                cardLines.Add(line);
            }

            var buttonGo = new GameObject("Continue", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            var buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.SetParent(cardRoot, false);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.5f, .5f);
            buttonRect.pivot = new Vector2(.5f, .5f);
            buttonRect.sizeDelta = new Vector2(250, 48);
            buttonRect.anchoredPosition = new Vector2(0, -118);
            var buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.sprite = MakeRoundedSprite();
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = new Color32(118, 226, 232, 255);
            buttonImage.raycastTarget = true;
            cardButton = buttonGo.GetComponent<Button>();
            cardButton.targetGraphic = buttonImage;
            cardButton.onClick.AddListener(() => confirmed = true);
            NewText(buttonRect, "Label", "回到潮汐洞穴  ↵", 17, new Color32(14, 34, 38, 255),
                Vector2.zero, new Vector2(230, 40), TextAnchor.MiddleCenter, FontStyle.Bold, font);

            NewText(cardRoot, "Hint", "Enter / 空格 / 点击继续", 12,
                new Color32(150, 190, 190, 220), new Vector2(0, -152), new Vector2(300, 20),
                TextAnchor.MiddleCenter, FontStyle.Normal, font);

            cardRoot.gameObject.SetActive(false);
        }

        static Text NewText(Transform parent, string name, string value, int size, Color color,
            Vector2 position, Vector2 box, TextAnchor anchor, FontStyle style, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = box;
            rect.anchoredPosition = position;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // ----------------------------------------------------------- iris

        Vector2 IrisCenter => new Vector2(0f, canvasRect.rect.height / 6f);

        float IrisMaxRadius =>
            Mathf.Sqrt(canvasRect.rect.width * canvasRect.rect.width +
                       canvasRect.rect.height * canvasRect.rect.height) * .5f + 12f;

        IEnumerator IrisCloseRoutine(float seconds)
        {
            iris.enabled = true;
            iris.center = IrisCenter;
            float max = IrisMaxRadius;
            for (float t = 0f; t < seconds; t += Dt)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                iris.radius = Mathf.Lerp(max, 0f, k);
                iris.SetVerticesDirty();
                yield return null;
            }
            iris.radius = 0f;
            iris.SetVerticesDirty();
        }

        IEnumerator IrisOpenRoutine(float seconds)
        {
            iris.enabled = true;
            iris.center = IrisCenter;
            float max = IrisMaxRadius;
            for (float t = 0f; t < seconds; t += Dt)
            {
                float k = EaseOutCubic(Mathf.Clamp01(t / seconds));
                iris.radius = max * k;
                iris.SetVerticesDirty();
                yield return null;
            }
            iris.enabled = false;
        }

        // ------------------------------------------------------ scene routes

        public static IEnumerator Close(float seconds = IrisSeconds) =>
            Instance.IrisCloseRoutine(seconds);

        /// <summary>Iris in → black hold → load the level → iris out.</summary>
        public static void GoToLevel(int level)
        {
            var fx = Instance;
            if (fx.transitioning) return;
            fx.StartCoroutine(fx.GoRoutine(() => GameSession.EnterLevel(level)));
        }

        public static void GoToHub(int fromLevel)
        {
            var fx = Instance;
            if (fx.transitioning) return;
            fx.StartCoroutine(fx.GoRoutine(() => GameSession.EnterHub(fromLevel)));
        }

        /// <summary>Restart the current level with a full iris blink.</summary>
        public static void RestartLevelWithIris()
        {
            var fx = Instance;
            if (fx.transitioning) return;
            fx.StartCoroutine(fx.GoRoutine(() => LevelManager.Instance.RestartLevel()));
        }

        /// <summary>
        /// The screen is already black (end of the death blast); just load and
        /// let the iris open on the fresh scene.
        /// </summary>
        public static void RestartLevelUnderBlack()
        {
            var fx = Instance;
            if (fx.transitioning) return;
            fx.StartCoroutine(fx.UnderBlackRoutine(() => LevelManager.Instance.RestartLevel()));
        }

        /// <summary>The caller already closed the iris; hold, then load and open.</summary>
        public static void GoToLevelAfterClose(int level)
        {
            var fx = Instance;
            if (fx.transitioning) return;
            fx.StartCoroutine(fx.UnderBlackRoutine(() => GameSession.EnterLevel(level)));
        }

        IEnumerator GoRoutine(Action load)
        {
            transitioning = true;
            ResetOverlays();
            yield return IrisCloseRoutine(IrisSeconds);
            blackout.enabled = true;
            blackout.color = new Color32(5, 8, 10, 255);
            iris.enabled = false;
            yield return new WaitForSecondsRealtime(BlackHoldSeconds);
            pendingOpen = true;
            load();
        }

        IEnumerator UnderBlackRoutine(Action load)
        {
            transitioning = true;
            ResetOverlays();
            blackout.enabled = true;
            blackout.color = new Color32(5, 8, 10, 255);
            iris.enabled = true;
            iris.radius = 0f;
            iris.SetVerticesDirty();
            yield return new WaitForSecondsRealtime(BlackHoldSeconds);
            pendingOpen = true;
            load();
        }

        void ResetOverlays()
        {
            dissolve.enabled = false;
            flash.enabled = false;
            ring.enabled = false;
            blackout.enabled = false;
            HideCard();
            ClearDebris();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!pendingOpen)
            {
                transitioning = false;
                return;
            }
            pendingOpen = false;
            StartCoroutine(OpenOnLoadRoutine());
        }

        IEnumerator OpenOnLoadRoutine()
        {
            blackout.enabled = false;
            iris.enabled = true;
            iris.center = IrisCenter;
            iris.radius = 0f;
            iris.SetVerticesDirty();
            yield return null;
            yield return IrisOpenRoutine(IrisSeconds);
            transitioning = false;
        }

        // -------------------------------------------------------- death blast

        /// <summary>Cold flash, shock ring, diamond-grid shatter and pixel debris.</summary>
        public IEnumerator DeathBlast(Vector2 screenPoint)
        {
            Vector2 focus = ToCanvas(screenPoint);
            dissolve.focus = focus;
            dissolve.progress = 0f;
            dissolve.color = new Color32(5, 8, 10, 255);
            dissolve.enabled = true;
            dissolve.SetVerticesDirty();

            flash.color = new Color(0.84f, 0.96f, 1f, 0f);
            flash.enabled = true;
            ring.enabled = true;
            ring.rectTransform.anchoredPosition = focus;
            ring.rectTransform.localScale = Vector3.one * .25f;
            SpawnDebris(focus, 46);

            float total = .86f;
            for (float t = 0f; t < total; t += Dt)
            {
                float flashK = Mathf.Clamp01(t / .16f);
                flash.color = new Color(0.84f, 0.96f, 1f, (1f - flashK) * .92f);
                flash.enabled = flashK < 1f;

                float ringK = Mathf.Clamp01(t / .44f);
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(.25f, 3.6f, EaseOutCubic(ringK));
                ring.color = new Color(1f, 1f, 1f, (1f - ringK) * .92f);
                ring.enabled = ringK < 1f;

                // Let the first death frames read before the grid starts eating
                // the room: the flash owns the first 0.18 s.
                dissolve.progress = EaseOutCubic(Mathf.Clamp01((t - .18f) / .62f));
                dissolve.SetVerticesDirty();

                StepDebris(Dt);
                yield return null;
            }

            dissolve.progress = 1f;
            dissolve.SetVerticesDirty();
            blackout.enabled = true;
            blackout.color = new Color32(5, 8, 10, 255);
            flash.enabled = false;
            ring.enabled = false;
            yield return new WaitForSecondsRealtime(.08f);
            dissolve.enabled = false;
        }

        void SpawnDebris(Vector2 focus, int count)
        {
            var palette = new[]
            {
                new Color32(249, 180, 94, 255), new Color32(232, 129, 58, 255),
                new Color32(255, 224, 174, 255), new Color32(118, 226, 232, 255),
                new Color32(43, 23, 8, 255),
            };
            debrisLayer.SetAsLastSibling();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Bit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(debrisLayer, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                float size = UnityEngine.Random.Range(4f, 11f);
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = focus + UnityEngine.Random.insideUnitCircle * 26f;
                rect.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));

                var image = go.GetComponent<Image>();
                image.sprite = MakeSquareSprite();
                image.color = palette[UnityEngine.Random.Range(0, palette.Length)];
                image.raycastTarget = false;

                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float speed = UnityEngine.Random.Range(90f, 380f);
                debris.Add(new Debris
                {
                    Rect = rect, Image = image,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed
                               + new Vector2(0f, UnityEngine.Random.Range(60f, 190f)),
                    Spin = UnityEngine.Random.Range(-520f, 520f),
                    Age = 0f,
                    Life = UnityEngine.Random.Range(.6f, 1.05f),
                    Size = size,
                });
            }
        }

        void StepDebris(float dt)
        {
            for (int i = debris.Count - 1; i >= 0; i--)
            {
                var bit = debris[i];
                bit.Age += dt;
                if (bit.Age >= bit.Life || bit.Rect == null)
                {
                    if (bit.Rect != null) Destroy(bit.Rect.gameObject);
                    debris.RemoveAt(i);
                    continue;
                }
                bit.Velocity += new Vector2(0f, -820f) * dt;
                bit.Rect.anchoredPosition += bit.Velocity * dt;
                bit.Rect.localRotation *= Quaternion.Euler(0, 0, bit.Spin * dt);
                float k = bit.Age / bit.Life;
                bit.Rect.sizeDelta = Vector2.one * bit.Size * (1f - k * .55f);
                var color = bit.Image.color;
                color.a = 1f - k * k;
                bit.Image.color = color;
            }
        }

        void ClearDebris()
        {
            foreach (var bit in debris)
                if (bit.Rect != null) Destroy(bit.Rect.gameObject);
            debris.Clear();
        }

        // ----------------------------------------------------------- clear card

        /// <summary>
        /// Dims and softens the finished level, then pops the teal cloud card and
        /// fades the result lines in one at a time. Returns once the player
        /// confirms (button, Enter/Space/Esc or a click).
        /// </summary>
        public IEnumerator ShowClearCard(string title, string subtitle, IList<string> lines)
        {
            yield return CaptureBackdrop();

            cardRoot.gameObject.SetActive(true);
            cardRoot.SetAsLastSibling();
            cardTitle.text = title;
            cardSubtitle.text = subtitle;
            for (int i = 0; i < cardLines.Count; i++)
            {
                bool used = lines != null && i < lines.Count;
                cardLines[i].gameObject.SetActive(used);
                cardLines[i].text = used ? lines[i] : "";
                var color = cardLines[i].color;
                color.a = 0f;
                cardLines[i].color = color;
            }

            confirmed = false;
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = true;
            cardRoot.localScale = Vector3.one * .86f;
            if (dimmer != null) dimmer.color = new Color(.03f, .07f, .08f, 0f);
            if (blurImage != null) blurImage.color = new Color(1f, 1f, 1f, 0f);

            float t = 0f;
            while (t < .3f)
            {
                t += Dt;
                float k = Mathf.Clamp01(t / .3f);
                cardGroup.alpha = k;
                cardRoot.localScale = Vector3.one * Mathf.LerpUnclamped(.86f, 1f, OutBack(k));
                if (dimmer != null) dimmer.color = new Color(.03f, .07f, .08f, .58f * k);
                if (blurImage != null) blurImage.color = new Color(1f, 1f, 1f, .85f * k);
                yield return null;
            }
            cardGroup.alpha = 1f;
            cardRoot.localScale = Vector3.one;
            if (dimmer != null) dimmer.color = new Color(.03f, .07f, .08f, .58f);
            if (blurImage != null) blurImage.color = new Color(1f, 1f, 1f, .85f);

            for (int i = 0; i < cardLines.Count; i++)
            {
                if (!cardLines[i].gameObject.activeSelf) continue;
                float lineT = 0f;
                while (lineT < .16f)
                {
                    lineT += Dt;
                    var color = cardLines[i].color;
                    color.a = Mathf.Clamp01(lineT / .16f);
                    cardLines[i].color = color;
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(.04f);
            }

            float waited = 0f;
            while (!confirmed && waited < 15f)
            {
                if (ConfirmPressed()) confirmed = true;
                waited += Dt;
                yield return null;
            }
            cardGroup.blocksRaycasts = false;
        }

        void HideCard()
        {
            if (cardRoot == null) return;
            cardRoot.gameObject.SetActive(false);
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
            if (dimmer != null) dimmer.color = new Color(.03f, .07f, .08f, 0f);
            if (blurImage != null) blurImage.color = new Color(1f, 1f, 1f, 0f);
        }

        static bool ConfirmPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                                     keyboard.numpadEnterKey.wasPressedThisFrame ||
                                     keyboard.spaceKey.wasPressedThisFrame ||
                                     keyboard.escapeKey.wasPressedThisFrame))
                return true;
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        IEnumerator CaptureBackdrop()
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = null;
            try
            {
                shot = ScreenCapture.CaptureScreenshotAsTexture();
            }
            catch (Exception)
            {
                shot = null;
            }
            if (shot == null) yield break;

            int width = Mathf.Max(1, shot.width / 5);
            int height = Mathf.Max(1, shot.height / 5);
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(shot, target);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var small = new Texture2D(width, height, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            small.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Destroy(shot);

            small.filterMode = FilterMode.Bilinear;
            small.wrapMode = TextureWrapMode.Clamp;
            if (blurTexture != null) Destroy(blurTexture);
            blurTexture = small;
            blurImage.texture = blurTexture;
            blurImage.enabled = true;
        }

        // ------------------------------------------------------------ helpers

        public Vector2 ToCanvas(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out var local);
            return local;
        }

        static float EaseOutCubic(float k)
        {
            k = Mathf.Clamp01(k);
            float inverse = 1f - k;
            return 1f - inverse * inverse * inverse;
        }

        static float OutBack(float k)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            k -= 1f;
            return 1f + c3 * k * k * k + c1 * k * k;
        }

        static Sprite MakeSquareSprite()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) {filterMode = FilterMode.Point};
            texture.SetPixels32(new[] {new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255)});
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 2f);
        }

        static Sprite MakeRing(int size, float thickness)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            float half = (size - 1) * .5f;
            float radius = half * .78f;
            float band = half * thickness;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - half, dy = y - half;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - Mathf.Abs(distance - radius) / band);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        }

        static Sprite MakeRoundedSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cornerX = Mathf.Max(0, Mathf.Abs(x - 31.5f) - 24);
                float cornerY = Mathf.Max(0, Mathf.Abs(y - 31.5f) - 24);
                if (cornerX * cornerX + cornerY * cornerY < 59)
                    colors[y * size + x] = new Color32(255, 255, 255, 255);
            }
            texture.SetPixels32(colors);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(9, 9, 9, 9));
        }

        /// <summary>
        /// A rounded rectangle with a scalloped "cloud" edge: bumps are stamped
        /// along the perimeter, then the rim is inked one pixel wide.
        /// </summary>
        static Sprite MakeCloudSprite(int width, int height, Color32 body, Color32 rim)
        {
            var inside = new bool[width * height];
            const int inset = 22, rounding = 34, bump = 17, step = 29;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - (width - 1) * .5f) - (width * .5f - inset - rounding));
                float dy = Mathf.Max(0, Mathf.Abs(y - (height - 1) * .5f) - (height * .5f - inset - rounding));
                if (dx * dx + dy * dy <= rounding * rounding &&
                    x >= inset && x < width - inset && y >= inset && y < height - inset)
                    inside[y * width + x] = true;
            }

            var centers = new List<Vector2Int>();
            for (int x = inset + rounding; x <= width - inset - rounding; x += step)
            {
                centers.Add(new Vector2Int(x, inset));
                centers.Add(new Vector2Int(x, height - 1 - inset));
            }
            for (int y = inset + rounding; y <= height - inset - rounding; y += step)
            {
                centers.Add(new Vector2Int(inset, y));
                centers.Add(new Vector2Int(width - 1 - inset, y));
            }
            int corner = inset + rounding / 2;
            centers.Add(new Vector2Int(corner, corner));
            centers.Add(new Vector2Int(width - 1 - corner, corner));
            centers.Add(new Vector2Int(corner, height - 1 - corner));
            centers.Add(new Vector2Int(width - 1 - corner, height - 1 - corner));

            foreach (var center in centers)
            for (int y = center.y - bump; y <= center.y + bump; y++)
            for (int x = center.x - bump; x <= center.x + bump; x++)
            {
                if (x < 0 || x >= width || y < 0 || y >= height) continue;
                int dx = x - center.x, dy = y - center.y;
                if (dx * dx + dy * dy <= bump * bump) inside[y * width + x] = true;
            }

            var edge = new bool[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!inside[y * width + x]) continue;
                bool rimPixel = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                if (rimPixel || !inside[y * width + x - 1] || !inside[y * width + x + 1] ||
                    !inside[(y - 1) * width + x] || !inside[(y + 1) * width + x])
                    edge[y * width + x] = true;
            }

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (edge[index]) pixels[index] = rim;
                else if (inside[index]) pixels[index] = body;
                else pixels[index] = new Color32(0, 0, 0, 0);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100f);
        }

        sealed class Debris
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Spin, Age, Life, Size;
        }
    }

    /// <summary>
    /// The hard-edged circular mask: everything outside a circle is filled, so
    /// animating the radius closes the screen to a point and opens it again.
    /// Built as a mesh, never a scaled texture, so the edge stays crisp.
    /// </summary>
    public sealed class IrisGraphic : MaskableGraphic
    {
        public Vector2 center;
        public float radius;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            float maxRadius = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height) * .5f + 8f;
            if (radius >= maxRadius) return;

            const int segments = 160;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                Vector2 d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                Vector2 inner0 = center + d0 * radius;
                Vector2 inner1 = center + d1 * radius;
                Vector2 outer0 = RayToRect(center, d0, rect);
                Vector2 outer1 = RayToRect(center, d1, rect);
                AddQuad(vh, inner0, inner1, outer1, outer0, color);
            }
        }

        static Vector2 RayToRect(Vector2 origin, Vector2 direction, Rect rect)
        {
            float tx = Mathf.Abs(direction.x) < 1e-5f ? float.PositiveInfinity
                : ((direction.x > 0 ? rect.xMax : rect.xMin) - origin.x) / direction.x;
            float ty = Mathf.Abs(direction.y) < 1e-5f ? float.PositiveInfinity
                : ((direction.y > 0 ? rect.yMax : rect.yMin) - origin.y) / direction.y;
            return origin + direction * Mathf.Min(tx, ty);
        }

        static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            int index = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }

    /// <summary>
    /// A grid of 45°-rotated cells that blacken outward from a focus point, used
    /// for the death shatter. Per-cell alpha is computed from the normalised
    /// distance to the focus plus a stable hash, so the front looks hand-broken
    /// rather than like a perfect circle.
    /// </summary>
    public sealed class DiamondGraphic : MaskableGraphic
    {
        public float progress;
        public Vector2 focus;
        const float Half = 27f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (progress <= 0f) return;

            Rect rect = rectTransform.rect;
            float maxDistance = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height) * .5f;
            int columns = Mathf.CeilToInt(rect.width / (Half * 2f)) + 3;
            int rows = Mathf.CeilToInt(rect.height / (Half * 2f)) + 3;
            Color32 baseColor = color;

            for (int j = -rows; j <= rows; j++)
            for (int i = -columns; i <= columns; i++)
            {
                if (((i + j) & 1) != 0) continue;
                var cell = new Vector2(i * Half, j * Half);
                float distance = Vector2.Distance(cell, focus) / Mathf.Max(1f, maxDistance);
                float jitter = Mathf.Repeat(Mathf.Sin(i * 12.9898f + j * 78.233f) * 43758.5453f, 1f);
                float threshold = Mathf.Clamp01(distance * 1.06f + jitter * .10f);
                float alpha = Mathf.Clamp01((progress * 1.14f - threshold) / .10f);
                if (alpha <= 0f) continue;

                if (cell.x + Half < rect.xMin || cell.x - Half > rect.xMax) continue;
                if (cell.y + Half < rect.yMin || cell.y - Half > rect.yMax) continue;
                var a = cell + new Vector2(-Half, 0f);
                var b = cell + new Vector2(0f, Half);
                var c = cell + new Vector2(Half, 0f);
                var d = cell + new Vector2(0f, -Half);

                var tint = new Color32(baseColor.r, baseColor.g, baseColor.b, (byte)(alpha * baseColor.a));
                int index = vh.currentVertCount;
                vh.AddVert(a, tint, Vector2.zero);
                vh.AddVert(b, tint, Vector2.zero);
                vh.AddVert(c, tint, Vector2.zero);
                vh.AddVert(d, tint, Vector2.zero);
                vh.AddTriangle(index, index + 1, index + 2);
                vh.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}

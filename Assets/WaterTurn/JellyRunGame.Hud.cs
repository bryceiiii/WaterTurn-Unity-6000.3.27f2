using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// HUD, chapter rail, entrance prompt, and the intro/help/win overlays.
    /// Screen-fixed on the same overlay canvas as the world.
    /// </summary>
    public sealed partial class JellyRunGame
    {
        RectTransform introRoot, winRoot, helpRoot, promptRoot, toastRoot, zoneRoot, railRoot;
        CanvasGroup promptGroup, toastGroup, zoneGroup;
        Text counterText, chapterText, moistureText, zoneTitle, zoneTip, toastText, promptText, promptKey;
        Text winStats, helpText, muteLabel, railHint;
        Image moistureFill, promptKeyBg, railFish, railPortal;
        readonly List<Image> railPips = new List<Image>();
        readonly List<Image> railNodes = new List<Image>();
        Button muteButton;

        void BuildHud()
        {
            var panel = HudBox("CounterPanel", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -20f), new Vector2(286f, 66f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = JellyRunArt.Rounded;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color32(8, 22, 29, 205);
            panelImage.raycastTarget = false;

            HudImage(panel, "PearlIcon", JellyRunArt.Pearl, Color.white,
                new Vector2(28f, -33f), new Vector2(24f, 24f), new Vector2(0f, 1f));
            counterText = HudText(panel, "Counter", "关卡 0 / 20", 20, new Color32(242, 251, 250, 255),
                TextAnchor.MiddleLeft, new Vector2(50f, -8f), new Vector2(220f, 28f), new Vector2(0f, 1f), FontStyle.Bold);
            chapterText = HudText(panel, "Chapter", "章节 0 / 5 · 水分 100%", 12,
                new Color32(170, 205, 205, 230), TextAnchor.MiddleLeft,
                new Vector2(50f, -34f), new Vector2(226f, 20f), new Vector2(0f, 1f));

            var barBack = HudImage(panel, "MoistureBack", JellyRunArt.Rounded, new Color(1f, 1f, 1f, .14f),
                new Vector2(28f, -54f), new Vector2(230f, 8f), new Vector2(0f, 1f));
            barBack.type = Image.Type.Sliced;
            moistureFill = HudImage(panel, "MoistureFill", JellyRunArt.Rounded, new Color32(102, 226, 255, 255),
                new Vector2(28f, -54f), new Vector2(230f, 8f), new Vector2(0f, 1f));
            moistureFill.type = Image.Type.Sliced;
            moistureFill.rectTransform.pivot = new Vector2(0f, 1f);
            moistureText = HudText(panel, "MoistureText", "", 11, new Color32(190, 226, 226, 220),
                TextAnchor.MiddleRight, new Vector2(236f, -54f), new Vector2(44f, 16f), new Vector2(0f, 1f));

            zoneRoot = HudBox("ZonePanel", new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0f, -20f), new Vector2(440f, 78f));
            var zoneImage = zoneRoot.gameObject.AddComponent<Image>();
            zoneImage.sprite = JellyRunArt.Rounded;
            zoneImage.type = Image.Type.Sliced;
            zoneImage.color = new Color32(8, 22, 29, 178);
            zoneImage.raycastTarget = false;
            zoneGroup = zoneRoot.gameObject.AddComponent<CanvasGroup>();
            zoneGroup.blocksRaycasts = false;
            zoneTitle = HudText(zoneRoot, "ZoneTitle", "", 19, new Color32(140, 245, 220, 255),
                TextAnchor.MiddleCenter, new Vector2(0f, -10f), new Vector2(420f, 28f),
                new Vector2(.5f, 1f), FontStyle.Bold);
            zoneTip = HudText(zoneRoot, "ZoneTip", "", 12, new Color32(180, 210, 210, 230),
                TextAnchor.MiddleCenter, new Vector2(0f, -40f), new Vector2(416f, 30f),
                new Vector2(.5f, 1f));

            MakeButton(hudRoot, "HubButton", "回潮汐洞穴", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -18f), new Vector2(128f, 34f), ReturnToHub);
            muteButton = MakeButton(hudRoot, "MuteButton", "静音", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-154f, -18f), new Vector2(88f, 34f), ToggleMute);
            muteLabel = muteButton.GetComponentInChildren<Text>();

            railRoot = HudBox("ChapterRail", new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(0f, 12f), new Vector2(900f, 30f));
            var railLine = HudImage(railRoot, "Line", JellyRunArt.Rounded, new Color(160 / 255f, 230 / 255f, 225 / 255f, .16f),
                new Vector2(0f, -14f), new Vector2(860f, 3f), new Vector2(.5f, .5f));
            railLine.type = Image.Type.Sliced;

            for (int c = 0; c < JellyRunData.Entrances.Length; c++)
            {
                float px = RailX(JellyRunData.Entrances[c].entryX);
                var node = HudImage(railRoot, "ChapterNode " + c, JellyRunArt.Circle,
                    new Color(1f, 1f, 1f, .35f), new Vector2(px, 0f), new Vector2(9f, 9f), new Vector2(.5f, .5f));
                railNodes.Add(node);
                for (int p = 0; p < 4; p++)
                {
                    var pip = HudImage(railRoot, "ChapterPip " + c + "." + p, JellyRunArt.Circle,
                        new Color(1f, 1f, 1f, .22f), new Vector2(px - 12f + p * 8f, -13f),
                        new Vector2(5f, 5f), new Vector2(.5f, .5f));
                    railPips.Add(pip);
                }
            }
            railPortal = HudImage(railRoot, "PortalDot", JellyRunArt.Circle,
                new Color(150 / 255f, 180 / 255f, 190 / 255f, .55f),
                new Vector2(RailX(JellyRunData.Portal.x + JellyRunData.Portal.w * .5f), 0f),
                new Vector2(11f, 11f), new Vector2(.5f, .5f));
            railFish = HudImage(railRoot, "FishDot", JellyRunArt.Circle,
                new Color(1f, .70f, .48f, 1f), new Vector2(0f, 0f), new Vector2(12f, 12f),
                new Vector2(.5f, .5f));

            promptRoot = HudBox("EntrancePrompt", new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(0f, 88f), new Vector2(540f, 54f));
            var promptImage = promptRoot.gameObject.AddComponent<Image>();
            promptImage.sprite = JellyRunArt.Rounded;
            promptImage.type = Image.Type.Sliced;
            promptImage.color = new Color32(18, 34, 36, 242);
            promptImage.raycastTarget = false;
            promptGroup = promptRoot.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0f;
            promptGroup.blocksRaycasts = false;
            promptKeyBg = HudImage(promptRoot, "Key", JellyRunArt.Rounded, new Color32(63, 224, 192, 255),
                new Vector2(28f, -27f), new Vector2(38f, 38f), new Vector2(0f, .5f));
            promptKeyBg.type = Image.Type.Sliced;
            promptKey = HudText(promptKeyBg.rectTransform, "KeyText", "E", 20, new Color32(4, 35, 31, 255),
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(38f, 38f), new Vector2(.5f, .5f), FontStyle.Bold);
            promptText = HudText(promptRoot, "PromptText", "", 13, new Color32(234, 247, 244, 255),
                TextAnchor.MiddleLeft, new Vector2(58f, -27f), new Vector2(466f, 46f), new Vector2(0f, .5f));

            toastRoot = HudBox("Toast", new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0f, -112f), new Vector2(680f, 44f));
            var toastImage = toastRoot.gameObject.AddComponent<Image>();
            toastImage.sprite = JellyRunArt.Rounded;
            toastImage.type = Image.Type.Sliced;
            toastImage.color = new Color32(8, 20, 26, 220);
            toastImage.raycastTarget = false;
            toastGroup = toastRoot.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastText = HudText(toastRoot, "ToastText", "", 14, new Color32(255, 217, 160, 255),
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(640f, 36f), new Vector2(.5f, .5f));

            HudText(hudRoot, "Controls",
                "A/D 游 · W/S 上下 · Space 跳/二段跳 · E 进入潮心 · R 回水池 · H 帮助 · M 静音 · Esc 回洞穴",
                12, new Color32(166, 196, 196, 210), TextAnchor.MiddleLeft,
                new Vector2(28f, 50f), new Vector2(760f, 22f), new Vector2(0f, 0f));

            var flashGo = new GameObject("RedFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            flashGo.transform.SetParent(hudRoot, false);
            var flashRt = (RectTransform)flashGo.transform;
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = flashRt.offsetMax = Vector2.zero;
            redFlash = flashGo.GetComponent<Image>();
            redFlash.sprite = JellyRunArt.RedVignette;
            redFlash.color = new Color(1f, 1f, 1f, 0f);
            redFlash.raycastTarget = false;
            redFlash.type = Image.Type.Simple;

            BuildIntroOverlay();
            BuildWinOverlay();
            BuildHelpOverlay();
        }

        void BuildIntroOverlay()
        {
            introRoot = HudStretch("IntroOverlay");
            var back = introRoot.gameObject.AddComponent<Image>();
            back.color = new Color32(4, 12, 17, 218);
            back.raycastTarget = true;

            var panel = HudBox(introRoot, "IntroPanel", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0f, 10f), new Vector2(700f, 420f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = JellyRunArt.Rounded;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color32(9, 26, 33, 246);
            panelImage.raycastTarget = true;

            HudText(panel, "Title", "果冻潮池", 38, new Color32(140, 245, 220, 255),
                TextAnchor.MiddleCenter, new Vector2(0f, -36f), new Vector2(620f, 50f),
                new Vector2(.5f, 1f), FontStyle.Bold);
            HudText(panel, "Subtitle", "2D 横版 · 5 处潮心 · 20 个关卡", 15,
                new Color32(170, 205, 205, 235), TextAnchor.MiddleCenter,
                new Vector2(0f, -84f), new Vector2(620f, 26f), new Vector2(.5f, 1f));

            string body = "你是一条离不开水的小鱼。果冻水既是泳池，也是弹簧——潜得越深、蓄力越满，弹得越高。\n\n" +
                          "一路上有五处“潮心”石台：站上石台、靠近潮心，按 E 进入。每处潮心连打 4 关，\n" +
                          "第 4 关通关后回到这里。5 处 × 4 关全部通过，终点才会开启。";
            HudText(panel, "Body", body, 14, new Color32(196, 220, 220, 255),
                TextAnchor.UpperLeft, new Vector2(22f, -122f), new Vector2(654f, 112f),
                new Vector2(0f, 1f));

            HudText(panel, "Keys", "A/D 游动   W/S 上浮下潜   Space 跃出/二段跳   E 进入潮心   R 回最近水池   H 帮助",
                12, new Color32(150, 198, 198, 235), TextAnchor.MiddleCenter,
                new Vector2(0f, -246f), new Vector2(620f, 24f), new Vector2(.5f, 1f));

            MakeButton(panel, "StartButton", "开始游戏", new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(0f, 34f), new Vector2(240f, 46f), StartRun);
        }

        void BuildWinOverlay()
        {
            winRoot = HudStretch("WinOverlay");
            var back = winRoot.gameObject.AddComponent<Image>();
            back.color = new Color32(4, 12, 17, 205);
            back.raycastTarget = true;

            var panel = HudBox(winRoot, "WinPanel", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0f, 0f), new Vector2(560f, 350f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = JellyRunArt.Rounded;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color32(12, 32, 38, 248);
            panelImage.raycastTarget = true;

            HudText(panel, "Title", "潮汐全通", 34, new Color32(255, 224, 150, 255),
                TextAnchor.MiddleCenter, new Vector2(0f, -32f), new Vector2(500f, 48f),
                new Vector2(.5f, 1f), FontStyle.Bold);
            HudText(panel, "Subtitle", "5 处潮心全亮 · 20 个关卡全部通过", 14,
                new Color32(180, 220, 214, 240), TextAnchor.MiddleCenter,
                new Vector2(0f, -76f), new Vector2(500f, 24f), new Vector2(.5f, 1f));
            winStats = HudText(panel, "Stats", "", 17, new Color32(226, 244, 240, 255),
                TextAnchor.UpperCenter, new Vector2(0f, -118f), new Vector2(480f, 90f),
                new Vector2(.5f, 1f));

            MakeButton(panel, "AgainButton", "再跑一次", new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(-124f, 30f), new Vector2(210f, 46f), RestartRun);
            MakeButton(panel, "WinHubButton", "回潮汐洞穴", new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(124f, 30f), new Vector2(210f, 46f), ReturnToHub);
            winRoot.gameObject.SetActive(false);
        }

        void BuildHelpOverlay()
        {
            helpRoot = HudStretch("HelpOverlay");
            var back = helpRoot.gameObject.AddComponent<Image>();
            back.color = new Color32(4, 12, 17, 220);
            back.raycastTarget = true;

            var panel = HudBox(helpRoot, "HelpPanel", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(660f, 370f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = JellyRunArt.Rounded;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color32(10, 28, 35, 246);
            panelImage.raycastTarget = true;

            HudText(panel, "Title", "操作 / 规则", 26, new Color32(140, 245, 220, 255),
                TextAnchor.MiddleCenter, new Vector2(0f, -28f), new Vector2(580f, 38f),
                new Vector2(.5f, 1f), FontStyle.Bold);
            string body = "A / D、← / →：水里游动，空中微调\n" +
                          "W / S、↑ / ↓：上浮 / 下潜蓄力（潜得越深，弹得越高）\n" +
                          "Space：跃出果冻；在空中再按一次是甩尾二段跳\n" +
                          "E：站在潮心石台上时进入章节（每处连续 4 关）\n" +
                          "R：回到最近的水池   H：关闭帮助   M：静音   Esc：回潮汐洞穴\n\n" +
                          "离开果冻会持续失去水分；尖刺、锯片、闸门和深渊会让你回到最近的水池。\n" +
                          "5 处潮心：潮池 01–04 / 断桥 05–08 / 水闸 09–12 / 尖刺走廊 13–16 / 终章 17–20。";
            helpText = HudText(panel, "Body", body, 14, new Color32(204, 226, 226, 255),
                TextAnchor.UpperLeft, new Vector2(22f, -84f), new Vector2(616f, 240f),
                new Vector2(0f, 1f));
            MakeButton(panel, "CloseButton", "知道了", new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(0f, 24f), new Vector2(200f, 42f), ToggleHelp);
            helpRoot.gameObject.SetActive(false);
        }

        void RefreshHud(float dt)
        {
            int cleared = CountCleared(GameSession.JellyRunCleared);
            int chapters = 0;
            for (int c = 0; c < 5; c++) if (ChapterComplete(c)) chapters++;
            if (counterText != null) counterText.text = "关卡 " + cleared + " / 20";
            if (chapterText != null)
                chapterText.text = "章节 " + chapters + " / 5 · 水分 " + Mathf.RoundToInt(moisture) + "%";
            if (moistureFill != null)
            {
                float mp = Mathf.Clamp01(moisture / 100f);
                moistureFill.rectTransform.sizeDelta = new Vector2(230f * mp, 8f);
                moistureFill.color = mp < .3f ? new Color32(255, 125, 107, 255) : new Color32(102, 226, 255, 255);
            }
            if (moistureText != null) moistureText.text = Mathf.RoundToInt(moisture) + "%";

            if (zoneGroup != null)
            {
                float target = zoneT > 0f && phase != Phase.Intro ? 1f : 0f;
                zoneGroup.alpha = Mathf.MoveTowards(zoneGroup.alpha, target, dt * 4f);
                int zi = Mathf.Clamp(zoneIdx, 0, JellyRunData.Zones.Length - 1);
                zoneTitle.text = JellyRunData.Zones[zi].name;
                zoneTip.text = JellyRunData.Zones[zi].tip;
            }

            if (toastGroup != null)
            {
                toastGroup.alpha = toastT > 0f ? Mathf.Clamp01(toastT * 2.2f) : Mathf.MoveTowards(toastGroup.alpha, 0f, dt * 4f);
                if (toastText != null) toastText.text = toastMsg;
            }

            UpdatePrompt();
            UpdateRail(cleared);

            if (muteLabel != null) muteLabel.text = sfx.Muted ? "已静音" : "静音";
            if (winRoot != null && phase == Phase.Win && !winRoot.gameObject.activeSelf) ShowWinOverlay();
            if (introRoot != null && introRoot.gameObject.activeSelf && phase != Phase.Intro)
                introRoot.gameObject.SetActive(false);
        }

        void UpdatePrompt()
        {
            if (promptGroup == null) return;
            bool show = phase == Phase.Play && !helpOpen && nearestEntrance >= 0;
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            if (!show) return;
            var e = JellyRunData.Entrances[nearestEntrance];
            int done = 0;
            for (int i = e.FirstLevel; i <= e.LastLevel; i++) if (LevelCleared(i)) done++;
            bool complete = done >= 4;
            promptKey.text = "E";
            promptText.text = (complete ? "E 重玩 " : "E 进入 ") + e.Title + "（" + e.LevelRange + "）" +
                              (complete ? " · 已完成" : " · 已通 " + done + " / 4");
        }

        void UpdateRail(int cleared)
        {
            if (railFish == null) return;
            railFish.rectTransform.anchoredPosition = new Vector2(RailX(fishPos.x), 0f);
            for (int c = 0; c < railNodes.Count; c++)
            {
                bool complete = ChapterComplete(c);
                var accent = JellyRunData.Entrances[c].accent;
                railNodes[c].color = complete
                    ? new Color(accent.r / 255f, accent.g / 255f, accent.b / 255f, 1f)
                    : new Color(1f, 1f, 1f, .34f);
                for (int p = 0; p < 4; p++)
                {
                    bool lit = LevelCleared(c * 4 + p);
                    railPips[c * 4 + p].color = lit
                        ? new Color(1f, .85f, .52f, .95f)
                        : new Color(1f, 1f, 1f, .20f);
                }
            }
            if (railPortal != null)
                railPortal.color = cleared >= 20
                    ? new Color(1f, .86f, .52f, 1f)
                    : new Color(150 / 255f, 180 / 255f, 190 / 255f, .55f);
        }

        float RailX(float worldX)
        {
            return -430f + Mathf.Clamp01(worldX / JellyRunData.WorldW) * 860f;
        }

        void ShowToast(string message)
        {
            toastMsg = message;
            toastT = 1.9f;
            if (toastGroup != null) toastGroup.alpha = 1f;
        }

        void ToggleMute()
        {
            sfx.SetMuted(!sfx.Muted);
            ShowToast(sfx.Muted ? "已静音" : "声音已打开");
        }

        void ToggleHelp()
        {
            helpOpen = !helpOpen;
            if (helpRoot != null) helpRoot.gameObject.SetActive(helpOpen);
            if (helpOpen) jumpQueued = interactQueued = false;
        }

        void ShowWinOverlay()
        {
            if (winRoot == null) return;
            winRoot.gameObject.SetActive(true);
            if (winStats != null)
            {
                int min = Mathf.FloorToInt(elapsed / 60f);
                int sec = Mathf.FloorToInt(elapsed % 60f);
                winStats.text = "用时   " + min.ToString("00") + ":" + sec.ToString("00") +
                                "\n失误   " + deaths +
                                "\n关卡   20 / 20   ·   章节   5 / 5";
            }
        }

        RectTransform HudStretch(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(hudRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        RectTransform HudBox(string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(hudRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        RectTransform HudBox(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        Image HudImage(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text HudText(Transform parent, string name, string content, int size, Color color,
            TextAnchor align, Vector2 position, Vector2 box, Vector2 anchor, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = box;
            var text = go.GetComponent<Text>();
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

        Button MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = JellyRunArt.Rounded;
            img.type = Image.Type.Sliced;
            img.color = new Color32(25, 58, 62, 245);
            img.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(.8f, .9f, .9f, 1f);
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(.5f, .5f);
            trt.pivot = new Vector2(.5f, .5f);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = new Vector2(size.x - 14f, size.y);
            var text = textGo.GetComponent<Text>();
            text.font = font;
            text.text = label;
            text.fontSize = 15;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color32(140, 245, 220, 255);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return button;
        }
    }
}

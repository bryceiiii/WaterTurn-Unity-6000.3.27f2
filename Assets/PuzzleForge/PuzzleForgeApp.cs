using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WaterTurn.Forge
{
    /// <summary>
    /// 谜题工坊主控：一个场景把「参数配置 → 生成 → 手改 → 预览/试玩 → 保存/导出」串成一条线。
    /// 界面全部由代码搭建（与项目里 HubGame 的做法一致），场景里只要挂上这个组件即可运行。
    /// </summary>
    public sealed class PuzzleForgeApp : MonoBehaviour
    {
        const int RoleBg = 0;
        const int RolePanel = 1;
        const int RolePanelAlt = 2;
        const int RoleLine = 3;
        const int RoleText = 4;
        const int RoleDim = 5;
        const int RoleAccent = 6;
        const int RoleAccentText = 7;
        const int RoleHazard = 8;
        const int RoleExit = 9;

        static readonly string[] PaletteCaptions =
        {
            "地", "水", "鱼", "出", "墙", "箱", "压", "1", "2", "→",
            "↑", "↓", "←", "感", "时", "闸", "珠", "膜", "泉", "栅"
        };

        sealed class ThemeTarget
        {
            public Graphic graphic;
            public int role;
        }

        ForgeSpec spec = new ForgeSpec();
        ForgePack pack;
        ForgePalette palette;
        readonly List<ThemeTarget> themeTargets = new List<ThemeTarget>();

        int cursor;
        char brush = '~';
        readonly List<string> rows = new List<string>();
        ForgeValidation validation;

        // ---- 界面引用 ----
        RectTransform canvasRect;
        Image background, headerBar, leftPanel, middlePanel, rightPanel, bottomBar;
        Text titleText, subtitleText, statusText;
        Text themeValue, difficultyNote, countValue, moveLimitValue, widthValue, heightValue, brushLabel;
        readonly List<Button> difficultySegments = new List<Button>();
        readonly List<Button> hintSegments = new List<Button>();
        readonly List<Button> winSegments = new List<Button>();
        readonly List<Toggle> ruleToggles = new List<Toggle>();
        InputField hintField, seedField, asciiField, answerField;
        Button generateButton, validateButton, trialButton, replayButton, saveButton, exportButton, loadButton, clearButton;
        ForgeBoard painter, preview;
        Text previewTitle, previewInfo, previewHint, previewAnswer, trialStatus, levelCountLabel;
        RectTransform tabsRow;
        readonly List<Button> tabButtons = new List<Button>();
        readonly List<Button> paletteButtons = new List<Button>();
        readonly List<Button> moveButtons = new List<Button>();

        // ---- 试玩状态 ----
        bool trialActive, trialWon;
        bool syncingAnswer;
        Level trialLevel;
        Position trialPosition;
        int trialSteps;
        Coroutine replayRoutine;
        float statusTimeout;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "PuzzleForge") return;
            if (FindFirstObjectByType<PuzzleForgeApp>() != null) return;
            new GameObject("PuzzleForge").AddComponent<PuzzleForgeApp>();
        }

        void Awake()
        {
            palette = ForgePalette.Of(spec.theme);
            BuildUi();
            ApplyTheme();
            NewWorkspace(ForgeGenerator.Blank(7));
            SetStatus("就绪：左侧调参数 → 生成关卡 → 中间手改 → 右侧试玩 → 底部保存/导出。");
        }

        void Update()
        {
            if (statusTimeout > 0 && Time.realtimeSinceStartup > statusTimeout)
            {
                statusTimeout = 0;
                if (statusText != null) statusText.text = "";
            }

            if (!trialActive || !TrialKeysAllowed()) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) PlayMove(Move.Right);
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) PlayMove(Move.Left);
            else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) PlayMove(Move.Up);
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) PlayMove(Move.Down);
            else if (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) PlayMove(Move.Turn);
            else if (kb.qKey.wasPressedThisFrame) PlayMove(Move.TurnLeft);
            else if (kb.rKey.wasPressedThisFrame) ResetTrial();
#else
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) PlayMove(Move.Right);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) PlayMove(Move.Left);
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) PlayMove(Move.Up);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) PlayMove(Move.Down);
            else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)) PlayMove(Move.Turn);
            else if (Input.GetKeyDown(KeyCode.Q)) PlayMove(Move.TurnLeft);
            else if (Input.GetKeyDown(KeyCode.R)) ResetTrial();
#endif
        }

        static bool TrialKeysAllowed()
        {
            var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            return selected == null || selected.GetComponent<InputField>() == null;
        }

        // ==================================================================== 搭建

        void BuildUi()
        {
            var canvasGo = new GameObject("ForgeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)canvasGo.transform;
            EnsureEventSystem();

            background = ForgeUi.Box(canvasRect, "Background", palette.bg, 0, 0, 0, 0);
            ForgeUi.Fill(background.rectTransform);

            BuildHeader();
            BuildLeftPanel();
            BuildMiddlePanel();
            BuildRightPanel();
            BuildBottomBar();
        }

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void BuildHeader()
        {
            headerBar = ForgeUi.Box(canvasRect, "Header", palette.panel, 0, 0, 1920, 84);
            Tint(headerBar, RolePanel);
            titleText = ForgeUi.Label(headerBar.rectTransform, "谜题工坊 · PuzzleForge", 26, palette.text,
                TextAnchor.UpperLeft, 28, 12, 700, 34, false);
            Tint(titleText, RoleText);
            subtitleText = ForgeUi.Label(headerBar.rectTransform,
                "参数配置 → 生成 → 手动编辑 → 预览试玩 → 保存/导出；盘面就是 WaterTurn.Level 的 ASCII 定义", 13,
                palette.dim, TextAnchor.UpperLeft, 28, 48, 1000, 22, false);
            Tint(subtitleText, RoleDim);
            statusText = ForgeUi.Label(headerBar.rectTransform, "", 14, palette.accent, TextAnchor.UpperRight,
                1000, 26, 892, 30, false);
            Tint(statusText, RoleAccent);
        }

        void BuildLeftPanel()
        {
            leftPanel = ForgeUi.Box(canvasRect, "LeftPanel", palette.panel, 24, 100, 400, 892);
            Tint(leftPanel, RolePanel);
            var root = leftPanel.rectTransform;

            var heading = ForgeUi.Label(root, "① 谜题参数配置", 17, palette.accent, TextAnchor.UpperLeft, 20, 14, 360, 26, false);
            Tint(heading, RoleAccent);

            // 主题
            var themeLabel = ForgeUi.Label(root, "主题", 12, palette.dim, TextAnchor.UpperLeft, 20, 48, 80, 18, false);
            Tint(themeLabel, RoleDim);
            var themeBox = ForgeUi.Box(root, "ThemeBox", palette.panelAlt, 60, 66, 220, 30);
            Tint(themeBox, RolePanelAlt);
            themeValue = ForgeUi.Label(root, ForgeLabels.Of(spec.theme), 14, palette.text, TextAnchor.MiddleCenter,
                60, 66, 220, 30, false);
            Tint(themeValue, RoleText);
            var prevTheme = ForgeUi.Button(root, "◀", 14, palette.panelAlt, palette.text, 20, 66, 36, 30, () => CycleTheme(-1));
            var nextTheme = ForgeUi.Button(root, "▶", 14, palette.panelAlt, palette.text, 284, 66, 36, 30, () => CycleTheme(1));
            Tint(prevTheme.targetGraphic, RolePanelAlt);
            Tint(nextTheme.targetGraphic, RolePanelAlt);

            // 机制规则
            var ruleLabel = ForgeUi.Label(root, "机制规则（勾选后才会进生成池）", 12, palette.dim,
                TextAnchor.UpperLeft, 20, 104, 360, 18, false);
            Tint(ruleLabel, RoleDim);
            for (int i = 0; i < ForgeLabels.RuleNames.Length; i++)
            {
                int index = i;
                float x = 20 + (i % 2) * 180;
                float y = 126 + (i / 2) * 30;
                var toggle = ForgeUi.Toggle(root, ForgeLabels.RuleNames[i], RuleOf(index), palette.line, palette.accent,
                    palette.text, x, y, 172, 26, on => SetRule(index, on));
                Tint(toggle.targetGraphic, RoleLine);
                Tint(toggle.graphic, RoleAccent);
                ruleToggles.Add(toggle);
            }

            // 难度
            var difficultyLabel = ForgeUi.Label(root, "难度", 12, palette.dim, TextAnchor.UpperLeft, 20, 250, 80, 18, false);
            Tint(difficultyLabel, RoleDim);
            difficultyNote = ForgeUi.Label(root, ForgeLabels.DifficultyNotes[(int)spec.difficulty], 12, palette.accent,
                TextAnchor.UpperRight, 200, 250, 180, 18, false);
            Tint(difficultyNote, RoleAccent);
            for (int i = 0; i < ForgeLabels.Difficulties.Length; i++)
            {
                int index = i;
                var button = ForgeUi.Button(root, ForgeLabels.Difficulties[i], 13, palette.panelAlt, palette.text,
                    20 + i * 90, 272, 86, 30, () => SetDifficulty(index));
                Tint(button.targetGraphic, RolePanelAlt);
                difficultySegments.Add(button);
            }

            // 关卡数量
            var countLabel = ForgeUi.Label(root, "关卡数量", 13, palette.text, TextAnchor.UpperLeft, 20, 314, 120, 24, false);
            Tint(countLabel, RoleText);
            var countBox = ForgeUi.Box(root, "CountBox", palette.panelAlt, 262, 312, 58, 30);
            Tint(countBox, RolePanelAlt);
            countValue = ForgeUi.Label(root, spec.levelCount.ToString(), 14, palette.accent, TextAnchor.MiddleCenter,
                262, 312, 58, 30, false);
            Tint(countValue, RoleAccent);
            var minusCount = ForgeUi.Button(root, "−", 16, palette.panelAlt, palette.text, 226, 312, 34, 30, () => ChangeCount(-1));
            var plusCount = ForgeUi.Button(root, "+", 16, palette.panelAlt, palette.text, 322, 312, 34, 30, () => ChangeCount(1));
            Tint(minusCount.targetGraphic, RolePanelAlt);
            Tint(plusCount.targetGraphic, RolePanelAlt);

            // 提示
            var hintLabel = ForgeUi.Label(root, "提示方式", 13, palette.text, TextAnchor.UpperLeft, 20, 356, 120, 24, false);
            Tint(hintLabel, RoleText);
            for (int i = 0; i < ForgeLabels.HintModes.Length; i++)
            {
                int index = i;
                var button = ForgeUi.Button(root, ForgeLabels.HintModes[i], 12, palette.panelAlt, palette.text,
                    20 + i * 120, 380, 116, 28, () => SetHintMode(index));
                Tint(button.targetGraphic, RolePanelAlt);
                hintSegments.Add(button);
            }

            var authorLabel = ForgeUi.Label(root, "自定义提示（可留空，会拼在系统提示前）", 11, palette.dim,
                TextAnchor.UpperLeft, 20, 416, 360, 18, false);
            Tint(authorLabel, RoleDim);
            hintField = ForgeUi.Field(root, spec.authorHint, "例：先把水引到右上角", 13, palette.bg, palette.text,
                palette.dim, 20, 436, 360, 32);
            Tint(hintField.targetGraphic, RoleBg);
            hintField.onValueChanged.AddListener(value => spec.authorHint = value);

            // 胜负条件
            var winLabel = ForgeUi.Label(root, "胜负条件", 13, palette.text, TextAnchor.UpperLeft, 20, 480, 120, 24, false);
            Tint(winLabel, RoleText);
            for (int i = 0; i < ForgeLabels.WinRules.Length; i++)
            {
                int index = i;
                var button = ForgeUi.Button(root, ForgeLabels.WinRules[i], 12, palette.panelAlt, palette.text,
                    20 + i * 120, 504, 116, 28, () => SetWinRule(index));
                Tint(button.targetGraphic, RolePanelAlt);
                winSegments.Add(button);
            }

            var moveLabel = ForgeUi.Label(root, "限步数（仅“限步抵达”生效）", 11, palette.dim, TextAnchor.UpperLeft,
                20, 540, 200, 18, false);
            Tint(moveLabel, RoleDim);
            var moveBox = ForgeUi.Box(root, "MoveBox", palette.panelAlt, 262, 562, 58, 30);
            Tint(moveBox, RolePanelAlt);
            moveLimitValue = ForgeUi.Label(root, spec.moveLimit.ToString(), 14, palette.accent, TextAnchor.MiddleCenter,
                262, 562, 58, 30, false);
            Tint(moveLimitValue, RoleAccent);
            var minusMove = ForgeUi.Button(root, "−", 16, palette.panelAlt, palette.text, 226, 562, 34, 30, () => ChangeMoveLimit(-2));
            var plusMove = ForgeUi.Button(root, "+", 16, palette.panelAlt, palette.text, 322, 562, 34, 30, () => ChangeMoveLimit(2));
            Tint(minusMove.targetGraphic, RolePanelAlt);
            Tint(plusMove.targetGraphic, RolePanelAlt);

            // 种子
            var seedLabel = ForgeUi.Label(root, "随机种子", 13, palette.text, TextAnchor.UpperLeft, 20, 602, 120, 24, false);
            Tint(seedLabel, RoleText);
            seedField = ForgeUi.Field(root, spec.seed.ToString(), "种子", 13, palette.bg, palette.text, palette.dim,
                20, 626, 236, 32);
            Tint(seedField.targetGraphic, RoleBg);
            seedField.onValueChanged.AddListener(value =>
            {
                if (int.TryParse(value, out int parsed)) spec.seed = parsed;
            });
            var reroll = ForgeUi.Button(root, "换一个", 13, palette.panelAlt, palette.text, 264, 626, 116, 32, () =>
            {
                spec.seed = UnityEngine.Random.Range(1000, 999999);
                seedField.text = spec.seed.ToString();
            });
            Tint(reroll.targetGraphic, RolePanelAlt);

            // 生成
            generateButton = ForgeUi.Button(root, "生成关卡  →", 15, palette.accent, palette.bg, 20, 676, 360, 46, Generate);
            Tint(generateButton.targetGraphic, RoleAccent);
            Tint(ForgeUi.TextOf(generateButton), RoleAccentText);

            var note = ForgeUi.Label(root,
                "每一关都跑过求解器：可解才算生成成功。\n若手改把水路改断，右侧会立刻标红提醒。", 11, palette.dim,
                TextAnchor.UpperLeft, 20, 736, 360, 60);
            Tint(note, RoleDim);
        }

        void BuildMiddlePanel()
        {
            middlePanel = ForgeUi.Box(canvasRect, "MiddlePanel", palette.panel, 440, 100, 560, 892);
            Tint(middlePanel, RolePanel);
            var root = middlePanel.rectTransform;

            var heading = ForgeUi.Label(root, "② 手动编辑区", 17, palette.accent, TextAnchor.UpperLeft, 20, 14, 300, 26, false);
            Tint(heading, RoleAccent);
            var note = ForgeUi.Label(root, "点色板选格子，再点棋盘涂改；每次改动都会重新求解校验。", 11, palette.dim,
                TextAnchor.UpperLeft, 20, 42, 520, 18, false);
            Tint(note, RoleDim);

            // 尺寸
            var sizeCaption = ForgeUi.Label(root, "尺寸", 12, palette.dim, TextAnchor.UpperLeft, 20, 68, 34, 22, false);
            Tint(sizeCaption, RoleDim);
            var widthCaption = ForgeUi.Label(root, "宽", 12, palette.dim, TextAnchor.UpperLeft, 58, 68, 18, 22, false);
            Tint(widthCaption, RoleDim);
            var widthMinus = ForgeUi.Button(root, "−", 14, palette.panelAlt, palette.text, 78, 64, 24, 28, () => Resize(-1, 0));
            var widthPlus = ForgeUi.Button(root, "+", 14, palette.panelAlt, palette.text, 136, 64, 24, 28, () => Resize(1, 0));
            widthValue = ForgeUi.Label(root, "7", 12, palette.text, TextAnchor.MiddleCenter, 104, 64, 30, 28, false);
            Tint(widthValue, RoleText);

            var heightCaption = ForgeUi.Label(root, "高", 12, palette.dim, TextAnchor.UpperLeft, 172, 68, 18, 22, false);
            Tint(heightCaption, RoleDim);
            var heightMinus = ForgeUi.Button(root, "−", 14, palette.panelAlt, palette.text, 192, 64, 24, 28, () => Resize(0, -1));
            var heightPlus = ForgeUi.Button(root, "+", 14, palette.panelAlt, palette.text, 250, 64, 24, 28, () => Resize(0, 1));
            heightValue = ForgeUi.Label(root, "7", 12, palette.text, TextAnchor.MiddleCenter, 218, 64, 30, 28, false);
            Tint(heightValue, RoleText);

            Tint(widthMinus.targetGraphic, RolePanelAlt);
            Tint(widthPlus.targetGraphic, RolePanelAlt);
            Tint(heightMinus.targetGraphic, RolePanelAlt);
            Tint(heightPlus.targetGraphic, RolePanelAlt);

            levelCountLabel = ForgeUi.Label(root, "", 12, palette.accent, TextAnchor.MiddleRight, 300, 64, 240, 28, false);
            Tint(levelCountLabel, RoleAccent);

            // 色板
            brushLabel = ForgeUi.Label(root, "", 12, palette.accent, TextAnchor.UpperLeft, 20, 98, 300, 18, false);
            Tint(brushLabel, RoleAccent);
            for (int i = 0; i < ForgeTiles.Palette.Length; i++)
            {
                int index = i;
                char tile = ForgeTiles.Palette[i];
                var button = ForgeUi.Button(root, PaletteCaptions[i], 13, palette.panelAlt, palette.text,
                    20 + (i % 8) * 64, 120 + (i / 8) * 32, 60, 28, () => SetBrush(tile));
                Tint(button.targetGraphic, RolePanelAlt);
                paletteButtons.Add(button);
                if (tile == brush) SetBrush(tile);
            }

            // 棋盘
            var boardCaption = ForgeUi.Label(root, "关卡棋盘", 12, palette.dim, TextAnchor.UpperLeft, 20, 226, 300, 18, false);
            Tint(boardCaption, RoleDim);
            painter = ForgeBoard.Create(root, "Painter", 20, 246, 520, 428, true, PaintCell);

            // ASCII
            var asciiCaption = ForgeUi.Label(root, "ASCII 盘面（可整体粘贴/替换后点应用）", 11, palette.dim,
                TextAnchor.UpperLeft, 20, 684, 380, 18, false);
            Tint(asciiCaption, RoleDim);
            var apply = ForgeUi.Button(root, "应用文本", 12, palette.panelAlt, palette.text, 440, 680, 100, 24, ApplyAscii);
            Tint(apply.targetGraphic, RolePanelAlt);
            asciiField = ForgeUi.Field(root, "", "# 墙  . 地面  ~ 水  P 鱼  E 出口", 10, palette.bg, palette.text,
                palette.dim, 20, 708, 520, 152, true);
            Tint(asciiField.targetGraphic, RoleBg);
        }

        void BuildRightPanel()
        {
            rightPanel = ForgeUi.Box(canvasRect, "RightPanel", palette.panel, 1016, 100, 880, 892);
            Tint(rightPanel, RolePanel);
            var root = rightPanel.rectTransform;

            var heading = ForgeUi.Label(root, "③ 关卡预览与校验", 17, palette.accent, TextAnchor.UpperLeft, 20, 14, 400, 26, false);
            Tint(heading, RoleAccent);
            var note = ForgeUi.Label(root, "选中关卡看盘面与答案，也可以直接试玩验证。", 11, palette.dim,
                TextAnchor.UpperLeft, 20, 42, 500, 18, false);
            Tint(note, RoleDim);

            tabsRow = ForgeUi.Node(root, "Tabs");
            ForgeUi.Place(tabsRow, 20, 66, 840, 32);

            preview = ForgeBoard.Create(root, "Preview", 20, 108, 520, 520, false, null);

            previewTitle = ForgeUi.Label(root, "未生成", 16, palette.text, TextAnchor.UpperLeft, 556, 110, 304, 26, false);
            Tint(previewTitle, RoleText);
            previewInfo = ForgeUi.Label(root, "", 12, palette.dim, TextAnchor.UpperLeft, 556, 140, 304, 122);
            Tint(previewInfo, RoleDim);

            var hintCaption = ForgeUi.Label(root, "提示", 12, palette.accent, TextAnchor.UpperLeft, 556, 266, 304, 16, false);
            Tint(hintCaption, RoleAccent);
            previewHint = ForgeUi.Label(root, "—", 12, palette.text, TextAnchor.UpperLeft, 556, 284, 304, 74);
            Tint(previewHint, RoleText);

            var answerCaption = ForgeUi.Label(root, "参考答案（求解器给出的最少步法）", 11, palette.accent,
                TextAnchor.UpperLeft, 556, 362, 304, 16, false);
            Tint(answerCaption, RoleAccent);
            previewAnswer = ForgeUi.Label(root, "—", 12, palette.text, TextAnchor.UpperLeft, 556, 380, 304, 68);
            Tint(previewAnswer, RoleText);

            var noteCaption = ForgeUi.Label(root, "答案备注（手写，随导出一起输出）", 11, palette.dim,
                TextAnchor.UpperLeft, 556, 454, 304, 16, false);
            Tint(noteCaption, RoleDim);
            answerField = ForgeUi.Field(root, "", "例：先破闸，再借泉眼过桥", 11, palette.bg, palette.text,
                palette.dim, 556, 472, 304, 32);
            Tint(answerField.targetGraphic, RoleBg);
            answerField.onValueChanged.AddListener(value =>
            {
                if (syncingAnswer) return;
                if (pack == null || cursor >= pack.levels.Count) return;
                pack.levels[cursor].answerNote = value;
            });

            var recompute = ForgeUi.Button(root, "按当前盘面重算提示与答案", 12, palette.panelAlt, palette.text,
                556, 512, 304, 28, RecomputeHint);
            Tint(recompute.targetGraphic, RolePanelAlt);

            trialStatus = ForgeUi.Label(root, "", 12, palette.dim, TextAnchor.UpperLeft, 556, 548, 304, 76);
            Tint(trialStatus, RoleDim);

            // 试玩控制
            var trialCaption = ForgeUi.Label(root, "试玩（方向键 / WASD 移动，E 顺时针、Q 逆时针，R 重置）", 11,
                palette.dim, TextAnchor.UpperLeft, 20, 640, 840, 18, false);
            Tint(trialCaption, RoleDim);

            moveButtons.Add(ForgeUi.Button(root, "←", 15, palette.panelAlt, palette.text, 20, 662, 74, 34, () => PlayMove(Move.Left)));
            moveButtons.Add(ForgeUi.Button(root, "↑", 15, palette.panelAlt, palette.text, 100, 662, 74, 34, () => PlayMove(Move.Up)));
            moveButtons.Add(ForgeUi.Button(root, "↓", 15, palette.panelAlt, palette.text, 180, 662, 74, 34, () => PlayMove(Move.Down)));
            moveButtons.Add(ForgeUi.Button(root, "→", 15, palette.panelAlt, palette.text, 260, 662, 74, 34, () => PlayMove(Move.Right)));
            moveButtons.Add(ForgeUi.Button(root, "顺时针", 12, palette.panelAlt, palette.text, 344, 662, 84, 34, () => PlayMove(Move.Turn)));
            moveButtons.Add(ForgeUi.Button(root, "逆时针", 12, palette.panelAlt, palette.text, 432, 662, 84, 34, () => PlayMove(Move.TurnLeft)));
            moveButtons.Add(ForgeUi.Button(root, "重置", 12, palette.panelAlt, palette.text, 520, 662, 74, 34, ResetTrial));
            moveButtons.Add(ForgeUi.Button(root, "结束试玩", 12, palette.panelAlt, palette.text, 600, 662, 96, 34, ExitTrial));
            foreach (var button in moveButtons) Tint(button.targetGraphic, RolePanelAlt);

            var legend = ForgeUi.Label(root,
                "图例：墙 #    地面 .    水 ~    鱼 P    出口 E    箱子 B    重压 X    门 1/2    阀 ↑→↓←    感应 S/s    闸 D    珠 K    膜 M    泉 o    栅 G",
                10, palette.dim, TextAnchor.UpperLeft, 20, 712, 840, 40);
            Tint(legend, RoleDim);
        }

        void BuildBottomBar()
        {
            bottomBar = ForgeUi.Box(canvasRect, "BottomBar", palette.panel, 24, 1004, 1872, 56);
            Tint(bottomBar, RolePanel);
            var root = bottomBar.rectTransform;

            var gen = ForgeUi.Button(root, "生成关卡", 14, palette.accent, palette.bg, 0, 8, 196, 40, Generate);
            validateButton = ForgeUi.Button(root, "校验当前关", 14, palette.panelAlt, palette.text, 208, 8, 196, 40, ValidateAndReport);
            trialButton = ForgeUi.Button(root, "试玩", 14, palette.panelAlt, palette.text, 416, 8, 196, 40, StartTrial);
            replayButton = ForgeUi.Button(root, "回放答案", 14, palette.panelAlt, palette.text, 624, 8, 196, 40, ReplaySolution);
            saveButton = ForgeUi.Button(root, "保存工程", 14, palette.panelAlt, palette.text, 832, 8, 196, 40, SavePack);
            exportButton = ForgeUi.Button(root, "导出关卡", 14, palette.panelAlt, palette.text, 1040, 8, 196, 40, ExportPack);
            var sceneButton = ForgeUi.Button(root, "导出场景", 14, palette.panelAlt, palette.text, 1248, 8, 196, 40, ExportScenePack);
            loadButton = ForgeUi.Button(root, "读取工程", 14, palette.panelAlt, palette.text, 1456, 8, 196, 40, LoadPack);
            clearButton = ForgeUi.Button(root, "清空重来", 14, palette.panelAlt, palette.text, 1664, 8, 196, 40, ClearWorkspace);

            Tint(gen.targetGraphic, RoleAccent);
            Tint(ForgeUi.TextOf(gen), RoleAccentText);
            foreach (var button in new[] {validateButton, trialButton, replayButton, saveButton, exportButton, sceneButton, loadButton, clearButton})
                Tint(button.targetGraphic, RolePanelAlt);
        }

        // ==================================================================== 主题

        void Tint(Graphic graphic, int role)
        {
            if (graphic == null) return;
            themeTargets.Add(new ThemeTarget {graphic = graphic, role = role});
            graphic.color = RoleColor(role);
        }

        Color RoleColor(int role)
        {
            switch (role)
            {
                case RoleBg: return palette.bg;
                case RolePanel: return palette.panel;
                case RolePanelAlt: return palette.panelAlt;
                case RoleLine: return palette.line;
                case RoleText: return palette.text;
                case RoleDim: return palette.dim;
                case RoleAccent: return palette.accent;
                case RoleAccentText: return palette.bg;
                case RoleHazard: return palette.hazard;
                case RoleExit: return palette.exit;
                default: return palette.text;
            }
        }

        void ApplyTheme()
        {
            palette = ForgePalette.Of(spec.theme);
            foreach (var target in themeTargets)
                if (target.graphic != null)
                    target.graphic.color = RoleColor(target.role);

            if (themeValue != null) themeValue.text = ForgeLabels.Of(spec.theme);
            PaintSegments();
            PaintPalette();
            RefreshPainter();
            RefreshPreview();
        }

        void CycleTheme(int delta)
        {
            int count = Enum.GetValues(typeof(ForgeTheme)).Length;
            spec.theme = (ForgeTheme)(((int)spec.theme + delta + count) % count);
            spec.titlePrefix = ForgeLabels.Of(spec.theme);
            ApplyTheme();
            SetStatus("主题切换为「" + ForgeLabels.Of(spec.theme) + "」，重新生成即可应用新配色与关卡名。");
        }

        // ================================================================ 参数交互

        void SetBrush(char tile)
        {
            brush = tile;
            PaintPalette();
            var style = ForgeTiles.Style(tile, palette);
            brushLabel.text = $"色板 · 当前画笔：{style.name}（{tile}）";
        }

        void PaintPalette()
        {
            for (int i = 0; i < paletteButtons.Count; i++)
            {
                bool on = i < ForgeTiles.Palette.Length && ForgeTiles.Palette[i] == brush;
                var button = paletteButtons[i];
                ForgeUi.SetBg(button, on ? palette.accent : palette.panelAlt);
                var text = ForgeUi.TextOf(button);
                if (text != null) text.color = on ? palette.bg : palette.text;
            }
        }

        void PaintSegments()
        {
            ForgeUi.PaintSegments(difficultySegments, (int)spec.difficulty, palette.panelAlt, palette.accent,
                palette.text, palette.bg);
            ForgeUi.PaintSegments(hintSegments, (int)spec.hintMode, palette.panelAlt, palette.accent,
                palette.text, palette.bg);
            ForgeUi.PaintSegments(winSegments, (int)spec.winRule, palette.panelAlt, palette.accent,
                palette.text, palette.bg);
        }

        void SetDifficulty(int index)
        {
            spec.difficulty = (ForgeDifficulty)Mathf.Clamp(index, 0, 3);
            difficultyNote.text = ForgeLabels.DifficultyNotes[(int)spec.difficulty];
            PaintSegments();
            SetStatus($"难度设为「{ForgeLabels.Of(spec.difficulty)}」，盘面 {ForgeGenerator.SizeOf(spec.difficulty)}×{ForgeGenerator.SizeOf(spec.difficulty)}。");
        }

        void SetHintMode(int index)
        {
            spec.hintMode = (ForgeHintMode)Mathf.Clamp(index, 0, 2);
            PaintSegments();
            RecomputeHint();
        }

        void SetWinRule(int index)
        {
            spec.winRule = (ForgeWinRule)Mathf.Clamp(index, 0, 2);
            PaintSegments();
            ValidateCurrent();
            RefreshPreview();
            SetStatus(spec.winRule == ForgeWinRule.MoveLimit
                ? "胜负条件改为「限步抵达」，生成时会过滤掉超过限步的盘面。"
                : $"胜负条件：{ForgeLabels.Of(spec.winRule)}。");
        }

        void ChangeCount(int delta)
        {
            spec.levelCount = Mathf.Clamp(spec.levelCount + delta, 1, 8);
            countValue.text = spec.levelCount.ToString();
        }

        void ChangeMoveLimit(int delta)
        {
            spec.moveLimit = Mathf.Clamp(spec.moveLimit + delta, 4, 60);
            moveLimitValue.text = spec.moveLimit.ToString();
            ValidateCurrent();
            RefreshPreview();
        }

        bool RuleOf(int index)
        {
            var rules = spec.rules;
            switch (index)
            {
                case 0: return rules.crates;
                case 1: return rules.portals;
                case 2: return rules.valves;
                case 3: return rules.sensorGates;
                case 4: return rules.plates;
                case 5: return rules.membranes;
                default: return rules.springs;
            }
        }

        void SetRule(int index, bool value)
        {
            var rules = spec.rules;
            switch (index)
            {
                case 0: rules.crates = value; break;
                case 1: rules.portals = value; break;
                case 2: rules.valves = value; break;
                case 3: rules.sensorGates = value; break;
                case 4: rules.plates = value; break;
                case 5: rules.membranes = value; break;
                default: rules.springs = value; break;
            }
        }

        // ================================================================ 生成与编辑

        void Generate()
        {
            ExitTrial();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            pack = ForgeGenerator.Generate(spec);
            watch.Stop();
            cursor = 0;
            RebuildTabs();
            LoadLevelIntoEditor();
            int solved = 0;
            foreach (var level in pack.levels) if (level.solvable) solved++;
            SetStatus($"已生成 {pack.levels.Count} 关（{ForgeLabels.Of(spec.difficulty)} · {ForgeLabels.Of(spec.theme)}），可解 {solved} 关，用时 {watch.ElapsedMilliseconds} ms。");
        }

        void NewWorkspace(string[] data)
        {
            pack = new ForgePack
            {
                packName = "Forge_草稿",
                spec = spec.Clone(),
                levels = new List<ForgeLevel>
                {
                    new ForgeLevel
                    {
                        name = $"{spec.titlePrefix} 01",
                        rows = data,
                        theme = spec.theme,
                        difficulty = spec.difficulty,
                        winRule = spec.winRule
                    }
                }
            };
            cursor = 0;
            RebuildTabs();
            LoadLevelIntoEditor();
        }

        void ClearWorkspace()
        {
            ExitTrial();
            spec.authorHint = "";
            hintField.text = "";
            NewWorkspace(ForgeGenerator.Blank(7));
            SetStatus("已清空为一张 7×7 空白盘，可直接手画。");
        }

        void LoadLevelIntoEditor()
        {
            rows.Clear();
            if (pack == null || pack.levels.Count == 0)
            {
                RefreshPainter();
                RefreshPreview();
                return;
            }
            cursor = Mathf.Clamp(cursor, 0, pack.levels.Count - 1);
            var level = pack.levels[cursor];
            if (level.rows != null && level.rows.Length > 0) rows.AddRange(level.rows);
            else rows.AddRange(ForgeGenerator.Blank(7));

            if (level.theme != spec.theme) level.theme = spec.theme;
            if (level.winRule != spec.winRule) level.winRule = spec.winRule;

            if (answerField != null)
            {
                syncingAnswer = true;
                answerField.text = level.answerNote ?? "";
                syncingAnswer = false;
            }

            RefreshPainter();
            RefreshAscii();
            ValidateCurrent();
            RefreshPreview();
            PaintTabs();
        }

        void SelectLevel(int index)
        {
            if (pack == null) return;
            ExitTrial();
            cursor = Mathf.Clamp(index, 0, pack.levels.Count - 1);
            LoadLevelIntoEditor();
            SetStatus($"已选中 {pack.levels[cursor].name}。");
        }

        void RebuildTabs()
        {
            foreach (var button in tabButtons)
                if (button != null)
                {
                    button.gameObject.SetActive(false);
                    Destroy(button.gameObject);
                }
            tabButtons.Clear();
            if (pack == null || tabsRow == null) return;

            for (int i = 0; i < pack.levels.Count; i++)
            {
                int index = i;
                var button = ForgeUi.Button(tabsRow, (i + 1).ToString(), 13, palette.panelAlt, palette.text,
                    i * 52, 0, 46, 30, () => SelectLevel(index));
                Tint(button.targetGraphic, RolePanelAlt);
                tabButtons.Add(button);
            }
            PaintTabs();
        }

        void PaintTabs()
        {
            for (int i = 0; i < tabButtons.Count; i++)
            {
                bool on = i == cursor;
                ForgeUi.SetBg(tabButtons[i], on ? palette.accent : palette.panelAlt);
                var text = ForgeUi.TextOf(tabButtons[i]);
                if (text != null) text.color = on ? palette.bg : palette.text;
            }
        }

        void PaintCell(int x, int y)
        {
            if (rows.Count == 0 || y < 0 || y >= rows.Count) return;
            if (x < 0 || x >= rows[y].Length) return;

            if (brush == 'P' || brush == 'E' || brush == '1' || brush == '2') ClearTile(brush);
            var chars = rows[y].ToCharArray();
            chars[x] = brush;
            rows[y] = new string(chars);

            ExitTrial();
            RefreshPainter();
            RefreshAscii(false);
            ValidateCurrent();
            RefreshPreview();
        }

        void ClearTile(char tile)
        {
            for (int y = 0; y < rows.Count; y++)
            {
                if (rows[y].IndexOf(tile) < 0) continue;
                var chars = rows[y].ToCharArray();
                for (int x = 0; x < chars.Length; x++)
                    if (chars[x] == tile)
                        chars[x] = '.';
                rows[y] = new string(chars);
            }
        }

        void ApplyAscii()
        {
            if (asciiField == null) return;
            var lines = new List<string>();
            foreach (var raw in asciiField.text.Replace("\r", "").Split('\n'))
                lines.Add(raw);
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1])) lines.RemoveAt(lines.Count - 1);
            if (lines.Count == 0) { SetStatus("文本为空，没东西可应用。", true); return; }

            int width = 3;
            foreach (var line in lines) width = Mathf.Max(width, line.Length);
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Length < width) lines[i] = lines[i].PadRight(width, '#');

            rows.Clear();
            rows.AddRange(lines);
            ExitTrial();
            RefreshPainter();
            RefreshAscii(false);
            ValidateCurrent();
            RefreshPreview();
            SetStatus($"已应用文本盘面：{width}×{lines.Count}。{validation.message}");
        }

        void Resize(int deltaWidth, int deltaHeight)
        {
            if (rows.Count == 0) return;
            int width = Mathf.Clamp(rows[0].Length + deltaWidth, 5, 15);
            int height = Mathf.Clamp(rows.Count + deltaHeight, 5, 15);
            var next = new List<string>(height);
            for (int y = 0; y < height; y++)
            {
                var sb = new StringBuilder(width);
                for (int x = 0; x < width; x++)
                {
                    bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    if (border) sb.Append('#');
                    else sb.Append(y < rows.Count && x < rows[y].Length ? rows[y][x] : '.');
                }
                next.Add(sb.ToString());
            }
            rows.Clear();
            rows.AddRange(next);
            ExitTrial();
            RefreshPainter();
            RefreshAscii(false);
            ValidateCurrent();
            RefreshPreview();
        }

        void RefreshPainter()
        {
            if (painter == null) return;
            if (rows.Count == 0) { painter.Clear(); return; }
            painter.Render(rows.ToArray(), palette);
            if (widthValue != null) widthValue.text = rows[0].Length.ToString();
            if (heightValue != null) heightValue.text = rows.Count.ToString();
            levelCountLabel.text = pack == null ? "" : $"工程共 {pack.levels.Count} 关";
        }

        void RefreshAscii(bool force = true)
        {
            if (asciiField == null || rows.Count == 0) return;
            if (!force && asciiField.isFocused) return;
            asciiField.text = string.Join("\n", rows);
        }

        void ValidateCurrent()
        {
            if (rows.Count == 0)
            {
                validation = new ForgeValidation {message = "盘面为空"};
                return;
            }
            validation = ForgeGenerator.Validate(rows.ToArray(), spec.winRule, spec.moveLimit);
            if (pack == null || cursor >= pack.levels.Count) return;
            var level = pack.levels[cursor];
            level.rows = rows.ToArray();
            level.solvable = validation.ok;
            level.solution = validation.solution ?? "";
            level.steps = validation.steps;
            level.issue = validation.ok ? "" : validation.message;
        }

        void ValidateAndReport()
        {
            ValidateCurrent();
            RefreshPreview();
            SetStatus(validation.message, !validation.ok);
        }

        // ================================================================ 预览与试玩

        void RefreshPreview()
        {
            if (preview == null) return;
            if (trialActive && trialLevel != null && trialPosition != null)
                preview.Render(RowsFromPosition(trialLevel, trialPosition), palette);
            else if (rows.Count > 0)
                preview.Render(rows.ToArray(), palette);

            RefreshInfo();
        }

        void RefreshInfo()
        {
            if (previewTitle == null) return;
            var level = pack != null && cursor < pack.levels.Count ? pack.levels[cursor] : null;
            previewTitle.text = level == null ? "未生成" : $"{level.name} · {ForgeLabels.Of(spec.difficulty)}";
            previewInfo.text =
                $"尺寸　{validation.width}×{validation.height}\n" +
                $"最少步数　{(validation.steps > 0 ? validation.steps.ToString() : "—")}\n" +
                $"胜负　{ForgeLabels.Of(spec.winRule)}{(spec.winRule == ForgeWinRule.MoveLimit ? $"（限 {spec.moveLimit} 步）" : "")}\n" +
                $"种子　{(level == null ? "—" : level.seed.ToString())}\n" +
                $"状态　{(validation.ok ? "可解" : "待修")}";
            previewInfo.color = validation.ok ? palette.dim : palette.hazard;

            previewHint.text = level == null || string.IsNullOrEmpty(level.hint) ? "—" : level.hint;
            previewAnswer.text = string.IsNullOrEmpty(validation.solution)
                ? "—"
                : $"最少 {validation.steps} 步：\n{ForgeGenerator.RouteText(validation.solution)}";

            if (trialActive)
            {
                trialStatus.text = trialWon
                    ? $"✓ 通关 · {trialSteps} 步（最优 {validation.steps} 步）"
                    : $"试玩中 · 已走 {trialSteps} 步";
                trialStatus.color = trialWon ? palette.exit : palette.accent;
            }
            else
            {
                trialStatus.text = validation.ok ? "✓ " + validation.message : "✗ " + validation.message;
                trialStatus.color = validation.ok ? palette.exit : palette.hazard;
            }
        }

        static string[] RowsFromPosition(Level level, Position position) => ForgeBoard.RowsFrom(level, position);

        void StartTrial()
        {
            if (replayRoutine != null)
            {
                StopCoroutine(replayRoutine);
                replayRoutine = null;
            }
            if (rows.Count == 0) return;
            if (!validation.playable && !validation.ok)
            {
                SetStatus("先修好盘面再试玩：" + validation.message, true);
                return;
            }

            Level level;
            try
            {
                level = new Level("trial", "", rows.ToArray());
            }
            catch (Exception e)
            {
                SetStatus("无法试玩：" + e.Message, true);
                return;
            }

            trialLevel = level;
            trialPosition = Puzzle.Begin(level);
            trialSteps = 0;
            trialWon = false;
            trialActive = true;
            RefreshPreview();
            SetStatus("试玩中：方向键/WASD 移动，E 顺时针、Q 逆时针，R 重置。");
        }

        void ExitTrial()
        {
            if (replayRoutine != null)
            {
                StopCoroutine(replayRoutine);
                replayRoutine = null;
            }
            trialActive = false;
            trialWon = false;
            trialLevel = null;
            trialPosition = null;
            if (trialStatus != null) trialStatus.text = "";
        }

        void ResetTrial()
        {
            if (trialLevel == null)
            {
                StartTrial();
                return;
            }
            trialPosition = Puzzle.Begin(trialLevel);
            trialSteps = 0;
            trialWon = false;
            trialActive = true;
            RefreshPreview();
            SetStatus("已重置到开局。");
        }

        void PlayMove(Move move)
        {
            if (!trialActive || trialPosition == null || trialLevel == null) return;
            if (trialWon)
            {
                SetStatus("这一关已经通关了，按 R 重置可以再玩一次。");
                return;
            }

            var next = Puzzle.Apply(trialLevel, trialPosition, move, out var block);
            if (next == null)
            {
                SetStatus(block == Block.Dry ? "那边没有水，鱼游不过去。" :
                    block == Block.Crate ? "箱子顶不动。" :
                    block == Block.Grate ? "栅栏挡住鱼，水才过得去。" :
                    block == Block.Arrow ? "单向阀方向不对。" : "这一步走不了。", true);
                return;
            }
            if (Puzzle.OnGate(trialLevel, next))
            {
                SetStatus("踩到闸门，鱼被冲走了。按 R 重来。", true);
                trialPosition = Puzzle.Begin(trialLevel);
                trialSteps = 0;
                RefreshPreview();
                return;
            }

            trialPosition = next;
            trialSteps++;
            RefreshPreview();

            if (Puzzle.Won(trialLevel, next))
            {
                trialWon = true;
                SetStatus($"通关！用了 {trialSteps} 步，最优 {validation.steps} 步。");
                trialStatus.text = $"✓ 通关 · {trialSteps} 步";
                trialStatus.color = palette.exit;
            }
        }

        void ReplaySolution()
        {
            if (string.IsNullOrEmpty(validation.solution))
            {
                SetStatus("当前盘面没有可用答案，先在右侧看校验结果。", true);
                return;
            }
            if (replayRoutine != null) StopCoroutine(replayRoutine);
            replayRoutine = StartCoroutine(ReplayRoutine());
        }

        IEnumerator ReplayRoutine()
        {
            StartTrial();
            if (!trialActive) yield break;

            yield return new WaitForSeconds(0.3f);
            foreach (char c in validation.solution)
            {
                if (!trialActive) yield break;
                int index = c - '0';
                if (index >= 0 && index <= 5) PlayMoveSilent((Move)index);
                yield return new WaitForSeconds(0.45f);
            }
            replayRoutine = null;
            SetStatus($"答案回放结束：{validation.steps} 步通关。");
        }

        void PlayMoveSilent(Move move)
        {
            if (!trialActive || trialPosition == null) return;
            var next = Puzzle.Apply(trialLevel, trialPosition, move, out _);
            if (next == null) return;
            trialPosition = next;
            trialSteps++;
            RefreshPreview();
            if (Puzzle.Won(trialLevel, next))
            {
                trialWon = true;
                trialStatus.text = $"✓ 通关 · {trialSteps} 步";
                trialStatus.color = palette.exit;
            }
        }

        void RecomputeHint()
        {
            if (pack == null || cursor >= pack.levels.Count)
            {
                SetStatus("先生成一关再重算提示。", true);
                return;
            }
            if (rows.Count == 0) return;

            var level = pack.levels[cursor];
            try
            {
                var parsed = new Level(level.name, "", rows.ToArray());
                var route = Puzzle.Solve(parsed);
                level.rows = rows.ToArray();
                level.solution = route ?? "";
                level.steps = string.IsNullOrEmpty(route) ? 0 : route.Length;
                level.solvable = !string.IsNullOrEmpty(route);
                level.hint = string.IsNullOrEmpty(route)
                    ? spec.authorHint
                    : ForgeGenerator.ComposeHint(spec, parsed, route);
                level.issue = level.solvable ? "" : "无解：水到不了出口";
            }
            catch (Exception e)
            {
                SetStatus("重算提示失败：" + e.Message, true);
                return;
            }

            ValidateCurrent();
            RefreshPreview();
            SetStatus("已按当前盘面重算提示与答案。");
        }

        // ================================================================ 存档与导出

        void SavePack()
        {
            if (pack == null) { SetStatus("还没有可保存的工程，先点“生成关卡”。", true); return; }
            SyncPackSpec();
            string path = ForgeIO.Save(pack);
            SetStatus("已保存工程：" + Shorten(path));
        }

        void ExportPack()
        {
            if (pack == null) { SetStatus("还没有可导出的工程，先点“生成关卡”。", true); return; }
            SyncPackSpec();
            string folder = ForgeIO.Export(pack);
            SetStatus("已导出（json / txt / cs），关卡场景退出播放后生成：" + Shorten(folder));
        }

        void ExportScenePack()
        {
            if (pack == null) { SetStatus("还没有可导出的工程，先点“生成关卡”。", true); return; }
            SyncPackSpec();
            try
            {
                string folder = ForgeIO.ExportScene(pack);
                SetStatus("已排队生成正式关卡场景：退出播放后自动写入 " + Shorten(folder) + "（Hierarchy 与 Level_XX 一致）");
            }
            catch (Exception e)
            {
                SetStatus("导出场景失败：" + e.Message, true);
            }
        }

        void LoadPack()
        {
            if (!ForgeIO.TryLoadLast(out var loaded, out var file))
            {
                SetStatus("没有找到已保存的工程（先保存一次）。", true);
                return;
            }
            ExitTrial();
            pack = loaded;
            spec = pack.spec ?? new ForgeSpec();
            if (spec.rules == null) spec.rules = new ForgeRules();
            spec.levelCount = Mathf.Clamp(pack.levels.Count, 1, 8);
            cursor = 0;
            SyncSpecUi();
            ApplyTheme();
            RebuildTabs();
            LoadLevelIntoEditor();
            SetStatus($"已读取工程：{Shorten(file)}（{pack.levels.Count} 关）");
        }

        void SyncPackSpec()
        {
            pack.spec = spec.Clone();
            pack.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            if (pack.levels.Count > 0 && cursor < pack.levels.Count)
            {
                var level = pack.levels[cursor];
                level.theme = spec.theme;
                level.winRule = spec.winRule;
                level.moveLimit = spec.winRule == ForgeWinRule.MoveLimit ? spec.moveLimit : 0;
            }
        }

        void SyncSpecUi()
        {
            themeValue.text = ForgeLabels.Of(spec.theme);
            difficultyNote.text = ForgeLabels.DifficultyNotes[(int)spec.difficulty];
            countValue.text = spec.levelCount.ToString();
            moveLimitValue.text = spec.moveLimit.ToString();
            seedField.text = spec.seed.ToString();
            hintField.text = spec.authorHint;
            for (int i = 0; i < ruleToggles.Count; i++) ruleToggles[i].isOn = RuleOf(i);
            PaintSegments();
        }

        // ================================================================ 杂项

        void SetStatus(string message, bool error = false)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.color = error ? palette.hazard : palette.accent;
            statusTimeout = Time.realtimeSinceStartup + 8f;
        }

        static string Shorten(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string data = Application.dataPath.Replace('\\', '/');
            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith(data) ? "Assets" + normalized.Substring(data.Length) : normalized;
        }
    }
}

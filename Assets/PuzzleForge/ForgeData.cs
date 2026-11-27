using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterTurn.Forge
{
    public enum ForgeTheme
    {
        WaterHall = 0,
        MossRuin = 1,
        LavaCave = 2,
        DeepRift = 3
    }

    public enum ForgeDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Expert = 3
    }

    /// <summary>胜负条件：抵达出口 / 先取珍珠再离开 / 在限定步数内抵达。</summary>
    public enum ForgeWinRule
    {
        ReachExit = 0,
        PearlThenExit = 1,
        MoveLimit = 2
    }

    public enum ForgeHintMode
    {
        None = 0,
        OneLine = 1,
        Steps = 2
    }

    /// <summary>生成器可用的机制开关。</summary>
    [Serializable]
    public sealed class ForgeRules
    {
        public bool crates = true;
        public bool portals = true;
        public bool valves;
        public bool sensorGates;
        public bool plates;
        public bool membranes;
        public bool springs;

        public void CopyFrom(ForgeRules other)
        {
            if (other == null) return;
            crates = other.crates;
            portals = other.portals;
            valves = other.valves;
            sensorGates = other.sensorGates;
            plates = other.plates;
            membranes = other.membranes;
            springs = other.springs;
        }
    }

    /// <summary>一次生成的输入参数，保存工程时整份落盘。</summary>
    [Serializable]
    public sealed class ForgeSpec
    {
        public ForgeTheme theme = ForgeTheme.WaterHall;
        public ForgeDifficulty difficulty = ForgeDifficulty.Normal;
        public ForgeRules rules = new ForgeRules();
        public int levelCount = 3;
        public ForgeHintMode hintMode = ForgeHintMode.OneLine;
        public ForgeWinRule winRule = ForgeWinRule.ReachExit;
        public int moveLimit = 24;
        public int seed = 20261125;
        public string titlePrefix = "回廊";
        public string authorHint = "";

        public ForgeSpec Clone()
        {
            var copy = new ForgeSpec
            {
                theme = theme,
                difficulty = difficulty,
                rules = new ForgeRules(),
                levelCount = levelCount,
                hintMode = hintMode,
                winRule = winRule,
                moveLimit = moveLimit,
                seed = seed,
                titlePrefix = titlePrefix,
                authorHint = authorHint
            };
            copy.rules.CopyFrom(rules);
            return copy;
        }
    }

    /// <summary>单个关卡：rows 是权威 ASCII 盘面，可直接喂给 WaterTurn.Level。</summary>
    [Serializable]
    public sealed class ForgeLevel
    {
        public string name = "Level";
        public string hint = "";
        /// <summary>设计者手写的答案说明，会跟自动求解出来的步法一起导出。</summary>
        public string answerNote = "";
        public string[] rows = new string[0];
        public string solution = "";
        public int steps;
        public bool solvable = true;
        public string issue = "";
        public ForgeTheme theme;
        public ForgeDifficulty difficulty;
        public ForgeWinRule winRule;
        public int moveLimit;
        public int seed;

        public int Width => rows == null || rows.Length == 0 ? 0 : rows[0].Length;
        public int Height => rows == null ? 0 : rows.Length;

        public ForgeLevel Clone()
        {
            return new ForgeLevel
            {
                name = name,
                hint = hint,
                answerNote = answerNote,
                rows = rows == null ? new string[0] : (string[])rows.Clone(),
                solution = solution,
                steps = steps,
                solvable = solvable,
                issue = issue,
                theme = theme,
                difficulty = difficulty,
                winRule = winRule,
                moveLimit = moveLimit,
                seed = seed
            };
        }
    }

    [Serializable]
    public sealed class ForgePack
    {
        public string packName = "PuzzleForgePack";
        public string savedAt = "";
        public ForgeSpec spec = new ForgeSpec();
        public List<ForgeLevel> levels = new List<ForgeLevel>();
    }

    /// <summary>校验结果：给预览区显示"能不能通关、最少几步、怎么走"。</summary>
    public struct ForgeValidation
    {
        public bool ok;
        public bool playable;
        public int steps;
        public string solution;
        public string message;
        public int width;
        public int height;
    }

    public struct TileStyle
    {
        public Color bg;
        public Color fg;
        public string glyph;
        public string name;
    }

    /// <summary>一套主题皮肤：UI 面板 + 盘面格子都从这里取色。</summary>
    public struct ForgePalette
    {
        public Color bg, panel, panelAlt, line, text, dim, accent, accentDark;
        public Color wall, floor, water, fish, exit, hazard, crate, plate, valve, portal, pearl, membrane, spring;

        public static ForgePalette Of(ForgeTheme theme)
        {
            switch (theme)
            {
                case ForgeTheme.MossRuin:
                    return new ForgePalette
                    {
                        bg = Hex("12180F"), panel = Hex("1B2416"), panelAlt = Hex("243020"),
                        line = Hex("3A4A2E"), text = Hex("EDF6E3"), dim = Hex("9DB08A"),
                        accent = Hex("B9E36A"), accentDark = Hex("1B2416"),
                        wall = Hex("38472C"), floor = Hex("2B3823"), water = Hex("3F8F6E"),
                        fish = Hex("FFD08A"), exit = Hex("8FE38B"), hazard = Hex("D9704F"),
                        crate = Hex("B0824F"), plate = Hex("93A08C"), valve = Hex("D9C46A"),
                        portal = Hex("C3A2FF"), pearl = Hex("F6EFC4"), membrane = Hex("6FA8B8"),
                        spring = Hex("63D3C4")
                    };
                case ForgeTheme.LavaCave:
                    return new ForgePalette
                    {
                        bg = Hex("1A0E0C"), panel = Hex("261512"), panelAlt = Hex("331E19"),
                        line = Hex("553026"), text = Hex("FCEBE2"), dim = Hex("C79C8A"),
                        accent = Hex("FF9A5C"), accentDark = Hex("261512"),
                        wall = Hex("4A2A22"), floor = Hex("2E1B16"), water = Hex("C4552F"),
                        fish = Hex("FFE0A3"), exit = Hex("9BE07B"), hazard = Hex("FF5C4D"),
                        crate = Hex("9C6A3E"), plate = Hex("B79C93"), valve = Hex("FFC15E"),
                        portal = Hex("D9A6FF"), pearl = Hex("FFF0C9"), membrane = Hex("7FA6AE"),
                        spring = Hex("FFD08A")
                    };
                case ForgeTheme.DeepRift:
                    return new ForgePalette
                    {
                        bg = Hex("080E1B"), panel = Hex("111B2E"), panelAlt = Hex("182742"),
                        line = Hex("2A3F63"), text = Hex("E8F0FF"), dim = Hex("8AA2C4"),
                        accent = Hex("7FB4FF"), accentDark = Hex("111B2E"),
                        wall = Hex("283A5A"), floor = Hex("182742"), water = Hex("2F6FD0"),
                        fish = Hex("FFD48A"), exit = Hex("7BE9C2"), hazard = Hex("E0698A"),
                        crate = Hex("9A8467"), plate = Hex("9FB0C6"), valve = Hex("9ED8FF"),
                        portal = Hex("C0A6FF"), pearl = Hex("F2F6D6"), membrane = Hex("5FC7D6"),
                        spring = Hex("5FE3FF")
                    };
                default:
                    return new ForgePalette
                    {
                        bg = Hex("101216"), panel = Hex("1C2026"), panelAlt = Hex("2A3038"),
                        line = Hex("404853"), text = Hex("F0F1ED"), dim = Hex("ABB5BF"),
                        accent = Hex("EDCA80"), accentDark = Hex("181512"),
                        wall = Hex("2A4753"), floor = Hex("1C3038"), water = Hex("2E8FB8"),
                        fish = Hex("FFC17C"), exit = Hex("7BE38F"), hazard = Hex("E06C5A"),
                        crate = Hex("C08A5E"), plate = Hex("9AA6A0"), valve = Hex("F2DE8C"),
                        portal = Hex("B08BFF"), pearl = Hex("F5F0C8"), membrane = Hex("6FC3D6"),
                        spring = Hex("7BE3D0")
                    };
            }
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }

    public static class ForgeLabels
    {
        public static readonly string[] Themes = {"水之回廊", "苔石遗迹", "熔岩洞窟", "深海裂隙"};
        public static readonly string[] Difficulties = {"简单", "普通", "困难", "专家"};
        public static readonly string[] DifficultyNotes = {"7×7 · 入门", "9×9 · 进阶", "11×11 · 烧脑", "13×13 · 硬核"};
        public static readonly string[] WinRules = {"抵达出口", "取珠后离开", "限步抵达"};
        public static readonly string[] HintModes = {"不提示", "一句话", "分步指引"};
        public static readonly string[] RuleNames = {"箱子", "传送门", "单向阀", "感应闸", "重压板", "薄膜", "泉眼"};

        public static string Of(ForgeTheme theme) => Themes[Mathf.Clamp((int)theme, 0, Themes.Length - 1)];
        public static string Of(ForgeDifficulty difficulty) => Difficulties[Mathf.Clamp((int)difficulty, 0, Difficulties.Length - 1)];
        public static string Of(ForgeWinRule rule) => WinRules[Mathf.Clamp((int)rule, 0, WinRules.Length - 1)];
        public static string Of(ForgeHintMode mode) => HintModes[Mathf.Clamp((int)mode, 0, HintModes.Length - 1)];
    }

    public static class ForgeTiles
    {
        /// <summary>色板顺序：与 LevelDefinition.rows 的图例一一对应，方便手动编辑。</summary>
        public static readonly char[] Palette =
        {
            '.', '~', 'P', 'E', '#', 'B', 'X', '1',
            '2', '>', '^', 'v', '<', 'S', 's', 'D',
            'K', 'M', 'o', 'G'
        };

        static TileStyle Make(Color bg, Color fg, string glyph, string name) =>
            new TileStyle {bg = bg, fg = fg, glyph = glyph, name = name};

        public static TileStyle Style(char tile, ForgePalette p)
        {
            switch (tile)
            {
                case '#': return Make(p.wall, p.dim, "", "墙");
                case '~': return Make(p.water, p.text, "", "水");
                case 'P': return Make(p.fish, p.accentDark, "鱼", "玩家");
                case 'E': return Make(p.exit, p.accentDark, "出", "出口");
                case 'B':
                case 'b': return Make(p.crate, p.accentDark, "箱", "箱子");
                case 'X': return Make(p.plate, p.accentDark, "压", "重压格");
                case '1': return Make(p.portal, p.accentDark, "1", "传送门甲");
                case '2': return Make(p.portal, p.accentDark, "2", "传送门乙");
                case '^': return Make(p.valve, p.accentDark, "↑", "单向阀");
                case '>': return Make(p.valve, p.accentDark, "→", "单向阀");
                case 'v': return Make(p.valve, p.accentDark, "↓", "单向阀");
                case '<': return Make(p.valve, p.accentDark, "←", "单向阀");
                case 'S': return Make(p.hazard, p.accentDark, "感", "感应格");
                case 's': return Make(p.hazard, p.accentDark, "时", "时效感应格");
                case 'D': return Make(p.hazard, p.accentDark, "闸", "石门");
                case 'd': return Make(p.portal, p.accentDark, "逆", "逆闸");
                case 'K': return Make(p.pearl, p.accentDark, "珠", "珍珠");
                case 'M': return Make(p.membrane, p.accentDark, "膜", "薄膜");
                case 'o': return Make(p.spring, p.accentDark, "泉", "泉眼");
                case 'G': return Make(p.line, p.text, "栅", "栅栏");
                default: return Make(p.floor, p.dim, "", "地面");
            }
        }

        public static bool IsKnown(char tile) => Array.IndexOf(Palette, tile) >= 0;

        public static int Count(string[] rows, char tile)
        {
            int count = 0;
            if (rows == null) return 0;
            foreach (var row in rows)
                foreach (var c in row)
                    if (c == tile) count++;
            return count;
        }
    }
}

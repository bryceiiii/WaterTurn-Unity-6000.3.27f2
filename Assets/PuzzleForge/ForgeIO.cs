using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace WaterTurn.Forge
{
    /// <summary>工程的落盘与导出：JSON 存档、每关 ASCII 纯文本、可直接粘贴的 C# 关卡表。</summary>
    public static class ForgeIO
    {
        const string LastPackKey = "WaterTurn.Forge.LastPack";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public static string Root => Path.Combine(Application.dataPath, "PuzzleForge", "Exports");

        public static string Save(ForgePack pack)
        {
            Directory.CreateDirectory(Root);
            string file = Path.Combine(Root, SafeName(pack.packName) + ".json");
            File.WriteAllText(file, JsonUtility.ToJson(pack, true), Utf8);
            PlayerPrefs.SetString(LastPackKey, file);
            PlayerPrefs.Save();
            return file;
        }

        public static bool TryLoadLast(out ForgePack pack, out string file)
        {
            file = PlayerPrefs.GetString(LastPackKey, "");
            pack = null;
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return false;
            return TryLoad(file, out pack);
        }

        public static bool TryLoad(string file, out ForgePack pack)
        {
            pack = null;
            try
            {
                pack = JsonUtility.FromJson<ForgePack>(File.ReadAllText(file, Encoding.UTF8));
            }
            catch (Exception e)
            {
                Debug.LogWarning("PuzzleForge: 读取失败 " + e.Message);
                return false;
            }
            if (pack == null) return false;
            if (pack.spec == null) pack.spec = new ForgeSpec();
            if (pack.spec.rules == null) pack.spec.rules = new ForgeRules();
            if (pack.levels == null) pack.levels = new List<ForgeLevel>();
            foreach (var level in pack.levels)
                if (level.rows == null) level.rows = new string[0];
            return true;
        }

        /// <summary>
        /// 排一个"生成正式关卡场景"的请求。Play 模式下建不了场景，所以这里只落一个
        /// 点号开头的请求文件（Unity 不会导入），退出播放后 ForgeSceneBuilder 自动接单。
        /// </summary>
        public static string RequestScenes(ForgePack pack)
        {
            string folder = Path.Combine(Application.dataPath, "PuzzleForge");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, ".forge-scene-request.json");
            File.WriteAllText(file, JsonUtility.ToJson(pack, true), Utf8);
            return file;
        }

        /// <summary>导出：JSON + 每关 txt + 汇总 txt + C# 关卡表，并排队生成正式关卡场景。返回导出目录。</summary>
        public static string Export(ForgePack pack)
        {
            string baseName = SafeName(pack.packName);
            string folder = Path.Combine(Root, baseName);
            Directory.CreateDirectory(folder);

            File.WriteAllText(Path.Combine(Root, baseName + ".json"), JsonUtility.ToJson(pack, true), Utf8);

            var all = new StringBuilder();
            all.AppendLine($"# {pack.packName}");
            all.AppendLine($"# 主题 {ForgeLabels.Of(pack.spec.theme)} / 难度 {ForgeLabels.Of(pack.spec.difficulty)} / 提示 {ForgeLabels.Of(pack.spec.hintMode)} / 胜负 {ForgeLabels.Of(pack.spec.winRule)}");
            all.AppendLine();

            for (int i = 0; i < pack.levels.Count; i++)
            {
                var level = pack.levels[i];
                string body = LevelText(pack, level, i);
                all.AppendLine(body);
                all.AppendLine();
                File.WriteAllText(Path.Combine(folder, $"level_{i + 1:00}.txt"), body, Utf8);
            }

            File.WriteAllText(Path.Combine(folder, "all_levels.txt"), all.ToString(), Utf8);
            // 用 .cs.txt 后缀：放到 Assets 下不会被 Unity 当脚本编译（否则每次导出都会触发重编译并打断 Play）。
            File.WriteAllText(Path.Combine(folder, baseName + "_Levels.cs.txt"), CSharpSnippet(pack, baseName), Utf8);

            string request = "";
            try
            {
                request = RequestScenes(pack);
            }
            catch (Exception e)
            {
                Debug.LogWarning("PuzzleForge: 场景请求写入失败 —— " + e.Message);
            }

            File.WriteAllText(Path.Combine(folder, "README.txt"),
                "PuzzleForge 导出说明\r\n" +
                "1) all_levels.txt / level_XX.txt：ASCII 盘面 + 提示 + 最少步数答案，可直接对照关卡图例。\r\n" +
                "2) " + baseName + "_Levels.cs.txt：把内容另存为 .cs（或直接粘进 Campaign）即可开玩。留 .txt 后缀是为了不触发 Unity 重编译。\r\n" +
                "3) " + baseName + ".json：完整工程档，可在 PuzzleForge 场景的“读取工程”里回灌继续改。\r\n" +
                "4) Scenes/：每关一个正式关卡场景，Hierarchy 与 Level_01..Level_21 完全一致\r\n" +
                "   （GameRoot 上 WaterGame + LevelManager + LevelDefinition，GameCanvas 用 UI_GameHud 预制体，\r\n" +
                "   GameCanvas/BoardCenter 下挂 Room（LevelBoard），Room 里是 Tile_Wall / Tile_Floor /\r\n" +
                "   Effect_Water / Effect_Foam / Prop_Exit / Player_Fish 的真实预制体实例），\r\n" +
                "   打开直接按 Play 就能玩，操作、HUD、提示、旋转、过关演出都走正式关卡那套。\r\n" +
                "   Play 模式下建不了场景，所以点完按钮后要先退出播放，场景才会出现在 Scenes/ 里。\r\n" +
                "   也可以随时用菜单 Tools/PuzzleForge/用最近工程生成正式关卡场景 重新生成。\r\n" +
                "   注意：LevelManager.levelIndex 是包内序号，“下一关”仍按 Campaign 的 Level_XX 走，\r\n" +
                "   要塞进主线需要把场景改名为 Level_XX 并扩 Campaign/Campaign.Levels。\r\n" +
                (string.IsNullOrEmpty(request) ? "" : "5) 已排队场景生成，退出播放后自动开跑。\r\n"), Utf8);

            return folder;
        }

        /// <summary>只排队生成正式关卡场景，返回场景目录。</summary>
        public static string ExportScene(ForgePack pack)
        {
            RequestScenes(pack);
            return Path.Combine(Root, SafeName(pack.packName), "Scenes");
        }

        static string LevelText(ForgePack pack, ForgeLevel level, int index)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {level.name} (第 {index + 1} 关) ===");
            sb.AppendLine($"主题：{ForgeLabels.Of(pack.spec.theme)}  难度：{ForgeLabels.Of(pack.spec.difficulty)}  胜负：{ForgeLabels.Of(level.winRule)}" +
                          (level.moveLimit > 0 ? $"（限 {level.moveLimit} 步）" : ""));
            sb.AppendLine($"尺寸：{level.Width}×{level.Height}  最少步数：{level.steps}  可解：{(level.solvable ? "是" : "否")}");
            if (!string.IsNullOrEmpty(level.hint)) sb.AppendLine("提示：" + level.hint.Replace("\n", " / "));
            sb.AppendLine("答案：" + ForgeGenerator.RouteText(level.solution));
            if (!string.IsNullOrEmpty(level.answerNote)) sb.AppendLine("答案备注：" + level.answerNote.Replace("\n", " / "));
            if (!string.IsNullOrEmpty(level.issue)) sb.AppendLine("问题：" + level.issue);
            sb.AppendLine();
            foreach (var row in level.rows) sb.AppendLine(row);
            return sb.ToString();
        }

        static string CSharpSnippet(ForgePack pack, string baseName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// 由 PuzzleForge 导出。直接放进任意 asmdef 覆盖的脚本目录即可。");
            sb.AppendLine("// 用法：WaterTurn.Generated." + Identifier(baseName) + ".Levels");
            sb.AppendLine("using WaterTurn;");
            sb.AppendLine();
            sb.AppendLine("namespace WaterTurn.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static class " + Identifier(baseName));
            sb.AppendLine("    {");
            sb.AppendLine("        public static readonly Level[] Levels =");
            sb.AppendLine("        {");
            for (int i = 0; i < pack.levels.Count; i++)
            {
                var level = pack.levels[i];
                sb.AppendLine($"            // {level.name} · 最少 {level.steps} 步 · {ForgeGenerator.RouteText(level.solution)}");
                sb.Append("            new Level(").Append(Quote(level.name)).Append(", ").Append(Quote(level.hint)).AppendLine(",");
                for (int y = 0; y < level.rows.Length; y++)
                {
                    sb.Append("                ").Append(Quote(level.rows[y]));
                    sb.AppendLine(y < level.rows.Length - 1 ? "," : "),");
                }
                sb.AppendLine();
            }
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static string Quote(string value) => "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        static string Identifier(string value)
        {
            var sb = new StringBuilder();
            foreach (var c in value)
                sb.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '_');
            if (sb.Length == 0) return "ForgePack";
            if (char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }

        static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "ForgePack";
            var sb = new StringBuilder();
            foreach (var c in value.Trim())
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            return sb.ToString();
        }
    }
}

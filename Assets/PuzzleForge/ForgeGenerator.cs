using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WaterTurn.Forge
{
    /// <summary>
    /// 谜题生成器：先随机搭一个"水在下、出口在上"的房间，再交给
    /// <see cref="Puzzle.Solve"/> 做真实求解校验，只有"确实能赢、步数落在难度区间"的
    /// 盘面才会被采纳。因此工坊产出的每一关都能直接丢进 <see cref="Level"/> 里玩。
    /// </summary>
    public static class ForgeGenerator
    {
        const string ProbeName = "forge-probe";

        public static int SizeOf(ForgeDifficulty difficulty) =>
            difficulty == ForgeDifficulty.Easy ? 7 :
            difficulty == ForgeDifficulty.Normal ? 9 :
            difficulty == ForgeDifficulty.Hard ? 11 : 13;

        public static int MinStepsOf(ForgeDifficulty difficulty) =>
            difficulty == ForgeDifficulty.Easy ? 2 :
            difficulty == ForgeDifficulty.Normal ? 4 :
            difficulty == ForgeDifficulty.Hard ? 6 : 9;

        static float WallDensityOf(ForgeDifficulty difficulty)
        {
            switch (difficulty)
            {
                case ForgeDifficulty.Easy: return 0.05f;
                case ForgeDifficulty.Normal: return 0.09f;
                case ForgeDifficulty.Hard: return 0.12f;
                default: return 0.15f;
            }
        }

        static int MechanismCountOf(ForgeSpec spec)
        {
            int cap = (int)spec.difficulty;      // 简单 0，普通 1，困难 2，专家 3
            int available = 0;
            var r = spec.rules;
            if (r.crates) available++;
            if (r.portals) available++;
            if (r.valves) available++;
            if (r.sensorGates) available++;
            if (r.plates) available++;
            if (r.membranes) available++;
            if (r.springs) available++;
            return Mathf.Min(cap, available);
        }

        // ------------------------------------------------------------------ 生成

        public static ForgePack Generate(ForgeSpec spec)
        {
            var pack = new ForgePack
            {
                packName = $"Forge_{spec.titlePrefix}_{spec.difficulty}_{spec.seed}",
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                spec = spec.Clone(),
                levels = new List<ForgeLevel>()
            };
            int count = Mathf.Clamp(spec.levelCount, 1, 8);
            for (int i = 0; i < count; i++)
                pack.levels.Add(Build(spec, i));
            return pack;
        }

        static ForgeLevel Build(ForgeSpec spec, int index)
        {
            int size = SizeOf(spec.difficulty);
            int attempts = spec.difficulty == ForgeDifficulty.Expert ? 240 : 160;
            int minSteps = MinStepsOf(spec.difficulty);
            int moveCap = spec.winRule == ForgeWinRule.MoveLimit ? Mathf.Max(4, spec.moveLimit) : 999;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int seed = unchecked(spec.seed * 7919 + index * 104729 + attempt * 31 + 17);
                var rng = new System.Random(seed);
                var grid = MakeRoom(spec, size, rng, attempt);
                if (grid == null) continue;

                var level = Parse(grid);
                if (level == null) continue;

                string route;
                try { route = Puzzle.Solve(level); }
                catch { continue; }
                if (string.IsNullOrEmpty(route)) continue;

                // 越往后越放宽步数要求，避免"太难导致一关都生成不出来"。
                int floorSteps = Mathf.Max(1, minSteps - attempt / 50);
                if (route.Length < floorSteps) continue;
                if (route.Length > 34) continue;
                if (route.Length > moveCap) continue;

                return Finish(spec, index, seed, grid, route, level);
            }

            return Fallback(spec, index);
        }

        static char[,] MakeRoom(ForgeSpec spec, int size, System.Random rng, int attempt)
        {
            var grid = new char[size, size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    grid[y, x] = x == 0 || y == 0 || x == size - 1 || y == size - 1 ? '#' : '.';

            // 内部散点墙：后段尝试降低密度，给"水能连通"留出空间。
            float density = WallDensityOf(spec.difficulty) * (attempt < 40 ? 1f : attempt < 90 ? 0.7f : 0.45f);
            int walls = Mathf.RoundToInt((size - 2) * (size - 2) * density);
            for (int i = 0; i < walls; i++)
            {
                int x = rng.Next(1, size - 1), y = rng.Next(1, size - 1);
                grid[y, x] = '#';
            }

            AddMechanisms(grid, spec, rng);
            if (spec.winRule == ForgeWinRule.PearlThenExit)
                PlacePearl(grid, rng);

            int free = CountFree(grid);
            if (free < 8) return null;
            float fraction = spec.difficulty == ForgeDifficulty.Easy ? 0.52f : 0.46f;
            FillWater(grid, Mathf.Max(6, Mathf.RoundToInt(free * fraction)), rng);

            if (!PlacePlayer(grid, rng)) return null;
            if (!PlaceExit(grid, rng)) return null;
            return grid;
        }

        static void AddMechanisms(char[,] grid, ForgeSpec spec, System.Random rng)
        {
            int want = MechanismCountOf(spec);
            var pool = new List<string>();
            if (spec.rules.crates) pool.Add("crate");
            if (spec.rules.portals) pool.Add("portal");
            if (spec.rules.valves) pool.Add("valve");
            if (spec.rules.sensorGates) pool.Add("sensor");
            if (spec.rules.plates) pool.Add("plate");
            if (spec.rules.membranes) pool.Add("membrane");
            if (spec.rules.springs) pool.Add("spring");
            if (pool.Count == 0) return;

            // 先洗一遍再按顺序取：同一关不会重复堆同一种机制，相邻关卡也更容易换花样。
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                string tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            for (int i = 0; i < want && i < pool.Count; i++)
            {
                string kind = pool[i];
                bool placed;
                if (kind == "crate") placed = PlaceCrate(grid, rng);
                else if (kind == "portal") placed = PlacePortal(grid, rng);
                else if (kind == "valve") placed = PlaceValve(grid, rng);
                else if (kind == "sensor") placed = PlaceSensorGate(grid, rng, 'S');
                else if (kind == "plate") placed = PlaceSensorGate(grid, rng, 'X');
                else if (kind == "membrane") placed = PlaceMembrane(grid, rng);
                else placed = PlaceSpring(grid, rng);
                if (!placed) continue;
            }
        }

        static bool PlaceCrate(char[,] grid, System.Random rng)
        {
            if (!PickFree(grid, rng, out int x, out int y)) return false;
            grid[y, x] = 'B';
            return true;
        }

        static bool PlacePortal(char[,] grid, System.Random rng)
        {
            if (!PickFree(grid, rng, out int ax, out int ay)) return false;
            if (!PickFree(grid, rng, out int bx, out int by)) return false;
            if (ax == bx && ay == by) return false;
            grid[ay, ax] = '1';
            grid[by, bx] = '2';
            return true;
        }

        static bool PlaceValve(char[,] grid, System.Random rng)
        {
            if (!PickFree(grid, rng, out int x, out int y)) return false;
            char[] valves = {'^', '>', 'v', '<'};
            grid[y, x] = valves[rng.Next(valves.Length)];
            return true;
        }

        static bool PlaceSensorGate(char[,] grid, System.Random rng, char sensor)
        {
            if (!PickFree(grid, rng, out int sx, out int sy)) return false;
            if (!PickFree(grid, rng, out int gx, out int gy)) return false;
            if (sx == gx && sy == gy) return false;
            grid[sy, sx] = sensor;
            grid[gy, gx] = 'D';
            return true;
        }

        static bool PlaceMembrane(char[,] grid, System.Random rng)
        {
            int h = grid.GetLength(0), w = grid.GetLength(1);
            if (!PickFree(grid, rng, out int x, out int y)) return false;
            int length = 0;
            for (int i = 0; i < 3 && x + i < w - 1 && grid[y, x + i] == '.'; i++)
            {
                grid[y, x + i] = 'M';
                length++;
            }
            return length > 0;
        }

        static bool PlaceSpring(char[,] grid, System.Random rng)
        {
            if (!PickFree(grid, rng, out int x, out int y)) return false;
            grid[y, x] = 'o';
            return true;
        }

        static void PlacePearl(char[,] grid, System.Random rng)
        {
            if (PickFree(grid, rng, out int x, out int y))
                grid[y, x] = 'K';
        }

        static bool PickFree(char[,] grid, System.Random rng, out int x, out int y)
        {
            int h = grid.GetLength(0), w = grid.GetLength(1);
            for (int i = 0; i < 60; i++)
            {
                int px = rng.Next(1, w - 1), py = rng.Next(1, h - 1);
                if (grid[py, px] == '.')
                {
                    x = px;
                    y = py;
                    return true;
                }
            }

            x = y = -1;
            return false;
        }

        static int CountFree(char[,] grid)
        {
            int count = 0;
            for (int y = 1; y < grid.GetLength(0) - 1; y++)
                for (int x = 1; x < grid.GetLength(1) - 1; x++)
                    if (grid[y, x] == '.') count++;
            return count;
        }

        /// <summary>从最底行往上灌水，保证开局是一潭"躺在底部"的水。</summary>
        static void FillWater(char[,] grid, int target, System.Random rng)
        {
            int h = grid.GetLength(0), w = grid.GetLength(1), placed = 0;
            for (int y = h - 2; y >= 1 && placed < target; y--)
            {
                var cells = new List<int>();
                for (int x = 1; x < w - 1; x++)
                    if (grid[y, x] == '.') cells.Add(x);
                for (int i = 0; i < cells.Count && placed < target; i++)
                {
                    grid[y, cells[i]] = '~';
                    placed++;
                }
            }
        }

        static bool PlacePlayer(char[,] grid, System.Random rng)
        {
            int h = grid.GetLength(0), w = grid.GetLength(1);
            var candidates = new List<int>();
            int lowest = -1;
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                    if (grid[y, x] == '~')
                    {
                        if (y > lowest)
                        {
                            lowest = y;
                            candidates.Clear();
                        }
                        if (y == lowest) candidates.Add(x * 1000 + y);
                    }
            if (candidates.Count == 0) return false;
            int pick = candidates[rng.Next(candidates.Count)];
            grid[pick % 1000, pick / 1000] = 'P';
            return true;
        }

        /// <summary>出口故意放在偏上的干格：必须先把水转过来淹到它，才有"解题"可言。</summary>
        static bool PlaceExit(char[,] grid, System.Random rng)
        {
            int h = grid.GetLength(0), w = grid.GetLength(1);
            var upper = new List<int>();
            var any = new List<int>();
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    if (grid[y, x] != '.') continue;
                    any.Add(x * 1000 + y);
                    if (y < (h - 1) * 2 / 3) upper.Add(x * 1000 + y);
                }

            var pool = upper.Count > 0 ? upper : any;
            if (pool.Count == 0) return false;
            int cell = pool[rng.Next(pool.Count)];
            grid[cell % 1000, cell / 1000] = 'E';
            return true;
        }

        // ------------------------------------------------------------- 解析/收尾

        static string[] RowsOf(char[,] grid)
        {
            int h = grid.GetLength(0), w = grid.GetLength(1);
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var sb = new StringBuilder(w);
                for (int x = 0; x < w; x++) sb.Append(grid[y, x]);
                rows[y] = sb.ToString();
            }
            return rows;
        }

        static Level Parse(char[,] grid)
        {
            var rows = RowsOf(grid);
            try
            {
                var level = new Level(ProbeName, "", rows);
                var start = Puzzle.Begin(level);
                return start == null ? null : level;
            }
            catch
            {
                return null;
            }
        }

        static ForgeLevel Finish(ForgeSpec spec, int index, int seed, char[,] grid, string route, Level level)
        {
            var rows = RowsOf(grid);
            var data = new ForgeLevel
            {
                name = $"{spec.titlePrefix} {index + 1:00}",
                rows = rows,
                solution = route,
                steps = route.Length,
                solvable = true,
                theme = spec.theme,
                difficulty = spec.difficulty,
                winRule = spec.winRule,
                moveLimit = spec.winRule == ForgeWinRule.MoveLimit ? spec.moveLimit : 0,
                seed = seed,
                issue = ""
            };
            data.hint = BuildHint(spec, level, route);
            return data;
        }

        /// <summary>生成失败时的兜底：退化成"空房间 + 半池水"，这种盘面几乎必然可解。</summary>
        static ForgeLevel Fallback(ForgeSpec spec, int index)
        {
            int size = SizeOf(spec.difficulty);
            for (int attempt = 0; attempt < 90; attempt++)
            {
                int seed = unchecked(spec.seed * 3571 + index * 40503 + attempt * 977);
                var rng = new System.Random(seed);
                var grid = new char[size, size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        grid[y, x] = x == 0 || y == 0 || x == size - 1 || y == size - 1 ? '#' : '.';

                FillWater(grid, Mathf.RoundToInt((size - 2) * (size - 2) * 0.55f), rng);
                if (!PlacePlayer(grid, rng)) continue;
                if (!PlaceExit(grid, rng)) continue;
                var level = Parse(grid);
                if (level == null) continue;
                string route;
                try { route = Puzzle.Solve(level); }
                catch { continue; }
                if (string.IsNullOrEmpty(route)) continue;
                return Finish(spec, index, seed, grid, route, level);
            }

            // 理论上不会走到这里；留一份手写盘面并如实标注"未通过求解校验"。
            var rows = new[]
            {
                "#######",
                "#.....#",
                "#..E..#",
                "#~~~~~#",
                "#P~~~~#",
                "#~~~~~#",
                "#######"
            };
            return new ForgeLevel
            {
                name = $"{spec.titlePrefix} {index + 1:00}",
                rows = rows,
                hint = "",
                solution = "",
                steps = 0,
                solvable = false,
                issue = "生成器未能凑出可解盘面，请调整规则或难度后重试",
                theme = spec.theme,
                difficulty = spec.difficulty,
                winRule = spec.winRule,
                moveLimit = 0,
                seed = spec.seed
            };
        }

        // ------------------------------------------------------------------ 校验

        public static ForgeValidation Validate(string[] rows, ForgeWinRule winRule, int moveLimit)
        {
            var result = new ForgeValidation();
            if (rows == null || rows.Length == 0)
            {
                result.message = "盘面为空";
                return result;
            }

            int width = rows[0] == null ? 0 : rows[0].Length;
            if (width < 3)
            {
                result.message = "盘面宽度至少 3 格";
                return result;
            }

            result.width = width;
            result.height = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null || rows[i].Length != width)
                {
                    result.message = $"第 {i + 1} 行宽度是 {rows[i]?.Length ?? 0}，与首行 {width} 不一致";
                    return result;
                }
            }

            var flat = string.Concat(rows);
            if (flat.IndexOf('P') < 0)
            {
                result.message = "缺少玩家起点 P";
                return result;
            }
            if (flat.IndexOf('E') < 0)
            {
                result.message = "缺少出口 E";
                return result;
            }

            Level level;
            try
            {
                level = new Level("edited", "", rows);
            }
            catch (Exception e)
            {
                result.message = "关卡数据不合法：" + e.Message;
                return result;
            }

            try
            {
                if (Puzzle.Begin(level) == null)
                {
                    result.message = "开局即死：玩家落在栅栏或关闭的闸门上";
                    return result;
                }

                var route = Puzzle.Solve(level);
                if (string.IsNullOrEmpty(route))
                {
                    result.playable = true;
                    result.message = "可运行但无解：水永远到不了出口";
                    return result;
                }

                result.playable = true;
                result.solution = route;
                result.steps = route.Length;
                if (winRule == ForgeWinRule.MoveLimit && moveLimit > 0 && route.Length > moveLimit)
                {
                    result.ok = false;
                    result.message = $"可解但超限：最少 {route.Length} 步 > 限步 {moveLimit}";
                    return result;
                }

                result.ok = true;
                result.message = $"可解 · 最少 {route.Length} 步";
                return result;
            }
            catch (Exception e)
            {
                result.message = "求解异常：" + e.Message;
                return result;
            }
        }

        /// <summary>清空/新建时给一个合法的最小盘面，方便直接手动改。</summary>
        public static string[] Blank(int size = 7)
        {
            size = Mathf.Clamp(size, 5, 15);
            var rows = new string[size];
            for (int y = 0; y < size; y++)
            {
                var sb = new StringBuilder(size);
                for (int x = 0; x < size; x++)
                {
                    bool border = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    if (border) sb.Append('#');
                    else if (y == size - 2 && x < size - 1) sb.Append('~');
                    else if (y == size - 2 && x == size - 1) sb.Append('E');
                    else if (y == size - 2 && x == 1) sb.Append('P');
                    else sb.Append('.');
                }
                rows[y] = sb.ToString();
            }

            // 出口那格改成干格，起点单独用 P 占一格水。
            var last = rows[size - 2].ToCharArray();
            if (last[size - 2] == '~') last[size - 2] = 'E';
            last[1] = 'P';
            rows[size - 2] = new string(last);
            return rows;
        }

        // ------------------------------------------------------------- 提示与答案

        public static string Token(char move)
        {
            switch (move)
            {
                case '0': return "上";
                case '1': return "右";
                case '2': return "下";
                case '3': return "左";
                case '4': return "顺时针";
                case '5': return "逆时针";
                default: return "?";
            }
        }

        public static List<KeyValuePair<string, int>> RouteParts(string route)
        {
            var parts = new List<KeyValuePair<string, int>>();
            if (string.IsNullOrEmpty(route)) return parts;
            int i = 0;
            while (i < route.Length)
            {
                char c = route[i];
                int n = 1;
                while (i + n < route.Length && route[i + n] == c) n++;
                parts.Add(new KeyValuePair<string, int>(Token(c), n));
                i += n;
            }
            return parts;
        }

        public static string RouteText(string route)
        {
            if (string.IsNullOrEmpty(route)) return "无解";
            var sb = new StringBuilder();
            foreach (var part in RouteParts(route))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(part.Value > 1 ? $"{part.Key}×{part.Value}" : part.Key);
            }
            return sb.ToString();
        }

        static string OneLineIdea(Level level, string route)
        {
            int turns = 0, moves = 0;
            foreach (var c in route)
            {
                if (c == '4' || c == '5') turns++;
                else moves++;
            }

            if (level.HasPortal) return "门是双向的：先让水流进门，从另一头冒出来再往下落。";
            if (level.HasValve) return "单向阀只认一个方向，转房间前先看清箭头，别把水路顶死。";
            if (level.HasInvertedGate) return "逆闸是常开的，压住感应格反而会关门——先把鱼送过去再放水。";
            if (level.HasMembrane) return "鱼能穿膜、水不能：用薄膜把鱼送到干区，再想办法把水引过去。";
            if (level.HasSpring) return "泉眼永远有水，是不动的落脚点，拿它当中转站。";
            if (level.Plates.Length > 0) return "箱子是用来压重压板的，先推箱开门，再走鱼。";
            if (moves == 0) return turns > 0
                ? $"转 {turns} 次就够了：出口正好在那一侧的水位上。"
                : "开局就在水面上，顺着水走就行。";
            return turns > 0
                ? $"先转 {turns} 次把水铺到出口那侧，再走 {moves} 步过去。"
                : $"水已经铺好了，顺着水面走 {moves} 步。";
        }

        static string StepHint(string route)
        {
            var parts = RouteParts(route);
            if (parts.Count == 0) return "";
            var marks = new[] {"①", "②", "③", "④"};
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count && i < marks.Length; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append(marks[i]).Append(' ').Append(parts[i].Key);
                if (parts[i].Value > 1) sb.Append('×').Append(parts[i].Value);
            }
            if (parts.Count > marks.Length) sb.Append(" …");
            return sb.ToString();
        }

        /// <summary>手改之后想重新贴一句提示时用这个入口。</summary>
        public static string ComposeHint(ForgeSpec spec, Level level, string route) => BuildHint(spec, level, route);

        static string BuildHint(ForgeSpec spec, Level level, string route)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(spec.authorHint)) sb.Append(spec.authorHint.Trim());
            if (spec.hintMode == ForgeHintMode.None) return sb.ToString();
            if (sb.Length > 0) sb.Append(spec.hintMode == ForgeHintMode.Steps ? "\n" : " ");

            sb.Append(spec.hintMode == ForgeHintMode.OneLine ? OneLineIdea(level, route) : StepHint(route));
            return sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace WaterTurn
{
    public enum Move { Up, Right, Down, Left, Turn, TurnLeft }
    public enum Block { None, Dry, Crate, Grate, Arrow }

    public sealed class Level
    {
        public readonly string Name, Hint;
        public readonly int Width, Height, Exit, Sensor, Pearl;
        public readonly string Tiles;
        public readonly Position Start;

        /// <summary>
        /// 传送门配对表：每格记录它的姊妹门下标，-1 表示这一格不是门。
        /// 水（和骑在上面的鱼）进入门格，会从姊妹门下游的第一格冒出来继续落；
        /// 水永远不会停在门格里。
        /// </summary>
        public readonly int[] Portal;

        /// <summary>重压格：只有箱子能压住它，压住以后闸门永久打开。</summary>
        public readonly int[] Plates;

        /// <summary>
        /// 时效感应格 's'：只要有一格水停在它上面，闸门就开着；水一离开，当回合就关。
        /// 和固定感应格 'S'（压住即永久开）是同一套判定的两种寿命。
        /// </summary>
        public readonly int Latch;

        public bool HasPortal => Portal.Any(partner => partner >= 0);
        public bool HasLatch => Latch >= 0;

        static readonly char[] ValveChars = {'^', '>', 'v', '<'};
        public bool HasValve => Tiles.IndexOfAny(ValveChars) >= 0;

        public Level(string name, string hint, params string[] rows)
        {
            Name = name;
            Hint = hint;
            Width = rows[0].Length;
            Height = rows.Length;
            if (rows.Any(row => row.Length != Width)) throw new ArgumentException(name);
            Tiles = string.Concat(rows);
            Exit = Tiles.IndexOf('E');
            Sensor = Tiles.IndexOf('S');
            Latch = Tiles.IndexOf('s');
            Pearl = Tiles.IndexOf('K');

            Portal = Enumerable.Repeat(-1, Tiles.Length).ToArray();
            var mouths = new List<int>();
            for (int p = 0; p < Tiles.Length; p++)
                if (Tiles[p] == '1' || Tiles[p] == '2') mouths.Add(p);
            if (mouths.Count == 2)
            {
                Portal[mouths[0]] = mouths[1];
                Portal[mouths[1]] = mouths[0];
            }
            else if (mouths.Count != 0)
                throw new ArgumentException(name + ": 传送门必须成对（1 和 2）");

            Plates = Enumerable.Range(0, Tiles.Length).Where(p => Tiles[p] == 'X').ToArray();

            var water = new List<int>();
            var boxes = new List<int>();
            int rider = -1;
            for (int p = 0; p < Tiles.Length; p++)
            {
                char tile = Tiles[p];
                if (tile == 'P') rider = water.Count;
                if (tile == 'P' || tile == '~' || tile == 'B' || tile == 'b') water.Add(p);
                if (tile == 'B' || tile == 'b') boxes.Add(p);
            }
            if (rider < 0 || Exit < 0) throw new ArgumentException(name);
            boxes.Sort();
            Start = new Position(water.ToArray(), rider, boxes.ToArray(), 0, false, false);
        }
    }

    public sealed class Position
    {
        public readonly int[] Water;
        public readonly int Rider;
        /// <summary>箱子的格子下标，升序。箱子固定在地图上，不随房间旋转下落。</summary>
        public readonly int[] Boxes;
        public readonly int Turn;
        public readonly bool Gate, Pearl;

        public int Fish => Water[Rider];

        public Position(int[] water, int rider, int[] boxes, int turn, bool gate, bool pearl)
        {
            Water = water;
            Rider = rider;
            Boxes = boxes;
            Turn = turn;
            Gate = gate;
            Pearl = pearl;
        }

        public bool HasBox(int cell) => Array.IndexOf(Boxes, cell) >= 0;
    }

    public static class Puzzle
    {
        static readonly int[] X = {0, 1, 0, -1};
        static readonly int[] Y = {-1, 0, 1, 0};

        public static Position Begin(Level level) => Fall(level, level.Start, out _);

        public static bool Won(Level level, Position p) =>
            p.Fish == level.Exit && (level.Pearl < 0 || p.Pearl);

        /// <summary>
        /// A gate is deadly to the fish (the water it lets through is not). Used
        /// both by the runtime death check and by the hint solver, so a hint can
        /// never route the fish into a gate.
        /// </summary>
        public static bool OnGate(Level level, Position p) =>
            p != null && level.Tiles[p.Fish] == 'D';

        public static Position Apply(Level level, Position p, Move move, out Block block)
        {
            block = Block.None;
            if (move == Move.Turn || move == Move.TurnLeft)
            {
                int turn = (p.Turn + (move == Move.Turn ? 1 : 3)) % 4;
                return Fall(level, new Position(p.Water, p.Rider, p.Boxes, turn, p.Gate, p.Pearl), out block);
            }

            int direction = ((int)move - p.Turn + 4) % 4;
            int step = Step(level, p.Fish, direction);
            if (step < 0) { block = Block.Dry; return null; }
            // 阀格对鱼同样生效。鱼可以顺着箭头游进去，侧着、逆着都会撞在阀门前；
            // 被水带着穿过阀格属于水的通行，不受这一条限制。
            if (ArrowBlocks(level, step, direction)) { block = Block.Arrow; return null; }
            // 鱼只能待在水的格子里；箱子压过之后那一格可能已经干了。
            int rider = Array.IndexOf(p.Water, step);
            if (rider < 0) { block = Block.Dry; return null; }
            if (level.Tiles[step] == 'G') { block = Block.Grate; return null; }

            bool hasPearl = p.Pearl || step == level.Pearl;
            if (!p.HasBox(step))
                return new Position(p.Water, rider, p.Boxes, p.Turn, p.Gate, hasPearl);

            // 推箱：鱼站的那格必须是水，箱子沿同方向再走一格。
            int dest = Step(level, step, direction);
            if (!CanPushInto(level, dest, p))
            {
                block = Block.Crate;
                return null;
            }
            var boxes = (int[])p.Boxes.Clone();
            boxes[Array.IndexOf(boxes, step)] = dest;
            Array.Sort(boxes);
            return Fall(level, new Position(p.Water, rider, boxes, p.Turn, p.Gate, hasPearl), out block);
        }

        static bool CanPushInto(Level level, int cell, Position p)
        {
            if (cell < 0) return false;
            char tile = level.Tiles[cell];
            if (tile == '#' || tile == 'G' || tile == 'E') return false;
            if (tile == 'D' && !p.Gate) return false;
            if (p.HasBox(cell)) return false;
            // 门格可以推上去：那就是把箱子当塞子用，门会失效。
            return true;
        }

        static int Step(Level level, int pos, int direction)
        {
            int x = pos % level.Width + X[direction], y = pos / level.Width + Y[direction];
            return x < 0 || x >= level.Width || y < 0 || y >= level.Height ? -1 : x + y * level.Width;
        }

        /// <summary>
        /// 重压格只认箱子；全部压住时闸门打开（一旦打开就永久开着，和感应格一致）。
        /// </summary>
        static bool PlatesPressed(Level level, int[] boxes)
        {
            if (level.Plates.Length == 0) return false;
            foreach (int plate in level.Plates)
                if (Array.IndexOf(boxes, plate) < 0) return false;
            return true;
        }

        /// <summary>
        /// The same move as <see cref="Apply"/>, but grates do not block: the fish is
        /// dragged onto (or through) the bars and <paramref name="touchedGrate"/> says
        /// so. The runtime uses this only to let the fatal move finish on the board
        /// before the death sequence starts; the puzzle solver never calls it, so
        /// hints still treat grates as walls.
        /// </summary>
        public static Position ApplyLethal(Level level, Position p, Move move, out bool touchedGrate)
        {
            touchedGrate = false;
            if (move == Move.Turn || move == Move.TurnLeft)
            {
                int turn = (p.Turn + (move == Move.Turn ? 1 : 3)) % 4;
                return FallLethal(level,
                    new Position(p.Water, p.Rider, p.Boxes, turn, p.Gate, p.Pearl), ref touchedGrate);
            }

            int direction = ((int)move - p.Turn + 4) % 4;
            int step = Step(level, p.Fish, direction);
            if (step < 0) return null;
            if (ArrowBlocks(level, step, direction)) return null;

            int rider = Array.IndexOf(p.Water, step);
            if (rider < 0)
            {
                // The fish reaches into the bars: keep the snapshot consistent by
                // letting its own water cell become the grate cell.
                if (level.Tiles[step] != 'G') return null;
                rider = Array.IndexOf(p.Water, p.Fish);
                if (rider < 0) return null;
                var held = (int[])p.Water.Clone();
                held[rider] = step;
                touchedGrate = true;
                return new Position(held, rider, p.Boxes, p.Turn, p.Gate, p.Pearl || step == level.Pearl);
            }

            if (level.Tiles[step] == 'G') touchedGrate = true;
            bool hasPearl = p.Pearl || step == level.Pearl;
            if (!p.HasBox(step))
                return new Position(p.Water, rider, p.Boxes, p.Turn, p.Gate, hasPearl);

            int dest = Step(level, step, direction);
            if (!CanPushInto(level, dest, p)) return null;
            var boxes = (int[])p.Boxes.Clone();
            boxes[Array.IndexOf(boxes, step)] = dest;
            Array.Sort(boxes);
            return FallLethal(level,
                new Position(p.Water, rider, boxes, p.Turn, p.Gate, hasPearl), ref touchedGrate);
        }

        static Position FallLethal(Level level, Position p, ref bool touchedGrate)
        {
            int direction = (6 - p.Turn) % 4;
            int[] water = (int[])p.Water.Clone();
            bool gate = p.Gate || PlatesPressed(level, p.Boxes);
            int width = level.Width;
            int Projection(int cell) => X[direction] * (cell % width) + Y[direction] * (cell / width);
            int[] order = Enumerable.Range(0, water.Length).OrderByDescending(i => Projection(water[i])).ToArray();

            for (int pass = 0; pass < 2; pass++)
            {
                var wet = new HashSet<int>(water);
                foreach (int i in order)
                {
                    wet.Remove(water[i]);
                    int next = Step(level, water[i], direction);
                    while (next >= 0 && Free(level, next, gate) && !p.HasBox(next) && !wet.Contains(next))
                    {
                        if (i == p.Rider && level.Tiles[next] == 'G')
                        {
                            // The fish catches on the bars instead of slipping through
                            // like the water does; that cell is where it dies.
                            touchedGrate = true;
                            water[i] = next;
                            break;
                        }
                        water[i] = next;
                        next = Step(level, next, direction);
                    }
                    wet.Add(water[i]);
                }
                if (gate || !wet.Contains(level.Sensor)) break;
                gate = true;
            }
            return new Position(water, p.Rider, p.Boxes, p.Turn, gate, p.Pearl || water[p.Rider] == level.Pearl);
        }

        static bool Free(Level level, int cell, bool gate) =>
            cell >= 0 && level.Tiles[cell] != '#' && (gate || level.Tiles[cell] != 'D');

        static int ArrowOf(char tile) =>
            tile == '^' ? 0 : tile == '>' ? 1 : tile == 'v' ? 2 : tile == '<' ? 3 : -1;

        /// <summary>
        /// 单向阀：水要进入这一格，方向必须和箭头一致，否则等于撞墙、停在阀门前。
        /// 箭头和水的流动方向都写在「房间坐标」里（箭头钉在房间上，房间转一次它跟着
        /// 转 90°，水也跟着转），所以这里直接比，不能再折算回合——一折算就等于把
        /// 箭头当成「屏幕坐标」的固定方向，画面上那条箭头和判定就对不上了。
        /// 水在每个朝向下只有一个流动方向，于是同一扇阀只在「箭头正好朝下」的那一档
        /// 通水：`v` 是 0 转、`>` 是 1 转、`^` 是 2 转、`<` 是 3 转；其余三档它就是一
        /// 道墙，水停在阀的入口侧——这就是第 07 关的题眼。
        /// </summary>
        static bool ArrowBlocks(Level level, int cell, int direction)
        {
            int arrow = ArrowOf(level.Tiles[cell]);
            return arrow >= 0 && arrow != direction;
        }

        static Position Fall(Level level, Position p, out Block block)
        {
            block = Block.None;
            int direction = (6 - p.Turn) % 4;
            int width = level.Width;
            // 传送门可能把水送回它刚走过的位置，给每次落定一个硬上限。
            int guard = level.Width * level.Height * 4;
            bool gate = p.Gate || PlatesPressed(level, p.Boxes);
            int[] water = (int[])p.Water.Clone();
            bool pearl = p.Pearl;

            // 闸门可能在同一次落定里开或关，最多重落三趟：普通感应格只会「开」，
            // 时效格还会「关」，两者都靠这一个循环收敛。
            for (int pass = 0; pass < 3; pass++)
            {
                block = Block.None;
                water = (int[])p.Water.Clone();
                int[] order = Enumerable.Range(0, water.Length)
                    .OrderByDescending(i => X[direction] * (water[i] % width) + Y[direction] * (water[i] / width))
                    .ToArray();
                var wet = new HashSet<int>(water);
                foreach (int i in order)
                {
                    wet.Remove(water[i]);
                    int cell = water[i];
                    int hops = 0;
                    while (hops++ < guard)
                    {
                        int next = Step(level, cell, direction);
                        if (next < 0) break;
                        if (ArrowBlocks(level, next, direction)) break;

                        int far = level.Portal[next];
                        if (far >= 0)
                        {
                            // 门被箱子塞住（这一头或那一头）就不再送水。
                            if (p.HasBox(next) || p.HasBox(far)) break;
                            int outlet = Step(level, far, direction);
                            if (!Free(level, outlet, gate) || wet.Contains(outlet) || p.HasBox(outlet)) break;
                            cell = outlet;
                            continue;
                        }

                        if (!Free(level, next, gate) || wet.Contains(next) || p.HasBox(next)) break;
                        if (i == p.Rider && level.Tiles[next] == 'G')
                        {
                            block = Block.Grate;
                            return null;
                        }
                        cell = next;
                    }
                    water[i] = cell;
                    wet.Add(cell);
                }

                pearl = p.Pearl || water[p.Rider] == level.Pearl;
                if (level.Latch >= 0)
                {
                    bool pressed = wet.Contains(level.Latch);
                    if (pressed && !gate)
                    {
                        // 落定途中才被压住：门开了，水还得再落一趟才穿得过去。
                        gate = true;
                        continue;
                    }
                    // 门跟着水位开关，但这一趟已经流过去的水收不回来——
                    // 这就是「时效」的全部含义：窗口只在那一次落定里有效。
                    gate = pressed;
                    break;
                }
                if (gate || !wet.Contains(level.Sensor)) break;
                gate = true;
            }

            // 阀格是通道，不是水库。落定后若某格水停在阀里、而当前朝向并不允许水沿这个
            // 方向流过，说明它是转向之前进去的：把水弹回阀的入口侧，保证「只有顺着箭头
            // 才进得去」在每一个可见状态下都成立。
            for (int i = 0; i < water.Length; i++)
            {
                int cell = water[i];
                if (ArrowOf(level.Tiles[cell]) < 0) continue;
                if (!ArrowBlocks(level, cell, direction)) continue;
                // 优先退回入口侧；入口被别的水堵住时，从两侧挤出去。
                int[] slots =
                {
                    Step(level, cell, (direction + 2) % 4),
                    Step(level, cell, (direction + 1) % 4),
                    Step(level, cell, (direction + 3) % 4),
                };
                foreach (int slot in slots)
                {
                    if (slot < 0 || !Free(level, slot, gate)) continue;
                    if (ArrowOf(level.Tiles[slot]) >= 0) continue;   // 别连环跳进另一扇阀
                    if (Array.IndexOf(water, slot) >= 0 || p.HasBox(slot)) continue;
                    if (i == p.Rider && level.Tiles[slot] == 'G') continue;
                    water[i] = slot;
                    break;
                }
            }
            pearl = pearl || water[p.Rider] == level.Pearl;

            return new Position(water, p.Rider, p.Boxes, p.Turn, gate, pearl);
        }

        public static string Solve(Level level, Position start = null)
        {
            start = start ?? Begin(level);
            if (start == null || OnGate(level, start)) return null;
            var frontier = new Queue<(Position position, string route)>();
            var seen = new HashSet<string>();
            frontier.Enqueue((start, ""));
            seen.Add(Key(start));
            while (frontier.Count > 0)
            {
                var (pos, route) = frontier.Dequeue();
                if (Won(level, pos)) return route;
                for (int i = 0; i <= (int)Move.TurnLeft; i++)
                {
                    Position next = Apply(level, pos, (Move)i, out _);
                    if (next != null && !OnGate(level, next) && seen.Add(Key(next)))
                        frontier.Enqueue((next, route + i));
                }
            }
            return null;
        }

        static string Key(Position p)
        {
            return $"{p.Turn}|{string.Join(",", p.Boxes)}|{p.Gate}|{p.Pearl}|{p.Fish}|" +
                   string.Join(",", p.Water.OrderBy(x => x));
        }
    }
}

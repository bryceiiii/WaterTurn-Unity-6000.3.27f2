using System;
using System.Collections.Generic;
using System.Linq;

namespace WaterTurn
{
    public enum Move { Up, Right, Down, Left, Turn }
    public enum Block { None, Dry, Crate, Grate }

    public sealed class Level
    {
        public readonly string Name, Hint;
        public readonly int Width, Height, Exit, Sensor, Pearl;
        public readonly string Tiles;
        public readonly Position Start;

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
            Pearl = Tiles.IndexOf('K');
            var water = new List<int>();
            int rider = -1, box = -1;
            for (int p = 0; p < Tiles.Length; p++)
            {
                char tile = Tiles[p];
                if (tile == 'P') rider = water.Count;
                if (tile == 'P' || tile == '~' || tile == 'B') water.Add(p);
                if (tile == 'B' || tile == 'b') box = p;
            }
            if (rider < 0 || Exit < 0) throw new ArgumentException(name);
            Start = new Position(water.ToArray(), rider, box, 0, false, false);
        }
    }

    public sealed class Position
    {
        public readonly int[] Water;
        public readonly int Rider, Box, Turn;
        public readonly bool Gate, Pearl;
        public int Fish => Water[Rider];

        public Position(int[] water, int rider, int box, int turn, bool gate, bool pearl)
        {
            Water = water;
            Rider = rider;
            Box = box;
            Turn = turn;
            Gate = gate;
            Pearl = pearl;
        }
    }

    public static class Puzzle
    {
        static readonly int[] X = {0, 1, 0, -1};
        static readonly int[] Y = {-1, 0, 1, 0};

        public static Position Begin(Level level) => Fall(level, level.Start, out _);
        public static bool Won(Level level, Position p) => p.Fish == level.Exit && (level.Pearl < 0 || p.Pearl);

        public static Position Apply(Level level, Position p, Move move, out Block block)
        {
            if (move == Move.Turn)
                return Fall(level, new Position(p.Water, p.Rider, p.Box, (p.Turn + 1) % 4, p.Gate, p.Pearl), out block);

            block = Block.None;
            int direction = ((int)move - p.Turn + 4) % 4;
            int step = Step(level, p.Fish, direction);
            int rider = Array.IndexOf(p.Water, step);
            if (rider < 0) { block = Block.Dry; return null; }
            if (level.Tiles[step] == 'G') { block = Block.Grate; return null; }

            bool hasPearl = p.Pearl || step == level.Pearl;
            if (step != p.Box) return new Position(p.Water, rider, p.Box, p.Turn, p.Gate, hasPearl);

            int box = Step(level, p.Box, direction);
            if (!Free(level, box, p.Gate) || level.Tiles[box] == 'G' || box == level.Exit)
            {
                block = Block.Crate;
                return null;
            }
            return Fall(level, new Position(p.Water, rider, box, p.Turn, p.Gate, hasPearl), out block);
        }

        static int Step(Level level, int pos, int direction)
        {
            int x = pos % level.Width + X[direction], y = pos / level.Width + Y[direction];
            return x < 0 || x >= level.Width || y < 0 || y >= level.Height ? -1 : x + y * level.Width;
        }

        static bool Free(Level level, int cell, bool gate) =>
            cell >= 0 && level.Tiles[cell] != '#' && (gate || level.Tiles[cell] != 'D');

        static Position Fall(Level level, Position p, out Block block)
        {
            block = Block.None;
            int direction = (6 - p.Turn) % 4;
            int[] water = (int[])p.Water.Clone();
            bool gate = p.Gate;
            int Projection(int cell) => X[direction] * (cell % level.Width) + Y[direction] * (cell / level.Width);
            int[] order = Enumerable.Range(0, water.Length).OrderByDescending(i => Projection(water[i])).ToArray();

            // A newly opened gate can let the same body of water fall farther.
            for (int pass = 0; pass < 2; pass++)
            {
                var wet = new HashSet<int>(water);
                foreach (int i in order)
                {
                    wet.Remove(water[i]);
                    int next = Step(level, water[i], direction);
                    while (Free(level, next, gate) && next != p.Box && !wet.Contains(next))
                    {
                        if (i == p.Rider && level.Tiles[next] == 'G')
                        {
                            block = Block.Grate;
                            return null;
                        }
                        water[i] = next;
                        next = Step(level, next, direction);
                    }
                    wet.Add(water[i]);
                }
                if (gate || !wet.Contains(level.Sensor)) break;
                gate = true;
            }
            return new Position(water, p.Rider, p.Box, p.Turn, gate, p.Pearl || water[p.Rider] == level.Pearl);
        }

        public static string Solve(Level level, Position start = null)
        {
            start = start ?? Begin(level);
            if (start == null) return null;
            var frontier = new Queue<(Position position, string route)>();
            var seen = new HashSet<string>();
            frontier.Enqueue((start, ""));
            seen.Add(Key(start));
            while (frontier.Count > 0)
            {
                var (pos, route) = frontier.Dequeue();
                if (Won(level, pos)) return route;
                for (int i = 0; i <= (int)Move.Turn; i++)
                {
                    Position next = Apply(level, pos, (Move)i, out _);
                    if (next != null && seen.Add(Key(next))) frontier.Enqueue((next, route + i));
                }
            }
            return null;
        }

        static string Key(Position p)
        {
            return $"{p.Turn}|{p.Box}|{p.Gate}|{p.Pearl}|{p.Fish}|{string.Join(",", p.Water.OrderBy(x => x))}";
        }
    }
}

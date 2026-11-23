using System.Collections.Generic;
using UnityEngine;

namespace WaterTurn
{
    /// <summary>
    /// The tidal cave is now a puzzle board rather than one connected lake.
    /// Rock walls cut the water into sealed chambers, and a handful of 2x2
    /// "water blocks" can be rotated a quarter turn at a time.  Each block
    /// holds a two-cell bar of water; depending on its angle it joins two
    /// adjacent sides of its niche:
    ///
    ///     state 0  top row     north + east
    ///     state 1  right col   east  + south
    ///     state 2  bottom row  south + west
    ///     state 3  left col    west  + north
    ///
    /// Every chamber is only reachable once the player has turned the right
    /// blocks, which is what turns travelling to another level into a puzzle.
    /// </summary>
    public sealed class HubPuzzle
    {
        public const int W = 45, H = 27;

        public sealed class Valve
        {
            public readonly int X, Y;
            public readonly int InitialState;
            public readonly int Solution;
            public int State;

            public Valve(int x, int y, int state, int solution)
            {
                X = x;
                Y = y;
                State = InitialState = state;
                Solution = solution;
            }

            public string LinkLabel => SidePair(State);
        }

        // '#' rock, '~' water, '.' dry stone / rotatable water block, 'g'/'x' doors.
        static readonly string[] Rows =
        {
            "#############################################",
            "#############################################",
            "#############################################",
            "#############################################",
            "#############################################",
            "#############################################",
            "###.......########################.......####",
            "###....~~~~#######################.......####",
            "###...~~..~~#############..~~~~x~~..~~...####",
            "###...~~..#..~###########..######~~~~~...####",
            "###.......#..~#..#########~#######.......####",
            "###.......###~~..#########~#######.......####",
            "###.......######~#########~#######.......####",
            "################~~~~~~~~~~~##################",
            "###################...~~..###################",
            "###################...~~..###################",
            "###.......#####~~~~~~~~~..########.......####",
            "###.......###..~###....~..########.......####",
            "###...~~..###..####....~~.########..~~...####",
            "###...~~~.####~####.....~.########..~~...####",
            "###.....~.####~#########~#####..##..~....####",
            "###.....~~~~~g~#########~####~..##..~....####",
            "###.......##############..~~~~#~~~~~~....####",
            "########################..###################",
            "#############################################",
            "#############################################",
            "#############################################",
        };

        public readonly Valve[] Valves =
        {
            // Top-left cell, initial state, intended solution.
            new Valve(11, 9, 1, 0),   // north-west route, block 1
            new Valve(15, 10, 0, 2),  // north-west route, block 2
            new Valve(25, 8, 0, 1),   // north-east route
            new Valve(13, 17, 0, 1),  // south-west route
            new Valve(24, 22, 1, 0),  // south-east route, block 1
            new Valve(30, 20, 0, 2),  // south-east route, block 2
        };

        public HubPuzzle()
        {
            // The strings are the single source of truth; this only checks the
            // kind of mistake that would otherwise fail silently at runtime.
            if (Rows.Length != H)
                Debug.LogError("WaterTurn: hub map has " + Rows.Length + " rows, expected " + H + ".");
            for (int y = 0; y < Rows.Length; y++)
                if (Rows[y].Length != W)
                    Debug.LogError("WaterTurn: hub map row " + y + " has " + Rows[y].Length +
                                   " columns, expected " + W + ".");
        }

        public char BaseTile(int x, int y)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return '#';
            return Rows[y][x];
        }

        public bool IsValveCell(int x, int y) => ValveAt(x, y) != null;

        public Valve ValveAt(int x, int y)
        {
            foreach (var v in Valves)
                if (x >= v.X && x <= v.X + 1 && y >= v.Y && y <= v.Y + 1)
                    return v;
            return null;
        }

        public bool IsWalkableCell(int x, int y)
        {
            char c = BaseTile(x, y);
            return c == '~' || c == '.' || IsGate(c);
        }

        public static bool IsGate(char c) => c == 'g' || c == 'x';

        /// <summary>Tile as the fish sees it right now, doors included.</summary>
        public char Tile(int x, int y, bool doorsOpen)
        {
            char c = BaseTile(x, y);
            if (IsGate(c)) return doorsOpen ? '~' : c;
            if (c != '.') return c;

            var v = ValveAt(x, y);
            if (v == null) return c;
            return WaterOffset(x - v.X, y - v.Y, v.State) ? '~' : '#';
        }

        public bool IsWaterCell(int x, int y, bool doorsOpen)
        {
            return Tile(x, y, doorsOpen) == '~';
        }

        public static bool WaterOffset(int dx, int dy, int state)
        {
            switch (state & 3)
            {
                case 0: return dy == 0; // top row
                case 1: return dx == 1; // right column
                case 2: return dy == 1; // bottom row
                default: return dx == 0; // left column
            }
        }

        public static string SidePair(int state)
        {
            switch (state & 3)
            {
                case 0: return "北 ↔ 东";
                case 1: return "东 ↔ 南";
                case 2: return "南 ↔ 西";
                default: return "西 ↔ 北";
            }
        }

        /// <summary>
        /// Which sides the block currently joins to actual water.  Used for the
        /// glow and the HUD counter, not for movement (movement just tests tiles).
        /// </summary>
        public int WetSides(Valve v, bool doorsOpen)
        {
            int mask = 0;
            if (WaterOffset(0, 0, v.State) && IsWaterCell(v.X, v.Y - 1, doorsOpen)) mask |= 1;      // N
            if (WaterOffset(1, 0, v.State) && IsWaterCell(v.X + 2, v.Y, doorsOpen)) mask |= 2;      // E
            if (WaterOffset(1, 1, v.State) && IsWaterCell(v.X + 1, v.Y + 2, doorsOpen)) mask |= 4;  // S
            if (WaterOffset(0, 1, v.State) && IsWaterCell(v.X - 1, v.Y + 1, doorsOpen)) mask |= 8;  // W
            return mask;
        }

        public bool IsBlockOpen(Valve v, bool doorsOpen)
        {
            int sides = WetSides(v, doorsOpen);
            // More than one wet side means the block actually carries water
            // across from one corridor to another.
            return sides != 0 && (sides & (sides - 1)) != 0;
        }

        public void Rotate(Valve v)
        {
            v.State = (v.State + 1) & 3;
        }

        public void Reset()
        {
            foreach (var v in Valves) v.State = v.InitialState;
        }

        public void Solve()
        {
            foreach (var v in Valves) v.State = v.Solution;
        }

        public int OpenCount(bool doorsOpen)
        {
            int count = 0;
            foreach (var v in Valves)
                if (IsBlockOpen(v, doorsOpen)) count++;
            return count;
        }
    }
}

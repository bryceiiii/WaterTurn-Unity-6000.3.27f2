"""Port of Assets/WaterTurn/Puzzle.cs - BFS solver + level stats for WaterTurn."""
from collections import deque
import itertools, statistics

UP, RIGHT, DOWN, LEFT, TURN, TURNLEFT = 0, 1, 2, 3, 4, 5
X = [0, 1, 0, -1]
Y = [-1, 0, 1, 0]


class Level:
    def __init__(self, name, hint, rows):
        self.name, self.hint, self.rows = name, hint, rows
        self.w, self.h = len(rows[0]), len(rows)
        self.tiles = "".join(rows)
        self.exit = self.tiles.find("E")
        self.sensor = self.tiles.find("S")
        self.pearl = self.tiles.find("K")
        water, rider, box = [], -1, -1
        for p, t in enumerate(self.tiles):
            if t == "P":
                rider = len(water)
            if t in "P~B":
                water.append(p)
            if t == "B":
                box = p
        self.start = (tuple(water), rider, box, 0, False, False)


def step(lv, pos, d):
    x, y = pos % lv.w + X[d], pos // lv.w + Y[d]
    return -1 if x < 0 or x >= lv.w or y < 0 or y >= lv.h else x + y * lv.w


def free(lv, cell, gate):
    return cell >= 0 and lv.tiles[cell] != "#" and (gate or lv.tiles[cell] != "D")


def fall(lv, st):
    water, rider, box, turn, gate, pearl = st
    d = (6 - turn) % 4
    water = list(water)
    proj = lambda c: X[d] * (c % lv.w) + Y[d] * (c // lv.w)
    order = sorted(range(len(water)), key=lambda i: -proj(water[i]))
    for _ in range(2):
        wet = set(water)
        for i in order:
            wet.discard(water[i])
            nxt = step(lv, water[i], d)
            while free(lv, nxt, gate) and nxt != box and nxt not in wet:
                if i == rider and lv.tiles[nxt] == "G":
                    return None, "grate"
                water[i] = nxt
                nxt = step(lv, nxt, d)
            wet.add(water[i])
        if gate or lv.sensor not in wet:
            break
        gate = True
    return (tuple(water), rider, box, turn, gate, pearl or water[rider] == lv.pearl), None


def apply(lv, st, mv):
    water, rider, box, turn, gate, pearl = st
    if mv in (TURN, TURNLEFT):
        t = (turn + (1 if mv == TURN else 3)) % 4
        return fall(lv, (water, rider, box, t, gate, pearl))
    d = (mv - turn + 4) % 4
    nxt = step(lv, water[rider], d)
    if nxt < 0 or nxt not in water:
        return None, "dry"
    if lv.tiles[nxt] == "G":
        return None, "grate"
    hp = pearl or nxt == lv.pearl
    if nxt != box:
        return (water, water.index(nxt), box, turn, gate, hp), None
    nb = step(lv, box, d)
    if not free(lv, nb, gate) or lv.tiles[nb] == "G" or nb == lv.exit:
        return None, "crate"
    return fall(lv, (water, water.index(nxt), nb, turn, gate, hp))


def won(lv, st):
    return st[0][st[1]] == lv.exit and (lv.pearl < 0 or st[5])


def key(st):
    return (st[3], st[2], st[4], st[5], st[0][st[1]], tuple(sorted(st[0])))


NAMES = {UP: "↑", RIGHT: "→", DOWN: "↓", LEFT: "←", TURN: "R", TURNLEFT: "Q"}


def solve(lv):
    start, err = fall(lv, lv.start)
    if start is None:
        return None, 0, 0
    q, seen, depth = deque([(start, "")]), {key(start)}, 1
    best, states_at_depth = None, {}
    while q:
        st, route = q.popleft()
        if won(lv, st):
            return route, len(seen), len(route)
        states_at_depth[len(route)] = states_at_depth.get(len(route), 0) + 1
        for mv in range(6):
            nxt, _ = apply(lv, st, mv)
            if nxt is None:
                continue
            k = key(nxt)
            if k in seen:
                continue
            seen.add(k)
            q.append((nxt, route + str(mv)))
    return None, len(seen), 0


def pretty(route):
    return " ".join(NAMES[int(c)] for c in route) if route else "-"


def stats(lv):
    """Count first-move branches that lead to a win (min-step) vs dead ends."""
    start, _ = fall(lv, lv.start)
    if start is None:
        return {}
    out = {}
    for mv in range(6):
        nxt, why = apply(lv, start, mv)
        if nxt is None:
            out[NAMES[mv]] = "非法/死亡"
            continue
        # BFS from nxt
        q, seen = deque([(nxt, 1)]), {key(nxt)}
        found = None
        while q:
            st, d = q.popleft()
            if won(lv, st):
                found = d
                break
            for m2 in range(6):
                n2, _ = apply(lv, st, m2)
                if n2 is not None and key(n2) not in seen:
                    seen.add(key(n2))
                    q.append((n2, d + 1))
        out[NAMES[mv]] = f"{found}步" if found else "死路"
    return out


LEVELS = [
    ("01 先游再转", 2, ["#####", "#~.E#", "#P###", "#####"]),
    ("02 同水异路", 2, ["#######", "#...#.#", "#....##", "#..~~~#", "#.EP~##", "#######"]),
    ("03 换一股水", 4, ["#######", "#.#.#.#", "####.##", "#~~~..#", "#P~#E.#", "#######"]),
    ("04 沿岸折返", 5, ["#######", "#....E#", "#.#...#", "#~~~#.#", "#P~#.##", "#######"]),
]

if __name__ == "__main__":
    for name, want, rows in LEVELS:
        lv = Level(name, "", rows)
        route, seen, d = solve(lv)
        print(f"\n=== {name}  期望{want}步 ===")
        print("最短解:", pretty(route), "| 长度", d, "| 搜索状态数", seen)
        print("首步分支:", stats(lv))

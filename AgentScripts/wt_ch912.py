"""09-12 章（03 / 隔栅）新机制模拟器 —— 与 Assets/WaterTurn/Puzzle.cs 同规则。

本章新增三种材料 / 地形，全部围绕「谁能穿过什么」：
  M 薄膜 : 水进不去（等于墙）；鱼可以直接穿过（可连穿多格），但落点仍必须是水。
           和栅栏 G（水能过、鱼不能）正好互为反面，是本章的教学骨架。
  o 泉眼 : 永远有水、永远不动的一格。房间怎么转它都不掉，是「不会走的桥」。
  F 霜格 : 水「停」在霜格上会当场结冰（这一格水永久消失、变成冰墙 I）；
           鱼骑着的那格水结冰 = 鱼死。冰是永久地形，挡水也挡鱼。

用法：
    python wt_ch912.py            # 跑 CANDIDATES 里所有盘面
    python wt_ch912.py 09         # 只看 09
    python wt_ch912.py --play 09  # 打印最短解每一步的水位
"""
import sys
from collections import deque

UP, RIGHT, DOWN, LEFT, TURN, TURNLEFT = 0, 1, 2, 3, 4, 5
NAMES = {UP: "↑", RIGHT: "→", DOWN: "↓", LEFT: "←", TURN: "R", TURNLEFT: "Q"}
X = [0, 1, 0, -1]
Y = [-1, 0, 1, 0]
ARROW = {"^": 0, ">": 1, "v": 2, "<": 3}


class Level:
    def __init__(self, name, rows, membrane_limit=99):
        self.name, self.rows = name, rows
        self.w, self.h = len(rows[0]), len(rows)
        assert all(len(r) == self.w for r in rows), f"{name}: 行宽不一致 {[len(r) for r in rows]}"
        self.tiles = "".join(rows)
        self.exit = self.tiles.find("E")
        self.sensor = self.tiles.find("S")
        self.latch = self.tiles.find("s")
        self.pearl = self.tiles.find("K")
        self.frosts = [p for p, t in enumerate(self.tiles) if t == "F"]
        self.inverted = "d" in self.tiles
        self.springs = [p for p, t in enumerate(self.tiles) if t == "o"]
        self.membrane_limit = membrane_limit
        self.portals = {}
        mouths = [p for p, t in enumerate(self.tiles) if t in "12"]
        if len(mouths) == 2:
            self.portals[mouths[0]] = mouths[1]
            self.portals[mouths[1]] = mouths[0]
        elif mouths:
            raise ValueError(f"{name}: 传送门必须成对")
        water, rider, boxes = [], -1, []
        for p, t in enumerate(self.tiles):
            if t == "P":
                rider = len(water)
            if t in "P~Bo":
                water.append(p)
            if t == "B":
                boxes.append(p)
        if rider < 0:
            raise ValueError(f"{name}: 没有鱼 P")
        if self.exit < 0:
            raise ValueError(f"{name}: 没有出口 E")
        self.start = (tuple(water), rider, tuple(sorted(boxes)), 0, self.inverted, False, ())

    def step(self, cell, d):
        x, y = cell % self.w + X[d], cell // self.w + Y[d]
        if x < 0 or x >= self.w or y < 0 or y >= self.h:
            return -1
        return x + y * self.w

    def arrow_ok(self, cell, d):
        a = ARROW.get(self.tiles[cell])
        return a is None or a == d


def free(lv, cell, gate, ice):
    if cell < 0 or cell in ice:
        return False
    t = lv.tiles[cell]
    if t == "#" or t == "M" or t == "o":
        return False
    if t in "Dd" and not gate:
        return False
    return True


def settle(lv, water, rider, boxes, d, gate, ice):
    """单趟落定。返回 (water, None) 或 (None, reason)。"""
    water = list(water)
    boxset = set(boxes)
    proj = lambda c: X[d] * (c % lv.w) + Y[d] * (c // lv.w)
    order = [i for i in sorted(range(len(water)), key=lambda i: -proj(water[i])) if water[i] >= 0]
    for i in order:
        cell = water[i]
        if lv.tiles[cell] == "o":      # 泉眼不动
            continue
        wet = {c for c in water if c >= 0}
        wet.discard(cell)
        hops = 0
        while hops < lv.w * lv.h * 4:
            hops += 1
            nxt = lv.step(cell, d)
            if nxt < 0 or not lv.arrow_ok(nxt, d):
                break
            far = lv.portals.get(nxt)
            if far is not None:
                if nxt in boxset or far in boxset:
                    break
                out = lv.step(far, d)
                if not free(lv, out, gate, ice) or out in wet or out in boxset:
                    break
                cell = out
                continue
            if not free(lv, nxt, gate, ice) or nxt in wet or nxt in boxset:
                break
            if i == rider and lv.tiles[nxt] == "G":
                return None, "grate"
            cell = nxt
        water[i] = cell
    return water, None


def eject_from_valves(lv, water, rider, d, gate, boxes, ice):
    water = list(water)
    boxset = set(boxes)
    for i, cell in enumerate(water):
        if cell < 0 or lv.tiles[cell] not in ARROW or lv.arrow_ok(cell, d):
            continue
        for slot in (lv.step(cell, (d + 2) % 4), lv.step(cell, (d + 1) % 4),
                     lv.step(cell, (d + 3) % 4)):
            if slot < 0 or lv.tiles[slot] == "#" or lv.tiles[slot] in ARROW:
                continue
            if lv.tiles[slot] == "M" or slot in ice:
                continue
            if lv.tiles[slot] == "D" and not gate:
                continue
            if slot in water or slot in boxset:
                continue
            if i == rider and lv.tiles[slot] == "G":
                continue
            water[i] = slot
            break
    return water


def freeze(lv, water, rider):
    """水停在霜格上 → 结冰。返回 (结冰的格子, 鱼是否被冻)。"""
    made = []
    for i, cell in enumerate(water):
        if cell < 0 or lv.tiles[cell] != "F":
            continue
        if i == rider:
            return made, True
        made.append(cell)
        water[i] = -1
    return made, False


def fall(lv, st):
    water, rider, boxes, turn, gate, pearl, ice = st
    d = (6 - turn) % 4
    ice = tuple(ice)
    g = gate
    plates = [p for p, t in enumerate(lv.tiles) if t == "X"]
    if plates and all(p in boxes for p in plates):
        g = True

    settled = tuple(water)
    for _ in range(4):
        w, err = settle(lv, settled, rider, boxes, d, g, set(ice))
        if w is None:
            return None, err
        if lv.inverted and lv.sensor >= 0:
            # 逆闸：通行状态在落定结束才更新。门关了不回收已经流过去的水，
            # 门开了也要等下一步水才过得去；状态跨回合保持，这是它和时效格的区别。
            g = lv.sensor not in w
        elif lv.latch >= 0:
            pressed = lv.latch in w
            if pressed and not g:
                g = True
                continue
            g = pressed
        elif not g and lv.sensor >= 0 and lv.sensor in w:
            g = True
            continue
        made, rider_frozen = freeze(lv, w, rider)
        if rider_frozen:
            return None, "frozen"
        if made:
            ice = tuple(sorted(set(ice) | set(made)))
            settled = tuple(w)
            continue
        break
    w = eject_from_valves(lv, w, rider, d, g, boxes, ice)
    return (tuple(w), rider, boxes, turn, g, pearl or w[rider] == lv.pearl, ice), None


def apply(lv, st, mv):
    water, rider, boxes, turn, gate, pearl, ice = st
    if mv in (TURN, TURNLEFT):
        t = (turn + (1 if mv == TURN else 3)) % 4
        return fall(lv, (water, rider, boxes, t, gate, pearl, ice))
    d = (mv - turn + 4) % 4
    if water[rider] < 0:
        return None, "dead"
    cell = water[rider]
    nxt = lv.step(cell, d)
    if nxt < 0:
        return None, "dry"
    # 膜：鱼可以连穿多格，落点必须是水
    hops = 0
    while nxt >= 0 and lv.tiles[nxt] == "M" and hops < lv.membrane_limit:
        hops += 1
        nxt = lv.step(nxt, d)
    if nxt < 0:
        return None, "dry"
    if lv.tiles[nxt] in ARROW and ARROW[lv.tiles[nxt]] != d:
        return None, "valve"
    if nxt not in water:
        return None, "dry"
    if lv.tiles[nxt] == "G":
        return None, "grate"
    hp = pearl or nxt == lv.pearl
    if nxt in boxes:
        dest = lv.step(nxt, d)
        if dest < 0 or dest in boxes or lv.tiles[dest] in "#GEMo" or dest in ice:
            return None, "crate"
        if lv.tiles[dest] in "Dd" and not gate:
            return None, "crate"
        nb = tuple(sorted(dest if b == nxt else b for b in boxes))
        return fall(lv, (water, water.index(nxt), nb, turn, gate, hp, ice))
    return (water, water.index(nxt), boxes, turn, gate, hp, ice), None


def won(lv, st):
    water, rider, boxes, turn, gate, pearl, ice = st
    return water[rider] == lv.exit and not (lv.pearl >= 0 and not pearl)


def key(st):
    water, rider, boxes, turn, gate, pearl, ice = st
    return (turn, boxes, gate, pearl, ice, water[rider], tuple(sorted(water)))


def solve(lv, limit=400000):
    start, err = fall(lv, lv.start)
    if start is None:
        return None, 0, err
    q, seen = deque([(start, "")]), {key(start)}
    while q:
        st, route = q.popleft()
        if won(lv, st):
            return route, len(seen), None
        if len(seen) > limit:
            return None, len(seen), "limit"
        for mv in range(6):
            nxt, _ = apply(lv, st, mv)
            if nxt is None:
                continue
            k = key(nxt)
            if k in seen:
                continue
            seen.add(k)
            q.append((nxt, route + str(mv)))
    return None, len(seen), "exhausted"


def depth(lv, st):
    q, seen = deque([(st, 0)]), {key(st)}
    while q:
        s, d = q.popleft()
        if won(lv, s):
            return d
        for mv in range(6):
            n2, _ = apply(lv, s, mv)
            if n2 is not None and key(n2) not in seen:
                seen.add(key(n2))
                q.append((n2, d + 1))
    return None


def report(tag, rows, verbose=True):
    try:
        lv = Level(tag, rows)
        start, err = fall(lv, lv.start)
    except Exception as exc:
        print(f"\n--- {tag} ---\n   盘面错误: {exc}")
        return None
    if verbose:
        print(f"\n--- {tag} ---")
        for r in rows:
            print("   ", r)
    if start is None:
        print(f"   ❌ 起点即死 ({err})")
        return None
    route, states, err = solve(lv)
    if route is None:
        print(f"   ❌ 无解 ({err}, 局面 {states})")
        return None
    turns = route.count("4") + route.count("5")
    steps = len(route) - turns
    first = {}
    for mv in range(6):
        nxt, why = apply(lv, start, mv)
        if nxt is None:
            first[NAMES[mv]] = "死:" + (why or "?")
        else:
            d = depth(lv, nxt)
            first[NAMES[mv]] = (d + 1) if d is not None else "死:死路"
    traps = [k for k, v in first.items() if v == "死:死路"]
    wins = [k for k, v in first.items() if v == steps + turns]
    print(f"   par {turns} 转 / {steps} 步 = {len(route)}   帧 {states}")
    print(f"   路线: {' '.join(NAMES[int(c)] for c in route)}")
    print(f"   首步: " + "  ".join(f"{k}:{v}" for k, v in first.items()))
    print(f"   最短首步 {wins or '无'}   首步即死路 {traps or '无'}")
    return dict(lv=lv, route=route, par=len(route), states=states, first=first,
                turns=turns, steps=steps, start=start)


def play(rows, route=None):
    lv = Level("replay", rows)
    st, err = fall(lv, lv.start)
    if st is None:
        print("起点即死:", err)
        return
    if not route:
        route, _, err = solve(lv)
        if route is None:
            print("无解:", err)
            return

    def dump(s, tag):
        water, rider, boxes, turn, gate, pearl, ice = s
        g = [list(r) for r in lv.rows]
        for c in ice:
            g[c // lv.w][c % lv.w] = "I"
        for i, c in enumerate(water):
            if c < 0:
                continue
            g[c // lv.w][c % lv.w] = "P" if i == rider else "~"
        print(f"  {tag:<3} turn={turn} 门={'开' if gate else '关'} 水={sum(1 for c in water if c >= 0)}")
        for r in g:
            print("      " + "".join(r))

    dump(st, "start")
    for ch in route:
        st, why = apply(lv, st, int(ch))
        if st is None:
            print("  死:", NAMES[int(ch)], why)
            return
        dump(st, NAMES[int(ch)])
    print("  won:", won(lv, st))


CANDIDATES = {
    "09 薄膜": [
        "#######",
        "#..#..#",
        "#.#~E##",
        "###M###",
        "#.....#",
        "#P~~~~#",
        "#######",
    ],
    "10 逆闸": [
        "#########",
        "#.......#",
        "#.P~~...#",
        "#.......#",
        "#.......#",
        "#..~~.dE#",
        "#..S..#.#",
        "#########",
    ],
    "11 泉眼": [
        "###########",
        "#...oo..E.#",
        "#...##....#",
        "#.P~~#~~..#",
        "#.~~~~#~~~#",
        "###########",
    ],
    "12 合流": [
        "###########",
        "#..oo..M..#",
        "#...##....#",
        "#.P~~#~~..#",
        "#.~~~~#~~E#",
        "###########",
    ],
}

# 每关只替换掉一种材料：用来证明"这个机制不是装饰"。
NECESSITY = {
    "09 薄膜": [("去掉薄膜", "M", ".")],
    "10 逆闸": [("逆闸常开", "d", ".")],
    "11 泉眼": [("去掉泉眼", "o", ".")],
    "12 合流": [("去掉泉眼", "o", "."), ("去掉薄膜", "M", ".")],
}

if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    want = args[0] if args else None
    if "--play" in sys.argv:
        play(CANDIDATES[want])
    elif "--need" in sys.argv:
        for tag, rows in CANDIDATES.items():
            if want and not tag.startswith(want):
                continue
            base = solve(Level(tag, rows))[0]
            print(f"{tag}: par {len(base)}  {' '.join(NAMES[int(c)] for c in base)}")
            for label, old, new in NECESSITY.get(tag, []):
                rows2 = [r.replace(old, new) for r in rows]
                route, states, err = solve(Level(tag, rows2))
                if route is None:
                    print(f"    {label}: 无解（{err}）")
                else:
                    print(f"    {label}: par {len(route)}  {' '.join(NAMES[int(c)] for c in route)}")
    else:
        for k, rows in CANDIDATES.items():
            if want and not k.startswith(want):
                continue
            report(k, rows)

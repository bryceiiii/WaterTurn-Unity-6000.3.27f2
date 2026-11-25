"""05-08 章「推箱 + 传送门」的机制模拟与盘面评估。

在图块上扩展 Puzzle.cs（保持水/鱼/箱/闸门/珍珠语义不变），新增两类东西：

传送门 '1' '2'（成对）：
  - 水进入门格 -> 从配对门格"穿出"，并在穿出方向继续下落（落点 = 门后第一格）。
    门后那一格被堵、或另一扇门被箱子占住 -> 这一格水停在门前，门不生效。
  - 鱼骑在被传送的水格上，一起过去（这就是鱼唯一的"跨墙"手段）。
  - 水永远不会"停在"门格里，所以门格既是水路捷径，也永远不会当水库。
  - 箱子可以推进门格，但推上去就把门堵死（门变墙）——「箱子是塞子」。

推箱（对齐现有 Puzzle.cs 并收紧为可判定的规则）：
  - 鱼游进箱子格 -> 箱子沿同方向推一格。
  - 箱子只能推进「无水、非墙、非出口、非关闭闸门、非栅栏、非另一箱」的格子
    （等价于"箱子推不动水"，与现有关卡的实际用法一致）。
  - 箱子不随旋转下落（地图坐标固定），只被鱼推动。
  - 目标格 'X'：Sokoban 的靶；全部靶被箱子压住 + 鱼到出口 = 通关。

用法：
    python wt_ch58.py            # 评估 CANDIDATES 里所有候选盘面
    python wt_ch58.py 05         # 只评估某一个
"""
import sys
from collections import deque

UP, RIGHT, DOWN, LEFT, TURN, TURNLEFT = 0, 1, 2, 3, 4, 5
NAMES = {UP: "↑", RIGHT: "→", DOWN: "↓", LEFT: "←", TURN: "R", TURNLEFT: "Q"}
X = [0, 1, 0, -1]
Y = [-1, 0, 1, 0]

WALL, DRY, WATER, EXIT, PORTAL = "#", ".", "~", "E", "12"
CRATE_BLOCK = set("#GE")          # 箱子进不去的格子（栅栏 G、闸门 D 另按开闭判断）


class Level:
    """盘面。图块：
    # 墙   . 干地   ~ 水   P 鱼起始(水)   B 箱子   b 箱子已压在靶上
    X 靶   E 出口   1/2 传送门   D 闸门   S 感应格   G 栅栏   K 珍珠
    """

    def __init__(self, name, rows):
        self.name, self.rows = name, rows
        self.w, self.h = len(rows[0]), len(rows)
        assert all(len(r) == self.w for r in rows), f"{name}: 行宽不一致"
        self.tiles = "".join(rows)
        self.exit = self.tiles.find("E")
        self.sensor = self.tiles.find("S")
        self.pearl = self.tiles.find("K")

        water, rider = [], -1
        boxes, targets = [], []
        for p, t in enumerate(self.tiles):
            if t == "P":
                rider = len(water)
            if t in "P~B":
                water.append(p)
            if t == "B":
                boxes.append(p)
            if t == "b":
                water.append(p)          # 压在靶上的箱子同样占一格水
                boxes.append(p)
                targets.append(p)
            if t == "X":
                targets.append(p)
        portal_cells = [p for p, t in enumerate(self.tiles) if t in "12"]
        portals = {}
        if len(portal_cells) == 2:
            portals[portal_cells[0]] = portal_cells[1]
            portals[portal_cells[1]] = portal_cells[0]
        elif portal_cells:
            raise ValueError(f"{name}: 传送门必须成对（找到 {len(portal_cells)} 个）")
        if rider < 0:
            raise ValueError(f"{name}: 没有鱼 P")
        if self.exit < 0:
            raise ValueError(f"{name}: 没有出口 E")
        self.portals = portals
        self.start = (tuple(water), rider, tuple(sorted(set(boxes))), 0, False, False)

    # --- 基础查询 ---
    def at(self, cell):
        return self.tiles[cell] if cell >= 0 else WALL

    def step(self, cell, d):
        x, y = cell % self.w + X[d], cell // self.w + Y[d]
        if x < 0 or x >= self.w or y < 0 or y >= self.h:
            return -1
        return x + y * self.w

    def water_ok(self, cell, gate, boxes, wet):
        """一格水能不能落进 cell（不含门逻辑）。"""
        if cell < 0 or cell in boxes or cell in wet:
            return False
        t = self.tiles[cell]
        if t == "#" or t == "G":
            return False
        if t == "D" and not gate:
            return False
        return True


def fall(lv, st):
    """水按重力落定，返回 (新状态, 错误原因)。错误只会是鱼撞栅栏。"""
    water, rider, boxes, turn, gate, pearl = st
    d = (6 - turn) % 4
    water = list(water)
    boxset = set(boxes)
    proj = lambda c: X[d] * (c % lv.w) + Y[d] * (c // lv.w)
    order = sorted(range(len(water)), key=lambda i: -proj(water[i]))

    plates = [p for p, t in enumerate(lv.tiles) if t == "X"]
    if plates and all(p in boxset for p in plates):
        gate = True

    for _ in range(2):                      # 闸门可能在同一落里开启，再落一次
        wet = set(water)
        for i in order:
            wet.discard(water[i])
            cur = water[i]
            seen = {cur}
            while True:
                nxt = lv.step(cur, d)
                if nxt < 0:
                    break
                if lv.tiles[nxt] in PORTAL:
                    if nxt in boxset:                       # 箱子堵住门
                        break
                    far = lv.portals[nxt]
                    if far in boxset or far in wet:
                        break
                    out = lv.step(far, d)
                    if not lv.water_ok(out, gate, boxset, wet):   # 门后第一格不通 = 门失效
                        break
                    if out in seen:
                        break
                    seen.add(out)
                    cur = out
                    continue
                if lv.water_ok(nxt, gate, boxset, wet):
                    cur = nxt
                    continue
                break
            if i == rider and lv.tiles[cur] == "G":
                return None, "grate"
            water[i] = cur
            wet.add(cur)
        if gate or lv.sensor not in wet:
            break
        gate = True
    return (tuple(water), rider, boxes, turn, gate,
            pearl or water[rider] == lv.pearl), None


def apply(lv, st, mv):
    """apply() 的整理版：统一返回 (state|None, reason)。"""
    if mv in (TURN, TURNLEFT):
        water, rider, boxes, turn, gate, pearl = st
        t = (turn + (1 if mv == TURN else 3)) % 4
        return fall(lv, (water, rider, boxes, t, gate, pearl))
    water, rider, boxes, turn, gate, pearl = st
    d = (mv - turn + 4) % 4
    nxt = lv.step(water[rider], d)
    if nxt < 0:
        return None, "dry"
    hp = pearl or nxt == lv.pearl
    if nxt not in water:                       # 鱼只能在水的格子里移动（包括去推箱子）
        return None, "dry"
    if nxt in boxes:
        if nxt in lv.portals:
            return None, "crate"
        dest = lv.step(nxt, d)
        if dest < 0 or dest in boxes:
            return None, "crate"
        t = lv.tiles[dest]
        if t in "#GE":
            return None, "crate"
        if t == "D" and not gate:
            return None, "crate"
        # 箱子可以推进水格：水留在原地、被箱子压住；鱼跟进箱子原来的那一格水。
        newboxes = tuple(sorted(dest if b == nxt else b for b in boxes))
        return fall(lv, (water, water.index(nxt), newboxes, turn, gate, hp))
    if nxt not in water:
        return None, "dry"
    if lv.tiles[nxt] == "G":
        return None, "grate"
    return (water, water.index(nxt), boxes, turn, gate, hp), None


def won(lv, st):
    water, rider, boxes, turn, gate, pearl = st
    if water[rider] != lv.exit:
        return False
    if lv.pearl >= 0 and not pearl:
        return False
    targets = [p for p, t in enumerate(lv.tiles) if t in "Xb"]
    return all(p in boxes for p in targets)


def key(st):
    water, rider, boxes, turn, gate, pearl = st
    return (turn, boxes, gate, pearl, water[rider], tuple(sorted(water)))


def solve(lv, limit=400000):
    start, err = fall(lv, lv.start)
    if start is None:
        return None, 0
    q, seen = deque([(start, "")]), {key(start)}
    while q:
        st, route = q.popleft()
        if won(lv, st):
            return route, len(seen)
        if len(seen) > limit:
            return None, len(seen)
        for mv in range(6):
            nxt, _ = apply(lv, st, mv)
            if nxt is None:
                continue
            k = key(nxt)
            if k in seen:
                continue
            seen.add(k)
            q.append((nxt, route + str(mv)))
    return None, len(seen)


def turns_steps(route):
    return (route.count("4") + route.count("5"),
            sum(route.count(str(i)) for i in range(4)))


def first_move_table(lv):
    start, _ = fall(lv, lv.start)
    out = {}
    for mv in range(6):
        nxt, why = apply(lv, start, mv)
        if nxt is None:
            out[NAMES[mv]] = "死:" + (why or "?")
            continue
        route, _ = solve(lv, limit=200000)
        # 从 nxt 出发的最短剩余
        q, seen, found = deque([(nxt, 1)]), {key(nxt)}, None
        while q:
            s, depth = q.popleft()
            if won(lv, s):
                found = depth
                break
            for m2 in range(6):
                n2, _ = apply(lv, s, m2)
                if n2 is not None and key(n2) not in seen:
                    seen.add(key(n2))
                    q.append((n2, depth + 1))
        out[NAMES[mv]] = found if found else "死:死路"
    return out


def report(tag, rows, verbose=True):
    lv = Level(tag, rows)
    route, states = solve(lv)
    if verbose:
        print(f"\n--- {tag} ---")
        for r in rows:
            print("   ", r)
    if route is None:
        print("   ❌ 无解")
        return None
    t, s = turns_steps(route)
    fm = first_move_table(lv)
    traps = [k for k, v in fm.items() if v == "死:死路"]
    ok = [k for k, v in fm.items() if isinstance(v, int) and v <= len(route)]
    print(f"   par {t} 转 / {s} 步   路线: {' '.join(NAMES[int(c)] for c in route)}")
    print(f"   局面 {states}   首步: " + "  ".join(f"{k}:{v}" for k, v in fm.items()))
    print(f"   par 内可行首步 {ok or '无'}   首步即死路 {traps or '无'}")
    return dict(turns=t, steps=s, route=route, states=states, first=fm, traps=traps)


CANDIDATES = {}


if __name__ == "__main__":
    want = sys.argv[1] if len(sys.argv) > 1 else None
    for k, rows in CANDIDATES.items():
        if want and not k.startswith(want):
            continue
        report(k, rows)

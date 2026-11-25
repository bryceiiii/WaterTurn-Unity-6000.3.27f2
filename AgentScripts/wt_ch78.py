"""07/08 两关新机制的独立模拟器（不再与 05/06 共用推箱/传送门规则）。

07 「单向阀」核心机制 —— 箭头格 '^' '>' 'v' '<'：
  - 水只能沿箭头方向进入这一格，逆着流等于撞墙（水停在箭头前面）。
  - 箭头是「房间坐标」的方向，房间转一次，箭头在屏幕上就跟着转 90°。
    水每个朝向只有一条流动方向，所以一扇阀只在「箭头正好朝下那一档」通水，
    其余三档它就是墙——这就是整关的推理核心。
  - 鱼自己游动不受阀门限制（鱼是主动游，水是被动流）。
  - 07 不用箱子、不用闸门、不用传送门。

（下面这段排水规则保留在模拟器里，供以后做别的关，07 不用。）
07 「排水塔」核心机制 —— 排水口 'v'：
  - 水落到排水口，这一格水永久消失（水位数组里留 -1 的洞）。
  - 每个排水口每「一次落定」最多吞一格：吞掉之后它在本次落定里临时变成实心
    地板，后面的水可以停在它上面（否则整条水带会被一口气吸干，没法读）。
  - 鱼骑着的那格水被吞 = 鱼死（返回 None）。
  - 排水不可逆：少一格水就是少一格，只能靠撤销回退。这是「减法」。
  - 07 不用箱子、不用闸门、不用传送门：动词只有「转」和「游」。

08 「回廊时效闸门」核心机制 —— 时效感应格 's'：
  - 只要有一格水停在 s 上，闸门 D 立刻打开；水一离开 s，闸门当回合就关。
  - 与固定感应格 'S'（压住就永久开）形成对照：S 是开关，s 是窗口。
  - 08 不用箱子、不用排水口、不用传送门：靠「两股水同时到位」的时序。
"""
import sys
from collections import deque

UP, RIGHT, DOWN, LEFT, TURN, TURNLEFT = 0, 1, 2, 3, 4, 5
NAMES = {UP: "↑", RIGHT: "→", DOWN: "↓", LEFT: "←", TURN: "R", TURNLEFT: "Q"}
X = [0, 1, 0, -1]
Y = [-1, 0, 1, 0]
ARROW = {"^": 0, ">": 1, "v": 2, "<": 3}


class Level:
    """# 墙  . 干地  ~ 水  P 鱼  E 出口  D 闸门  S 固定感应  s 时效感应
       v 排水口  K 珍珠  1/2 传送门  X 重压格  B 箱子  G 栅栏"""

    def __init__(self, name, rows):
        self.name, self.rows = name, rows
        self.w, self.h = len(rows[0]), len(rows)
        assert all(len(r) == self.w for r in rows), f"{name}: 行宽不一致 {[len(r) for r in rows]}"
        self.tiles = "".join(rows)
        self.exit = self.tiles.find("E")
        self.sensor = self.tiles.find("S")
        self.latch = self.tiles.find("s")
        self.pearl = self.tiles.find("K")
        self.drains = [p for p, t in enumerate(self.tiles) if t == "o"]

        water, rider, boxes = [], -1, []
        for p, t in enumerate(self.tiles):
            if t == "P":
                rider = len(water)
            if t in "P~B":
                water.append(p)
            if t == "B":
                boxes.append(p)
        portal_cells = [p for p, t in enumerate(self.tiles) if t in "12"]
        self.portals = {}
        if len(portal_cells) == 2:
            self.portals[portal_cells[0]] = portal_cells[1]
            self.portals[portal_cells[1]] = portal_cells[0]
        elif portal_cells:
            raise ValueError(f"{name}: 传送门必须成对")
        if rider < 0:
            raise ValueError(f"{name}: 没有鱼 P")
        if self.exit < 0:
            raise ValueError(f"{name}: 没有出口 E")
        self.start = (tuple(water), rider, tuple(sorted(boxes)), 0, False, False)

    def step(self, cell, d):
        x, y = cell % self.w + X[d], cell // self.w + Y[d]
        if x < 0 or x >= self.w or y < 0 or y >= self.h:
            return -1
        return x + y * self.w

    def arrow_ok(self, cell, d, turn):
        """单向阀：箭头和水的流动方向都在房间坐标里，直接比，不再折算 turn。

        折算 turn 等于把箭头当成「屏幕固定方向」，可画面上箭头是跟着房间转的，
        于是画着 → 的阀会放行「向屏下流」的水——玩家看到的就是水从阀的侧面钻进去。
        不折算以后，一扇阀只在「箭头正好朝下」的那一档通水（水永远朝屏下流）：
            v 阀 turn0 / > 阀 turn1 / ^ 阀 turn2 / < 阀 turn3，其余三档都是墙。
        """
        a = ARROW.get(self.tiles[cell])
        return a is None or a == d

    def ok(self, cell, gate, boxes, wet, drained):
        if cell < 0 or cell in boxes or cell in wet:
            return False
        t = self.tiles[cell]
        if t in "#G":
            return False
        if t == "o" and cell not in drained:
            return False
        if t == "D" and not gate:
            return False
        return True


def settle(lv, water, rider, boxes, turn, gate, pearl, drained):
    """单趟落定（不处理闸门自触发）。返回 (water, None) 或 (None, reason)。"""
    d = (6 - turn) % 4
    water = list(water)
    boxset = set(boxes)
    proj = lambda c: X[d] * (c % lv.w) + Y[d] * (c // lv.w)
    order = [i for i in sorted(range(len(water)), key=lambda i: -proj(water[i])) if water[i] >= 0]

    for i in order:
        wet = {c for c in water if c >= 0}
        wet.discard(water[i])
        cur = water[i]
        seen = {cur}
        while True:
            nxt = lv.step(cur, d)
            if nxt < 0:
                break
            t = lv.tiles[nxt]
            if t == "o" and nxt not in drained:
                drained.add(nxt)
                if i == rider:
                    return None, "drain"
                water[i] = -1
                break
            if not lv.arrow_ok(nxt, d, turn):
                break
            if t in lv.portals:
                if nxt in boxset:
                    break
                far = lv.portals[nxt]
                if far in boxset or far in wet:
                    break
                out = lv.step(far, d)
                if not lv.ok(out, gate, boxset, wet, drained):
                    break
                if out in seen:
                    break
                seen.add(out)
                cur = out
                continue
            if lv.ok(nxt, gate, boxset, wet, drained):
                cur = nxt
                continue
            break
        if water[i] >= 0:
            if i == rider and lv.tiles[cur] == "G":
                return None, "grate"
            water[i] = cur
    return water, None


def eject_from_valves(lv, water, rider, turn, gate, boxes):
    """阀格是通道不是水库：转向后若水停在阀里、当前朝向又不允许它沿这个方向流，
    就把它挤回入口侧（入口被堵则从两侧出去）。"""
    d = (6 - turn) % 4
    water = list(water)
    boxset = set(boxes)
    for i, cell in enumerate(water):
        if cell < 0 or lv.tiles[cell] not in ARROW or lv.arrow_ok(cell, d, turn):
            continue
        for slot in (lv.step(cell, (d + 2) % 4), lv.step(cell, (d + 1) % 4), lv.step(cell, (d + 3) % 4)):
            if slot < 0 or lv.tiles[slot] in "#" or lv.tiles[slot] in ARROW:
                continue
            if lv.tiles[slot] == "D" and not gate:
                continue
            if slot in water or slot in boxset:
                continue
            if i == rider and lv.tiles[slot] == "G":
                continue
            water[i] = slot
            break
    return tuple(water)


def fall(lv, st):
    water, rider, boxes, turn, gate, pearl = st
    boxset = set(boxes)
    drained = set()

    base = gate
    plates = [p for p, t in enumerate(lv.tiles) if t == "X"]
    if plates and all(p in boxset for p in plates):
        base = True
    if lv.sensor >= 0 and any(c == lv.sensor for c in water):
        base = True

    w1, err = settle(lv, water, rider, boxes, turn, base, pearl, drained)
    if w1 is None:
        return None, err

    final_gate = base
    if lv.latch >= 0:
        pressed = any(c == lv.latch for c in w1)
        if pressed and not base:
            w2, err2 = settle(lv, water, rider, boxes, turn, True, pearl, set(drained))
            if w2 is not None and any(c == lv.latch for c in w2):
                w1, final_gate = w2, True
            else:
                final_gate = False
        else:
            final_gate = pressed
    else:
        if lv.sensor >= 0 and any(c == lv.sensor for c in w1):
            final_gate = True

    w1 = eject_from_valves(lv, w1, rider, turn, final_gate, boxes)

    return (tuple(w1), rider, boxes, turn, final_gate,
            pearl or w1[rider] == lv.pearl), None


def apply(lv, st, mv):
    water, rider, boxes, turn, gate, pearl = st
    if mv in (TURN, TURNLEFT):
        t = (turn + (1 if mv == TURN else 3)) % 4
        return fall(lv, (water, rider, boxes, t, gate, pearl))
    d = (mv - turn + 4) % 4
    if water[rider] < 0:
        return None, "dead"
    nxt = lv.step(water[rider], d)
    if nxt < 0:
        return None, "dry"
    # 阀格对鱼同样生效：只能顺着箭头游进去（被水带过去不受限）。
    if lv.tiles[nxt] in ARROW and (ARROW[lv.tiles[nxt]] + turn) % 4 != d:
        return None, "valve"
    if nxt not in water:
        return None, "dry"
    if lv.tiles[nxt] == "G":
        return None, "grate"
    hp = pearl or nxt == lv.pearl
    if nxt in boxes:
        dest = lv.step(nxt, d)
        if dest < 0 or dest in boxes or lv.tiles[dest] in "#GE":
            return None, "crate"
        if lv.tiles[dest] == "D" and not gate:
            return None, "crate"
        nb = tuple(sorted(dest if b == nxt else b for b in boxes))
        return fall(lv, (water, water.index(nxt), nb, turn, gate, hp))
    return (water, water.index(nxt), boxes, turn, gate, hp), None


def won(lv, st):
    water, rider, boxes, turn, gate, pearl = st
    if water[rider] != lv.exit:
        return False
    return not (lv.pearl >= 0 and not pearl)


def key(st):
    water, rider, boxes, turn, gate, pearl = st
    return (turn, boxes, gate, pearl, water[rider], tuple(sorted(water)))


def solve(lv, limit=3000000):
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


def turns_steps(route):
    return (route.count("4") + route.count("5"),
            sum(route.count(str(i)) for i in range(4)))


def depth_from(lv, st):
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
    except Exception as exc:
        print(f"\n--- {tag} ---\n   盘面错误: {exc}")
        return None
    route, states, err = solve(lv)
    if verbose:
        print(f"\n--- {tag} ---")
        for r in rows:
            print("   ", r)
    if route is None:
        print(f"   ❌ 无解 ({err}, 局面 {states})")
        return None
    t, s = turns_steps(route)
    start, _ = fall(lv, lv.start)
    first = {}
    for mv in range(6):
        nxt, why = apply(lv, start, mv)
        if nxt is None:
            first[NAMES[mv]] = "死:" + (why or "?")
        else:
            d = depth_from(lv, nxt)
            first[NAMES[mv]] = (d + 1) if d is not None else "死:死路"
    traps = [k for k, v in first.items() if v == "死:死路"]
    wins = [k for k, v in first.items() if v == s]
    print(f"   par {t} 转 / {s} 步   路线: {' '.join(NAMES[int(c)] for c in route)}")
    print(f"   局面 {states}   首步: " + "  ".join(f"{k}:{v}" for k, v in first.items()))
    print(f"   最短首步 {wins or '无'}   首步即死路 {traps or '无'}")
    return dict(turns=t, steps=s, route=route, states=states, first=first,
                traps=traps, wins=wins, lv=lv)


def play(rows, route=None):
    lv = Level("replay", rows)
    st, err = fall(lv, lv.start)
    if st is None:
        print("起点即死:", err)
        return

    def dump(s, tag=""):
        water, rider, boxes, turn, gate, pearl = s
        g = [list(r) for r in lv.rows]
        for i, c in enumerate(water):
            if c < 0:
                continue
            g[c // lv.w][c % lv.w] = "P" if i == rider else "~"
        print(f"  {tag}  turn={turn} gate={'开' if gate else '关'} pearl={pearl} 水={sum(1 for c in water if c >= 0)}")

    dump(st, "start")
    if not route:
        route, _, err = solve(lv)
        if route is None:
            print("  无解")
            return
    for ch in route:
        st, why = apply(lv, st, int(ch))
        if st is None:
            print("  死:", NAMES[int(ch)], why)
            return
        dump(st, NAMES[int(ch)])
    print("  won:", won(lv, st))


if __name__ == "__main__":
    CANDIDATES = {}
    want = sys.argv[1] if len(sys.argv) > 1 else None
    for k, rows in CANDIDATES.items():
        if want and not k.startswith(want):
            continue
        report(k, rows)

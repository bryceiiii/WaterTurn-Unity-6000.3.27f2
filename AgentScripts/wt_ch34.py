"""03/04 关（含栅栏 'G'）的盘面评估与搜索。

栅栏语义（对齐 Puzzle.cs / ApplyLethal）：
  水：穿过栅栏格继续下落
  鱼：所骑的水格若跨进栅栏格 -> 死亡（"grate"）
关键指标：grate_load_bearing —— 把 G 换成墙后最少转数是否上升，用来证明栅栏真的在承担谜题。
"""
import random
from collections import deque

from wt_solver import (Level, apply, fall, won, key, NAMES, solve, X, Y,
                       UP, RIGHT, DOWN, LEFT, TURN, TURNLEFT)
from wt_chapter1 import first_move_budget, turn_first_cost, reachable


def trace(rows, show=False):
    """返回 (转数, 步数, 路线, 逐步状态)，并可选打印。"""
    lv = Level("t", "", rows)
    route, seen, _ = solve(lv)
    if route is None:
        return None
    st, _ = fall(lv, lv.start)
    turns = route.count("4") + route.count("5")
    path = [st]
    for ch in route:
        st, why = apply(lv, st, int(ch))
        if st is None:
            return None
        path.append(st)
    if show:
        for i, s in enumerate(path):
            mv = "start" if i == 0 else NAMES[int(route[i - 1])]
            fish = divmod(s[0][s[1]], lv.w)
            pool = [divmod(c, lv.w) for j, c in enumerate(s[0]) if j != s[1]]
            print(f"    {mv:>5} 鱼{fish} 水{pool} turn={s[3]}")
    return turns, route.count("0") + route.count("1") + route.count("2") + route.count("3"), \
        "".join(NAMES[int(c)] for c in route), path


def grate_load_bearing(rows):
    """承重判定：把栅栏换成空地后解法是否变短。

    栅栏换成空地 = 水照样漏下去、但鱼不再死。若此时解法更短，
    说明玩家本来会走那条「穿过栅栏」的捷径，是栅栏把他逼上了绕路。
    """
    a = trace(rows)
    b = trace([r.replace("G", ".") for r in rows])
    if a is None:
        return "原盘无解"
    if b is None:
        return f"承重(换空地即无解，原 {a[0]} 转)"
    if b[0] < a[0]:
        return f"承重(换空地 {a[0]}->{b[0]} 转)"
    return f"装饰(换空地仍 {b[0]} 转)"


def settled(rows):
    """盘面是否为落定态（Begin 后水不再移动）。"""
    lv = Level("t", "", rows)
    st, err = fall(lv, lv.start)
    if st is None:
        return False
    return tuple(sorted(st[0])) == tuple(sorted(lv.start[0])) and st[0][st[1]] == lv.start[0][lv.start[1]]


def report(tag, rows, want=None, verbose=True):
    lv = Level(tag, "", rows)
    tr = trace(rows)
    if verbose:
        print(f"\n--- {tag} ---")
        for r in rows:
            print("   ", r)
    if tr is None:
        print("   无解")
        return None
    turns, steps, path, _ = tr
    fm = first_move_budget(lv)
    states, dead = reachable(lv)
    pure = turn_first_cost(lv)
    traps = [k for k, v in fm.items() if v is None and k in "↑→↓←"]
    ok_first = [k for k, v in fm.items() if v and v[0] <= turns]
    if verbose:
        print(f"   par {turns}转 / {steps}步   路线: {path}")
        print("   首步: " + "  ".join(f"{k}:{(v[0] if v else '死')}" for k, v in fm.items()))
        print(f"   纯转: {'死' if pure is None else f'{pure[0]}转/{pure[1]}步'}   死路首步: {traps or '无'}"
              f"   par内可行首步: {ok_first}")
        print(f"   栅栏: {grate_load_bearing(rows)}   状态{states}/死角{dead}  落定态={settled(rows)}")
        if want and (turns, steps) != want:
            print(f"   ⚠ 期望 {want}")
        trace(rows, show=True)
    return dict(turns=turns, steps=steps, path=path, pure=pure, first=fm, traps=traps,
                ok_first=ok_first, states=states, dead=dead, rows=rows)


# ---------------- 候选盘面 ----------------

CANDIDATES = {
    # 栅栏 = 水能漏下去、鱼不能漏。鱼必须先在别的格子上"搭车"，让水先漏过去。
    "03a": [
        "#######",
        "#...#E#",
        "#.#.#.#",
        "#.#G#.#",
        "#~P~..#",
        "#######",
    ],
    # 栅栏横排：整层筛网，鱼被挡在上面
    "03b": [
        "#######",
        "#....E#",
        "#..#..#",
        "#GG.GG#",
        "#~P~..#",
        "#######",
    ],
    # 栅栏做"单向门"：水漏到下层，鱼留在上层
    "03c": [
        "#######",
        "#..#.E#",
        "#~~#..#",
        "#GG#G.#",
        "#.P.#.#",
        "#######",
    ],
}


if __name__ == "__main__":
    for k, rows in CANDIDATES.items():
        report(k, rows)


# ---------------- 随机搜索 ----------------

def par_of(rows):
    t = trace(rows)
    return None if t is None else (t[0], t[1])


def search(want_turns, want_free, w=7, h=6, tries=8000, seed=1, water_rng=(3, 4),
           grate_rng=(1, 2), want=6, verbose=True):
    """找：有栅栏 par=want_turns 转；栅栏换空地后 par=want_free 转（即栅栏承重）。"""
    random.seed(seed)
    interior = [(x, y) for y in range(1, h - 1) for x in range(1, w - 1)]
    hits = []
    for _ in range(tries):
        g = [["#"] * w for _ in range(h)]
        dens = random.uniform(0.22, 0.40)
        for (x, y) in interior:
            if random.random() > dens:
                g[y][x] = "."
        free_cells = [(x, y) for (x, y) in interior if g[y][x] == "."]
        if len(free_cells) < water_rng[1] + 4:
            continue
        px, py = random.choice(free_cells)
        near = [(x, y) for (x, y) in free_cells
                if abs(x - px) + abs(y - py) <= 3 and (x, y) != (px, py)]
        k = random.randint(*water_rng)
        if len(near) < k:
            continue
        pool = random.sample(near, min(k, len(near)))
        g[py][px] = "P"
        for (x, y) in pool:
            g[y][x] = "~"
        rest = [(x, y) for (x, y) in free_cells if g[y][x] == "."]
        if not rest:
            continue
        ex, ey = random.choice(rest)
        g[ey][ex] = "E"
        # 栅栏：贴着某个水格的下方/上方（竖直流向上才有戏）
        cand = [(x, y) for (x, y) in interior if g[y][x] == "."
                and any((x + dx, y + dy) in pool or (x + dx, y + dy) == (px, py)
                        for dx, dy in ((0, 1), (0, -1), (1, 0), (-1, 0)))]
        if len(cand) < grate_rng[0]:
            continue
        for (x, y) in random.sample(cand, min(random.randint(*grate_rng), len(cand))):
            g[y][x] = "G"
        rows = ["".join(r) for r in g]
        if not settled(rows):
            continue
        a = par_of(rows)
        if a is None or a[0] != want_turns:
            continue
        b = par_of([r.replace("G", ".") for r in rows])
        if b is None or b[0] != want_free:
            continue
        lv = Level("t", "", rows)
        fm = first_move_budget(lv)
        ok = [k2 for k2, v in fm.items() if v and v[0] <= want_turns]
        if len(ok) != 1:
            continue
        pure = turn_first_cost(lv)
        if pure is not None and pure[0] < want_turns + 2:
            continue
        traps = [k2 for k2, v in fm.items() if v is None and k2 in "↑→↓←"]
        if not traps:
            continue
        hits.append((a, b, rows, ok, traps, pure))
        if len(hits) >= want:
            break
    if verbose:
        print(f"\n命中 {len(hits)} 个（par {want_turns} 转，去栅栏后 {want_free} 转）")
        for a, b, rows, ok, traps, pure in hits:
            print(f"\n  par{a[0]}转/{a[1]}步  唯一首步{ok}  踩栅栏死{traps}  纯转{pure}")
            for r in rows:
                print("     ", r)
    return hits


def settle(rows):
    """把盘面转成"落定态"：Begin() 会先 Fall 一次，玩家看到的是落定后的样子。"""
    lv = Level("t", "", rows)
    st, err = fall(lv, lv.start)
    if st is None:
        return None
    g = [list(r) for r in rows]
    for y in range(len(g)):
        for x in range(len(g[0])):
            if g[y][x] in "~P":
                g[y][x] = "."
    for i, c in enumerate(st[0]):
        y, x = divmod(c, lv.w)
        g[y][x] = "P" if i == st[1] else "~"
    return ["".join(r) for r in g]


def first_moves(rows):
    """首步逐项：返回 {动作: '步数' 或 '死:原因'}，原因区分 grate(踩栅栏)/dry(游不动)。"""
    from wt_solver import apply as _apply, fall as _fall
    lv = Level("t", "", rows)
    st, err = _fall(lv, lv.start)
    if st is None:
        return None
    out = {}
    for mv in range(6):
        nxt, why = _apply(lv, st, mv)
        if nxt is None:
            out[NAMES[mv]] = "死:" + (why or "?")
            continue
        # 最短剩余步数
        q, seen, found = deque([(nxt, 1)]), {key(nxt)}, None
        while q:
            s, d = q.popleft()
            if won(lv, s):
                found = d
                break
            for m2 in range(6):
                n2, _ = _apply(lv, s, m2)
                if n2 is not None and key(n2) not in seen:
                    seen.add(key(n2))
                    q.append((n2, d + 1))
        out[NAMES[mv]] = f"{found}" if found else "死:无路"
    return out


def ok_moves(rows):
    fm = first_moves(rows) or {}
    return {k: v for k, v in fm.items() if not v.startswith("死")}


def drains(rows):
    """回放最优解，统计"水穿过栅栏"的次数（教学可见性指标）。"""
    lv = Level("t", "", rows)
    route, seen, _ = solve(lv)
    if route is None:
        return -1
    st, _ = fall(lv, lv.start)
    total = 0
    for ch in route:
        mv = int(ch)
        nxt, why = apply(lv, st, mv)
        if nxt is None:
            return total
        if mv in (TURN, TURNLEFT):
            d = (6 - nxt[3]) % 4
            for i, c in enumerate(st[0]):
                x, y = c % lv.w, c // lv.w
                nx, ny = nxt[0][i] % lv.w, nxt[0][i] // lv.w
                while (x, y) != (nx, ny):
                    x, y = x + X[d], y + Y[d]
                    if lv.tiles[x + y * lv.w] == "G":
                        total += 1
                        break
        st = nxt
    return total

"""在给定骨架上随机加墙、挪出口、挪排水口，筛出 par 合适的盘面。

骨架决定"房间长相"（规整、可读），随机只决定"哪几格是墙/水/门"，
所以出来的盘面不会像噪声。
"""
import random
import sys
from collections import deque
import wt_ch78 as W
from wt_hunt2 import shortest

NAMES = W.NAMES


def mutate(base, rng, wall_tries=6, marks=None, marks_wanted=None, keep_marks=True):
    h, w = len(base), len(base[0])
    grid = [list(r) for r in base]
    water_cells = [(x, y) for y in range(h) for x in range(w) if grid[y][x] in 'P~']
    mark_cells = [(x, y) for y in range(h) for x in range(w) if grid[y][x] in 'v sD12X']
    fixed = set(water_cells) | set(mark_cells)

    # 随机把一些空地变成墙（成块）
    for _ in range(wall_tries):
        bw, bh = rng.choice([1, 1, 2, 2, 3]), rng.choice([1, 1, 2, 2])
        x, y = rng.randint(1, w - 2), rng.randint(1, h - 2)
        cells = [(x + i, y + j) for i in range(bw) for j in range(bh)
                 if 1 <= x + i < w - 1 and 1 <= y + j < h - 1]
        if any(c in fixed or grid[c[1]][c[0]] != '.' for c in cells):
            continue
        if any(grid[c[1]][c[0]] == 'E' for c in cells):
            continue
        for cx, cy in cells:
            grid[cy][cx] = '#'

    # 出口重摆
    for y in range(h):
        for x in range(w):
            if grid[y][x] == 'E':
                grid[y][x] = '.'
    free = [(x, y) for y in range(1, h - 1) for x in range(1, w - 1)
            if grid[y][x] == '.']
    if not free:
        return None
    # 只保留和水域连通、且不在水里的空地
    ex, ey = rng.choice(free)
    grid[ey][ex] = 'E'

    # 排水口 / 时效格重摆
    if marks:
        for y in range(h):
            for x in range(w):
                if grid[y][x] in marks:
                    grid[y][x] = '.'
        free = [(x, y) for x in range(1, w - 1) for y in range(1, h - 1)
                if grid[y][x] == '.']
        rng.shuffle(free)
        for tag, count in marks_wanted:
            for _ in range(count):
                if not free:
                    return None
                x, y = free.pop()
                grid[y][x] = tag
    return [''.join(r) for r in grid]


def connected(rows):
    h, w = len(rows), len(rows[0])
    start = next(((x, y) for y in range(h) for x in range(w) if rows[y][x] != '#'), None)
    if start is None:
        return False
    seen = {start}
    q = deque([start])
    total = 0
    for y in range(h):
        for x in range(w):
            if rows[y][x] != '#':
                total += 1
    while q:
        x, y = q.popleft()
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and rows[ny][nx] != '#' and (nx, ny) not in seen:
                seen.add((nx, ny))
                q.append((nx, ny))
    return len(seen) == total


def load_bearing(rows, kind):
    """把核心机制拿掉（v->./ s->.）后必须无解，否则这一关不承重。"""
    if kind == "07":                       # 箭头全变墙：必须绕不过去
        alt = [r.translate(str.maketrans("^v<>", "####")) for r in rows]
    else:                                  # 时效格变空地：门永远不开
        alt = [r.replace("s", ".") for r in rows]
    try:
        lv = W.Level("alt", alt)
    except Exception:
        return False
    route, states, err = W.solve(lv, limit=20000)
    return route is None


def evaluate(rows, want_turns, min_states=100, max_states=9000, want_wins=1, kind="07"):
    try:
        lv = W.Level("c", rows)
    except Exception:
        return None
    st, err = W.fall(lv, lv.start)
    if st is None:
        return "startdead"
    route, states, err = W.solve(lv, limit=max_states + 1)
    if route is None:
        return "unsolved" if err == "limit" else None
    t, s = W.turns_steps(route)
    if not (want_turns[0] <= t <= want_turns[1]) or s < 1:
        return None
    if not (min_states <= states <= max_states):
        return None
    wins, traps = [], []
    for mv in range(6):
        n2, why = W.apply(lv, st, mv)
        if n2 is None:
            continue
        d = shortest(lv, n2)
        if d is None:
            traps.append(NAMES[mv])
        elif d + 1 == len(route):
            wins.append(NAMES[mv])
    if len(wins) != want_wins or not traps:
        return None
    if not load_bearing(rows, kind):
        return None
    return dict(rows=rows, turns=t, steps=s, states=states, route=route,
                wins=wins, traps=traps, lv=lv, start=st)


def hunt(base, marks, want_turns, tries=5000, keep=6, seed0=0, label="cand", kind="07"):
    out, tally = [], {}
    for i in range(tries):
        rng = random.Random(seed0 + i)
        rows = mutate(base, rng, marks={t for t, _ in marks}, marks_wanted=marks)
        if rows is None:
            tally['nogrid'] = tally.get('nogrid', 0) + 1
            continue
        if not connected(rows):
            tally['disconnected'] = tally.get('disconnected', 0) + 1
            continue
        r = evaluate(rows, want_turns, kind=kind)
        if not isinstance(r, dict):
            tally[str(r)] = tally.get(str(r), 0) + 1
            continue
        out.append(r)
        print(f"[{len(out)}] {label} par {r['turns']}转/{r['steps']}步 局面{r['states']} "
              f"首步{r['wins']} 死路{len(r['traps'])} route={r['route']}")
        for row in rows:
            print("    " + row)
        if len(out) >= keep:
            break
    print("统计:", tally)
    return out


BASE_07 = [
    "#########",
    "#.......#",
    "#.......#",
    "#.......#",
    "#...~...#",
    "#...~...#",
    "#...~...#",
    "#...P...#",
    "#########",
]

BASE_08 = [
    "###########",
    "#....#....#",
    "#....#....#",
    "#.~.......#",
    "#.~..#..s.#",
    "#.P..#....#",
    "###########",
]

if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "07"
    if mode == "07":
        hunt(BASE_07, marks=[('^', 1), ('v', 1)], want_turns=(3, 5), tries=8000, label="07", kind="07")
    else:
        hunt(BASE_08, marks=[('s', 1), ('D', 1)], want_turns=(3, 5), tries=6000, label="08", kind="08")

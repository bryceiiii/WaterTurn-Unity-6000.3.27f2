"""用矩形墙块拼房间，再筛 07（排水）/ 08（时效闸门）的候选。

随机单格墙画出来是噪声；改成随机 1x2 / 2x1 / 2x2 的墙块，
盘面才像一张关卡。筛选口径与 wt_hunt 相同。
"""
import random
import sys
import wt_ch78 as W
from collections import deque

UP, RIGHT, DOWN, LEFT, TURN, TURNLEFT = 0, 1, 2, 3, 4, 5
NAMES = W.NAMES


def carve(w, h, blocks, rng, openness_guard=42):
    grid = [['#'] * w for _ in range(h)]
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            grid[y][x] = '.'
    placed = 0
    tries = 0
    while placed < blocks and tries < 200:
        tries += 1
        bw = rng.choice([1, 1, 2, 2, 3])
        bh = rng.choice([1, 1, 2, 2, 3])
        x = rng.randint(1, w - 1 - bw)
        y = rng.randint(1, h - 1 - bh)
        # 别贴边贴成一整块，也避免把房间封死
        cells = [(x + i, y + j) for i in range(bw) for j in range(bh)]
        if any(grid[cy][cx] == '#' for cx, cy in cells):
            continue
        for cx, cy in cells:
            grid[cy][cx] = '#'
        if floor_ratio(grid) < openness_guard / 100:
            for cx, cy in cells:
                grid[cy][cx] = '.'
            continue
        placed += 1
    return grid


def floor_ratio(grid):
    total = (len(grid) - 2) * (len(grid[0]) - 2)
    free = sum(row[1:-1].count('.') for row in grid[1:-1])
    return free / max(total, 1)


def reachable(grid, start):
    h, w = len(grid), len(grid[0])
    seen = {start}
    q = deque([start])
    while q:
        x, y = q.popleft()
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            nx, ny = x + dx, y + dy
            if 1 <= nx < w - 1 and 1 <= ny < h - 1 and grid[ny][nx] != '#' and (nx, ny) not in seen:
                seen.add((nx, ny))
                q.append((nx, ny))
    return seen


def build(w, h, blocks, water_len, marks, rng):
    grid = carve(w, h, blocks, rng)
    free = [(x, y) for y in range(1, h - 1) for x in range(1, w - 1) if grid[y][x] == '.']
    if len(free) < water_len + 6:
        return None
    # 连通性：所有空地必须互通
    if len(reachable(grid, free[0])) != len(free):
        return None

    # 水带：直线或 L 形
    for _ in range(60):
        x, y = rng.choice(free)
        cells = [(x, y)]
        d = rng.randrange(4)
        for step in range(water_len - 1):
            if step == water_len // 2 and rng.random() < 0.5:
                d = (d + rng.choice([1, 3])) % 4
            dx, dy = [(0, -1), (1, 0), (0, 1), (-1, 0)][d]
            nx, ny = cells[-1][0] + dx, cells[-1][1] + dy
            if not (1 <= nx < w - 1 and 1 <= ny < h - 1) or grid[ny][nx] != '.':
                break
            cells.append((nx, ny))
        if len(cells) == water_len:
            break
    else:
        return None

    rider = cells[0] if rng.random() < .5 else cells[-1]
    for (cx, cy) in cells:
        grid[cy][cx] = 'P' if (cx, cy) == rider else '~'

    left = [(x, y) for (x, y) in free if grid[y][x] == '.']
    if len(left) < 3 + len(marks):
        return None

    # 出口：不能贴着鱼
    far = [c for c in left if abs(c[0] - rider[0]) + abs(c[1] - rider[1]) >= 4]
    if not far:
        return None
    ex, ey = rng.choice(far)
    grid[ey][ex] = 'E'
    left = [c for c in left if c != (ex, ey)]

    rng.shuffle(left)
    for tag, count in marks:
        for _ in range(count):
            if not left:
                return None
            x, y = left.pop()
            grid[y][x] = tag
    return [''.join(r) for r in grid]


def shortest(lv, st, limit=300000):
    q, seen = deque([(st, 0)]), {W.key(st)}
    while q:
        s, d = q.popleft()
        if W.won(lv, s):
            return d
        if len(seen) > limit:
            return None
        for mv in range(6):
            n2, _ = W.apply(lv, s, mv)
            if n2 is not None and W.key(n2) not in seen:
                seen.add(W.key(n2))
                q.append((n2, d + 1))
    return None


def evaluate(rows, want_turns, min_states=100, max_states=8000, want_wins=1):
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
    if not (want_turns[0] <= t <= want_turns[1]):
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
    return dict(rows=rows, turns=t, steps=s, states=states, route=route,
                wins=wins, traps=traps, lv=lv)


def hunt(w, h, blocks, water_len, marks, want_turns, tries=4000, keep=5, seed0=0,
         min_states=100, max_states=8000):
    out, tally = [], {}
    for i in range(tries):
        rng = random.Random(seed0 + i)
        rows = build(w, h, blocks, water_len, marks, rng)
        if rows is None:
            tally['nogrid'] = tally.get('nogrid', 0) + 1
            continue
        r = evaluate(rows, want_turns, min_states, max_states)
        if not isinstance(r, dict):
            tally[str(r)] = tally.get(str(r), 0) + 1
            continue
        out.append(r)
        print(f"[{len(out)}] par {r['turns']}转/{r['steps']}步 局面{r['states']} "
              f"首步{r['wins']} 死路{len(r['traps'])} route={r['route']}")
        for row in rows:
            print("    " + row)
        if len(out) >= keep:
            break
    print("统计:", tally)
    return out


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "07"
    if mode == "07":
        hunt(7, 9, blocks=7, water_len=5, marks=[('v', 2)],
             want_turns=(4, 5), tries=4000)
    else:
        hunt(9, 7, blocks=8, water_len=4, marks=[('s', 1), ('D', 1)],
             want_turns=(3, 4), tries=4000)

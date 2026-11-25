"""随机搜索 07（排水塔）/ 08（回廊时效闸门）的候选盘面。

筛选口径：
  - 可解
  - par 转数 4-6（07）/ 3-5（08）
  - 局面数 60-4000（太大说明是搜索爆炸而不是推理）
  - 首步唯一（只有一步能走到 par）
  - 至少一步首步即死路
  - 水格剩余 3-6（排水后不能只剩一格，否则"减法"没手感）
"""
import random
import sys
from wt_ch78 import Level, solve, apply, fall, won, key, turns_steps, NAMES


def random_level(w, h, drain=2.5, water_len=None, openness=0.70, seed=None):
    rng = random.Random(seed)
    grid = [['#'] * w for _ in range(h)]
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            grid[y][x] = '.' if rng.random() < openness else '#'

    free = [(x, y) for y in range(1, h - 1) for x in range(1, w - 1) if grid[y][x] == '.']
    if len(free) < 20:
        return None

    # 连一条水带
    n = water_len or rng.randint(4, 6)
    for _ in range(40):
        x, y = rng.choice(free)
        cells = [(x, y)]
        d = rng.randrange(4)
        for _ in range(n - 1):
            dx, dy = [(0, -1), (1, 0), (0, 1), (-1, 0)][d]
            nx, ny = cells[-1][0] + dx, cells[-1][1] + dy
            if not (1 <= nx < w - 1 and 1 <= ny < h - 1) or grid[ny][nx] != '.':
                d = rng.randrange(4)
                dx, dy = [(0, -1), (1, 0), (0, 1), (-1, 0)][d]
                nx, ny = cells[-1][0] + dx, cells[-1][1] + dy
                if not (1 <= nx < w - 1 and 1 <= ny < h - 1) or grid[ny][nx] != '.':
                    break
            cells.append((nx, ny))
        if len(cells) >= 4:
            break
    else:
        return None

    rider = cells[rng.randrange(len(cells))]
    for (cx, cy) in cells:
        grid[cy][cx] = 'P' if (cx, cy) == rider else '~'

    # 出口：离鱼足够远的空地
    cand = [(x, y) for (x, y) in free
            if grid[y][x] == '.' and abs(x - rider[0]) + abs(y - rider[1]) >= 3]
    if not cand:
        return None
    ex, ey = rng.choice(cand)
    grid[ey][ex] = 'E'

    # 排水口 / 时效感应格
    cand = [(x, y) for (x, y) in free if grid[y][x] == '.']
    rng.shuffle(cand)
    kinds = []
    count = drain if isinstance(drain, int) else rng.randint(2, 3)
    for _ in range(count):
        if not cand:
            break
        x, y = cand.pop()
        grid[y][x] = 'v'
        kinds.append('v')
    return [''.join(r) for r in grid]


def evaluate(rows, want_turns=(4, 6), min_states=60, max_states=4000):
    try:
        lv = Level("cand", rows)
    except Exception:
        return None
    route, states, err = solve(lv, limit=max_states + 1)
    if route is None:
        return None
    t, s = turns_steps(route)
    if not (want_turns[0] <= t <= want_turns[1]) or s < 1:
        return None
    if not (min_states <= states <= max_states):
        return None
    start, _ = fall(lv, lv.start)
    if start is None:
        return None
    wins, traps = [], []
    for mv in range(6):
        nxt, why = apply(lv, start, mv)
        if nxt is None:
            continue
        d = shortest(lv, nxt, max_states)
        if d is None:
            traps.append(NAMES[mv])
        elif d + 1 == s:
            wins.append(NAMES[mv])
    if len(wins) != 1:
        return None
    if not traps:
        return None
    return dict(rows=rows, turns=t, steps=s, states=states, route=route,
                wins=wins, traps=traps, lv=lv)


def shortest(lv, st, limit=200000):
    from collections import deque
    q, seen = deque([(st, 0)]), {key(st)}
    while q:
        s, d = q.popleft()
        if won(lv, s):
            return d
        if len(seen) > limit:
            return None
        for mv in range(6):
            n2, _ = apply(lv, s, mv)
            if n2 is not None and key(n2) not in seen:
                seen.add(key(n2))
                q.append((n2, d + 1))
    return None


def hunt(w, h, drain, want_turns, tries=4000, keep=6, seed0=0):
    found = []
    for i in range(tries):
        rows = random_level(w, h, drain=drain, seed=seed0 + i)
        if rows is None:
            continue
        r = evaluate(rows, want_turns=want_turns)
        if r is None:
            continue
        found.append(r)
        print(f"[{len(found)}] par {r['turns']}转/{r['steps']}步 局面{r['states']} "
              f"首步{r['wins']} 死路{r['traps']}")
        for row in rows:
            print("    " + row)
        if len(found) >= keep:
            break
    return found


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "07"
    if mode == "07":
        hunt(7, 9, drain=2, want_turns=(4, 5), tries=3000)
    else:
        hunt(9, 9, drain=0, want_turns=(3, 5), tries=3000)

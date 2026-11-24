"""Refined search: water-rich boards where exactly one first move fits the budget."""
import random, heapq
from wt_solver import Level, apply, fall, key, won, NAMES, TURN, TURNLEFT


def min_rot_from(lv, start):
    pq = [(0, 0, start)]
    seen = {key(start): (0, 0)}
    while pq:
        r, s, st = heapq.heappop(pq)
        if won(lv, st):
            return r, s
        if seen.get(key(st), (9, 9)) < (r, s):
            continue
        for mv in range(6):
            nxt, _ = apply(lv, st, mv)
            if nxt is None:
                continue
            nr, ns = r + (1 if mv in (TURN, TURNLEFT) else 0), s + 1
            kk = key(nxt)
            if seen.get(kk, (9, 9)) <= (nr, ns):
                continue
            seen[kk] = (nr, ns)
            heapq.heappush(pq, (nr, ns, nxt))
    return None


def evaluate(rows):
    lv = Level("c", "", rows)
    start, err = fall(lv, lv.start)
    if start is None:
        return None
    res = min_rot_from(lv, start)
    if res is None:
        return None
    r0, s0 = res
    fms, legal = {}, 0
    for mv in range(6):
        nxt, _ = apply(lv, start, mv)
        if nxt is None:
            continue
        legal += 1
        rr = min_rot_from(lv, nxt)
        fms[NAMES[mv]] = None if rr is None else (rr[0] + (1 if mv in (TURN, TURNLEFT) else 0), rr[1] + 1)
    return r0, s0, fms, legal


def search(w, h, target_rot, min_steps, seed=0, tries=120000, want=8):
    random.seed(seed)
    px, py = 1, h - 2
    interior = [(x, y) for y in range(1, h - 1) for x in range(1, w - 1)]
    hits, seen_boards = [], set()
    for _ in range(tries):
        cells = [c for c in interior if c != (px, py)]
        ex, ey = random.choice(cells)
        grid = [["#"] * w for _ in range(h)]
        walls = 0
        for (x, y) in interior:
            if (x, y) in ((px, py), (ex, ey)):
                grid[y][x] = "."
            elif random.random() < 0.26:
                grid[y][x] = "#"
                walls += 1
            else:
                grid[y][x] = "."
        if not (2 <= walls <= 6):
            continue
        free_cells = [(x, y) for (x, y) in interior if grid[y][x] == "."]
        n_water = random.randint(3, 7)
        water = random.sample([c for c in free_cells if c != (px, py)], n_water)
        for (x, y) in water:
            grid[y][x] = "~"
        grid[py][px] = "P"
        grid[ey][ex] = "E"
        rows = tuple("".join(r) for r in grid)
        if rows in seen_boards:
            continue
        seen_boards.add(rows)
        ev = evaluate(list(rows))
        if ev is None:
            continue
        r0, s0, fms, legal = ev
        if r0 != target_rot or s0 < min_steps or legal < 2:
            continue
        good = [k for k, v in fms.items() if v is not None and v[0] <= target_rot]
        if len(good) != 1:
            continue
        bad_swim = [k for k, v in fms.items() if k in "↑→↓←" and (v is None or v[0] > target_rot)]
        score = len(water) * 2 + (6 if bad_swim else 0) + min(s0, 8) - len(good)
        hits.append((score, r0, s0, fms, list(rows)))
        hits.sort(key=lambda t: -t[0])
        hits = hits[:want]
    return hits


if __name__ == "__main__":
    for tag, w, h, tr, ms in (("L3 7x6 目标2转", 7, 6, 2, 4), ("L4 8x7 目标3转", 8, 7, 3, 5)):
        print(f"\n########## {tag} ##########")
        for score, r0, s0, fms, rows in search(w, h, tr, ms, seed=3 if tr == 2 else 5):
            print(f"\n[{r0}转/{s0}步] " + " ".join(f"{k}:{v[0] if v else '死'}" for k, v in fms.items()))
            for r in rows:
                print("   ", r)

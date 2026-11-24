"""Random search for boards that hold up under a rotation budget."""
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
            if seen.get(key(nxt), (9, 9)) <= (nr, ns):
                continue
            seen[key(nxt)] = (nr, ns)
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
    fms = {}
    for mv in range(6):
        nxt, _ = apply(lv, start, mv)
        if nxt is None:
            continue
        rr = min_rot_from(lv, nxt)
        if rr is None:
            fms[NAMES[mv]] = None
            continue
        fms[NAMES[mv]] = (rr[0] + (1 if mv in (TURN, TURNLEFT) else 0), rr[1] + 1)
    return r0, s0, fms


def search(w, h, target_rot, seed=0, tries=40000):
    random.seed(seed)
    px, py = 1, h - 2
    interior = [(x, y) for y in range(1, h - 1) for x in range(1, w - 1)]
    hits = []
    for _ in range(tries):
        cells = [c for c in interior if c != (px, py)]
        ex, ey = random.choice(cells)
        grid = [["#"] * w for _ in range(h)]
        walls = 0
        for (x, y) in interior:
            if (x, y) == (px, py) or (x, y) == (ex, ey):
                grid[y][x] = "."
            elif random.random() < 0.30:
                grid[y][x] = "#"
                walls += 1
            else:
                grid[y][x] = "."
        if walls > 7:
            continue
        free_cells = [(x, y) for (x, y) in interior if grid[y][x] == "."]
        k = random.randint(1, 5)
        water = random.sample([c for c in free_cells if c != (px, py)], k)
        for (x, y) in water:
            grid[y][x] = "~"
        grid[py][px] = "P"
        grid[ey][ex] = "E"
        rows = ["".join(r) for r in grid]
        ev = evaluate(rows)
        if ev is None:
            continue
        r0, s0, fms = ev
        if r0 != target_rot:
            continue
        good = [k for k, v in fms.items() if v is not None and v[0] <= target_rot]
        if len(good) != 1 or good[0] not in "↑→↓←":
            continue
        # rotating first should be clearly costly
        rot_cost = [v[0] for k, v in fms.items() if k in ("R", "Q") and v]
        if not rot_cost or min(rot_cost) < target_rot + 2:
            continue
        hits.append((r0, s0, fms, rows))
        if len(hits) >= 6:
            break
    return hits


if __name__ == "__main__":
    for tag, w, h, tr in (("L3 7x6 目标2转", 7, 6, 2), ("L4 8x7 目标3转", 8, 7, 3)):
        print(f"\n########## {tag} ##########")
        hits = search(w, h, tr, seed=7 if tr == 2 else 11)
        for r0, s0, fms, rows in hits:
            print(f"\n最少{r0}转/{s0}步  " + " ".join(f"{k}:{v[0] if v else '-'}" for k, v in fms.items()))
            for r in rows:
                print("   ", r)

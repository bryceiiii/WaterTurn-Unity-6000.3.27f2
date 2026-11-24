"""Search candidate boards for the reworked chapter 1, one level at a time."""
import random
import sys
import time

from wt_chapter1 import report


def scan(tag, w, h, target_par, want_traps, max_par_ways, tries, seed,
         water_min, water_max, wall_min, wall_max, steps_range, states_min=0,
         require_pure_dead=False, require_unique_first=False, min_pure_gap=2):
    random.seed(seed)
    px, py = 1, h - 2
    interior = [(x, y) for y in range(1, h - 1) for x in range(1, w - 1)]
    best = []
    for _ in range(tries):
        cells = [c for c in interior if c != (px, py)]
        ex, ey = random.choice(cells)
        grid = [["#"] * w for _ in range(h)]
        walls = 0
        for (x, y) in interior:
            if (x, y) in ((px, py), (ex, ey)):
                grid[y][x] = "."
            elif random.random() < 0.45:
                grid[y][x] = "#"
                walls += 1
            else:
                grid[y][x] = "."
        if not (wall_min <= walls <= wall_max):
            continue
        free = [(x, y) for (x, y) in interior if grid[y][x] == "."]
        k = random.randint(water_min, water_max)
        if len(free) - 1 < k:
            continue
        for (x, y) in random.sample([c for c in free if c != (px, py)], k):
            grid[y][x] = "~"
        grid[py][px] = "P"
        grid[ey][ex] = "E"
        rows = ["".join(r) for r in grid]
        try:
            m = report(tag, rows, par=target_par, verbose=False)
        except Exception:
            continue
        if not m or m["rot"] != target_par:
            continue
        if not (steps_range[0] <= m["steps"] <= steps_range[1]):
            continue
        if len(m["traps"]) < want_traps:
            continue
        if len(m["parWays"]) > max_par_ways:
            continue
        if require_pure_dead and m["pure"] is not None:
            continue
        if m["pure"] is not None and m["pure"][0] < target_par + min_pure_gap:
            continue
        if require_unique_first and len(m["parWays"]) != 1:
            continue
        if m["states"] < states_min:
            continue
        best.append(m)
        if len(best) >= 6:
            break
    print(f"\n##### {tag}: {len(best)} 命中")
    for m in best:
        print(f"  par {m['rot']}/{m['steps']}  乱转={m['pure']}  首步={m['parWays']}  死路={m['traps']}  状态{m['states']}")
        for r in m["rows"]:
            print("     ", r)
    return best


if __name__ == "__main__":
    which = sys.argv[1] if len(sys.argv) > 1 else "2"
    t0 = time.time()
    if which == "2":
        scan("L2", 7, 6, 2, 1, 2, 4000, 31, 3, 4, 2, 5, (3, 4))
    elif which == "3":
        scan("L3", 7, 6, 3, 1, 2, 5000, 37, 4, 6, 2, 6, (5, 6))
    elif which == "4":
        scan("L4", 8, 7, 4, 2, 1, 6000, 43, 4, 6, 3, 8, (5, 7), states_min=300,
             require_pure_dead=True, require_unique_first=True)
    print("elapsed", round(time.time() - t0, 1))

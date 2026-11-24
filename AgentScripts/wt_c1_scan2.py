"""Faster chapter-1 board search: cheap filters first, slow metrics only on hits."""
import random
import sys
import time

from wt_chapter1 import best_route, turn_first_cost, reachable
from wt_budget import first_move_budget
from wt_solver import Level, NAMES


def quick(rows):
    lv = Level("t", "", rows)
    br = best_route(lv)
    if not br:
        return None
    r, s, path = br
    tf = turn_first_cost(lv)
    fm = first_move_budget(lv)
    traps = [k for k, v in fm.items() if v is None and k in "↑→↓←"]
    parways = [k for k, v in fm.items() if v and v[0] <= r]
    return dict(rot=r, steps=s, path=path, pure=tf, traps=traps, parways=parways,
                first=fm, rows=rows)


def scan(tag, w, h, target, tries, seed, water_min, water_max, wall_min, wall_max,
         p_on_water, pure_mode, max_parways, min_traps, steps_range, want=5,
         states_min=0, show=5):
    random.seed(seed)
    interior = [(x, y) for y in range(1, h - 1) for x in range(1, w - 1)]
    hits = []
    t0 = time.time()
    for _ in range(tries):
        px, py = random.choice(interior)
        cells = [c for c in interior if c != (px, py)]
        ex, ey = random.choice(cells)
        grid = [["#"] * w for _ in range(h)]
        walls = 0
        for (x, y) in interior:
            if (x, y) in ((px, py), (ex, ey)):
                grid[y][x] = "."
            elif random.random() < 0.42:
                grid[y][x] = "#"
                walls += 1
            else:
                grid[y][x] = "."
        if not (wall_min <= walls <= wall_max):
            continue
        free = [(x, y) for (x, y) in interior if grid[y][x] == "."]
        k = random.randint(water_min, water_max)
        pool = [c for c in free if p_on_water or c != (px, py)]
        if len(pool) < k:
            continue
        water = random.sample(pool, k)
        for (x, y) in water:
            grid[y][x] = "~"
        grid[py][px] = "P"
        grid[ey][ex] = "E"
        rows = ["".join(r) for r in grid]
        try:
            m = quick(rows)
        except Exception:
            continue
        if not m or m["rot"] != target:
            continue
        if not (steps_range[0] <= m["steps"] <= steps_range[1]):
            continue
        if len(m["traps"]) < min_traps or len(m["parways"]) > max_parways:
            continue
        if pure_mode == "dead" and m["pure"] is not None:
            continue
        if pure_mode == "worse" and (m["pure"] is None or m["pure"][0] < target + 2):
            continue
        hits.append(m)
        if len(hits) >= want:
            break
    print(f"\n##### {tag}: {len(hits)} 命中  ({round(time.time()-t0,1)}s, {tries} tries)")
    for m in hits[:show]:
        states = -1
        if states_min:
            states, _ = reachable(Level("t", "", m["rows"]))
            if states < states_min:
                continue
        line = "".join(NAMES[int(c)] for c in m["path"])
        print(f"  par {m['rot']}/{m['steps']} 线[{line}] 乱转={m['pure']} 首步={m['parways']} 死路={m['traps']} 状态={states}")
        for r in m["rows"]:
            print("     ", r)
    return hits


if __name__ == "__main__":
    which = sys.argv[1] if len(sys.argv) > 1 else "2"
    if which == "2":
        scan("L2", 7, 6, 2, 1500, 71, 3, 4, 2, 5, True, "worse", 2, 1, (3, 4))
    elif which == "3":
        scan("L3", 7, 6, 3, 1500, 73, 4, 5, 2, 6, True, "worse", 2, 1, (5, 6))
    else:
        scan("L4", 8, 7, 4, 2000, 79, 4, 6, 3, 8, False, "dead", 1, 2, (6, 7))

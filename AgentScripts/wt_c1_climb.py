"""Local search around the shipped chapter-1 boards: keep the silhouette, fix the metrics."""
import random
import sys
import time

from wt_chapter1 import best_route, turn_first_cost
from wt_budget import first_move_budget
from wt_solver import Level, NAMES


def evaluate(rows):
    lv = Level("t", "", rows)
    br = best_route(lv)
    if not br:
        return None
    r, s, path = br
    tf = turn_first_cost(lv)
    fm = first_move_budget(lv)
    traps = [k for k, v in fm.items() if v is None and k in "↑→↓←"]
    parways = [k for k, v in fm.items() if v and v[0] <= r]
    return dict(rot=r, steps=s, path=path, pure=tf, traps=traps, parways=parways, rows=rows)


def mutate(rows, n=1):
    grid = [list(r) for r in rows]
    h, w = len(grid), len(grid[0])
    fixed = {(y, x) for y in range(h) for x in range(w) if grid[y][x] in "PE~"}
    for _ in range(n):
        for _try in range(50):
            y = random.randrange(1, h - 1)
            x = random.randrange(1, w - 1)
            if (y, x) in fixed:
                continue
            grid[y][x] = "." if grid[y][x] == "#" else "#"
            break
    return ["".join(r) for r in grid]


def climb(tag, seed_rows, target, tries=4000, seed=1, want=6, pure_floor=5):
    random.seed(seed)
    hits = []
    base = seed_rows
    for _ in range(tries):
        rows = mutate(base, random.choice([1, 1, 2]))
        m = evaluate(rows)
        if not m or m["rot"] != target:
            continue
        pure = 99 if m["pure"] is None else m["pure"][0]
        if pure < pure_floor:
            continue
        if not m["traps"]:
            continue
        if len(m["parways"]) > 2:
            continue
        hits.append(m)
        if len(hits) >= want:
            break
    print(f"\n##### {tag}: {len(hits)}")
    for m in hits:
        line = "".join(NAMES[int(c)] for c in m["path"])
        print(f"  par {m['rot']}/{m['steps']} 线[{line}] 乱转={m['pure']} 首步={m['parways']} 死路={m['traps']}")
        for r in m["rows"]:
            print("     ", r)
    return hits


if __name__ == "__main__":
    which = sys.argv[1] if len(sys.argv) > 1 else "3"
    t0 = time.time()
    if which == "3":
        climb("L3", ["#######", "#.#.#.#", "####.##", "#~~~..#", "#P~#E.#", "#######"], 3, 6000, 3, 5, 5)
    else:
        climb("L4", ["#######", "#....E#", "#.#...#", "#~~~#.#", "#P~#.##", "#######"], 3, 6000, 5, 5, 5)
    print("elapsed", round(time.time() - t0, 1))

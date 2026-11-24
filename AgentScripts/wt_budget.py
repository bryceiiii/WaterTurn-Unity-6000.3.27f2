"""Cost-optimal analysis: minimum rotations vs steps, for 'rotation budget' design."""
import heapq
from wt_solver import Level, apply, fall, key, won, NAMES, TURN, TURNLEFT


def min_rot(lv):
    """Dijkstra on (rotations, steps) lexicographic."""
    start, _ = fall(lv, lv.start)
    if start is None:
        return None
    pq = [(0, 0, start)]
    seen = {(key(start)): (0, 0)}
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


def first_move_budget(lv):
    """For each legal first move: min rotations needed from there (+1 if it is a turn)."""
    start, _ = fall(lv, lv.start)
    out = {}
    for mv in range(6):
        nxt, _ = apply(lv, start, mv)
        if nxt is None:
            out[NAMES[mv]] = None
            continue
        res = min_rot_from(lv, nxt)
        if res is None:
            out[NAMES[mv]] = None
            continue
        r, s = res
        out[NAMES[mv]] = (r + (1 if mv in (TURN, TURNLEFT) else 0), s + 1)
    return out


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


def show(tag, rows, budget=None):
    lv = Level(tag, "", rows)
    res = min_rot(lv)
    print(f"\n--- {tag} ---")
    for r in rows:
        print("   ", r)
    if res is None:
        print("   无解")
        return
    r, s = res
    print(f"   最少旋转 {r} 次 / 对应 {s} 步")
    fm = first_move_budget(lv)
    line = "  ".join(f"{k}:{'-' if v is None else f'{v[0]}转'}" for k, v in fm.items())
    print(f"   首步所需旋转: {line}")
    if budget is not None:
        ok = [k for k, v in fm.items() if v is not None and v[0] <= budget]
        print(f"   预算{budget}转内可行的首步: {ok}（其余会超预算 → 失败）")


if __name__ == "__main__":
    show("现 03", ["#######", "#.#.#.#", "####.##", "#~~~..#", "#P~#E.#", "#######"], 2)
    show("现 04", ["#######", "#....E#", "#.#...#", "#~~~#.#", "#P~#.##", "#######"], 3)
    show("候 03D 死胡同", ["#######", "#.E...#", "#.#.#.#", "#~~~#.#", "#P~#..#", "#######"], 2)
    show("候 03A 双水袋", ["#######", "#..E..#", "#..#..#", "#~~#~~#", "#P~~~.#", "#######"], 2)
    show("候 03B 隔墙换乘", ["#######", "#..E..#", "#.#.#.#", "#~#~#.#", "#P~#~.#", "#######"], 2)

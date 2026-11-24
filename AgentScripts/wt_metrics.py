"""Metrics for existing levels + candidate redesigns."""
from collections import deque
from wt_solver import (Level, apply, fall, key, won, pretty, TURN, TURNLEFT,
                       UP, RIGHT, DOWN, LEFT, NAMES)


def reachable_states(lv, moves):
    start, _ = fall(lv, lv.start)
    q, seen = deque([(start, 1)]), {key(start)}
    win_d = None
    while q:
        st, d = q.popleft()
        if won(lv, st):
            win_d = d
            break
        for mv in moves:
            nxt, _ = apply(lv, st, mv)
            if nxt is None or key(nxt) in seen:
                continue
            seen.add(key(nxt))
            q.append((nxt, d + 1))
    return win_d


def stalls(lv):
    """Count states from which the exit is unreachable (true dead ends)."""
    start, _ = fall(lv, lv.start)
    allm = range(6)
    # full reachability graph
    q, graph = deque([start]), {key(start): start}
    while q:
        st = q.popleft()
        for mv in allm:
            nxt, _ = apply(lv, st, mv)
            if nxt is None or key(nxt) in graph:
                continue
            graph[key(nxt)] = nxt
            q.append(nxt)
    good = {k for k, st in graph.items() if won(lv, st)}
    changed = True
    while changed:
        changed = False
        for k, st in graph.items():
            if k in good:
                continue
            for mv in allm:
                nxt, _ = apply(lv, st, mv)
                if nxt is not None and key(nxt) in good:
                    good.add(k)
                    changed = True
                    break
    return len(graph) - len(good), len(graph)


def first_moves(lv):
    start, _ = fall(lv, lv.start)
    out = {}
    for mv in range(6):
        nxt, why = apply(lv, start, mv)
        if nxt is None:
            out[NAMES[mv]] = "死" if why == "grate" else "非法"
            continue
        # shortest win from here, full moves
        q, seen, found = deque([(nxt, 1)]), {key(nxt)}, None
        while q:
            st, d = q.popleft()
            if won(lv, st):
                found = d
                break
            for m2 in range(6):
                n2, _ = apply(lv, st, m2)
                if n2 is not None and key(n2) not in seen:
                    seen.add(key(n2))
                    q.append((n2, d + 1))
        # rotation-only from here
        rot = reachable_states_from(nxt, [TURN, TURNLEFT])
        out[NAMES[mv]] = (found, rot)
    return out


def reachable_states_from(start, moves):
    q, seen, found = deque([(start, 0)]), {key(start)}, None
    while q:
        st, d = q.popleft()
        if won(lv_cur, st):
            found = d
            break
        for mv in moves:
            nxt, _ = apply(lv_cur, st, mv)
            if nxt is not None and key(nxt) not in seen:
                seen.add(key(nxt))
                q.append((nxt, d + 1))
    return found


def report(name, rows):
    global lv_cur
    lv = lv_cur = Level(name, "", rows)
    start, err = fall(lv, lv.start)
    if start is None:
        print(f"{name}: 起始即死/非法 ({err})")
        return
    full = reachable_states(lv, range(6))
    rot = reachable_states(lv, [TURN, TURNLEFT])
    st_n, total = stalls(lv)
    print(f"\n=== {name} ===")
    for r in rows:
        print("   ", r)
    print(f"  最短解(全动作)      : {full} 步")
    print(f"  只用旋转(Turn/CCW)  : {rot if rot else '无解 ← 好'}")
    print(f"  死局状态            : {st_n}/{total}")
    fm = first_moves(lv)
    print("  首步 → (最短步, 只转可行?) :")
    for k, v in fm.items():
        if isinstance(v, str):
            print(f"     {k}: {v}")
        else:
            print(f"     {k}: {v[0] if v[0] else '死局'} 步, 只转={v[1] if v[1] else '不行'}")


if __name__ == "__main__":
    report("03 换一股水", ["#######", "#.#.#.#", "####.##", "#~~~..#", "#P~#E.#", "#######"])
    report("04 沿岸折返", ["#######", "#....E#", "#.#...#", "#~~~#.#", "#P~#.##", "#######"])

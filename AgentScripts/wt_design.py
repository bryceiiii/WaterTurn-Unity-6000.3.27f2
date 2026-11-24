"""Design candidates for level 03 / 04, verified with the ported solver."""
from collections import deque
from wt_solver import Level, apply, fall, key, won, NAMES, TURN, TURNLEFT


def optimal(lv):
    start, err = fall(lv, lv.start)
    if start is None:
        return None, [], 0
    # BFS by depth, collect all optimal routes
    q = deque([(start, "")])
    seen = {key(start): 0}
    depth_found, routes = None, []
    while q:
        st, route = q.popleft()
        if depth_found is not None and len(route) > depth_found:
            break
        if won(lv, st):
            depth_found = len(route)
            routes.append(route)
            continue
        for mv in range(6):
            nxt, _ = apply(lv, st, mv)
            if nxt is None:
                continue
            k = key(nxt)
            nd = len(route) + 1
            if k in seen and seen[k] < nd:
                continue
            seen.setdefault(k, nd)
            seen[k] = min(seen[k], nd)
            q.append((nxt, route + str(mv)))
    return depth_found, routes, len(seen)


def rot_only(lv):
    start, _ = fall(lv, lv.start)
    q, seen = deque([(start, 0)]), {key(start)}
    while q:
        st, d = q.popleft()
        if won(lv, st):
            return d
        for mv in (TURN, TURNLEFT):
            nxt, _ = apply(lv, st, mv)
            if nxt is not None and key(nxt) not in seen:
                seen.add(key(nxt))
                q.append((nxt, d + 1))
    return None


def stall_count(lv):
    start, _ = fall(lv, lv.start)
    q, graph = deque([start]), {key(start): start}
    while q:
        st = q.popleft()
        for mv in range(6):
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
            for mv in range(6):
                nxt, _ = apply(lv, st, mv)
                if nxt is not None and key(nxt) in good:
                    good.add(k); changed = True; break
    return len(graph) - len(good), len(graph)


def show(tag, rows):
    lv = Level(tag, "", rows)
    steps, routes, states = optimal(lv)
    ro = rot_only(lv)
    st_n, tot = stall_count(lv)
    swims = [r for r in routes if any(int(c) < 4 for c in r)]
    print(f"\n--- {tag} ---")
    for r in rows:
        print("   ", r)
    if steps is None:
        print("   无解!")
        return
    print(f"   最短 {steps} 步 | 最优解 {len(routes)} 条 | 含游泳的 {len(swims)} 条 | 状态 {states}")
    print(f"   只转通关: {ro if ro else '无解 ✓'} | 死局 {st_n}/{tot}")
    demo = swims[0] if swims else routes[0]
    print(f"   示例: {' '.join(NAMES[int(c)] for c in demo)}")


CANDIDATES_03 = {
    "A 双水袋": ["#######", "#..E..#", "#..#..#", "#~~#~~#", "#P~~~.#", "#######"],
    "B 隔墙换乘": ["#######", "#..E..#", "#.#.#.#", "#~#~#.#", "#P~#~.#", "#######"],
    "C 梯级上岸": ["#######", "#....E#", "#.##..#", "#~#~#.#", "#P~~~##", "#######"],
    "D 死胡同": ["#######", "#.E...#", "#.#.#.#", "#~~~#.#", "#P~#..#", "#######"],
}

CANDIDATES_04 = {
    "A 沿岸": ["#######", "#E....#", "#.###.#", "#~~~#.#", "#P~##.#", "#######"],
    "B 折返槽": ["#######", "#E...#.", "#.##.#.", "#~~~#.#", "#P~#..#", "#######"],
    "C 双槽": ["#######", "#..E..#", "#.~~~.#", "#.#.#.#", "#P~#..#", "#######"],
}

if __name__ == "__main__":
    for k, v in list(CANDIDATES_03.items()) + list(CANDIDATES_04.items()):
        show(k, v)

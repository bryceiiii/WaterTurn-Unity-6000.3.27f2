"""Chapter 1 (levels 1-4) rework: metric helpers + candidate evaluation.

Metrics we care about for a teaching chapter:
  par      = minimum rotations (the lesson's unit of cost)
  steps    = moves in that optimal line
  route    = the optimal move string (for writing walkthroughs)
  pureTurn = rotations needed if the FIRST move must be a rotation
  traps    = legal first moves that leave no solution at all (dead ends)
  parWays  = first moves that still finish within par
"""
import sys
from wt_solver import Level, apply, fall, key, won, NAMES, TURN, TURNLEFT, pretty
from wt_budget import min_rot, min_rot_from, first_move_budget

X = {0: 0, 1: 1, 2: 0, 3: -1}
Y = {0: -1, 1: 0, 2: 1, 3: 0}


def best_route(lv):
    """Lexicographic (rotations, steps) Dijkstra that also returns the move string."""
    import heapq
    start, _ = fall(lv, lv.start)
    if start is None:
        return None
    pq = [(0, 0, start, "")]
    seen = {key(start): (0, 0)}
    while pq:
        r, s, st, path = heapq.heappop(pq)
        if won(lv, st):
            return r, s, path
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
            heapq.heappush(pq, (nr, ns, nxt, path + str(mv)))
    return None


def turn_first_cost(lv):
    """Cheapest solution whose first move is a rotation (what a 'just press space' player pays)."""
    start, _ = fall(lv, lv.start)
    best = None
    for mv in (TURN, TURNLEFT):
        nxt, _ = apply(lv, start, mv)
        if nxt is None:
            continue
        res = min_rot_from(lv, nxt)
        if res is None:
            continue
        cost = (res[0] + 1, res[1] + 1)
        if best is None or cost < best:
            best = cost
    return best


def reachable(lv):
    """BFS over legal states; returns (states, dead_states)."""
    start, _ = fall(lv, lv.start)
    if start is None:
        return 0, 0
    seen = {key(start)}
    queue = [start]
    dead = 0
    while queue:
        st = queue.pop()
        if won(lv, st):
            continue
        alive = False
        for mv in range(6):
            nxt, _ = apply(lv, st, mv)
            if nxt is None:
                continue
            if won(lv, nxt) or min_rot_from(lv, nxt) is not None:
                alive = True
            if key(nxt) not in seen:
                seen.add(key(nxt))
                queue.append(nxt)
        if not alive:
            dead += 1
    return len(seen), dead


def report(tag, rows, par=None, verbose=True):
    lv = Level(tag, "", rows)
    br = best_route(lv)
    if verbose:
        print(f"\n--- {tag} ---")
        for r in rows:
            print("   ", r)
    if br is None:
        if verbose:
            print("   无解")
        return None
    r, s, path = br
    tf = turn_first_cost(lv)
    fm = first_move_budget(lv)
    states, dead = reachable(lv)
    traps = [k for k, v in fm.items() if v is None and k in "↑→↓←"]
    parWays = [k for k, v in fm.items() if v and v[0] <= (par if par is not None else r)]
    if verbose:
        moves = "".join(NAMES[int(c)] for c in path)
        print(f"   par {r}转 / {s}步   最优线: {moves}")
        print("   首步: " + "  ".join(f"{k}:{(v[0] if v else '死')}" for k, v in fm.items()))
        print(f"   乱转首步: {'死' if tf is None else f'{tf[0]}转/{tf[1]}步'}"
              f"   死路首步: {traps or '无'}   par内首步: {parWays}")
        print(f"   状态 {states} / 死角 {dead}")
    return dict(rot=r, steps=s, path=path, pure=tf, first=fm, states=states, dead=dead,
                traps=traps, parWays=parWays, rows=rows)


if __name__ == "__main__":
    report("现 01", ["#####", "#~.E#", "#P###", "#####"])
    report("现 02", ["#######", "#...#.#", "#....##", "#..~~~#", "#.EP~##", "#######"])
    report("现 03", ["#######", "#.#.#.#", "####.##", "#~~~..#", "#P~#E.#", "#######"])
    report("现 04", ["#######", "#....E#", "#.#...#", "#~~~#.#", "#P~#.##", "#######"])

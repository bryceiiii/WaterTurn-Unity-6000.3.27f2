"""盘面作者工具：落定态归一化 + 逐步回放 + 指标。

用法：
    python wt_ch58_check.py 05        # 跑 CANDIDATES 里名字以 05 开头的盘面
"""
import sys
from wt_ch58 import Level, fall, apply, solve, report, NAMES


def settle(rows):
    """把盘面转成 Begin() 之后的样子（水先落一次），并清掉 P 标记。"""
    lv = Level("t", [r if "P" in r else r for r in rows])
    st, err = fall(lv, lv.start)
    if st is None:
        return None
    water, rider, boxes, turn, gate, pearl = st
    g = [list(r) for r in rows]
    for y in range(lv.h):
        for x in range(lv.w):
            if g[y][x] in "P~Bb":
                g[y][x] = "X" if g[y][x] == "b" else ("X" if (x + y * lv.w) in lv_targets(lv) else ".")
    for i, c in enumerate(water):
        g[c // lv.w][c % lv.w] = "P" if i == rider else "~"
    for b in boxes:
        y, x = divmod(b, lv.w)
        g[y][x] = "B" if g[y][x] != "X" else "b"
    return ["".join(r) for r in g]


def lv_targets(lv):
    return {p for p, t in enumerate(lv.tiles) if t in "Xb"}


def draw(rows, st=None, tag=""):
    lv = Level("t", rows)
    if st is None:
        st, _ = fall(lv, lv.start)
    water, rider, boxes, turn, gate, pearl = st
    g = [[c for c in r] for r in lv.rows]
    for y in range(lv.h):
        for x in range(lv.w):
            if g[y][x] in "P~":
                g[y][x] = "."
    for b in boxes:
        y, x = divmod(b, lv.w)
        g[y][x] = "b" if g[y][x] == "X" else "B"
    for i, c in enumerate(water):
        y, x = divmod(c, lv.w)
        g[y][x] = "P" if i == rider else "~"
    print(f"   [{tag}] turn={turn} gate={gate}")
    for r in g:
        print("      " + "".join(r))


def play(rows):
    lv = Level("t", rows)
    route, states = solve(lv)
    print("   par:", (route and (route.count("4") + route.count("5"),
                                 sum(route.count(str(i)) for i in range(4)))), "局面", states)
    if route is None:
        return None
    st, _ = fall(lv, lv.start)
    draw(rows, st, "start")
    for ch in route:
        st2, why = apply(lv, st, int(ch))
        if st2 is None:
            print("      !! 解析失败", NAMES[int(ch)], why)
            break
        st = st2
        draw(rows, st, NAMES[int(ch)])
    return route


if __name__ == "__main__":
    import wt_ch58
    want = sys.argv[1] if len(sys.argv) > 1 else None
    for k, rows in wt_ch58.CANDIDATES.items():
        if want and not k.startswith(want):
            continue
        print(f"\n===== {k} =====")
        for r in rows:
            print("   ", r)
        report(k, rows, verbose=False)
        play(rows)

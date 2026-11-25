"""把一张盘面在 4 个朝向下依次转动的落定结果画出来，用来手调关卡。

用法：
    python wt_show.py "#######" "#..E..#" ...     # 直接给行
    python wt_show.py                             # 跑 DEMO 里的盘面
"""
import sys
import wt_ch78 as W


def draw(lv, st, title=""):
    water, rider, boxes, turn, gate, pearl = st
    grid = [list(r) for r in lv.rows]
    for i, c in enumerate(water):
        if c < 0:
            continue
        x, y = c % lv.w, c // lv.w
        if grid[y][x] in "v":
            grid[y][x] = "!"
        else:
            grid[y][x] = "P" if i == rider else "~"
    # 已排空的洞
    for i, c in enumerate(water):
        if c >= 0:
            continue
    print(f"   {title}  turn={turn}  gate={'开' if gate else '关'}  pearl={pearl}")
    for row in grid:
        print("     " + "".join(row))


def walk(rows, turns=4):
    lv = W.Level("show", rows)
    st, err = W.fall(lv, lv.start)
    if st is None:
        print("起点即死:", err)
        return
    draw(lv, st, "start")
    for k in range(turns):
        st, why = W.apply(lv, st, W.TURN)
        print()
        if st is None:
            print(f"   R{k+1} 死: {why}")
            return
        draw(lv, st, f"R{k+1}")


DEMO = [
    "#########",
    "#.......#",
    "#..###..#",
    "#..#E#..#",
    "#..#.#..#",
    "#..~~~..#",
    "#..P....#",
    "#.v...v.#",
    "#########",
]

if __name__ == "__main__":
    rows = sys.argv[1:] or DEMO
    print("盘面:")
    for r in rows:
        print("   ", r)
    walk(rows)
    print()
    W.report("demo", rows)

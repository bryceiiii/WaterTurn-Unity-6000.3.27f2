"""Board visualiser: show the board after each of the four gravity directions."""
import sys
from wt_solver import Level, apply, fall, NAMES, TURN, key


def render(lv, st):
    grid = [[" " if c == "." else c for c in r] for r in lv.rows]
    for i, cell in enumerate(st[0]):
        y, x = divmod(cell, lv.w)
        grid[y][x] = "F" if i == st[1] else "~"
    return ["".join(r) for r in grid]


def show_rotations(tag, rows):
    lv = Level(tag, "", rows)
    st, err = fall(lv, lv.start)
    print(f"\n=== {tag} ===")
    print("  起点(已落定):")
    for r in render(lv, st):
        print("   ", r)
    cur = st
    for step in range(1, 5):
        cur, why = apply(lv, cur, TURN)
        if cur is None:
            print(f"  R x{step}: {why}")
            break
        print(f"  R x{step}:")
        for r in render(lv, cur):
            print("   ", r)
    # Also: where does each water cell sit at start?
    print("  水格坐标:", [divmod(c, lv.w)[::-1] for c in st[0]])


if __name__ == "__main__":
    for tag, rows in [
        ("A", ["#######", "#....E#", "#.#.#.#", "#~~#..#", "#P~#..#", "#######"]),
        ("B", ["#######", "#..E..#", "#.#.#.#", "#~~.##.", "#P~#..#", "#######"]),
        ("C", ["#######", "#....E#", "#..#..#", "#~~~#.#", "#P~#..#", "#######"]),
    ]:
        show_rotations(tag, rows)

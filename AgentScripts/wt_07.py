"""07 单向阀：候选盘面体检台。

正确语义（和画面一致，也和玩家直觉一致）：
  箭头钉在「房间坐标」里，房间转它跟着转；水只能顺着箭头流进去。
  因为水在每个朝向下只有一个流动方向，所以阀门实际是「只在箭头朝下那一档
  才通水」的单向门：
      ^ 水从下往上  只在 turn=2 通
      > 水从左往右  只在 turn=1 通
      v 水从上往下  只在 turn=0 通
      < 水从右往左  只在 turn=3 通
体检项：可解 / par / 局面数 / 阀门换墙 / 阀门换地板 / 可见局面里有没有水停在阀格。
"""
import sys
from collections import deque

sys.path.insert(0, ".")
import wt_ch78 as W


def arrow_ok(self, cell, d, turn):
    a = W.ARROW.get(self.tiles[cell])
    return a is None or a == d


W.Level.arrow_ok = arrow_ok


def audit(tag, rows, dump=True, swap=True):
    print("\n==== " + tag)
    for r in rows: print("   ", r)
    try:
        lv = W.Level(tag, rows)
    except Exception as exc:
        print("   盘面错误:", exc)
        return None
    route, states, err = W.solve(lv)
    if route is None:
        print(f"   ❌ 无解 ({err}) 局面 {states}")
        return None
    t, s = W.turns_steps(route)
    print(f"   par {t} 转 / {s} 步   路线 {' '.join(W.NAMES[int(c)] for c in route)}   局面 {states}")
    if swap:
        for nm, mp in (("阀门换墙", str.maketrans("^><v", "####")),
                       ("阀门换地板", str.maketrans("^><v", "...."))):
            b = [r.translate(mp) for r in rows]
            try:
                lv2 = W.Level(nm, b)
                rt2, s2, e2 = W.solve(lv2)
                print(f"   {nm}: {'可解 ' + ' '.join(W.NAMES[int(c)] for c in rt2) if rt2 is not None else '无解 ✓'}   局面 {s2}")
            except Exception as e:
                print("   ", nm, e)
    # 可见局面里水停在阀格的数量
    seen, bad = {W.key(lv.start)}, 0
    st, _ = W.fall(lv, lv.start)
    if st:
        seen, q = {W.key(st)}, deque([st])
        while q:
            s0 = q.popleft()
            if any(c >= 0 and lv.tiles[c] in W.ARROW for c in s0[0]):
                bad += 1
            for mv in range(6):
                n, _ = W.apply(lv, s0, mv)
                if n is None or W.key(n) in seen:
                    continue
                seen.add(W.key(n)); q.append(n)
        print(f"   可达局面 {len(seen)}   水停阀格 {bad}")
    if dump:
        W.play(rows)
    return lv


BOARDS = {}

if __name__ == "__main__":
    for name, rows in BOARDS.items():
        audit(name, rows)

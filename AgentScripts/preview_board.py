"""按 Unity 的绘制顺序把关卡合成成图，用于肉眼校验盘面读感。

wall 根节点 = rounded 深色块(size=cell+1)
Rock 子图   = wall_tile 砖纹（修复前固定 100px，修复后跟随父节点拉伸）
floor       = rounded 深色块(size=cell-2)
water/exit/fish 同场景尺寸比例
"""
import sys
from PIL import Image

ROOT = r"D:/UnityProjects/WaterTurn-Unity-6000.3.27f2/Assets"
ART = ROOT + "/Resources/Art"

CELL = 84.0          # 放大 1.5 倍便于观察
MARGIN = 0.0
RSCALE = 2.0


def load(p):
    return Image.open(p).convert("RGBA")


def rounded(color, size, radius_px=9 / 64.0):
    """用 rounded.png 的 alpha 做蒙版，复刻 Simple 模式拉伸的圆角块。"""
    base = load(ROOT + "/Art/UI/rounded.png")
    w = max(1, int(round(size)))
    img = base.resize((w, w), Image.LANCZOS)
    a = img.split()[-1]
    solid = Image.new("RGBA", (w, w), color + (255,))
    solid.putalpha(a)
    return solid


def grate(size):
    """复刻 Obstacle_Grate：半透明底板 + 3 根竖条。"""
    s = int(round(size))
    plate = rounded((101, 122, 140), s)
    a = plate.split()[-1].point(lambda v: int(v * 0.63))
    plate.putalpha(a)
    img = plate.copy()
    bar_w = max(2, int(s * 4 / 100.0))
    bar_h = int(s * 65 / 100.0)
    for dx in (-0.24, 0.0, 0.24):
        bx = int(s / 2 + dx * s - bar_w / 2)
        by = int((s - bar_h) / 2)
        bar = rounded((156, 189, 193), max(bar_w, bar_h))
        bar = bar.resize((bar_w, bar_h), Image.LANCZOS)
        img.alpha_composite(bar, (max(0, bx), max(0, by)))
    return img


def render(rows, rock_mode, cell_px=CELL):
    wall_tex = load(ART + "/Wall/wall_tile.png")
    water_tex = load(ART + "/Water/water_0.png")
    fish_tex = load(ART + "/Fish/swim_right_0.png")

    CELL = cell_px
    h, w = len(rows), len(rows[0])
    W = int(w * CELL + 2 * MARGIN)
    H = int(h * CELL + 2 * MARGIN)
    canvas = Image.new("RGBA", (W, H), (14, 23, 30, 255))

    def center(x, y, size):
        px = MARGIN + x * CELL + (CELL - size) / 2.0
        py = MARGIN + y * CELL + (CELL - size) / 2.0
        return int(round(px)), int(round(py))

    for y in range(h):
        for x in range(w):
            ch = rows[y][x]
            if ch == "#":
                base = rounded((32, 42, 52), CELL + 1)
                canvas.alpha_composite(base, center(x, y, CELL + 1))
                rock_size = 100 / 56.32 * CELL if rock_mode == "before" else CELL + 1
                rock = wall_tex.resize((int(round(rock_size)), int(round(rock_size))), Image.LANCZOS)
                canvas.alpha_composite(rock, center(x, y, rock_size))
            else:
                canvas.alpha_composite(rounded((16, 29, 38), CELL - 2), center(x, y, CELL - 2))
                if ch == "G":
                    gs = CELL * 0.75
                    canvas.alpha_composite(grate(gs), center(x, y, gs))
                if ch == "E":
                    canvas.alpha_composite(rounded((196, 148, 82), 43.15 / 56.32 * CELL), center(x, y, 43.15 / 56.32 * CELL))

    for y in range(h):
        for x in range(w):
            if rows[y][x] in "~P":
                s = 57.32 / 56.32 * CELL
                canvas.alpha_composite(water_tex.resize((int(s), int(s)), Image.LANCZOS), center(x, y, s))

    for y in range(h):
        for x in range(w):
            if rows[y][x] == "P":
                s = 37.62 / 56.32 * CELL
                canvas.alpha_composite(fish_tex.resize((int(s), int(s)), Image.LANCZOS), center(x, y, s))
    return canvas


def side_by_side(rows, out):
    a = render(rows, "before")
    b = render(rows, "after")
    gap = 24
    canvas = Image.new("RGBA", (a.width * 2 + gap, a.height), (8, 12, 16, 255))
    canvas.paste(a, (0, 0))
    canvas.paste(b, (a.width + gap, 0))
    canvas = canvas.resize((int(canvas.width / RSCALE), int(canvas.height / RSCALE)), Image.LANCZOS)
    canvas.save(out)
    return out


if __name__ == "__main__":
    rows = sys.argv[1:] if len(sys.argv) > 1 else [
        "#######", "#....##", "#....E#", "#.P.#.#", "#.~...#", "#######",
    ]
    print(side_by_side(rows, r"D:/UnityProjects/WaterTurn-Unity-6000.3.27f2/AgentScripts/preview_before_after.png"))

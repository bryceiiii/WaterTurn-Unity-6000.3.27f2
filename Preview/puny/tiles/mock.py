# -*- coding: utf-8 -*-
"""离线渲染 Level_06 的 9x7 棋盘，比较墙体瓦片方案。"""
import os
from PIL import Image, ImageDraw

ROOT = r"D:/UnityProjects/WaterTurn-Unity-6000.3.27f2"
ATLAS = os.path.join(ROOT, "Assets/Resources/PunyDungeon/punyworld-dungeon-tileset.png")
OUT = os.path.join(ROOT, "Preview/puny/tiles")
T = 16

atlas = Image.open(ATLAS).convert("RGBA")
_c = {}


def tile(name, cell):
    key = (name, cell)
    if key in _c:
        return _c[key]
    cc, rr = name[1:].split("_")
    img = atlas.crop((int(cc) * T, int(rr) * T, int(cc) * T + T, int(rr) * T + T))
    img = img.resize((cell, cell), Image.NEAREST)
    _c[key] = img
    return img


def tinted(name, hexcol, cell):
    key = ("t" + name + hexcol, cell)
    if key in _c:
        return _c[key]
    img = tile(name, cell).copy()
    col = tuple(int(hexcol.lstrip("#")[i:i + 2], 16) for i in (0, 2, 4))
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            px[x, y] = (r * col[0] // 255, g * col[1] // 255, b * col[2] // 255, a)
    _c[key] = img
    return img


ROOM = [
    "#########",
    "####0####",
    "#0##0#00#",
    "#0##0#00#",
    "#0##0#00#",
    "#0000000#",
    "#########",
]
WALL = [[ch == "#" for ch in row] for row in ROOM]
COLS, ROWS = 9, 7
WATER = set([(5, 1), (5, 2), (5, 3), (5, 4)])


def is_wall(c, r):
    if c < 0 or c >= COLS or r < 0 or r >= ROWS:
        return True if r >= ROWS or r < 0 else False
    return WALL[r][c]


def south_open(c, r):
    """南侧是走廊（或地图外）→ 需要露出墙面阴影"""
    if r == ROWS - 1:
        return True
    return not is_wall(c, r + 1)


FILL = {"S1": "T0_1", "S2": "T2_1", "S3": "T0_1"}


def pick(c, r, scheme):
    fill = FILL[scheme]
    # 四角
    if r == 0 and c == 0:
        return "T1_0"
    if r == 0 and c == COLS - 1:
        return "T3_0"
    if r == ROWS - 1 and c == 0:
        return "T1_2"
    if r == ROWS - 1 and c == COLS - 1:
        return "T3_2"
    if scheme == "S3" and (r == 0 or r == ROWS - 1):
        return "T2_3"
    if south_open(c, r):
        return "T2_3"
    return fill


def render(scheme, floor_tint, cell):
    img = Image.new("RGBA", (COLS * cell, ROWS * cell), (0, 0, 0, 0))
    for r in range(ROWS):
        for c in range(COLS):
            if WALL[r][c]:
                img.alpha_composite(tinted(pick(c, r, scheme), "FFFFFF", cell), (c * cell, r * cell))
            else:
                img.alpha_composite(tinted("T16_14", floor_tint, cell), (c * cell, r * cell))
    return img


def decorate(img, cell):
    s = cell / 46.0
    for (r, c) in WATER:
        w = Image.open(os.path.join(ROOT, "Assets/Resources/Art/Water/water_0.png")).convert("RGBA").resize((int(46.7 * s), int(46.7 * s)), Image.NEAREST)
        img.alpha_composite(w, (int(c * cell), int(r * cell)))
    for (r, c) in WATER:
        f = Image.open(os.path.join(ROOT, "Assets/Resources/Art/Water/foam_0.png")).convert("RGBA").resize((int(70 * s), int(70 * s)), Image.NEAREST)
        img.alpha_composite(f, (int(c * cell - 12 * s), int(r * cell - 12 * s)))
    crate = tile("T21_18", cell).resize((int(38 * s), int(38 * s)), Image.NEAREST)
    img.alpha_composite(crate, (int(2 * cell + 4 * s), int(5 * cell + 4 * s)))
    pl = Image.open(os.path.join(ROOT, "Assets/Resources/Art/Fish/swim_right_0.png")).convert("RGBA").resize((int(30 * s), int(30 * s)), Image.NEAREST)
    img.alpha_composite(pl, (int(1 * cell + 8 * s), int(5 * cell + 8 * s)))
    ex = Image.new("RGBA", (int(35 * s), int(35 * s)), (196, 148, 82, 255))
    ex.paste(Image.new("RGBA", (int(18 * s), int(18 * s)), (26, 34, 44, 255)), (int(9 * s), int(9 * s)))
    img.alpha_composite(ex, (int(4 * cell + 6 * s), int(1 * cell + 6 * s)))
    return img


if __name__ == "__main__":
    cell = 64
    plans = [("S1", "D9CFBD"), ("S2", "D9CFBD"), ("S3", "D9CFBD")]
    imgs = []
    for scheme, ft in plans:
        im = decorate(render(scheme, ft, cell), cell)
        canvas = Image.new("RGBA", (im.width, im.height + 24), (250, 250, 252, 255))
        canvas.alpha_composite(im, (0, 24))
        ImageDraw.Draw(canvas).text((6, 6), "%s  fill=%s" % (scheme, FILL[scheme]), fill=(20, 20, 24, 255))
        imgs.append(canvas)
    pad = 12
    W = sum(i.width for i in imgs) + pad * (len(imgs) + 1)
    H = max(i.height for i in imgs) + pad * 2
    sheet = Image.new("RGBA", (W, H), (34, 34, 38, 255))
    x = pad
    for i in imgs:
        sheet.alpha_composite(i, (x, pad))
        x += i.width + pad
    sheet.save(os.path.join(OUT, "mock_compare.png"))
    for (s, _), im in zip(plans, imgs):
        im.save(os.path.join(OUT, "mock_%s.png" % s))
    print("ok", sheet.size)

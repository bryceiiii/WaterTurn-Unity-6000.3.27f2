from PIL import Image, ImageDraw

SRC = r"D:/UnityProjects/WaterTurn-Unity-6000.3.27f2/Assets/Resources/PunyDungeon/punyworld-dungeon-tileset.png"
OUT = r"D:/UnityProjects/WaterTurn-Unity-6000.3.27f2/Preview/puny/tiles"
T = 16
img = Image.open(SRC).convert("RGBA")


def crop(c0, c1, r0, r1, Z, name):
    w = 40 + (c1 - c0) * T * Z
    h = 30 + (r1 - r0) * T * Z
    out = Image.new("RGBA", (w, h), (250, 250, 252, 255))
    reg = img.crop((c0 * T, r0 * T, c1 * T, r1 * T)).resize(((c1 - c0) * T * Z, (r1 - r0) * T * Z), Image.NEAREST)
    out.paste(reg, (40, 30), reg)
    d = ImageDraw.Draw(out)
    for c in range(c0, c1):
        d.text((40 + (c - c0) * T * Z + 3, 10), "c%d" % c, fill=(200, 30, 30, 255))
    for r in range(r0, r1):
        d.text((6, 30 + (r - r0) * T * Z + 3), "r%d" % r, fill=(200, 30, 30, 255))
    for c in range(c0, c1 + 1):
        x = 40 + (c - c0) * T * Z
        d.line([(x, 30), (x, 30 + (r1 - r0) * T * Z)], fill=(0, 0, 0, 90))
    for r in range(r0, r1 + 1):
        y = 30 + (r - r0) * T * Z
        d.line([(40, y), (40 + (c1 - c0) * T * Z, y)], fill=(0, 0, 0, 90))
    out.save(OUT + "/" + name)


crop(0, 4, 0, 6, 14, "zone_gray_wall.png")
crop(13, 22, 13, 20, 12, "zone_tan.png")
print("ok")

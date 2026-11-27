from PIL import Image, ImageDraw

SRC = r"D:/UnityProjects/WaterTurn-Unity-6000.3.27f2/Assets/Resources/PunyDungeon/punyworld-dungeon-tileset.png"
OUT = r"D:/UnityProjects/WaterTurn-Unity-6000.3.27f2/Preview/puny/tiles"
T = 16
img = Image.open(SRC).convert("RGBA")
cols, rows = img.width // T, img.height // T
Z = 7
LEFT = 34
TOP = 30

half = (0, 13), (13, 26)
for idx, (c0, c1) in enumerate(half):
    w = LEFT + (c1 - c0) * T * Z
    h = TOP + rows * T * Z
    out = Image.new("RGBA", (w, h), (28, 28, 32, 255))
    reg = img.crop((c0 * T, 0, c1 * T, rows * T)).resize(((c1 - c0) * T * Z, rows * T * Z), Image.NEAREST)
    out.paste(reg, (LEFT, TOP))
    d = ImageDraw.Draw(out)
    for c in range(c0, c1):
        d.text((LEFT + (c - c0) * T * Z + 2, 8), str(c), fill=(255, 210, 90, 255))
    for r in range(rows):
        d.text((4, TOP + r * T * Z + 2), str(r), fill=(255, 210, 90, 255))
    for c in range(c0, c1 + 1):
        x = LEFT + (c - c0) * T * Z
        d.line([(x, TOP), (x, TOP + rows * T * Z)], fill=(255, 255, 255, 45))
    for r in range(rows):
        y = TOP + r * T * Z
        d.line([(LEFT, y), (LEFT + (c1 - c0) * T * Z, y)], fill=(255, 255, 255, 45))
    # highlight used tiles
    used = [(16,16),(1,2),(16,14),(2,0),(2,3),(0,1),(3,2),(21,18)]
    for (c, r) in used:
        if c0 <= c < c1:
            x = LEFT + (c - c0) * T * Z
            y = TOP + r * T * Z
            d.rectangle([x + 1, y + 1, x + T * Z - 2, y + T * Z - 2], outline=(255, 70, 70, 255))
    out.save(OUT + "/atlas_%d.png" % idx)
print("ok")

from PIL import Image, ImageDraw
import os

SRC = r"D:/UnityProjects/WaterTurn-Unity-6000.3.27f2/Assets/Resources/PunyDungeon/punyworld-dungeon-tileset.png"
OUT = r"D:/UnityProjects/WaterTurn-Unity-6000.3.27f2/Preview/puny/tiles"
T = 16
img = Image.open(SRC).convert("RGBA")
cols, rows = img.width // T, img.height // T
print("grid", cols, rows)

def tile(c, r):
    return img.crop((c * T, r * T, c * T + T, r * T + T))

# 1) named tiles sheet
names = [(16,16),(1,2),(16,14),(2,0),(2,3),(0,1),(3,2),(21,18)]
Z = 8
pad = 40
W = len(names) * (T*Z + 12)
H = T*Z + pad
sheet = Image.new("RGBA", (W, H), (40,40,44,255))
d = ImageDraw.Draw(sheet)
for i,(c,r) in enumerate(names):
    t = tile(c,r).resize((T*Z, T*Z), Image.NEAREST)
    x = i*(T*Z+12)
    sheet.paste(t, (x, 0))
    d.text((x+4, T*Z+6), "T%d_%d" % (c,r), fill=(255,255,255,255))
    d.rectangle([x,0,x+T*Z-1,T*Z-1], outline=(90,90,96,255))
sheet.save(os.path.join(OUT, "used_tiles.png"))

# 2) region cols 0..5, rows 0..4 enlarged with grid labels
c0,c1,r0,r1 = 0,6,0,5
reg = img.crop((c0*T, r0*T, c1*T, r1*T))
Z2 = 6
reg = reg.resize(((c1-c0)*T*Z2, (r1-r0)*T*Z2), Image.NEAREST)
W2, H2 = reg.width + 40, reg.height + 28
out = Image.new("RGBA", (W2,H2), (30,30,34,255))
out.paste(reg, (40,24))
d2 = ImageDraw.Draw(out)
for c in range(c0,c1):
    d2.text((40+(c-c0)*T*Z2+2, 4), "%d" % c, fill=(255,220,120,255))
for r in range(r0,r1):
    d2.text((8, 24+(r-r0)*T*Z2+2), "%d" % r, fill=(255,220,120,255))
for c in range(c0,c1+1):
    d2.line([(40+(c-c0)*T*Z2,24),(40+(c-c0)*T*Z2,24+(r1-r0)*T*Z2)], fill=(255,255,255,60))
for r in range(r0,r1+1):
    d2.line([(40,24+(r-r0)*T*Z2),(40+(c1-c0)*T*Z2,24+(r-r0)*T*Z2)], fill=(255,255,255,60))
out.save(os.path.join(OUT, "region_topleft.png"))
print("done")

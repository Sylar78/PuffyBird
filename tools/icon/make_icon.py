"""Génère l'icône de l'application (1024 × 1024, PNG sans canal alpha, exigé par l'App Store).

Aucune dépendance : dessin par formes analytiques avec anticrénelage, écriture PNG à la main.
Usage : python3 tools/icon/make_icon.py [chemin de sortie]
"""
import math
import struct
import sys
import zlib

SIZE = 1024
OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/PuffyBird/Icons/AppIcon.png"


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


# Palette du jeu (Palette.cs)
SKY_TOP = hex_rgb("#3C9BDB")
SKY_LOW = hex_rgb("#BFEAF2")
SUN = hex_rgb("#FFF6D8")
GRASS_LIGHT = hex_rgb("#9CE659")
GRASS_DARK = hex_rgb("#73BF2E")
SAND = hex_rgb("#DED895")
SAND_EDGE = hex_rgb("#D7A84C")
PIPE = hex_rgb("#3FB6A8")
PIPE_SHADE = hex_rgb("#2B8C82")
PIPE_LIGHT = hex_rgb("#8FE0D2")
PIPE_RIM = hex_rgb("#F2C35B")
BODY = hex_rgb("#4EA6D8")
BELLY = hex_rgb("#BFE6F7")
SHADE = hex_rgb("#2C6FA0")
BEAK = hex_rgb("#F7A23B")
CHEEK = hex_rgb("#F59AA8")
OUTLINE = hex_rgb("#543847")
WHITE = (1.0, 1.0, 1.0)
PUPIL = hex_rgb("#1A1016")

img = [[0.0, 0.0, 0.0] for _ in range(SIZE * SIZE)]


def blend(x, y, color, a):
    if a <= 0.0:
        return
    p = img[y * SIZE + x]
    if a >= 1.0:
        p[0], p[1], p[2] = color
    else:
        p[0] += (color[0] - p[0]) * a
        p[1] += (color[1] - p[1]) * a
        p[2] += (color[2] - p[2]) * a


def cov(d):
    """Couverture d'un pixel à la distance signée d (négative = dedans)."""
    return min(1.0, max(0.0, 0.5 - d))


def ellipse(cx, cy, rx, ry, color, shade=None, rot=0.0):
    """Ellipse pleine ; shade(x, y) -> couleur optionnelle pour un dégradé."""
    c, s = math.cos(rot), math.sin(rot)
    r = max(rx, ry) + 2
    for y in range(max(0, int(cy - r)), min(SIZE, int(cy + r) + 1)):
        for x in range(max(0, int(cx - r)), min(SIZE, int(cx + r) + 1)):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            u, v = dx * c + dy * s, -dx * s + dy * c
            k = math.sqrt((u / rx) ** 2 + (v / ry) ** 2)
            a = cov((k - 1.0) * min(rx, ry))
            if a > 0.0:
                blend(x, y, shade(x, y) if shade else color, a)


def rect(x0, y0, x1, y1, color, shade=None):
    for y in range(max(0, int(y0) - 1), min(SIZE, int(y1) + 2)):
        for x in range(max(0, int(x0) - 1), min(SIZE, int(x1) + 2)):
            px, py = x + 0.5, y + 0.5
            d = max(x0 - px, px - x1, y0 - py, py - y1)
            a = cov(d)
            if a > 0.0:
                blend(x, y, shade(x, y) if shade else color, a)


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


# Ciel en dégradé
for y in range(SIZE):
    col = lerp(SKY_TOP, SKY_LOW, (y / (SIZE - 1)) ** 1.2)
    for x in range(SIZE):
        img[y * SIZE + x] = list(col)

# Soleil et halo
ellipse(210, 230, 170, 170, None, shade=lambda x, y: lerp(lerp(SKY_TOP, SKY_LOW, 0.3), SUN, 0.3))
ellipse(210, 230, 105, 105, SUN)

# Nuages
for cx, cy, r in ((600, 170, 70), (680, 150, 90), (770, 175, 65), (90, 470, 55), (150, 455, 70)):
    ellipse(cx, cy, r * 1.15, r, WHITE)


def pipe_shade(x0, x1):
    def f(x, y):
        t = (x - x0) / (x1 - x0)
        if t < 0.18:
            return lerp(PIPE, PIPE_LIGHT, 0.6)
        if t > 0.72:
            return PIPE_SHADE
        return PIPE
    return f


# Tuyaux jade à liseré doré (tuyau du haut et du bas, à droite)
BX0, BX1, CX0, CX1 = 760, 930, 735, 955
rect(BX0, -10, BX1, 250, None, pipe_shade(BX0, BX1))
rect(CX0, 250, CX1, 330, None, pipe_shade(CX0, CX1))
rect(CX0, 300, CX1, 316, PIPE_RIM)
rect(BX0, 700, BX1, 900, None, pipe_shade(BX0, BX1))
rect(CX0, 640, CX1, 720, None, pipe_shade(CX0, CX1))
rect(CX0, 654, CX1, 670, PIPE_RIM)

# Sol : gazon rayé puis sable
for y in range(870, SIZE):
    for x in range(SIZE):
        if y < 930:
            stripe = ((x + y) // 36) % 2 == 0
            img[y * SIZE + x] = list(GRASS_LIGHT if stripe else GRASS_DARK)
        elif y < 944:
            img[y * SIZE + x] = list(SAND_EDGE)
        else:
            img[y * SIZE + x] = list(SAND)

# Oiseau
BX, BY, RX, RY = 430, 520, 270, 235
OUT_W = 16
ellipse(BX, BY, RX + OUT_W, RY + OUT_W, OUTLINE)


def body_shade(x, y):
    t = min(1.0, max(0.0, (y - (BY - RY)) / (2 * RY)))
    return lerp(BODY, SHADE, max(0.0, t - 0.45) * 1.3)


ellipse(BX, BY, RX, RY, None, shade=body_shade)
ellipse(BX - 10, BY + 105, 190, 105, BELLY)
# Reflet
ellipse(BX - 110, BY - 140, 70, 38, lerp(BODY, WHITE, 0.55), rot=-0.5)
# Aile
ellipse(BX - 150, BY + 30, 125 + OUT_W, 78 + OUT_W, OUTLINE, rot=-0.25)
ellipse(BX - 150, BY + 30, 125, 78, lerp(BODY, WHITE, 0.35), rot=-0.25)
# Œil
ellipse(BX + 105, BY - 85, 88 + OUT_W, 92 + OUT_W, OUTLINE)
ellipse(BX + 105, BY - 85, 88, 92, WHITE)
ellipse(BX + 135, BY - 80, 38, 44, PUPIL)
ellipse(BX + 122, BY - 98, 12, 12, WHITE)
# Joue et petit bec pointu
ellipse(BX + 60, BY + 40, 52, 34, CHEEK)
ellipse(BX + 262, BY + 5, 72 + OUT_W, 42 + OUT_W, OUTLINE, rot=0.18)
ellipse(BX + 262, BY + 5, 72, 42, BEAK, rot=0.18)


def png(path, pixels):
    raw = bytearray()
    for y in range(SIZE):
        raw.append(0)
        for x in range(SIZE):
            r, g, b = pixels[y * SIZE + x]
            raw += bytes((int(r * 255 + 0.5), int(g * 255 + 0.5), int(b * 255 + 0.5)))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(bytes(raw), 9)))
        f.write(chunk(b"IEND", b""))


png(OUT, img)
print("icône écrite :", OUT)

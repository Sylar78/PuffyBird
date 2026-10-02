"""Génère l'icône de l'application, le phénix (1024 × 1024, PNG sans canal alpha, exigé par l'App Store).

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


# Palette du phénix (Palette.cs)
SKY_TOP = hex_rgb("#1B1446")
SKY_MID = hex_rgb("#5A2A7A")
SKY_LOW = hex_rgb("#FF8A4C")
HALO = hex_rgb("#FFC86B")
BODY = hex_rgb("#14261C")
OUTLINE = hex_rgb("#0B140F")
SHEEN = hex_rgb("#2F8F4E")
SHEEN_LIGHT = hex_rgb("#5FD08A")
GOLD = hex_rgb("#F2B53A")
GOLD_LIGHT = hex_rgb("#FFE07A")
TIP_A = hex_rgb("#E8399A")
TIP_B = hex_rgb("#9B4DFF")
FLAME_ROOT = hex_rgb("#E8340A")
FLAME_TIP = hex_rgb("#FF8C1A")
OCELLUS = hex_rgb("#FFB52E")
RING = hex_rgb("#22B8C8")
CORE = hex_rgb("#1A3C8C")
WISP = hex_rgb("#9FF3FF")
BEAK = hex_rgb("#F4D58A")
EYE = hex_rgb("#FFE07A")
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


def ellipse(cx, cy, rx, ry, color, shade=None, rot=0.0, alpha=1.0):
    """Ellipse pleine ; shade(x, y) -> couleur optionnelle pour un dégradé."""
    c, s = math.cos(rot), math.sin(rot)
    r = max(rx, ry) + 2
    for y in range(max(0, int(cy - r)), min(SIZE, int(cy + r) + 1)):
        for x in range(max(0, int(cx - r)), min(SIZE, int(cx + r) + 1)):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            u, v = dx * c + dy * s, -dx * s + dy * c
            k = math.sqrt((u / rx) ** 2 + (v / ry) ** 2)
            a = cov((k - 1.0) * min(rx, ry)) * alpha
            if a > 0.0:
                blend(x, y, shade(x, y) if shade else color, a)


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def segment(x0, y0, angle, start, end, width, color, outline=0):
    """Plume : ellipse allongée le long de angle (radians, y vers le bas), de start à end depuis (x0, y0)."""
    mid = (start + end) * 0.5
    cx, cy = x0 + math.cos(angle) * mid, y0 + math.sin(angle) * mid
    if outline:
        ellipse(cx, cy, (end - start) * 0.5 + outline, width + outline, OUTLINE, rot=angle)
    ellipse(cx, cy, (end - start) * 0.5, width, color, rot=angle)


def glow(cx, cy, radius, color, strength):
    for y in range(max(0, int(cy - radius * 2)), min(SIZE, int(cy + radius * 2))):
        for x in range(max(0, int(cx - radius * 2)), min(SIZE, int(cx + radius * 2))):
            d2 = ((x - cx) ** 2 + (y - cy) ** 2) / (radius * radius)
            blend(x, y, color, strength * math.exp(-d2))


# Ciel du crépuscule : nuit en haut, braise à l'horizon.
for y in range(SIZE):
    t = y / (SIZE - 1)
    col = lerp(SKY_TOP, SKY_MID, t / 0.55) if t < 0.55 else lerp(SKY_MID, SKY_LOW, (t - 0.55) / 0.45)
    for x in range(SIZE):
        img[y * SIZE + x] = list(col)

# Petites étoiles
rnd = 7
for i in range(40):
    rnd = (rnd * 1103515245 + 12345) & 0x7FFFFFFF
    sx = rnd % SIZE
    rnd = (rnd * 1103515245 + 12345) & 0x7FFFFFFF
    sy = rnd % 420
    ellipse(sx, sy, 3 + (i % 3), 3 + (i % 3), WHITE, alpha=0.5 + 0.1 * (i % 5))

# Halo de feu derrière l'oiseau
glow(560, 520, 360, HALO, 0.7)
glow(590, 500, 190, GOLD_LIGHT, 0.45)

# Repère : épaule, corps tourné vers la droite.
SX, SY = 580, 560

# Queue de feu : longues plumes vers le bas à gauche, terminées par un ocelle.
TAIL_X, TAIL_Y = SX - 140, SY + 110
for angle_deg, length, ocellus in ((160, 300, True), (174, 320, True), (146, 270, True), (188, 270, True), (134, 210, False), (200, 210, False)):
    a0 = math.radians(angle_deg)
    px, py = TAIL_X, TAIL_Y
    steps = 14
    for i in range(1, steps + 1):
        u = i / steps
        # La plume s'incurve doucement puis remonte au bout.
        a = a0 + 0.25 * u
        nx = TAIL_X + math.cos(a) * length * u
        ny = TAIL_Y + math.sin(a) * length * u + 70 * math.sin(u * math.pi * 0.9)
        seg_a = math.atan2(ny - py, nx - px)
        seg_len = math.hypot(nx - px, ny - py)
        w = (22 - 12 * u) * (1.0 if ocellus else 0.55)
        col = lerp(FLAME_ROOT, FLAME_TIP, u) if ocellus else WISP
        segment(px, py, seg_a, -4, seg_len + 4, w, col)
        px, py, last = nx, ny, seg_a
    if ocellus:
        segment(px, py, last, -14, 66, 28, OCELLUS, outline=5)
        segment(px, py, last, 2, 50, 18, RING)
        segment(px, py, last, 14, 40, 8, CORE)

# Aile lointaine (plus sombre), levée derrière le corps.
for k in range(8):
    t = k / 7
    a = math.radians(-155 + 65 * t)
    length = 290 - 60 * t
    rx, ry = SX + 30, SY - 20
    segment(rx, ry, a, 0, length * 0.55, 20, lerp(BODY, SHEEN, 0.3), outline=5)
    segment(rx, ry, a, length * 0.35, length * 0.9, 22, lerp(TIP_B if k % 2 else TIP_A, BODY, 0.35), outline=5)
    segment(rx, ry, a, length * 0.8, length, 18, lerp(GOLD, BODY, 0.3))

# Corps
ellipse(SX, SY + 60, 175 + 10, 105 + 10, OUTLINE, rot=-0.45)
ellipse(SX, SY + 60, 175, 105, None, rot=-0.45,
        shade=lambda x, y: lerp(SHEEN, BODY, min(1.0, max(0.0, (y - 420) / 180))))
ellipse(SX + 60, SY + 50, 95, 60, lerp(SHEEN, SHEEN_LIGHT, 0.3), rot=-0.6)

# Aile proche : grandes rémiges levées, sombres, rose et violet lumineux, bout doré.
WX, WY = SX - 10, SY + 10
for k in range(9):
    t = k / 8
    a = math.radians(-172 + 78 * t)
    length = 360 - 100 * t
    segment(WX, WY, a, 0, length * 0.5, 24, BODY, outline=6)
    segment(WX, WY, a, length * 0.3, length * 0.9, 27, TIP_A if k % 2 == 0 else TIP_B, outline=6)
    segment(WX, WY, a, length * 0.78, length, 22, GOLD, outline=5)
ellipse(WX, WY, 70 + 8, 52 + 8, OUTLINE)
ellipse(WX, WY, 70, 52, SHEEN)


# Cou et tête
ellipse(SX + 150, SY - 60, 52 + 10, 105 + 10, OUTLINE, rot=0.55)
ellipse(SX + 150, SY - 60, 52, 105, SHEEN, rot=0.55)
ellipse(SX + 205, SY - 150, 72 + 10, 70 + 10, OUTLINE)
ellipse(SX + 205, SY - 150, 72, 70, None,
        shade=lambda x, y: lerp(SHEEN_LIGHT, SHEEN, min(1.0, max(0.0, (y - (SY - 220)) / 140))))

# Collier de perles d'or à la base du cou, en travers du cou.
for k in range(7):
    t = (k - 3) / 3
    ellipse(SX + 109 + 0.85 * 50 * t, SY + 7 + 0.52 * 50 * t + 8 * (1 - t * t), 13, 13, GOLD_LIGHT if k % 2 else GOLD)

# Crête : trois flammèches vers l'arrière
for k, (dx, ang, length, col) in enumerate(((0, -2.1, 120, GOLD), (-25, -2.45, 140, TIP_A), (-50, -2.8, 110, GOLD))):
    segment(SX + 190 + dx, SY - 205, ang, 0, length, 20 - k * 2, col, outline=6)

# Œil doré et bec fin crochu
ellipse(SX + 230, SY - 165, 24, 24, EYE)
ellipse(SX + 238, SY - 165, 12, 13, PUPIL)
ellipse(SX + 232, SY - 172, 5, 5, WHITE)
segment(SX + 262, SY - 140, 0.45, -6, 90, 18, BEAK, outline=6)


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

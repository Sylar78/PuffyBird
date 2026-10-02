#!/usr/bin/env python3
"""Génère les 9 icônes de succès (512 × 512) pour Game Center et Play Games.

Usage : python3 tools/achievements/make_icons.py [dossier de sortie]
Nécessite Pillow (pip install pillow). Les fichiers portent la clé du succès : score10.png, star.png...
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

SIZE = 1024  # dessiné en grand puis réduit à 512 pour des bords lisses
GOLD = (243, 194, 43)


def font(px):
    try:
        return ImageFont.load_default(size=px)
    except TypeError:  # Pillow ancien : police bitmap fixe
        return ImageFont.load_default()


def disc(d, cx, cy, r, fill, outline=None, width=0):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=fill, outline=outline, width=width)


def centered(d, text, cx, cy, px, fill, stroke=None):
    f = font(px)
    d.text((cx, cy), text, font=f, fill=fill, anchor="mm", stroke_width=px // 16 if stroke else 0, stroke_fill=stroke)


def star(d, cx, cy, r, fill):
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rad = r if i % 2 == 0 else r * 0.42
        pts.append((cx + rad * math.cos(a), cy + rad * math.sin(a)))
    d.polygon(pts, fill=fill)


def flame(d, cx, cy, h, fill):
    w = h * 0.62
    pts = [(cx, cy - h / 2), (cx + w / 2, cy + h * 0.08), (cx + w * 0.38, cy + h / 2 - h * 0.1), (cx, cy + h / 2),
           (cx - w * 0.38, cy + h / 2 - h * 0.1), (cx - w / 2, cy + h * 0.08)]
    d.polygon(pts, fill=fill)


def base(bg_top, bg_bottom):
    img = Image.new("RGB", (SIZE, SIZE))
    px = img.load()
    for y in range(SIZE):
        t = y / (SIZE - 1)
        c = tuple(int(bg_top[i] + (bg_bottom[i] - bg_top[i]) * t) for i in range(3))
        for x in range(SIZE):
            px[x, y] = c
    return img


def medal(number, rim, face, text):
    img = base((38, 70, 120), (18, 30, 64))
    d = ImageDraw.Draw(img)
    c = SIZE // 2
    disc(d, c, c, 400, rim)
    disc(d, c, c, 340, face)
    disc(d, c, c, 300, rim)
    disc(d, c, c, 284, face)
    centered(d, number, c, c + 10, 300, text, stroke=(0, 0, 0))
    return img


def make(key):
    c = SIZE // 2
    if key == "score10":
        return medal("10", (150, 90, 45), (208, 135, 75), (255, 240, 215))
    if key == "score20":
        return medal("20", (130, 130, 140), (199, 199, 199), (255, 255, 255))
    if key == "score30":
        return medal("30", (190, 140, 20), (243, 194, 43), (255, 250, 220))
    if key == "score40":
        return medal("40", (110, 160, 190), (232, 244, 248), (60, 100, 140))
    img = base((38, 70, 120), (18, 30, 64))
    d = ImageDraw.Draw(img)
    if key == "star":
        for i, col in enumerate([(255, 70, 70), (255, 160, 40), (255, 230, 60), (70, 200, 90), (60, 150, 255), (150, 90, 255)]):
            disc(d, c, c, 430 - i * 22, col)
        disc(d, c, c, 296, (18, 30, 64))
        star(d, c, c + 10, 250, GOLD)
    elif key == "closecalls":
        disc(d, c, c, 420, (24, 40, 80), outline=(143, 230, 255), width=18)
        for i in range(12):
            a = i * math.pi / 6
            d.line((c + 240 * math.cos(a), c + 240 * math.sin(a), c + 380 * math.cos(a), c + 380 * math.sin(a)), fill=(255, 255, 255) if i % 2 else (143, 230, 255), width=26)
        disc(d, c, c, 190, (255, 255, 255))
        centered(d, "x5", c, c + 6, 210, (18, 30, 64))
    elif key in ("streak7", "streak30"):
        disc(d, c, c, 420, (60, 28, 12))
        flame(d, c, c - 20, 640, (255, 106, 0))
        flame(d, c, c + 20, 440, (255, 190, 50))
        centered(d, "7" if key == "streak7" else "30", c, c + 120, 250, (255, 255, 255), stroke=(120, 40, 0))
    else:  # daily
        disc(d, c, c, 420, (232, 137, 43))
        for i in range(12):
            a = i * math.pi / 6
            d.line((c + 270 * math.cos(a), c + 270 * math.sin(a), c + 360 * math.cos(a), c + 360 * math.sin(a)), fill=(255, 244, 200), width=30)
        disc(d, c, c, 220, (255, 244, 200))
        star(d, c, c + 8, 150, (232, 137, 43))
    return img


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "succes"
    os.makedirs(out, exist_ok=True)
    for key in ("score10", "score20", "score30", "score40", "star", "closecalls", "streak7", "streak30", "daily"):
        make(key).resize((512, 512), Image.LANCZOS).save(os.path.join(out, key + ".png"))
        print(key)


if __name__ == "__main__":
    main()

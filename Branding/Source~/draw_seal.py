"""Pixel-art wax seal in the anvil's style: dark outline, red wax lit from the top left, the anvil pressed in."""
import math, struct, sys, zlib
W = H = 27
C = 13  # centre pixel
OUT = (23, 20, 35)
DEEP, DARK, MID, LIGHT, SHINE = (92, 16, 30), (146, 28, 40), (196, 48, 56), (232, 92, 78), (255, 170, 140)
def radius(a):          # wax spreads unevenly: seven soft lobes
    return 11.7 + 0.55 * math.cos(5 * a + 0.9) + 0.3 * math.cos(9 * a + 2.0)
def wax(x, y):
    dx, dy = x - C, y - C
    return math.hypot(dx, dy) <= radius(math.atan2(dy, dx))
ANVIL = ["...##########",
         "#############",
         ".##########..",
         "....#####....",
         ".....###.....",
         "....#####....",
         "...#######...",
         "...##...##..."]
AX, AY = C - 6, C - 4
def anvil(x, y):
    j, i = x - AX, y - AY
    return 0 <= i < len(ANVIL) and 0 <= j < len(ANVIL[0]) and ANVIL[i][j] == "#"
px = []
for y in range(H):
    row = []
    for x in range(W):
        if wax(x, y):
            dx, dy = x - C, y - C
            r = math.hypot(dx, dy)
            lit = -(dx + dy) / (r * 1.414) if r else 0   # +1 facing the light (top left)
            if r <= 8.3:                                  # the pressed face, the anvil standing out of it
                c = MID
                if anvil(x, y):
                    c = SHINE if not anvil(x - 1, y) or not anvil(x, y - 1) else LIGHT
                elif anvil(x - 1, y) or anvil(x, y - 1) or anvil(x - 1, y - 1):
                    c = DEEP                                  # its shadow, down and right
            elif r <= 9.4:                                # the groove round the face
                c = DEEP if lit > -0.2 else DARK
            else:                                         # the raised rim of wax
                c = LIGHT if lit > 0.3 else DARK if lit < -0.3 else MID
                if lit < 0.3 and not (wax(x + 1, y) and wax(x, y + 1)):
                    c = DEEP
            row.append(c + (255,))
        elif any(wax(x + a, y + b) for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            row.append(OUT + (255,))
        else:
            row.append((0, 0, 0, 0))
    px.append(row)
for x, y in ((5, 7), (6, 6), (7, 5)):                     # a glint on the rim
    if wax(x, y): px[y][x] = SHINE + (255,)
raw = b"".join(b"\0" + bytes(v for p in row for v in p) for row in px)
chunk = lambda t, d: struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d))
open(sys.argv[1], "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 6, 0, 0, 0))
                              + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))

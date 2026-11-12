"""Pixel-art key in the anvil's palette: black outline, steel greys lit from the top left."""
import struct, sys, zlib
W, H = 34, 17
CX, CY = 8, 8  # bow centre (pixel)
def body(x, y):
    d2 = (x - CX) ** 2 + (y - CY) ** 2
    if 2.6 ** 2 < d2 <= 6.6 ** 2: return True                 # bow ring
    if 14 <= x <= 16 and CY - 2 <= y <= CY + 2: return True    # collar
    if 14 <= x <= 31 and CY - 1 <= y <= CY + 1: return True    # shaft
    if 23 <= x <= 25 and CY + 2 <= y <= CY + 5: return True    # first bit
    if 28 <= x <= 30 and CY + 2 <= y <= CY + 4: return True    # second bit
    return False
inside = lambda x, y: 0 <= x < W and 0 <= y < H and body(x, y)
OUT, LIGHT, MID, DARK, SHINE = (10, 10, 12), (176, 176, 184), (118, 118, 126), (66, 66, 72), (228, 228, 234)
px = []
for y in range(H):
    row = []
    for x in range(W):
        if inside(x, y):
            if not inside(x, y - 1) or not inside(x - 1, y): c = LIGHT
            elif not inside(x, y + 1) or not inside(x + 1, y): c = DARK
            else: c = MID
            row.append(c + (255,))
        elif any(inside(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            row.append(OUT + (255,))
        else:
            row.append((0, 0, 0, 0))
    px.append(row)
for x, y in ((5, 3), (4, 4), (3, 5), (18, 7)):  # glints on the bow and shaft
    if inside(x, y): px[y][x] = SHINE + (255,)
raw = b"".join(b"\0" + bytes(v for p in row for v in p) for row in px)
chunk = lambda t, d: struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d))
png = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 6, 0, 0, 0))
       + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
open(sys.argv[1], "wb").write(png)

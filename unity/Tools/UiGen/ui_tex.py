"""Draws the HUD's wood and coral-stone textures (Resources/IslandSim/UI/ui_*.png).

Maldivian joinery and coral-stone carving, kept clean: a tileable teak grain for the bars, a pale honey grain for panel
headers, a carved coral-stone frieze (the lozenge-and-dot chain cut round old mosque walls such as Hukuru Miskiy) that
replaces the lacquer bands, and a 9-sliced panel frame of teak with a honey inlay line and brass studs at the corners (with a pale
coral-stone field for panels, or a teak board for the build bar).
Everything is drawn at 2x and shown at half size (-unity-slice-scale / background-size), so it stays sharp on HiDPI.

    python3 ui_tex.py            # writes into ../../Assets/IslandSim/Resources/IslandSim/UI
"""
import os, numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../Assets/IslandSim/Resources/IslandSim/UI")
rng = np.random.default_rng(7)


def periodic_noise(n, octaves=((2, 1.0), (3, .6), (5, .35), (9, .2), (17, .1))):
    """Smooth noise that tiles over an n x n square (a sum of integer-frequency waves with random phases)."""
    y, x = np.mgrid[0:n, 0:n] / n
    v = np.zeros((n, n))
    for f, a in octaves:
        for _ in range(3):
            fx, fy = rng.integers(-f, f + 1, 2)
            if fx == 0 and fy == 0: fx = f
            v += a * np.sin(2 * np.pi * (fx * x + fy * y) + rng.uniform(0, 2 * np.pi))
    return v / np.abs(v).max()


def streaks(n, fy_max, fx_max, count, amp=1.0):
    """Tileable noise stretched along x: high frequencies across the grain, low ones along it."""
    y, x = np.mgrid[0:n, 0:n] / n
    v = np.zeros((n, n))
    for _ in range(count):
        fy = int(rng.integers(fy_max // 3, fy_max + 1)); fx = int(rng.integers(-fx_max, fx_max + 1))
        v += np.sin(2 * np.pi * (fx * x + fy * y) + rng.uniform(0, 2 * np.pi)) / np.sqrt(fy)
    return amp * v / np.abs(v).max()


def wood(n, base, dark, light, rings=11, warp=.9, fleck=.05, contrast=.3):
    """Tileable quarter-sawn teak running along x: long fine streaks, a few soft growth lines, a little colour drift."""
    y, x = np.mgrid[0:n, 0:n] / n
    w = periodic_noise(n, ((1, 1.0), (2, .5), (3, .25)))
    g = .5 + .5 * np.sin(2 * np.pi * (rings * y + warp * .25 * w))
    lines = g ** 6                                                          # soft late-wood lines
    st = streaks(n, 90, 2, 60) * .5 + .5                                    # fine long streaks
    st2 = streaks(n, 24, 1, 20) * .5 + .5
    pores = np.clip(streaks(n, 160, 6, 80) * .5 + .5, 0, 1) ** 6
    drift = periodic_noise(n, ((1, 1.0), (2, .5))) * .5 + .5
    base, dark, light = (np.array(c, float) for c in (base, dark, light))
    t = (st * .55 + st2 * .45)[..., None]
    col = dark[None, None] * (1 - t) + light[None, None] * t
    col = base[None, None] * (1 - contrast) + col * contrast
    col = col * (1 - lines[..., None] * .12) * (.96 + .08 * drift[..., None])
    col = col * (1 - pores[..., None] * fleck * 3)
    return np.clip(col, 0, 255)


def blur_wrap_x(h, r):
    k = 2 * r + 1; out = h.copy()
    for _ in range(2):
        out = sum(np.roll(out, s, axis=1) for s in range(-r, r + 1)) / k
        pad = np.pad(out, ((r, r), (0, 0)), mode="edge")
        out = sum(pad[r + s:r + s + h.shape[0]] for s in range(-r, r + 1)) / k
    return out


def frieze(wd=72, ht=28):
    """Carved coral stone, tiling along x: raised fillets top and bottom, a lozenge with a sunk drilled eye between
    them, and small triangles in the spandrels. Lit from the top left like the sun on a carved wall."""
    y, x = np.mgrid[0:ht, 0:wd].astype(float)
    h = np.zeros((ht, wd))
    h[:] = .35                                                               # sunk ground
    h[(y < 4.5) | (y >= ht - 4.5)] = 1.0                                     # fillets
    cx, cy = wd / 2, ht / 2
    d = np.abs(x - cx) / (wd * .36) + np.abs(y - cy) / (ht * .36)
    h[d < 1] = .9                                                            # lozenge
    h[(d < .66) & (d >= .5)] = .6                                            # cut groove ring
    h[np.hypot(x - cx, y - cy) < 2.4] = .4                                   # drilled eye
    for ex in (0, wd):                                                       # half lozenges meeting at the seam
        e = np.abs(x - ex) / (wd * .16) + np.abs(y - cy) / (ht * .2)
        h[e < 1] = .8
    h = blur_wrap_x(h, 1)
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * .5
    gy = np.gradient(h, axis=0)
    shade = 1 - 1.5 * (gx + gy)                                              # light from top-left
    occl = .86 + .14 * h                                                       # recesses a touch darker
    stone = np.array([236, 226, 207], float)
    speck = (periodic_noise(wd, ((13, 1.), (29, .6), (47, .4)))[:ht, :wd] * .5 + .5) ** 3
    col = stone[None, None] * (shade * occl)[..., None] * (1 - .05 * speck[..., None])
    col[0] *= .78; col[-1] *= .72                                            # crisp edges against the wood
    return np.clip(col, 0, 255).astype(np.uint8)


def frame(n=96, s=20, r=12, wood_field=False):
    """The panel: a rounded teak frame (grain follows each side), honey inlay, brass studs in the corners, and a clean
    pale coral-stone field. Slice at s px (2x)."""
    img = np.zeros((n, n, 4))
    y, x = np.mgrid[0:n, 0:n] + .5
    # distance inside the rounded rectangle
    qx = np.maximum(np.maximum(r - x, x - (n - r)), 0); qy = np.maximum(np.maximum(r - y, y - (n - r)), 0)
    outside = np.hypot(qx, qy) - r
    inner = np.minimum(np.minimum(x, n - x), np.minimum(y, n - y))
    dist = np.where((qx > 0) & (qy > 0), -outside, inner)                   # depth from the outer edge
    alpha = np.clip(dist + .5, 0, 1)
    wh = wood(128, (150, 98, 58), (92, 56, 30), (176, 122, 74), rings=9, fleck=.08)
    wv = np.transpose(wh, (1, 0, 2))
    xi, yi = x.astype(int) % 128, y.astype(int) % 128
    horiz = np.abs(y - n / 2) > np.abs(x - n / 2)                            # top/bottom sides vs left/right (mitred)
    woodc = np.where(horiz[..., None], wh[yi, xi], wv[yi, xi])
    walnut = np.array([58, 36, 21.]); honey = np.array([214, 160, 84.]); field = np.array([246, 240, 229.])
    col = np.where((dist < 2)[..., None], walnut, woodc)
    bevel = np.clip(1 - np.abs(dist - 8) / 7, 0, 1)                          # rounded edge catches the light
    col = col * (.86 + .22 * bevel[..., None])
    col = np.where(((dist >= 13) & (dist < 15.2))[..., None], honey, col)    # inlay
    col = np.where(((dist >= 15.2) & (dist < 16.5))[..., None], walnut * 1.3, col)
    if wood_field:                                                           # a teak board inside the frame
        fw = wood(128, (132, 86, 50), (84, 50, 26), (156, 106, 64), rings=9, fleck=.06)
        col = np.where((dist >= 16.5)[..., None], fw[yi, xi], col)
    else:
        col = np.where((dist >= 16.5)[..., None], field, col)
    sh = np.clip(1 - (dist - 16.5) / 3, 0, 1) * (dist >= 16.5)              # soft inner shadow under the frame
    col = col * (1 - (.25 if wood_field else .1) * sh[..., None])
    for cx, cy in ((8.2, 8.2), (n - 8.2, 8.2), (8.2, n - 8.2), (n - 8.2, n - 8.2)):
        rr = np.hypot(x - cx, y - cy)
        stud = np.clip(3.6 - rr, 0, 1)
        lit = np.clip(1.15 - ((x - cx + 1.2) ** 2 + (y - cy + 1.2) ** 2) / 14, .55, 1.15)
        col = col * (1 - stud[..., None]) + (honey * lit[..., None]) * stud[..., None]
    img[..., :3] = col; img[..., 3] = alpha * 255
    return np.clip(img, 0, 255).astype(np.uint8)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(wood(256, (124, 80, 46), (78, 46, 24), (146, 98, 60)).astype(np.uint8)).save(os.path.join(OUT, "ui_wood.png"))
    Image.fromarray(wood(256, (232, 206, 164), (196, 160, 112), (242, 222, 188), rings=7, warp=.7, fleck=.03).astype(np.uint8)).save(os.path.join(OUT, "ui_wood_light.png"))
    Image.fromarray(frieze()).save(os.path.join(OUT, "ui_frieze.png"))
    Image.fromarray(frame(), "RGBA").save(os.path.join(OUT, "ui_frame.png"))
    Image.fromarray(frame(wood_field=True), "RGBA").save(os.path.join(OUT, "ui_frame_wood.png"))
    print("wrote", OUT)

#!/usr/bin/env python3
"""Generate the tileable PBR texture sets used by the web build (assets/textures).

Everything is synthesised from seeded noise, so the output is reproducible and free of licensing questions.
Run:  python3 tools/gen_textures.py   (needs numpy and Pillow)
Albedo maps are sRGB JPEGs; normal maps are OpenGL convention (+Y up); foliage is RGBA PNG.
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), '..', 'assets', 'textures')
N = 1024


# ----------------------------------------------------------------- noise helpers (all periodic)
def spectral(n, beta, seed, lo=1.0, hi=None, aniso=(1.0, 1.0)):
    """Tileable power-law noise in [0,1]; beta controls roughness, lo/hi band-limit in cycles per tile."""
    rng = np.random.default_rng(seed)
    f = np.fft.fft2(rng.normal(size=(n, n)))
    ky = np.fft.fftfreq(n)[:, None] * n * aniso[1]
    kx = np.fft.fftfreq(n)[None, :] * n * aniso[0]
    k = np.sqrt(kx * kx + ky * ky)
    k[0, 0] = 1
    amp = 1.0 / k ** beta
    amp[k < lo] = 0
    if hi is not None:
        amp *= np.exp(-(k / hi) ** 2)
    out = np.real(np.fft.ifft2(f * amp))
    out -= out.min()
    return out / max(out.max(), 1e-9)


def ridged(n, seed, octaves=5, base=4):
    h = np.zeros((n, n)); a = 1.0; tot = 0
    for o in range(octaves):
        v = spectral(n, 1.2, seed + o, lo=base * 2 ** o * .7, hi=base * 2 ** o * 1.6)
        v = 1 - np.abs(v * 2 - 1)
        h += v * v * a; tot += a; a *= .5
    return h / tot


def voronoi(n, count, seed, jitter=1.0):
    """Tileable F1/F2 distances (in pixels) and cell ids, evaluated with wrap-around."""
    rng = np.random.default_rng(seed)
    pts = rng.random((count, 2)) * n
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    f1 = np.full((n, n), 1e9, np.float32); f2 = np.full((n, n), 1e9, np.float32); cid = np.zeros((n, n), np.int32)
    for i, (px, py) in enumerate(pts):
        dx = np.abs(xx - px); dx = np.minimum(dx, n - dx)
        dy = np.abs(yy - py); dy = np.minimum(dy, n - dy)
        d = np.sqrt(dx * dx + dy * dy)
        closer = d < f1
        f2 = np.where(closer, f1, np.minimum(f2, d))
        cid = np.where(closer, i, cid)
        f1 = np.where(closer, d, f1)
    return f1, f2, cid


def normal_map(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * .5
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * .5
    nx, ny, nz = -dx * strength, dy * strength, np.ones_like(h)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / l, ny / l, nz / l], -1) * .5 + .5


def lerp(a, b, t):
    t = t[..., None] if np.ndim(t) == 2 else t
    return a + (np.asarray(b) - a) * t


def rgb(hexs):
    return np.array([int(hexs[i:i + 2], 16) / 255 for i in (1, 3, 5)])


def save_rgb(arr, name, q=90):
    Image.fromarray((np.clip(arr, 0, 1) * 255 + .5).astype(np.uint8), 'RGB').save(os.path.join(OUT, name), quality=q, optimize=True)


def save_rgba(arr, name):
    Image.fromarray((np.clip(arr, 0, 1) * 255 + .5).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name), optimize=True)


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


# ----------------------------------------------------------------- ground sets
def sand():
    n = N
    yy, xx = np.mgrid[0:n, 0:n] / n
    warp = spectral(n, 2.2, 11, lo=1, hi=6) * 2 - 1
    warp2 = spectral(n, 2.2, 12, lo=1, hi=6) * 2 - 1
    # wind ripples: integer wave vector keeps them tileable; warped so they meander and fork
    ph = 2 * np.pi * (19 * xx + 7 * yy) + warp * 7 + warp2 * 3
    rip = (np.sin(ph) * .5 + .5) ** 1.6
    amp = smooth(.25, .75, spectral(n, 2.0, 13, lo=1, hi=4))
    grain = spectral(n, .3, 14, lo=200)
    h = rip * (.35 + .65 * amp) * .55 + grain * .25 + spectral(n, 1.6, 15, lo=2, hi=40) * .4
    base = lerp(rgb('#d9c49b'), rgb('#e9dcbc'), smooth(.3, .8, spectral(n, 1.8, 16, lo=1, hi=10)))
    col = base * (.88 + .16 * grain[..., None]) * (.94 + .08 * rip[..., None])
    rng = np.random.default_rng(17)
    specks = rng.random((n, n))
    col = np.where((specks < .018)[..., None], col * rgb('#6f6250') / rgb('#d9c49b') * .9, col)       # dark mineral grains
    col = np.where((specks > .992)[..., None], np.minimum(1, col * 1.12 + .05), col)                     # shell fragments
    save_rgb(col, 'sand_albedo.jpg'); save_rgb(normal_map(h, 6), 'sand_normal.jpg')


def grass():
    n = N
    # blades: high-frequency noise stretched along a slowly varying direction, plus clumps
    blades = spectral(n, .2, 21, lo=90, aniso=(1, 4)) * .5 + spectral(n, .2, 22, lo=90, aniso=(4, 1)) * .5
    blades = smooth(.3, .75, blades)
    clumps = spectral(n, 1.1, 23, lo=6, hi=120)
    soil = smooth(.66, .8, spectral(n, 1.6, 24, lo=3, hi=40)) * smooth(.4, .6, 1 - blades)
    dry = smooth(.55, .8, spectral(n, 2.0, 25, lo=1, hi=8))
    h = blades * .6 + clumps * .5 - soil * .4
    green = lerp(rgb('#4f7f2c'), rgb('#7aa33a'), clumps)
    green = lerp(green, rgb('#9a9a4a'), dry * .6)
    dirt = lerp(rgb('#6b5236'), rgb('#8a6d48'), blades)
    col = lerp(green, dirt, soil * .85)
    col = col * (.62 + .6 * blades[..., None])
    save_rgb(col, 'grass_albedo.jpg'); save_rgb(normal_map(h, 7), 'grass_normal.jpg')


def forest():
    n = N
    base_h = spectral(n, 1.5, 31, lo=2, hi=80)
    col = lerp(rgb('#3b2c1d'), rgb('#57412a'), base_h)
    h = base_h * .4
    img = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8), 'RGB')
    hm = Image.fromarray((h * 255).astype(np.uint8), 'L')
    d, dh = ImageDraw.Draw(img), ImageDraw.Draw(hm)
    rng = np.random.default_rng(32)
    leaf_cols = ['#5e4326', '#6e5130', '#7a5c36', '#4f3a24', '#4a4f2a', '#665a34', '#6b4a2a']
    for _ in range(3600):
        x, y = rng.random(2) * n; L = 6 + rng.random() * 16; W = L * (.35 + rng.random() * .25); a = rng.random() * np.pi
        c = leaf_cols[rng.integers(len(leaf_cols))]; v = int(140 + rng.random() * 115)
        for ox in (-n, 0, n):
            for oy in (-n, 0, n):
                cx, cy = x + ox, y + oy
                if -30 < cx < n + 30 and -30 < cy < n + 30:
                    ca, sa = np.cos(a), np.sin(a)
                    poly = [(cx + ca * L, cy + sa * L), (cx - sa * W, cy + ca * W), (cx - ca * L, cy - sa * L), (cx + sa * W, cy - ca * W)]
                    d.polygon(poly, fill=c); dh.polygon(poly, fill=v)
    col = np.asarray(img) / 255 * (.85 + .25 * spectral(n, .4, 33, lo=150)[..., None])
    h = np.asarray(hm.filter(ImageFilter.GaussianBlur(1.2))) / 255
    save_rgb(col, 'forest_albedo.jpg'); save_rgb(normal_map(h, 5), 'forest_normal.jpg')


def rock():
    n = N
    r = ridged(n, 41, octaves=6, base=3)
    strata = np.sin(2 * np.pi * (np.mgrid[0:n, 0:n][0] / n * 9 + spectral(n, 2.0, 42, lo=1, hi=5) * 2.5)) * .5 + .5
    cracks = smooth(.0, .035, np.abs(spectral(n, 1.2, 43, lo=4, hi=120, aniso=(1, .6)) * 2 - 1))   # thin meandering fissures
    fine = spectral(n, .5, 44, lo=120)
    h = r * .6 + strata * .12 + fine * .15 - (1 - cracks) * .15
    tone = spectral(n, 1.6, 45, lo=1, hi=20)
    col = lerp(rgb('#6d655c'), rgb('#9a9187'), tone)
    col = lerp(col, rgb('#7d6a52'), smooth(.5, .8, spectral(n, 1.8, 46, lo=2, hi=10)) * .5)
    col = col * (.75 + .4 * r[..., None]) * (.9 + .15 * fine[..., None])
    col = col * (.72 + .28 * cracks[..., None])
    lichen = smooth(.7, .78, spectral(n, 1.3, 47, lo=8, hi=90)) * smooth(.45, .7, r)
    col = lerp(col, rgb('#a8a46a'), lichen * .55)
    save_rgb(col, 'rock_albedo.jpg'); save_rgb(normal_map(h, 9), 'rock_normal.jpg')


def reef():
    n = N
    base = spectral(n, 1.8, 51, lo=1, hi=30)
    fine = spectral(n, .4, 52, lo=110)
    rubble_n = spectral(n, 1.0, 53, lo=25, hi=160)
    rubble = smooth(.62, .7, rubble_n)                                           # coral rubble pieces
    pink = smooth(.55, .7, spectral(n, 1.5, 54, lo=4, hi=40))
    grassp = smooth(.68, .8, spectral(n, 1.6, 55, lo=2, hi=24))                  # seagrass meadows
    blades = spectral(n, .5, 56, lo=80, aniso=(1, 3))
    h = base * .3 + fine * .2 + rubble * .5 + grassp * blades * .3
    sandc = lerp(rgb('#cdbb92'), rgb('#e0d2ac'), base) * (.88 + .18 * fine[..., None])
    rub = lerp(rgb('#b8a07c'), rgb('#c79a8a'), pink) * (.7 + .4 * rubble_n[..., None])
    col = lerp(sandc, rub, rubble)
    col = lerp(col, rgb('#6f7a3c') * (.5 + .8 * blades[..., None]), grassp * smooth(.35, .6, blades) * .9)
    save_rgb(col, 'reef_albedo.jpg'); save_rgb(normal_map(h, 7), 'reef_normal.jpg')


def soil():
    n = N
    base = spectral(n, 1.7, 61, lo=1, hi=60)
    fine = spectral(n, .5, 62, lo=100)
    f1, f2, cid = voronoi(n // 2, 260, 63)
    peb = np.kron(smooth(7, 2, f1), np.ones((2, 2))) * np.kron((np.random.default_rng(64).random(cid.max() + 1) < .12)[cid], np.ones((2, 2)))
    tufts = smooth(.66, .8, spectral(n, .9, 65, lo=20, hi=200))
    h = base * .4 + fine * .2 + peb * .5 + tufts * .25
    col = lerp(rgb('#7a5a3a'), rgb('#a07c54'), base) * (.85 + .25 * fine[..., None])
    col = lerp(col, rgb('#9a9180'), peb * .8)
    col = lerp(col, rgb('#8c8a45'), tufts * .7)
    save_rgb(col, 'soil_albedo.jpg'); save_rgb(normal_map(h, 7), 'soil_normal.jpg')


# ----------------------------------------------------------------- foliage (RGBA)
def palm_frond():
    """One frond seen from above: rachis along +x, leaflets angled toward the tip, tapering at both ends."""
    w, h = 1024, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    rng = np.random.default_rng(71)
    cy = h / 2
    for side in (-1, 1):
        for i in range(70):
            t = i / 70
            x0 = 30 + t * (w - 60)
            length = (h * .48) * np.sin(np.pi * min(1, t * 1.05 + .05)) ** .7 * (.85 + rng.random() * .2)
            ang = np.radians(55 + rng.random() * 10)
            x1, y1 = x0 + np.cos(ang) * length * .9, cy + side * np.sin(ang) * length
            wdt = 3 + 4 * (1 - abs(t - .4))
            g = rng.random()
            col = (int(70 + 40 * g + 30 * t), int(110 + 50 * g + 20 * t), int(35 + 25 * g), 255)
            # leaflet: a thin tapering quad with a darker midrib
            nx, ny = -(y1 - cy), (x1 - x0); l = np.hypot(nx, ny); nx, ny = nx / l * wdt, ny / l * wdt
            mx, my = (x0 + x1) / 2, (cy + y1) / 2
            d.polygon([(x0, cy), (mx + nx, my + ny), (x1, y1), (mx - nx, my - ny)], fill=col)
            d.line([(x0, cy), (x1, y1)], fill=(col[0] - 25, col[1] - 30, col[2] - 15, 255), width=1)
    d.line([(10, cy), (w - 10, cy)], fill=(120, 110, 60, 255), width=6)
    d.line([(10, cy - 1), (w - 10, cy - 1)], fill=(150, 140, 80, 255), width=2)
    save_rgba(np.asarray(img) / 255, 'palm_frond.png')


def leaf_cluster():
    s = 512
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    rng = np.random.default_rng(81)
    for i in range(170):
        r = s * .42 * np.sqrt(rng.random()); a = rng.random() * 2 * np.pi
        cx, cy = s / 2 + np.cos(a) * r, s / 2 + np.sin(a) * r
        L = 22 + rng.random() * 18; W = L * .42; th = rng.random() * np.pi * 2
        g = rng.random(); depth = 1 - r / (s * .45)
        col = (int((40 + 40 * g) * (.7 + .5 * depth)), int((85 + 60 * g) * (.7 + .5 * depth)), int((30 + 20 * g) * (.7 + .4 * depth)), 255)
        ca, sa = np.cos(th), np.sin(th)
        pts = []
        for k in range(12):
            u = k / 11 * 2 - 1
            pts.append((cx + ca * u * L - sa * W * np.sqrt(max(0, 1 - u * u)), cy + sa * u * L + ca * W * np.sqrt(max(0, 1 - u * u))))
        for k in range(11, -1, -1):
            u = k / 11 * 2 - 1
            pts.append((cx + ca * u * L + sa * W * np.sqrt(max(0, 1 - u * u)), cy + sa * u * L - ca * W * np.sqrt(max(0, 1 - u * u))))
        d.polygon(pts, fill=col)
        d.line([(cx - ca * L, cy - sa * L), (cx + ca * L, cy + sa * L)], fill=(col[0] + 20, col[1] + 25, col[2] + 10, 255), width=1)
    save_rgba(np.asarray(img) / 255, 'leaf_cluster.png')


def grass_tuft():
    w, h = 256, 256
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    rng = np.random.default_rng(91)
    for i in range(46):
        x0 = w / 2 + (rng.random() - .5) * w * .5
        lean = (rng.random() - .5) * w * .7
        top = h * (.15 + rng.random() * .45)
        g = rng.random()
        col = (int(80 + 60 * g), int(115 + 55 * g), int(40 + 20 * g), 255)
        pts = [(x0 - 3, h), (x0 + lean * .5, (h + top) / 2), (x0 + lean, top), (x0 + lean * .5 + 2, (h + top) / 2), (x0 + 3, h)]
        d.polygon(pts, fill=col)
    save_rgba(np.asarray(img) / 255, 'grass_tuft.png')


# ----------------------------------------------------------------- building and trunk materials
def bark():
    n = 512
    yy = np.mgrid[0:n, 0:n][0] / n
    rings = (np.sin(2 * np.pi * (yy * 26 + spectral(n, 2, 101, lo=1, hi=4) * .6)) * .5 + .5) ** 3
    fib = spectral(n, .6, 102, lo=40, aniso=(.25, 4))
    h = rings * .6 + fib * .4
    col = lerp(rgb('#6e5d48'), rgb('#9c8a70'), fib) * (.7 + .35 * (1 - rings[..., None]))
    save_rgb(col, 'bark_albedo.jpg'); save_rgb(normal_map(h, 6), 'bark_normal.jpg')


def plaster():
    n = 512
    lo = spectral(n, 1.8, 111, lo=1, hi=30); fine = spectral(n, .5, 112, lo=80)
    stains = smooth(.6, .85, spectral(n, 1.6, 113, lo=2, hi=20))
    yy = np.mgrid[0:n, 0:n][0] / n
    drip = smooth(.5, 1, yy) * spectral(n, .8, 114, lo=8, hi=60, aniso=(4, .3))
    h = lo * .5 + fine * .5
    v = (.9 + .1 * lo) * (.93 + .1 * fine) * (1 - .05 * stains) * (1 - .08 * drip)
    col = np.stack([v, v * .985, v * .96], -1)
    save_rgb(col, 'plaster_albedo.jpg'); save_rgb(normal_map(h, 3), 'plaster_normal.jpg')


def roof_tiles():
    n = 512; rows, cols = 8, 8
    yy, xx = np.mgrid[0:n, 0:n] / n
    u = (xx * cols + np.floor(yy * rows) * .5) % 1; v = (yy * rows) % 1
    barrel = np.sin(np.pi * u) ** .6
    overlap = smooth(0, .18, v)
    h = barrel * overlap
    rng = np.random.default_rng(121)
    tile_id = (np.floor(yy * rows) * 37 + np.floor(xx * cols + np.floor(yy * rows) * .5)).astype(int) % 997
    var = rng.random(1000)[tile_id]
    fine = spectral(n, .6, 122, lo=60)
    col = lerp(rgb('#9c4a2c'), rgb('#c46a3e'), var) * (.6 + .45 * h[..., None]) * (.9 + .15 * fine[..., None])
    col = lerp(col, rgb('#6a6a52'), smooth(.7, .85, spectral(n, 1.5, 123, lo=2, hi=30)) * .35)
    save_rgb(col, 'roof_albedo.jpg'); save_rgb(normal_map(h, 10), 'roof_normal.jpg')


def planks():
    n = 512; boards = 6
    yy, xx = np.mgrid[0:n, 0:n] / n
    b = np.floor(yy * boards); v = (yy * boards) % 1
    seam = smooth(0, .05, v) * smooth(1, .95, v)
    grain = np.sin(2 * np.pi * (yy * boards * 3 + spectral(n, 1.5, 131, lo=1, hi=10, aniso=(.2, 2)) * 3 + b * .37) * 4) * .5 + .5
    fibre = spectral(n, .5, 132, lo=40, aniso=(.15, 3))
    rng = np.random.default_rng(133); bv = rng.random(boards)[b.astype(int)]
    h = seam * (.6 + .2 * grain + .2 * fibre)
    col = lerp(rgb('#7a5434'), rgb('#a8794c'), bv) * (.75 + .2 * grain[..., None] + .15 * fibre[..., None]) * (.4 + .6 * seam[..., None])
    save_rgb(col, 'wood_albedo.jpg'); save_rgb(normal_map(h, 6), 'wood_normal.jpg')


def thatch():
    n = 512
    straw = spectral(n, .5, 141, lo=30, aniso=(.12, 3))
    layers = (np.mgrid[0:n, 0:n][0] / n * 7 + spectral(n, 2, 142, lo=1, hi=4) * .4) % 1
    h = straw * .6 + smooth(0, .3, layers) * .4
    col = lerp(rgb('#8a7444'), rgb('#c9ac6a'), straw) * (.6 + .4 * smooth(0, .3, layers)[..., None])
    save_rgb(col, 'thatch_albedo.jpg'); save_rgb(normal_map(h, 6), 'thatch_normal.jpg')


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for f in (sand, grass, forest, rock, reef, soil, palm_frond, leaf_cluster, grass_tuft, bark, plaster, roof_tiles, planks, thatch):
        f(); print('wrote', f.__name__)

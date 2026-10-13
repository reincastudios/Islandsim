# Leaf-cluster textures for the Maldivian trees and bushes. Each is one twig tip as a tree's leaf card sees it: the twig
# enters at the bottom centre and its leaves fan out above. Every leaf is painted per pixel from its own (along, across)
# coordinates, so it gets a folded midrib (one half lit, one half shaded), veins, a waxy sheen and a darker base.
import numpy as np, sys
from PIL import Image, ImageFilter

def shape(kind, t):
    """Half-width of a leaf of length 1 at t (0 = stalk end, 1 = tip)."""
    t = np.clip(t, 0, 1)
    if kind == 'heart':      # sea hibiscus: broad, heart-shaped, pointed
        return 0.52 * np.sin(np.pi * np.clip(t * 0.92 + 0.08, 0, 1)) ** 0.55 * (1 - t) ** 0.25
    if kind == 'obovate':    # tropical almond: widest two thirds out, blunt tip
        return 0.36 * (t ** 0.75) * (1 - t) ** 0.3 * 2.0
    if kind == 'spatula':    # beach naupaka: narrow at the stalk, a fleshy rounded spoon
        return 0.30 * (t ** 1.2) * (1 - t ** 3) ** 0.45 * 1.9
    if kind == 'ellipse':    # Alexandrian laurel: glossy oval
        return 0.27 * np.sin(np.pi * t) ** 0.75
    if kind == 'velvet':     # octopus bush: narrow, soft, rounded
        return 0.22 * np.sin(np.pi * t) ** 0.6 * (1 - 0.3 * t)
    raise ValueError(kind)

def paint_leaf(img, alpha, kind, origin, ang, L, col, rng, veins=9, sheen=0.35, fold=0.22):
    """Paint one leaf into float RGB img / alpha (H, W) at pixel origin, pointing at ang (radians, 0 = up), L pixels long."""
    H, W = alpha.shape
    d = np.array([np.sin(ang), -np.cos(ang)]); n = np.array([d[1], -d[0]])          # along / across in image coords
    corners = [origin + d * a * L + n * b * L for a in (0, 1.05) for b in (-0.6, 0.6)]
    xs = [c[0] for c in corners]; ys = [c[1] for c in corners]
    x0, x1 = int(max(0, min(xs))), int(min(W - 1, max(xs) + 1)); y0, y1 = int(max(0, min(ys))), int(min(H - 1, max(ys) + 1))
    if x1 <= x0 or y1 <= y0: return
    gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    rx, ry = gx - origin[0], gy - origin[1]
    t = (rx * d[0] + ry * d[1]) / L; s = (rx * n[0] + ry * n[1]) / L
    bend = 0.06 * np.sin(np.pi * t) * rng.uniform(-1, 1)                               # leaves curve a little
    s = s - bend
    w = shape(kind, t); inside = (t >= 0) & (t <= 1) & (np.abs(s) <= w)
    if not inside.any(): return
    rel = np.where(w > 1e-4, s / np.maximum(w, 1e-4), 0)                               # -1 .. 1 across
    side = np.sign(rel + 1e-6)
    # folded along the midrib: one half catches the light, the other is in its own shade
    shade = 1.0 + fold * side * rng.choice([-1, 1]) * np.abs(rel) ** 0.6
    shade *= 0.78 + 0.22 * np.clip(t * 2.5, 0, 1)                                     # darker at the stalk
    shade *= 1 - 0.18 * np.abs(rel) ** 3                                               # edges curl away
    vein = np.exp(-(rel / 0.05) ** 2) * 0.35                                           # midrib
    ph = (t * veins - np.abs(rel) * 1.6) % 1.0                                         # lateral veins run toward the tip
    vein += np.exp(-((ph - 0.5) / 0.07) ** 2) * 0.12 * (np.abs(rel) < 0.9)
    gloss = sheen * np.exp(-((rel + 0.35 * side * 0 - 0.45) / 0.3) ** 2) * np.sin(np.pi * np.clip(t, 0, 1))
    c = col[None, None, :] * shade[..., None]
    c = c + (np.array([0.85, 0.95, 0.6]) - c) * vein[..., None] * 0.6
    c = c + gloss[..., None] * 0.22
    sub = img[y0:y1 + 1, x0:x1 + 1]; sa = alpha[y0:y1 + 1, x0:x1 + 1]
    sub[inside] = c[inside]; sa[inside] = 1.0

def stalk(img, alpha, p0, p1, wid, col):
    H, W = alpha.shape
    x0, x1 = int(max(0, min(p0[0], p1[0]) - wid)), int(min(W - 1, max(p0[0], p1[0]) + wid))
    y0, y1 = int(max(0, min(p0[1], p1[1]) - wid)), int(min(H - 1, max(p0[1], p1[1]) + wid))
    gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    d = p1 - p0; l2 = max(1e-6, d @ d); u = np.clip(((gx - p0[0]) * d[0] + (gy - p0[1]) * d[1]) / l2, 0, 1)
    dist = np.hypot(gx - p0[0] - u * d[0], gy - p0[1] - u * d[1]); ww = wid * (1 - 0.5 * u)
    m = dist <= ww; sh = 1 - 0.4 * (dist / np.maximum(ww, 1e-3))
    img[y0:y1 + 1, x0:x1 + 1][m] = (col[None, None, :] * sh[..., None])[m]; alpha[y0:y1 + 1, x0:x1 + 1][m] = 1

def flower(img, alpha, c, r, rng):
    # beach naupaka's white half-flower: five petals on one side
    for k in range(5):
        a = -np.pi / 2 + (k - 2) * 0.55 + rng.uniform(-.1, .1)
        paint_leaf(img, alpha, 'ellipse', c, a, r, np.array([0.95, 0.94, 0.88]), rng, veins=0, sheen=0.1, fold=0.1)

def rosette(img, alpha, hub, kind, n, col, jitter, size, spread, rng, S, twig, yellow, tilt=0.0):
    leaves = []
    for k in range(n):
        a = tilt + (k / max(1, n - 1) - 0.5) * 2 * spread + rng.uniform(-.15, .15)   # fan out over the top
        L = S * size * rng.uniform(0.7, 1.0) * (1 - 0.3 * abs(a - tilt) / max(spread, 1e-3))
        leaves.append((rng.random(), a, L))
    for _, a, L in sorted(leaves):   # random paint order so the leaves overlap naturally
        cc = col * rng.uniform(1 - jitter, 1 + jitter) + rng.uniform(-0.025, 0.025, 3)
        if rng.random() < yellow: cc = np.array([0.72, 0.62, 0.18]) * rng.uniform(.85, 1.05)
        o = hub + np.array([np.sin(a), -np.cos(a)]) * S * 0.015
        stalk(img, alpha, hub, o, S * 0.005, np.array(twig) * 1.3)
        paint_leaf(img, alpha, kind, o, a, L, np.clip(cc, 0, 1), rng)

def cluster(kind, n, col, jitter, size, spread, rng, S, twig=(0.33, 0.27, 0.18), flowers=0, hubs=3, yellow=0.0):
    """A twig that forks into a few rosettes of leaves, filling the card so a canopy needs fewer of them."""
    img = np.zeros((S, S, 3)); alpha = np.zeros((S, S))
    base = np.array([S / 2, S - 1.0]); fork = np.array([S / 2, S * 0.72])
    stalk(img, alpha, base, fork, S * 0.011, np.array(twig))
    spots = [np.array([0.5, 0.42])] if hubs == 1 else [np.array([0.5 + 0.27 * np.sin(a), 0.48 - 0.2 * np.cos(a)]) for a in np.linspace(-1.1, 1.1, hubs)]
    order = list(range(len(spots))); rng.shuffle(order)
    for k in order:
        hub = (spots[k] + rng.uniform(-.03, .03, 2)) * S
        stalk(img, alpha, fork, hub, S * 0.007, np.array(twig))
        tilt = (spots[k][0] - 0.5) * 1.6
        rosette(img, alpha, hub, kind, n, col, jitter, size, spread, rng, S, twig, yellow, tilt)
    for k in range(flowers):
        hub = spots[rng.integers(len(spots))] * S
        flower(img, alpha, hub + np.array([rng.uniform(-.04, .04) * S, -rng.uniform(.02, .08) * S]), S * 0.04, rng)
    return img, alpha

def needles(rng, S, col):
    # ironwood: jointed grey-green twigs drooping from a branchlet
    img = np.zeros((S, S, 3)); alpha = np.zeros((S, S))
    # branchlets spread from the twig, and fine needles arc out and droop from each
    stalk(img, alpha, np.array([S / 2, S - 1.0]), np.array([S / 2, S * 0.35]), S * 0.008, np.array([0.35, 0.3, 0.22]))
    for k in range(9):
        a0 = (k / 8 - 0.5) * 2.2; src = np.array([S / 2, S * (0.75 - 0.4 * rng.random())])
        tip = src + np.array([np.sin(a0), -np.cos(a0) * 0.6]) * S * 0.33
        stalk(img, alpha, src, tip, S * 0.004, np.array([0.38, 0.33, 0.22]))
        for m in range(16):
            p = src + (tip - src) * rng.uniform(0.2, 1.0); a = a0 * 0.9 + rng.uniform(-0.4, 0.4); L = S * rng.uniform(0.18, 0.36)
            v = np.array([np.sin(a), -np.cos(a)]) * 1.0; pts = [p.copy()]
            for j in range(10):
                v = v + np.array([0, 0.22]); v /= np.linalg.norm(v); p = p + v * L / 10; pts.append(p.copy())   # gravity bends them
            cc = np.clip(col * rng.uniform(0.75, 1.25), 0, 1)
            for j in range(10): stalk(img, alpha, pts[j], pts[j + 1], S * 0.0028, cc * (0.85 + 0.3 * j / 10))
    return img, alpha

def save(img, alpha, path, S):
    rgb = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).resize((S, S), Image.LANCZOS)
    a = Image.fromarray((alpha * 255).astype(np.uint8)).resize((S, S), Image.LANCZOS)
    # bleed colour into the transparent pixels so mipmaps keep the leaf colour at their edges
    r = np.asarray(rgb).astype(np.float32); m = np.asarray(a) > 20
    bled = rgb
    for _ in range(6): bled = bled.filter(ImageFilter.MaxFilter(3))
    out = np.where(m[..., None], r, np.asarray(bled)).astype(np.uint8)
    Image.fromarray(np.dstack([out, np.asarray(a)]), 'RGBA').save(path)

TEX = {   # name: (builder, args)
    'isim_leaf_hibiscus': lambda rng, S: cluster('heart', 5, np.array([0.30, 0.52, 0.14]), 0.12, 0.29, 1.3, rng, S),
    'isim_leaf_almond':   lambda rng, S: cluster('obovate', 7, np.array([0.17, 0.40, 0.10]), 0.12, 0.31, 1.7, rng, S),
    'isim_leaf_naupaka':  lambda rng, S: cluster('spatula', 9, np.array([0.40, 0.64, 0.17]), 0.08, 0.26, 1.9, rng, S, flowers=3),
    'isim_leaf_laurel':   lambda rng, S: cluster('ellipse', 8, np.array([0.16, 0.40, 0.10]), 0.1, 0.24, 1.6, rng, S),
    'isim_leaf_velvet':   lambda rng, S: cluster('velvet', 12, np.array([0.48, 0.60, 0.40]), 0.06, 0.24, 2.1, rng, S),
    'isim_leaf_ironwood': lambda rng, S: needles(rng, S, np.array([0.36, 0.49, 0.27])),
}
if __name__ == '__main__':
    out = sys.argv[1]; S = 512; ss = 2
    names = sys.argv[2:] or list(TEX)
    for k, name in enumerate(names):
        rng = np.random.default_rng(100 + list(TEX).index(name))
        img, alpha = TEX[name](rng, S * ss)
        save(img, alpha, out + name + '.png', S)
        print(name)

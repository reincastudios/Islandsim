# Screw pine (Pandanus, "kashikeyo"): a slanting trunk standing on stilt roots, forking a few times like a candelabra,
# each branch ending in a spiral tuft of long arching strap leaves; a pineapple-like fruit hangs under some tufts.
# Writes tree_4 (three levels of detail) and its leaf texture.
import numpy as np, sys
from PIL import Image, ImageDraw, ImageFilter
from objio import write_lods
from palm_geo import nz, merge, sphere, UP

def tube(pts, r0, r1, sides, v_scale=1.5):
    """A tapering tube along a polyline, bark UVs running up it."""
    P, N, UV, I = [], [], [], []; arc = 0
    for j, c in enumerate(pts):
        if j: arc += np.linalg.norm(c - pts[j - 1])
        tg = nz(pts[min(j + 1, len(pts) - 1)] - pts[max(j - 1, 0)])
        ref = UP if abs(tg @ UP) < 0.95 else np.array([1.0, 0, 0])
        sd = nz(np.cross(tg, ref)); fw = nz(np.cross(sd, tg)); r = r0 + (r1 - r0) * j / (len(pts) - 1)
        for i in range(sides + 1):
            a = i / sides * 2 * np.pi; n = sd * np.cos(a) + fw * np.sin(a)
            P.append(c + n * r); N.append(n); UV.append([i / sides, arc / v_scale])
    for j in range(len(pts) - 1):
        for i in range(sides):
            a = j * (sides + 1) + i; b = a + sides + 1; I += [[a, b, a + 1], [a + 1, b, b + 1]]
    return np.array(P), np.array(N), np.array(UV), np.array(I)

def curve(p0, d0, length, segs, gravity, rng, wobble=0.04):
    # its own random stream, so every level of detail draws the same skeleton whatever its segment count
    rng = np.random.default_rng(rng.integers(1 << 30)); wobble *= (8 / segs) ** 0.5
    pts = [p0]; d = nz(d0); p = p0.copy()
    for k in range(segs):
        d = nz(d + UP * gravity + rng.normal(0, wobble, 3)); p = p + d * length / segs; pts.append(p.copy())
    return pts

def strap(base, d, L, width, segs, droop, fold, twist, dark):
    """One long leaf: a ribbon folded into a V along its keel, arching up and over."""
    P, N, UV, I = [], [], [], []
    hz = nz(np.array([d[0], 0, d[2]]) + 1e-6); elev = np.arcsin(np.clip(d[1], -1, 1))
    pts, dirs = [], []; p = base.copy()
    for k in range(segs + 1):
        s = k / segs; e = elev - droop * s ** 1.5; dv = hz * np.cos(e) + UP * np.sin(e); dirs.append(dv); pts.append(p.copy())
        p = p + dv * L / segs
    for k in range(segs + 1):
        s = k / segs; t = dirs[k]; sd = nz(np.cross(UP, t)); sd = sd * np.cos(twist * s) + nz(np.cross(t, sd)) * np.sin(twist * s)
        nup = nz(np.cross(t, sd)); w = width * (1 - s ** 2.2) * (0.6 + 0.4 * min(1, s * 6))
        for r, side in enumerate((-1, 0, 1)):
            lat = sd * side * np.cos(fold) + nup * np.sin(fold) * abs(side)   # edges raised: a V-shaped trough
            P.append(pts[k] + lat * w); N.append(nz(nup * 0.7 + UP * 0.3 + sd * side * 0.25))
            ak = np.round(np.clip(dark + 0.25 * (1 - s), 0, 1) * 15)
            UV.append([0.03 + 0.94 * s + ak, r / 2])
    for k in range(segs):
        for r in range(2):
            a = k * 3 + r; b = a + 3; I += [[a, b, a + 1], [a + 1, b, b + 1]]
    return np.array(P), np.array(N), np.array(UV), np.array(I)

LOD = [dict(sides=7, segs=8, lseg=6, leaves=30, rsides=5), dict(sides=5, segs=4, lseg=3, leaves=14, rsides=4), dict(sides=4, segs=3, lseg=2, leaves=8, rsides=3)]

def pandanus(seed, H, lean, forks):
    lods = []
    for lp in LOD:
        rng = np.random.default_rng(seed); az = rng.uniform(0, 2 * np.pi); dz = np.array([np.cos(az), 0, np.sin(az)])
        bark, leaves, fruit = [], [], []
        base = np.array([0, H * 0.2, 0.0])
        trunk = curve(base, UP + dz * np.tan(lean), H * 0.3, lp['segs'], 0.05, rng)
        bark.append(tube(trunk, 0.17, 0.14, lp['sides']))
        # stilt roots: from the lower trunk, splaying out to the ground
        for k in range(6):
            a = az + k / 6 * 2 * np.pi + rng.uniform(-.3, .3); top = base * rng.uniform(0.45, 1.0) + dz * 0.03
            foot = np.array([np.cos(a), 0, np.sin(a)]) * rng.uniform(0.6, 1.0) + np.array([0, -0.05, 0])
            mid = top * 0.45 + foot * 0.55 + UP * 0.15
            bark.append(tube([top, mid, foot], 0.065, 0.05, lp['rsides'], 0.8))
        bark.append(tube([np.array([0, -0.05, 0.0]), base], 0.13, 0.16, lp['sides'], 0.8))
        # forks: each tip splits into two or three branches; the leaf tufts sit on the last ones
        tips = [(trunk[-1], nz(trunk[-1] - trunk[-2]), 0.12)]
        for f in range(forks):
            nt = []
            for p, d, r in tips:
                for b in range(rng.integers(2, 4) if f < forks - 1 else 2):
                    a = rng.uniform(0, 2 * np.pi); out = np.array([np.cos(a), 0, np.sin(a)])
                    dd = nz(d * 0.5 + out * 0.9 + UP * 0.25); L = H * rng.uniform(0.22, 0.3) * (0.8 ** f)
                    pts = curve(p, dd, L, max(2, lp['segs'] // 2), 0.12, rng); bark.append(tube(pts, r, r * 0.8, max(3, lp['sides'] - 2)))
                    nt.append((pts[-1], nz(pts[-1] - pts[-2]), r * 0.8))
            tips = nt
        for ti, (p, d, r) in enumerate(tips):
            n = lp['leaves']; trng = np.random.default_rng(seed * 100 + ti)   # same tuft on every level
            for k in range(n):
                rk = k / max(1, n - 1)   # 0 = youngest, upright in the middle .. 1 = oldest, arching out and down
                a = k * 2.39996 * LOD[0]['leaves'] / n + trng.uniform(-.2, .2); out = np.array([np.cos(a), 0, np.sin(a)])
                dv = nz(UP * (1.2 - 1.1 * rk) + out * (0.3 + 0.9 * rk))
                leaves.append(strap(p, dv, H * trng.uniform(0.22, 0.28) * (0.8 + 0.3 * rk), 0.085 * (LOD[0]['leaves'] / n) ** 0.5, lp['lseg'], np.radians(30 + 80 * rk),
                                    np.radians(30), trng.uniform(-.6, .6), dark=0.05 + 0.3 * rk))
            if lp is LOD[0] and ti % 3 == 0:
                c = p - UP * 0.35 + d * 0.05; fruit.append(sphere(c, 0.19, 1))
        parts = [('isim_bark_pandanus', *merge(bark)), ('isim_leaf_pandanus', *merge(leaves))]
        if fruit: parts.append(('isim_pandanus_fruit', *merge(fruit)))
        lods.append(parts)
    return lods

def textures(o):
    rng = np.random.default_rng(5)
    # leaf: a long strap (u along it) with a pale keel and fine teeth on its edges, green with yellow-green margins
    W, H = 1024, 128; im = np.zeros((H, W, 4)); y = np.linspace(0, 1, H)[:, None]; x = np.linspace(0, 1, W)[None, :]
    keel = np.exp(-((y - 0.5) / 0.05) ** 2); edge = np.clip(np.abs(y - 0.5) * 2 - 0.75, 0, 1) * 4
    streak = 0.92 + 0.08 * np.sin(y * 90 + rng.random((1, W)) * 0.5)
    col = np.array([0.24, 0.47, 0.13])[None, None, :] * streak[..., None] * (0.85 + 0.25 * x[..., None])
    col = col + (np.array([0.62, 0.72, 0.36]) - col) * (keel[..., None] * 0.45 + edge[..., None] * 0.35)
    tip = np.clip((x - 0.9) / 0.1, 0, 1)
    col = col + (np.array([0.55, 0.48, 0.25]) - col) * (tip[..., None] * 0.7)
    teeth = (np.abs(y - 0.5) < 0.47 + 0.03 * (np.sin(x * 700) > 0.6))
    im[..., :3] = np.broadcast_to(col, (H, W, 3)); im[..., 3] = teeth
    Image.fromarray((np.clip(im, 0, 1) * 255).astype(np.uint8), 'RGBA').save(o + 'isim_leaf_pandanus.png')
    # bark: grey-brown with pale leaf-scar rings
    W, H = 256, 512; y = np.arange(H)[:, None] / H; x = np.arange(W)[None, :] / W
    ring = np.exp(-(((y * 26 + 0.1 * np.sin(x * 12.6)) % 1 - 0.5) / 0.12) ** 2)
    n = rng.random((H // 8, W // 8)); n = np.asarray(Image.fromarray((n * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)) / 255
    col = np.array([0.45, 0.40, 0.33]) * (0.8 + 0.3 * n[..., None]) + np.array([0.15, 0.14, 0.12]) * ring[..., None]
    Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).save(o + 'isim_bark_pandanus.jpg', quality=92)
    # fruit: orange-green segments
    W, H = 256, 256; u = np.arange(W)[None, :] / W; v = np.arange(H)[:, None] / H
    cell = (np.sin(u * 2 * np.pi * 12) * np.sin(v * np.pi * 10)) ** 2
    col = np.array([0.55, 0.42, 0.12]) * (1 - 0.5 * cell[..., None]) + np.array([0.25, 0.35, 0.1]) * (v[..., None] < 0.3)
    Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).save(o + 'isim_pandanus_fruit.jpg', quality=92)

if __name__ == '__main__':
    plants, tex = sys.argv[1], sys.argv[2]
    lods = pandanus(8, 6.0, np.radians(12), 2)
    write_lods(plants, 'tree_4', lods, ['isim_bark_pandanus', 'isim_leaf_pandanus', 'isim_pandanus_fruit'])
    print('tree_4', [sum(len(p[4]) for p in l) for l in lods])
    textures(tex)

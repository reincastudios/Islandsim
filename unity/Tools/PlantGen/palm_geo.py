# Coconut palm generator: curved, leaning trunk with a swollen base and leaf-scar rings; a crown of arching fronds
# whose leaflets fold into a V and droop; dry fronds hanging below; a cluster of coconuts. Three levels of detail.
import numpy as np, sys, json
from objio import write_obj, write_lods
UP = np.array([0, 1, 0.0])
def nz(v): return v / (np.linalg.norm(v, axis=-1, keepdims=True) + 1e-9)

def trunk(H, lean, bend, az, sides, segs, rng):
    d = np.array([np.cos(az), 0, np.sin(az)])
    def centre(t):  # leans out, then curves back toward vertical near the top
        off = H * np.tan(lean) * (t - bend * t * t)
        return d * off + UP * H * t + d * 0.04 * np.sin(t * 7 + rng.random())
    P, N, UV, I = [], [], [], []
    arc = 0; prev = centre(0)
    for j in range(segs + 1):
        t = j / segs; c = centre(t); arc += np.linalg.norm(c - prev); prev = c
        tg = nz(centre(min(1, t + 1e-3)) - centre(max(0, t - 1e-3)))
        sd = nz(np.cross(tg, d)); fw = nz(np.cross(sd, tg))
        r = 0.15 + 0.06 * (1 - t) + 0.16 * np.exp(-t * 18) + 0.012 * np.sin(t * 60)   # swollen bole, slight taper
        for i in range(sides + 1):
            a = i / sides * 2 * np.pi; n = sd * np.cos(a) + fw * np.sin(a)
            P.append(c + n * r); N.append(n); UV.append([i / sides * 1.0, arc / 2.4])
    for j in range(segs):
        for i in range(sides):
            a = j * (sides + 1) + i; b = a + sides + 1
            I += [[a, b, a + 1], [a + 1, b, b + 1]]
    return np.array(P), np.array(N), np.array(UV), np.array(I), centre(1), nz(centre(1) - centre(0.97))

def frond(top, az, elev, L, droop, fold, width, segs, rows, curl, rng):
    # the rachis: a curve that starts at elev and bends under its own weight
    pts = [top]; dirs = []; p = top.copy(); ds = L / segs
    for k in range(segs + 1):
        s = k / segs; e = elev - droop * s ** 1.4
        dvec = np.array([np.cos(e) * np.cos(az), np.sin(e), np.cos(e) * np.sin(az)]); dirs.append(dvec)
        if k < segs: p = p + dvec * ds; pts.append(p)
    pts = np.array(pts); dirs = np.array(dirs)
    P, N, UV, I = [], [], [], []
    for side in (-1, 1):
        base = len(P)
        for k in range(segs + 1):
            s = k / segs; t = dirs[k]; sd = nz(np.cross(UP, t)) * side
            if np.linalg.norm(np.cross(UP, t)) < 1e-3: sd = np.array([side, 0, 0.0])
            nup = nz(np.cross(t, sd)) * side          # the frond's own "up"
            wd = width * (0.25 + 0.75 * np.sin(min(1, s * 1.25) * np.pi / 2)) * (1 - 0.6 * s ** 3)
            for r in range(rows + 1):
                v = r / rows
                lat = sd * np.cos(fold) - nup * np.sin(fold)   # leaflets fold down into a V
                q = pts[k] + lat * wd * v - UP * (curl * wd * v * v)      # and droop toward their tips
                P.append(q)
                surf = nz(np.cross(t, lat)) * side
                if surf @ UP < 0: surf = -surf
                out = nz(np.array([q[0] - top[0], 0, q[2] - top[2]]) + 1e-6)
                N.append(nz(surf * 0.5 + UP * 0.35 + out * 0.4)); UV.append([0.04 + 0.95 * s, 1 - v])
        for k in range(segs):
            for r in range(rows):
                a = base + k * (rows + 1) + r; b = a + rows + 1
                if side > 0: I += [[a, b, a + 1], [a + 1, b, b + 1]]
                else: I += [[a, a + 1, b], [a + 1, b + 1, b]]
    return np.array(P), np.array(N), np.array(UV), np.array(I)

def sphere(c, r, sub):
    # octahedron subdivided once or not
    v = [np.array(x, float) for x in [(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)]]
    f = [(0,2,4),(2,1,4),(1,3,4),(3,0,4),(2,0,5),(1,2,5),(3,1,5),(0,3,5)]
    for _ in range(sub):
        nf = []; cache = {}
        def mid(a, b):
            k = (min(a, b), max(a, b))
            if k not in cache: v.append(nz(v[a] + v[b])); cache[k] = len(v) - 1
            return cache[k]
        for a, b, cc in f:
            ab, bc, ca = mid(a, b), mid(b, cc), mid(cc, a); nf += [(a, ab, ca), (ab, b, bc), (ca, bc, cc), (ab, bc, ca)]
        f = nf
    V = np.array(v); P = c + V * r * np.array([1, 1.15, 1]); UVs = np.stack([np.arctan2(V[:, 2], V[:, 0]) / 6.283 + .5, V[:, 1] * .5 + .5], 1)
    return P, V, UVs, np.array(f)

def merge(parts):
    P, N, UV, I = [], [], [], []; b = 0
    for p, n, uv, i in parts: P.append(p); N.append(n); UV.append(uv); I.append(i + b); b += len(p)
    return np.concatenate(P), np.concatenate(N), np.concatenate(UV), np.concatenate(I)

LOD = [dict(sides=10, segs=26, fseg=10, rows=2, nuts=8, nsub=1), dict(sides=6, segs=10, fseg=5, rows=1, nuts=0, nsub=0), dict(sides=5, segs=4, fseg=3, rows=1, nuts=0, nsub=0)]

def palm(seed, H, lean, bend, nfr, ndry):
    lods = []
    for li, lp in enumerate(LOD):
        rng = np.random.default_rng(seed); az = rng.uniform(0, 2 * np.pi)
        tP, tN, tUV, tI, top, tdir = trunk(H, lean, bend, az, lp['sides'], lp['segs'], rng)
        green, dry, nuts = [], [], []
        g = 2.39996
        for k in range(nfr):
            rk = k / max(1, nfr - 1)    # 0 = youngest (top, upright) .. 1 = oldest (low, drooping)
            a = k * g + rng.uniform(-.15, .15)
            elev = np.radians(75 - 95 * rk ** 0.9 + rng.uniform(-6, 6)); L = 4.6 * (0.7 + 0.3 * np.sin(min(1, rk * 1.6) * np.pi / 2)) * rng.uniform(.9, 1.08)
            droop = np.radians(30 + 70 * rk) * rng.uniform(.85, 1.15)
            green.append(frond(top + UP * 0.1 * (1 - rk), a, elev, L, droop, np.radians(28 + 10 * rk), 1.05, lp['fseg'], lp['rows'], 0.35 + 0.3 * rk, rng))
        for k in range(ndry):
            a = rng.uniform(0, 2 * np.pi)
            dry.append(frond(top - UP * 0.15, a, np.radians(-55 + rng.uniform(-10, 10)), 3.8, np.radians(25), np.radians(50), 0.9, max(3, lp['fseg'] // 2), 1, 0.5, rng))
        for k in range(lp['nuts']):
            a = k / max(1, lp['nuts']) * 2 * np.pi + rng.uniform(-.3, .3); rr = 0.26 + rng.uniform(0, .08)
            c = top + np.array([np.cos(a) * rr, -0.25 - rng.uniform(0, .25), np.sin(a) * rr])
            nuts.append(sphere(c, 0.13 + rng.uniform(0, .03), lp['nsub']))
        # green and dry fronds share one texture: green in the top half, dry in the bottom, so one draw covers both
        gP, gN, gUV, gI = merge(green); gUV = gUV.copy(); gUV[:, 1] = 0.52 + 0.48 * gUV[:, 1]
        fr = [(gP, gN, gUV, gI)]
        if dry:
            dP, dN, dUV, dI = merge(dry); dUV = dUV.copy(); dUV[:, 1] = 0.48 * dUV[:, 1]; fr.append((dP, dN, dUV, dI))
        parts = [('isim_palm_bark', tP, tN, tUV, tI), ('isim_palm_frond', *merge(fr))]
        if nuts: parts.append(('isim_coconut', *merge(nuts)))
        lods.append(parts)
    return lods

VARIANTS = {'palm': (11, 9.0, np.radians(9), 0.35, 17, 2), 'palm_1': (23, 8.2, np.radians(20), 0.55, 15, 3),
            'palm_2': (37, 7.0, np.radians(4), 0.2, 18, 1)}
if __name__ == '__main__':
    out = sys.argv[1]; res = {}
    for name, v in VARIANTS.items():
        lods = palm(*v)
        write_lods(out, name, lods, ['isim_palm_bark', 'isim_palm_frond', 'isim_coconut'])
        print(name, [sum(len(p[4]) for p in l) for l in lods])

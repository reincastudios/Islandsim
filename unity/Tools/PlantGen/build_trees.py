# Turns ez-tree's branches and leaf cards (trees.json from gen.mjs) into the game's OBJ files, one per level of detail.
# Far levels keep fewer leaf cards, scaled up so the canopy stays full. Leaf normals bend outward from the canopy centre
# for soft, rounded canopy light, and each card carries how deep inside the canopy it sits (see ao_encode).
import json, sys, numpy as np
from objio import write_lods, compact
LEAF_KEEP = {'tree': [0.5, 0.15, 0.045], 'bush': [0.34, 0.11, 0.04]}
def arr(x, k): return np.array(x, np.float32).reshape(-1, k)

def ao_encode(UV, dark):
    """The game's foliage shader reads ambient occlusion from the whole-number part of u: u + k, k = 0 (open) .. 15 (deep
    inside). Leaf textures only use u in 0..1, so this costs nothing and needs no second UV set (OBJ has only one)."""
    k = np.clip(np.round(np.asarray(dark) * 15), 0, 15)
    UV = UV.copy(); UV[:, 0] = np.clip(UV[:, 0], 0.002, 0.998) + k; return UV

TILT = np.radians(55)
def nz(x): return x / (np.linalg.norm(x) + 1e-9)

def tilt(v, o, centre, ang):
    """ez-tree's leaf cards stand upright, so a camera looking down sees them edge-on. Tip each card over its base edge,
    away from the canopy centre, so the canopy reads as leaves from above as well as from the side."""
    a = nz(v[2] - v[1]); top = (v[0] + v[3]) / 2 - o; n = nz(np.cross(a, top))
    w = o - centre; w[1] = 0
    sgn = 1.0 if n @ w >= 0 else -1.0
    th = ang * sgn; c, s_ = np.cos(th), np.sin(th)
    r = v - o   # Rodrigues rotation about a through o
    r = r * c + np.cross(a, r) * s_ + np.outer(r @ a, a) * (1 - c)
    # pick the rotation direction that moves the card's top toward the outward normal
    tn = r[0] + r[3]
    if (tn @ (n * sgn)) < 0:
        th = -th; c, s_ = np.cos(th), np.sin(th); r = v - o
        r = r * c + np.cross(a, r) * s_ + np.outer(r @ a, a) * (1 - c)
    return o + r

def leaves_lod(L, keep, rng):
    P, N, UV = arr(L['pos'], 3), arr(L['nrm'], 3), arr(L['uv'], 2)
    nq = len(P) // 4; groups = []; q = 0
    while q < nq:   # double billboards: two quads sharing a base midpoint form one leaf
        m0 = (P[q*4+1] + P[q*4+2]) / 2
        if q + 1 < nq and np.allclose(m0, (P[q*4+5] + P[q*4+6]) / 2, atol=1e-4): groups.append([q, q+1]); q += 2
        else: groups.append([q]); q += 1
    sel = [g for g in groups if rng.random() < keep]; s = (1 / keep) ** 0.5
    # the canopy as an ellipsoid around the leaf cards; centre a little high so normals bend outward and up
    lo, hi = np.percentile(P, 4, axis=0), np.percentile(P, 96, axis=0)
    centre = (lo + hi) / 2 + np.array([0, (hi[1] - lo[1]) * 0.08, 0]); R = np.maximum((hi - lo) / 2, 1e-3)
    oP, oN, oUV, oI = [], [], [], []
    for g in sel:
        for qq in g:
            v = P[qq*4:qq*4+4].copy(); o = (v[1] + v[2]) / 2; v = o + (v - o) * s
            v = tilt(v, o, centre, TILT); n0 = nz(np.cross(v[2] - v[1], v[0] - v[1]))
            out = v - centre; out /= np.linalg.norm(out, axis=1, keepdims=True) + 1e-6
            if n0 @ out.mean(0) < 0: n0 = -n0
            n = n0 * 0.4 + out * 0.6; n /= np.linalg.norm(n, axis=1, keepdims=True)
            # open at the canopy's skin, darker toward its core and its underside
            m = v.mean(0); r = np.linalg.norm((m - centre) / R); h = (m[1] - lo[1]) / max(hi[1] - lo[1], 1e-3)
            dark = min(1.0, np.clip(1 - (r - 0.25) / 0.75, 0, 1) ** 1.3 * 0.55 + np.clip(0.45 - h, 0, 1) * 0.45)
            b = len(oP) * 4; oP.append(v); oN.append(n); oUV.append(ao_encode(UV[qq*4:qq*4+4], np.full(4, dark)))
            oI += [[b, b+1, b+2], [b, b+2, b+3]]
    return np.concatenate(oP), np.concatenate(oN), np.concatenate(oUV), np.array(oI)

if __name__ == '__main__':
    D = json.load(open('trees.json')); OUT = sys.argv[1] if len(sys.argv) > 1 else 'out/'
    textures = set()
    for name, t in D.items():
        kind = 'bush' if name.startswith('bush') else 'tree'
        bm, lm = f"isim_bark_{t['bark']}", f"isim_leaf_{t['leaf']}"; textures |= {t['bark']}
        ts = t['texScale']
        BP, BN, BUV = arr(t['B']['pos'], 3), arr(t['B']['nrm'], 3), arr(t['B']['uv'], 2) * np.array([ts['x'], 1 / ts['y']], np.float32)
        lods = []
        for li, idx in enumerate(t['lodB']):
            I = np.array(idx).reshape(-1, 3); bp = compact(BP, BN, BUV, I)
            lp = leaves_lod(t['L'], LEAF_KEEP[kind][li], np.random.default_rng(11 + li))
            lods.append([(bm, *bp), (lm, *lp)])
            print(name, 'LOD', li, 'tris', len(bp[3]) + len(lp[3]))
        write_lods(OUT, name, lods, [bm, lm])
    json.dump(sorted(textures), open('tree_textures.json', 'w'))

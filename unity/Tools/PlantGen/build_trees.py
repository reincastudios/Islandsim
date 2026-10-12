import json, sys, numpy as np
from PIL import Image
from objio import write_obj, write_lods, compact
from render import render, load_tex
D = json.load(open('trees.json')); OUT = 'lods3/'
LEAF_KEEP = {'tree': [0.5, 0.15, 0.045], 'bush': [0.5, 0.16, 0.05]}
LEAF_OVERRIDE = {'tree_1': 'ash', 'bush_1': 'oak', 'bush_2': 'ash'}
def arr(x, k): return np.array(x, np.float32).reshape(-1, k)

def leaves_lod(L, keep, rng):
    P, N, UV = arr(L['pos'], 3), arr(L['nrm'], 3), arr(L['uv'], 2)
    nq = len(P) // 4; groups = []; q = 0
    while q < nq:   # double billboards: two quads sharing a base midpoint form one leaf
        m0 = (P[q*4+1] + P[q*4+2]) / 2
        if q + 1 < nq and np.allclose(m0, (P[q*4+5] + P[q*4+6]) / 2, atol=1e-4): groups.append([q, q+1]); q += 2
        else: groups.append([q]); q += 1
    sel = [g for g in groups if rng.random() < keep]; s = (1 / keep) ** 0.5
    centre = P.mean(0) * np.array([1, 1.15, 1])   # canopy centre, a little high: normals bend outward from it
    oP, oN, oUV, oI = [], [], [], []
    for g in sel:
        for qq in g:
            v = P[qq*4:qq*4+4].copy(); o = (v[1] + v[2]) / 2; v = o + (v - o) * s
            n0 = N[qq*4]; out = v - centre; out /= np.linalg.norm(out, axis=1, keepdims=True) + 1e-6
            n = n0 * 0.35 + out * 0.65; n /= np.linalg.norm(n, axis=1, keepdims=True)
            b = len(oP) * 4; oP.append(v); oN.append(n); oUV.append(UV[qq*4:qq*4+4]); oI += [[b, b+1, b+2], [b, b+2, b+3]]
    return np.concatenate(oP), np.concatenate(oN), np.concatenate(oUV), np.array(oI)

textures = set(); previews = []
for name, t in D.items():
    kind = 'bush' if name.startswith('bush') else 'tree'; rng = np.random.default_rng(7)
    bark, leaf = t['bark'], LEAF_OVERRIDE.get(name, t['leaf']); ts = t['texScale']
    bm, lm = f"isim_bark_{bark}", f"isim_leaf_{leaf}"; textures |= {('bark', bark), ('leaf', leaf)}
    BP, BN, BUV = arr(t['B']['pos'], 3), arr(t['B']['nrm'], 3), arr(t['B']['uv'], 2) * np.array([ts['x'], 1 / ts['y']], np.float32)
    lods = []
    for li, idx in enumerate(t['lodB']):
        I = np.array(idx).reshape(-1, 3); bp = compact(BP, BN, BUV, I)
        lp = leaves_lod(t['L'], LEAF_KEEP[kind][li], np.random.default_rng(11 + li))
        lods.append([(bm, *bp), (lm, *lp)])
        print(name, 'LOD', li, 'tris', len(bp[3]) + len(lp[3]))
    write_lods(OUT, name, lods, [bm, lm])
    if '--preview' in sys.argv:
        bt = load_tex(f'package/src/lib/assets/bark/{bark}_color_1k.jpg'); lt = load_tex(f'package/src/lib/assets/leaves/{leaf}_color.png')
        row = []
        for li in range(3):
            (m1, *b), (m2, *l) = lods[li]
            row.append(render([(b[0], b[1], b[2], b[3], bt, None, (1, 1)), (l[0], l[1], l[2], l[3], lt, None, (1, 1))], size=(320, 320)))
        im = Image.new('RGB', (960, 320)); [im.paste(r, (i * 320, 0)) for i, r in enumerate(row)]; im.save(f'prev_{name}.png')
json.dump(sorted(textures), open('tree_textures.json', 'w'))

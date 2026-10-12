# Preview the OBJ files the game will load: python3 prev_obj.py <plants folder> <textures folder> name [name...]
import sys, numpy as np
from PIL import Image
from render import render, load_tex

def read_obj(path):
    V, T, N, parts, cur = [], [], [], {}, None
    for line in open(path):
        p = line.split()
        if not p: continue
        if p[0] == 'v': V.append([float(x) for x in p[1:4]])
        elif p[0] == 'vt': T.append([float(x) for x in p[1:3]])
        elif p[0] == 'vn': N.append([float(x) for x in p[1:4]])
        elif p[0] == 'usemtl': cur = parts.setdefault(p[1], [])
        elif p[0] == 'f': cur.append([int(x.split('/')[0]) - 1 for x in p[1:4]])
    V, T, N = np.array(V), np.array(T), np.array(N); out = []
    for mat, F in parts.items():
        F = np.array(F); used = np.unique(F); remap = -np.ones(len(V), int); remap[used] = np.arange(len(used))
        out.append((mat, V[used], N[used], T[used], remap[F]))
    return out

if __name__ == '__main__':
    pdir, tdir, names = sys.argv[1], sys.argv[2], sys.argv[3:]
    cache = {}
    def tex(m):
        if m not in cache:
            for ext in ('.png', '.jpg'):
                try: cache[m] = load_tex(tdir + m + ext); break
                except FileNotFoundError: pass
        return cache.get(m)
    for name in names:
        row = []
        for suffix, yaw, pitch in [('', 0.6, 0.15), ('', 2.2, 0.7), ('_LOD1', 0.6, 0.15), ('_LOD2', 0.6, 0.15)]:
            parts = read_obj(f'{pdir}{name}{suffix}.obj')
            row.append(render([(P, N, UV, I, tex(m), (0.5, 0.5, 0.5), (1, 1), 'leaf' in m or 'frond' in m) for m, P, N, UV, I in parts], size=(360, 360), yaw=yaw, pitch=pitch))
        im = Image.new('RGB', (1440, 360)); [im.paste(r, (i * 360, 0)) for i, r in enumerate(row)]; im.save(f'prev_{name}.png')
        print(name)

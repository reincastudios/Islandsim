import numpy as np
def write_obj(path, name, lods, mtl_names):
    """lods: list of parts; each part = (mat, P, N, UV, I). Writes one object per LOD (name_LOD0...) and a matching .mtl."""
    lines = [f"# Island Sim plant: {name}", f"mtllib {name}.mtl"]; base = 1
    for li, parts in enumerate(lods):
        lines.append(f"o {name}" if len(lods) == 1 else f"o {name}_LOD{li}")
        for mat, P, N, UV, I in parts:
            lines += [f"v {p[0]:.4f} {p[1]:.4f} {p[2]:.4f}" for p in P]
            lines += [f"vt {u[0]:.4f} {u[1]:.4f}" for u in UV]
            lines += [f"vn {n[0]:.3f} {n[1]:.3f} {n[2]:.3f}" for n in N]
            lines.append(f"usemtl {mat}")
            lines += [f"f {a+base}/{a+base}/{a+base} {b+base}/{b+base}/{b+base} {c+base}/{c+base}/{c+base}" for a, b, c in I]
            base += len(P)
    open(path, 'w').write("\n".join(lines) + "\n")
    mtl = []
    for m in mtl_names: mtl += [f"newmtl {m}", "Kd 1 1 1", "Ns 10", ""]
    open(path[:-4] + ".mtl", 'w').write("\n".join(mtl))

def write_lods(out, name, lods, mtl_names):
    """One OBJ per level (name, name_LOD1, name_LOD2...), so every importer keeps the levels apart."""
    for li, parts in enumerate(lods):
        n = name if li == 0 else f"{name}_LOD{li}"
        write_obj(out + n + ".obj", n, [parts], mtl_names)

def compact(P, N, UV, I):
    used = np.unique(I); remap = -np.ones(len(P), int); remap[used] = np.arange(len(used))
    return P[used], N[used], UV[used], remap[I]

# Tiny software renderer for checking plant models: z-buffer, textures with alpha test, sun + sky lighting.
import numpy as np
from PIL import Image

def load_tex(path):
    im = Image.open(path).convert('RGBA'); return np.asarray(im).astype(np.float32) / 255.0

def render(meshes, size=(512, 512), yaw=0.6, pitch=0.25, bg=(0.72, 0.82, 0.9), fit=None):
    """meshes: list of (P[n,3], N[n,3], UV[n,2], I[m,3], tex[h,w,4] or None, color[3], uv_tile[, foliage])
    foliage: decode the canopy occlusion from u's whole-number part and light it like the game's foliage shader."""
    W, H = size
    allp = np.concatenate([m[0] for m in meshes])
    lo, hi = allp.min(0), allp.max(0); c = (lo + hi) / 2; ext = (hi - lo).max() * 0.55
    cy, sy, cp, sp = np.cos(yaw), np.sin(yaw), np.cos(pitch), np.sin(pitch)
    R = np.array([[cy, 0, -sy], [0, 1, 0], [sy, 0, cy]]) ; Rp = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]]); M = Rp @ R
    img = np.zeros((H, W, 3), np.float32); img[:] = bg; zb = np.full((H, W), np.inf, np.float32)
    sun = np.array([0.4, 0.8, 0.45]); sun /= np.linalg.norm(sun)
    for m in meshes:
        P, N, UV, I, tex, col, tile = m[:7]; fol = len(m) > 7 and m[7]
        AO = np.ones(len(P))
        if fol: k = np.floor(UV[:, 0]); AO = 1 - k / 15 * 0.85; UV = UV.copy(); UV[:, 0] -= k
        q = (P - c) @ M.T; sx = (q[:, 0] / ext * 0.5 + 0.5) * W; syy = (0.5 - q[:, 1] / ext * 0.5) * H; z = q[:, 2]
        nl = N @ np.eye(3)
        for t in I:
            a, b, d = t
            xs = np.array([sx[a], sx[b], sx[d]]); ys = np.array([syy[a], syy[b], syy[d]])
            x0, x1 = int(max(0, np.floor(xs.min()))), int(min(W - 1, np.ceil(xs.max()))); y0, y1 = int(max(0, np.floor(ys.min()))), int(min(H - 1, np.ceil(ys.max())))
            if x1 < x0 or y1 < y0: continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
            den = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
            if abs(den) < 1e-9: continue
            w0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / den
            w1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / den
            w2 = 1 - w0 - w1; inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
            if not inside.any(): continue
            zz = w0 * z[a] + w1 * z[b] + w2 * z[d]
            sub = zb[y0:y1 + 1, x0:x1 + 1]; vis = inside & (zz < sub)
            if not vis.any(): continue
            n = w0[..., None] * nl[a] + w1[..., None] * nl[b] + w2[..., None] * nl[d]
            n /= np.linalg.norm(n, axis=-1, keepdims=True) + 1e-9
            if tex is not None:
                u = (w0 * UV[a, 0] + w1 * UV[b, 0] + w2 * UV[d, 0]) * tile[0]; v = (w0 * UV[a, 1] + w1 * UV[b, 1] + w2 * UV[d, 1]) * tile[1]
                th, tw = tex.shape[:2]; tx = (np.mod(u, 1) * (tw - 1)).astype(int); ty = ((1 - np.mod(v, 1)) * (th - 1)).astype(int)
                s = tex[ty, tx]; vis &= s[..., 3] > 0.45; base = s[..., :3]
            else: base = np.broadcast_to(np.array(col, np.float32), gx.shape + (3,))
            if fol:
                ao = w0 * AO[a] + w1 * AO[b] + w2 * AO[d]
                ndl = n @ sun; lam = (np.clip((ndl + 0.35) / 1.35, 0, 1) * 0.8 + 0.3 + 0.1 * n[..., 1]) * ao
                shade = base * lam[..., None] + base * base * 0.5 * ao[..., None]
            else:
                lam = np.abs(n @ sun) * 0.75 + 0.35 + 0.1 * n[..., 1]
                shade = base * lam[..., None]
            sub[vis] = zz[vis]; img[y0:y1 + 1, x0:x1 + 1][vis] = shade[vis]
    return Image.fromarray((np.clip(img, 0, 1) ** (1 / 1.0) * 255).astype(np.uint8))

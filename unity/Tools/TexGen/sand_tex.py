"""Maldivian beach sand for the terrain (Resources/IslandSim/Textures/sand_albedo.jpg, sand_normal.jpg).

Maldivian beaches are crushed coral: almost white with a warm cream cast, very fine grain, scattered pale fragments
of coral and shell (white, blush pink, ivory, a little grey), and soft wind ripples. No dark specks: there is no
volcanic sand. Everything tiles (noise is made in the frequency domain), 1024 px.

    python3 sand_tex.py
"""
import os, numpy as np
from PIL import Image

N = 1024
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../Assets/IslandSim/Resources/IslandSim/Textures")
rng = np.random.default_rng(1207)
fy, fx = np.meshgrid(np.fft.fftfreq(N) * N, np.fft.fftfreq(N) * N, indexing="ij")
fr = np.hypot(fx, fy)


def noise(lo, hi, aniso=(1, 1)):
    """Tileable band-limited noise, frequencies lo..hi cycles per tile, normalised to -1..1."""
    r = np.hypot(fx * aniso[0], fy * aniso[1])
    band = np.exp(-((np.log(np.maximum(r, 1e-3)) - np.log(np.sqrt(lo * hi))) ** 2) / (2 * (np.log(hi / lo) / 2.5) ** 2))
    band[0, 0] = 0
    w = np.fft.ifft2(np.fft.fft2(rng.standard_normal((N, N))) * band).real
    return w / np.abs(w).max()


def ripples():
    """Soft wind ripples: wavy crests about 9 per tile, warped so they wander."""
    y, x = np.mgrid[0:N, 0:N] / N
    warp = noise(1, 4) * 0.9 + noise(3, 8) * 0.25
    ph = 2 * np.pi * (9 * y + 2 * x + warp)
    r = np.sin(ph) + 0.35 * np.sin(2 * ph + 1.3)       # asymmetric crest, like real ripples
    return r / np.abs(r).max() * (0.6 + 0.4 * (noise(1, 3) * 0.5 + 0.5))


def fragments(count, rmin, rmax):
    """Scattered coral and shell bits: mask, colour index and a small bump, all wrapping at the edges."""
    m = np.zeros((N, N)); idx = np.zeros((N, N), int); h = np.zeros((N, N))
    for _ in range(count):
        cx, cy = rng.uniform(0, N, 2); r = rng.uniform(rmin, rmax); k = rng.choice(4, p=[.45, .25, .2, .1])
        ar, ang = rng.uniform(1, 2.2), rng.uniform(0, np.pi)
        x0, x1, y0, y1 = int(cx - r * ar - 2), int(cx + r * ar + 3), int(cy - r * ar - 2), int(cy + r * ar + 3)
        yy, xx = np.mgrid[y0:y1, x0:x1]
        dx, dy = xx - cx, yy - cy
        u = (dx * np.cos(ang) + dy * np.sin(ang)) / (r * ar); v = (-dx * np.sin(ang) + dy * np.cos(ang)) / r
        d = np.hypot(u, v); a = np.clip((1 - d) * r, 0, 1)
        sl = (yy % N, xx % N)
        upd = a > m[sl]
        m[sl] = np.maximum(m[sl], a); idx[sl] = np.where(upd, k, idx[sl]); h[sl] = np.maximum(h[sl], np.sqrt(np.clip(1 - d * d, 0, 1)) * a)
    return m, idx, h


grain = noise(180, 480) * 0.6 + noise(90, 200) * 0.4               # individual grains
mottle = noise(2, 10) * 0.7 + noise(10, 30) * 0.3                   # broad colour drift
rip = ripples()
fm, fi, fh = fragments(1800, 0.8, 2.0)
fm2, fi2, fh2 = fragments(60, 2.2, 4.0)                            # a few larger shell pieces
fm = np.maximum(fm, fm2); fi = np.where(fm2 > 0, fi2, fi); fh = np.maximum(fh, fh2)

base = np.array([234, 221, 194.])                                   # warm coral white (sRGB), cream enough to stay warm under the blue sky light
warm = np.array([226, 208, 176.]); cool = np.array([236, 228, 208.])
t = (mottle * 0.5 + 0.5)[..., None]
col = warm * (1 - t) + cool * t
col = col * 0.5 + base * 0.5
col *= (1 + 0.09 * grain[..., None])                                # grain sparkle
col *= (1 + 0.05 * rip[..., None])                                 # ripple crests catch a little more light
FRAG = np.array([[248, 242, 230], [238, 214, 200], [236, 222, 196], [210, 202, 188.]])   # white, blush pink, ivory, grey
col = col * (1 - fm[..., None] * 0.55) + FRAG[fi] * (fm[..., None] * 0.55)
alb = np.clip(col, 0, 255).astype(np.uint8)

hgt = rip * 1.0 + grain * 0.35 + fh * 1.0
gx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 0.5; gy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 0.5
s = 2.2
nrm = np.dstack([-gx * s, -gy * s, np.ones_like(gx)]); nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
nimg = ((nrm * 0.5 + 0.5) * 255).astype(np.uint8)

Image.fromarray(alb).save(os.path.join(OUT, "sand_albedo.jpg"), quality=93)
Image.fromarray(nimg).save(os.path.join(OUT, "sand_normal.jpg"), quality=93)
print("mean", alb.reshape(-1, 3).mean(0).round(1))

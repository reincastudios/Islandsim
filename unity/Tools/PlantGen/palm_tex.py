# Procedural textures for the coconut palm: frond halves (green and dry), trunk bark with its normal map, coconut skin.
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
rng = np.random.default_rng(3)

def frond(path, dry=False, W=1024, H=512, ss=2):
    w, h = W * ss, H * ss
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    n = 120
    for k in range(n):
        s = 0.1 + 0.86 * k / n; x0 = s * w + rng.uniform(-2, 2) * ss                  # a bare stalk for the first tenth
        env = np.sin(np.clip((s - 0.1) / 0.9, 0, 1) ** 0.75 * np.pi) ** 0.7             # leaflets short at base and tip
        L = h * (0.25 + 0.72 * env) * rng.uniform(0.85, 1.0)
        if dry and rng.random() < 0.18: continue                        # dry fronds lose leaflets
        if not dry and rng.random() < 0.03: L *= 0.5                    # the odd torn leaflet
        slant = L * rng.uniform(0.16, 0.24)                              # leaflets point toward the tip
        wid = (7 + 4 * env) * ss
        steps = 14; pts_l, pts_r = [], []
        for i in range(steps + 1):
            t = i / steps; cx = x0 + slant * t; cy = 8 * ss + L * t
            ww = wid * (np.sin(min(1, t * 1.2) * np.pi) ** 0.5 if t > 0.05 else 0.35)
            pts_l.append((cx - ww / 2, cy)); pts_r.append((cx + ww / 2, cy))
        poly = pts_l + pts_r[::-1]
        if dry:
            base = np.array([0.55, 0.45, 0.30]) * rng.uniform(0.8, 1.1)
        else:
            base = np.array([0.40, 0.57, 0.13]) * rng.uniform(0.84, 1.1) + rng.uniform(-0.03, 0.03, 3)   # sunlit yellow-green
        col = tuple(int(c * 255) for c in np.clip(base, 0, 1)) + (255,)
        d.polygon(poly, fill=col)
        # midrib highlight and a yellowed or browned tip on some leaflets
        mid = [((pts_l[i][0] + pts_r[i][0]) / 2, pts_l[i][1]) for i in range(steps + 1)]
        mc = tuple(int(c * 255) for c in np.clip(base * 1.35 + 0.05, 0, 1)) + (255,)
        d.line(mid[1:], fill=mc, width=max(1, int(1.2 * ss)))
        if rng.random() < (0.5 if dry else 0.22):
            tipc = (150, 125, 70, 255) if not dry else (110, 85, 55, 255)
            j = int(steps * rng.uniform(0.7, 0.88)); d.polygon(pts_l[j:] + pts_r[j:][::-1], fill=tipc)
    # rachis along the top edge
    rc = (150, 140, 60, 255) if not dry else (140, 115, 80, 255)
    d.rectangle([0, 0, w, 10 * ss], fill=rc); d.line([(0, 10 * ss), (w, 10 * ss)], fill=(100, 95, 40, 255), width=2 * ss)
    im = im.resize((W, H), Image.LANCZOS)
    a = np.asarray(im).astype(np.float32)
    # shading: darker toward the rachis (self-shadow), slight noise
    yy = np.linspace(0, 1, H)[:, None]; shade = 0.8 + 0.2 * np.clip(yy * 3, 0, 1)
    a[..., :3] *= shade[..., None] * (0.94 + 0.06 * rng.random((H, W, 1)))
    # bleed colour into transparent pixels so mipmaps do not darken edges
    rgb = Image.fromarray(np.clip(a[..., :3], 0, 255).astype(np.uint8)); alpha = a[..., 3]
    bled = rgb.filter(ImageFilter.MaxFilter(5))
    out = np.where(alpha[..., None] > 10, np.asarray(rgb), np.asarray(bled))
    Image.fromarray(np.dstack([out, alpha.astype(np.uint8)]).astype(np.uint8), 'RGBA').save(path)

def noise(shape, scale, oct=4):
    h, w = shape; acc = np.zeros(shape)
    for o in range(oct):
        f = scale * 2 ** o; g = rng.random((int(h / f) + 2, int(w / f) + 2))
        im = Image.fromarray((g * 255).astype(np.uint8)).resize((w + int(2 * f), h + int(2 * f)), Image.BICUBIC)
        acc += np.asarray(im)[:h, :w] / 255.0 / 2 ** o
    return acc / (2 - 2 ** (1 - oct))

def bark(path_c, path_n, W=512, H=1024):
    # rings of old leaf scars every ~1/22 of the height, vertical fissures, grey-brown
    y = np.arange(H)[:, None] / H; x = np.arange(W)[None, :] / W
    ring_f = 22; ph = y * ring_f + 0.08 * np.sin(x * 2 * np.pi * 2 + y * 9) + 0.05 * noise((H, W), 32)
    fr = ph - np.floor(ph); ring = np.exp(-((fr - 0.12) / 0.06) ** 2) + 0.5 * np.exp(-((fr - 0.2) / 0.05) ** 2)
    fis = noise((H, W), 6, 3); fis = np.abs(np.sin((x * 40 + fis * 3) * np.pi)) ** 6
    n = noise((H, W), 24, 5)
    hgt = 0.6 - 0.45 * ring - 0.25 * fis * (0.5 + n) + 0.15 * n
    base = np.array([0.56, 0.53, 0.48]); dark = np.array([0.33, 0.30, 0.26]); light = np.array([0.72, 0.70, 0.65])   # weathered grey
    t = np.clip(hgt, 0, 1)[..., None]
    col = dark * (1 - t) + base * t; col = col + (light - base) * np.clip(n[..., None] - 0.55, 0, 1) * 1.5
    col *= 0.9 + 0.2 * noise((H, W), 64, 2)[..., None]
    Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).save(path_c, quality=92)
    gy, gx = np.gradient(hgt); s = 6.0
    nx, ny, nz = -gx * W / 64 * s / 8, gy * H / 64 * s / 8, np.ones_like(hgt)
    l = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2); nm = np.dstack([nx / l, ny / l, nz / l]) * 0.5 + 0.5
    Image.fromarray((nm * 255).astype(np.uint8)).save(path_n, quality=95)

def coconut(path, W=256, H=256):
    n = noise((H, W), 16, 4)
    col = np.array([0.42, 0.46, 0.18]) * (1 - n[..., None] * 0.5) + np.array([0.45, 0.32, 0.16]) * n[..., None] * 0.6
    Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).save(path, quality=92)

if __name__ == '__main__':
    import sys; o = sys.argv[1]
    frond(o + '_g.png'); frond(o + '_d.png', dry=True)
    g, d = Image.open(o + '_g.png'), Image.open(o + '_d.png'); W, H = g.size; a = Image.new('RGBA', (W, 2 * H), (0, 0, 0, 0))
    # top half green (v 0.52..1), bottom half dry (v 0..0.48); the rows between stay empty
    gs = g.resize((W, int(H * 0.96))); ds = d.resize((W, int(H * 0.96)))
    a.paste(gs, (0, 0)); a.paste(ds, (0, 2 * H - ds.size[1])); a.save(o + 'isim_palm_frond.png')
    import os; os.remove(o + '_g.png'); os.remove(o + '_d.png')
    bark(o + 'isim_palm_bark.jpg', o + 'isim_palm_bark_normal.jpg'); coconut(o + 'isim_coconut.jpg')

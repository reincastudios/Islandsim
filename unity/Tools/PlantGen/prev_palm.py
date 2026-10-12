import sys, numpy as np
from PIL import Image
from palm_geo import palm, VARIANTS
from render import render, load_tex
tex = {'isim_palm_bark': load_tex('tex/isim_palm_bark.jpg'), 'isim_palm_frond': load_tex('tex2/isim_palm_frond.png'),
       'isim_coconut': load_tex('tex/isim_coconut.jpg')}
names = sys.argv[1:] or list(VARIANTS)
for name in names:
    lods = palm(*VARIANTS[name]); row = []
    for li, yaw, pitch in [(0, 0.6, 0.12), (0, 2.0, 0.6), (1, 0.6, 0.12), (2, 0.6, 0.12)]:
        row.append(render([(p[1], p[2], p[3], p[4], tex[p[0]], None, (1, 1)) for p in lods[li]], size=(360, 360), yaw=yaw, pitch=pitch))
    im = Image.new('RGB', (1440, 360)); [im.paste(r, (i * 360, 0)) for i, r in enumerate(row)]; im.save(f'prev_{name}.png')

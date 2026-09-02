#!/usr/bin/env python3
"""spr_<종>_<0..7>.png 8장을 가로 스트립 한 장으로 합친다.

fishgen.py --mode sprites 는 프레임을 낱장으로 뱉는다. 유니티는 스트립 한 장을
슬라이스하는 편이 임포트 설정이 단순하므로 여기서 합친다.

  python3 strip.py out19

출력: sheet_<종>.png (가로 8칸), sprites_preview.png (전 종 나열, 검수용)
셀은 정사각이고 크기가 종마다 다르다 — 128 px/유닛이라 상대 크기가 자동으로 맞는다.
"""
import sys, os
from PIL import Image

SPECIES = ['anchovy', 'salmon', 'mahi', 'squid', 'tuna']
FRAMES = 8

d = sys.argv[1] if len(sys.argv) > 1 else 'out19'
made = []

for k in SPECIES:
    fs = [os.path.join(d, f'spr_{k}_{i}.png') for i in range(FRAMES)]
    missing = [f for f in fs if not os.path.exists(f)]
    if missing:
        print(f'SKIP {k} — 프레임 없음: {missing[0]}')
        continue
    ims = [Image.open(f).convert('RGBA') for f in fs]
    w, h = ims[0].size
    assert all(im.size == (w, h) for im in ims), f'{k}: 프레임 크기가 다르다'
    strip = Image.new('RGBA', (w * FRAMES, h), (0, 0, 0, 0))
    for i, im in enumerate(ims):
        strip.paste(im, (i * w, 0))
    out = os.path.join(d, f'sheet_{k}.png')
    strip.save(out)
    made.append((k, w, h, out))
    print(f'SPRITE {k}  셀 {w}x{h}  스트립 {w*FRAMES}x{h}  -> {out}')

# 검수용 한 장 — 종별 스트립을 세로로 쌓는다
if made:
    W = max(w * FRAMES for _, w, _, _ in made)
    H = sum(h for _, _, h, _ in made)
    prev = Image.new('RGBA', (W, H), (14, 26, 33, 255))
    y = 0
    for _, w, h, out in made:
        prev.paste(Image.open(out), (0, y), Image.open(out))
        y += h
    prev.save(os.path.join(d, 'sprites_preview.png'))
    print(f'PREVIEW {W}x{H} -> {d}/sprites_preview.png')

#!/usr/bin/env python3
"""Lay out PNGs in a labelled grid: contact_sheet.py out.png cols width img1 img2 ..."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
out, cols, width = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
paths = sys.argv[4:]
ims = [Image.open(p).convert("RGB") for p in paths]
w = width // cols
h = int(w * ims[0].height / ims[0].width)
rows = (len(ims) + cols - 1) // cols
sheet = Image.new("RGB", (w * cols, (h + 18) * rows), (20, 20, 24))
d = ImageDraw.Draw(sheet)
for i, (p, im) in enumerate(zip(paths, ims)):
    x, y = (i % cols) * w, (i // cols) * (h + 18)
    sheet.paste(im.resize((w, h), Image.LANCZOS), (x, y + 18))
    d.text((x + 4, y + 3), os.path.basename(p), fill=(230, 230, 230))
sheet.save(out)

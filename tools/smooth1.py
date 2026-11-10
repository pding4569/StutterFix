import sys, os, numpy as np
from PIL import Image
Image.MAX_IMAGE_PIXELS = None
def err(im, f):
    w, h = im.size; a = np.asarray(im).astype(np.float32)
    a[..., :3] *= a[..., 3:4] / 255.0   # premultiplied
    pm = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")
    lo = pm.resize((max(1, w // f), max(1, h // f)), Image.BOX).resize((w, h), Image.BILINEAR)
    d = np.abs(np.asarray(lo).astype(np.int16) - np.asarray(pm).astype(np.int16)).max(axis=2)
    mse = float(((np.asarray(lo).astype(np.float32) - np.asarray(pm).astype(np.float32)) ** 2).mean())
    return d.max(), float(np.percentile(d, 99.9)), 10 * np.log10(255 ** 2 / max(mse, 1e-9)), float((d > 8).mean() * 100)
root = sys.argv[1]; names = sys.argv[2:]
for n in names:
    p = None
    for dp, _, fs in os.walk(root):
        if n in fs: p = os.path.join(dp, n); break
    if not p: print(n, "NOT FOUND"); continue
    im = Image.open(p).convert("RGBA")
    if max(im.size) > 3072: s = 3072 / max(im.size); im = im.resize((int(im.size[0] * s), int(im.size[1] * s)), Image.BOX)
    r2 = err(im, 2); r4 = err(im, 4)
    print("%-34s %5dx%-5d | 1/2: max %3d p99.9 %5.1f PSNR %5.1f >8 %5.2f%% | 1/4: max %3d p99.9 %5.1f PSNR %5.1f >8 %5.2f%%" % (n[:34], im.size[0], im.size[1], *r2, *r4))

"""Offline storage-rounding experiment. No Unity, game launch, or installation.

Requires numpy and Pillow. Values are blend-space values; no sRGB conversion.
UNORM uses nearest-even, half uses IEEE binary16. Hardware blend precision and
tie behaviour are assumptions, not measurements of Unity or a GPU.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image


def texture(path):
    if path.is_file():
        with Image.open(path) as im:
            rgba = np.asarray(im.convert("RGBA"))
        meta = {"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                "size": list(rgba.shape[1::-1]), "rgb_extrema": [int(rgba[..., :3].min()), int(rgba[..., :3].max())]}
    else:
        y, x = np.mgrid[:512, :512]
        a = np.clip((0.48 - np.hypot((x + .5) / 512 - .5, (y + .5) / 512 - .5)) * 256, 0, 1)
        rgba = np.full((512, 512, 4), 255, np.uint8)
        rgba[..., 3] = np.rint(a * 255).astype(np.uint8)
        meta = {"path": "synthetic soft circle", "size": [512, 512]}
    # Radius of the largest centred disk guaranteed opaque under bilinear sampling.
    # Each contributing texel centre is at most one texel away on either axis.
    h, w = rgba.shape[:2]
    distance = .5  # also stay inside UV square
    outer = 0.
    inner_empty = .5
    for row in range(h):
        xs = np.flatnonzero(rgba[row, :, 3] != 255)
        if len(xs):
            distance = min(distance, float(np.hypot((xs + .5) / w - .5, (row + .5) / h - .5).min()))
        xs = np.flatnonzero(rgba[row, :, 3] > 0)
        if len(xs):
            radii = np.hypot((xs + .5) / w - .5, (row + .5) / h - .5)
            outer = max(outer, float(radii.max()))
            inner_empty = min(inner_empty, float(radii.min()))
    meta["opaque_radius_uv"] = max(0, distance - math.hypot(1 / w, 1 / h))
    meta["transparent_outside_radius_uv"] = outer + math.hypot(1 / w, 1 / h)
    meta["transparent_inside_radius_uv"] = max(0, inner_empty - math.hypot(1 / w, 1 / h))
    visible = rgba[..., 3] > 0
    meta["visible_rgb_white"] = bool(np.all(rgba[..., :3][visible] == 255))
    meta["alpha_nonzero_fraction"] = float(visible.mean())
    meta["alpha_opaque_fraction"] = float(np.mean(rgba[..., 3] == 255))
    meta["alpha_rotate90_mismatch_fraction"] = float(np.mean(rgba[..., 3] != np.rot90(rgba[..., 3]))) if h == w else None
    # General-image alternative: conservative 32x32 texel rectangles, extended by
    # one texel on all sides for bilinear taps. Only mixed rectangles need RGBA.
    blocks = {"transparent": 0, "opaque": 0, "mixed": 0}
    for y in range(0, h, 32):
        for x in range(0, w, 32):
            a = rgba[max(0, y - 1):min(h, y + 33), max(0, x - 1):min(w, x + 33), 3]
            kind = "transparent" if a.max() == 0 else "opaque" if a.min() == 255 else "mixed"
            blocks[kind] += 1
    meta["conservative_32texel_blocks"] = blocks
    return rgba, meta


def bilinear(tex, u, v):
    # Normalised Unity-style pixel centres, clamp-to-edge, then quad coverage.
    h, w = tex.shape[:2]
    x, y = u * w - .5, v * h - .5
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = (x - x0).astype(np.float32), (y - y0).astype(np.float32)
    def tap(dx, dy):
        return tex[np.clip(y0 + dy, 0, h - 1), np.clip(x0 + dx, 0, w - 1)].astype(np.float32) / 255
    a = tap(0, 0) * (1 - fx[..., None]) + tap(1, 0) * fx[..., None]
    b = tap(0, 1) * (1 - fx[..., None]) + tap(1, 1) * fx[..., None]
    return a * (1 - fy[..., None]) + b * fy[..., None]


def store(x, fmt, rounding="nearest_even"):
    if fmt == "ARGBHalf":
        return x.astype(np.float16).astype(np.float32)
    scaled = np.clip(x, 0, 1) * 255
    rounded = np.rint(scaled) if rounding == "nearest_even" else np.floor(scaled + .5)
    return rounded.astype(np.float32) / 255


def metrics(a, b):
    delta = np.abs(a.astype(np.float64) - b.astype(np.float64)) * 255
    pixel = delta.max(axis=-1)
    mse = float(np.mean((a.astype(np.float64) - b.astype(np.float64)) ** 2))
    return {"max_8bit_units": float(delta.max()),
            "pixels_gt0_percent": float(100 * np.mean(pixel > 0)),
            "pixels_gt1_percent": float(100 * np.mean(pixel > 1 + 1e-4)),
            "pixels_gt2_percent": float(100 * np.mean(pixel > 2 + 1e-4)),
            "psnr_db": None if mse == 0 else -10 * math.log10(mse)}


def run(args):
    tex, meta = texture(args.texture)
    n = args.size
    yy, xx = np.mgrid[:n, :n].astype(np.float32)
    # Square orthographic view 24 world units; input is a HELLO-like fixture,
    # not an exported game transform sequence. Layer i is drawn before i+1.
    x, y = ((xx + .5) / n - .5) * 24, ((yy + .5) / n - .5) * 24
    backgrounds = {"black": np.zeros((n, n, 4), np.float32), "white": np.ones((n, n, 4), np.float32)}
    pattern = np.empty((n, n, 4), np.float32)
    pattern[..., 0] = ((xx // 16 + yy // 16) % 2) * .8 + .1
    pattern[..., 1] = xx / max(1, n - 1)
    pattern[..., 2] = yy / max(1, n - 1)
    pattern[..., 3] = 1
    backgrounds["pattern"] = pattern
    # Include translucent destination alpha to expose alpha blend differences.
    backgrounds["black"][..., 3] = 1
    states = {}
    for fmt in ("ARGB32", "ARGBHalf"):
        for blend in ("straight", "premultiplied"):
            for bg, values in backgrounds.items():
                initial = store(values, fmt, args.rounding)
                states[(fmt, blend, bg)] = [initial.copy(), initial.copy()]
    trans = np.ones((n, n), np.float32)
    core_pixels = sample_pixels = 0
    analytic_max_error = 0.
    transforms = []
    for i in range(150):
        size = 20 - .1 * i  # 20.0 .. 5.1 (150 layers)
        angle = math.radians(i * .37)
        dx, dy = .18 * math.sin(i * .31), .18 * math.cos(i * .27)
        u = (math.cos(angle) * (x - dx) + math.sin(angle) * (y - dy)) / size + .5
        v = (-math.sin(angle) * (x - dx) + math.cos(angle) * (y - dy)) / size + .5
        covered = (u >= 0) & (u <= 1) & (v >= 0) & (v <= 1)
        sampled = bilinear(tex, u, v)
        a = sampled[..., 3] * np.float32(.02) * covered
        core = covered & ((u - .5) ** 2 + (v - .5) ** 2 < meta["opaque_radius_uv"] ** 2)
        analytic_max_error = max(analytic_max_error, float(np.max(np.abs(sampled[..., 3][core] - 1), initial=0)))
        core_pixels += int(core.sum())
        sample_pixels += int((covered & ~core).sum())
        trans *= 1 - a
        for (fmt, blend, bg), (sequential, folded) in states.items():
            src = sampled.copy()
            src[..., 3] = a if blend == "premultiplied" else a * a
            src[..., :3] *= a[..., None]
            sequential[:] = store(sequential * (1 - a[..., None]) + src, fmt, args.rounding)
            folded[:] = folded * (1 - a[..., None]) + src
        transforms.append({"size": size, "angle_degrees": i * .37, "translation": [dx, dy]})
    args.out.mkdir(parents=True, exist_ok=True)
    rows = []
    for (fmt, blend, bg), (sequential, folded) in states.items():
        folded = store(folded, fmt, args.rounding)
        row = {"format": fmt, "blend": blend, "background": bg,
               "rgba": metrics(sequential, folded), "rgb": metrics(sequential[..., :3], folded[..., :3])}
        # Direct common-white product formula, checked separately from generic RGBA recurrence.
        if meta["visible_rgb_white"]:
            initial = store(backgrounds[bg], fmt, args.rounding)
            product = store(initial[..., :3] * trans[..., None] + (1 - trans[..., None]), fmt, args.rounding)
            row["product_vs_folded_rgb"] = metrics(product, folded[..., :3])
        rows.append(row)
        stem = f"{fmt}-{blend}-{bg}"
        for suffix, values in (("sequential", sequential), ("folded", folded)):
            Image.fromarray(np.rint(np.clip(values, 0, 1) * 255).astype(np.uint8)).save(args.out / f"{stem}-{suffix}.png")
        diff = np.max(np.abs(sequential[..., :3] - folded[..., :3]), axis=-1)
        Image.fromarray(np.clip(diff * 255 * 8, 0, 255).astype(np.uint8)).save(args.out / f"{stem}-diff8x.png")
    report = {"texture": meta, "resolution": [n, n], "rounding": args.rounding,
              "arithmetic": "float32 multiply/add without FMA; blend-space values, no sRGB conversion",
              "layers": transforms, "core_fragments": core_pixels, "edge_or_outside_disk_fragments": sample_pixels,
              "core_alpha_max_error": analytic_max_error, "results": rows}
    (args.out / "results.json").write_text(json.dumps(report, indent=2, ensure_ascii=False, allow_nan=False) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "layers"}, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--texture", type=Path, default=Path(r"D:\얼불춤 맵 파일\HELLO (BPM) 2026\pt2_circle1.png"))
    p.add_argument("--size", type=int, default=512)
    p.add_argument("--rounding", choices=("nearest_even", "half_up"), default="nearest_even")
    p.add_argument("--out", type=Path, default=Path(__file__).parent / "results")
    args = p.parse_args()
    if args.size < 8 or args.size > 2048:
        p.error("--size must be 8..2048")
    run(args)

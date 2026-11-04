"""Offline conservative alpha>0 hulls, including bilinear Repeat edge support.

Writes diagnostic manifests only; area ratios are not GPU measurements.
"""
import argparse
import importlib.util
import json
from pathlib import Path
from collections import Counter
from PIL import Image, ImageFilter, ImageChops

spec = importlib.util.spec_from_file_location('audit', Path(__file__).with_name('sprite-alpha-audit.py'))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


def capped_hull(points, limit, width, height):
    """Remove supporting edges, intersect their neighbors; never cut a source point.

    Greedy outer approximation, not Humus source or a minimum-area claim.
    """
    original = points
    polygon = list(points)
    if len(points) > 24:
        # Bound offline combinatorics; this is a safe outer rectangle, not an
        # optimal small polygon. No small hull is inferred from a subsample.
        l, r = min(x for x, y in points), max(x for x, y in points)
        b, t = min(y for x, y in points), max(y for x, y in points)
        return [(l, b), (r, b), (r, t), (l, t)]
    def cross(a, b, p):
        return (b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0])
    def area(p):
        return abs(sum(p[i-1][0]*v[1]-v[0]*p[i-1][1] for i, v in enumerate(p)))/2
    while len(polygon) > limit:
        choices = []
        n = len(polygon)
        for i in range(n):
            ordered = polygon[i:] + polygon[:i]
            a, b, c, d = ordered[-1], ordered[0], ordered[1], ordered[2]
            u = (b[0]-a[0], b[1]-a[1])
            v = (d[0]-c[0], d[1]-c[1])
            den = u[0]*v[1]-u[1]*v[0]
            if abs(den) < 1e-10:
                continue
            t = ((c[0]-a[0])*v[1]-(c[1]-a[1])*v[0])/den
            p = (a[0]+t*u[0], a[1]+t*u[1])
            if not (0 <= p[0] <= width and 0 <= p[1] <= height):
                continue
            candidate = [p] + ordered[2:]
            if all(cross(candidate[j-1], q, x) >= -1e-6
                   for j, q in enumerate(candidate) for x in original):
                choices.append((area(candidate), candidate))
        if not choices:
            return [(0, 0), (width, 0), (width, height), (0, height)]
        polygon = min(choices, key=lambda c: c[0])[1]
    return polygon


def geometry(path, pad):
    with Image.open(path) as src:
        rgba = src.convert('RGBA')
        a = rgba.getchannel('A')
        w, h = a.size
        # Extend periodically before dilation: alpha at the opposite edge can
        # contribute to a Repeat sample. Pixel-square corners include texel extent.
        extended = Image.new('L', (w + 2*pad, h + 2*pad))
        for dx in (-w, 0, w):
            for dy in (-h, 0, h):
                extended.paste(a, (pad + dx, pad + dy))
        mask = extended.filter(ImageFilter.MaxFilter(2*pad+1)).crop((pad, pad, pad+w, pad+h))
        points = []
        for y in range(h):
            box = mask.crop((0, y, w, y+1)).getbbox()
            if box:
                points.extend((x, yy) for x in (box[0], box[2]) for yy in (h-y-1, h-y))
        hull = audit.hull(points)
        area = abs(sum(hull[i-1][0]*p[1] - p[0]*hull[i-1][1] for i, p in enumerate(hull)))/2
        rgb = ImageChops.lighter(ImageChops.lighter(rgba.getchannel('R'), rgba.getchannel('G')), rgba.getchannel('B'))
        colored_zero = ImageChops.multiply(rgb.point(lambda v: 255 if v else 0), a.point(lambda v: 0 if v else 255)).histogram()[255]
        variants = {}
        for count in (4, 6, 8):
            poly = capped_hull(hull, count, w, h)
            poly_area = abs(sum(poly[i-1][0]*p[1]-p[0]*poly[i-1][1] for i, p in enumerate(poly)))/2
            variants['poly'+str(count)] = dict(xy=[v for p in poly for v in p], ratio=poly_area/(w*h))
        return dict(name=path.name, width=w, height=h, xy=[v for p in hull for v in p], **variants,
                    padded_ratio=area/(w*h), padded_vertices=len(hull), colored_alpha0=colored_zero)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('chart', type=Path)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--pad', type=int, default=2)
    p.add_argument('--min-instances', type=int, default=0)
    args = p.parse_args()
    if not 1 <= args.pad <= 8:
        p.error('pad must be 1..8')
    result = audit.audit(args.chart.resolve(), args.min_instances)
    names = Counter(Path(r['image']).name for r in result['records'])
    images = []
    for r in result['records']:
        if 'hull_area' in r:
            path = args.chart.parent / r['image']
            try:
                r.update(geometry(path, args.pad))
                r['ambiguous_basename'] = names[path.name] != 1
                if not r['ambiguous_basename']:
                    images.append(r)
            except (OSError, ValueError) as e:
                r['hull_error'] = str(e)
    result.update(pad=args.pad, wrap='Repeat', minimum_initial_instances=args.min_instances, images=images)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ('images', 'records')}, ensure_ascii=False))

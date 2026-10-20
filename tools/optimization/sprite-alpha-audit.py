"""Read-only image/map audit; writes only an explicitly named output JSON.

Requires Pillow (already installed here). Hull is a pixel-square convex proxy,
not Unity's Tight tessellator, nor a projected/runtime GPU saving measurement.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import sys
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'decotrack-research'))
from analyze import load


def hull(points):
    points = sorted(set(points))
    if len(points) <= 1:
        return points
    def cross(o, a, b):
        return (a[0]-o[0])*(b[1]-o[1]) - (a[1]-o[1])*(b[0]-o[0])
    halves = []
    for seq in (points, reversed(points)):
        half = []
        for p in seq:
            while len(half) >= 2 and cross(half[-2], half[-1], p) <= 0:
                half.pop()
            half.append(p)
        halves.append(half[:-1])
    return halves[0] + halves[1]


def measure(path):
    before = hashlib.sha256(path.read_bytes()).hexdigest()
    with Image.open(path) as image:
        a = image.convert('RGBA').getchannel('A')
        w, h = a.size
        box = a.getbbox()
        # Per-row extreme pixel-square corners bound every alpha>0 pixel.
        points = []
        if box:
            for y in range(box[1], box[3]):
                row = a.crop((box[0], y, box[2], y+1)).getbbox()
                if row:
                    left, right = box[0]+row[0], box[0]+row[2]
                    points += [(left, y), (right, y), (left, y+1), (right, y+1)]
        polygon = hull(points)
        area = abs(sum(polygon[i-1][0]*p[1] - p[0]*polygon[i-1][1] for i, p in enumerate(polygon))) / 2
        bbox_area = (box[2]-box[0])*(box[3]-box[1]) if box else 0
        nonzero = w*h - a.histogram()[0]
    after = hashlib.sha256(path.read_bytes()).hexdigest()
    if after != before:
        raise RuntimeError('source changed during read: ' + str(path))
    return dict(sha256=before, width=w, height=h, pixels=w*h, nonzero=nonzero,
                bbox=box, bbox_area=bbox_area, bbox_ratio=bbox_area/(w*h),
                hull_area=area, hull_ratio=area/(w*h), hull_vertices=len(polygon),
                hull_triangles=max(0, len(polygon)-2))


def audit(chart):
    original_hash = hashlib.sha256(chart.read_bytes()).hexdigest()
    data = load(chart)
    decorations = [d for d in data.get('decorations', []) if d.get('eventType') == 'AddDecoration']
    # Some schema versions put AddDecoration into actions. Count source instances once.
    decorations += [d for d in data.get('actions', []) if d.get('eventType') == 'AddDecoration']
    counts = Counter(d.get('decorationImage', '') for d in decorations)
    swaps = Counter(d['decorationImage'] for d in data.get('actions', [])
                    if d.get('eventType') != 'AddDecoration' and d.get('decorationImage'))
    records = []
    for name in sorted(set(counts) | set(swaps)):
        row = dict(image=name, instances=counts[name], image_change_events=swaps[name])
        p = (chart.parent / name.replace('\\', '/')).resolve()
        # Keep reads scoped to the selected map directory.
        if not p.is_relative_to(chart.parent.resolve()) or not name:
            row['error'] = 'empty or outside map directory'
        else:
            try:
                row.update(measure(p))
            except (OSError, ValueError) as e:
                row['error'] = str(e)
        records.append(row)
    valid = [r for r in records if 'error' not in r and r['instances']]
    n = sum(r['instances'] for r in valid)
    pixels = sum(r['instances']*r['pixels'] for r in valid)
    def weighted(key):
        return sum(r['instances']*r[key] for r in valid)
    result = dict(chart=str(chart), chart_sha256=original_hash,
        scope='all source AddDecoration instances, initial images; no time/projection/visibility weighting',
        source_instances=sum(counts.values()), measured_instances=n,
        unmeasured_instances=sum(r['instances'] for r in records if 'error' in r),
        unique_references=len(records), measured_images=sum('error' not in r for r in records),
        instance_weighted_bbox_reduction_percent=100*(1-weighted('bbox_ratio')/n),
        texel_area_weighted_bbox_reduction_percent=100*(1-weighted('bbox_area')/pixels),
        instance_weighted_hull_proxy_reduction_percent=100*(1-weighted('hull_ratio')/n),
        texel_area_weighted_hull_proxy_reduction_percent=100*(1-weighted('hull_area')/pixels),
        fullrect_vertices=4*n, fullrect_triangles=2*n,
        hull_proxy_vertices=weighted('hull_vertices'), hull_proxy_triangles=weighted('hull_triangles'),
        records=records)
    assert hashlib.sha256(chart.read_bytes()).hexdigest() == original_hash
    return result


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('chart', type=Path)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    if args.output.resolve().is_relative_to(args.chart.resolve().parent):
        p.error('output must be outside the source map directory')
    result = audit(args.chart.resolve())
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'records'}, ensure_ascii=False, indent=2))

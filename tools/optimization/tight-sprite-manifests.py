"""Compact offline audits into immutable, small runtime geometry fixtures."""
import importlib.util
import json
from pathlib import Path
import shutil

spec = importlib.util.spec_from_file_location('tight', Path(__file__).with_name('tight-sprite-audit.py'))
tight = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tight)
folder = Path('docs/deco-spec/results/evidence')
raw = Path('E:/StutterFixWork/night-results/tight-raw-audits')
raw.mkdir(exist_ok=True)
summaries = []
for key in ('dd', 'wf', 'hl', 's7', 'sh', 'ar'):
    path = folder/('tight-'+key+'.json')
    data = json.loads(path.read_text(encoding='utf-8'))
    if 'records' not in data:
        data = json.loads((raw/path.name).read_text(encoding='utf-8'))
    else:
        shutil.copy2(path, raw/path.name)
    images = []
    weighted = {str(n): 0 for n in (4, 6, 8)}
    padded = 0
    padded_bbox = 0
    instances = sum(r['instances'] for r in data['images'])
    for r in data['images']:
        points = list(zip(r['xy'][::2], r['xy'][1::2]))
        row = {k: r[k] for k in ('name', 'width', 'height', 'sha256')}
        row['file'] = r['image']
        padded += r['instances']*r['padded_ratio']
        if points:
            padded_bbox += r['instances']*(max(x for x,y in points)-min(x for x,y in points))*(max(y for x,y in points)-min(y for x,y in points))/(r['width']*r['height'])
        for n in (4, 6, 8):
            p = tight.capped_hull(points, n, r['width'], r['height'])
            area = abs(sum(p[i-1][0]*v[1]-v[0]*p[i-1][1] for i, v in enumerate(p)))/2
            ratio = area/(r['width']*r['height'])
            row['poly'+str(n)] = dict(xy=[v for point in p for v in point], ratio=ratio)
            weighted[str(n)] += r['instances']*ratio
        if any(len(row['poly'+str(n)]['xy']) >= 6 for n in (4,6,8)) and min(row['poly'+str(n)]['ratio'] for n in (4, 6, 8)) < .8:
            images.append(row)
    summary = dict(map=key, chart=data['chart'], chart_sha256=data['chart_sha256'],
        source_instances=data['source_instances'], measured_instances=data['measured_instances'],
        measured_images=data['measured_images'], runtime_candidate_images=len(images),
        min_initial_instances=data.get('minimum_initial_instances', 0), pad=data['pad'],
        bbox_area_remaining=1-data['instance_weighted_bbox_reduction_percent']/100,
        hull_area_remaining=1-data['instance_weighted_hull_proxy_reduction_percent']/100,
        padded_bbox_area_remaining=padded_bbox/instances,
        padded_hull_area_remaining=padded/instances,
        **{'poly'+k+'_area_remaining': v/instances for k, v in weighted.items()})
    summaries.append(summary)
    # One immutable image record per line keeps the generated diff reviewable.
    header = json.dumps(summary, ensure_ascii=False, indent=2)[:-2]
    records = ',\n    '.join(json.dumps(row, ensure_ascii=False, separators=(',', ':')) for row in images)
    path.write_text(header+',\n  "images": [\n    '+records+'\n  ]\n}\n', encoding='utf-8')
    lines = ['name\twidth\theight\t4ratio\t4xy\t6ratio\t6xy\t8ratio\t8xy']
    for row in images:
        cells = [row['name'], str(row['width']), str(row['height'])]
        for n in (4, 6, 8):
            poly = row['poly'+str(n)]
            cells += [str(poly['ratio']), ','.join(map(str, poly['xy']))]
        lines.append('\t'.join(cells))
    path.with_suffix('.tsv').write_text('\n'.join(lines)+'\n', encoding='utf-8')
(folder/'tight-audit-summary.json').write_text(json.dumps(summaries, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(summaries, ensure_ascii=False))

"""Read actual Arche events offline; never modify maps or launch the game.

Target-zero counts are exact source counts. Start values below are a STATIC
last-target model (Kill(true)), not observed runtime values or frame transitions.
Repeat expansion, checkpoints and other event writers are deliberately excluded.
"""
import collections
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'decotrack-research'))
from analyze import load, tags

path = next(Path('D:/얼불춤 맵 파일/Arche').glob('*FULL.adofai'))
data = load(path)
decs = data['decorations']
index = collections.defaultdict(set)
for i, dec in enumerate(decs):
    for tag in tags(dec.get('tag')):
        index[tag].add(i)

def alpha(value):
    value = value.lstrip('#')
    if len(value) == 6:
        return 1.0
    if len(value) == 8:
        return int(value[6:8], 16) / 255
    raise ValueError('Unsupported color: ' + repr(value))

previous = {'color': [alpha(d.get('color', 'ffffff')) for d in decs],
            'opacity': [d.get('opacity', 100) for d in decs]}
counts = {k: collections.Counter() for k in previous}
actions = sorted(enumerate(data['actions']), key=lambda p: (p[1].get('floor', 0), p[1].get('angleOffset', 0), p[0]))
for _, event in actions:
    if event.get('eventType') != 'MoveDecorations' or event.get('active') is False:
        continue
    targets = set().union(*(index.get(t, set()) for t in tags(event.get('tag'))))
    for prop in counts:
        if event.get(prop) is None:
            continue
        value = alpha(event[prop]) if prop == 'color' else float(event[prop])
        c = counts[prop]
        c['source_events'] += 1
        c['source_target_zero'] += value == 0
        c['positive_duration_events'] += event.get('duration', 0) > 0
        c['positive_duration_target_zero'] += event.get('duration', 0) > 0 and value == 0
        for i in targets:
            start = previous[prop][i]
            c['bindings'] += 1
            c['model_start_zero'] += start == 0
            c['target_zero_bindings'] += value == 0
            c['model_either_endpoint_zero'] += start == 0 or value == 0
            c['model_zero_sign_changes'] += (start == 0) != (value == 0)
            previous[prop][i] = value

result = {'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
          'actions': len(data['actions']), 'decorations': len(decs),
          'repeat_events': sum(a.get('eventType') == 'RepeatEvents' for a in data['actions']),
          'scope': __doc__, 'counts': counts}
for c in counts.values():
    c['source_target_zero_percent'] = 100 * c['source_target_zero'] / c['source_events'] if c['source_events'] else 0
    c['model_either_endpoint_zero_percent'] = 100 * c['model_either_endpoint_zero'] / c['bindings'] if c['bindings'] else 0
out = Path(__file__).resolve().parents[2] / 'docs/handoff/decoengine2-evidence'
out.mkdir(exist_ok=True)
(out / 'arche-alpha.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))

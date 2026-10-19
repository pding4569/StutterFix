"""Read-only map statistics. No game or source modifications. Python stdlib only."""
import collections as C
import hashlib
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent
OUT = ROOT / 'out'

def load(p):
    s = p.read_text(encoding='utf-8-sig')
    # Remove trailing commas only outside JSON strings.
    s = re.sub(r'"(?:\\.|[^"\\])*"|,(\s*[}\]])', lambda m: m[0] if m[1] is None else m[1], s)
    # Observed game file omits the root comma before decorations. Repair only
    # this exact boundary in memory; never write back to the source map.
    s = re.sub(r'(\]\s*)("decorations"\s*:)', r'\1,\2', s)
    try:
        return json.loads(s, strict=False)
    except json.JSONDecodeError as e:
        print('JSON ERROR', p, repr(s[e.pos-150:e.pos+150]))
        raise

def hist(xs):
    return dict(C.Counter(map(str, xs)).most_common())

def dist(xs):
    xs = sorted(xs)
    if not xs: return {'n': 0}
    return {'n': len(xs), 'sum': sum(xs), 'min': xs[0], 'p50': xs[(len(xs)-1)//2], 'p95': xs[int((len(xs)-1)*.95)], 'max': xs[-1],
            'bins': hist('0' if x == 0 else '1' if x == 1 else '2-9' if x < 10 else '10-99' if x < 100 else '100-999' if x < 1000 else '1000+' for x in xs)}

def tags(v): return str(v or '').split()

def analyze(label, p):
    d = load(p)
    acts = d['actions']; decs = d.get('decorations', [])
    md = [a for a in acts if a.get('eventType') == 'MoveDecorations']
    index = C.defaultdict(set)
    for i, a in enumerate(decs):
        for t in tags(a.get('tag')): index[t].add(i)
    hits = [0] * len(decs); counts = []; repeated = set()
    repeats = [a for a in acts if a.get('eventType') == 'RepeatEvents']
    for a in repeats: repeated.update(tags(a.get('tag')))
    props = ['positionOffset','rotationOffset','scale','opacity','color','pivotOffset','parallax','parallaxOffset']
    combos = C.Counter(); reasons = C.Counter(); simple = 0; simplehits = 0
    groups = C.Counter(); floorangles = []; bydeco = C.defaultdict(list)
    for seq, a in enumerate(md):
        targets = set().union(*(index.get(t, set()) for t in tags(a.get('tag'))))
        counts.append(len(targets))
        used = tuple(k for k in props if k in a and a[k] is not None
                     and (not isinstance(a[k], list) or any(v is not None for v in a[k])))
        combos['+'.join(used) or '(none)'] += 1
        timekey = (a['floor'], a.get('angleOffset', 0))
        floorangles.append(timekey)
        groups[(str(timekey), str(a.get('duration', '<default>')), str(a.get('ease', '<default>')))] += 1
        rs = []
        if a.get('relativeTo', '<default>') not in ('Tile', 'Global'): rs.append('relative_or_default')
        if any('random' in k.lower() and v not in (None, 0, '', False) for k,v in a.items()): rs.append('random')
        if set(tags(a.get('eventTag'))) & repeated: rs.append('repeat_tag')
        if any(k not in props + ['floor','eventType','duration','tag','angleOffset','ease','eventTag','relativeTo'] for k in a): rs.append('other_fields')
        for r in rs: reasons[r] += 1
        if not rs: simple += 1; simplehits += len(targets)
        for i in targets:
            hits[i] += 1
            bydeco[i].append((timekey, a.get('duration',0)))
    ordered = sorted(floorangles)
    samefloor = [ordered[i][1]-ordered[i-1][1] for i in range(1,len(ordered)) if ordered[i][0] == ordered[i-1][0]]
    floordelta = [ordered[i][0]-ordered[i-1][0] for i in range(1,len(ordered))]
    return {'label': label, 'path': str(p), 'bytes': p.stat().st_size, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest(),
        'actions': len(acts), 'decorations':len(decs), 'move':len(md), 'types': hist(a['eventType'] for a in acts),
        'tag_count':len(index), 'tag_sizes':dist([len(v) for v in index.values()]), 'target_counts':dist(counts), 'deco_event_counts':dist(hits),
        'duration':hist(a.get('duration','<default>') for a in md), 'ease':hist(a.get('ease','<default>') for a in md),
        'relativeTo':hist(a.get('relativeTo','<default>') for a in md), 'combinations':dict(combos.most_common()),
        'fields':hist(k for a in md for k in a), 'move_eventTag_nonempty':sum(bool(a.get('eventTag')) for a in md),
        'repeat_events':len(repeats), 'repeat_samples':repeats[:8], 'repeat_tag_intersection':sum(bool(set(tags(a.get('eventTag'))) & repeated) for a in md),
        'random_fields':hist(k for a in acts+decs for k,v in a.items() if 'random' in k.lower() and v not in (None,0,'',False)),
        'same_floor_angle_deltas':dist(samefloor), 'floor_deltas':dist(floordelta), 'same_floor_angle_top':hist(samefloor),
        'unique_clock_groups':len(groups), 'clock_group_sizes':dist(list(groups.values())),
        'samples':md[:3], 'settings':{k:d.get('settings',{}).get(k) for k in ['bpm','offset','relativeTo']}}

if __name__ == '__main__':
    base = pathlib.Path('D:/얼불춤 맵 파일')
    paths = [('A', next((base/'Arche').glob('*FULL.adofai'))), ('H',base/'HELLO (BPM) 2026/level.adofai'), ('W',base/'Windflower/Windflower.adofai')]
    OUT.mkdir(parents=True, exist_ok=True)
    result = [analyze(label,p) for label,p in paths]
    (OUT/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    for r in result:
        print(json.dumps({k:v for k,v in r.items() if k not in ('types','fields','duration','ease','combinations','samples','repeat_samples','same_floor_angle_top')},ensure_ascii=False))

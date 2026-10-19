"""Static structural eligibility, not an animation equivalence proof."""
import collections as C
import json
from pathlib import Path
from analyze import load, tags, dist, hist, OUT

BASE = Path('D:/얼불춤 맵 파일')
PATHS = [('A', next((BASE/'Arche').glob('*FULL.adofai'))),
         ('H', BASE/'HELLO (BPM) 2026/level.adofai'),
         ('W', BASE/'Windflower/Windflower.adofai')]
MOTION = ['positionOffset', 'rotationOffset', 'scale']
PROPS = MOTION + ['opacity', 'color', 'pivotOffset', 'parallax', 'parallaxOffset']
CONTROL = {'floor', 'eventType', 'duration', 'tag', 'angleOffset', 'ease', 'eventTag', 'relativeTo', 'active'}
def effective(a, k):
    v = a.get(k)
    return v is not None and (not isinstance(v, list) or any(x is not None for x in v))
def analyze(label, path):
    d = load(path); acts = d['actions']; decs = d.get('decorations', [])
    moves = [a for a in acts if a['eventType'] == 'MoveDecorations']
    idx = C.defaultdict(set)
    for i, a in enumerate(decs):
        for tag in tags(a.get('tag')): idx[tag].add(i)
    repeat_tags = set(t for a in acts if a['eventType']=='RepeatEvents' for t in tags(a.get('tag')))
    by_type = C.Counter(a['eventType'] for a in acts)
    counters = C.Counter(); examples = {}; key_segments = 0; bindings = 0
    combos = C.Counter(); other = C.Counter(); clocks = C.Counter(); payloads = C.Counter()
    intervals = C.defaultdict(list)
    target_order = []
    affine_buckets = C.defaultdict(list)
    for order, a in enumerate(moves):
        targets = set().union(*(idx.get(t, set()) for t in tags(a.get('tag'))))
        props = [k for k in PROPS if effective(a, k)]
        combos['+'.join(props) or '(none)'] += 1
        extra = [k for k in a if k not in CONTROL and k not in PROPS and effective(a,k)]
        other.update(extra)
        repeated = bool(set(tags(a.get('eventTag'))) & repeat_tags)
        no_extra = not extra
        simple = no_extra and not any(effective(a,k) for k in ['pivotOffset','parallax','parallaxOffset']) and 'relativeTo' not in a
        motion = any(effective(a,k) for k in MOTION)
        dur = a.get('duration',0)
        for name, value in [('zero_duration',dur==0), ('positive_duration',dur>0), ('repeat_linked',repeated),
            ('no_target',not targets), ('simple_properties',simple), ('simple_nonrepeat',simple and not repeated),
            ('simple_nonrepeat_zero',simple and not repeated and dur==0),
            ('simple_nonrepeat_positive',simple and not repeated and dur>0),
            ('engine_motion_no_extra',simple and motion),
            ('effective_no_numeric_property',not props),
            ('inactive',a.get('active') is False),
            ('move_random',any('random' in k.lower() and effective(a,k) for k in a))]:
            if value: counters[name] += 1
        if simple and not repeated:
            counters['simple_nonrepeat_bindings'] += len(targets)
            counters['simple_nonrepeat_zero_bindings' if dur==0 else 'simple_nonrepeat_positive_bindings'] += len(targets)
        keys = []
        for k in PROPS:
            if not effective(a,k): continue
            v = a[k]
            if k == 'parallax': keys.append(k)  # one Vector2 record in DecoAnim
            elif k == 'scale' and not isinstance(v,list): keys.extend(['scale0','scale1'])
            else: keys.extend((k+str(j) for j,x in enumerate(v) if x is not None) if isinstance(v,list) else [k])
        key_segments += len(keys)*len(targets); bindings += len(targets)
        for t in targets:
            for key in keys: intervals[(t,key,a['floor'])].append((a.get('angleOffset',0),dur,order))
        clock = (a['floor'],a.get('angleOffset',0),dur,a.get('ease','<default>'))
        clocks[clock] += 1
        payloads[json.dumps({k:v for k,v in a.items() if k not in ['tag','eventTag','floor','angleOffset']},sort_keys=True)] += 1
        target_order.append((clock, tuple(keys)))
        if len(targets) == 1 and props and not extra:
            values = []
            for k in props:
                v = a[k]
                values.extend(v if isinstance(v,list) else [v])
            if all(isinstance(v,(int,float)) and not isinstance(v,bool) for v in values):
                affine_buckets[(clock, tuple(props), len(values))].append((order,tuple(values)))
    overlaps = equal_start = pairs = 0
    for entries in intervals.values():
        entries.sort()
        for prev, cur in zip(entries,entries[1:]):
            pairs += 1
            equal_start += prev[0] == cur[0]
            overlaps += prev[1]>0 and cur[0] < prev[0] + prev[1]*180
    runs = 0; last = None
    for shape in target_order:
        if shape != last: runs += 1
        last = shape
    dec_relative = hist(a.get('relativeTo','<default>') for a in decs)
    affine_events = affine_runs = 0
    for bucket in affine_buckets.values():
        # Non-overlapping runs, original file order within an equal-clock bucket.
        i = 0
        while i+2 < len(bucket):
            delta = tuple(y-x for x,y in zip(bucket[i][1],bucket[i+1][1]))
            j = i+2
            while j < len(bucket) and tuple(y-x for x,y in zip(bucket[j-1][1],bucket[j][1])) == delta:
                j += 1
            if j-i >= 3:
                affine_runs += 1; affine_events += j-i; i = j
            else: i += 1
    return dict(label=label, counters=dict(counters), effective_combinations=dict(combos.most_common()),
        other_fields=dict(other), binding_count=bindings, property_slot_bindings=key_segments,
        same_floor_adjacent_key_pairs=pairs, same_floor_nominal_overlap_pairs=overlaps,
        same_floor_equal_start_pairs=equal_start, unique_payload_templates=len(payloads),
        adjacent_clock_and_key_runs=runs, unique_clocks=len(clocks),
        decoration_relativeTo=dec_relative,
        affine_numeric_candidate_runs=affine_runs, affine_numeric_candidate_events=affine_events,
        related_types={k:v for k,v in by_type.items() if any(t in k for t in ['Repeat','Filter','Conditional','Variable','Program','Follow','Orbit','Input','Speed','Twirl','Pause','Hold','MultiPlanet'])},
        tag_nonempty=sum(bool(tags(a.get('tag'))) for a in decs),
        max_target_tags=max(len(tags(a.get('tag'))) for a in moves))

if __name__ == '__main__':
    rows = [analyze(label,path) for label,path in PATHS]
    (OUT/'details.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
    for r in rows:
        print(json.dumps({k:v for k,v in r.items() if k!='effective_combinations'},ensure_ascii=False))

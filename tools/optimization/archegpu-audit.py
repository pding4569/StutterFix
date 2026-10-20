"""Read-only Arche event audit. No game invocation; optional game floor-time join."""
import argparse
import collections
import csv
import hashlib
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / 'decotrack-research'))
from analyze import load


def audit(chart, timeline=None):
    data = load(chart)
    actions = data['actions']
    relevant = {'SetFilter', 'Bloom', 'MoveCamera', 'ShakeScreen', 'ScreenTile', 'ScreenScroll'}
    control = {'SetSpeed', 'Pause', 'Twirl', 'Hold', 'MultiPlanet', 'RepeatEvents'}
    times = {}
    if timeline:
        expected = hashlib.sha256(chart.read_bytes()).hexdigest()
        if timeline.with_suffix('.sha256').read_text(encoding='utf-8-sig').strip() != expected:
            raise ValueError('timeline chart SHA256 mismatch')
        with timeline.open(encoding='utf-8-sig') as f:
            times = {int(r['floor']): float(r['entry_seconds']) for r in csv.DictReader(f, delimiter='\t')}
    events = []
    for index, event in enumerate(actions):
        if event['eventType'] not in relevant | control:
            continue
        item = {'source_index': index, **event}
        if event['floor'] in times:
            item['floor_entry_seconds'] = times[event['floor']]
            # This is a floor anchor, never falsely presented as event fire time.
            item['floor_anchor_in_258_278'] = 258 <= times[event['floor']] <= 278
        events.append(item)
    anchored = [e for e in events if e.get('floor_anchor_in_258_278')]
    last = {}
    for e in events:
        if e.get('floor_entry_seconds', float('inf')) < 258 and e['eventType'] in relevant:
            key = (e['eventType'], e.get('filter', ''))
            # Preserve source indices: floor anchor cannot resolve angleOffset/repeats.
            if key not in last or (e['floor_entry_seconds'], e['source_index']) > (last[key]['floor_entry_seconds'], last[key]['source_index']):
                last[key] = e
    return {
        'chart_sha256': hashlib.sha256(chart.read_bytes()).hexdigest(),
        'timeline_sha256': hashlib.sha256(timeline.read_bytes()).hexdigest() if timeline else None,
        'timing_status': 'game_floor_anchor_only' if timeline else 'unresolved_no_game_floor_times',
        'timing_limit': 'angleOffset, duration, repeats, disableOthers and pre-window state must be resolved; floor anchors are not firing times',
        'settings': {k: data['settings'].get(k) for k in ('bpm', 'offset')},
        'event_counts': dict(collections.Counter(e['eventType'] for e in events)),
        'floor_anchor_window_counts': dict(collections.Counter(e['eventType'] for e in anchored)),
        'floor_anchor_window': anchored,
        'preceding_source_candidates': list(last.values()),
        'events': events,
    }


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('chart', type=pathlib.Path)
    p.add_argument('--timeline', type=pathlib.Path)
    p.add_argument('-o', required=True, type=pathlib.Path)
    a = p.parse_args()
    result = audit(a.chart, a.timeline)
    a.o.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: result[k] for k in ('chart_sha256', 'timing_status', 'event_counts', 'floor_anchor_window_counts')}, ensure_ascii=False))

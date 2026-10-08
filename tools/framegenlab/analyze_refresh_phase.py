"""Offline native interpolation phase; never camera-pose or image-quality evidence.

Target QPC is reconstructed from the native submitted timestamp and its recorded
timeline delay. Source frame IDs identify the two completed scene timestamps.
Run after performance trials, not alongside the game.
"""
import argparse
import json
import math
from pathlib import Path
from measure_outside import read, stats


def analyze(capture):
    sources = read(capture / 'sources.csv')
    pairs = {int(b['unity_frame']): (float(a['source_s']), float(b['source_s']))
             for a, b in zip(sources, sources[1:])}
    phases = []
    ambiguous = []
    generated = []
    for row in read(capture / 'presents.csv'):
        if row['real'] != '0' or not 5 <= float(row['song_s']) < 45:
            continue
        generated.append(row)
        pair = pairs.get(int(row['unity_frame']))
        delay = float(row.get('timeline_to_submit_ms', 'nan'))
        if pair is None or pair[1] <= pair[0] or not math.isfinite(delay):
            ambiguous.append(float(row['song_s']))
            continue
        target = float(row['present_s']) - delay / 1000
        phase = (target - pair[0]) / (pair[1] - pair[0])
        if not -.00001 <= phase <= 1.00001:
            ambiguous.append(float(row['song_s']))
            continue
        phases.append(max(0, min(1, phase)))
    return dict(
        scope='Native target phase, final song reset, generated outputs at song5..45. Not image motion, physical display phase, or fresh-picture proof.',
        generated=len(generated), matched=len(phases), ambiguous=len(ambiguous),
        ambiguous_song_s=ambiguous,
        phase=stats(phases),
        endpoint_zero=sum(x <= .00001 for x in phases),
        endpoint_one=sum(x >= .99999 for x in phases),
        interior=sum(.00001 < x < .99999 for x in phases),
        bins_0_25_50_75_100=[sum(i / 4 <= x < (i + 1) / 4 for x in phases) for i in range(4)],
        endpoint_scope='1e-5 phase tolerance for timestamp serialization. Endpoint1 repeats the newer true picture; interior is only an eligibility bound, not a quality result.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('capture', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    result = analyze(args.capture)
    args.out.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k != 'ambiguous_song_s'}))

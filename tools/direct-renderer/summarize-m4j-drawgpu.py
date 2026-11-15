"""Rank a drfull drawgpu dump. GPU elapsed intervals include stalls, not busy time.

Usage: py summarize-m4j-drawgpu.py drfull-drawgpu-TIME.tsv [--output result.json]
Each dump has a sibling *-textures.tsv. No game access or launch.
"""
import argparse
import csv
import json
from collections import defaultdict
from pathlib import Path


def summarize(path):
    with path.open(encoding='utf-8-sig', newline='') as f:
        rows = list(csv.DictReader(f, delimiter='\t'))
    if not rows:
        raise ValueError('No completed intervals: check pending/skipped and extend capture; not evidence of zero cost')
    groups, frames = defaultdict(list), defaultdict(list)
    for row in rows:
        us, segment = float(row['gpu_us']), float(row['segment_us'])
        if not 0 <= us <= segment:
            raise ValueError('Invalid GPU time ordering')
        groups[int(row['draw'])].append(us)
        frames[(int(row['frame']), int(row['segment']))].append(row)
    texture_path = path.with_name(path.stem + '-textures.tsv')
    textures = defaultdict(list)
    if texture_path.exists():
        with texture_path.open(encoding='utf-8-sig', newline='') as f:
            for row in csv.DictReader(f, delimiter='\t'):
                textures[int(row['draw'])].append(row)
    ranking = sorted((dict(draw=draw, samples=len(times), mean_us=sum(times)/len(times),
                           max_us=max(times), sum_us=sum(times), textures=textures[draw])
                      for draw, times in groups.items()), key=lambda r: r['sum_us'], reverse=True)
    segments = []
    for (frame, segment), values in frames.items():
        ordinals = [int(v['ordinal']) for v in values]
        if len(set(ordinals)) != len(ordinals):
            raise ValueError('Duplicate ordinal in one frame/segment; do not merge overlapping windows')
        total = float(values[0]['segment_us'])
        if any(float(v['segment_us']) != total for v in values):
            raise ValueError('Segment envelope inconsistent')
        covered = sum(float(v['gpu_us']) for v in values)
        if covered > total + .01:
            raise ValueError('Draw intervals exceed segment envelope')
        segments.append(dict(frame=frame, segment=segment, segment_us=total, covered_draw_us=covered,
                             covered_fraction=covered/total if total else 0,
                             unmeasured_us=total-covered, first_ordinal=min(ordinals), last_ordinal=max(ordinals)))
    return dict(source=str(path), rows=len(rows), ranking=ranking, segments=segments,
                validity='Bounded sampled windows; unmeasured includes prepare, gaps and draws outside window. Different sessions cannot share native IDs. GPU elapsed is not GPU busy/page migration proof.')


def self_test():
    directory = Path(__file__).resolve().parents[2]/'out/direct-renderer/m4j/analyzer-fixture'
    directory.mkdir(parents=True, exist_ok=True)
    path = directory/'synthetic.tsv'
    path.write_text('frame\tsegment\tordinal\tdraw\tgpu_us\tsegment_us\n64\t0\t0\t10\t1000\t41000\n64\t0\t1\t20\t39000\t41000\n', encoding='utf-8')
    result = summarize(path)
    assert result['ranking'][0]['draw'] == 20
    assert result['segments'][0]['unmeasured_us'] == 1000
    empty = directory/'empty.tsv'
    empty.write_text('frame\tsegment\tordinal\tdraw\tgpu_us\tsegment_us\n', encoding='utf-8')
    try:
        summarize(empty)
    except ValueError:
        pass
    else:
        raise AssertionError('Empty probe falsely passed')
    print('PASS M4j draw analyzer synthetic_41ms_dominant_draw=20 unmeasured=1000us empty=rejected hardware=UNMEASURED')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path, nargs='?')
    parser.add_argument('--output', type=Path)
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args()
    if args.self_test:
        self_test()
    else:
        if not args.input:
            parser.error('input dump required')
        result = summarize(args.input)
        encoded = json.dumps(result, ensure_ascii=False, indent=2)
        if args.output:
            args.output.write_text(encoded, encoding='utf-8')
        else:
            print(encoded)

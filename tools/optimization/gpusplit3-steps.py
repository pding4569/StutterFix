"""Print sf_run JSON only. Does not import sfmeasure, install or launch anything."""
import argparse
import json
from pathlib import Path

FILTERS = ['live:invisible', 'live:tiny', 'live:invisible+tiny', 'live:visible']
SAMPLES = [262.1, 264, 265.8]


def rewind():
    return ['gpusplit off', 'gpuprobe off', 'stop', 'seek 262 12',
            'waitsongpos 258 40']


def build(chart, filters=FILTERS):
    steps = ['timeout 180', 'auto on', 'open ' + str(chart), 'archegpu on']
    order = ['none']
    for group in filters:
        order += [group, 'none']
    for group in order:
        steps += rewind()
        steps += ['gpuprobe on', 'archegpu stamp',
                  'gpusplit census' if group == 'none' else 'gpusplit hide ' + group,
                  'waitsongpos 262 20', 'archegpu stamp', 'gpuprobe reset', 'gpusplit reset',
                  'waitsongpos 266 20', 'archegpu stamp', 'gpuprobe state',
                  'gpusplit state', 'gpusplit off', 'gpuprobe off', 'stop']
    # Separate repeat of the identical song section. Readbacks never share GPU windows.
    # First/last baseline renders control for unstable render callbacks within each capture.
    for group in filters:
        steps += rewind()
        for sample in SAMPLES:
            steps += [f'waitsongpos {sample} 20', 'archegpu stamp',
                      'gpusplit pixeldiff ' + group, 'wait 0.2']
        steps += ['gpusplit off', 'stop']
    steps += ['gpuprobe off', 'gpusplit off', 'archegpu off', 'quit']
    return dict(steps=steps, timeout_min=30, tag='gpusplit3-same-262-266')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--chart', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    output = json.dumps(build(args.chart), ensure_ascii=False, indent=2) + '\n'
    if args.output:
        args.output.write_text(output, encoding='utf-8')
    else:
        print(output, end='')

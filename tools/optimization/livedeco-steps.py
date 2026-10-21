"""Generate gate-1 sf_run JSON only; never install, launch or import sfmeasure."""
import argparse
import json
from pathlib import Path


def build(chart):
    steps = ['timeout 180', 'auto on', 'open ' + str(chart),
             'gpuprobe off', 'gpusplit off', 'matshare off', 'livedeco off',
             'archegpu on']
    # Independent repeats: a census stall at 262 must not shift the 270 snapshot.
    # These are census runs, never performance or framecmp evidence.
    for sample in (262, 270):
        steps += ['stop', 'seek 262 12', f'waitsongpos {sample} 40',
                  'archegpu stamp', 'livedeco census', 'livedeco state', 'stop']
    steps += ['livedeco off', 'archegpu off', 'quit']
    return dict(steps=steps, timeout_min=15, tag='livedeco-gate1-262-270')


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

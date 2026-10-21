"""Generate sf_run JSON only. Never install, launch, or import sfmeasure."""
import argparse
import json
from pathlib import Path


def build(chart, apply='gpusplit hide deco-image:small=1', restore='gpusplit off', png=False):
    steps = ['timeout 180', 'auto on', 'open ' + str(chart),
             'gpuprobe off', 'gpusplit off', 'matshare off', 'seek 262 8',
             'waitsongpos 264 40', 'pause', 'framecmp filters off',
             'framecmp ui exclude', 'framecmp mask center', 'framecmp noise N1 N2', 'framecmp grab A',
             apply, 'wait 0.2', 'framecmp grab B', restore, 'wait 0.2',
             'framecmp grab A2', 'framecmp assertzero A A2' + (' png' if png else ''),
             'framecmp diff A B' + (' 0 png' if png else ''),
             'framecmp control order', 'framecmp control hide',
             'framecmp filters on', 'resume', 'framecmp result', 'stop', 'quit']
    return dict(steps=steps, timeout_min=15, tag='framecmp-paused-264')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--chart', type=Path, required=True)
    parser.add_argument('--apply', default='gpusplit hide deco-image:small=1')
    parser.add_argument('--restore', default='gpusplit off')
    parser.add_argument('--png', action='store_true')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    output = json.dumps(build(args.chart, args.apply, args.restore, args.png), ensure_ascii=False, indent=2) + '\n'
    if args.output:
        args.output.write_text(output, encoding='utf-8')
    else:
        print(output, end='')

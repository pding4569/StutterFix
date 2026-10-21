"""Generate Gate 2 steps only; never launch, install, or import sfmeasure."""
import argparse
import json
from pathlib import Path


def build(chart):
    steps = ['timeout 180', 'auto on', 'open ' + str(chart),
             'gpuprobe off', 'gpusplit off', 'matshare off', 'livedeco reset']
    # Setter profiling is a separate baseline window, outside FPS evidence.
    steps += ['stop', 'seek 262 12', 'waitsongpos 262 40',
              'livedeco profile begin', 'waitsongpos 276 40',
              'livedeco profile end', 'stop']
    for time in (262, 270, 276):
        # Independent seeks; atlas creation is outside all performance windows.
        steps += ['livedeco off', 'stop', 'seek 262 12',
                  f'waitsongpos {time} 40', 'pause', 'framecmp clear',
                  'framecmp filters on', 'framecmp ui exclude',
                  'framecmp mask center', 'framecmp noise N1 N2',
                  'framecmp grab A', 'livedeco on', 'wait 0.2',
                  'framecmp grab B', 'livedeco off', 'wait 0.2',
                  'framecmp grab A2', 'framecmp diff A B 0 png',
                  'framecmp diff A A2 0 png', f'livedeco pixel {time} A B A2',
                  'framecmp clear', 'framecmp ui include', 'resume', 'stop']
    for mode in ('off', 'on', 'on', 'off'):
        steps += ['livedeco off', 'stop', 'seek 262 12',
                  'waitsongpos 251 40']
        if mode == 'on':
            steps += ['livedeco on']
        steps += ['waitsongpos 262 40', 'fuse begin', f'livedeco perf begin {mode}',
                  'waitsongpos 276 40', 'livedeco perf end', 'fuse end',
                  'livedeco state', 'livedeco off', 'stop']
    steps += ['livedeco off', 'livedeco result', 'framecmp clear', 'quit']
    return dict(steps=steps, timeout_min=25, tag='livedeco2-B-pixel-262-270-276-ABBA')


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

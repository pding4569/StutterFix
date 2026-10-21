"""Generate Gate-2 A sf_run JSON. Never launch, install or import sfmeasure."""
import argparse
import json
from pathlib import Path


def build(chart):
    steps = ['timeout 180', 'auto on', 'open ' + str(chart),
             'gpuprobe off', 'gpusplit off', 'matshare off', 'livedeco2 reset']
    # ProfApply/ProfSet is a separate baseline, outside pixel and FPS windows.
    steps += ['stop', 'seek 262 12', 'waitsongpos 262 40', 'livedeco2 profilebegin',
              'waitsongpos 276 40', 'livedeco2 profileend', 'stop']
    for sample in (262, 270, 276):
        steps += ['livedeco off', 'stop', f'seek {sample} 12',
                  f'waitsongpos {sample} 40', 'pause', 'framecmp clear',
                  'framecmp filters on', 'framecmp ui exclude', 'framecmp mask center',
                  'framecmp noise N1 N2', 'framecmp assertzero N1 N2',
                  'framecmp grab A', 'livedeco on',
                  'wait 0.2', 'framecmp grab B', 'livedeco off', 'wait 0.2',
                  'framecmp grab A2', 'framecmp diff A B 0', 'framecmp diff A A2 0',
                  f'livedeco2 pixel {sample}', 'resume']
    for index, mode in enumerate(('off', 'on', 'on', 'off')):
        steps += ['livedeco off', 'stop', 'seek 262 12', 'waitsongpos 252 40',
                  f'livedeco {mode}', 'waitsongpos 262 40', 'fuse begin',
                  f'livedeco2 begin {mode} {index}', 'waitsongpos 276 40',
                  'livedeco2 end', 'fuse end', 'livedeco off']
    steps += ['livedeco off', 'livedeco2 result', 'stop', 'quit']
    return dict(steps=steps, timeout_min=20, tag='livedeco-gate2-A-262-270-276-abba')


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

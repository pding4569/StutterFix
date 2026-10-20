"""Print sf_run arguments only; never imports sfmeasure or launches a game."""
import argparse
import json
from pathlib import Path

FILTERS = ['deco-image', 'deco-image:onscreen', 'deco-image:offscreen',
           'deco-image:big=1', 'deco-image:small=1', 'deco-image:additive',
           'deco-image:alpha', 'deco-image:transparent',
           'deco-image:onscreen,big=100', 'deco-image:onscreen,transparent']


def build(chart, groups):
    order = ['none']
    for group in groups:
        order += [group, 'none']  # Each B is sandwiched by two same-section A windows.
    steps = ['timeout 180', 'auto on', 'open ' + str(chart), 'archegpu on']
    for group in order:
        steps += ['gpusplit off', 'gpuprobe off', 'stop', 'seek 262 12',
                  'waitsongpos 258 40', 'gpuprobe on', 'archegpu stamp']
        steps += ['gpusplit census' if group == 'none' else 'gpusplit hide ' + group]
        steps += ['waitsongpos 262 20', 'archegpu stamp', 'gpuprobe reset',
                  'waitsongpos 266 20', 'archegpu stamp', 'gpuprobe state',
                  'gpusplit state', 'gpusplit off', 'gpuprobe off', 'stop']
    steps += ['archegpu off', 'quit']
    return dict(steps=steps, timeout_min=30, tag='gpusplit2-same-262-266')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--chart', type=Path)
    p.add_argument('--groups', nargs='+', default=FILTERS)
    p.add_argument('--output', type=Path)
    args = p.parse_args()
    chart = args.chart or next(Path('D:/얼불춤 맵 파일/Arche').glob('*FULL.adofai'))
    output = json.dumps(build(chart, args.groups), ensure_ascii=False, indent=2)
    if args.output:
        args.output.write_text(output + '\n', encoding='utf-8')
    else:
        print(output)

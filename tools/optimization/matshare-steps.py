"""Generate sf_run JSON only. Never install, launch, or import sfmeasure."""
import argparse
import json
from pathlib import Path


def build(chart):
    steps = ['timeout 180', 'auto on', 'open ' + str(chart), 'archegpu on',
             'stop', 'matshare off', 'matshare census']
    # Apply in stopped editor BEFORE seek/play; each timing window repeats the same section.
    for enabled in (False, True, False):
        steps += ['gpuprobe off', 'stop', 'matshare off']
        if enabled:
            steps += ['matshare on', 'matshare state']
        steps += ['seek 262 12', 'waitsongpos 258 40', 'gpuprobe on',
                  'waitsongpos 262 20', 'archegpu stamp', 'gpuprobe reset',
                  'waitsongpos 266 20', 'archegpu stamp', 'gpuprobe state',
                  'matshare state', 'gpuprobe off', 'stop', 'matshare off']
    # Readbacks and synthetic sorting fixture are isolated from performance windows.
    steps += ['stop', 'matshare on', 'seek 262 12', 'waitsongpos 264 40',
              'archegpu stamp', 'matshare compare', 'matshare state', 'matshare off', 'stop',
              'sortprobe all 50 20261021', 'archegpu off', 'quit']
    return dict(steps=steps, timeout_min=30, tag='matshare-same-262-266')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--chart', type=Path, required=True)
    p.add_argument('--output', type=Path)
    a = p.parse_args()
    output = json.dumps(build(a.chart), ensure_ascii=False, indent=2) + '\n'
    if a.output:
        a.output.write_text(output, encoding='utf-8')
    else:
        print(output, end='')

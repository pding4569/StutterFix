"""Generate Arche diagnostic JSON only; never launch, install, or import sfmeasure."""
import argparse
import json
from pathlib import Path

MODES = ('off', 'pos', 'anim', 'both', 'off', 'both', 'anim', 'pos', 'off')


def build(chart):
    steps = ['timeout 180', 'auto on', 'open ' + str(chart),
             'gpuprobe off', 'gpusplit off', 'matshare off', 'livedeco off',
             'fuse begin', 'fuse end',  # install the timing hooks before any window
             'decofreeze reset']
    for mode in MODES:
        # stop is essential: seek does not restart while already playing.
        # Rebuild from unfrozen state, then allow 4s to settle in this mode.
        steps += ['decofreeze off', 'stop', 'seek 258 8',
                  'waitsongpos 258 40', 'decofreeze ' + mode,
                  'waitsongpos 262 40', 'fuse begin',
                  'waitsongpos 276 40', 'fuse end', 'decofreeze state']
    steps += ['decofreeze off', 'stop', 'decofreeze summary', 'quit']
    return dict(steps=steps, timeout_min=20, tag='decofreeze-Arche-262-276-symmetric')


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

"""모드 managed CPU 시험 계획 생성만 수행한다. 게임 실행·설치 기능 없음."""
import argparse
import json
from pathlib import Path


def window(cached=None):
    steps = [] if cached is None else ['gpudeco latecache ' + ('on' if cached else 'off')]
    steps += ['seek 260 12', 'decobake ready', 'waitsongpos 256 40',
              'gpudeco packets compiled', 'gpudeco live on', 'waitsongpos 260 40',
              'gpudeco live census']
    if cached is not None:
        steps += ['gpudeco latecache validate']
    steps += ['fuse begin', 'onupdate begin', 'phasewindow begin', 'gpudeco micro begin',
              'waitsongpos 270 25', 'gpudeco micro end', 'phasewindow end', 'onupdate end', 'fuse end']
    if cached is not None:
        steps += ['gpudeco latecache validate', 'gpudeco latecache state']
    return steps + ['gpudeco live off', 'stop']


def build(chart, mode='abba'):
    if mode not in ('baseline', 'abba'):
        raise ValueError('unknown mode: ' + mode)
    steps = ['gpudeco costprobe off', 'gpudeco effectcache off', 'timeout 600',
             'auto on', 'decobake on', 'gpudeco off', 'gpudeco reset', 'gpudeco nombp off',
             'gpudeco g2 reset', 'gpudeco nativebatch on', 'gpudeco nativecopy on8masked',
             'gpudeco zeroalpha on',
             'gpudeco bundle E:/StutterFixWork/wt-gpu/tools/gpudeco-lab/evidence/bundle/stutterfix_gpudeco',
             'open ' + str(chart), 'select 0', 'play', 'gpudeco scheduled', 'pause',
             'decobake state', 'decobake ready', 'gpudeco live scope song', 'gpudeco live prepare',
             'resume', 'stop', 'gpuprobe off', 'gpusplit off', 'matshare off', 'livedeco off', 'phasewindow on']
    for cached in ((None, None) if mode == 'baseline' else (False, True, True, False)):
        steps += window(cached)
    steps += ['quit']
    return dict(steps=steps, settings={'FrameGenOutside': 0, 'FrameGenRefresh': False},
                timeout_min=25, tag='modcpu-260-270' if mode == 'baseline' else 'modcpu-260-270-abba-latecache1')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--chart', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--mode', choices=('baseline', 'abba'), default='abba')
    args = parser.parse_args()
    Path(args.output).write_text(json.dumps(build(args.chart, args.mode), ensure_ascii=False, indent=1), encoding='utf-8')


if __name__ == '__main__':
    main()

"""Generate G2 ABBA live playback + stability sf_run steps; never launch or install anything."""
import argparse
import json
from pathlib import Path


def build(chart, bundle=None):
    chart = Path(chart)
    if 'Arche' not in str(chart) or not chart.name.endswith('FULL.adofai'):
        raise ValueError('Arche *FULL.adofai 경로만 허용합니다')
    if bundle is None:
        bundle = Path(__file__).resolve().parents[1] / 'gpudeco-lab/evidence/bundle/stutterfix_gpudeco'
    bundle = Path(bundle).resolve()
    if not bundle.is_file():
        raise ValueError('G2 번들 없음: ' + str(bundle))

    steps = [
        'timeout 420',
        'auto on',
        'decobake on',
        'gpudeco off',
        'gpudeco reset',
        'gpudeco g2 reset',
        'gpudeco bundle ' + str(bundle),
        'open ' + str(chart),
        'select 0',
        'play',
        'gpudeco scheduled',
        'pause',
        'wait 60',
        'decobake state',
        'wait 60',
        'decobake ready',
        'gpudeco live prepare',
        'resume',
        'stop',
        'gpuprobe off',
        'gpusplit off',
        'matshare off',
        'livedeco off',
    ]

    # ABBA windows (off 0, on 1, on 2, off 3): 258->262s warmup (>=3s), 262->276s measurement
    for index, mode in ((0, 'off'), (1, 'on'), (2, 'on'), (3, 'off')):
        steps += [
            'stop',
            'seek 262 12',
            'decobake ready',
            'waitsongpos 258 40',
            'pause',
            f'gpudeco live {mode}',
            f'gpudeco g2 ready {mode}',
            'resume',
            'waitsongpos 262 40',
            'fuse begin',
            f'gpudeco g2 begin {mode} {index}',
            'waitsongpos 276 40',
            'gpudeco g2 end',
            'fuse end',
            'gpudeco live off',
        ]

    # Stability 1 & 2: Pause/resume while gpudeco on + natural song end while gpudeco on
    steps += [
        'stop',
        'seek 272 8',
        'decobake ready',
        'waitsongpos 270 40',
        'pause',
        'gpudeco live on',
        'resume',
        'waitsongpos 272 40',
        'pause',
        'wait 0.2',
        'resume',
        'gpudeco g2 stability pause_resume',
        'waitsong',
        'gpudeco g2 stability song_end',
        'stop',
    ]

    # Stability 3: Retry / restart / editor return while gpudeco on
    steps += [
        'seek 262 12',
        'decobake ready',
        'waitsongpos 258 40',
        'pause',
        'gpudeco live on',
        'resume',
        'waitsongpos 260 40',
        'stop',
        'gpudeco g2 stability retry_restart',
    ]

    # Stability 4: Map reopen -> bake -> prepare -> live on/off -> release & error check
    steps += [
        'open ' + str(chart),
        'select 0',
        'play',
        'gpudeco scheduled',
        'pause',
        'wait 60',
        'decobake state',
        'wait 60',
        'decobake ready',
        'gpudeco live prepare',
        'resume',
        'stop',
        'seek 262 12',
        'decobake ready',
        'waitsongpos 258 40',
        'pause',
        'gpudeco live on',
        'resume',
        'waitsongpos 260 40',
        'gpudeco live off',
        'stop',
        'gpudeco g2 stability reopen_toggle',
        'gpudeco g2 result',
        'decobake off',
        'quit',
    ]

    return dict(
        steps=steps,
        timeout_min=20,
        tag='gpudeco-live-g2-262-276-abba',
        stage='live-g2',
        expected_verdict='MEASURE',
        window_seconds=14,
        warmup_seconds=4,
        reason='Arche 262->276초 ABBA(끔·켬·켬·끔) 실시간 GPU 장식 렌더링 FPS·CPU 비용·안정성 검증',
    )


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--chart', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--bundle', type=Path)
    args = parser.parse_args()
    try:
        value = build(args.chart, bundle=args.bundle)
    except ValueError as error:
        parser.error(str(error))
    output = json.dumps(value, ensure_ascii=False, indent=2) + '\n'
    if args.output:
        args.output.write_text(output, encoding='utf-8')
    else:
        print(output, end='')

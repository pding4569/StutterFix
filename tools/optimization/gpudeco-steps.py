"""Generate export-first sf_run steps only; never launch, install, or claim G1."""
import argparse
import json
from pathlib import Path


def build(chart, export_dir):
    chart, export_dir = Path(chart), Path(export_dir).resolve()
    if 'Arche' not in str(chart) or not chart.name.endswith('FULL.adofai'):
        raise ValueError('Arche *FULL.adofai 경로만 허용합니다')
    steps = ['timeout 180', 'auto on', 'decobake on', 'open ' + str(chart),
             'decobake ready', 'gpuprobe off', 'gpusplit off', 'matshare off',
             'livedeco off', 'gpudeco off', 'gpudeco reset']
    for sample in (262, 270, 276):
        steps += ['stop', f'seek {sample} 12', 'decobake ready',
                  f'waitsongpos {sample} 40', 'pause', 'wait 0.2',
                  f'gpudeco export {export_dir / (str(sample) + ".gpudeco")}', 'resume']
    steps += ['gpudeco off', 'gpudeco result', 'stop', 'decobake off', 'quit']
    return dict(steps=steps, timeout_min=20, tag='gpudeco-export-262-270-276',
                stage='export-first', expected_verdict='ERROR',
                reason='독립 랩의 실제 G0와 번들 검증 전 G1 게임 연결을 하지 않음')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--chart', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--export-dir', type=Path,
                        default=Path(__file__).resolve().parents[1] / 'gpudeco-lab' / 'exports')
    args = parser.parse_args()
    try:
        value = build(args.chart, args.export_dir)
    except ValueError as error:
        parser.error(str(error))
    output = json.dumps(value, ensure_ascii=False, indent=2) + '\n'
    if args.output:
        args.output.write_text(output, encoding='utf-8')
    else:
        print(output, end='')

"""Generate export-first sf_run steps only; never launch, install, or claim G1."""
import argparse
import json
from pathlib import Path
from uuid import uuid4


def build(chart, export_dir=None):
    if export_dir is None:
        export_dir = Path(__file__).resolve().parents[1] / 'gpudeco-lab' / 'exports' / ('run-' + uuid4().hex)
    chart, export_dir = Path(chart), Path(export_dir).resolve()
    if 'Arche' not in str(chart) or not chart.name.endswith('FULL.adofai'):
        raise ValueError('Arche *FULL.adofai 경로만 허용합니다')
    if export_dir.exists() and (not export_dir.is_dir() or any(export_dir.glob('*.gpudeco'))):
        raise ValueError('기존 export 보존: 새 출력 폴더를 지정하세요: ' + str(export_dir))
    steps = ['timeout 420', 'auto on', 'decobake on', 'gpudeco off',
             'gpudeco reset', 'gpudeco prepare', 'open ' + str(chart),
             'select 0', 'play', 'gpudeco scheduled', 'pause',
             'wait 60', 'decobake state', 'wait 60', 'decobake ready',
             'resume', 'stop',
             'gpuprobe off', 'gpusplit off', 'matshare off', 'livedeco off']
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
    parser.add_argument('--export-dir', type=Path, help='새 출력 폴더 (기본: 고유 run 하위 폴더)')
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

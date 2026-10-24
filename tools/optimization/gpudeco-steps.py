"""Generate paused G0/G1 sf_run steps; never launch or install anything."""
import argparse
import json
from pathlib import Path
from uuid import uuid4


def build(chart, export_dir=None, pixels=True, bundle=None, attribution=False):
    if attribution and not pixels:
        raise ValueError('attribution requires paused pixel capture')
    if export_dir is None:
        export_dir = Path(__file__).resolve().parents[1] / 'gpudeco-lab' / 'exports' / ('run-' + uuid4().hex)
    chart, export_dir = Path(chart), Path(export_dir).resolve()
    if 'Arche' not in str(chart) or not chart.name.endswith('FULL.adofai'):
        raise ValueError('Arche *FULL.adofai 경로만 허용합니다')
    if export_dir.exists() and (not export_dir.is_dir() or any(export_dir.glob('*.gpudeco'))):
        raise ValueError('기존 export 보존: 새 출력 폴더를 지정하세요: ' + str(export_dir))
    if bundle is None:
        bundle = Path(__file__).resolve().parents[1] / 'gpudeco-lab/evidence/bundle/stutterfix_gpudeco'
    bundle = Path(bundle).resolve()
    if pixels and not bundle.is_file():
        raise ValueError('G1 번들 없음: ' + str(bundle))
    steps = ['timeout 420', 'auto on', 'decobake on', 'gpudeco off',
             'gpudeco reset', 'gpudeco prepare', 'open ' + str(chart),
             'select 0', 'play', 'gpudeco scheduled', 'pause',
             'wait 60', 'decobake state', 'wait 60', 'decobake ready',
             'resume', 'stop',
             'gpuprobe off', 'gpusplit off', 'matshare off', 'livedeco off']
    if pixels:
        steps += ['gpudeco bundle ' + str(bundle)]
    for index, sample in enumerate((262, 270, 276)):
        steps += ['stop', f'seek {sample} 12', 'decobake ready',
                  f'waitsongpos {sample} 40', 'pause', 'wait 0.2',
                  f'gpudeco export {export_dir / (str(sample) + ".gpudeco")}']
        if pixels:
            steps += ['framecmp mask none', 'framecmp ui exclude', 'framecmp filters on',
                      'framecmp noise N1 N2', 'framecmp grab A', 'gpudeco on',
                      'wait 0.2', 'framecmp grab B', 'gpudeco draw off',
                      'wait 0.2', 'framecmp grab A2', 'framecmp assertzero A A2 png',
                      'framecmp diff A B 0 png', f'gpudeco pixel {index}']
            if attribution and index < 2:
                for mode in ('world', 'single'):
                    steps += [f'gpudeco diagnostic {mode}', 'framecmp grab A', 'gpudeco on',
                              'wait 0.2', 'framecmp grab B', 'gpudeco draw off', 'wait 0.2',
                              'framecmp grab A2', 'framecmp assertzero A A2 png',
                              'framecmp diff A B 0 png', 'gpudeco diagnostic pixel',
                              'gpudeco diagnostic off']
        steps += ['resume']
    steps += ['gpudeco off', 'gpudeco result', 'stop', 'decobake off', 'quit']
    return dict(steps=steps, timeout_min=20, tag='gpudeco-paused-g1-262-270-276' if pixels else 'gpudeco-export-262-270-276',
                stage='paused-attribution' if attribution else 'paused-g1' if pixels else 'export-first', expected_verdict='ERROR' if attribution else 'MEASURE' if pixels else 'ERROR',
                export_schema=2, evaluation_clock='actual-source-progress',
                reason='실제 GPU G0 후 정지 화면 끔/켬/끔 G1; 재생 구동과 G2는 별도' if pixels else 'export와 독립 랩만 측정')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--chart', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--export-dir', type=Path, help='새 출력 폴더 (기본: 고유 run 하위 폴더)')
    parser.add_argument('--export-only', action='store_true')
    parser.add_argument('--bundle', type=Path)
    parser.add_argument('--attribution', action='store_true', help='정점 정답/단일 구간 대조; G1 PASS 금지')
    args = parser.parse_args()
    try:
        value = build(args.chart, args.export_dir, pixels=not args.export_only, bundle=args.bundle, attribution=args.attribution)
    except ValueError as error:
        parser.error(str(error))
    output = json.dumps(value, ensure_ascii=False, indent=2) + '\n'
    if args.output:
        args.output.write_text(output, encoding='utf-8')
    else:
        print(output, end='')

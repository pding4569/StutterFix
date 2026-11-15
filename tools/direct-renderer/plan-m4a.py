"""Generate M4a ABBA/census plans only. Never install DLLs or run the game."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import sys

sys.dont_write_bytecode = True
root = Path(__file__).resolve().parents[2]


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--maps', default='hl50,wf,dd,sh')
    p.add_argument('--rt', default='3440x1440')
    p.add_argument('--output', type=Path, default=root / 'out/direct-renderer/m4/plans')
    args = p.parse_args()
    os.environ['BENCH_WT'] = root.name
    os.environ['BENCH_RT'] = args.rt
    # Ambient benchmark overrides must not silently hide/disable extra content.
    for key in ('BENCH_PRE', 'BENCH_MID', 'BENCH_POST', 'BENCH_GAME'):
        if os.environ.get(key):
            raise SystemExit(f'clear {key} before generating a controlled M4a plan')
    bench_path = root.parent / 'bench_v14.py'
    spec = importlib.util.spec_from_file_location('bench_v14', bench_path)
    bench = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(bench)
    assert bench.WT.resolve() == root.resolve()
    settings, commands = bench.CFG2['ppGpuCam']
    settings = dict(bench.CLEAN, **settings)
    assert settings['GpuDeco'] == 0 and settings['RunLod'] is False
    commands = list(commands) + ['dr2 off', 'drfull off', 'drfull assert off']
    # This runner's renderer presets must remain render-only ceilings.
    assert not any(c.startswith(('hidegroup', 'scriptskip', 'drx ', 'dr bench')) for c in commands)
    args.output.mkdir(parents=True, exist_ok=True)
    manifest = dict(workspace=str(root), branch='claude/drfull', launch=False, preset='ppGpuCam', rt=args.rt,
                    interval_s=12, measure_order=['A1', 'B1', 'B2', 'A2'], plans=[])
    for map_key in args.maps.split(','):
        path, at = bench.MAPS[map_key]
        for label in ('A1', 'B1', 'B2', 'A2', 'census'):
            candidate = label.startswith('B')
            name = f'm4a-{map_key}-{label}'
            mid = list(bench.CFG_MID['ppGpuCam'])
            if label == 'census':
                mid += ['drfull census']
            mid += ['drfull ceil' if candidate else 'drfull off', 'wait 1', 'drfull state', 'drfull assert ceil' if candidate else 'drfull assert off', 'cpuwindow begin']
            post = ['cpuwindow end', 'drfull assert ceil' if candidate else 'drfull assert off', 'drfull state', 'drfull off', 'drfull assert off', 'drfull state'] + list(bench.CFG_POST['ppGpuCam'])
            steps = bench.steps(path, at, name, commands, False, mid, post)
            plan = dict(tag=name, steps=steps, settings=settings, timeout_min=15,
                        measurement=dict(scope='render-only-ceiling' if candidate else 'Unity reference',
                                         song_start_s=at, duration_s=12, candidate=candidate,
                                         census_exclude_from_ABBA=label == 'census'))
            file = args.output / (name + '.json')
            file.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')
            manifest['plans'].append(str(file))
    (args.output / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'PLAN ONLY {len(manifest["plans"])} JSON files; no game/installation operation; {args.output}')


if __name__ == '__main__':
    main()

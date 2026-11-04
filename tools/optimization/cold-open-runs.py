"""Run cold/warm Player fixtures, preserving the original persistent cache.

Invoke from E:/StutterFixWork with no other cache tests running. Each game uses
run_locked.py and restores the installed Player DLL via run_json.py.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path('E:/StutterFixWork')
OUT = ROOT / 'night-results'
MAPS = {
    'dd': ('DDONGSSADA3302 asansis remake/level.adofai', 50),
    'wf': ('Windflower/Windflower.adofai', 258),
    'ar': (str(next(Path('D:/얼불춤 맵 파일/Arche').glob('*.adofai')).relative_to(Path('D:/얼불춤 맵 파일'))), 270),
}


def run(key, mode, warm):
    chart, at = MAPS[key]
    name = f'{a.label}-{key}-{mode}-{warm}'
    steps = ['timeout 900', 'wait 2', 'auto on', 'fpscap 0',
             'coldopen ' + ('legacy' if mode == 'legacy' else 'wait'),
             'game D:/얼불춤 맵 파일/' + chart, 'wait 4', 'coldopen stats',
             'phasewindow on', 'press', f'waitsongpos {at-3} 600', 'gpuprobe on', f'waitsongpos {at-1} 60',
             'gpuprobe reset', 'phasewindow begin', 'fpsmark start', 'wait 8',
             f'fpsmark end {name}', 'phasewindow end', 'gpuprobe state',
             'gpuprobe off', 'phasewindow off', 'coldopen stats', 'wait 1', 'quit']
    fixture = OUT / (name + '.json')
    fixture.write_text(json.dumps(dict(steps=steps, settings={
        'GpuDeco': 0, 'FrameGenOutside': 0, 'FrameGenRefresh': False,
        'LowCompressImages': mode != 'off'}, timeout_min=16, tag=name), ensure_ascii=False), encoding='utf-8')
    print('BEGIN', name, time.strftime('%H:%M:%S'), flush=True)
    result = subprocess.run([sys.executable, str(ROOT/'run_locked.py'), str(fixture),
                             'wt-partial', name, '--probe', '--tail', '2'], cwd=ROOT,
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    (OUT/(name+'.runner.txt')).write_text(result.stdout + result.stderr, encoding='utf-8')
    text = (OUT/(name+'.log')).read_text(encoding='utf-8', errors='replace')
    if result.returncode or '단계 실패' in text or f'[FPS 구간] {name} :' not in text:
        raise RuntimeError(name + ' failed; inspect raw log')
    print('END', name, time.strftime('%H:%M:%S'), flush=True)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--maps', nargs='+', default=list(MAPS))
    p.add_argument('--modes', nargs='+', default=['legacy', 'wait', 'off'])
    p.add_argument('--label', default='cold')
    a = p.parse_args()
    cache = Path(os.environ['LOCALAPPDATA'])/'StutterFix'/'asset-cache'
    backup = cache.with_name('asset-cache-before-cold-open')
    if backup.exists():
        raise RuntimeError('existing backup; recover it first')
    existed = cache.exists()
    if existed:
        cache.rename(backup)
    try:
        for key in a.maps:
            for mode in a.modes:
                for warm in ('first', 'second'):
                    run(key, mode, warm)
                if cache.exists():
                    dest = cache.with_name(f'asset-cache-cold-{key}-{mode}-{int(time.time())}')
                    cache.rename(dest)
                    print('PRESERVED', dest, flush=True)
    finally:
        if cache.exists():
            cache.rename(cache.with_name('asset-cache-interrupted-' + str(int(time.time()))))
        if existed:
            backup.rename(cache)
        print('ORIGINAL_CACHE_RESTORED', flush=True)

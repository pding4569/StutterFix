"""Bracket ordinary-build FPS samples without installing observation hooks while OFF."""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--map', type=Path, required=True)
    p.add_argument('--label', required=True)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--width', type=int, help='Required actual game window width')
    p.add_argument('--height', type=int, default=1440)
    a = p.parse_args()
    a.out.mkdir(parents=True, exist_ok=False)
    binary = HERE.parents[1]/'bin/PlayerAuto/StutterFix.dll'
    digest = hashlib.sha256(binary.read_bytes()).hexdigest()
    result = dict(label=a.label, managed_sha256=digest, modes=[0, 2, 4, 0], runs=[])
    condition = None
    for i, mode in enumerate(result['modes']):
        if hashlib.sha256(binary.read_bytes()).hexdigest() != digest:
            raise RuntimeError('Build changed during comparison')
        out = a.out/f'{i:02d}-{mode}x'
        command = [sys.executable, str(HERE/'normal_smoke.py'), '--map', str(a.map),
                   '--out', str(out), '--mode', str(mode), '--no-shot']
        print(f'START normal {a.label} {i+1}/4 mode={mode}', flush=True)
        child = subprocess.run(command, capture_output=True, text=True, encoding='utf8', errors='replace')
        (a.out/(out.name+'.log')).write_text(child.stdout+child.stderr, encoding='utf8')
        if child.returncode:
            raise RuntimeError('Normal comparison failed; inspect '+out.name+'.log')
        row = json.loads((out/'summary.json').read_text(encoding='utf8'))
        if row['sample'] is None or row['sample']['seconds'] < 34:
            raise RuntimeError('Missing explicit Unity/QPC clock sample')
        found = re.findall(r'\[곡 시작\] 화면: 수직동기 (\d+), 목표 FPS (\d+), (\w+), (\d+)x(\d+) (\d+)Hz, 창 (\d+)x(\d+)', (out/'game.log').read_text(encoding='utf8'))
        if not found or (condition is not None and found[-1] != condition):
            raise RuntimeError('Missing or changed actual game conditions')
        condition = found[-1]
        if a.width is not None and (int(condition[6]), int(condition[7])) != (a.width, a.height):
            raise RuntimeError('Actual game size differs from the requested comparison size')
        result['conditions'] = dict(zip(['vsync', 'target_fps', 'fullscreen_mode', 'width', 'height', 'refresh_hz', 'window_width', 'window_height'], condition))
        result['runs'].append(row)
        (a.out/'summary.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
        print(json.dumps(dict(label=a.label, mode=mode, **row['sample'])), flush=True)
    controls = [r['sample'] for r in result['runs'] if r['mode'] == 0]
    off = sum(r['unity_frames'] for r in controls)/sum(r['seconds'] for r in controls)
    result['pooled_off_unity_fps'] = off
    for row in result['runs']:
        row['source_loss_percent'] = 100*(1-row['sample']['real_fps']/off)
    (a.out/'summary.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf8')


if __name__ == '__main__':
    main()

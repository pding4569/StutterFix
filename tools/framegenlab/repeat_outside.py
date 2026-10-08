"""Research-only fresh-process repeat/visual runs; child restores the exact installation."""
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
    p.add_argument('--modes', default='0,2,4,4,2,0')
    p.add_argument('--seconds', type=int, default=55)
    p.add_argument('--clip', action='store_true')
    p.add_argument('--camera-blend', action='store_true', help='Research-only delayed known-camera interpolation')
    p.add_argument('--screen-border', action='store_true', help='Research-only fixed binary screen border')
    p.add_argument('--image-gate', action='store_true', help='Research-only actual-image agreement and jump limit')
    p.add_argument('--block-flow', action='store_true', help='Research-only GPU image block interpolation')
    p.add_argument('--width', type=int, help='Required actual game width; reject a different condition')
    p.add_argument('--height', type=int, default=1440)
    a = p.parse_args()
    modes = [int(v) for v in a.modes.split(',')]
    if not a.map.is_file() or not modes or any(v != 0 and not 2 <= v <= 8 for v in modes):
        p.error('Existing map and modes OFF or 2..8 required')
    if not 25 <= a.seconds <= 600:
        p.error('25..600 seconds required')
    a.out.mkdir(parents=True, exist_ok=False)
    binary = HERE/'out/outside/StutterFix.dll'
    result = dict(label=a.label, visual_only=a.clip, block_flow=a.block_flow, camera_blend=a.camera_blend, screen_border=a.screen_border, image_gate=a.image_gate, modes=modes,
                  research_binary_sha256=hashlib.sha256(binary.read_bytes()).hexdigest(), runs=[])
    condition = None
    for i, mode in enumerate(modes):
        root = a.out/f'{i:02d}-{mode}x'
        command = [sys.executable, str(HERE/'measure_outside.py'), '--map', str(a.map),
                   '--label', f'{a.label}-{i}-{mode}', '--mode', str(mode),
                   '--seconds', str(a.seconds), '--out', str(root), '--timeout-min', '4']
        if a.clip:
            command.append('--clip')
        if a.camera_blend and mode:
            command.append('--camera-blend')
        if a.screen_border and mode:
            command.append('--screen-border')
        if a.image_gate and mode:
            command.append('--image-gate')
        if a.block_flow and mode:
            command.append('--block-flow')
        print(f'START {a.label} {i+1}/{len(modes)} mode={mode}', flush=True)
        run = subprocess.run(command, capture_output=True, text=True, encoding='utf8', errors='replace')
        (a.out/f'{i:02d}-{mode}x.log').write_text(run.stdout+run.stderr, encoding='utf8')
        if run.returncode:
            raise RuntimeError(f'Child failed; inspect {root.name}.log; no retry: {run.stderr[-600:]}')
        summary = json.loads((root/'summary.json').read_text(encoding='utf8'))
        log = (root/'game.log').read_text(encoding='utf8')
        found = re.findall(r'\[곡 시작\] 화면: 수직동기 (\d+), 목표 FPS (\d+), (\w+), (\d+)x(\d+) (\d+)Hz, 창 (\d+)x(\d+)', log)
        if not found or found[-1][0] != '0':
            raise RuntimeError('Missing actual screen conditions; stop comparison')
        current = found[-1]
        if a.width is not None and (int(current[6]), int(current[7])) != (a.width, a.height):
            raise RuntimeError(f'Required game size {a.width}x{a.height}, observed {current[6]}x{current[7]}; stop comparison')
        if condition is not None and current != condition:
            raise RuntimeError(f'Screen conditions changed: {condition} -> {current}; stop comparison')
        condition = current
        result['conditions'] = dict(zip(['vsync','target_fps','fullscreen_mode','width','height','refresh_hz','window_width','window_height'], current))
        row = dict(index=i, mode=mode, native=summary['native'], safety=summary['safety'])
        result['runs'].append(row)
        (a.out/'repeat.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
        n = row['native']
        print(json.dumps(dict(label=a.label, index=i, mode=mode, source_fps=n['real_fps'],
                              scene_fps=n['scene_fps'], output_fps=n['output_fps'],
                              output_over_source=n['output_fps']/n['real_fps']), ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()

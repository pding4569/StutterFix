"""Fresh-process compatibility/setting smoke tests. Every child restores its installation."""
import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
CASES = [
    ('fsr', 4, 55, {'LowRenderScale': 50, 'LowFsr': True, 'LowSharpUpscale': False, 'LowAutoRes': False}, ['--capture']),
    ('half-suspended', 4, 30, {'LowHalfRender': True}, ['--expect-inactive']),
    ('switch', 0, 40, {}, ['--switch-smoke']),
    ('ui-off', 0, 25, {}, ['--ui-smoke']),
    ('ui-on', 5, 25, {}, ['--ui-smoke']),
]


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--map', type=Path, required=True)
    p.add_argument('--out', type=Path, required=True)
    a = p.parse_args()
    if not a.map.is_file(): p.error('Existing map required')
    a.out.mkdir(parents=True, exist_ok=False)
    result = dict(research_binary_sha256=hashlib.sha256((HERE/'out/outside/StutterFix.dll').read_bytes()).hexdigest(), cases=[])
    for label, mode, seconds, settings, flags in CASES:
        file = a.out/(label+'-settings.json')
        file.write_text(json.dumps(settings), encoding='utf8')
        command = [sys.executable, str(HERE/'measure_outside.py'), '--map', str(a.map),
                   '--out', str(a.out/label), '--label', 'compat-'+label, '--mode', str(mode),
                   '--seconds', str(seconds), '--settings', str(file), '--timeout-min', '4', *flags]
        print('START compatibility '+label, flush=True)
        child = subprocess.run(command, capture_output=True, text=True, encoding='utf8', errors='replace')
        (a.out/(label+'.log')).write_text(child.stdout+child.stderr, encoding='utf8')
        if child.returncode: raise RuntimeError('Compatibility failed; inspect '+label+'.log; no automatic retry')
        case = json.loads((a.out/label/'summary.json').read_text(encoding='utf8'))
        result['cases'].append(case)
        (a.out/'summary.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
        print('PASS compatibility '+label, flush=True)


if __name__ == '__main__': main()

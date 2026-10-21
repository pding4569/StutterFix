"""Offline sequencing/CLI contract; no game execution."""
import importlib.util
import json
import subprocess
import sys
import uuid
from pathlib import Path

path = Path(__file__).with_name('livedeco2-steps.py')
spec = importlib.util.spec_from_file_location('livedeco2_steps', path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
chart = Path('Arche 한글 FULL.adofai')
run = module.build(chart)
steps = run['steps']
assert steps.index('livedeco2 profileend') < steps.index('pause')
assert steps[steps.index('livedeco2 profilebegin')-1] == 'waitsongpos 262 40'
assert steps[steps.index('livedeco2 profileend')-1] == 'waitsongpos 276 40'
assert [s for s in steps if s.startswith('livedeco2 pixel ')] == [
    f'livedeco2 pixel {t}' for t in (262, 270, 276)]
for t in (262, 270, 276):
    i = steps.index(f'livedeco2 pixel {t}')
    a = steps[:i]
    start = max(n for n, s in enumerate(a) if s == 'stop')
    sample = a[start:]
    assert sample[:4] == ['stop', f'seek {t} 12', f'waitsongpos {t} 40', 'pause']
    assert sample.index('framecmp noise N1 N2') < sample.index('framecmp grab A')
    assert sample.index('framecmp grab A') < sample.index('livedeco on') < sample.index('framecmp grab B')
    assert sample.index('framecmp grab B') < sample.index('livedeco off') < sample.index('framecmp grab A2')
assert [s for s in steps if s.startswith('livedeco2 begin ')] == [
    f'livedeco2 begin {mode} {i}' for i, mode in enumerate(('off', 'on', 'on', 'off'))]
for i, mode in enumerate(('off', 'on', 'on', 'off')):
    n = steps.index(f'livedeco2 begin {mode} {i}')
    assert steps[n-2:n] == ['waitsongpos 262 40', 'fuse begin']
    assert steps[n+1:n+5] == ['waitsongpos 276 40', 'livedeco2 end', 'fuse end', 'livedeco off']
    start = max(k for k in range(n) if steps[k] == 'stop')
    assert 'pause' not in steps[start:n+5]
    assert steps[start+1:start+4] == ['seek 262 12', 'waitsongpos 252 40', f'livedeco {mode}']
assert steps[-4:] == ['livedeco off', 'livedeco2 result', 'stop', 'quit']
output = path.parent / ('.livedeco2-steps-' + uuid.uuid4().hex + '.json')
try:
    subprocess.run([sys.executable, str(path), '--chart', str(chart), '--output', str(output)], check=True)
    assert json.loads(output.read_text(encoding='utf-8')) == run
finally:
    output.unlink(missing_ok=True)
print(f'livedeco2 steps: {len(steps)} steps; 3 paused/restored pixels; ABBA unpaused windows; UTF-8 CLI passed')

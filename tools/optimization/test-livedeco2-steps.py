import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import uuid

path = Path(__file__).with_name('livedeco2-steps.py')
spec = importlib.util.spec_from_file_location('livedeco2_steps', path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
chart = Path('E:/fixture/한글 경로/Arche FULL.adofai')
doc = module.build(chart)
steps = doc['steps']
assert steps.count('open ' + str(chart)) == 1
assert steps.count('seek 262 12') == 8
assert steps.count('livedeco profile begin') == steps.count('livedeco profile end') == 1
assert [s for s in steps if s.startswith('livedeco pixel')] == [f'livedeco pixel {t} A B A2' for t in (262, 270, 276)]
assert [s for s in steps if s.startswith('livedeco perf begin')] == ['livedeco perf begin ' + m for m in ('off', 'on', 'on', 'off')]
assert steps.count('fuse begin') == steps.count('fuse end') == steps.count('livedeco perf end') == 4
for i, s in enumerate(steps):
    if s.startswith('livedeco pixel'):
        assert steps[i-6:i-1] == ['framecmp grab B', 'livedeco off', 'wait 0.2', 'framecmp grab A2', 'framecmp diff A B 0 png']
        assert steps[i-1] == 'framecmp diff A A2 0 png'
    if s.startswith('livedeco perf begin'):
        assert steps[i-2:i] == ['waitsongpos 262 40', 'fuse begin']
        assert steps[i+1:i+4] == ['waitsongpos 276 40', 'livedeco perf end', 'fuse end']
        assert 'pause' not in steps[i:i+4]
assert steps[-4:] == ['livedeco off', 'livedeco result', 'framecmp clear', 'quit']
output = path.parent / ('.livedeco2-steps-' + uuid.uuid4().hex + '.json')
try:
    subprocess.run([sys.executable, str(path), '--chart', str(chart), '--output', str(output)], check=True)
    assert json.loads(output.read_text(encoding='utf-8')) == doc
finally:
    output.unlink(missing_ok=True)
print(f'Gate2 steps: {len(steps)} steps; three independent pixel windows; playback ABBA; final off; UTF-8 CLI passed')

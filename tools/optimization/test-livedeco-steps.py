import importlib.util
import json
import subprocess
import sys
import uuid
from pathlib import Path

path = Path(__file__).with_name('livedeco-steps.py')
spec = importlib.util.spec_from_file_location('livedeco_steps', path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
chart = str(Path('E:/fixture/한글 경로/Arche FULL.adofai'))
document = module.build(chart)
steps = document['steps']
assert document['timeout_min'] == 15
assert steps.count('open ' + chart) == 1
assert steps.count('livedeco census') == 2
assert steps.count('seek 262 12') == 2
assert [s for s in steps if s.startswith('waitsongpos')] == ['waitsongpos 262 40', 'waitsongpos 270 40']
for i, step in enumerate(steps):
    if step == 'livedeco census':
        assert steps[i-2].startswith('waitsongpos') and steps[i-1] == 'archegpu stamp'
        assert steps[i+1:i+3] == ['livedeco state', 'stop']
assert not any(s in steps for s in ('pause', 'livedeco on', 'gpuprobe on', 'fuse begin'))
assert not any(s.startswith('framecmp ') for s in steps)
assert steps[-3:] == ['livedeco off', 'archegpu off', 'quit']
output = path.parent / ('.livedeco-steps-' + uuid.uuid4().hex + '.json')
try:
    subprocess.run([sys.executable, str(path), '--chart', chart, '--output', str(output)], check=True)
    assert json.loads(output.read_text(encoding='utf-8')) == document
    console = subprocess.check_output([sys.executable, '-X', 'utf8', str(path), '--chart', chart])
    assert json.loads(console.decode('utf-8')) == document
finally:
    output.unlink(missing_ok=True)
print(f'livedeco steps checks passed: {len(steps)} steps; independent 262/270 census; UTF-8 CLI roundtrip; no gate-2 commands')

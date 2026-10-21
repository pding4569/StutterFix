"""Offline protocol check: no sfmeasure import or game process."""
import importlib.util
import ast
import json
import re
import subprocess
import sys
import uuid
from pathlib import Path

path = Path(__file__).with_name('decofreeze-steps.py')
spec = importlib.util.spec_from_file_location('decofreeze_steps', path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
chart = str(Path('E:/fixture/한글 경로/Arche FULL.adofai'))
doc = module.build(chart)
steps = doc['steps']
assert steps.count('open ' + chart) == 1
reset = steps.index('decofreeze reset')
assert steps[reset-2:reset] == ['fuse begin', 'fuse end']
assert steps.count('seek 258 8') == 9
cursor = reset + 1
observed = []
for _ in range(9):
    window = steps[cursor:cursor+10]
    assert window[:4] == ['decofreeze off', 'stop', 'seek 258 8', 'waitsongpos 258 40']
    observed.append(window[4].removeprefix('decofreeze '))
    assert window[5:] == ['waitsongpos 262 40', 'fuse begin', 'waitsongpos 276 40', 'fuse end', 'decofreeze state']
    cursor += 10
assert observed == ['off', 'pos', 'anim', 'both', 'off', 'both', 'anim', 'pos', 'off']
assert observed == observed[::-1]
assert steps[cursor:] == ['decofreeze off', 'stop', 'decofreeze summary', 'quit']
assert doc['timeout_min'] >= 9 * 40 * 2 / 60
assert all(float(s.split()[-1]) < 180 for s in steps if s.startswith('waitsongpos '))
# Read the real sf_errors pattern as data, without importing sfmeasure/server.
server = ast.parse((path.parents[1] / 'sfmeasure' / 'server.py').read_text(encoding='utf-8'))
pattern = next(ast.literal_eval(n.value.args[0]) for n in server.body
               if isinstance(n, ast.Assign) and any(isinstance(t, ast.Name) and t.id == 'RE_ERR' for t in n.targets))
err = re.compile(pattern)
assert err.search('[StutterFix] [decofreeze] [Error] invalid_window mode=both')
assert not err.search('[StutterFix] [decofreeze] RESULT mode=off decoanim_errors=0 unity_errors=0')
output = path.parent / ('.decofreeze-test-' + uuid.uuid4().hex + '.json')
try:
    subprocess.run([sys.executable, str(path), '--chart', chart, '--output', str(output)], check=True)
    assert json.loads(output.read_text(encoding='utf-8')) == doc
    stdout = subprocess.check_output([sys.executable, '-X', 'utf8', str(path), '--chart', chart])
    assert json.loads(stdout.decode('utf-8')) == doc
finally:
    output.unlink(missing_ok=True)
print(f'PASS: {len(steps)} steps; 9 symmetric windows; stop/seek/reset/warmup; timeout; UTF-8 CLI roundtrip')

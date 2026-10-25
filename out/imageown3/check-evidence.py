import hashlib
import importlib.util
import json
import re
import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'out/imageown3'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


before = module('before', out / 'imageown-steps-before.py')
after = module('after', root / 'tools/optimization/imageown-steps.py')
for mode in ('diag', 'diag1', 'abba'):
    for pose in (False, True):
        for bundle in (before.DEFAULT_BUNDLE, 'X:/validated/bundle'):
            assert before.build('Arche.adofai', mode, bundle, pose) == after.build('Arche.adofai', mode, bundle, pose)
print('legacy explicit diag/diag1/abba full outputs: 12 cases identical')

raw = (root.parent / 'night-results/al-im-2.log').read_text(encoding='utf-8-sig').splitlines()
evidence = (root / 'docs/handoff/im-autoloop-run2-evidence.txt').read_text(encoding='utf-8-sig').splitlines()
selected = [line for line in evidence if line.startswith('[StutterFix]')]
cursor = 0
for line in selected:
    match = next((i for i in range(cursor, len(raw)) if raw[i] == line or
                  (len(line) == 1500 and raw[i].startswith(line))), None)
    assert match is not None, line
    cursor = match + 1
print(f'evidence ordered direct lines matched: {len(selected)}')
diagnostics = [(i+1, s) for i, s in enumerate(raw) if 'GPUIMG_DIAG second=' in s]
assert len(diagnostics) == 3
for line, value in diagnostics:
    fields = dict(re.findall(r'(\w+)=([^ ]+)', value))
    assert fields['gpu_max'] == fields['refreshpose_max'] == fields['cpu_vertices_max'] == '1'
    assert all(fields[k] == '0' for k in ('noise', 'restore', 'cpu_noise', 'pose_noise'))
    assert fields['controls'] == 'True'
    print(f'raw line {line}: {value}')
ownership = [s for s in raw if 'activation_ownership allocated=' in s]
assert len(ownership) == 6 and all('GPU_owned=28523 CPU_fallback=0' in s for s in ownership)
restores = [s for s in raw if 'on=False owned=0 originals_restored=True' in s]
assert len(restores) == 6
for s in restores:
    fields = dict(re.findall(r'(\w+)=([^ ]+)', s))
    assert fields['resumed_recs'] == fields['resume_expected']
assert any('shutdown_disposed=True native_contexts=0' in s for s in raw)
assert not any('IMAGEOWN_WINDOW' in s or 'IMAGEOWN_PIXEL' in s for s in raw)
print('activation count=6 CPU fallback=0; restore count=6 exact; no ABBA/integrated pixels')

old_plan = json.loads((root / 'tools/optimization/out/imageown-diag2-steps.json').read_text(encoding='utf-8-sig'))
chart = next(s[5:] for s in old_plan['steps'] if s.startswith('open '))
plan = after.build(chart)
target = root / 'tools/optimization/out/imageown-abba2-steps.json'
target.write_text(json.dumps(plan, ensure_ascii=False, indent=1), encoding='utf-8')
print(f'new default generated only: {plan["tag"]}, steps={len(plan["steps"])}')
print(f'{target.name} SHA256={hashlib.sha256(target.read_bytes()).hexdigest()}')

test = subprocess.run([sys.executable, str(root / 'tools/optimization/imageown-steps.py'), '--self-test'], capture_output=True)
(out / 'generator-tests.txt').write_bytes(test.stdout + test.stderr)
assert test.returncode == 0
print('generator tests: 8 passed')

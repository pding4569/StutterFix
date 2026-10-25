import hashlib
import importlib.util
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'out/imageown2'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


before = module('imageown_before', out / 'imageown-steps-before.py')
after = module('imageown_after', root / 'tools/optimization/imageown-steps.py')
for mode in ('diag', 'abba'):
    for pose in (False, True):
        for bundle in (before.DEFAULT_BUNDLE, 'X:/validated/bundle'):
            old = before.build('Arche.adofai', mode, bundle, pose)
            new = after.build('Arche.adofai', 'diag1' if mode == 'diag' else mode, bundle, pose)
            assert old == new, (mode, pose, bundle)
print('legacy diag1 and explicit abba full outputs: 8 cases identical')

raw = (root.parent / 'night-results/al-im-1.log').read_text(encoding='utf-8-sig').splitlines()
evidence = (root / 'docs/handoff/im-autoloop-run1-evidence.txt').read_text(encoding='utf-8-sig').splitlines()
selected = [line for line in evidence if line.startswith('[StutterFix]')]
for line in selected:
    assert line in raw or (len(line) >= 1500 and any(s.startswith(line) for s in raw)), line
print(f'evidence direct lines matched to raw: {len(selected)}')
failure = next(i for i, s in enumerate(raw) if "'framecmp noise U UN'" in s)
resume = next(i for i, s in enumerate(raw) if i > failure and 'resume restored continuity_frame=' in s)
guard = next(i for i, s in enumerate(raw) if i > resume and 'original_guard bypass=' in s)
assert failure < resume < guard
assert not any('GPUIMG_DIAG second=' in s for s in raw)
assert not any('[framecmp] diff O G ' in s or '[framecmp] diff O D ' in s or '[framecmp] diff O U ' in s for s in raw)
print(f'failure/resume/original guard raw lines: {failure+1}/{resume+1}/{guard+1}')
print('O/G, O/D, O/U and final diagnostic results: absent')

old_plan = root / 'tools/optimization/out/imageown-diag1-steps.json'
data = json.loads(old_plan.read_text(encoding='utf-8-sig'))
chart = next(s[5:] for s in data['steps'] if s.startswith('open '))
plan = after.build(chart)
target = root / 'tools/optimization/out/imageown-diag2-steps.json'
target.write_text(json.dumps(plan, ensure_ascii=False, indent=1), encoding='utf-8')
print(f'new default generated only: {plan["tag"]}, steps={len(plan["steps"])}')
for path in (old_plan, root / 'tools/optimization/out/imageown-abba1-steps.json', target):
    print(f'{path.name} SHA256={hashlib.sha256(path.read_bytes()).hexdigest()}')

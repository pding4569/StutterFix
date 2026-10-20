import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location('matshare_steps', Path(__file__).with_name('matshare-steps.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
steps = module.build('fixture.adofai')['steps']
assert steps.count('waitsongpos 262 20') == steps.count('waitsongpos 266 20') == 3
assert steps.count('gpuprobe reset') == 3
active = probe = False
windows = []
for i, step in enumerate(steps):
    if step == 'matshare on':
        assert not probe and steps[i-1] == 'stop' or (not probe and steps[i-1] == 'matshare off')
        active = True
    if step == 'matshare off':
        active = False
    if step == 'gpuprobe on':
        probe = True
    if step == 'gpuprobe off':
        probe = False
    if step == 'gpuprobe reset':
        windows.append(active)
    if step == 'matshare compare' or step.startswith('sortprobe '):
        assert not probe
assert windows == [False, True, False]
assert not active and not probe
assert steps.count('open fixture.adofai') == 1
print(f'matshare steps checks passed: {len(steps)} steps; OFF/ON/OFF; isolated compare/fixture')

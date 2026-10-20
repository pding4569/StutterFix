"""Offline structural checks: no game or measurement connector is used."""
import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location('steps3', Path(__file__).with_name('gpusplit3-steps.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
steps = module.build('Arche.adofai')['steps']
assert steps.count('gpuprobe reset') == 9
assert steps.count('gpusplit census') == 5
assert steps.count('gpusplit reset') == 9
assert steps.count('waitsongpos 266 20') == 9
assert steps.count('gpuprobe state') == 9
probe = False
windows = []
for step in steps:
    if step == 'gpuprobe on':
        probe = True
    elif step == 'gpuprobe off':
        probe = False
    elif step == 'gpusplit census' or step.startswith('gpusplit hide '):
        windows.append(step)
    elif step.startswith('gpusplit pixeldiff '):
        assert not probe, 'readback contaminated timing window'
for i, group in enumerate(module.FILTERS):
    assert windows[2*i:2*i+3] == ['gpusplit census', 'gpusplit hide ' + group, 'gpusplit census']
    assert steps.count('gpusplit pixeldiff ' + group) == len(module.SAMPLES)
assert steps[-1] == 'quit'
print(f'gpusplit3 steps checks passed: {len(steps)} steps, 9 timing windows, 12 isolated pixel captures; no game executed')

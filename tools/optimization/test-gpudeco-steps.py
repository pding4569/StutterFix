"""Export-stage sequencing and fail-closed contract, no game execution."""
import importlib.util
from pathlib import Path

source = Path(__file__).with_name('gpudeco-steps.py')
spec = importlib.util.spec_from_file_location('gpudeco_steps', source)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
run = module.build(Path('Arche 한글/Arche FULL.adofai'), Path('exports with spaces'))
steps = run['steps']
assert steps[0] == 'timeout 420'
assert steps.index('gpudeco reset') < steps.index('gpudeco prepare') < next(i for i, s in enumerate(steps) if s.startswith('open '))
opened = next(i for i, s in enumerate(steps) if s.startswith('open '))
assert steps[opened+1:opened+5] == ['wait 60', 'decobake state', 'wait 60', 'decobake ready']
assert all(float(s.split()[1]) <= 60 for s in steps if s.startswith('wait '))
assert steps.index('decobake on') < next(i for i, s in enumerate(steps) if s.startswith('open '))
assert len([s for s in steps if s.startswith('open ')]) == 1
assert run['stage'] == 'export-first' and run['expected_verdict'] != 'PASS'
assert 'gpudeco on' not in steps
assert all('GPUDECO_G1=PASS' not in s for s in steps)
exports = [s for s in steps if s.startswith('gpudeco export ')]
assert len(exports) == 3
for sample, command in zip((262, 270, 276), exports):
    i = steps.index(command)
    assert steps[i-6:i] == ['stop', f'seek {sample} 12', 'decobake ready', f'waitsongpos {sample} 40', 'pause', 'wait 0.2']
    assert command.endswith(f'{sample}.gpudeco') and 'exports with spaces' in command
    assert steps[i+1] == 'resume'
assert steps[-5:] == ['gpudeco off', 'gpudeco result', 'stop', 'decobake off', 'quit']
for path in ('Other FULL.adofai', 'Arche/chart.adofai'):
    try:
        module.build(Path(path), Path('exports'))
    except ValueError:
        pass
    else:
        raise AssertionError('chart restriction absent')
print('gpudeco export steps: PASS (3 paused snapshots; no on/PASS before actual lab gate)')

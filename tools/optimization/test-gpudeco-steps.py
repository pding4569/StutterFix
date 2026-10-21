"""Export-stage sequencing and fail-closed contract, no game execution."""
import importlib.util
from pathlib import Path
from uuid import uuid4

source = Path(__file__).with_name('gpudeco-steps.py')
spec = importlib.util.spec_from_file_location('gpudeco_steps', source)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
run = module.build(Path('Arche 한글/Arche FULL.adofai'), Path('exports with spaces'))
steps = run['steps']
assert steps[0] == 'timeout 420'
assert steps.index('gpudeco reset') < steps.index('gpudeco prepare') < next(i for i, s in enumerate(steps) if s.startswith('open '))
opened = next(i for i, s in enumerate(steps) if s.startswith('open '))
assert steps[opened+1:opened+11] == ['select 0', 'play', 'gpudeco scheduled', 'pause',
                                   'wait 60', 'decobake state', 'wait 60', 'decobake ready', 'resume', 'stop']
assert all(float(s.split()[1]) <= 60 for s in steps if s.startswith('wait '))
assert steps.index('decobake on') < next(i for i, s in enumerate(steps) if s.startswith('open '))
assert len([s for s in steps if s.startswith('open ')]) == 1
assert run['stage'] == 'export-first' and run['expected_verdict'] != 'PASS'
assert run['export_schema'] == 2 and run['evaluation_clock'] == 'actual-source-progress'
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
folder = source.resolve().parents[2] / 'obj' / ('gpudeco steps ' + uuid4().hex)
folder.mkdir(parents=True)
try:
    saved = folder / '262.gpudeco'
    saved.write_bytes(b'preserved truth')
    try:
        module.build(Path('Arche FULL.adofai'), folder)
    except ValueError:
        pass
    else:
        raise AssertionError('existing export accepted before expensive game preparation')
    assert saved.read_bytes() == b'preserved truth'
finally:
    saved.unlink(missing_ok=True)
    folder.rmdir()
a = module.build(Path('Arche FULL.adofai'))
b = module.build(Path('Arche FULL.adofai'))
assert [s for s in a['steps'] if s.startswith('gpudeco export ')] != [s for s in b['steps'] if s.startswith('gpudeco export ')]
print('gpudeco export collision: PASS (unique default; occupied explicit directory rejected; truth preserved)')

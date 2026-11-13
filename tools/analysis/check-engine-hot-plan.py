"""Offline admission and sequencing checks; no runner or game calls."""
import importlib.util
from pathlib import Path

root = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location('check_engine_bench', root / 'bench_v14.py')
b = importlib.util.module_from_spec(spec)
spec.loader.exec_module(b)
for key, knob in b.ENGINE_KNOBS.items():
    cfg, commands = b.CFG2[key]
    assert cfg == b.CFG2['rlNR'][0] and commands == b.CFG2['rlNR'][1]
    steps = b.steps('fixture.adofai', 46, 'offline', commands, False, b.CFG_MID[key], b.CFG_POST[key])
    assert next(i for i, s in enumerate(steps) if s.startswith('waitsongpos')) < steps.index(f'engknob {knob} on') < steps.index('fpsmark start')
    assert steps.index('fpsmark end offline') < steps.index(f'engknob assert {knob}') < steps.index('engknob off')
    accepted = f'[engknob] accepted {knob}'
    restored = f'[engknob] restored {knob} errors=0'
    assert b.scriptpath_admitted(key, accepted + '\n' + restored)
    for invalid in (accepted, restored, accepted + '\n' + restored.replace('errors=0', 'errors=1'),
                    accepted + '\n' + restored + '\n[engknob] restore error mismatch',
                    accepted + '\n' + restored + '\nengknob INVALID: camera changed'):
        assert not b.scriptpath_admitted(key, invalid), (key, invalid)
print(f'PASS: {len(b.ENGINE_KNOBS)} engine configurations, post-seek sequencing and rejected incomplete/failed restoration logs')

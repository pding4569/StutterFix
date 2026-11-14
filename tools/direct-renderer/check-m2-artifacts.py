"""Verify local M2 artifacts and installed-file preservation without running the game."""
from pathlib import Path
import hashlib
import json
import re
import sys

root = Path(__file__).resolve().parents[2]
out = root / 'out/direct-renderer/m2'
native = (root / 'native/sfnative.dll').read_bytes()
managed = ['bin/Release/StutterFix.dll', 'bin/Player/StutterFix.dll', 'bin/PlayerAuto/StutterFix.dll']
embedded = {}
for name in managed:
    data = (root / name).read_bytes()
    embedded[name] = {'sha256': hashlib.sha256(data).hexdigest(), 'contains_current_native': native in data}
assert all(v['contains_current_native'] for v in embedded.values()), embedded
(out / 'embedded-native.json').write_text(json.dumps(embedded, indent=2), encoding='utf-8')

before = json.loads((out / 'installed-before.json').read_text(encoding='utf-8-sig'))
after = [{'Path': entry['Path'], 'Hash': hashlib.sha256(Path(entry['Path']).read_bytes()).hexdigest().upper()} for entry in before]
(out / 'installed-after.json').write_text(json.dumps(after, indent=2), encoding='utf-8')
preserved = all(a['Hash'] == b['Hash'] for a, b in zip(after, before))
paths = [root / 'native/sfnative.dll', *(root / name for name in managed), *(Path(entry['Path']) for entry in before)]
hashes = [{'Path': str(path), 'Hash': hashlib.sha256(path.read_bytes()).hexdigest().upper(), 'Algorithm': 'SHA256'} for path in paths]
(out / 'final-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')

def read(name):
    data = (out / name).read_bytes()
    return data.decode('utf-16') if data.startswith((b'\xff\xfe', b'\xfe\xff')) else data.decode('utf-8-sig')

for name in ['dev', 'player', 'playerauto']:
    log = read(f'build-{name}.log')
    assert re.search(r'(?:오류\s*0개|0 Error)', log), name
assert 'Native exports preserved: 73' in read('exports-result.txt')
assert 'M1b/M1c/M2 offline PASS' in read('fixture.log')

history = {}
for name in ['hl50', 'wf', 'dd', 'sh']:
    source = root.parent / 'night-results' / f'dp6-{name}-drPix.log'
    log = source.read_text(encoding='utf-8-sig', errors='replace')
    history[name] = {'source': str(source), 'prior_m1c_states': re.findall(r'\[dr2\] active=True[^\r\n]+', log),
                     'prior_activation_count': log.count('[직통 장식] 켜짐:'),
                     'is_m2_result': False}
(out / 'prior-m1c-history.json').write_text(json.dumps(history, indent=2, ensure_ascii=False), encoding='utf-8')
print('PASS 3 managed builds contain current native; exports=73; WARP M2 PASS')
print('PASS installed StutterFix/sfnative SHA-256 unchanged (2/2)' if preserved else 'HOLD installed DLL hashes changed during work; before/after saved; no restoration attempted')
print('Previous dp6 logs retained as M1c history, not M2 measurements')
sys.exit(0 if preserved else 2)

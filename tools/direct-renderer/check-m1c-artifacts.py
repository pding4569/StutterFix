"""Read-only artifact/installed-file checks; never launches or changes the game."""
from pathlib import Path
import hashlib
import json
import re

root = Path(__file__).resolve().parents[2]
out = root / 'out/direct-renderer/m1c'
native = (root / 'native/sfnative.dll').read_bytes()
files = ['bin/Release/StutterFix.dll', 'bin/Player/StutterFix.dll', 'bin/PlayerAuto/StutterFix.dll']
embedded = {p: {'contains_current_native': native in (root / p).read_bytes(),
                'sha256': hashlib.sha256((root / p).read_bytes()).hexdigest()} for p in files}
assert all(v['contains_current_native'] for v in embedded.values()), embedded
(out / 'embedded-native.json').write_text(json.dumps(embedded, indent=2), encoding='utf-8')
before = json.loads((out / 'installed-before.json').read_text(encoding='utf-8-sig'))
after = [{'Path': p['Path'], 'Hash': hashlib.sha256(Path(p['Path']).read_bytes()).hexdigest().upper()} for p in before]
assert all(a['Hash'] == b['Hash'] for a, b in zip(after, before)), 'installed DLL changed'
(out / 'installed-after.json').write_text(json.dumps(after, indent=2), encoding='utf-8')

history = {}
for name in ['hl50', 'wf', 'dd', 'sh']:
    path = root.parent / 'night-results' / f'dp5-{name}-drPix.log'
    text = path.read_text(encoding='utf-8-sig', errors='replace')
    # Previous M1b data only. These are not results from the M1c candidate.
    activations = re.findall(r'\[직통 장식\] 켜짐: 맡은 장식 (\d+)개', text)
    history[name] = {'source': str(path), 'activation_counts': {n: activations.count(n) for n in sorted(set(activations))},
                     'boundary_deactivations': text.count('꺼짐: 순서 경계 변경'),
                     'static_full_deactivations': text.count('꺼짐: 정적 값 검증 실패')}
(out / 'm1b-log-history.json').write_text(json.dumps(history, indent=2), encoding='utf-8')
print('PASS current native embedded in 3 managed DLLs; installed 2 DLL hashes unchanged')
print(json.dumps(history, indent=2))

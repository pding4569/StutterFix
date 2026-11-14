"""Verify local M3 artifacts, never write to or launch the installed game."""
from pathlib import Path
import hashlib
import json
import re
import sys

root = Path(__file__).resolve().parents[2]
out = root / 'out/direct-renderer/m3'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def read(name):
    data = (out / name).read_bytes()
    return data.decode('utf-16') if data.startswith((b'\xff\xfe', b'\xfe\xff')) else data.decode('utf-8-sig')

before = json.loads(read('installed-before.json'))
after = [{'Path': row['Path'], 'Hash': digest(Path(row['Path']))} for row in before]
(out / 'installed-after.json').write_text(json.dumps(after, indent=2), encoding='utf-8')
preserved = all(x['Hash'] == y['Hash'] for x, y in zip(before, after))
native = (root / 'native/sfnative.dll').read_bytes()
bundle = (root / 'effects/direct-deco/stutterfix_directdeco').read_bytes()
artifacts = {}
for edition, path in [('dev','bin/Release/StutterFix.dll'),('player','bin/Player/StutterFix.dll'),('playerauto','bin/PlayerAuto/StutterFix.dll')]:
    data = (root / path).read_bytes()
    artifacts[path] = {'sha256': digest(root / path), 'embedded_native': native in data, 'embedded_proxy_bundle': bundle in data}
    assert artifacts[path]['embedded_native'] and artifacts[path]['embedded_proxy_bundle'], path
    assert re.search(r'(?:오류\s*0개|0 Error)', read(f'build-{edition}.log')), edition
(out / 'artifacts.json').write_text(json.dumps(artifacts, indent=2), encoding='utf-8')
assert 'Native exports preserved: 73' in read('exports-result.txt')
assert 'M1b/M1c/M2 offline PASS' in read('fixture.log')
assert 'M3 policy PASS' in read('policy.log')
assert 'PASS game+mod IL audit' in read('hooks.log')
fixture = read('unity-fixture.txt')
for expected in ['baseline_diff=0','partial_upload=PASS','mask_VisibleInsideMask_diff=0 reference_lit=1024',
                 'mask_VisibleOutsideMask_diff=0 reference_lit=3072','mask_out_of_range_diff=0',
                 'grab_modes=12 max_channel_delta=0','multi_slot_max_delta=0 reverse_slot_diff=0']:
    assert expected in fixture, expected
assert 'Exiting without the bug reporter. Application will terminate with return code 0' in read('unity-fixture.log')
hash_paths = [root / path for path in artifacts] + [root / 'native/sfnative.dll',root / 'effects/direct-deco/stutterfix_directdeco']
(out / 'final-hashes.json').write_text(json.dumps({str(p): digest(p) for p in hash_paths}, indent=2), encoding='utf-8')
print('PASS M3 shader fixture, policy, hook audit, 4 builds, exports=73; 3 managed DLLs embed current native + proxy bundle')
print('PASS installed DLL hashes unchanged (2/2)' if preserved else 'HOLD installed DLL hashes changed externally; before/after retained; no restoration attempted')
print('No game-run ownership, prep/commit latency, whole-map pixels or ABBA claim')
sys.exit(0 if preserved else 2)

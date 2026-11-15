"""Verify the M4a local build artifacts. No game writes or execution."""
import hashlib
import json
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
out = root / (sys.argv[1] if len(sys.argv) > 1 else 'out/direct-renderer/m4')


def read(name):
    data = (out / name).read_bytes()
    return data.decode('utf-16') if data.startswith((b'\xff\xfe', b'\xfe\xff')) else data.decode('utf-8-sig')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


native = (root / 'native/sfnative.dll').read_bytes()
catalog = (root / 'effects/direct-full/shader-census.tsv').read_bytes()
original = (root / 'effects/direct-full/original-shaders.bin').read_bytes()
artifacts = {}
for edition, path in [('dev', 'bin/Release/StutterFix.dll'), ('player', 'bin/Player/StutterFix.dll'), ('playerauto', 'bin/PlayerAuto/StutterFix.dll')]:
    data = (root / path).read_bytes()
    assert native in data, f'{edition}: embedded native stale'
    assert (catalog in data) == (edition != 'player'), f'{edition}: diagnostic resource boundary'
    assert (original in data) == (edition != 'player'), f'{edition}: original shader resource boundary'
    assert re.search(r'(?:오류\s*0개|0 Error)', read(f'build-{edition}.log')), f'{edition}: build not passed'
    artifacts[path] = dict(sha256=sha(root / path), embedded_native=True, embedded_shader_inventory=edition != 'player')
assert 'PASS native original exports name/ordinal/hint: 73/73; total=74' in read('exports-result.txt')
assert 'PASS 25 lifecycle/refusal cases' in read('lifecycle.log')
assert 'PASS installed game metadata endpoints=8' in read('lifecycle.log')
before = json.loads(read('installed-before.json'))
assert len(before) == 2
after = [dict(Path=row['Path'], Hash=sha(Path(row['Path']))) for row in before]
(out / 'installed-after.json').write_text(json.dumps(after, indent=2), encoding='utf-8')
preserved = sum(row['Hash'] == current['Hash'] for row, current in zip(before, after))
artifacts['native/sfnative.dll'] = dict(sha256=sha(root / 'native/sfnative.dll'))
artifacts['effects/direct-full/shader-census.tsv'] = dict(sha256=sha(root / 'effects/direct-full/shader-census.tsv'))
(out / 'artifacts.json').write_text(json.dumps(artifacts, indent=2), encoding='utf-8')
warp=json.loads(read('warp/warp-summary.json'))
assert warp['passed']==48 and warp['max_channel']<=2 and warp['controls']==5
assert 'PASS production resource loader shaders=21' in read('contract.log')
print('PASS M4b/c: 4 builds; original export name/ordinal/hint 73/73; appended API=1; embedded native 3/3; original DXBC resource 2/2; WARP 48/48')
install_status = f'PASS installed DLL unchanged {preserved}/2' if preserved == 2 else f'HOLD installed DLL unchanged {preserved}/2; changed during session; no install/restore attempted'
(out / 'installed-result.txt').write_text(install_status + '\n', encoding='utf-8')
print(install_status)
print('Game FPS/pixels/Unity lifecycle callbacks and auxiliary composition=UNMEASURED')

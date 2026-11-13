"""Read build outputs only: export preservation and managed edition isolation."""
import json
import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(root / 'out/engine-hot/deps'))
import pefile

def exports(data):
    return {x.name.decode(): x.ordinal for x in pefile.PE(data=data).DIRECTORY_ENTRY_EXPORT.symbols if x.name}
baseline = sys.argv[1] if len(sys.argv)>1 else 'a9244d6'
old = exports(subprocess.check_output(['git', 'show', baseline+':native/sfnative.dll'], cwd=root))
new = exports((root / 'native/sfnative.dll').read_bytes())
assert old.keys() <= new.keys(), 'An existing named export was removed'
assert all(new[name] == ordinal for name, ordinal in old.items()), 'An existing ordinal changed'
extra = sorted(new.keys() - old.keys())
assert extra == sorted(['sf_presentpath_event_ptr', 'sf_presentpath_pin', 'sf_presentpath_pending', 'sf_presentpath_disable', 'sf_presentpath_stat'])
editions = {}
embedded_native = {}
native_data = (root / 'native/sfnative.dll').read_bytes()
for name, path in [('AUTOTEST', 'bin/PlayerAuto/StutterFix.dll'), ('Player', 'bin/Player/StutterFix.dll'), ('DEV', 'bin/Release/StutterFix.dll')]:
    data = (root / path).read_bytes()
    editions[name] = b'PresentPath\x00' in data
    assert (b'BufferResize\x00' in data) == (name=='AUTOTEST'), 'BufferResize edition leak'
    assert (b'ScreenPrefs\x00' in data) == (name=='AUTOTEST'), 'ScreenPrefs edition leak'
    embedded_native[name] = native_data in data
    assert embedded_native[name], 'Stale/missing embedded sfnative: '+name
assert editions == {'AUTOTEST': True, 'Player': False, 'DEV': False}, editions
head = exports(subprocess.check_output(['git', 'show', 'HEAD:native/sfnative.dll'], cwd=root))
assert all(new.get(name)==ordinal for name,ordinal in head.items()), 'HEAD export changed'
result = {'baseline': baseline, 'existing_named_exports_preserved': len(old), 'existing_ordinals_preserved': True, 'HEAD_named_exports_preserved':len(head), 'new_exports': extra, 'PresentPath_type_present': editions, 'current_native_embedded':embedded_native}
(root / 'out/present-path/artifact-check.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))

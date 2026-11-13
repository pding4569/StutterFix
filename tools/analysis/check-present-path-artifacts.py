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
old = exports(subprocess.check_output(['git', 'show', 'HEAD:native/sfnative.dll'], cwd=root))
new = exports((root / 'native/sfnative.dll').read_bytes())
assert old.keys() <= new.keys(), 'An existing named export was removed'
assert all(new[name] == ordinal for name, ordinal in old.items()), 'An existing ordinal changed'
extra = sorted(new.keys() - old.keys())
assert extra == sorted(['sf_presentpath_event_ptr', 'sf_presentpath_pin', 'sf_presentpath_pending', 'sf_presentpath_disable', 'sf_presentpath_stat'])
editions = {}
for name, path in [('AUTOTEST', 'bin/PlayerAuto/StutterFix.dll'), ('Player', 'bin/Player/StutterFix.dll'), ('DEV', 'bin/Release/StutterFix.dll')]:
    data = (root / path).read_bytes()
    editions[name] = b'PresentPath\x00' in data
assert editions == {'AUTOTEST': True, 'Player': False, 'DEV': False}, editions
result = {'existing_named_exports_preserved': len(old), 'existing_ordinals_preserved': True, 'new_exports': extra, 'PresentPath_type_present': editions}
(root / 'out/present-path/artifact-check.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))

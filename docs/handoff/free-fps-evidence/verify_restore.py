import json,hashlib,sys
from pathlib import Path
sys.path.insert(0,str(Path('tools/sfmeasure').resolve()))
import server as S
mod=Path(S.MOD_DIR);root=Path('docs/handoff/free-fps-evidence')
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
r={'game_running':S.game_running(),'dll_sha256':h(mod/'StutterFix.dll'),'player_build_sha256':h(Path('bin/Player/StutterFix.dll')),'original_dll_sha256':h(root/'original.dll'),'settings_sha256':h(mod/'Settings.xml'),'original_settings_sha256':h(root/'Settings.original.xml')}
assert not r['game_running']
assert r['dll_sha256']==r['player_build_sha256']==r['original_dll_sha256']
assert r['settings_sha256']==r['original_settings_sha256']
root.joinpath('restoration.json').write_text(json.dumps(r,indent=2))
print('Restoration verified; game off; original DLL and settings identical')

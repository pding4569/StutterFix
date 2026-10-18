import json,shutil,hashlib,datetime
from pathlib import Path
root=Path.cwd();out=root/'tools/effects/out/shader30-fg';pub=root/'tools/effects/validation-14'
summary=json.loads((out/'summary.json').read_text())
for trial in summary['trials']:
 label=trial['trial']; troot=out/label
 for name in ['paired.json','regions.json','effect-identity.json']:
  shutil.copyfile(troot/name,pub/(label+'-'+name))
 reg=json.loads((troot/'regions.json').read_text())['summary']; trial['regions']=reg
 print(label, trial['visual']['captured_new_picture_generated'],trial['visual']['captured_generated'],trial['glow_both_source_frames_confirmed'],trial['ambiguous_song_seconds'])
shutil.copyfile(out/'summary.json',pub/'strong-neon-motion.json')
shutil.copyfile(root/'tools/effects/out/shader30-compat/summary.json',pub/'new-compat.json')
paths={
 'installed_managed':(Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix/StutterFix.dll'),'dd9ca8cadd52bb61aad3e3ab41a54f07e693fb8367653c27513bdef07fc193e1'),
 'installed_native':(Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix/sfnative.dll'),'ad2dfdc5ed4d5cd416488bb8e1d96863601d9c3a57690cc25756c5f71eec89d1'),
 'settings':(Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix/Settings.xml'),'fa6e781728a4c72c53aa1969a4d08707d837f15932bcf4e5c912bbbaf28d8cd5'),
 'preserved_tester_zip':(root/'dist/FrameGenLab-2026-10-07-tester.zip','689de906e1b0513c526c34c0aff33dbff6b1d12fe9544adf78b8295a4932c134')}
proof={'time':datetime.datetime.now().astimezone().isoformat(),'files':{}}
for k,(p,expected) in paths.items():
 actual=hashlib.sha256(p.read_bytes()).hexdigest();proof['files'][k]={'sha256':actual,'matches_original':actual==expected}; assert actual==expected,(k,actual)
(pub/'restoration.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')

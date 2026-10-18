import json,hashlib,shutil,datetime
from pathlib import Path
R=Path('E:/StutterFixWork/StutterFix');source=R/'tools/effects/out/ch15-number-input-final';out=R/'tools/effects/validation-15'
for name in ['summary.json','fx-number-input.png']:
 shutil.copyfile(source/name,out/('numeric-input.json' if name=='summary.json' else name))
summary=json.loads((out/'numeric-input.json').read_text(encoding='utf8'));summary['fixture_log_sha256']=hashlib.sha256((source/'game.log').read_bytes()).hexdigest();summary['normal_player_loadcheck']={'checked':219,'errors':0};summary['playerauto_loadcheck']={'checked':220,'errors':0};summary['bundle_rebuilt']=False;summary['mouse_and_keyboard_actions']='not tested; explicit GUI text injection only';(out/'numeric-input.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
expected=json.loads((R/'tools/effects/validation-14/restoration.json').read_text())['files'];mod=Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix');paths={'installed_managed':mod/'StutterFix.dll','installed_native':mod/'sfnative.dll','settings':mod/'Settings.xml','preserved_tester_zip':R/'dist/FrameGenLab-2026-10-07-tester.zip'};files={n:{'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for n,p in paths.items()}
for n,item in files.items():
 item['matches_original']=item['sha256']==expected[n]['sha256']
 if not item['matches_original']:raise RuntimeError(n+' restoration mismatch')
(out/'restoration.json').write_text(json.dumps({'time':datetime.datetime.now().astimezone().isoformat(),'files':files},indent=2),encoding='utf8');print(json.dumps(summary,ensure_ascii=False));print('Restoration4/4')

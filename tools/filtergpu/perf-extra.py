import sys,time,shutil,hashlib
from pathlib import Path
sys.path.insert(0,'tools/sfmeasure')
import server as s
out=Path('docs/handoff/filters-evidence');mod=Path(s.MOD_DIR)
s.kill_game=lambda: (_ for _ in ()).throw(RuntimeError('force kill disabled'))
def digest(p):return hashlib.sha256(p.read_bytes()).digest()
while not (out/'quality-extra.log').exists() or s.game_running() or digest(mod/'StutterFix.dll')!=digest(out/'original.dll'):time.sleep(2)
time.sleep(3)
shutil.copy2('bin/PlayerAuto/StutterFix.dll',mod/'StutterFix.dll');shutil.copy2('tools/filtergpu/filtergpu.dll',mod/'filtergpu.dll')
try:
 steps=['timeout 900','set FrameGenOutside 0','fpscap 0','auto on','open D:/얼불춤 맵 파일/Windflower/Windflower.adofai']
 for name,tile in [('Blur_BlurHole',71),('Blur_Radial',71),('AAA_SuperComputer',1154),('TV_WideScreenHorizontal',1)]:
  steps+=['filterhalfperf off','select '+str(tile),'play','wait 20','stop','wait 1']
  for mode in ['A','B','B','A']:
   steps+=['log HALF_ABBA '+name+' '+mode,'filterhalfperf '+('off' if mode=='A' else 'CameraFilterPack_'+name),'select '+str(tile),'play','wait 20','stop','filterhalfperf off','wait 1']
 steps+=['quit']
 text,_=s.sf_run(steps,timeout_min=15,tag='filters-abba-extra')
 (out/'abba-extra-summary.txt').write_text(text,encoding='utf8');shutil.copy2(s.PLAYER_LOG,out/'abba-extra.log')
 print('extra ABBA complete',flush=True)
finally:
 if s.game_running():s.sf_quit()
 if not s.game_running():
  shutil.copy2(out/'original.dll',mod/'StutterFix.dll');shutil.copy2(out/'Settings.original.xml',mod/'Settings.xml');(mod/'filtergpu.dll').unlink(missing_ok=True)
  print('original DLL and settings restored',flush=True)

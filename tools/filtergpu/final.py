import sys,time,shutil,re
from pathlib import Path
sys.path.insert(0,'tools/sfmeasure')
import server as s
out=Path('docs/handoff/filters-evidence');mod=Path(s.MOD_DIR)
s.kill_game=lambda: (_ for _ in ()).throw(RuntimeError('forced termination disabled'))
while not (out/'arche.log').exists() or s.game_running():time.sleep(2)
shutil.copy2('bin/PlayerAuto/StutterFix.dll',mod/'StutterFix.dll')
shutil.copy2('tools/filtergpu/filtergpu.dll',mod/'filtergpu.dll')
def run(name,steps,limit=15):
 text,_=s.sf_run(steps,timeout_min=limit,tag='filters-'+name)
 (out/(name+'-summary.txt')).write_text(text,encoding='utf8');shutil.copy2(s.PLAYER_LOG,out/(name+'.log'))
 print(name+' complete',flush=True)
 return (out/(name+'.log')).read_text(encoding='utf8')
try:
 for name,path in [('final-hello','D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai'),('final-windflower','D:/얼불춤 맵 파일/Windflower/Windflower.adofai')]:
  text=run(name,['timeout 900','set FrameGenOutside 0','fpscap 0','auto on','open '+path,'filtergpu on','play','filtergpu reset','waitsong','wait 2','filtergpu state','filtergpu off','stop','quit'])
  if '[임무완주]' not in text:raise RuntimeError(name+' did not finish')
 steps=['timeout 900','set FrameGenOutside 0','fpscap 0','auto on','open D:/얼불춤 맵 파일/Windflower/Windflower.adofai']
 for name,tile in [('Blur_Movie',14),('Blur_BlurHole',71),('Blur_Radial',71)]:
  steps+=['select '+str(tile),'filterhalf CameraFilterPack_'+name,'play','wait 20','filterhalf off','stop','wait 1']
 steps+=['select 71']
 # First unmeasured run warms playback before the ABBA sequence.
 steps+=['play','wait 20','stop','wait 1']
 for mode in ['A','B','B','A']:
  steps+=['log HALF_ABBA '+mode,'filterhalfperf '+('off' if mode=='A' else 'CameraFilterPack_Blur_Movie'),'select 71','play','wait 20','stop','filterhalfperf off','wait 1']
 steps+=['quit']
 run('quality-abba',steps)
finally:
 if s.game_running():s.sf_quit()
 if not s.game_running():
  shutil.copy2(out/'original.dll',mod/'StutterFix.dll')
  shutil.copy2(out/'Settings.original.xml',mod/'Settings.xml')
  (mod/'filtergpu.dll').unlink(missing_ok=True)
  print('original DLL and settings restored',flush=True)

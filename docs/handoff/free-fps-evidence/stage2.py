import sys,time,shutil
from pathlib import Path
sys.path.insert(0,str(Path('tools/sfmeasure').resolve()))
import server as S
S.kill_game=lambda: (_ for _ in ()).throw(RuntimeError('force kill forbidden'))
while not Path(S.END_FILE).exists():time.sleep(1)
steps=['set FrameGenOutside 0','fpscap 0','auto on','open D:/얼불춤 맵 파일/Windflower/Windflower.adofai','freefps state']
for mode in ['A','B','B','A']:
 steps+=['freefps restore','log FREEFPS WindflowerStatic '+mode,'play']
 if mode=='B':steps+=['freefps off BGStaticCam']
 steps+=['wait 30','stop','wait 1']
steps+=['freefps restore']
print(S.sf_live(steps,timeout_min=6,tag='freefps-windflower-abba'),flush=True)
S._batch(['quit'],1,launch_ok=False)
for _ in range(20):
 if not S.game_running():break
 time.sleep(1)
if S.game_running():raise RuntimeError('quit failed; DLL not replaced')
shutil.copy2('bin/PlayerAuto/StutterFix.dll',Path(S.MOD_DIR)/'StutterFix.dll')
print('SAFE RESTART FOR PIXEL CHECK',flush=True)
steps=['set FrameGenOutside 0','fpscap 0','auto on','open D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai','freefps batches','freefps verify','play']
for _ in range(180):steps+=['wait 2','filterstate']
steps+=['stop','freefps verify','freefps batches']
print(S.sf_live(steps,timeout_min=12,tag='freefps-pixels-filters'),flush=True)

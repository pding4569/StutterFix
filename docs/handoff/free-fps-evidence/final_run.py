import sys,time
from pathlib import Path
sys.path.insert(0,str(Path('tools/sfmeasure').resolve()))
import server as S
S.kill_game=lambda: (_ for _ in ()).throw(RuntimeError('force kill forbidden'))
steps=['set FrameGenOutside 0','fpscap 0','auto on','open D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai','freefps batches','freefps verify']
for name in ['BGStaticCam','BGMovingCam','Camera']:
 for mode in ['A','B','B','A']:
  steps+=['freefps restore','log FREEFPS MASK '+name+' '+mode,'play','freefps '+('off '+name if mode=='B' else 'state'),'log equal-window','wait 20','freefps state','stop','wait 1']
steps+=['freefps restore','play']
for _ in range(180):steps+=['wait 2','filterstate']
steps+=['stop','freefps verify','freefps batches']
print(S.sf_live(steps,timeout_min=16,tag='freefps-mask-hello'),flush=True)
steps=['set FrameGenOutside 0','fpscap 0','auto on','open D:/얼불춤 맵 파일/Windflower/Windflower.adofai','freefps batches','freefps verify']
for mode in ['A','B','B','A']:
 steps+=['freefps restore','log FREEFPS MASK WindflowerStatic '+mode,'play','freefps '+('off BGStaticCam' if mode=='B' else 'state'),'log equal-window','wait 30','freefps state','stop','wait 1']
steps+=['freefps restore']
print(S.sf_live(steps,timeout_min=5,tag='freefps-mask-windflower'),flush=True)

import sys
from pathlib import Path
sys.path.insert(0,str(Path('tools/sfmeasure').resolve()))
import server as S
S.kill_game=lambda: (_ for _ in ()).throw(RuntimeError('force kill forbidden'))
steps=['set FrameGenOutside 0','fpscap 0','auto on']
for name in ['BGStaticCam','BGMovingCam','Camera']:
 for mode in ['A','B','B','A']:
  steps+=['freefps restore','log FREEFPS '+name+' '+mode,'play']
  if mode=='B':steps+=['freefps off '+name]
  steps+=['wait 20','stop','wait 1']
steps+=['freefps restore']
print(S.sf_live(steps,timeout_min=10,tag='freefps-camera-abba'),flush=True)

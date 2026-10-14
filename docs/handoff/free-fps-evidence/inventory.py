import sys
from pathlib import Path
sys.path.insert(0,str(Path('tools/sfmeasure').resolve()))
import server as S
S.kill_game=lambda: (_ for _ in ()).throw(RuntimeError('force kill forbidden'))
print(S.sf_live(['set FrameGenOutside 0','fpscap 0','open D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai','auto on','freefps state','play','wait 15','filterstate','stop'],timeout_min=2,tag='freefps-inventory'),flush=True)

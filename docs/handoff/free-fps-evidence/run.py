import sys,json,time
from pathlib import Path
sys.path.insert(0,str(Path('tools/sfmeasure').resolve()))
import server as S
S.kill_game=lambda: (_ for _ in ()).throw(RuntimeError('Mission forbids force kill'))
t=time.time()
try:
 print(S.sf_live(['set FrameGenOutside 0','fpscap 0','freefps state'],timeout_min=2,tag='freefps-start'),flush=True)
except Exception as e: print(type(e).__name__,str(e),flush=True)
Path('docs/handoff/free-fps-evidence/start.json').write_text(json.dumps({'elapsed':time.time()-t,'running':S.game_running()},indent=2))

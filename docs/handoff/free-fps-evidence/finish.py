import sys,time,shutil
from pathlib import Path
sys.path.insert(0,str(Path('tools/sfmeasure').resolve()))
import server as S
S.kill_game=lambda: (_ for _ in ()).throw(RuntimeError('force kill forbidden'))
try:S._batch(['quit'],1,launch_ok=False)
except Exception:
 if S.game_running():raise
for _ in range(20):
 if not S.game_running():break
 time.sleep(1)
if S.game_running():raise RuntimeError('Game did not quit: refusing replacement')
shutil.copy2('bin/PlayerAuto/StutterFix.dll',Path(S.MOD_DIR)/'StutterFix.dll')
steps=['set FrameGenOutside 0','fpscap 0','auto on','open D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai','freefps counters','wait 2','freefps counters','freefps verify','play','log pixel-window','wait 12','freefps counters','wait 2','freefps counters','freefps verify','wait 15','freefps verify','stop','freefps restore']
print(S.sf_live(steps,timeout_min=3,tag='freefps-final-format-counters'),flush=True)
try:S._batch(['quit'],1,launch_ok=False)
except Exception:
 if S.game_running():raise
for _ in range(20):
 if not S.game_running():break
 time.sleep(1)
if S.game_running():raise RuntimeError('Game still running; restoration withheld')
shutil.copy2('bin/Player/StutterFix.dll',Path(S.MOD_DIR)/'StutterFix.dll')
shutil.copy2('docs/handoff/free-fps-evidence/Settings.original.xml',Path(S.MOD_DIR)/'Settings.xml')
print('RESTORED PLAYER AND SETTINGS; GAME OFF',flush=True)

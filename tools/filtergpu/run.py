import sys
from pathlib import Path
sys.path.insert(0,'tools/sfmeasure')
import server as s
out=Path('docs/handoff/filters-evidence')
maps=[('hello',Path('D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai'),360),('windflower',Path('D:/얼불춤 맵 파일/Windflower/Windflower.adofai'),180),('arche',next(p for p in Path('D:/얼불춤 맵 파일/Arche').glob('*FULL.adofai') if 'NONTILE' not in p.name),360)]
for name,path,duration in maps:
 steps=['timeout 900','set FrameGenOutside 0','fpscap 0','auto on','open '+str(path),'filtergpu on']
 if name=='hello':steps+=['set Language ko','ui 1.1','wait 2','shot filters-warm-ko','set Language en','wait 2','shot filters-warm-en','ui close']
 steps+=['play','filtergpu reset']+['wait 30']*(duration//30)+['filtergpu state','filtergpu off','stop','quit']
 result=s.sf_run(steps,timeout_min=15,tag='filters-'+name)[0]
 (out/(name+'-summary.txt')).write_text(result,encoding='utf8')
 import shutil
 shutil.copy2(s.PLAYER_LOG,out/(name+'.log'))
 print(name+' complete',flush=True)

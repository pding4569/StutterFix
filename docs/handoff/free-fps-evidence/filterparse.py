import re,json
from pathlib import Path
s=(Path.home()/'AppData/LocalLow/7th Beat Games/A Dance of Fire and Ice/Player.log').read_text(encoding='utf-8',errors='replace')
rows=[]
last=-1
for line in s.splitlines():
 m=re.search(r'\[필터 상태\] 곡 ([\d.]+)초, 켜진 필터 (\d+)개(.*)',line)
 if not m:continue
 t=float(m[1])
 if t<=last:continue
 last=t
 names=re.findall(r':(\w+)\(',m[3]);simple=[x for x in names if x in ['Color_BrightContrastSaturation','Color_Brightness','Color_Contrast','Color_Saturate','Color_Saturation','Color_Noise']]
 rows.append({'song_s':t,'names':names,'simple':simple})
print('samples',len(rows),'last_song_s',last,'3plus_simple',sum(len(x['simple'])>=3 for x in rows))
Path('docs/handoff/free-fps-evidence/filter-progress.json').write_text(json.dumps(rows,indent=2))

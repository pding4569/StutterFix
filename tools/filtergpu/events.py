import json
from pathlib import Path
p=Path('D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai')
d=json.loads(p.read_text(encoding='utf-8-sig'))
n=0
for e in d['actions']:
 if 'Filter' in str(e.get('eventType','')) and any(x in str(e) for x in ['Radial','Movie','Noise','BrightContrast','WideScreen']):
  if n<12:print(str(e)[:600])
  n+=1
print('events',n)

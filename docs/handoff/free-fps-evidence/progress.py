import re,json
from pathlib import Path
root=Path('docs/handoff/free-fps-evidence')
live=Path.home()/'AppData/LocalLow/7th Beat Games/A Dance of Fire and Ice/Player.log'
s=live.read_text(encoding='utf-8',errors='replace')
rows=[]
for b in re.split(r'\[StutterFix\] \[자동 시험\] FREEFPS MASK ',s)[1:]:
 f=re.search(r'\[곡\] 평균 (\d+) FPS',b);g=re.search(r'GPU 평균 ([\d.]+)ms',b);n=re.search(r'30ms 초과 프레임 (\d+)개',b)
 if f:rows.append({'label':b.splitlines()[0],'fps':int(f[1]),'gpu_ms':float(g[1]) if g else None,'over30':int(n[1]) if n else None})
print(json.dumps(rows,indent=2))
root.joinpath('mask-progress.json').write_text(json.dumps(rows,indent=2))

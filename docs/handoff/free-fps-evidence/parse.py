import re,json
from pathlib import Path
log=Path.home()/'AppData/LocalLow/7th Beat Games/A Dance of Fire and Ice/Player.log'
logs=list((Path.home()/'AppData/Local/StutterFix/runs').glob('*freefps*abba.log'))
s='\n'.join(p.read_text(encoding='utf-8',errors='replace') for p in logs)
if not logs:s=log.read_text(encoding='utf-8',errors='replace')
rows=[]
for block in re.split(r'\[StutterFix\] \[자동 시험\] FREEFPS ',s)[1:]:
 label=block.splitlines()[0]; fps=re.search(r'\[곡\] 평균 (\d+) FPS',block); gpu=re.search(r'GPU 평균 ([\d.]+)ms',block); over=re.search(r'30ms 초과 프레임 (\d+)개',block)
 if fps: rows.append(dict(label=label,fps=int(fps[1]),gpu_ms=float(gpu[1]) if gpu else None,over30=int(over[1]) if over else None))
print(json.dumps(rows,ensure_ascii=False,indent=2))
Path('docs/handoff/free-fps-evidence/camera-rows.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')

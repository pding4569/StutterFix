import re,json,collections
from pathlib import Path
root=Path('docs/handoff/free-fps-evidence');rows=[]
for p in root.glob('*freefps-mask-*.log'):
 s=p.read_text(encoding='utf-8',errors='replace')
 for b in re.split(r'\[StutterFix\] \[자동 시험\] FREEFPS MASK ',s)[1:]:
  f=re.search(r'\[곡\] 평균 (\d+) FPS',b);g=re.search(r'GPU 평균 ([\d.]+)ms',b);n=re.search(r'30ms 초과 프레임 (\d+)개',b)
  if f:rows.append(dict(label=b.splitlines()[0],fps=int(f[1]),gpu_ms=float(g[1]),over30=int(n[1]),display=re.findall(r'\[곡 시작\] 화면: (.*)',b)[0]))
root.joinpath('camera-final.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
g=collections.defaultdict(list)
for r in rows:g[r['label']].append(r)
for k,v in g.items():print(k,'n',len(v),'fps',sum(x['fps'] for x in v)/len(v),'gpu',sum(x['gpu_ms'] for x in v)/len(v),'over30',sum(x['over30'] for x in v))
print('display_conditions',len(set(x['display'] for x in rows)))

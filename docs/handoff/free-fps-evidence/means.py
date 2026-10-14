import json,collections
from pathlib import Path
rows=json.loads(Path('docs/handoff/free-fps-evidence/mask-progress.json').read_text())
g=collections.defaultdict(list)
for r in rows:g[r['label']].append(r)
for k,v in g.items():print(k,len(v),sum(r['fps'] for r in v)/len(v),sum(r['gpu_ms'] for r in v)/len(v),sum(r['over30'] for r in v))

import json
from pathlib import Path
r=json.loads(Path('docs/handoff/free-fps-evidence/filter-final.json').read_text())
print([x for x in r['rows'] if len(x['simple'])>=3])

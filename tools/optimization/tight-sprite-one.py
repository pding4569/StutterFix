"""Generate one explicit source-image padding diagnostic, without editing input."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path

s = importlib.util.spec_from_file_location('tight', Path(__file__).with_name('tight-sprite-audit.py'))
tight = importlib.util.module_from_spec(s)
s.loader.exec_module(tight)
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('image', type=Path)
p.add_argument('--pad', type=int, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
before = hashlib.sha256(a.image.read_bytes()).hexdigest()
r = tight.geometry(a.image, a.pad)
r['sha256'] = before
a.output.with_suffix('.json').write_text(json.dumps(r, ensure_ascii=False, indent=2), encoding='utf-8')
cells = [r['name'], str(r['width']), str(r['height'])]
for n in (4, 6, 8):
    cells += [str(r['poly'+str(n)]['ratio']), ','.join(map(str, r['poly'+str(n)]['xy']))]
a.output.write_text('name\twidth\theight\t4ratio\t4xy\t6ratio\t6xy\t8ratio\t8xy\n'+'\t'.join(cells)+'\n', encoding='utf-8')
assert hashlib.sha256(a.image.read_bytes()).hexdigest() == before

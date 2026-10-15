from pathlib import Path
import re,sys
root=Path(__file__).resolve().parents[1]
bad=[]
for p in root.rglob('*.cs'):
 if any(x in {'obj','bin','.git'} for x in p.relative_to(root).parts):continue
 for n,line in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1):
  literals=re.findall(r'"(?:\\.|[^"\\])*"',line)
  if any('??' in s for s in literals) or re.search(r'\bT\(\s*"\?',line):
   bad.append(f'{p.name}:{n}: warning SFENC001 suspicious question marks in literal')
print('\n'.join(bad) if bad else 'SFENC001: clean')
sys.exit(bool(bad))

"""Extract every failed/no-op/active sprite trial; never infer success from zero edits."""
import json
from pathlib import Path
import re
import shutil

root = Path('E:/StutterFixWork')
out = root/'wt-partial/docs/deco-spec/results/evidence/tight-sprite'
out.mkdir(exist_ok=True)
summary = []
for log in sorted((root/'night-results').glob('tight-*-*.log')):
    text = log.read_text(encoding='utf-8', errors='replace')
    selected = [l for l in text.splitlines() if l.startswith('[StutterFix] [tightsprite]')
        or any(s in l for s in ('[framecmp] diff ', '[framecmp] noise outside=',
            '[이미지] 미리 푼 것', '단계 실패'))]
    if not selected:
        continue
    (out/(log.stem+'.txt')).write_text('\n'.join(selected)+'\n', encoding='utf-8')
    source = log.with_suffix('.json')
    if source.exists():
        shutil.copy2(source, out/source.name)
    summary.append(dict(run=log.stem, failed='단계 실패' in text,
        installs=[dict(api=a, cap=int(c), changed=int(n), bounds_changed=int(b), api_rejected=int(r))
                  for a,c,n,b,r in re.findall(r'api=(\w+) cap=(\d+).*? changed=(\d+).*? bounds_changed=(\d+) api_rejected=(\d+)', text)],
        comparisons=[dict(a=a,b=b,maximum=maximum,changed_pixels=int(n))
                     for a,b,maximum,n in re.findall(r'\[framecmp\] diff (\S+) (\S+) threshold=0 maxRGBA=(\S+) changed=(\d+)/', text)]))
(out/'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
print('extracted', len(summary), 'trials')

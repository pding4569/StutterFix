import re,json,shutil
from pathlib import Path
root=Path('docs/handoff/free-fps-evidence');runs=Path.home()/'AppData/Local/StutterFix/runs'
for p in runs.glob('*freefps*.log'):shutil.copy2(p,root/p.name)
p=next(root.glob('*freefps-mask-hello.log'));s=p.read_text(encoding='utf-8',errors='replace');samples=[]
for line in s.splitlines():
 if '[임무완주]' in line:break
 m=re.search(r'\[필터 상태\] 곡 ([\d.]+)초, 켜진 필터 (\d+)개(.*)',line)
 if m:
  names=re.findall(r':(\w+)\(',m[3]);simple=[n for n in names if n in ['Color_BrightContrastSaturation','Color_Contrast','Color_Noise','Colors_Brightness','Color_Brightness','Color_Saturate','Color_Saturation']];samples.append(dict(song_s=float(m[1]),names=names,simple=simple))
result=dict(samples=len(samples),over3=sum(len(x['simple'])>=3 for x in samples),last_sample_s=samples[-1]['song_s'],portal='[임무완주]' in s,rows=samples)
root.joinpath('filter-final.json').write_text(json.dumps(result,indent=2))
print({k:v for k,v in result.items() if k!='rows'})
for l in s.splitlines():
 if '[무료FPS 대조]' in l:print(l)

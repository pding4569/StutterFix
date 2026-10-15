from pathlib import Path
import re,json
out=Path('docs/handoff/filters-evidence')
quality={}
for filename in ['quality-abba.log','quality-extra.log']:
 p=out/filename
 if not p.exists():continue
 for m in re.finditer(r'\[filterhalf\] (\S+) sample=(\d+) max=(\d+) psnr=(\S+) control_max=(\d+).*?cumulative_max=(\d+) cumulative_psnr=(\S+)',p.read_text(encoding='utf8')):
  typ,n,mx,ps,ctrl,cmx,cps=m.groups()
  if typ not in quality:quality[typ]=dict(samples=0,max=0,min_psnr=float('inf'),control_max=0)
  r=quality[typ];r['samples']+=1;r['max']=max(r['max'],int(mx));r['min_psnr']=min(r['min_psnr'],float(ps));r['control_max']=max(r['control_max'],int(ctrl));r['cumulative_psnr']=float(cps)
trials=[]
for filename in ['abba-final.log']:
 p=out/filename
 if not p.exists():continue
 text=p.read_text(encoding='utf8')
 markers=list(re.finditer(r'\[자동 시험\] HALF_ABBA (?:(\S+) )?([AB])\r?\n',text))
 for j,m in enumerate(markers):
  name=m[1] or 'Blur_Movie';mode=m[2];part=text[m.end():markers[j+1].start() if j+1<len(markers) else len(text)]
  fps=re.findall(r'\[곡\] 평균 (\d+) FPS',part);gpu=re.findall(r'GPU 평균 ([\d.]+)ms',part);over=re.findall(r'\[임무30\] 시작 연출 뒤 30ms 초과 프레임 (\d+)개',part)
  proof=re.findall(r'\[filterhalfperf\] (\S+) calls=(\d+) frames=(\d+)',part)
  trials.append(dict(type=name,mode=mode,proof=proof,fps=int(fps[0]) if fps else None,gpu=float(gpu[0]) if gpu else None,over30=int(over[0]) if over else None))
(out/'quality-metrics.json').write_text(json.dumps(dict(quality=quality,trials=trials),indent=2),encoding='utf8')
print(json.dumps(dict(quality=quality,trials=trials),indent=2))

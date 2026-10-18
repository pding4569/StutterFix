from pathlib import Path
import json,hashlib,collections,re
root=Path('D:/얼불춤 맵 파일/Arche'); path=next(x for x in root.glob('*.adofai') if x.name.endswith(' FULL.adofai'))
def clean(s):
 out=[];quoted=False;escape=False
 for i,c in enumerate(s):
  if quoted:
   out.append(c)
   if escape: escape=False
   elif c=='\\': escape=True
   elif c=='"': quoted=False
  elif c=='"': quoted=True;out.append(c)
  elif c==',' and s[i+1:].lstrip().startswith(('}',']')): pass
  else:out.append(c)
 return ''.join(out)
rows=[];bad=[]
for n,line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(),1):
 line=line.strip().rstrip(',')
 if not line.startswith('{ "'): continue
 try: rows.append((n,json.loads(clean(line))))
 except ValueError as e:bad.append([n,str(e)])
rays=[(n,x) for n,x in rows if 'nev_travel_' in str(x.get('decorationImage',''))]
window=[(n,x) for n,x in rows if 3400<=x.get('floor',-1)<=3699]
summary={'file':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'parsed_object_lines':len(rows),'parse_errors':bad,'ray_add_events':len(rays),'ray_images':dict(collections.Counter(x.get('decorationImage') for _,x in rays)),'ray_depth_counts':dict(sorted(collections.Counter(x.get('depth') for _,x in rays).items())),'floor_window_events':len(window),'floor_window_types':dict(collections.Counter(x.get('eventType') for _,x in window)),'floor_window_ray_add_events':sum('nev_travel_' in str(x.get('decorationImage','')) for _,x in window),'first_rays':rays[:3],'window_image_swaps':[(n,x) for n,x in window if 'decorationImage' in x][:12], 'fog_images':dict(collections.Counter(x.get('decorationImage') for _,x in rows if 'fog' in str(x.get('decorationImage','')).lower()))}
wr=[x for _,x in window if x.get('eventType')=='AddDecoration' and 'nev_travel_' in str(x.get('decorationImage',''))]
summary['window_ray_per_floor_distribution']=dict(collections.Counter(collections.Counter(x['floor'] for x in wr).values()))
summary['window_ray_depth_counts']=dict(sorted(collections.Counter(x.get('depth') for x in wr).items()))
summary['window_ray_images']=dict(collections.Counter(x.get('decorationImage') for x in wr))
summary['example_floors']={floor:dict(collections.Counter(x.get('decorationImage') for x in wr if x.get('floor')==floor)) for floor in [3440,3441,3442,3443]}
summary['window_ray_tile_values']=dict(collections.Counter(str(x.get('tile')) for x in wr))
summary['window_ray_smoothing']=dict(collections.Counter(str(x.get('imageSmoothing')) for x in wr))
Path('docs/handoff/codex-gpu3-20261018-map.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
print({k:v for k,v in summary.items() if k not in ['first_rays','window_image_swaps','ray_depth_counts']})
print('first rays',summary['first_rays'][:1]);print('window swaps',summary['window_image_swaps'][:1])

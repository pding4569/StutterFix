"""Join the explicit completed effect frame to native source frames, offline only."""
import argparse,csv,json
from pathlib import Path
def analyze(root):
 rows=list(csv.DictReader((root/'cost-sources.csv').open(newline='')))
 if rows and 'fx_frame' not in rows[0]:return {'scope':'Effect frame identity was not recorded','confirmed_glow_triplets':None}
 effects={int(r['fx_frame']):r['fx_glow']=='1' for r in rows}
 sources=list(csv.DictReader((root/'sources.csv').open(newline='')))
 previous={int(b['unity_frame']):int(a['unity_frame']) for a,b in zip(sources,sources[1:])}
 output=[]
 for r in csv.DictReader((root/'clip.csv').open(newline='')):
  new=int(r['unity_frame']);old=previous.get(new)
  known=new in effects and old in effects
  output.append(dict(index=int(r['index']),song_s=float(r['song_s']),new_frame=new,old_frame=old,
   known=known,glow_both=known and effects[new] and effects[old]))
 return dict(scope='Explicit completed FX frame keys, not nearest callback or current config. Both endpoints must have executed glow. Identity does not prove a visible pixel change.',triplets=len(output),confirmed_glow_triplets=sum(r['glow_both'] for r in output),unknown=sum(not r['known'] for r in output),rows=output)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('capture',type=Path);p.add_argument('--out',type=Path,required=True);a=p.parse_args();r=analyze(a.capture);a.out.write_text(json.dumps(r,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in r.items() if k!='rows'}))

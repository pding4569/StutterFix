"""Visual-only effect/interpolation comparisons; timing is deliberately excluded."""
import argparse,csv,json,subprocess,sys
from pathlib import Path
LAB=Path(__file__).resolve().parents[1]/'framegenlab'
def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--early',action='store_true');a=p.parse_args();a.out.mkdir(parents=True,exist_ok=False)
 base={n:False for n in ['FxColor','FxSharp','FxAA','FxGlow','FxLight','FxVignette','FxLut','LowHalfRender','LowSharpen','LowFsr']};base['LowRenderScale']=100
 trials=[]
 for name,mapfile in [('hello','D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai'),('arche','D:/얼불춤 맵 파일/Arche/backup.adofai')]:
  for glow in [False,True]:
   label=name+('-glow' if glow else '-off');root=a.out/label;settings=a.out/(label+'.json');settings.write_text(json.dumps({**base,'FxGlow':glow}),encoding='utf-8')
   cmd=[sys.executable,str(LAB/'measure_outside.py'),'--map',mapfile,'--out',str(root),'--label','fg29-'+label,'--mode','4','--seconds','12' if a.early else '28','--clip','--block-flow','--block-variant','3','--fixed-cost-stage','1','--cost-split','--settings',str(settings)]
   if a.early:cmd+=['--fx-state-window','0.05','1.2']
   print('START '+label,flush=True)
   with (a.out/(label+'.log')).open('w',encoding='utf-8') as f:subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,check=True)
   trials.append((label,root))
 # CPU image registration follows ALL game trials, never runs during measurement.
 results=[]
 for label,root in trials:
  for analyzer,output in [('analyze_paired_motion.py','paired.json'),('analyze_block_regions.py','regions.json')]:
   subprocess.run([sys.executable,str(LAB/analyzer),str(root/'capture'),'--mode','4','--out',str(root/output)],check=True)
  data=json.loads((root/'paired.json').read_text());log=(root/'game.log').read_text(encoding='utf-8')
  import re
  states=re.findall(r'\[화면효과상태\] ([^\r\n]+)',log)
  effect_rows=list(csv.DictReader((root/'capture/cost-sources.csv').open(newline='')))
  effect_frames={int(r['fx_frame']):r['fx_glow']=='1' for r in effect_rows if 'fx_frame' in r}
  source_rows=list(csv.DictReader((root/'capture/sources.csv').open(newline='')))
  previous={int(b['unity_frame']):int(a['unity_frame']) for a,b in zip(source_rows,source_rows[1:])}
  clips=list(csv.DictReader((root/'capture/clip.csv').open(newline='')))
  joined=[]
  for r in clips:
   frame=int(r['unity_frame']);old=previous.get(frame)
   joined.append(None if frame not in effect_frames or old not in effect_frames else effect_frames[frame] and effect_frames[old])
  results.append(dict(trial=label,visual=data['summary'],fx_states=states,glow_both_source_frames_confirmed=sum(x is True for x in joined),effect_frame_identity_unknown=sum(x is None for x in joined),ambiguous_song_seconds=[r['song_s'] for r in data['rows'] if not r['accepted']]))
 (a.out/'summary.json').write_text(json.dumps(dict(scope=('0.1..0.9s, FX states0.05/1.2s.' if a.early else '20..23s, FX states19/24s.')+' Step6 uncompressed triplets, same selected source/reference, not independent OFF or full-song quality. No performance inference.',trials=results),ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps(results,ensure_ascii=False),flush=True)
if __name__=='__main__':main()

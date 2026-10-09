"""Visual-only effect/interpolation comparisons; timing is deliberately excluded."""
import argparse,json,math,subprocess,sys
from pathlib import Path
LAB=Path(__file__).resolve().parents[1]/'framegenlab'
def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--early',action='store_true');p.add_argument('--map',type=Path);p.add_argument('--name');p.add_argument('--window',type=float,nargs=2);p.add_argument('--strong-neon',action='store_true',help='Compare FX OFF with strong neon candidate, not glow causality alone');a=p.parse_args()
 if bool(a.map)!=bool(a.name):p.error('--map and --name must be supplied together')
 if a.window and (a.early or not a.map or not 0<=a.window[0]<a.window[1] or a.window[1]-a.window[0]>4):p.error('Custom window requires a single map and a positive interval <=4s')
 begin,end=a.window or ([0.1,0.9] if a.early else [20,23]);state_window=[max(0.05,begin-1),end+1];seconds=max(12,math.ceil(end+5))
 a.out.mkdir(parents=True,exist_ok=False)
 base={n:False for n in ['FxColor','FxSharp','FxAA','FxGlow','FxLight','FxVignette','FxLut','LowHalfRender','LowSharpen','LowFsr']};base['LowRenderScale']=100
 trials=[]
 maps=[(a.name,str(a.map))] if a.map else [('hello','D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai'),('arche','D:/얼불춤 맵 파일/Arche/backup.adofai')]
 for name,mapfile in maps:
  for glow in [False,True]:
   label=name+('-glow' if glow else '-off');root=a.out/label;settings=a.out/(label+'.json');config={**base,'FxGlow':glow}
   if a.strong_neon and glow:config.update(FxPreset=6,FxColor=True,FxSharp=True,FxGlow=True,FxVignette=True,FxVibrance=.55,FxContrast=1.12,FxBrightness=-.1,FxGlowAmount=1.2,FxGlowThreshold=.28,FxGlowStack=True,FxToneMap=True,FxCeiling=.97)
   settings.write_text(json.dumps(config),encoding='utf-8')
   cmd=[sys.executable,str(LAB/'measure_outside.py'),'--map',mapfile,'--out',str(root),'--label','fg29-'+label,'--mode','4','--seconds',str(seconds),'--clip','--block-flow','--block-variant','3','--fixed-cost-stage','1','--cost-split','--settings',str(settings),'--fx-state-window',*[str(t) for t in state_window]]
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
  from effect_frame_identity import analyze
  identity=analyze(root/'capture')
  (root/'effect-identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8')
  times=[r['song_s'] for r in data['rows']]
  if not times or min(times)<begin-0.15 or max(times)>end+0.15:raise RuntimeError('Captured times do not match requested window; rebuild native AND managed for this interval')
  results.append(dict(trial=label,visual=data['summary'],fx_states=states,glow_both_source_frames_confirmed=identity['confirmed_glow_triplets'],effect_frame_identity_unknown=identity['unknown'],ambiguous_song_seconds=[r['song_s'] for r in data['rows'] if not r['accepted']]))
 (a.out/'summary.json').write_text(json.dumps(dict(scope=f'{begin}..{end}s, FX states{state_window}. Step6 uncompressed triplets, same selected source/reference, not independent FrameGen OFF or full-song quality. No performance inference. Strong neon comparison includes color/sharp/glow/vignette/tone and cannot isolate glow causality.',strong_neon=a.strong_neon,trials=results),ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps(results,ensure_ascii=False),flush=True)
if __name__=='__main__':main()

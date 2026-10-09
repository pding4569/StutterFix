"""Explicit offscreen GPU timestamps; static actual3440 scene, no capture while timing."""
import argparse,hashlib,json,re,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'framegenlab'))
from measure_maps import sf,ROOT
def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--profiles',default='passthrough,color,combined,sharp,fxaa,glow,light');a=p.parse_args();profiles=a.profiles.split(',')
 if not profiles or len(set(profiles))!=len(profiles) or any(n not in ['passthrough','color','combined','sharp','fxaa','glow','light','clear-strong','neon-strong','rays','streak','flare','tone','chromatic','grain','crt','pixel','posterize','blur'] for n in profiles):p.error('Unknown or duplicate GPU profile')
 if sf.game_running():raise RuntimeError('Closed game required')
 a.out.mkdir(parents=True,exist_ok=False);mod=Path(sf.MOD_DIR)
 original={n:(mod/n).read_bytes() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
 path=mod/'shots/identity16.png';previous=path.read_bytes() if path.exists() else None
 try:
  path.parent.mkdir(exist_ok=True);path.write_bytes((ROOT/'effects/identity16.png').read_bytes())
  (mod/'StutterFix.dll').write_bytes((ROOT/'bin/PlayerAuto/StutterFix.dll').read_bytes())
  steps=['game D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai','auto on','press','wait 20','fxstate']
  for name in profiles:steps+=['fxbench '+name,'wait 4','fxbenchreport']
  steps+=['fxbench off','wait 2','fxstate','quit']
  summary,metrics=sf.sf_run(steps,settings={**{k:False for k in ['FxColor','FxSharp','FxAA','FxGlow','FxLight','FxVignette','FxLut','LowHalfRender','LowFsr','LowSharpen']},'FrameStats':False,'LowRenderScale':100,'FrameGenOutside':0},tag='effects-gpu-fixture',timeout_min=4)
  log=sf.read_text(sf.PLAYER_LOG);(a.out/'game.log').write_text(log,encoding='utf-8');(a.out/'run.txt').write_text(summary,encoding='utf-8')
  rows=[dict(profile=m[0],samples=int(m[1]),mean_ms=float(m[2]),maximum_ms=float(m[3]),skipped=int(m[4]),errors=int(m[5])) for m in re.findall(r'\[화면효과GPU\] profile=(\S+) samples=(\d+) mean_ms=(\S+) max_ms=(\S+) skipped=(\d+) errors=(\d+)',log)]
  final={r['profile']:r for r in rows}
  if set(final)!=set(profiles) or metrics['errors'] or any(r['samples']<100 or r['errors'] for r in rows) or any(x in log for x in ['[화면효과GPU] 실패:','단계 실패','native failure']):raise RuntimeError('GPU fixture failed; preserve data')
  data=dict(scope='D3D11 disjoint/timestamp queries around private offscreen passes on one frozen true3440x1440 scene. Thirty warmup frames per profile; async8slot DONOTFLUSH, no GPU waits/CPU pixel reads. Not gameplay FPS, PresentMon, physical display or arbitrary-scene cost. Glow executes privately even if the live map has its own bloom; live effects unchanged.',profiles=final,rows=rows,managed_sha256=hashlib.sha256((ROOT/'bin/PlayerAuto/StutterFix.dll').read_bytes()).hexdigest(),native_sha256=hashlib.sha256((mod/'sfnative.dll').read_bytes()).hexdigest())
  (a.out/'summary.json').write_text(json.dumps(data,indent=2),encoding='utf-8');print(json.dumps(data))
 finally:
  if sf.game_running():sf.sf_quit()
  if sf.game_running():raise RuntimeError('Normal game shutdown required')
  for n,b in original.items():(mod/n).write_bytes(b)
  if previous is None:path.unlink(missing_ok=True)
  else:path.write_bytes(previous)
  assert all((mod/n).read_bytes()==b for n,b in original.items())
  print('Installed DLL/native/settings and LUT fixture restored exactly')
if __name__=='__main__':main()

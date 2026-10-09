"""Explicit normal-build compatibility/lifecycle run, never a performance trial."""
import argparse,hashlib,json,re,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/framegenlab'))
from measure_maps import sf

def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args()
 if sf.game_running():raise RuntimeError('Closed game required')
 a.out.mkdir(parents=True,exist_ok=False);mod=Path(sf.MOD_DIR)
 original={n:(mod/n).read_bytes() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
 try:
  (mod/'StutterFix.dll').write_bytes((ROOT/'bin/PlayerAuto/StutterFix.dll').read_bytes())
  (mod/'sfnative.dll').write_bytes((ROOT/'native/sfnative.dll').read_bytes())
  config=dict(FrameGenOutside=4,FrameGenRefresh=False,FrameGenRefreshRest=False,FrameStats=False,LowRenderScale=100,LowHalfRender=False,LowSharpen=False,LowFsr=False)
  steps=['game D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai','fxpreset 6','set FxRays true','auto on','press','wait 12','fxstate','fgstate',
   'set LowRenderScale 67','set LowFsr true','wait 3','fxstate','fgstate',
   'set LowFsr false','set LowRenderScale 100','set LowHalfRender true','wait 3','fxstate','fgstate',
   'set LowHalfRender false','wait 3','fxstate','fgstate',
   'set FrameGenRefresh true','set FrameGenRefreshRest true','wait 5','fxstate','fgstate',
   'set FrameGenOutside 0','fxpreset 0','wait 3','fxstate','fgstate','quit']
  why,metrics=sf.sf_run(steps,settings=config,tag='new-effects-compat',timeout_min=4)
  log=sf.read_text(sf.PLAYER_LOG);(a.out/'game.log').write_text(log,encoding='utf8');(a.out/'run.txt').write_text(why,encoding='utf8')
  fx=re.findall(r'\[화면효과상태\] ([^\r\n]+)',log);fg=re.findall(r'\[프레임상태\] ([^\r\n]+)',log)
  if metrics['errors'] or any(s in log for s in ['단계 실패','native failure','Crash!!!','[화면 효과] 실패:']):raise RuntimeError('Compatibility failed; preserve evidence')
  if len(fx)!=6 or len(fg)!=6:raise RuntimeError('Missing lifecycle observations')
  if any('enabled=True ready=True failed=False' not in s for s in fx[:5]) or 'enabled=False ready=False' not in fx[-1] or 'coordinator=False size=0x0' not in fx[-1]:raise RuntimeError('Effect cleanup/ready mismatch')
  expected=[True,True,False,True,True,False]
  if [('active=True native_installed=1' in s) for s in fg]!=expected or 'active=False native_installed=0' not in fg[-1]:raise RuntimeError('FSR/Half/rest output lifecycle mismatch')
  conditions=re.findall(r'\[곡 시작\] 화면: ([^\r\n]+)',log)
  if len(conditions)!=1 or '3440x1440 165Hz' not in conditions[0] or '창 3440x1440' not in conditions[0]:raise RuntimeError('Actual3440/165 required')
  data=dict(scope='Normal PlayerAuto strong neon+rays at4x; FSR67, HalfRender suspension/resume, deadline snapshot-rest and all OFF. Functional only, not motion/pixel equivalence, FPS loss or latency.',fx=fx,fg=fg,conditions=conditions,metrics=metrics,
    managed_sha256=hashlib.sha256((mod/'StutterFix.dll').read_bytes()).hexdigest(),native_sha256=hashlib.sha256((mod/'sfnative.dll').read_bytes()).hexdigest())
  (a.out/'summary.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(data,ensure_ascii=False))
 finally:
  if sf.game_running():sf.sf_quit()
  if sf.game_running():raise RuntimeError('Normal quit required; no overwrite while game alive')
  for n,b in original.items():(mod/n).write_bytes(b)
  assert all((mod/n).read_bytes()==b for n,b in original.items())
  print('Installed3 original bytes restored exactly')
if __name__=='__main__':main()

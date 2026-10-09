"""Explicit fullscreen lifecycle regressions; no forced kill, exact install/settings restore."""
import argparse,hashlib,json,re,subprocess,time
from pathlib import Path
from measure_maps import ROOT,sf

def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--only',choices=['hello','arche']);p.add_argument('--full',action='store_true');a=p.parse_args()
 if sf.game_running():raise RuntimeError('Existing game must close normally')
 a.out.mkdir(exist_ok=False,parents=True);mod=Path(sf.MOD_DIR)
 old={n:(mod/n).read_bytes() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
 for n,b in old.items():(a.out/(n+'.original')).write_bytes(b)
 sf.launch_game=lambda:subprocess.Popen([r'C:\Program Files (x86)\Steam\steam.exe','-applaunch','977950'])
 results=[]
 try:
  (mod/'StutterFix.dll').write_bytes((ROOT/'bin/PlayerAuto/StutterFix.dll').read_bytes())
  (mod/'sfnative.dll').write_bytes((ROOT/'native/sfnative.dll').read_bytes())
  for name,fg,refresh,start,map_path in [('hello',4,False,0,r'D:\얼불춤 맵 파일\HELLO (BPM) 2026\level.adofai'),('arche',2,True,1,r'D:\얼불춤 맵 파일\Arche\backup.adofai')]:
   if a.only and a.only!=name:continue
   if a.full:start=1
   print('START '+name,flush=True);backup,_=sf.apply_settings({'ExpFullscreen':start,'FrameGenOutside':fg,'FrameGenRefresh':refresh,'FrameStats':False})
   try:
    steps=['wait 4','fgstate','game '+map_path,'auto on','press','wait 9','fgstate']
    if a.full:steps+=['waitend 600','wait 3','fgstate']
    elif name=='hello':steps+=['set ExpFullscreen 1','wait 5','fgstate','set ExpFullscreen 0','wait 4','fgstate','set ExpFullscreen 1','set ExpFullscreen 0','wait 3','fgstate','set ExpFullscreen 1','wait 8','fgstate']
    else:steps+=['set ExpFullscreen 0','wait 4','fgstate','set ExpFullscreen 1','wait 8','fgstate','fxpreset 6','wait 5','fxstate','fgstate']
    steps+=['set FrameGenOutside 0','wait 3','fgstate','set FrameGenOutside '+str(fg),'wait 5','fgstate','set ExpFullscreen 0','wait 3','fgstate','set FrameGenOutside 0','wait 3','fgstate']
    why,_,sec=sf._batch(steps,12 if a.full else 5)
    if why!='끝':raise RuntimeError('Batch failed: '+why)
    sf._batch(['quit'],.5,launch_ok=False)
   except Exception as e:
    # Quit may remove the process before autotest.end exists. Only accept its
    # explicit final message plus engine cleanup, never arbitrary process exit.
    print('batch end: '+str(e),flush=True)
   finally:
    end=time.monotonic()+20
    while sf.game_running() and time.monotonic()<end:time.sleep(1)
    log=sf.read_text(sf.PLAYER_LOG);(a.out/(name+'.log')).write_text(log,encoding='utf8')
    if sf.game_running():raise RuntimeError('Game still running; retain test state, no forced kill')
    sf.restore_settings(backup)
   states=re.findall(r'\[화면상태\] mode=(\w+) pending=(\w+) applied=(\w+) window=([^\r\n]+)',log)
   fgstates=re.findall(r'\[프레임상태\] active=(\w+) native_installed=(\d+) sources=(\d+) generated=(\d+) runtime_initialized=(\w+) status=([^\r\n]*)',log)
   fatal=[l for l in log.splitlines() if any(k in l for k in ['Crash!!!','native failure','해제 확인 시간 초과','[프레임 늘리기]','전환 실패:'])]
   if fatal or '[자동 시험] 끝 - 게임을 끕니다' not in log or 'Shutdown.' not in log:raise RuntimeError('Abnormal shutdown/failure: '+str(fatal))
   if not states or not any(s[0]=='ExclusiveFullScreen' and s[1]=='False' for s in states) or states[-1][:3]!=('FullScreenWindow','False','False'):raise RuntimeError('Actual exclusive/restore missing')
   if not fgstates or fgstates[-1][0:2]!=('False','0') or any(s[-1] for s in fgstates):raise RuntimeError('Native final cleanup failed')
   if not any(s[0]=='True' and int(s[3])>0 for s in fgstates):raise RuntimeError('Generated outputs missing')
   if a.full and '[상태] Won' not in log:raise RuntimeError('Full song did not reach Won')
   result={'full_song':a.full,'won':('[상태] Won' in log),'map':name,'mode':'refresh' if refresh else '4x','startup_exclusive':bool(start),'screen_states':states,'framegen_states':fgstates,'transitions':re.findall(r'\[화면 출력\] ([^\r\n]+)',log),'conditions':re.findall(r'\[곡 시작\] 화면: ([^\r\n]+)',log),'normal_exit':True,'fatal_lines':fatal,'sf_errors':sf.metrics(log)['errors'],'managed_sha256':hashlib.sha256((mod/'StutterFix.dll').read_bytes()).hexdigest(),'native_sha256':hashlib.sha256((mod/'sfnative.dll').read_bytes()).hexdigest()}
   results.append(result);(a.out/'summary.json').write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n',encoding='utf8');print('DONE '+name,flush=True)
 finally:
  if sf.game_running():raise RuntimeError('Close game normally before restoring')
  for n,b in old.items():(mod/n).write_bytes(b)
  assert all((mod/n).read_bytes()==b for n,b in old.items())
  print('Exact originals restored',flush=True)
if __name__=='__main__':main()

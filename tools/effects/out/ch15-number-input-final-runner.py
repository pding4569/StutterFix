import sys,json,re,hashlib,subprocess
from pathlib import Path
ROOT=Path('E:/StutterFixWork/StutterFix');sys.path.insert(0,str(ROOT/'tools/framegenlab'))
from measure_maps import sf
out=ROOT/'tools/effects/out/ch15-number-input-final';out.mkdir(exist_ok=False)
mod=Path(sf.MOD_DIR);names=['StutterFix.dll','sfnative.dll','Settings.xml'];original={n:(mod/n).read_bytes() for n in names}
shot=mod/'shots/fx-number-input.png';oldshot=shot.read_bytes() if shot.exists() else None
try:
 if sf.game_running():raise RuntimeError('Existing game')
 sf.launch_game=lambda:subprocess.Popen(['C:/Program Files (x86)/Steam/steam.exe','-applaunch','977950'])
 (mod/'StutterFix.dll').write_bytes((ROOT/'bin/PlayerAuto/StutterFix.dll').read_bytes())
 (mod/'sfnative.dll').write_bytes((ROOT/'native/sfnative.dll').read_bytes())
 steps=['wait 3','game D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai','auto on','press','wait 10','ui 8','wait 2']
 for value in ['0.75','9','-','NaN','-0.25']:
  steps+=['fxinput fxvib '+value,'wait 0.2','fxpersist']
 steps+=['fxstate','shot fx-number-input','wait 1','fxstate','ui close','set FrameGenOutside 0','wait 2','fgstate','quit']
 config={k:False for k in ['FxColor','FxSharp','FxAA','FxGlow','FxVignette','FxLut','FxLight','FxToneMap','FxRays','FxStreak','FxFlare','FxChromatic','FxGrain','FxCrt','FxPixel','FxPosterize','FxBlur','LowHalfRender','LowFsr','LowSharpen']}
 config.update(FxColor=True,FxVibrance=.55,FxContrast=1.12,FxBrightness=0,FxTemperature=0,FxPreset=6,LowRenderScale=100,FrameGenOutside=4,FrameGenRefresh=False,FrameGenRefreshRest=False,FrameStats=False)
 summary,metrics=sf.sf_run(steps,settings=config,tag='fx-number-input',timeout_min=4)
 log=sf.read_text(sf.PLAYER_LOG);(out/'game.log').write_text(log,encoding='utf8');(out/'run.txt').write_text(summary,encoding='utf8')
 values=[float(v) for v in re.findall(r'\[화면효과입력\] key=fxvib text=\S+ value=(\S+)',log)]
 persisted=re.findall(r'\[화면효과저장\] float_fields=(\d+) mismatches=(\d+) preset=(\d+)',log)
 if values!=[.75,1,1,1,-.25] or len(persisted)!=5 or any(b!='0' or p!='4' for n,b,p in persisted):raise RuntimeError('GUI numeric path or saved values failed: '+str((values,persisted)))
 if metrics['errors'] or '단계 실패' in log or '[화면 효과] 실패:' in log or '시간 초과로 끔' in summary:raise RuntimeError('Game failed')
 conditions=re.findall(r'\[곡 시작\] 화면: ([^\r\n]+)',log)
 if len(conditions)!=1 or not all(s in conditions[0] for s in ['3440x1440 165Hz','창 3440x1440','수직동기 0']):raise RuntimeError('Screen changed')
 (out/'fx-number-input.png').write_bytes(shot.read_bytes())
 data=dict(scope='Actual IMGUI numeric acceptance and Custom Save path, explicit AUTOTEST text injection; not physical keyboard/mouse automation. Screenshot/functional run, excluded from performance.',inputs=['0.75','9','-','NaN','-0.25'],values=values,persistence=[dict(float_fields=int(n),mismatches=int(b),preset=int(p)) for n,b,p in persisted],conditions=conditions[0],errors=metrics['errors'],fx_times=re.findall(r'\[화면효과시각\] ([^\r\n]+)',log),managed_sha256=hashlib.sha256((mod/'StutterFix.dll').read_bytes()).hexdigest())
 (out/'summary.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(data,ensure_ascii=False))
finally:
 if sf.game_running():
  game=subprocess.run(['powershell','-NoProfile','-Command',"(Get-Process -Name 'A Dance of Fire and Ice').CloseMainWindow()"],capture_output=True)
  import time
  for _ in range(20):
   if not sf.game_running():break
   time.sleep(1)
 if sf.game_running():raise RuntimeError('Close normally before restoration')
 for n,b in original.items():(mod/n).write_bytes(b)
 if oldshot is None:shot.unlink(missing_ok=True)
 else:shot.write_bytes(oldshot)
 assert all((mod/n).read_bytes()==b for n,b in original.items());print('Exact3 restored')


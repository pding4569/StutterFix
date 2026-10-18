import sys,json,re,hashlib,subprocess
from pathlib import Path
root=Path('E:/StutterFixWork/StutterFix');sys.path.insert(0,str(root/'tools/framegenlab'))
from measure_maps import sf
out=root/'tools/framegenlab/out/ch31-default-off';out.mkdir(exist_ok=False)
mod=Path(sf.MOD_DIR);original={n:(mod/n).read_bytes() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
try:
 if sf.game_running():raise RuntimeError('Existing game')
 sf.launch_game=lambda:subprocess.Popen(['C:/Program Files (x86)/Steam/steam.exe','-applaunch','977950'])
 (mod/'StutterFix.dll').write_bytes((root/'bin/PlayerAuto/StutterFix.dll').read_bytes())
 (mod/'sfnative.dll').write_bytes((root/'native/sfnative.dll').read_bytes())
 settings={k:False for k in ['FxColor','FxSharp','FxAA','FxGlow','FxVignette','FxLut','FxLight','FxGlowStack','FxToneMap','FxRays','FxStreak','FxFlare','FxChromatic','FxGrain','FxCrt','FxPixel','FxPosterize','FxBlur']}
 settings.update(FrameGenOutside=0,FrameGenRefresh=True,FxPreset=0)
 summary,metrics=sf.sf_run(['wait 3','fgstate','fxstate','quit'],settings=settings,tag='ch31-default-off',timeout_min=2)
 log=sf.read_text(sf.PLAYER_LOG);(out/'game.log').write_text(log,encoding='utf8')
 fg=re.findall(r'\[프레임상태\] ([^\r\n]+)',log);fx=re.findall(r'\[화면효과상태\] ([^\r\n]+)',log)
 assert len(fg)==1 and 'active=False native_installed=0 sources=0 generated=0 runtime_initialized=False status='==fg[0],fg
 assert len(fx)==1 and 'enabled=False ready=False failed=False' in fx[0],fx
 assert metrics['errors']==0
 (out/'summary.json').write_text(json.dumps(dict(scope='Fresh game menu, default OFF with refresh preference true; no map or screenshot.',framegen=fg[0],effects=fx[0],errors=0,log_sha256=hashlib.sha256((out/'game.log').read_bytes()).hexdigest()),ensure_ascii=False,indent=2),encoding='utf8')
 print('Default OFF: initialized/hooks/sources/generated=0, effects ready=False, errors=0',flush=True)
finally:
 if sf.game_running():sf.sf_quit()
 if sf.game_running():raise RuntimeError('Normal quit before restore')
 for n,b in original.items():(mod/n).write_bytes(b)
 assert all((mod/n).read_bytes()==b for n,b in original.items())
 print('Exact originals restored',flush=True)

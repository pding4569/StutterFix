from pathlib import Path
import sys,json,time,subprocess,hashlib,zipfile,shutil,re
r=Path(r'E:/StutterFixWork/wt-282');base=Path(r'E:/StutterFixWork/StutterFix')
sys.path.insert(0,str(base/'tools/framegenlab'));from measure_maps import sf
out=r/'tools/ReleaseCheck/out';mod=Path(sf.MOD_DIR);helper=mod.parent/'StutterFixReleaseCheck'
assert not sf.game_running(),'Existing game; no changes'
assert not helper.exists(),'Existing helper; do not overwrite'
params=Path(sf.GAME_DIR)/'A Dance of Fire and Ice_Data/Managed/UnityModManager/Params.xml';parambytes=params.read_bytes()
settings=(mod/'Settings.xml').read_bytes();backup=Path(r'E:/StutterFixWork/install-backups/release-2.8.2/installed-original');backup.mkdir(parents=True,exist_ok=True)
names=['Settings.xml','Info.json','StutterFix.dll','sfnative.dll','effects-LICENSE.txt']
for n in names:
 if (mod/n).exists():shutil.copy2(mod/n,backup/n)
present={n:(mod/n).exists() for n in names}
zp=r/'dist/StutterFix-2.8.2-player.zip';package=dict(zip_sha256=hashlib.sha256(zp.read_bytes()).hexdigest(),managed_sha256=hashlib.sha256(zipfile.ZipFile(zp).read('StutterFix/StutterFix.dll')).hexdigest())
with zipfile.ZipFile(zp) as z:
 for n in ['Info.json','StutterFix.dll','effects-LICENSE.txt']:(mod/n).write_bytes(z.read('StutterFix/'+n))
assert hashlib.sha256((mod/'StutterFix.dll').read_bytes()).hexdigest()==package['managed_sha256']
helper.mkdir();shutil.copy2(base/'tools/ReleaseCheck/bin/Release/netstandard2.1/ReleaseCheck.dll',helper/'ReleaseCheck.dll')
(helper/'Info.json').write_text(json.dumps(dict(Id='StutterFixReleaseCheck',DisplayName='Temporary release validation',Author='naro',Version='1.0.0',ManagerVersion='0.22.14.0',AssemblyName='ReleaseCheck.dll',EntryMethod='Main.Load')),encoding='utf8')
(helper/'map.txt').write_text(r'D:\얼불춤 맵 파일\HELLO (BPM) 2026\level.adofai',encoding='utf8')
try:
 # Quartz 는 이 게임 판에서 매 프레임 예외를 던져 끊김 검증을 흐리므로 이번 실행 동안만 끈다(끝에 원래 Params.xml 로 되돌림)
 txt=parambytes.decode('utf-8-sig');txt=re.sub(r'(<Mod Id="Quartz" Enabled=")true',r'\1false',txt)
 params.write_bytes((b'\xef\xbb\xbf' if parambytes.startswith(b'\xef\xbb\xbf') else b'')+txt.encode('utf-8'))
 sf.launch_game()
 end=time.monotonic()+90
 while not sf.game_running() and time.monotonic()<end:time.sleep(1)
 assert sf.game_running(),'No game start'
 end=time.monotonic()+660;reported=set()
 while sf.game_running() and time.monotonic()<end:
  log=sf.read_text(sf.PLAYER_LOG)
  for state in ['Release ZIP unchanged','OPEN requested','PLAY requested','COMPLETE OnLandOnPortal','QUIT complete=True']:
   if state in log and state not in reported:print(state,flush=True);reported.add(state)
  if 'FAILED ' in log and '[StutterFixReleaseCheck]' in log:print('Inspect helper failure',flush=True)
  time.sleep(2)
 assert not sf.game_running(),'Still running; no forced kill'
 log=sf.read_text(sf.PLAYER_LOG);(out/'full-game-2.8.2.log').write_text(log,encoding='utf8')
 assert 'COMPLETE OnLandOnPortal' in log and 'QUIT complete=True' in log and 'Shutdown.' in log
 assert 'Crash!!!' not in log and 'native failure' not in log
 assert sf.metrics(log)['errors']==0
 summaries=re.findall(r'\[곡\] 평균[^\r\n]+',log);assert summaries
 result=dict(version='2.8.2',normal_player_zip_sha256=package['zip_sha256'],installed_dll_sha256=hashlib.sha256((mod/'StutterFix.dll').read_bytes()).hexdigest(),unchanged_player_dll=True,autotest_in_player=False,external_playback_helper=True,map='HELLO 2026',complete=True,normal_quit=True,sf_errors=0,summary=summaries[-1],performance_ab=False,quartz_disabled_for_run=True,launch='direct (nosteam.txt)')
 (out/'full-game-2.8.2.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8');print('Normal release ZIP completed',flush=True)
finally:
 if sf.game_running():raise RuntimeError('Game still running; preserve state, do not force terminate')
 try:sf.steam_dll_restore()
 except Exception as e:print('steam restore failed',e)
 params.write_bytes(parambytes)
 for n in names:
  if present[n]:shutil.copy2(backup/n,mod/n)
  elif (mod/n).exists():(mod/n).unlink()
 (mod/'Settings.xml').write_bytes(settings)
 assert helper.resolve().parent==mod.parent.resolve() and helper.name=='StutterFixReleaseCheck'
 shutil.rmtree(helper)
 assert (mod/'Settings.xml').read_bytes()==settings
 print('Helper removed, Settings/UMM params/installed files restored to the pre-check state',flush=True)

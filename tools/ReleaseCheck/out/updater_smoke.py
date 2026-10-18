from pathlib import Path
import sys,json,time,subprocess,hashlib,zipfile,ctypes,csv,io,xml.etree.ElementTree as ET
r=Path(r'E:/StutterFixWork/StutterFix');sys.path.insert(0,str(r/'tools/framegenlab'));from measure_maps import sf
out=r/'tools/ReleaseCheck/out';mod=Path(sf.MOD_DIR)
assert not sf.game_running(),'Existing game; no changes'
orig={n:(mod/n).read_bytes() for n in ['Info.json','StutterFix.dll','Settings.xml','sfnative.dll']}
package=json.loads((out/'package.json').read_text(encoding='utf-8-sig'))
assert json.loads(orig['Info.json'])['Version']=='2.6.0'
try:
 with zipfile.ZipFile(r/'dist/StutterFix-2.5.3.3-tester.zip') as z:
  for n in ['Info.json','StutterFix.dll']:(mod/n).write_bytes(z.read('StutterFix/'+n))
 settings=ET.fromstring(orig['Settings.xml']);settings.find('CheckUpdates').text='true';ET.ElementTree(settings).write(mod/'Settings.xml',encoding='utf-8',xml_declaration=True)
 subprocess.Popen([r'C:\Program Files (x86)\Steam\steam.exe','-applaunch','977950'])
 end=time.monotonic()+90
 while not sf.game_running() and time.monotonic()<end:time.sleep(1)
 assert sf.game_running(),'No game start'
 end=time.monotonic()+200;updated=False
 while sf.game_running() and time.monotonic()<end:
  log=sf.read_text(sf.PLAYER_LOG)
  if '[업데이트] v2.6.0 설치 (다시 켜면 적용)' in log:updated=True;break
  time.sleep(2)
 assert sf.game_running(),'Game exited during update'
 # Close only this game's visible main window, without killing the process.
 u=ctypes.windll.user32;u.GetWindowThreadProcessId.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_ulong)];u.IsWindowVisible.argtypes=[ctypes.c_void_p];u.GetClientRect.argtypes=[ctypes.c_void_p,ctypes.c_void_p];u.PostMessageW.argtypes=[ctypes.c_void_p,ctypes.c_uint,ctypes.c_size_t,ctypes.c_ssize_t]
 raw=subprocess.run(['tasklist','/FI','IMAGENAME eq A Dance of Fire and Ice.exe','/NH','/FO','CSV'],capture_output=True,text=True).stdout
 rows=[x for x in csv.reader(io.StringIO(raw)) if x and x[0].lower()==sf.EXE.lower()];assert len(rows)==1;pid=int(rows[0][1]);windows=[]
 callback_type=ctypes.WINFUNCTYPE(ctypes.c_bool,ctypes.c_void_p,ctypes.c_void_p)
 def visit(hwnd,_):
  p=ctypes.c_ulong();u.GetWindowThreadProcessId(hwnd,ctypes.byref(p))
  if p.value==pid and u.IsWindowVisible(hwnd):
   rect=(ctypes.c_long*4)();u.GetClientRect(hwnd,ctypes.byref(rect));windows.append(((rect[2]-rect[0])*(rect[3]-rect[1]),hwnd))
  return True
 callback=callback_type(visit);u.EnumWindows(callback,None);assert windows and max(windows)[0]>100000
 assert u.PostMessageW(max(windows)[1],0x10,0,0)
 end=time.monotonic()+45
 while sf.game_running() and time.monotonic()<end:time.sleep(1)
 assert not sf.game_running(),'Normal close pending'
 log=sf.read_text(sf.PLAYER_LOG);(out/'updater-game.log').write_text(log,encoding='utf8')
 assert updated,'Updater install not observed'
 assert 'Shutdown.' in log and 'Crash!!!' not in log and sf.metrics(log)['errors']==0
 assert json.loads((mod/'Info.json').read_text(encoding='utf-8-sig'))['Version']=='2.6.0'
 assert hashlib.sha256((mod/'StutterFix.dll').read_bytes()).hexdigest()==package['managed_sha256']
 proof={'previous_version':'2.5.3.3','updated_version':'2.6.0','actual_download_install':True,'installed_dll_matches_release':True,'normal_exit':True,'sf_errors':0,'different_pc':'확인 안 됨'}
 (out/'updater.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf8');print('Actual older tester -> 2.6.0 automatic download/install verified',flush=True)
finally:
 if sf.game_running():raise RuntimeError('Still running; no forced kill or live restore')
 for n,b in orig.items():(mod/n).write_bytes(b)
 print('2.6.0 remains installed; exact Settings restored',flush=True)

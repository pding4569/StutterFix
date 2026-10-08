"""Run elevated with Python; child consoles isolate PresentMon's stop handling.

The controller sends only a game PID, ASCII capture name, and final quit request.
No DLL/settings changes, process killing, global-session stop, or GUI interaction.
"""
import csv,ctypes,json,re,subprocess,time,traceback
from pathlib import Path
root=Path(r'C:\Users\Public\StutterFixTrace\fg27')
if not ctypes.windll.shell32.IsUserAnAdmin():
 raise RuntimeError('Start this measurement helper with administrator permission')
ctypes.windll.kernel32.SetConsoleCtrlHandler(None,True)
def status(**values):
 p=root/'agent2-status.pending';p.write_text(json.dumps(values),encoding='utf-8');p.replace(root/'agent2-status.json')
def start(arguments):
 s=subprocess.STARTUPINFO();s.dwFlags=subprocess.STARTF_USESHOWWINDOW;s.wShowWindow=subprocess.SW_HIDE
 return subprocess.Popen(['C:/SFBundle/PresentMon.exe',*arguments],startupinfo=s,creationflags=subprocess.CREATE_NEW_CONSOLE)
status(status='ready')
end=time.monotonic()+10800
while time.monotonic()<end:
 request=root/'command2.json'
 if not request.exists():time.sleep(.1);continue
 p=json.loads(request.read_text());request.unlink()
 if p['action']=='quit':break
 try:
  name=p['name'];pid=int(p['pid'])
  if p['action']!='pm' or not re.fullmatch('[a-zA-Z0-9_-]+',name) or pid<=0:raise ValueError('invalid request')
  task=subprocess.check_output(['tasklist','/FI',f'PID eq {pid}','/NH','/FO','CSV'],text=True,errors='replace')
  if not any(r and r[0].lower()=='a dance of fire and ice.exe' for r in csv.reader(task.splitlines())):raise ValueError('expected game process')
  output=root/(name+'.csv')
  if output.exists():raise ValueError('preserve existing CSV')
  pm=start(['--process_id',str(pid),'--session_name','SF27-'+name,'--output_file',str(output),'--v1_metrics','--qpc_time','--timed','240','--terminate_after_timed','--terminate_on_proc_exit','--no_console_stats'])
  status(status='recording',name=name,pid=pm.pid)
  while pm.poll() is None:
   stop=root/('stop-'+name)
   if stop.exists():
    closer=start(['--session_name','SF27-'+name,'--terminate_existing_session']);closer.wait(timeout=15);stop.unlink()
   time.sleep(.1)
  status(status='finished',name=name,exit=pm.returncode)
 except Exception:
  status(status='error',name=p.get('name'),error=traceback.format_exc())
status(status='ended')

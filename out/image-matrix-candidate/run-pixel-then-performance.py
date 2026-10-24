from pathlib import Path
import json,time,sys,re
r=Path.cwd(); sys.path.insert(0,str(Path.home()/'Documents/StutterFix/tools/sfmeasure'));import server
base=r/'tools/optimization/out'
def wait(name):
 p=base/name/'manifest.json'
 while True:
  if p.exists():
   m=json.loads(p.read_text(encoding='utf-8-sig'))
   if m.get('ended'):
    assert m['state']=='PASSED' and not m.get('game_running') and len(m.get('restored',{}))==9 and all(m['restored'].values()),name+' failed or restore incomplete'
    return p.parent,m
  time.sleep(3)
p,m=wait('round110-image-native-matrix')
lines=(p/'Player.log').read_text(encoding='utf-8-sig',errors='replace').splitlines()
comparison=next(x for x in lines if x.startswith('[StutterFix] [framecmp] diff OLD110 NEW110A'))
assert re.search(r' changed=0/',comparison),comparison
print('round110 PIXEL_ZERO and restored9; performance sequence starting',flush=True)
for mode,num in [('image',111),('old',112)]:
 plan=json.loads((base/('round108-'+mode+'-steps.json')).read_text(encoding='utf-8-sig'))
 plan['steps']=[x.replace('bundle-v1','bundle-v2') for x in plan['steps']]
 path=base/('round'+str(num)+'-'+mode+'-performance-steps.json');path.write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
 name='round'+str(num)+'-'+mode+'-performance'
 print(server.call('sf_run',{'steps':[],'trace':{'op':'start','workspace':str(r),'plan':str(path.relative_to(r)).replace('\\','/'),'run':name,'repeat_reason':'Image-native matrix passed actual pixel gate. Natural zero-start same-DLL image ownership '+mode+'; first capture-free heavy 250..260 and 260..270 windows for this implementation.'}}),flush=True)
 p,m=wait(name)
 text=(p/'Player.log').read_text(encoding='utf-8-sig',errors='replace')
 rows=[x for x in text.splitlines() if x.startswith('[StutterFix] [gpudeco] micro_end') or x.startswith('[StutterFix] [gpudeco] image_playback')]
 print(name+' '+json.dumps(rows[-4:]),flush=True)
print('capture-free image/old sequence complete and originals restored',flush=True)

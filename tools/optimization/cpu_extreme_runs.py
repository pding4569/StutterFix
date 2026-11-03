"""Serial fresh-process experiments; only run_locked.py starts the game."""
import hashlib,json,re,subprocess,sys
from pathlib import Path

ROOT=Path('E:/StutterFixWork')
WT=ROOT/'wt-partial'
STEPS=WT/'docs/deco-spec/results/cpu-steps'
OUT=ROOT/'night-results'
MOD=Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix')
MAP=Path('D:/얼불춤 맵 파일')
MAPS={'exit':'Exit from ship/Exit from SHiP_T2_.adofai',
      'wf':'Windflower/Windflower.adofai',
      'ar':'Arche/[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai'}

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def run(name,steps,gpu=0,wt='wt-partial',strict=True):
    spec={'tag':name,'timeout_min':25,'settings':{'GpuDeco':gpu,'FrameGenOutside':0,'FrameGenRefresh':False},
          'steps':['timeout 1200','wait 2','auto on','fpscap 0']+steps+['quit']}
    f=STEPS/(name+'.json');f.write_text(json.dumps(spec,ensure_ascii=False,indent=2),encoding='utf-8')
    artifact=ROOT/wt/'bin/PlayerAuto/StutterFix.dll'
    evidence={'dll_sha256':sha(artifact),'worktree':wt,'spec':str(f)}
    print('RUN',name,flush=True)
    result=subprocess.run([sys.executable,str(ROOT/'run_locked.py'),str(f),wt,name,'--keys',
        'FPS 구간,fixture,단계 실패,BARRIER_NOTIFY,live_state,idlefloor,holdreads,framecmp,diff,assertzero,effect_exception,원본 처리로 복귀',
        '--tail','5'],cwd=ROOT,capture_output=True,text=True,encoding='utf-8',errors='replace')
    print(result.stdout,flush=True)
    if result.stderr:print(result.stderr[-500:],flush=True)
    log=OUT/(name+'.log')
    evidence.update(returncode=result.returncode,
        restored_dll=sha(MOD/'StutterFix.dll')==sha(ROOT/'restore/StutterFix-rc-player.dll'),
        probe_absent=not (MOD/'gpuprobe.dll').exists(),
        gpu_default_zero=bool(re.search(r'<GpuDeco>0</GpuDeco>',(MOD/'Settings.xml').read_text(encoding='utf-8-sig'))),
        step_failure=not log.exists() or '단계 실패' in log.read_text(encoding='utf-8',errors='replace'))
    (OUT/(name+'.integrity.json')).write_text(json.dumps(evidence,indent=2),encoding='utf-8')
    assert evidence['restored_dll'] and evidence['probe_absent'] and evidence['gpu_default_zero'],evidence
    if strict:assert not evidence['step_failure'], 'step failure: '+name
    return not evidence['step_failure']

def game(k):return 'game '+str(MAP/MAPS[k])

mode=sys.argv[1]
if mode=='windows':
    run('cpu-idle-oracle',[game('exit').replace('game ','open ',1),'idlefloor check','seek 90 8','waitsongpos 100 160',
        'idlefloor verify','idlefloor state'])
    for i,on in enumerate((False,True,True,False)):
        run(f'cpu-idle-exit-{i}',[game('exit').replace('game ','open ',1),'idlefloor '+('on' if on else 'off'),'seek 90 8',
            'waitsongpos 90 160','fpsmark start','waitsongpos 100 60','fpsmark end idle-exit','idlefloor state'])
    for i,gpu in enumerate((0,1,1,0)):
        run(f'cpu-flash-wf-{i}',[game('wf'),'press','waitsongpos 8 120','fpsmark start',
            'waitsongpos 22 90','fpsmark end flash-wf','gpudeco live state'],gpu)
elif mode=='hold':
    for i,arg in enumerate(('check','off','on','on','off')):
        run(f'cpu-hold-exit-{i}',[game('exit').replace('game ','open ',1),'holdreads '+arg,'seek 90 8',
            'waitsongpos 90 160','fpsmark start','waitsongpos 100 60','fpsmark end hold-exit',
            'holdreads '+('verify' if arg=='check' else 'state')])
elif mode=='pixel':
    original=json.loads((WT/'docs/deco-spec/results/evidence/g1/g1-ar-262-controls.steps.json').read_text(encoding='utf-8-sig'))
    steps=original['steps'][3:-1]
    run('cpu-ar262-cand',steps,1,'wt-cand')
elif mode=='notify':
    MAPS['dd']='DDONGSSADA3302 asansis remake/level.adofai'
    for k in ('exit','ar','dd'):
        run('cpu-notify-'+k,['gpudeco drawcollapse notify check',game(k),'press','waitend 1200',
            'gpudeco drawcollapse notify state','gpudeco drawcollapse notify verify'],1,strict=False)
else:raise SystemExit('windows|pixel|notify')

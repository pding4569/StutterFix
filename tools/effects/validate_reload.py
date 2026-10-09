"""Visual/functional hot reload with distinct assemblies; never force-kill."""
import argparse,hashlib,json,re,shutil,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/framegenlab'))
from measure_maps import sf

def main():
    p=argparse.ArgumentParser();p.add_argument('--next-dll',type=Path,required=True);p.add_argument('--identity',type=Path,required=True);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
    if sf.game_running():raise RuntimeError('Existing game must be closed normally')
    before=(ROOT/'bin/PlayerAuto/StutterFix.dll').read_bytes();after=a.next_dll.read_bytes()
    identity=json.loads(a.identity.read_text(encoding='utf-8-sig'))
    if before==after or identity['before_mvid']==identity['after_mvid']:raise RuntimeError('A genuinely different assembly is required')
    if identity['before_sha256']!=hashlib.sha256(before).hexdigest() or identity['after_sha256']!=hashlib.sha256(after).hexdigest():raise RuntimeError('Assembly identity is stale')
    a.out.mkdir(parents=True,exist_ok=False);mod=Path(sf.MOD_DIR)
    original={n:(mod/n).read_bytes() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
    for n,b in original.items():(a.out/(n+'.original')).write_bytes(b)
    shot=mod/'shots/fx-reload.png';old_shot=shot.read_bytes() if shot.exists() else None
    phases=[]
    def batch(name,steps,timeout=3):
        why,log,seconds=sf._batch(steps,timeout)
        (a.out/(name+'.log')).write_text(log,encoding='utf8')
        phases.append(dict(name=name,why=why,seconds=seconds))
        if why not in ('끝','다시 불러오기'):raise RuntimeError('Batch failed: '+name+' '+why)
        return log
    try:
        (mod/'StutterFix.dll').write_bytes(before);(mod/'sfnative.dll').write_bytes((ROOT/'native/sfnative.dll').read_bytes())
        config={n:False for n in ['FxColor','FxSharp','FxAA','FxGlow','FxLight','FxVignette','FxLut','LowHalfRender','LowSharpen','LowFsr']}
        config.update(FxColor=True,FxSharp=True,FxGlow=True,FxVignette=True,FxPreset=3,LowRenderScale=100,FrameGenOutside=4,FrameGenRefresh=False,FrameStats=False)
        sf.apply_settings(config)
        first=batch('before',['wait 3','game D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai','auto on','press','wait 12','fxstate','fgstate'])
        (mod/'StutterFix.dll').write_bytes(after)
        reload_log=batch('reload',['reload'],2);time.sleep(3)
        if 'DLL 이 그대로라' in reload_log or '다시 불러오기' not in reload_log:raise RuntimeError('Reload was skipped')
        # ResetCustomLevel is a coroutine. A same-frame press can be consumed
        # before the reset reaches its input-wait state, leaving a static menu.
        last=batch('after',['wait 2','fxstate','fgstate','retry','wait 3','auto on','press','wait 12','fxstate','fgstate','shot fx-reload','wait 2',
            'set FrameGenOutside 0','set FxColor false','set FxSharp false','set FxGlow false','set FxVignette false','wait 2','fxstate','fgstate'])
        whole=sf.read_text(sf.PLAYER_LOG);(a.out/'game.log').write_text(whole,encoding='utf8')
        if any(x in whole for x in ['다시 불러오기가 깨졌습니다','다시 불러오기 실패:','[화면 효과] 실패:','native failure','Crash!!!','단계 실패']):raise RuntimeError('Reload failed; preserve evidence')
        fx=re.findall(r'\[화면효과상태\] ([^\r\n]+)',first+last)
        fg=re.findall(r'\[프레임상태\] ([^\r\n]+)',first+last)
        if len(fx)!=4 or len(fg)!=4 or any('enabled=True ready=True' not in s for s in fx[:3]) or 'enabled=False ready=False' not in fx[-1] or 'coordinator=False size=0x0' not in fx[-1]:raise RuntimeError('Effect lifecycle state mismatch')
        if 'active=True native_installed=1' not in fg[2] or 'active=False native_installed=0' not in fg[-1]:raise RuntimeError('Output lifecycle state mismatch')
        clocks=re.findall(r'\[화면효과시각\] song_s=([\d.]+) unity_frame=(\d+) effect_frame=(\d+) glow=(True|False)',last)
        if len(clocks)!=3 or not 5<float(clocks[1][0])<25 or '[상태] PlayerControl' not in last:raise RuntimeError('Actual replay not confirmed; do not accept an input-wait screenshot')
        old_frames=int(re.search(r'frames=(\d+)',fx[0])[1]);new_frames=int(re.search(r'frames=(\d+)',fx[1])[1])
        if new_frames>=old_frames:raise RuntimeError('Fresh effect static counters were not observed')
        shutil.copyfile(shot,a.out/'hello-neon-reloaded.png')
        data=dict(scope='Functional/visual only; reload, retry and screenshot excluded from performance.',identity=identity,before_sha256=hashlib.sha256(before).hexdigest(),after_sha256=hashlib.sha256(after).hexdigest(),phases=phases,fx_states=fx,fg_states=fg,fresh_static_frames=[old_frames,new_frames],capture_metadata=re.findall(r'화면 캡처: [^\r\n]*?\| ([^\r\n]+)',whole))
        (a.out/'summary.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(data,ensure_ascii=False),flush=True)
    finally:
        if sf.game_running():
            try:sf._batch(['quit'],1,launch_ok=False)
            except Exception:pass
            deadline=time.monotonic()+20
            while sf.game_running() and time.monotonic()<deadline:time.sleep(1)
        if sf.game_running():raise RuntimeError('Game did not close normally; originals preserved on disk, no force kill or overwrite')
        for n,b in original.items():(mod/n).write_bytes(b)
        if old_shot is None:shot.unlink(missing_ok=True)
        else:shot.write_bytes(old_shot)
        assert all((mod/n).read_bytes()==b for n,b in original.items())
        print('Installed originals restored exactly after normal quit',flush=True)

if __name__=='__main__':main()

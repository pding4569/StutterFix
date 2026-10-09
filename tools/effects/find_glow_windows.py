"""Locate real level intervals where the optional glow actually executes."""
import argparse,hashlib,json,re,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/framegenlab'))
from measure_maps import sf

def main():
    p=argparse.ArgumentParser();p.add_argument('--map',type=Path,required=True);p.add_argument('--dll',type=Path,required=True);p.add_argument('--seconds',type=int,required=True);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
    if sf.game_running():raise RuntimeError('Existing game must be closed normally')
    if not 4<=a.seconds<=240:raise RuntimeError('Use a bounded pre-end level interval')
    a.out.mkdir(parents=True,exist_ok=False);mod=Path(sf.MOD_DIR)
    original={n:(mod/n).read_bytes() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
    for n,b in original.items():(a.out/(n+'.original')).write_bytes(b)
    try:
        (mod/'StutterFix.dll').write_bytes(a.dll.read_bytes());(mod/'sfnative.dll').write_bytes((ROOT/'native/sfnative.dll').read_bytes())
        config={n:False for n in ['FxColor','FxSharp','FxAA','FxGlow','FxLight','FxVignette','FxLut','LowHalfRender','LowSharpen','LowFsr']}
        config.update(FxGlow=True,LowRenderScale=100,FrameGenOutside=0,FrameGenRefresh=False,FrameStats=False)
        sf.apply_settings(config)
        steps=['game '+str(a.map),'auto on','press','fxstate']
        for _ in range(a.seconds//2):steps+=['wait 2','fxstate']
        why,log,elapsed=sf._batch(steps,a.seconds/60+3)
        (a.out/'game.log').write_text(sf.read_text(sf.PLAYER_LOG),encoding='utf8')
        (a.out/'batch.log').write_text(log,encoding='utf8')
        if why!='끝' or '[상태] PlayerControl' not in log or '[화면 효과] 실패:' in log:raise RuntimeError('Scout did not execute the real level correctly')
        conditions=re.findall(r'\[곡 시작\] 화면: ([^\r\n]+)',log)
        if len(conditions)!=1 or '3440x1440 165Hz' not in conditions[0]:raise RuntimeError('Actual screen conditions changed')
        states=re.findall(r'\[화면효과상태\] ([^\r\n]+)',log)
        clocks=re.findall(r'\[화면효과시각\] song_s=([^ ]+) unity_frame=(\d+) effect_frame=(-?\d+) glow=(True|False)',log)
        if len(states)!=len(clocks) or len(states)<3:raise RuntimeError('Explicit effect/frame clock correspondence missing')
        records=[]
        for s,c in zip(states,clocks):
            fields=dict(re.findall(r'(\w+)=([^ ]*)',s));records.append(dict(song_s=float(c[0]),unity_frame=int(c[1]),effect_frame=int(c[2]),last_glow=c[3]=='True',frames=int(fields['frames']),rest=int(fields['glow_rest']),custom_skip=int(fields['custom_source_skipped']),size=fields['size']))
        windows=[]
        for old,new in zip(records,records[1:]):
            frames=new['frames']-old['frames'];rest=new['rest']-old['rest'];active=frames-rest
            if active<0 or frames<0 or new['song_s']<old['song_s']:raise RuntimeError('Counter/time rewind; preserve without combining')
            if active:windows.append(dict(from_song_s=old['song_s'],to_song_s=new['song_s'],executed_glow_frames=active,processed_frames=frames,rest_frames=rest,custom_skip=new['custom_skip']-old['custom_skip']))
        data=dict(scope='Visual diagnostics only, 2s sampled real-level glow execution counters. FrameGen OFF. Windows identify candidates, not visible motion or interpolation quality. No screenshots/readbacks/ETW/elevation; no complete-song claim.',conditions=conditions[0],seconds=elapsed,managed_sha256=hashlib.sha256(a.dll.read_bytes()).hexdigest(),records=records,active_windows=windows)
        (a.out/'summary.json').write_text(json.dumps(data,indent=2),encoding='utf8');print(json.dumps(dict(samples=len(records),active_windows=windows)),flush=True)
    finally:
        if sf.game_running():
            try:sf._batch(['quit'],1,launch_ok=False)
            except Exception:pass
            deadline=time.monotonic()+20
            while sf.game_running() and time.monotonic()<deadline:time.sleep(1)
        if sf.game_running():raise RuntimeError('Normal quit required; no force kill or installation overwrite')
        for n,b in original.items():(mod/n).write_bytes(b)
        assert all((mod/n).read_bytes()==b for n,b in original.items())
        print('Installed originals restored exactly',flush=True)

if __name__=='__main__':main()

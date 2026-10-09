"""Original game final output, with FrameGen fully OFF. Visual only, no elevation."""
import argparse,csv,hashlib,json,re,shutil,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/framegenlab'))
from measure_maps import sf

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--glow',action='store_true');p.add_argument('--window',type=float,nargs=2,default=[86,89]);p.add_argument('--seconds',type=int,default=94);a=p.parse_args();begin,end=a.window
    if not 0<=begin<end<=a.seconds-3 or end-begin>4:p.error('Use a bounded <=4s window with >=3s readback drain')
    if sf.game_running():raise RuntimeError('Existing game must close normally')
    a.out.mkdir(parents=True,exist_ok=False);mod=Path(sf.MOD_DIR);diagnostic=mod/'framegen-raw'
    if diagnostic.exists():raise RuntimeError('Preserve previous raw output first')
    original={n:(mod/n).read_bytes() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
    for n,b in original.items():(a.out/(n+'.original')).write_bytes(b)
    try:
        dll=ROOT/'tools/framegenlab/out/raw/StutterFix.dll';(mod/'StutterFix.dll').write_bytes(dll.read_bytes());(mod/'sfnative.dll').write_bytes((ROOT/'native/sfnative.dll').read_bytes())
        config={n:False for n in ['FxColor','FxSharp','FxAA','FxGlow','FxLight','FxVignette','FxLut','LowHalfRender','LowSharpen','LowFsr']}
        config.update(FxGlow=a.glow,LowRenderScale=100,FrameGenOutside=0,FrameGenRefresh=False,FrameStats=False);sf.apply_settings(config)
        steps=[f'rawstart {begin} {end}','game D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai','auto on','press',f'wait {a.seconds}','fxstate','fgstate','rawsave']
        try:why,log,elapsed=sf._batch(steps,5)
        finally:(a.out/'game.log').write_text(sf.read_text(sf.PLAYER_LOG),encoding='utf8')
        (a.out/'batch.log').write_text(log,encoding='utf8');(a.out/'game.log').write_text(sf.read_text(sf.PLAYER_LOG),encoding='utf8')
        if why!='끝' or '[상태] PlayerControl' not in log or '[원본캡처] saved ' not in log:raise RuntimeError('Raw playback/capture failed: '+why)
        conditions=re.findall(r'\[곡 시작\] 화면: ([^\r\n]+)',log)
        if len(conditions)!=1 or '3440x1440 165Hz' not in conditions[0]:raise RuntimeError('Actual screen conditions changed')
        shutil.copytree(diagnostic,a.out/'capture')
        state=(diagnostic/'state.txt').read_text();first=dict(part.split('=',1) for part in state.splitlines()[0].split())
        if any(int(first[n]) for n in ['dropped','errors','inflight']) or 'failure=\n' not in state or not all(s in state for s in ['active=False','native_installed=0','runtime_initialized=False']):raise RuntimeError('OFF/readback state failed; preserve evidence')
        rows=list(csv.DictReader((diagnostic/'frames.csv').open(newline='')))
        if len(rows)<max(4,int((end-begin)*30)) or any(r['ready']!='1' or not begin<=float(r['song_s'])<end for r in rows):raise RuntimeError('Incomplete raw window')
        if any(not (diagnostic/f'raw-{int(r["index"]):03d}.ppm').is_file() for r in rows):raise RuntimeError('Missing completed raw pixels')
        data=dict(scope='Visual only. Unity end-of-frame final game screenshot, UI included, bilinear step6 downsample and four asynchronous readback slots. No native Present connection/generation. CPU submission/row-copy timing is diagnostic cost, not GPU or physical latency. Files written only after the window on explicit rawsave. This capture path is not claimed byte-identical to native samples.',conditions=conditions[0],glow_requested=a.glow,seconds=elapsed,state=state,frames=len(rows),song_range=[float(rows[0]['song_s']),float(rows[-1]['song_s'])],exact_effect_frame=sum(r['fx_frame']==r['unity_frame'] for r in rows),glow_exact_effect_frames=sum(r['fx_frame']==r['unity_frame'] and r['fx_glow']=='1' for r in rows),managed_sha256=hashlib.sha256(dll.read_bytes()).hexdigest(),native_sha256=hashlib.sha256((ROOT/'native/sfnative.dll').read_bytes()).hexdigest())
        (a.out/'summary.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(data,ensure_ascii=False),flush=True)
    finally:
        if sf.game_running():
            try:sf._batch(['quit'],1,launch_ok=False)
            except Exception:pass
            deadline=time.monotonic()+20
            while sf.game_running() and time.monotonic()<deadline:time.sleep(1)
        if sf.game_running():raise RuntimeError('Normal quit required; no force kill or installation overwrite')
        for n,b in original.items():(mod/n).write_bytes(b)
        assert all((mod/n).read_bytes()==b for n,b in original.items());print('Installed originals restored exactly',flush=True)
        if diagnostic.exists():
            if not (a.out/'capture').exists():shutil.copytree(diagnostic,a.out/'capture')
            # This exact run-owned directory was absent at entry. No computed parent deletion.
            expected=Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix/framegen-raw').absolute()
            if diagnostic.resolve()!=expected:raise RuntimeError('Refuse recursive removal outside the exact run-owned directory')
            shutil.rmtree(diagnostic)

if __name__=='__main__':main()

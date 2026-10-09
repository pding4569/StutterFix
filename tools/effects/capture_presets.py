"""Full-resolution preset comparisons; explicit visual runs, never performance data."""
import argparse, hashlib, json, re, shutil, sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/framegenlab'))
from measure_maps import sf


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--map',type=Path,required=True)
    p.add_argument('--label',choices=['hello','arche'],required=True)
    p.add_argument('--song',type=float,default=20)
    p.add_argument('--out',type=Path,required=True)
    a=p.parse_args()
    if sf.game_running() or not a.map.is_file() or not 5<=a.song<=120:
        p.error('Closed game, existing map, song time5..120 required')
    a.out.mkdir(parents=True,exist_ok=False)
    mod=Path(sf.MOD_DIR)
    original={n:(mod/n).read_bytes() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
    profiles=['off','sharp','clear','neon']
    names=[f'shader29-{a.label}-{profile}-song{a.song:g}' for profile in profiles]
    shots={mod/'shots'/(n+'.png'):None for n in names}
    shots={f:f.read_bytes() if f.exists() else None for f in shots}
    try:
        for n,b in original.items(): (a.out/(n+'.original')).write_bytes(b)
        for f in shots: f.unlink(missing_ok=True)
        binary=(ROOT/'bin/PlayerAuto/StutterFix.dll').read_bytes()
        native=(ROOT/'native/sfnative.dll').read_bytes()
        (mod/'StutterFix.dll').write_bytes(binary)
        (mod/'sfnative.dll').write_bytes(native)
        steps=['wait 3','game '+str(a.map)]
        for index,name in enumerate(names):
            if index: steps+=['retry','wait 3']
            steps += [f'fxpreset {index}','auto on','press','wait 3',
                      f'shotat {a.song:g} {name}','wait 2','fxstate','fgstate']
        config={n:False for n in ['FxColor','FxSharp','FxAA','FxGlow','FxLight','FxVignette','FxLut','LowHalfRender','LowSharpen','LowFsr']}
        config.update(FxPreset=0,FrameGenOutside=0,FrameGenRefresh=False,FrameStats=False,LowRenderScale=100)
        summary,metrics=sf.sf_run(steps+['quit'],settings=config,timeout_min=12,tag='shader29-preset-'+a.label)
        log=sf.read_text(sf.PLAYER_LOG)
        (a.out/'game.log').write_text(log,encoding='utf8')
        (a.out/'run.txt').write_text(summary,encoding='utf8')
        # Preserve new PNG evidence before any validity guard rejects this trial.
        for name,profile in zip(names,profiles):
            f=mod/'shots'/(name+'.png')
            if f.exists(): shutil.copyfile(f,a.out/(profile+'.png'))
        if metrics['errors'] or any(s in log for s in ['Crash!!!','단계 실패','[화면 효과] 실패:','native failure']):
            raise RuntimeError('Visual trial failed; preserve evidence')
        conditions=re.findall(r'\[곡 시작\] 화면: ([^\r\n]+)',log)
        screen_samples=re.findall(r'\[예약 화면\] window=(\d+)x(\d+) display=(\d+)x(\d+) hz=([\d.]+) sync=(\d+)',log)
        if not conditions or len(set(conditions))!=1 or not all(s in conditions[0] for s in ['3440x1440 165Hz','창 3440x1440','수직동기 0']) or len(screen_samples)!=4 or any(tuple(map(int,s[:4]))!=(3440,1440,3440,1440) or round(float(s[4]))!=165 or s[5]!='0' for s in screen_samples):
            raise RuntimeError('Four identical actual3440x1440/165Hz conditions required')
        clock=re.findall(r'\[예약 캡처\] target_song_s=([\d.]+) request_song_s=([\d.]+) unity_frame=(\d+)',log)
        states=re.findall(r'\[프레임상태\] ([^\r\n]+)',log)
        if len(clock)!=4 or len(states)!=4 or any('active=False native_installed=0' not in s for s in states):
            raise RuntimeError('Four timed captures with frame generation fully OFF required')
        fx_states=re.findall(r'\[화면효과상태\] ([^\r\n]+)',log)
        if len(fx_states)!=4 or 'enabled=False' not in fx_states[0] or any(not all(token in s for token in ['enabled=True ready=True failed=False','coordinator=True','size=3440x1440']) or not re.search(r'frames=[1-9]\d*\b',s) for s in fx_states[1:]):
            raise RuntimeError('All three active presets must execute their actual material/coordinator')
        captures=[]
        sheet=Image.new('RGB',(1720,800),'#101012');draw=ImageDraw.Draw(sheet)
        for i,(name,profile,(target,song,frame)) in enumerate(zip(names,profiles,clock)):
            f=a.out/(profile+'.png')
            image=Image.open(f);image.load()
            if image.size!=(3440,1440) or not 0<=float(song)-float(target)<.05:
                raise RuntimeError('Full resolution or matching-time tolerance failed: '+name)
            destination=f
            captures.append(dict(profile=profile,target_song_s=float(target),request_song_s=float(song),
                request_unity_frame=int(frame),resolution=list(image.size),sha256=hashlib.sha256(destination.read_bytes()).hexdigest()))
            x=(i%2)*860;y=(i//2)*400
            draw.text((x+12,y+10),f'{a.label.upper()} | {profile.upper()} | request {float(song):.6f}s | 3440 x 1440 original',fill='#EEEFF1')
            sheet.paste(image.convert('RGB').resize((860,360),Image.Resampling.LANCZOS),(x,y+40))
        sheet.save(a.out/'comparison.png')
        data=dict(scope='Visual only; four presets in one process with full retry and identical song target. Original PNG bytes preserved. Contact sheet downsized4x; request timestamp is not GPU capture-completion time. No performance/latency or subjective quality conclusion.',
            conditions=conditions[0],capture_screen_samples=screen_samples,captures=captures,fx_states=fx_states,
            framegen_states=states,managed_sha256=hashlib.sha256(binary).hexdigest(),native_sha256=hashlib.sha256(native).hexdigest(),game_metrics=metrics)
        (a.out/'summary.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
        print(json.dumps(data,ensure_ascii=False),flush=True)
    finally:
        if sf.game_running():sf.sf_quit()
        if sf.game_running():raise RuntimeError('Normal shutdown required before restoration')
        for n,b in original.items(): (mod/n).write_bytes(b)
        for f,b in shots.items():
            if b is None:f.unlink(missing_ok=True)
            else:f.write_bytes(b)
        assert all((mod/n).read_bytes()==b for n,b in original.items())
        print('Installed originals and existing shots restored exactly',flush=True)


if __name__=='__main__':main()

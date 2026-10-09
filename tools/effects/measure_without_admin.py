"""General-build effect cost, bracketing controls; no ETW or elevation."""
import argparse,json,re,subprocess,sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
LAB=ROOT/'tools/framegenlab'
sys.path.insert(0,str(LAB))
from measure_maps import sf

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--map',type=Path,required=True)
    p.add_argument('--out',type=Path,required=True)
    p.add_argument('--preset',type=int,choices=[3,5,6],default=3)
    p.add_argument('--rays',action='store_true',help='Add Godrays to the active preset only')
    p.add_argument('--steam-exe',type=Path,help='Optional ordinary Steam -applaunch path')
    a=p.parse_args()
    if sf.game_running():raise RuntimeError('Existing game must be closed normally')
    a.out.mkdir(parents=True,exist_ok=False)
    native=Path(sf.MOD_DIR)/'sfnative.dll';original=native.read_bytes()
    results=[]
    try:
        native.write_bytes((ROOT/'native/sfnative.dll').read_bytes())
        active_name={3:'neon',5:'clear-strong',6:'neon-strong'}[a.preset]+('-rays' if a.rays else '')
        for name,active in [('off-before',False),(active_name,True),('off-after',False)]:
            config={n:False for n in ['FxColor','FxSharp','FxAA','FxGlow','FxLight','FxVignette','FxLut','FxGlowStack','FxToneMap','FxRays','FxStreak','FxFlare','FxChromatic','FxGrain','FxCrt','FxPixel','FxPosterize','FxBlur','LowHalfRender','LowSharpen','LowFsr']}
            config.update(LowRenderScale=100,FxPreset=a.preset if active else 0)
            if active:
                config.update(FxColor=True,FxSharp=True,FxGlow=a.preset!=5,FxVignette=a.preset!=5,
                    FxVibrance=.65 if a.preset==5 else .55 if a.preset==6 else .2,
                    FxContrast=1.22 if a.preset==5 else 1.12 if a.preset==6 else 1.08,
                    FxBrightness=-.1 if a.preset==6 else 0,FxTemperature=0,FxSharpAmount=.4,
                    FxGlowAmount=1.2 if a.preset==6 else .25,FxGlowThreshold=.28 if a.preset==6 else .75,
                    FxVignetteAmount=.15,FxGlowStack=a.preset==6,FxToneMap=a.preset==6,FxCeiling=.97,
                    FxRays=a.rays,FxRaysAmount=.8,FxRaysX=.5,FxRaysY=.25,FxRaysLength=.8)
            settings=a.out/(name+'.json');settings.write_text(json.dumps(config),encoding='utf8')
            trial=a.out/name
            cmd=[sys.executable,str(LAB/'normal_smoke.py'),'--map',str(a.map),'--out',str(trial),'--mode','4','--no-shot','--fx-state','--settings',str(settings)]
            if a.steam_exe:cmd+=['--steam-exe',str(a.steam_exe)]
            print('START '+name,flush=True)
            with (a.out/(name+'.log')).open('w',encoding='utf8') as f:subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,check=True)
            data=json.loads((trial/'summary.json').read_text(encoding='utf8'))
            log=(trial/'game.log').read_text(encoding='utf8')
            if '[상태] PlayerControl' not in log:
                raise RuntimeError('Real level playback not observed; an active output counter is insufficient')
            if '[화면 효과] 실패:' in log or (active and '[화면 효과] 번들 준비' not in log):
                raise RuntimeError('Configured effect did not prepare successfully; preserve logs')
            conditions=re.findall(r'\[곡 시작\] 화면: ([^\r\n]+)',log)
            if len(conditions)!=1 or not all(s in conditions[0] for s in ['3440x1440 165Hz','창 3440x1440','수직동기 0']):
                raise RuntimeError('Actual screen conditions changed; preserve trial without combining')
            reported=[dict(rounded_ms=int(ms),song_s=float(song),cause='unconfirmed') for ms,song in re.findall(r'\[모니터\] (\d+)ms[^\r\n]*?곡 ([\d.]+)초',log)]
            fx=re.findall(r'\[화면효과상태\] ([^\r\n]+)',log)
            if len(fx)!=2 or (active and any('enabled=True ready=True failed=False' not in s for s in fx)):
                raise RuntimeError('Missing endpoint effect readiness; preserve evidence')
            completed=[int(re.search(r'frames=(\d+)',s)[1]) for s in fx]
            if active and completed[1]<=completed[0]:raise RuntimeError('No completed effect frames')
            if not active and any('enabled=False ready=False' not in s for s in fx):
                raise RuntimeError('OFF effect unexpectedly active')
            if active and a.preset==6 and any('glow_rest=0 ' not in s for s in fx):
                raise RuntimeError('Strong neon must execute through map Bloom; preserve logs')
            if results and (conditions[0]!=results[0]['conditions'] or
                data['managed_sha256']!=results[0]['managed_sha256'] or data['native_sha256']!=results[0]['native_sha256']):
                raise RuntimeError('Trial conditions or installed binaries changed; do not combine')
            results.append(dict(profile=name,settings=config,conditions=conditions[0],sample=data['sample'],managed_sha256=data['managed_sha256'],native_sha256=data['native_sha256'],errors=data['game_metrics']['errors'],effect_prepared='[화면 효과] 번들 준비' in log,effect_states=fx,completed_effect_frames=completed[1]-completed[0],effect_execution_fraction='not measured: endpoint counters only',reported_hitches=reported))
            (a.out/'summary.json').write_text(json.dumps(dict(scope='General PlayerAuto, fixed4x, FX OFF/selected preset/OFF. Unity frame/QPC35s after wait10. No screenshots, ETW, GPU readbacks or research sampler. Exact native song window and physical display latency unconfirmed.',runs=results),indent=2),encoding='utf8')
        before,after=results[0]['sample']['real_fps'],results[-1]['sample']['real_fps']
        data=dict(scope='Incremental selected-effect cost at fixed4x, not total FrameGen loss. Separate fresh games, adjacent OFF controls, general Unity frame/QPC window. Exact native song window, physical display FPS and display latency unconfirmed.',preset=a.preset,rays=a.rays,runs=results,
            effect_real_fps_loss_percent_vs_controls=[100*(1-results[1]['sample']['real_fps']/x) for x in (before,after)])
        (a.out/'summary.json').write_text(json.dumps(data,indent=2),encoding='utf8');print(json.dumps(data),flush=True)
    finally:
        if sf.game_running():sf.sf_quit()
        if sf.game_running():raise RuntimeError('Normal shutdown required before native restoration')
        native.write_bytes(original)
        assert native.read_bytes()==original
        print('Native original restored exactly',flush=True)

if __name__=='__main__':main()

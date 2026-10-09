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
    a=p.parse_args()
    if sf.game_running():raise RuntimeError('Existing game must be closed normally')
    a.out.mkdir(parents=True,exist_ok=False)
    native=Path(sf.MOD_DIR)/'sfnative.dll';original=native.read_bytes()
    results=[]
    try:
        native.write_bytes((ROOT/'native/sfnative.dll').read_bytes())
        for name,neon in [('off-before',False),('neon',True),('off-after',False)]:
            config={n:False for n in ['FxColor','FxSharp','FxAA','FxGlow','FxLight','FxVignette','FxLut','LowHalfRender','LowSharpen','LowFsr']}
            config.update(LowRenderScale=100,FxPreset=3 if neon else 0)
            if neon:config.update(FxColor=True,FxSharp=True,FxGlow=True,FxVignette=True)
            settings=a.out/(name+'.json');settings.write_text(json.dumps(config),encoding='utf8')
            trial=a.out/name
            cmd=[sys.executable,str(LAB/'normal_smoke.py'),'--map',str(a.map),'--out',str(trial),'--mode','4','--no-shot','--settings',str(settings)]
            print('START '+name,flush=True)
            with (a.out/(name+'.log')).open('w',encoding='utf8') as f:subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,check=True)
            data=json.loads((trial/'summary.json').read_text(encoding='utf8'))
            log=(trial/'game.log').read_text(encoding='utf8')
            if '[화면 효과] 실패:' in log or (neon and '[화면 효과] 번들 준비' not in log):
                raise RuntimeError('Configured effect did not prepare successfully; preserve logs')
            conditions=re.findall(r'\[곡 시작\] 화면: ([^\r\n]+)',log)
            if len(conditions)!=1 or not all(s in conditions[0] for s in ['3440x1440 165Hz','창 3440x1440','수직동기 0']):
                raise RuntimeError('Actual screen conditions changed; preserve trial without combining')
            reported=[dict(rounded_ms=int(ms),song_s=float(song),cause='unconfirmed') for ms,song in re.findall(r'\[모니터\] (\d+)ms[^\r\n]*?곡 ([\d.]+)초',log)]
            results.append(dict(profile=name,conditions=conditions[0],sample=data['sample'],managed_sha256=data['managed_sha256'],native_sha256=data['native_sha256'],errors=data['game_metrics']['errors'],effect_prepared='[화면 효과] 번들 준비' in log,effect_execution_fraction='not measured',reported_hitches=reported))
            (a.out/'summary.json').write_text(json.dumps(dict(scope='General PlayerAuto, fixed4x, effects OFF/Neon/OFF. Unity frame/QPC35s after wait10. No screenshots, ETW, GPU readbacks or research sampler. Exact native song window and physical display latency unconfirmed.',runs=results),indent=2),encoding='utf8')
        before,after=results[0]['sample']['real_fps'],results[-1]['sample']['real_fps']
        data=dict(scope='Incremental Neon cost at fixed4x, not total FrameGen loss. Separate fresh games, adjacent OFF controls, general Unity frame/QPC window. Exact native song window, physical display FPS and display latency unconfirmed.',runs=results,
            neon_real_fps_loss_percent_vs_controls=[100*(1-results[1]['sample']['real_fps']/x) for x in (before,after)])
        (a.out/'summary.json').write_text(json.dumps(data,indent=2),encoding='utf8');print(json.dumps(data),flush=True)
    finally:
        if sf.game_running():sf.sf_quit()
        if sf.game_running():raise RuntimeError('Normal shutdown required before native restoration')
        native.write_bytes(original)
        assert native.read_bytes()==original
        print('Native original restored exactly',flush=True)

if __name__=='__main__':main()

"""Fresh-game screen-effect GPU comparisons, no screenshots or GPU readbacks."""
import argparse,csv,ctypes,json,subprocess,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'framegenlab'))
from measure_outside import read,stats
HERE=Path(__file__).resolve().parent
LAB=HERE.parent/'framegenlab'

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();a.out.mkdir(parents=True,exist_ok=False)
    base={n:False for n in ['FxColor','FxSharp','FxAA','FxGlow','FxLight','FxVignette','FxLut']}
    base.update(LowSharpen=False,LowHalfRender=False,LowFsr=False,LowRenderScale=100)
    profiles=[('off-before',{}),('color',{'FxColor':True}),('color-lut-vignette',{'FxColor':True,'FxLut':True,'FxVignette':True,'FxLutPath':str((Path('effects/identity16.png')).resolve())}),
        ('sharp',{'FxSharp':True}),('fxaa',{'FxAA':True}),('glow',{'FxGlow':True}),('light',{'FxLight':True}),('off-after',{})]
    results=[];frequency=ctypes.c_longlong();ctypes.windll.kernel32.QueryPerformanceFrequency(ctypes.byref(frequency))
    for index,(name,extra) in enumerate(profiles):
        settings=a.out/(name+'.json');settings.write_text(json.dumps({**base,**extra}),encoding='utf-8')
        target=a.out/name
        cmd=[sys.executable,str(LAB/'measure_fixed_cost.py'),'--map','D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai','--out',str(target),'--stage','fg29gpu-'+str(index),'--order','0','--variant','1','--settings',str(settings)]
        print('START '+name,flush=True)
        with (a.out/(name+'.log')).open('w',encoding='utf-8') as f:subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,check=True)
        batch=json.loads((target/'summary.json').read_text());trial=target/'00-0x'
        scenes=[r for r in read(trial/'capture/cost-sources.csv') if 5<=float(r['song_s'])<45];lo,hi=float(scenes[0]['source_s']),float(scenes[-1]['source_s'])
        rows=list(csv.DictReader((trial/'presentmon.csv').open(encoding='utf-8-sig',newline='')))
        selected=batch['runs'][0]['presentmon']['selected_chain']
        rows=[r for r in rows if lo<=float(r['QPCTime'])/frequency.value<=hi and r['SwapChainAddress']==selected]
        gpu=[float(r['msGPUActive']) for r in rows if r['msGPUActive'] not in ('','NA')]
        if not gpu:raise RuntimeError('No PresentMon GPU values; do not invent shader cost')
        log=(trial/'game.log').read_text(encoding='utf-8')
        if '[화면 효과] 실패:' in log:raise RuntimeError('Effect failed during timing')
        # The scene callback precedes the overlay. Count only explicit completed
        # effect frames, and retain stale/missing records instead of treating a
        # configured glow flag as proof that the level allowed glow to run.
        completed={int(r['fx_frame']):int(r['fx_glow']) for r in scenes
            if int(r['fx_frame'])==int(r['unity_frame'])-1}
        effect_scope=dict(scene_samples=len(scenes),completed_effect_frames=len(completed),
            glow_executed_frames=sum(completed.values()),
            stale_or_missing_effect_records=sum(int(r['fx_frame'])!=int(r['unity_frame'])-1 for r in scenes),
            moving_glow_quality='not measured')
        result=dict(profile=name,settings={**base,**extra},real_fps=batch['runs'][0]['real_fps'],gpu_active_ms=stats(gpu),gpu_samples=len(gpu),effect_scope=effect_scope,presentmon=batch['runs'][0]['presentmon'],build=batch['build'],conditions=batch['conditions'])
        results.append(result);(a.out/'summary.json').write_text(json.dumps(dict(scope='PresentMon v1 msGPUActive, whole true-frame GPU execution in 5..45s. Effect cost is difference from OFF, not isolated per-pass timestamp. No captures. Sequential fresh games; controls bracket the entire batch.',runs=results),indent=2),encoding='utf-8');print(json.dumps(result),flush=True)
    before,after=results[0]['gpu_active_ms']['mean'],results[-1]['gpu_active_ms']['mean']
    real_before,real_after=results[0]['real_fps'],results[-1]['real_fps']
    for r in results[1:-1]:
        r['gpu_increment_ms_off_range']=[r['gpu_active_ms']['mean']-before,r['gpu_active_ms']['mean']-after]
        r['real_fps_loss_percent_off_range']=[100*(1-r['real_fps']/real_before),100*(1-r['real_fps']/real_after)]
        if r['profile']=='glow' and not r['effect_scope']['glow_executed_frames']:
            r['glow_gpu_increment_ms']='not measured: level bloom/rest or unconfirmed completed effect'
    (a.out/'summary.json').write_text(json.dumps(dict(scope='Whole-frame PresentMon GPU execution difference, not isolated per-pass timestamp; OFF variation retained. 3440x1440 actual game, no captures.',runs=results),indent=2),encoding='utf-8')
if __name__=='__main__':main()

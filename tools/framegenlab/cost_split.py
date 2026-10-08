"""One ordered OFF/copy-only/2x/4x comparison, identical real-scene sampler, fresh process per trial."""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path
from measure_outside import analyze, cost_scenes, read, sf

HERE=Path(__file__).resolve().parent


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--map',type=Path,required=True)
    p.add_argument('--out',type=Path,required=True)
    p.add_argument('--width',type=int,default=2560)
    p.add_argument('--resume',action='store_true',help='Reuse completed trials; recover only the known mode1 GPU-coverage analysis error')
    a=p.parse_args()
    a.out.mkdir(parents=True,exist_ok=a.resume)
    result=dict(order=[0,1,2,4],scope='One ordered batch; no repeated or closing OFF control',runs=[])
    condition=None
    build=None
    for i,mode in enumerate(result['order']):
        root=a.out/f'{i:02d}-{mode}x'
        command=[sys.executable,str(HERE/'measure_outside.py'),'--map',str(a.map),'--label',f'ch26-cost-{mode}',
                 '--mode',str(mode),'--seconds','55','--cost-split','--out',str(root),'--timeout-min','4']
        if mode: command+=['--block-flow','--block-variant','3']
        if a.resume and root.exists():
            if not (root/'summary.json').exists():
                failure=(a.out/f'{i:02d}-{mode}x.log').read_text(encoding='utf-8')
                run=(root/'run.txt').read_text(encoding='utf-8')
                if mode!=1 or 'RuntimeError: Incomplete generated GPU coverage' not in failure or '끝 - 게임을 끕니다' not in run:
                    raise RuntimeError('Unrecognized incomplete trial; preserve it')
                safe=dict(part.split('=',1) for part in (root/'capture/safety.txt').read_text().split())
                if int(safe['worker_error']) or safe['frame_begins']!=safe['frame_ends']:
                    raise RuntimeError('Recovered mode1 safety failed')
                if any(r['real']=='0' for r in read(root/'capture/presents.csv')) or (root/'capture/block-flow.txt').exists():
                    raise RuntimeError('Recovered mode1 generated or searched')
                recovered=dict(label='ch26-cost-1',mode=1,cost_split=True,scene_metrics=cost_scenes(root/'capture'),
                               native=analyze(root/'capture',1),safety=safe,build=build,
                               game_metrics=sf.metrics((root/'game.log').read_text(encoding='utf-8')),
                               recovery='Existing successful trial, fixed postprocessing condition mode>=2; no rerun')
                (root/'summary.json').write_text(json.dumps(recovered,ensure_ascii=False,indent=2),encoding='utf-8')
            print(f'REUSE cost {i+1}/4 mode={mode}',flush=True)
        else:
            print(f'START cost {i+1}/4 mode={mode}',flush=True)
            done=subprocess.run(command,capture_output=True,text=True,encoding='utf-8',errors='replace')
            (a.out/f'{i:02d}-{mode}x.log').write_text(done.stdout+done.stderr,encoding='utf-8')
            if done.returncode: raise RuntimeError(f'Trial failed; no retry: {root.name}')
        data=json.loads((root/'summary.json').read_text(encoding='utf-8'))
        log=(root/'game.log').read_text(encoding='utf-8')
        found=re.findall(r'\[곡 시작\] 화면: 수직동기 (\d+), 목표 FPS (\d+), (\w+), (\d+)x(\d+) (\d+)Hz, 창 (\d+)x(\d+)',log)
        if not found or found[-1][0]!='0': raise RuntimeError('Missing sync0 screen conditions')
        current=found[-1]
        if (int(current[6]),int(current[7]))!=(a.width,1440): raise RuntimeError('Unexpected actual game size')
        if condition is not None and current!=condition: raise RuntimeError('Screen conditions changed')
        if build is not None and build!=data['build']: raise RuntimeError('Binary changed between trials')
        condition=current; build=data['build']
        if mode:
            safe=data['safety']
            if int(safe['worker_error']) or safe['frame_begins']!=safe['frame_ends']: raise RuntimeError('Safety check failed')
        scenes=data['scene_metrics']
        fps=scenes['real_fps']
        base=result['runs'][0]['real_scene_fps'] if result['runs'] else fps
        previous_ms=result['runs'][-1]['mean_scene_ms'] if result['runs'] else 1000/fps
        row=dict(mode=mode,real_scene_fps=fps,mean_scene_ms=1000/fps,loss_vs_off_percent=100*(1-fps/base),
                 increment_vs_previous_stage_ms=1000/fps-previous_ms,
                 output_submissions_per_second=data['native']['output_fps'] if mode else None,
                 native=data['native'],safety=data['safety'],scene_metrics=scenes,game_metrics=data['game_metrics'])
        result['runs'].append(row)
        result['conditions']=dict(zip(['vsync','target_fps','fullscreen_mode','width','height','refresh_hz','window_width','window_height'],current))
        result['build']=build
        (a.out/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print(json.dumps({k:row[k] for k in ['mode','real_scene_fps','loss_vs_off_percent','increment_vs_previous_stage_ms']},ensure_ascii=False),flush=True)


if __name__=='__main__': main()

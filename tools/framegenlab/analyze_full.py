"""Summarize a completed native whole-song trial without hiding long intervals."""
import argparse
import json
from pathlib import Path

from measure_outside import analyze, read


def analyze_full(root):
    metadata=json.loads((root/'summary.json').read_text(encoding='utf8'))
    mode=metadata['mode']
    if metadata['visual_smoke'] or metadata['scene_pair'] or (root/'capture/visual-pose.txt').exists():
        raise RuntimeError('Whole-song performance evidence must not contain readbacks/smoke tests')
    run=(root/'run.txt').read_text(encoding='utf8')
    if '곡 끝남' not in run or '곡이 끝나지 않음' in run:
        raise RuntimeError('Actual song completion not confirmed')
    folder=root/'capture'
    result=analyze(folder,mode,end=1e9)
    result['mode']=mode
    result['camera_blend']=metadata['camera_blend']
    result['scope']='Whole measured song after5s, fresh run, no readbacks. No paired whole-song OFF control;not a source-loss comparison or visual proof.'
    if metadata['camera_blend']:
        result['prediction_metrics_scope']='Counterfactual extrapolation;not delayed display-camera error'
    for name,file,time in [('source','sources.csv','source_s'),('output','presents.csv','present_s')]:
        rows=read(folder/file)
        result[name+'_gaps_over_33ms']=[dict(frame=int(b['unity_frame']),song_s=float(b['song_s']),
            interval_ms=(float(b[time])-float(a[time]))*1000,
            **({'real':b['real']=='1'} if name=='output' else {}))
            for a,b in zip(rows,rows[1:]) if float(b['song_s'])>=5 and
            (float(b[time])-float(a[time]))*1000>1000/30]
    return result


if __name__=='__main__':
    p=argparse.ArgumentParser()
    p.add_argument('directory',type=Path)
    a=p.parse_args()
    result=analyze_full(a.directory)
    (a.directory/'full-native.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(json.dumps({key:result[key] for key in ['mode','seconds','scene_fps','output_fps',
        'generated_gpu_ms','source_worst_ms','output_worst_ms','source_gaps_over_33ms','output_gaps_over_33ms']},ensure_ascii=False))

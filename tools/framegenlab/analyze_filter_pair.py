"""Offline same-frame input/output proof for one unmodified game image effect."""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image,ImageDraw
from analyze_edge_runs import edge_runs


def analyze(folder):
    source=np.asarray(Image.open(folder/'filter-input.png').convert('RGB')).astype(np.int16)
    output=np.asarray(Image.open(folder/'filter-output.png').convert('RGB')).astype(np.int16)
    if source.shape!=output.shape: raise RuntimeError('Filter pair dimensions differ')
    h,w=source.shape[:2]
    delta=np.max(np.abs(source-output),axis=2)
    black=(np.max(output,axis=2)<=3)&(np.max(source,axis=2)>8)
    center=delta[h//10:9*h//10,w//10:9*w//10]
    result=dict(scope='Same source frame, one original WideScreenHV call;GPU copies before/after,readback after native finish. Not performance or motion proof.',
                size=[w,h],metadata=(folder/'filter-pair.txt').read_text(),
                input_black_edge_runs_px=edge_runs(source),output_black_edge_runs_px=edge_runs(output),
                input_bright_to_output_black_pixels=int(black.sum()),
                input_bright_to_output_black_percent=float(black.mean()*100),
                center_max_channel_error_mean=float(center.mean()),
                center_max_channel_error_p99=float(np.percentile(center,99)))
    if (folder/'filter-mask.png').exists():
        mask=np.asarray(Image.open(folder/'filter-mask.png').convert('RGB')).astype(np.float32)/255
        error=np.max(np.abs(source*mask-output),axis=2)
        result['input_times_mask_model']=dict(mean_max_channel_error=float(error.mean()),
            p99_max_channel_error=float(np.percentile(error,99)),maximum=float(error.max()),
            agreement_within3_percent=float((error<=3).mean()*100))
    return result


if __name__=='__main__':
    p=argparse.ArgumentParser()
    p.add_argument('directory',type=Path)
    p.add_argument('--out',type=Path,required=True)
    p.add_argument('--preview',type=Path)
    a=p.parse_args()
    result=analyze(a.directory)
    a.out.write_text(json.dumps(result,indent=2)+'\n',encoding='utf8')
    if a.preview:
        canvas=Image.new('RGB',(1280,400),(17,17,20)); draw=ImageDraw.Draw(canvas)
        for i,(file,label) in enumerate([('filter-input.png','BEFORE original WideScreenHV'),('filter-output.png','AFTER original WideScreenHV')]):
            im=Image.open(a.directory/file).convert('RGB').resize((640,360),Image.Resampling.LANCZOS)
            canvas.paste(im,(i*640,40));draw.text((i*640+10,10),label,fill='white')
        a.preview.parent.mkdir(parents=True,exist_ok=True);canvas.save(a.preview)
    print(json.dumps(result))

"""Measure black edge runs in existing visual-only clips, not subjective shake."""
import argparse
import csv
import json
from pathlib import Path

import numpy as np
from PIL import Image


def edge_runs(image):
    h,w=image.shape[:2]
    dark=np.max(image,axis=2)<=3
    lines={'left':dark[h//4:3*h//4], 'right':dark[h//4:3*h//4,::-1],
           'top':dark[:,w//4:3*w//4].T,'bottom':dark[::-1,w//4:3*w//4].T}
    result={}
    for key,mask in lines.items():
        valid=~np.all(mask,axis=1)
        result[key]=float(np.median(np.argmax(~mask[valid],axis=1))) if valid.any() else None
    return result


def analyze(root, prediction):
    runs={}
    for label in ['off',prediction,'blend4']:
        folder=root/label
        meta=json.loads((folder/'summary.json').read_text(encoding='utf8'))
        if not meta['visual_smoke']: raise RuntimeError('Requires visual-only clip')
        with (folder/'capture/clip.csv').open(newline='') as f: rows=list(csv.DictReader(f))
        values=[]
        for row in rows:
            path=folder/'capture'/f"clip-{meta['mode']}x-{int(row['index']):03d}-{'real' if row['real']=='1' else 'generated'}.ppm"
            image=np.asarray(Image.open(path).convert('RGB'))
            h,w=image.shape[:2]
            bright=float((np.max(image[h//10:9*h//10,w//10:9*w//10],axis=2)>8).mean())
            values.append(dict(index=int(row['index']),frame=int(row['unity_frame']),
                               song_s=float(row['song_s']),real=row['real']=='1',
                               center_bright_fraction=bright,near_black=bright<.1,**edge_runs(image)))
        sides={}
        for key in ['left','right','top','bottom']:
            v=[row[key] for row in values if row[key] is not None and not row['near_black']]
            sides[key]=dict(minimum=min(v),median=float(np.median(v)),maximum=max(v))
        runs[label]=dict(samples=len(values),near_black_samples=sum(r['near_black'] for r in values),
                        size=list(Image.open(path).size),edges=sides,rows=values)
    return dict(scope='Existing separate HELLO20..23s GPU clips;step6 decimation, quantized edge run units are stored pixels (up to6 original pixels of sampling uncertainty). Near-black center(<10% bright) samples retained but excluded from edge summaries;cause of near-black is unknown. Readbacks affect pacing;not normal performance, physical display or quality approval.',runs=runs)


if __name__=='__main__':
    p=argparse.ArgumentParser()
    p.add_argument('directory',type=Path)
    p.add_argument('--prediction-run',default='predict4-stable')
    p.add_argument('--out',type=Path,required=True)
    a=p.parse_args()
    result=analyze(a.directory,a.prediction_run)
    a.out.write_text(json.dumps(result,indent=2)+'\n',encoding='utf8')
    print(json.dumps({label:{k:v for k,v in run.items() if k!='rows'} for label,run in result['runs'].items()}))

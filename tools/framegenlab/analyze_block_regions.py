"""Supplementary region registration for mixed stationary decorations/moving map.
Never replaces the whole-center reversal check. Rejected regions remain in JSON.
"""
import argparse,csv,json
from pathlib import Path
import numpy as np
from PIL import Image
from analyze_image_motion import phase

def analyze(folder,mode):
    records=[]
    def read(name):
        return np.asarray(Image.open(folder/name).convert('RGB'),np.float32)@np.array([.299,.587,.114],np.float32)
    def valid(p):return p['psr']>=8 and p['texture_std']>=3
    for row in csv.DictReader((folder/'clip.csv').open(newline='')):
        if row['real']=='1':continue
        i=int(row['index']);a=read(f'reference-{mode}x-{i:03d}.ppm');b=read(f'next-reference-{mode}x-{i:03d}.ppm');c=read(f'clip-{mode}x-{i:03d}-generated.ppm')
        h,w=a.shape
        for y in range(3):
            for x in range(3):
                roi=np.s_[int(h*(.2+y*.2)):int(h*(.4+y*.2)),int(w*(.2+x*.2)):int(w*(.4+x*.2))]
                source=phase(a[roi],b[roi]);added=phase(a[roi],c[roi]);ok=valid(source) and valid(added)
                records.append(dict(index=i,song_s=float(row['song_s']),region=[x,y],source=source,added=added,accepted=ok,
                    static_added=ok and source['length']<.1 and added['length']>.25,
                    opposite=ok and source['length']>.25 and added['length']>.25 and source['dx']*added['dx']+source['dy']*added['dy']<0,
                    overshoot=ok and added['length']>source['length']+.25))
    return dict(scope='Nine center regions; PSR>=8/std>=3; stored step6 pixels. Registration is a diagnostic, not ground truth for mixed layers, animation or occlusion.',
        summary=dict(regions=len(records),accepted=sum(r['accepted'] for r in records),static_added=sum(r['static_added'] for r in records),opposite=sum(r['opposite'] for r in records),overshoot=sum(r['overshoot'] for r in records)),rows=records)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('capture',type=Path);p.add_argument('--mode',type=int,required=True);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
    result=analyze(a.capture,a.mode);a.out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result['summary']))

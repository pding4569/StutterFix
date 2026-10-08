"""Measure emitted camera backtracking on stable, shake-free pans; not image quality."""
import argparse
import csv
import json
import math
from pathlib import Path


def analyze(folder, height=1440):
    def load(name):
        with (folder/name).open(newline='') as f: return list(csv.DictReader(f))
    sources={int(r['unity_frame']):r for r in load('sources.csv') if 5<=float(r['song_s'])<19}
    presents=load('presents.csv')
    real={int(r['unity_frame']):r for r in presents if r['real']=='1'}
    generated={}
    for r in presents:
        if r['real']=='0': generated[int(r['unity_frame'])]=r
    jumps=[]
    reverse=[]
    for frame,b in sources.items():
        if any(f not in sources or f not in real for f in [frame-1,frame,frame+1]) or frame not in generated: continue
        a,c=sources[frame-1],sources[frame+1]
        # Exclude game shake, zoom, rotation and direction changes. This isolates
        # smooth camera pans and never calls the remaining game motion incorrect.
        if any(r['scene_rendered']!='1' or abs(float(r['camera_x'])-float(r['base_x']))>1e-4 or abs(float(r['camera_y'])-float(r['base_y']))>1e-4 for r in [a,b,c]): continue
        size=float(b['camera_size'])
        if size<=0 or any(abs(float(r['camera_size'])/size-1)>.001 or abs(float(r['camera_angle'])-float(b['camera_angle']))>1e-4 for r in [a,c]): continue
        xy=lambda r: (float(r['camera_x']),float(r['camera_y']))
        sub=lambda x,y: (x[0]-y[0],x[1]-y[1])
        dot=lambda x,y: x[0]*y[0]+x[1]*y[1]
        before,after=sub(xy(b),xy(a)),sub(xy(c),xy(b))
        if dot(before,after)<=0 or max(math.hypot(*before),math.hypot(*after))>size: continue
        start,g,end=xy(real[frame]),xy(generated[frame]),xy(real[frame+1])
        jump=math.hypot(*sub(end,g))*height/(2*size)
        jumps.append(jump)
        if dot(sub(g,start),sub(end,g))<0 and jump>.1: reverse.append(jump)
    def stats(v):
        s=sorted(v)
        return dict(count=len(s),median=s[len(s)//2],p95=s[int((len(s)-1)*.95)],maximum=s[-1]) if s else None
    return dict(scope='Emitted native camera constants, song5..19 before readbacks;stable shake-free translation only;not pixels or subjective motion proof',eligible=len(jumps),backtracking_over_point1px=len(reverse),transition_jump_px=stats(jumps),backtracking_jump_px=stats(reverse))


if __name__=='__main__':
    p=argparse.ArgumentParser()
    p.add_argument('directory',type=Path)
    p.add_argument('--out',type=Path,required=True)
    a=p.parse_args()
    d=analyze(a.directory)
    a.out.write_text(json.dumps(d,indent=2)+'\n',encoding='utf8')
    print(json.dumps(d))

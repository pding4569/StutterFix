"""Offline independent OFF controls. No rendering/algorithm changes or FPS inference."""
import argparse,csv,hashlib,json,sys
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/framegenlab'))
from analyze_image_motion import phase

def frames(folder,kind):
    raw=kind=='raw'
    rows=list(csv.DictReader((folder/('frames.csv' if raw else 'clip.csv')).open(newline='')))
    data=[]
    for r in rows:
        i=int(r['index'])
        name=f'raw-{i:03d}.ppm' if raw else (f'reference-4x-{i:03d}.ppm' if kind=='reference' else f'clip-4x-{i:03d}-'+('real' if r['real']=='1' else 'generated')+'.ppm')
        path=folder/name
        rgb=np.asarray(Image.open(path).convert('RGB'),np.float32)
        h,w=rgb.shape[:2];gray=rgb@np.array([.299,.587,.114],np.float32)
        data.append(dict(index=i,song=float(r['song_s']),path=path,gray=gray[h//5:4*h//5,w//5:4*w//5]))
    return data,rows

def measure(data):
    values=[]
    for a,b in zip(data,data[1:]):
        r=phase(a['gray'],b['gray'])
        valid=a.get('eligible',True) and b.get('eligible',True) and a['index']!=b['index']
        correlation=r['psr']>=8 and r['texture_std']>=3
        r.update(song_s=b['song'],previous_song_s=a['song'],index=b['index'],accepted=bool(valid and correlation),correlation_accepted=bool(correlation),timing_eligible=bool(valid))
        values.append(r)
    reversals=[b['song_s'] for a,b in zip(values,values[1:]) if a['accepted'] and b['accepted'] and a['length']>.25 and b['length']>.25 and a['dx']*b['dx']+a['dy']*b['dy']<0]
    lengths=[r['length'] for r in values if r['accepted']]
    gaps=[(b['song']-a['song'])*1000 for a,b in zip(data,data[1:])]
    return dict(frames=len(data),pairs=len(values),accepted=sum(r['accepted'] for r in values),ambiguous_song_seconds=[r['song_s'] for r in values if not r['accepted']],correlation_ambiguous_song_seconds=[r['song_s'] for r in values if not r['correlation_accepted']],timing_excluded_song_seconds=[r['song_s'] for r in values if not r['timing_eligible']],reversals=len(reversals),reversal_song_seconds=reversals,length_stored_px=dict(median=float(np.median(lengths)),p95=float(np.percentile(lengths,95)),maximum=max(lengths)) if lengths else None,song_interval_ms=dict(mean=float(np.mean(gaps)),maximum=max(gaps)) if gaps else None,rows=values)

def main():
    p=argparse.ArgumentParser();p.add_argument('--raw-off',type=Path,required=True);p.add_argument('--raw-glow',type=Path,required=True);p.add_argument('--native',type=Path,required=True);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
    rng=np.random.default_rng(192);x=rng.normal(128,30,(80,100));check=phase(x,np.roll(x,(2,-3),(0,1)));assert abs(check['dx']+3)<.1 and abs(check['dy']-2)<.1
    # Same song grid, independent executions. Nearest samples only; no pixel interpolation.
    grid=np.arange(86.02,88.981,.02);series={};selected={};manifests=[];runs={};griddata={};gridoffsets={}
    for glow in [False,True]:
        label='glow' if glow else 'off';rawroot=a.raw_glow if glow else a.raw_off
        runs[label]=json.loads((rawroot/'summary.json').read_text(encoding='utf8'))
        for kind,folder in [('raw',rawroot/'capture'),('actual',a.native/f'hello-{label}'/'capture'),('reference',a.native/f'hello-{label}'/'capture')]:
            data,rows=frames(folder,kind);name=label+'-'+kind
            native_result=measure(data)
            picks=[min(data,key=lambda r:abs(r['song']-t)) for t in grid]
            errors=[float(abs(r['song']-t)*1000) for r,t in zip(picks,grid)]
            griddata[name]=picks;gridoffsets[name]=errors
            series[name]=dict(adjacent_stored=native_result)
            if kind=='raw':
                series[name]['cpu_diagnostic_ms']={key:dict(mean=float(np.mean([float(r[key]) for r in rows])),maximum=max(float(r[key]) for r in rows)) for key in ['submit_cpu_ms','copy_cpu_ms']}
            # New independent original vs earlier generated sample: visual context only.
            if kind!='reference':selected[name]=min(data,key=lambda r:abs(r['song']-86.2))
    # A shared timing mask excludes an entire grid instant if any series misses it.
    shared=[all(gridoffsets[name][i]<=12 for name in griddata) for i in range(len(grid))]
    for name,picks in griddata.items():
        errors=gridoffsets[name]
        common=measure([{**r,'eligible':shared[i]} for i,r in enumerate(picks)])
        common.update(target_grid_hz=50,target_grid_count=len(grid),shared_timing_eligible_instants=sum(shared),maximum_song_offset_ms=max(errors),offset_over12ms=int(sum(e>12 for e in errors)),duplicate_sample_picks=len(picks)-len({r['index'] for r in picks}),within12ms=max(errors)<=12)
        series[name]['common_grid']=common
    common_rows=[s['common_grid']['rows'] for s in series.values()]
    joint=[all(rows[i]['accepted'] for rows in common_rows) for i in range(len(grid)-1)]
    for s in series.values():
        rows=s['common_grid']['rows']
        eligible_tests=[i for i in range(1,len(rows)) if joint[i-1] and joint[i]]
        flags=[i for i in eligible_tests if rows[i-1]['length']>.25 and rows[i]['length']>.25 and rows[i-1]['dx']*rows[i]['dx']+rows[i-1]['dy']*rows[i]['dy']<0]
        s['joint_common_grid']=dict(eligible_pairs=sum(joint),eligible_adjacent_pair_tests=len(eligible_tests),reversals=len(flags),reversal_target_song_seconds=[float(grid[i+1]) for i in flags])
    w,h=Image.open(next(iter(selected.values()))['path']).size
    canvas=Image.new('RGB',(w*2,(h+40)*2),(16,16,18));draw=ImageDraw.Draw(canvas)
    for y,label in enumerate(['off','glow']):
        for col,kind in enumerate(['raw','actual']):
            r=selected[label+'-'+kind];path=r['path']
            title=('NEW FrameGen OFF' if kind=='raw' else 'EARLIER 4x sample')+' | FX glow '+label
            draw.text((col*w+4,y*(h+40)+3),title,fill=(238,239,241))
            draw.text((col*w+4,y*(h+40)+19),f'HELLO {r["song"]:.6f}s | different executions',fill=(160,160,166))
            canvas.paste(Image.open(path).convert('RGB'),(col*w,y*(h+40)+40))
            manifests.append(dict(panel=label+'-'+kind,file=path.name,song_s=r['song'],sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    canvas.save(a.out.with_suffix('.png'))
    result=dict(scope='New independent FrameGen completely OFF final-screen controls vs chapter9 existing 4x samples. Center60%, PSR>=8/std>=3, reversal dot<0 with both displacements >0.25 stored px. Raw Unity bilinear screenshots and native GPU samples are different paths and independent executions. Common50Hz grid uses nearest samples, duplicates/timing offsets retained. A translation statistic is not ground truth for zoom/rotation/grain/filter changes; counts do not establish causality or quality equivalence. Captures excluded from FPS/latency; CPU timings are diagnostic only.',runs=runs,series=series,figure_samples=manifests)
    a.out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps({k:dict(adjacent_reversals=v['adjacent_stored']['reversals'],common_reversals=v['common_grid']['reversals'],joint=v['joint_common_grid'],common_ambiguous=len(v['common_grid']['ambiguous_song_seconds']),max_offset_ms=v['common_grid']['maximum_song_offset_ms'],duplicates=v['common_grid']['duplicate_sample_picks']) for k,v in series.items()}),flush=True)

if __name__=='__main__':main()

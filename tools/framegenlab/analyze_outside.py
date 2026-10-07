"""Offline checks for chapter 15. Reads recorded states; no additional game probes."""
import argparse
import json
import math
from pathlib import Path
from measure_outside import analyze, read, stats


def diagnostic(directory, mode, legacy=False):
    result=analyze(directory,mode)
    source=read(directory/'sources.csv')
    output=read(directory/'presents.csv')
    schedule=read(directory/'schedule.csv') if (directory/'schedule.csv').exists() else []
    byframe={int(r['unity_frame']):r for r in source}
    index={int(r['unity_frame']):i for i,r in enumerate(source)}
    holds={int(r['unity_frame']):float(r['source_hold_ms']) for r in schedule}
    true_end={int(r['unity_frame']):float(r['present_s']) for r in output if r['real']=='1'}
    first=next(i for i,r in enumerate(source) if float(r['song_s'])>=5)
    last=next((i for i,r in enumerate(source[first:],first) if float(r['song_s'])>=45),len(source))
    frames=set(int(r['unity_frame']) for r in source[first:last])
    misses=dict(lock_window=0,new_source_before_due=0,worker_late_other=0,extra_beyond_per_source_quota=0,
                unresolved=0,timing_tolerance_ms=.02)
    if legacy:
        for r in schedule:
            f=int(r['unity_frame']); i=index.get(f)
            if f not in frames or i is None: continue
            made=int(r['generated']); misses['extra_beyond_per_source_quota']+=max(0,made-(mode-1))
            if i<1 or i+1>=len(source):
                misses['unresolved']+=max(0,mode-1-made); continue
            old=source[i]; nxt=source[i+1]; nf=int(nxt['unity_frame'])
            period=min(.05,max(.001,float(old['source_s'])-float(source[i-1]['source_s'])))
            begin=true_end.get(nf,math.nan)-holds.get(nf,math.nan)/1000
            end=float(nxt['source_s'])
            for slot in range(made+1,mode):
                due=float(old['source_s'])+period*slot/mode
                if due>end: misses['new_source_before_due']+=1
                elif math.isfinite(begin) and begin-.00002<=due<=end+.00002: misses['lock_window']+=1
                else: misses['worker_late_other']+=1
        result['discard_reasons_offline']=misses
    residual=[]; old_extra=[]; active=0
    for r in output:
        if r['real']!='0' or int(r['unity_frame']) not in frames: continue
        i=index[int(r['unity_frame'])]
        if i==0 or 'base_x' not in source[i]: continue
        a,b=source[i-1],source[i]
        dt=float(b['song_s' if legacy else 'source_s'])-float(a['song_s' if legacy else 'source_s'])
        if not .00001<dt<.1: continue
        ratio=min(float(r['source_age_ms'])/1000,.05)/dt
        size=float(b['camera_size'])
        if size<=.001: continue
        dx=float(b['base_x'])-float(a['base_x']); dy=float(b['base_y'])-float(a['base_y'])
        da=math.remainder(float(b['base_angle'])-float(a['base_angle']),2*math.pi)
        continuous=math.hypot(dx,dy)<size*2 and abs(da)<.2618
        expected=[float(b['camera_x'])+(dx*ratio if continuous else 0),float(b['camera_y'])+(dy*ratio if continuous else 0)]
        observed=[float(r['camera_x']),float(r['camera_y'])]
        residual.append(math.hypot(observed[0]-expected[0],observed[1]-expected[1])*1440/(2*size))
        shake_a=[float(a[k])-float(a[v]) for k,v in [('camera_x','base_x'),('camera_y','base_y')]]
        shake_b=[float(b[k])-float(b[v]) for k,v in [('camera_x','base_x'),('camera_y','base_y')]]
        song_delta=float(b['song_s'])-float(a['song_s'])
        old_ratio=min(float(r['source_age_ms'])/1000,.05)/song_delta if .00001<song_delta<.1 else 0
        extra=math.hypot(shake_b[0]-shake_a[0],shake_b[1]-shake_a[1])*old_ratio*1440/(2*size)
        if math.hypot(*shake_b)>.00001 or math.hypot(*shake_a)>.00001:
            active+=1; old_extra.append(extra)
    result['camera_shake_check']=dict(samples=len(residual),active_shake_samples=active,
        observed_added_translation_beyond_base_velocity_px=stats(residual),
        discarded_old_shake_extrapolation_px=stats(old_extra),
        scope='Recorded camera constants, not a whole-image or subjective motion-quality proof')
    return result


if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('directory',type=Path); parser.add_argument('mode',type=int); parser.add_argument('--legacy',action='store_true')
    a=parser.parse_args(); print(json.dumps(diagnostic(a.directory,a.mode,a.legacy),ensure_ascii=False,indent=2))

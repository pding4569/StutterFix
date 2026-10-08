"""Chapter27: fresh OFF/connected/4x, identical scene sampler plus elevated PresentMon."""
import argparse
import bisect
import csv
import ctypes
import json
import math
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path
from measure_outside import read, stats

HERE = Path(__file__).resolve().parent
TRACE = Path(r'C:\Users\Public\StutterFixTrace\fg27')


def pid():
    output = subprocess.check_output(['tasklist','/FI','IMAGENAME eq A Dance of Fire and Ice.exe','/NH','/FO','CSV'], text=True, errors='replace')
    return next((int(r[1]) for r in csv.reader(output.splitlines()) if r and r[0].lower()=='a dance of fire and ice.exe'), None)


def pm_summary(root):
    scenes = read(root/'capture/cost-sources.csv')
    scenes = [r for r in scenes if 5 <= float(r['song_s']) < 45]
    lo, hi = float(scenes[0]['source_s']), float(scenes[-1]['source_s'])
    frequency = ctypes.c_longlong()
    ctypes.windll.kernel32.QueryPerformanceFrequency(ctypes.byref(frequency))
    with (root/'presentmon.csv').open(newline='', encoding='utf-8-sig') as f:
        all_rows = [{k.lower():v for k,v in r.items()} for r in csv.DictReader(f)]
    if not all_rows:
        raise RuntimeError('PresentMon produced no rows')
    key = next((k for k in ['cpustartqpc','qpctime'] if k in all_rows[0]), None)
    if not key:
        raise RuntimeError('PresentMon has no absolute QPC; cannot align song window: '+str(list(all_rows[0])))
    rows = [r for r in all_rows if lo <= float(r[key])/frequency.value <= hi]
    chains = {}
    for r in rows:
        chains[r['swapchainaddress']] = chains.get(r['swapchainaddress'],0)+1
    if not chains:
        raise RuntimeError('No aligned PresentMon rows')
    chain = max(chains, key=chains.get)
    rows = [r for r in rows if r['swapchainaddress']==chain]
    def numbers(key, selection):
        return [float(r[key]) for r in selection if r.get(key) not in (None,'NA','')]
    displayed = [r for r in rows if r.get('dropped','0')=='0' and r.get('msuntildisplayed') not in (None,'NA','')]
    times = [float(r[key])/frequency.value+float(r['msuntildisplayed'])/1000 for r in displayed]
    ordered = sorted(set(times))
    camera_times=[float(r['source_s']) for r in scenes]
    camera_frames={int(r['unity_frame']):float(r['source_s']) for r in scenes}
    camera_display=[]
    is_flow=(root/'capture/block-flow.txt').is_file()
    # OFF has no native frame ID: the preceding CPU callback is only an estimate,
    # since Unity can enqueue another frame before the render thread Presents.
    # Active native frame IDs identify the interpolated callback timeline, which
    # still does not measure input latency or each protected/fallback region.
    if not is_flow:
        for r in displayed:
            api=float(r[key])/frequency.value
            at=bisect.bisect_right(camera_times,api)-1
            if at<0: continue
            display=api+float(r['msuntildisplayed'])/1000
            camera_display.append((display-camera_times[at])*1000)
    changes = [(b-a)*1000 for a,b in zip(ordered,ordered[1:]) if b>a]
    modes = {}
    for r in rows:
        modes[r['presentmode']] = modes.get(r['presentmode'],0)+1
    result=dict(scope='Same 5..45s QPC window; ETW display events, not optical or input latency',
                rows=len(rows),chains=chains,selected_chain=chain,displayed_rows=len(displayed),
                display_changes=len(ordered),observed_display_fps=(len(ordered)-1)/(ordered[-1]-ordered[0]) if len(ordered)>1 else None,
                display_interval_ms=stats(changes),present_to_display_ms=stats(numbers('msuntildisplayed',displayed)),
                present_call_ms=stats(numbers('msinpresentapi',rows)),modes=modes,
                sync_intervals=sorted(set(r['syncinterval'] for r in rows)))
    native=root/'capture/presents.csv'
    if native.is_file():
        source_rows=read(root/'capture/sources.csv')
        source_indices={int(r['unity_frame']):i for i,r in enumerate(source_rows)}
        # Native samples immediately after Present returns. Match the ETW API end,
        # never simply match timestamps from different frames by song time.
        endings=sorted((float(r[key])/frequency.value+float(r['msinpresentapi'])/1000,i,r) for i,r in enumerate(rows))
        ends=[r[0] for r in endings]
        used=set();latency=[];errors=[];eligible=matched=0;worst=None
        for r in read(native):
            qpc=float(r['present_s']);delay=float(r.get('timeline_to_submit_ms','nan'))
            if not lo<=qpc<=hi or not math.isfinite(delay): continue
            eligible+=1;at=bisect.bisect_left(ends,qpc)
            options=[n for n in [at-1,at] if 0<=n<len(ends) and endings[n][1] not in used]
            if not options: continue
            n=min(options,key=lambda n:abs(ends[n]-qpc))
            if abs(ends[n]-qpc)>.0002: continue
            used.add(endings[n][1]);matched+=1;errors.append(abs(ends[n]-qpc)*1000)
            pm=endings[n][2]
            if pm.get('dropped','0')!='0' or pm.get('msuntildisplayed') in (None,'NA',''): continue
            display=float(pm[key])/frequency.value+float(pm['msuntildisplayed'])/1000
            value=(display-(qpc-delay/1000))*1000
            latency.append(value)
            frame=int(r['unity_frame']);si=source_indices.get(frame)
            if si is not None and si>0 and is_flow:
                before,after=source_rows[si-1],source_rows[si]
                old_frame,new_frame=int(before['unity_frame']),int(after['unity_frame'])
                old_qpc,new_qpc=float(before['source_s']),float(after['source_s'])
                target=qpc-delay/1000
                if new_qpc>old_qpc and old_qpc-.000001<=target<=new_qpc+.000001 and old_frame in camera_frames and new_frame in camera_frames:
                    phase=max(0,min(1,(target-old_qpc)/(new_qpc-old_qpc)))
                    camera_target=camera_frames[old_frame]+phase*(camera_frames[new_frame]-camera_frames[old_frame])
                    camera_display.append((display-camera_target)*1000)
            if worst is None or value>worst['ms']:
                worst=dict(ms=value,packet_song_s=float(r['song_s']),frame=int(r['unity_frame']),real=int(r['real']))
        result['timeline_to_display_ms']=stats(latency)
        result['matched_native_presents']=matched;result['eligible_native_presents']=eligible
        result['displayed_timeline_samples']=len(latency);result['matching_error_ms']=stats(errors)
        result['worst_timeline_display']=worst
    result['camera_ready_timeline_to_display_ms']=stats(camera_display)
    result['camera_ready_timeline_display_samples']=len(camera_display)
    result['camera_callback_frame_identity_confirmed']=is_flow
    result['camera_latency_scope']='OFF/connection: nearest preceding CPU camera callback, frame identity UNCONFIRMED; Unity may enqueue ahead. Active: native frame IDs map the interpolated callback target. ETW display; not input latency or the age of every UI/fallback block. Do not subtract OFF from active as confirmed added latency.'
    return result


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--map',type=Path,required=True)
    parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--stage',required=True)
    parser.add_argument('--order',default='0,1,4')
    parser.add_argument('--variant',type=int,default=0,choices=[0,1,2,3,4])
    parser.add_argument('--resume',action='store_true',help='Reuse completed game trial and saved PM capture; never retry failed game')
    parser.add_argument('--baseline-off',type=Path,help='Reuse the explicitly named earlier same-build OFF for a remaining2x trial')
    a=parser.parse_args()
    if pid(): raise RuntimeError('Close game normally first')
    status=json.loads((TRACE/'agent2-status.json').read_text(encoding='utf-8-sig'))
    if status['status'] not in ['ready','finished']: raise RuntimeError('PresentMon agent not ready')
    a.out.mkdir(parents=True,exist_ok=a.resume)
    result=dict(stage=a.stage,variant=a.variant,order=[int(n) for n in a.order.split(',')],runs=[],scope='One ordered batch; no closing OFF control')
    condition=build=None
    baseline_fps=None
    if a.baseline_off:
        baseline=json.loads(a.baseline_off.read_text(encoding='utf-8'))
        if baseline['runs'][0]['mode']!=0: raise RuntimeError('Expected first OFF reference')
        baseline_fps=baseline['runs'][0]['real_fps'];condition=tuple(baseline['conditions']);build=baseline['build']
        result['baseline_off']=str(a.baseline_off)
        result['scope']='Remaining trial, explicitly reuses earlier same-build/same-condition OFF; no new OFF'
    for index,mode in enumerate(result['order']):
        root=a.out/f'{index:02d}-{mode}x'
        name=f'{a.stage}-{index}-{mode}x'
        command=[sys.executable,str(HERE/'measure_outside.py'),'--map',str(a.map),'--out',str(root),'--label',name,'--mode',str(mode),'--seconds','55','--cost-split','--timeout-min','4']
        if a.variant: command+=['--fixed-cost-stage',str(a.variant)]
        if mode: command+=['--block-flow','--block-variant','3']
        reused=a.resume and (root/'summary.json').is_file()
        print('START '+name,flush=True)
        if not reused:
          with (a.out/f'{index:02d}-{mode}x.log').open('w',encoding='utf-8') as log:
            process=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT)
            started=False
            while process.poll() is None:
                game=pid() if not started else None
                if game:
                    payload=json.dumps(dict(action='pm',name=name,pid=game))
                    temporary=TRACE/'command.pending'
                    temporary.write_text(payload,encoding='utf-8')
                    temporary.replace(TRACE/'command2.json')
                    started=True
                time.sleep(.5)
          if not started: raise RuntimeError('Game process missed')
          (TRACE/('stop-'+name)).touch()
          deadline=time.monotonic()+20
          while time.monotonic()<deadline:
            status=json.loads((TRACE/'agent2-status.json').read_text(encoding='utf-8-sig'))
            if status.get('name')==name and status['status']=='finished': break
            time.sleep(.5)
          else: raise RuntimeError('PresentMon did not finish normally')
          if status['exit']!=0: raise RuntimeError('PresentMon exit '+str(status['exit']))
          if process.returncode:
            shutil.copyfile(TRACE/(name+'.csv'),root/'presentmon.csv')
            raise RuntimeError('Trial failed, preserve evidence: '+name)
        shutil.copyfile(TRACE/(name+'.csv'),root/'presentmon.csv')
        data=json.loads((root/'summary.json').read_text(encoding='utf-8'))
        log=(root/'game.log').read_text(encoding='utf-8')
        found=re.findall(r'\[곡 시작\] 화면: 수직동기 (\d+), 목표 FPS (\d+), (\w+), (\d+)x(\d+) (\d+)Hz, 창 (\d+)x(\d+)',log)
        if not found or found[-1][0]!='0' or found[-1][6:]!=('3440','1440'):
            raise RuntimeError('Not actual game3440x1440 sync0')
        current=found[-1]
        if condition is not None and current!=condition: raise RuntimeError('Conditions changed')
        if build is not None and data['build']!=build: raise RuntimeError('Binary changed within stage')
        condition=current; build=data['build']
        fps=data['scene_metrics']['real_fps']
        base=baseline_fps if baseline_fps is not None else result['runs'][0]['real_fps'] if result['runs'] else fps
        native=data['native']
        flow=native.get('block_flow') if native else None
        row=dict(mode=mode,real_fps=fps,mean_scene_ms=1000/fps,loss_percent=100*(1-fps/base),
                 output_fps=native['output_fps'] if native else None,
                 new_picture_ratio=float(flow['new_picture'])/float(flow['generated']) if flow and int(flow['generated']) else None,
                 new_picture_scope='Research GPU metric, 5..45s,256 sampled pixels; not physical display count',
                 timeline_to_submit_ms=native.get('interpolated_timeline_to_submit_ms') if native else None,
                 safety=data['safety'],presentmon=pm_summary(root))
        if native:
            row['generated_gpu_ms']=native['generated_gpu_ms']
            row['missing_camera_callbacks']=native['missing_camera_callbacks']
            row['output_worst_ms']=native['output_worst_ms']
        if mode:
            row['ownership']=dict(part.split('=',1) for part in (root/'capture/ownership.txt').read_text().split())
            schedule=[r for r in read(root/'capture/schedule.csv') if 5<=float(r['song_s'])<45]
            row['source_hold_ms']=stats([float(r['source_hold_ms']) for r in schedule])
        result['runs'].append(row); result['conditions']=current; result['build']=build
        (a.out/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print(json.dumps(row,ensure_ascii=False),flush=True)


if __name__=='__main__': main()

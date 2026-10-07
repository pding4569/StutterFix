"""Chapter 14 final: OFF window, then complete 2x and 4x songs; no draw hooks.
Images start after song time 50s. Performance is always the clean 5..45s window.
"""
import argparse
import csv
import json
import re
import shutil
from pathlib import Path
from measure_outside import HERE, analyze, sf


def rows(path):
    with path.open(newline='',encoding='utf-8') as f:
        r=csv.DictReader(f)
        return r.fieldnames,list(r)


def summarize(directory):
    headers,sources=rows(directory/'capture/sources.csv')
    ph,presents=rows(directory/'capture/presents.csv')
    boundaries=[0]+[i+1 for i,(a,b) in enumerate(zip(sources,sources[1:])) if float(b['song_s'])<float(a['song_s'])-1]+[len(sources)]
    sections=[sources[a:b] for a,b in zip(boundaries,boundaries[1:]) if any(5<=float(r['song_s'])<6 for r in sources[a:b]) and any(44<=float(r['song_s'])<45 for r in sources[a:b])]
    if len(sections)!=3: raise RuntimeError(f'Expected OFF/2x/4x music windows, got {len(sections)}')
    results=[]
    for mode,section in zip([0,2,4],sections):
        window=[r for r in section if 5<=float(r['song_s'])<45]
        first,last=int(window[0]['unity_frame']),int(window[-1]['unity_frame'])
        outputs=[r for r in presents if first<=int(r['unity_frame'])<=last]
        phase=directory/f'phase-{mode}'; phase.mkdir(exist_ok=True)
        for name,head,data in [('sources.csv',headers,window),('presents.csv',ph,outputs)]:
            with (phase/name).open('w',newline='',encoding='utf-8') as f:
                w=csv.DictWriter(f,fieldnames=head); w.writeheader(); w.writerows(data)
        result=analyze(phase,mode)
        if result['seconds']<39: raise RuntimeError('Incomplete clean performance window')
        results.append(dict(mode=mode,**result))
    return results


def saved_result(directory, label, metrics=None):
    """Keep completion and camera-render verification separate; never discard raw evidence."""
    log=(directory/'game.log').read_text(encoding='utf-8')
    summary=(directory/'run.txt').read_text(encoding='utf-8')
    if summary.count('곡 끝남')!=2 or '곡이 끝나지 않음' in summary:
        raise RuntimeError('Both whole-song completions not confirmed')
    safety=dict(part.split('=',1) for part in (directory/'capture/safety.txt').read_text().split())
    if int(safety['worker_error']) or safety['frame_begins']!=safety['frame_ends']:
        raise RuntimeError('Native shutdown or frame balance failure')
    match=re.search(r'finish; missing real scenes=(\d+)',log)
    if not match: raise RuntimeError('Missing camera-render diagnostic')
    missing=int(match.group(1))
    performance=summarize(directory)
    for row in performance:
        # The diagnostic is a whole-session total: conservatively assign every missing
        # callback to each measured window. Its actual time/phase was not recorded.
        row['scene_fps_lower_bound']=max(0,row['real_fps']-missing/row['source_seconds'])
        row['scene_fps_upper_bound']=row['real_fps']
    result=dict(label=label,performance=performance,whole_songs_completed=2,safety=safety,
                missing_camera_callbacks=missing,all_camera_renders_confirmed=missing==0,
                missing_callback_location='not recorded',game_metrics=metrics)
    (directory/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    return result


def main():
    p=argparse.ArgumentParser(); p.add_argument('--map',type=Path,required=True); p.add_argument('--label',required=True); p.add_argument('--out',type=Path,required=True)
    a=p.parse_args()
    if not a.map.is_file() or sf.game_running(): raise RuntimeError('Existing map and closed game required')
    mod=Path(sf.MOD_DIR); diagnostic=mod/'framegen-outside'
    if diagnostic.exists(): raise RuntimeError('Preserve previous diagnostic output first')
    a.out.mkdir(parents=True,exist_ok=False)
    original={n:(mod/n).read_bytes() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
    for n,b in original.items(): (a.out/(n+'.original')).write_bytes(b)
    try:
        diagnostic.mkdir(); (mod/'StutterFix.dll').write_bytes((HERE/'out/outside/StutterFix.dll').read_bytes())
        steps=['game '+str(a.map),'auto on','press','wait 55']
        for mode in [2,4]:
            steps+=['retry','set FrameGenOutside '+str(mode),'set FrameGenCapture false','auto on','press','wait 50','set FrameGenCapture true','waitend 600']
        summary,metrics=sf.sf_run(steps+['quit'],settings={'FrameStats':False,'LowHalfRender':False,'FrameGenOutside':0,'FrameGenFlipY':True,'FrameGenCapture':False},tag='framegen-included-'+a.label,timeout_min=20)
        log=sf.read_text(sf.PLAYER_LOG); (a.out/'game.log').write_text(log,encoding='utf-8'); (a.out/'run.txt').write_text(summary,encoding='utf-8')
        if diagnostic.exists(): shutil.copytree(diagnostic,a.out/'capture')
        if any(v in log for v in ['Crash!!!','단계 실패','[안정성] 안전 모드로 켬','native failure','feature disabled']) or '시간 초과로 끔' in summary: raise RuntimeError('Experiment failed; inspect evidence')
        result=saved_result(a.out,a.label,metrics)
        if not result['all_camera_renders_confirmed']:
            print('Camera-render verification INCONCLUSIVE: report bounds, not exact scene FPS',flush=True)
        print(json.dumps(result,ensure_ascii=False),flush=True)
    finally:
        if sf.game_running(): sf.sf_quit()
        if sf.game_running(): raise RuntimeError('Close normally, then restore disk backups')
        for n,b in original.items(): (mod/n).write_bytes(b)
        if diagnostic.exists() and (a.out/'capture').is_dir():
            if diagnostic.resolve().parent!=mod.resolve() or diagnostic.name!='framegen-outside': raise RuntimeError('Unexpected cleanup path')
            shutil.rmtree(diagnostic)
        print('Installed DLL, native DLL and exact settings restored',flush=True)


if __name__=='__main__': main()

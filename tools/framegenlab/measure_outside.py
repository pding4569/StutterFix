"""Chapter 14: one original swapchain, gated native output, exact install restoration."""
import argparse
import csv
import json
import math
import shutil
import statistics
from pathlib import Path
from measure_maps import sf

HERE = Path(__file__).resolve().parent


def read(path):
    with path.open(newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    resets = [i+1 for i, (a,b) in enumerate(zip(rows,rows[1:])) if float(b["song_s"]) < float(a["song_s"])-1]
    return rows[resets[-1] if resets else 0:]


def stats(values):
    return dict(mean=statistics.mean(values),p95=sorted(values)[int(.95*(len(values)-1))],maximum=max(values)) if values else None


def analyze(directory, mode, end=45):
    all_sources=read(directory/"sources.csv")
    first=next(i for i,r in enumerate(all_sources) if float(r['song_s'])>=5)
    last=next((i for i,r in enumerate(all_sources[first:],first) if float(r['song_s'])>=end),len(all_sources))
    sources=all_sources[first:last]
    rows=[r for r in read(directory/'presents.csv') if int(sources[0]['unity_frame'])<=int(r['unity_frame'])<=int(sources[-1]['unity_frame'])]
    if len(rows)<100 or len(sources)<100 or any(int(r["hr"])!=0 for r in rows):
        raise RuntimeError("Missing successful output/source window")
    if any(int(b["unity_frame"])-int(a["unity_frame"])!=1 for a,b in zip(sources,sources[1:])):
        raise RuntimeError("Missing actual source scenes")
    generated=[r for r in rows if r["real"]=="0"]
    gpu=[float(r["gpu_ms"]) for r in generated if math.isfinite(float(r["gpu_ms"]))]
    if mode and (not generated or len(gpu)!=len(generated)):
        raise RuntimeError("Incomplete generated GPU coverage")
    interval=[(float(b["present_s"])-float(a["present_s"]))*1000 for a,b in zip(rows,rows[1:])]
    source_interval=[(float(b["source_s"])-float(a["source_s"]))*1000 for a,b in zip(sources,sources[1:])]
    seconds=float(rows[-1]["present_s"])-float(rows[0]["present_s"])
    source_seconds=float(sources[-1]["source_s"])-float(sources[0]["source_s"])
    errors=[r for r in sources if float(r["horizon_ms"])>0]
    result=dict(seconds=seconds,source_seconds=source_seconds,source_frames=len(sources),output_frames=len(rows),
                real_output_frames=len(rows)-len(generated),generated_frames=len(generated),
                real_fps=(len(sources)-1)/source_seconds,output_fps=(len(rows)-1)/seconds,
                generated_gpu_ms=stats(gpu),gpu_coverage=len(gpu),
                output_worst_ms=max(interval),output_over_33ms=sum(v>1000/30 for v in interval),
                source_worst_ms=max(source_interval),source_over_33ms=sum(v>1000/30 for v in source_interval),
                prediction_camera_px=stats([float(r["camera_px"]) for r in errors]),
                prediction_red_px=stats([float(r["red_px"]) for r in errors]),
                prediction_blue_px=stats([float(r["blue_px"]) for r in errors]),
                prediction_horizon_ms=stats([float(r["horizon_ms"]) for r in errors]),
                prediction_samples=len(errors),source_age_ms=stats([float(r["source_age_ms"]) for r in generated]))
    if 'scene_rendered' in sources[0]:
        missing=sum(r['scene_rendered']=='0' for r in sources)
        result['missing_camera_callbacks']=missing
        result['scene_fps']=(len(sources)-missing-1)/source_seconds
    path=directory/'schedule.csv'
    if path.exists():
        scheduled=[r for r in read(path) if int(sources[0]['unity_frame'])<=int(r['unity_frame'])<=int(sources[-1]['unity_frame'])]
        result['scheduler']=dict(samples=len(scheduled),
            expected_generated=(mode-1)*len(sources) if mode else 0,
            reset_miss_lock=sum(int(r['miss_lock']) for r in scheduled),
            reset_miss_next_before_due=sum(int(r['miss_next_before_due']) for r in scheduled),
            reset_miss_worker_late=sum(int(r['miss_worker_late']) for r in scheduled),
            continuous_miss_lock=int(scheduled[-1]['continuous_miss_lock'])-int(scheduled[0]['continuous_miss_lock']) if scheduled else 0,
            continuous_miss_timer=int(scheduled[-1]['continuous_miss_timer'])-int(scheduled[0]['continuous_miss_timer']) if scheduled else 0,
            worker_wait_ms=stats([float(r['worker_wait_ms']) for r in scheduled]),
            source_hold_ms=stats([float(r['source_hold_ms']) for r in scheduled]))
    return result


def main():
    p=argparse.ArgumentParser()
    p.add_argument("--map",type=Path,required=True)
    p.add_argument("--label",required=True)
    p.add_argument("--mode",type=int,choices=[0,*range(2,9)],required=True)
    p.add_argument("--seconds",type=int,default=55)
    p.add_argument("--out",type=Path,required=True)
    p.add_argument("--capture",action="store_true")
    p.add_argument("--clip",action="store_true",help="Sample actual GPU outputs at song 20..23s; visual-only, excluded from performance comparison")
    p.add_argument("--legacy",action="store_true",help="Chapter 14 full-frame gate and resetting clock; instrumentation control")
    p.add_argument("--full-song",action="store_true",help="Run to the actual song end; captures start after the performance window")
    p.add_argument("--switch-smoke",action="store_true")
    p.add_argument("--freeze-smoke",action="store_true",help="Insert a 100ms main-thread stop; not a performance run")
    p.add_argument("--sync-smoke",action="store_true",help="Verify OFF preserves Unity sync=1 while active uses sync=0; not a performance run")
    p.add_argument("--ui-smoke",action="store_true",help="Capture the research Graphics setting in a separate visual run")
    p.add_argument("--expect-inactive",action="store_true",help="Verify an incompatible setting suspends only frame generation")
    p.add_argument("--timeout-min",type=float,default=3)
    p.add_argument("--settings",type=Path,help="Additional compatibility settings JSON; experimental controls stay fixed")
    a=p.parse_args()
    if (HERE/"out/stop-outside-batch").exists():
        raise RuntimeError("Measurement batch stopped for the requested visual comparison; no installation changed")
    if not a.map.is_file() or not 10<=a.seconds<=600 or sf.game_running():
        raise RuntimeError("Existing map, 10..600 seconds and closed game required")
    if a.switch_smoke and a.mode!=0: p.error("switch test starts OFF")
    if a.sync_smoke and a.mode!=0: p.error("sync test starts OFF")
    if sum([a.switch_smoke,a.freeze_smoke,a.sync_smoke,a.ui_smoke])>1: p.error("Choose one smoke test")
    extra=json.loads(a.settings.read_text(encoding="utf-8")) if a.settings else {}
    if not isinstance(extra,dict) or any(k.startswith('FrameGen') for k in extra):
        p.error("Compatibility settings must be an object without FrameGen controls")
    mod=Path(sf.MOD_DIR); diagnostic=mod/"framegen-outside"
    if diagnostic.exists(): raise RuntimeError("Preserve prior framegen-outside output first")
    a.out.mkdir(parents=True,exist_ok=False)
    original={n:(mod/n).read_bytes() for n in ["StutterFix.dll","sfnative.dll","Settings.xml"]}
    for n,b in original.items(): (a.out/(n+".original")).write_bytes(b)
    shot=mod/'shots'/f'framegen-research-setting-{a.mode}x.png'
    old_shot=shot.read_bytes() if a.ui_smoke and shot.exists() else None
    try:
        diagnostic.mkdir()
        (mod/"StutterFix.dll").write_bytes((HERE/"out/outside/StutterFix.dll").read_bytes())
        steps=["game "+str(a.map),"auto on","press"]
        steps+=(["wait 10","set FrameGenOutside 2","wait 3","set FrameGenOutside 3","wait 3","set FrameGenOutside 4","wait 3","set FrameGenOutside 5","wait 3","set FrameGenOutside 8","wait 3","set FrameGenOutside 1","wait 3","set FrameGenOutside 9","wait 3","set FrameGenOutside 0","wait 3"] if a.switch_smoke else ["wait 10","kick freeze 100","wait 15"] if a.freeze_smoke else ["wait "+str(a.seconds)])
        if a.sync_smoke: steps=["game "+str(a.map),"auto on","press","wait 10","fgsync 1","wait 5","set FrameGenOutside 2","wait 5","set FrameGenOutside 0","wait 5","fgsync 0","wait 5"]
        if a.ui_smoke: steps=["game "+str(a.map),"auto on","press","wait 10","ui 3","wait 3",f"shot framegen-research-setting-{a.mode}x","wait 3","ui close","wait 10"]
        if a.full_song: steps[-1]="waitend 600"
        settings={"FrameStats":False,"LowHalfRender":False,**extra,"FrameGenOutside":a.mode,"FrameGenNarrow":not a.legacy,"FrameGenFlipY":True,"FrameGenCapture":a.capture,"FrameGenClip":a.clip}
        summary,metrics=sf.sf_run(steps+["quit"],settings=settings,tag="framegen-outside-"+a.label,timeout_min=a.timeout_min)
        log=sf.read_text(sf.PLAYER_LOG); (a.out/"game.log").write_text(log,encoding="utf-8"); (a.out/"run.txt").write_text(summary,encoding="utf-8")
        if a.ui_smoke: shutil.copyfile(shot,a.out/'settings.png')
        if diagnostic.exists(): shutil.copytree(diagnostic,a.out/"capture")
        if any(v in log for v in ["Crash!!!","단계 실패","[안정성] 안전 모드로 켬","native failure","feature disabled"]) or "시간 초과로 끔" in summary:
            raise RuntimeError("Failed experiment; inspect evidence")
        safety=dict(part.split("=",1) for part in (a.out/"capture/safety.txt").read_text().split())
        if int(safety["worker_error"]) or safety["frame_begins"]!=safety["frame_ends"]:
            raise RuntimeError("Native worker or frame start/end balance failed; stop the experiment")
        if a.sync_smoke and (int(safety.get('sync_preserved_off',0))==0 or int(safety.get('sync_forced_zero_active',0))==0):
            raise RuntimeError("OFF/active sync restoration was not observed")
        native=analyze(a.out/"capture",0 if a.expect_inactive else a.mode,end=45 if a.seconds>=50 else a.seconds-5)
        if a.expect_inactive and native['generated_frames']!=0: raise RuntimeError("Expected suspension did not occur")
        if a.full_song and ("곡 끝남" not in summary or "곡이 끝나지 않음" in summary):
            raise RuntimeError("Actual whole-song completion not confirmed")
        if a.seconds>=50 and native["seconds"]<39:
            raise RuntimeError("Incomplete 5..45 second performance window")
        result=dict(label=a.label,mode=a.mode,expected_inactive=a.expect_inactive,compatibility_settings=extra,visual_smoke=a.clip or a.switch_smoke or a.freeze_smoke or a.sync_smoke or a.ui_smoke or a.expect_inactive,native=native,safety=safety,game_metrics=metrics)
        (a.out/"summary.json").write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
        print(json.dumps(result,ensure_ascii=False),flush=True)
    finally:
        if sf.game_running(): sf.sf_quit()
        if sf.game_running(): raise RuntimeError("Close normally, then restore disk backups")
        for n,b in original.items(): (mod/n).write_bytes(b)
        if a.ui_smoke:
            if old_shot is not None: shot.write_bytes(old_shot)
            else: shot.unlink(missing_ok=True)
        if diagnostic.exists() and (a.out/"capture").is_dir():
            if diagnostic.resolve().parent!=mod.resolve() or diagnostic.name!="framegen-outside": raise RuntimeError("Unexpected cleanup path")
            shutil.rmtree(diagnostic)
        print("Installed managed/native DLL and exact settings restored",flush=True)


if __name__=="__main__": main()

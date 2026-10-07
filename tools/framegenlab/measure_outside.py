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
    rows = [r for r in read(directory/"presents.csv") if 5 <= float(r["song_s"]) < end]
    sources = [r for r in read(directory/"sources.csv") if 5 <= float(r["song_s"]) < end]
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
    return dict(seconds=seconds,source_seconds=source_seconds,source_frames=len(sources),output_frames=len(rows),
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


def main():
    p=argparse.ArgumentParser()
    p.add_argument("--map",type=Path,required=True)
    p.add_argument("--label",required=True)
    p.add_argument("--mode",type=int,choices=[0,2,4],required=True)
    p.add_argument("--seconds",type=int,default=55)
    p.add_argument("--out",type=Path,required=True)
    p.add_argument("--capture",action="store_true")
    p.add_argument("--full-song",action="store_true",help="Run to the actual song end; captures start after the performance window")
    p.add_argument("--switch-smoke",action="store_true")
    p.add_argument("--freeze-smoke",action="store_true",help="Insert a 100ms main-thread stop; not a performance run")
    p.add_argument("--timeout-min",type=float,default=3)
    a=p.parse_args()
    if (HERE/"out/stop-outside-batch").exists():
        raise RuntimeError("Measurement batch stopped for the requested visual comparison; no installation changed")
    if not a.map.is_file() or not 10<=a.seconds<=600 or sf.game_running():
        raise RuntimeError("Existing map, 10..600 seconds and closed game required")
    if a.switch_smoke and a.mode!=0: p.error("switch test starts OFF")
    mod=Path(sf.MOD_DIR); diagnostic=mod/"framegen-outside"
    if diagnostic.exists(): raise RuntimeError("Preserve prior framegen-outside output first")
    a.out.mkdir(parents=True,exist_ok=False)
    original={n:(mod/n).read_bytes() for n in ["StutterFix.dll","sfnative.dll","Settings.xml"]}
    for n,b in original.items(): (a.out/(n+".original")).write_bytes(b)
    try:
        diagnostic.mkdir()
        (mod/"StutterFix.dll").write_bytes((HERE/"out/outside/StutterFix.dll").read_bytes())
        steps=["game "+str(a.map),"auto on","press"]
        steps+=(["wait 10","set FrameGenOutside 2","wait 5","set FrameGenOutside 4","wait 5","set FrameGenOutside 0","wait 5"] if a.switch_smoke else ["wait 10","kick freeze 100","wait 15"] if a.freeze_smoke else ["wait "+str(a.seconds)])
        if a.full_song: steps[-1]="waitend 600"
        summary,metrics=sf.sf_run(steps+["quit"],settings={"FrameStats":False,"LowHalfRender":False,"FrameGenOutside":a.mode,"FrameGenFlipY":True,"FrameGenCapture":a.capture},tag="framegen-outside-"+a.label,timeout_min=a.timeout_min)
        log=sf.read_text(sf.PLAYER_LOG); (a.out/"game.log").write_text(log,encoding="utf-8"); (a.out/"run.txt").write_text(summary,encoding="utf-8")
        if diagnostic.exists(): shutil.copytree(diagnostic,a.out/"capture")
        if any(v in log for v in ["Crash!!!","단계 실패","[안정성] 안전 모드로 켬","native failure","feature disabled"]) or "시간 초과로 끔" in summary:
            raise RuntimeError("Failed experiment; inspect evidence")
        safety=dict(part.split("=",1) for part in (a.out/"capture/safety.txt").read_text().split())
        if int(safety["worker_error"]) or safety["frame_begins"]!=safety["frame_ends"]:
            raise RuntimeError("Native worker or frame start/end balance failed; stop the experiment")
        native=analyze(a.out/"capture",a.mode,end=45 if a.seconds>=50 else a.seconds-5)
        if a.full_song and ("곡 끝남" not in summary or "곡이 끝나지 않음" in summary):
            raise RuntimeError("Actual whole-song completion not confirmed")
        if a.seconds>=50 and native["seconds"]<39:
            raise RuntimeError("Incomplete 5..45 second performance window")
        result=dict(label=a.label,mode=a.mode,visual_smoke=a.capture or a.switch_smoke or a.freeze_smoke,native=native,safety=safety,game_metrics=metrics)
        (a.out/"summary.json").write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
        print(json.dumps(result,ensure_ascii=False),flush=True)
    finally:
        if sf.game_running(): sf.sf_quit()
        if sf.game_running(): raise RuntimeError("Close normally, then restore disk backups")
        for n,b in original.items(): (mod/n).write_bytes(b)
        if diagnostic.exists() and (a.out/"capture").is_dir():
            if diagnostic.resolve().parent!=mod.resolve() or diagnostic.name!="framegen-outside": raise RuntimeError("Unexpected cleanup path")
            shutil.rmtree(diagnostic)
        print("Installed managed/native DLL and exact settings restored",flush=True)


if __name__=="__main__": main()

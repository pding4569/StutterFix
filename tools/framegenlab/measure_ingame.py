"""Research-only in-game FG run. Fresh process, native Present counter, disk backups and restore."""
import argparse
import csv
import json
import math
import shutil
import statistics
from pathlib import Path
from measure_maps import sf

HERE = Path(__file__).resolve().parent


def analyze(path, mode):
    with path.open(newline="", encoding="utf-8") as f:
        recorded = list(csv.DictReader(f))
    # The conductor retains a menu/loading clock until the music starts, then resets.
    # Keep the final song segment before selecting a common music-time window.
    resets=[i+1 for i,(a,b) in enumerate(zip(recorded,recorded[1:])) if float(b["song_s"])<float(a["song_s"])-1]
    rows=[r for r in recorded[resets[-1] if resets else 0:] if 5<=float(r["song_s"])<45]
    if len(rows) < 100 or any(int(r["hr"]) != 0 for r in rows):
        raise RuntimeError("No complete successful Present window")
    seconds = float(rows[-1]["present_s"]) - float(rows[0]["present_s"])
    intervals = [(float(b["present_s"])-float(a["present_s"]))*1000 for a,b in zip(rows,rows[1:])]
    real = [r for r in rows if r["real"] == "1"]
    generated = [r for r in rows if r["real"] == "0"]
    gpu = [float(r["gpu_ms"]) for r in generated if math.isfinite(float(r["gpu_ms"]))]
    if mode and (len(gpu) != len(generated) or not generated):
        raise RuntimeError("Missing generated GPU timestamps; reject performance table")
    if any(int(b["unity_frame"])-int(a["unity_frame"]) != 1 for a,b in zip(rows,rows[1:])):
        raise RuntimeError("Present/Unity frame gap or duplicate; reject rates")
    if mode and any(int(b["unity_frame"])-int(a["unity_frame"]) != mode for a,b in zip(real,real[1:])):
        raise RuntimeError("Source frame cadence changed")
    source_intervals = [(float(b["present_s"])-float(a["present_s"]))*1000 for a,b in zip(real,real[1:])]
    return dict(seconds=seconds,song_start=float(rows[0]["song_s"]),song_end=float(rows[-1]["song_s"]), presents=len(rows), real_frames=len(real), generated_frames=len(generated),
                output_fps=(len(rows)-1)/seconds, real_fps=(len(real)-1)/(float(real[-1]["present_s"])-float(real[0]["present_s"])),
                generated_gpu_mean_ms=statistics.mean(gpu) if gpu else None,
                generated_gpu_p95_ms=sorted(gpu)[int(.95*(len(gpu)-1))] if gpu else None,
                generated_gpu_max_ms=max(gpu) if gpu else None, gpu_coverage=len(gpu),
                output_worst_ms=max(intervals), output_over_33ms=sum(t>1000/30 for t in intervals),
                source_worst_ms=max(source_intervals), source_over_33ms=sum(t>1000/30 for t in source_intervals))


def main():
    p=argparse.ArgumentParser()
    p.add_argument("--map",type=Path,required=True)
    p.add_argument("--label",required=True)
    p.add_argument("--mode",type=int,choices=[0,2,4],required=True)
    p.add_argument("--seconds",type=int,default=55,help="55s run collects the common song 5..45s window")
    p.add_argument("--out",type=Path,required=True)
    p.add_argument("--flip",action=argparse.BooleanOptionalAction,default=True)
    p.add_argument("--capture",action="store_true",help="Visual smoke only: GPU readback disrupts timing")
    p.add_argument("--switch-smoke",action="store_true",help="Functional 0/2/4/0 transition check, excluded from performance tables")
    args=p.parse_args()
    if args.switch_smoke and args.mode!=0:
        p.error("--switch-smoke starts with --mode 0")
    if not args.map.is_file() or not 10<=args.seconds<=600 or sf.game_running():
        raise RuntimeError("Existing map, 10..600 seconds and closed game required")
    mod=Path(sf.MOD_DIR)
    destination=mod/"framegen-ingame"
    if destination.exists():
        raise RuntimeError("Preserve previous framegen-ingame directory first")
    args.out.mkdir(parents=True,exist_ok=False)
    originals={name:(mod/name).read_bytes() for name in ["StutterFix.dll","sfnative.dll","Settings.xml"]}
    for name,data in originals.items():
        (args.out/(name+".original")).write_bytes(data)
    try:
        (mod/"StutterFix.dll").write_bytes((HERE/"out"/"ingame"/"StutterFix.dll").read_bytes())
        steps=["game "+str(args.map),"auto on","press"]
        steps+=(["wait 10","set FrameGenExperiment 2","wait 4","set FrameGenExperiment 4","wait 4","set FrameGenExperiment 0","wait 4"] if args.switch_smoke else ["wait "+str(args.seconds)])
        summary,metrics=sf.sf_run(steps+["quit"],
            settings={"FrameStats":False,"LowHalfRender":False,"FrameGenExperiment":args.mode,
                      "FrameGenFlipY":args.flip,"FrameGenCapture":args.capture},tag="framegen-ingame-"+args.label,timeout_min=8)
        log=sf.read_text(sf.PLAYER_LOG)
        (args.out/"game.log").write_text(log,encoding="utf-8")
        (args.out/"run.txt").write_text(summary,encoding="utf-8")
        if destination.exists():
            shutil.copytree(destination,args.out/"capture")
        if any(s in log for s in ["단계 실패","[안정성] 안전 모드로 켬","native failure","feature disabled","swapchain unavailable","layer 31 occupied"]) or "시간 초과로 끔" in summary:
            raise RuntimeError("Experiment failed; inspect log, reject this run")
        result=dict(label=args.label,mode=args.mode,wait_seconds=args.seconds,visual_smoke=args.capture or args.switch_smoke,switch_smoke=args.switch_smoke,flip=args.flip,
                    native=analyze(args.out/"capture"/"presents.csv",args.mode),game_metrics=metrics)
        if args.switch_smoke and (result["native"]["generated_frames"]!=result["native"]["gpu_coverage"] or "state restore differences=0" not in log):
            raise RuntimeError("Transition GPU/restore check failed")
        (args.out/"summary.json").write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
        print(json.dumps(result,ensure_ascii=False),flush=True)
    finally:
        if sf.game_running(): sf.sf_quit()
        if sf.game_running(): raise RuntimeError("Game still running; close normally then restore disk backups")
        for name,data in originals.items(): (mod/name).write_bytes(data)
        if destination.exists() and (args.out/"capture").is_dir():
            if destination.resolve().parent!=mod.resolve() or destination.resolve().name!="framegen-ingame":
                raise RuntimeError("Unexpected diagnostic cleanup path")
            shutil.rmtree(destination)
        print("Installed DLL, native DLL and exact settings restored",flush=True)


if __name__=="__main__": main()

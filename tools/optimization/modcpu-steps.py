# 모드 쪽 managed CPU 시간 줄이기용 시험 계획 생성기: 증명된 순서(곡 처음에서 준비 -> seek 260 -> live on)로 260~270초를 10초씩 두 번 잰다.
# 사용: python modcpu-steps.py --chart <Arche FULL.adofai> --output <steps.json>
import argparse, json
ap = argparse.ArgumentParser(); ap.add_argument("--chart", required=True); ap.add_argument("--output", required=True); a = ap.parse_args()
def window(tag):
    return ["seek 260 12", "decobake ready", "waitsongpos 256 40", "gpudeco packets compiled", "gpudeco live on", "waitsongpos 260 40",
            "gpudeco live census", "fuse begin", "onupdate begin", "phasewindow begin", "gpudeco micro begin", "waitsongpos 270 25",
            "gpudeco micro end", "phasewindow end", "onupdate end", "fuse end", "gpudeco live off", "stop"]
steps = ["gpudeco costprobe off", "gpudeco effectcache off", "timeout 600", "auto on", "decobake on", "gpudeco off", "gpudeco reset", "gpudeco nombp off",
         "gpudeco g2 reset", "gpudeco nativebatch on", "gpudeco nativecopy on8masked", "gpudeco zeroalpha on",
         "gpudeco bundle E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\evidence\bundle\stutterfix_gpudeco",
         "open " + a.chart, "select 0", "play", "gpudeco scheduled", "pause", "decobake state", "decobake ready", "gpudeco live scope song",
         "gpudeco live prepare", "resume", "stop", "gpuprobe off", "gpusplit off", "matshare off", "livedeco off", "phasewindow on"]
steps += window("a") + window("b") + ["quit"]
json.dump({"steps": steps, "settings": {"FrameGenOutside": 0, "FrameGenRefresh": False}, "timeout_min": 25, "tag": "modcpu-260-270"}, open(a.output, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

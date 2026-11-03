"""Run one bounded experiment via the shared game lock; always restore GpuDeco."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(r"E:\StutterFixWork")
HERE = Path(__file__).resolve().parent
OUT = ROOT / "night-results"
SETTINGS = Path(r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\Settings.xml")

def run(chart, name, enabled=1, window=None):
    source = json.loads((ROOT / f"nv-{chart}-on.json").read_text(encoding="utf-8"))
    game = next(s for s in source["steps"] if s.startswith("game "))
    steps = ["timeout 600", "wait 2", "auto on", game]
    if window:
        start, end = window
        steps += ["press", f"waitsongpos {start} 400", "fpsmark start",
                  f"waitsongpos {end} 60", f"fpsmark end {name}"]
        if enabled:
            steps += ["pause", "wait 0.2", "framecmp filters on", "framecmp ui exclude",
                      "framecmp mask none", "framecmp grab GPU", "gpudeco live off",
                      "wait 0.2", "framecmp grab CPU", "framecmp diff GPU CPU 3 png", "resume"]
    else:
        steps += ["wait 2"]
    steps += ["quit"]
    spec = {"steps": steps, "settings": {"GpuDeco": enabled, "GpuDecoImages": 1,
            "FrameGenOutside": 0, "FrameGenRefresh": False}, "timeout_min": 14, "tag": name}
    path = HERE / f"{name}.json"
    path.write_text(json.dumps(spec, ensure_ascii=False, indent=2), encoding="utf-8")
    try:
        result = subprocess.run([sys.executable, str(ROOT / "run_locked.py"), str(path),
                                 "wt-partial", name], cwd=ROOT, capture_output=True)
        (OUT / f"{name}-runner.txt").write_bytes(result.stdout + result.stderr)
    finally:
        text = SETTINGS.read_text(encoding="utf-8")
        SETTINGS.write_text(re.sub(r"<GpuDeco>\d+</GpuDeco>", "<GpuDeco>0</GpuDeco>", text), encoding="utf-8")
    log = (OUT / f"{name}.log").read_text(encoding="utf-8", errors="replace")
    if result.returncode or "단계 실패" in log or "오류로 그만둠" in log:
        raise SystemExit(f"Invalid run: {name}; inspect before continuing")
    selected = [line for line in log.splitlines() if any(key in line for key in
                ("PREFLIGHT_DENSITY", "[fpsmark]", "[framecmp]", "[GPU 장식]", "시험 끝", "단계 실패"))]
    (HERE / f"{name}.txt").write_text("\n".join(selected), encoding="utf-8")
    print(name, "completed", flush=True)

if __name__ == "__main__":
    dll = ROOT / "wt-partial/bin/PlayerAuto/StutterFix.dll"
    (HERE / "candidate-dll.sha256").write_text(hashlib.sha256(dll.read_bytes()).hexdigest()+"\n")
    if sys.argv[1] == "admission":
        chart = sys.argv[2]
        run(chart, f"round03-{chart.lower()}-admission")
    elif sys.argv[1] == "abba":
        chart, start, end = sys.argv[2], float(sys.argv[3]), float(sys.argv[4])
        for i, enabled in enumerate((0, 1, 1, 0)):
            run(chart, f"round03-{chart.lower()}-{i}-g{enabled}", enabled, (start, end))


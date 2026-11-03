"""Bounded ABBA for a preparation-only population-dominance hypothesis."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(r"E:\StutterFixWork")
OUT = ROOT / "night-results"
EVIDENCE = ROOT / "wt-partial/docs/deco-spec/results/evidence/round01"
EVIDENCE.mkdir(parents=True, exist_ok=True)
spec = json.loads((ROOT / "nv-Exit-on.json").read_text(encoding="utf-8"))
game = next(s for s in spec["steps"] if s.startswith("game "))
dll = ROOT / "wt-partial/bin/PlayerAuto/StutterFix.dll"
(EVIDENCE / "candidate-dll.sha256").write_text(hashlib.sha256(dll.read_bytes()).hexdigest()+"\n")
for i, enabled in enumerate((0, 1, 1, 0)):
    tag = f"round01-exit-{i}-g{enabled}"
    steps = ["timeout 400", "wait 2", "auto on", game, "press",
             "waitsongpos 90 240", "fpsmark start", "waitsongpos 100 60",
             f"fpsmark end {tag}"]
    if enabled:
        steps += ["pause", "wait 0.2", "framecmp filters on", "framecmp ui exclude",
                  "framecmp mask none", "framecmp grab GPU", "gpudeco live off",
                  "wait 0.2", "framecmp grab CPU", "framecmp diff GPU CPU 3 png", "resume"]
    steps += ["quit"]
    run = {"steps": steps, "settings": {"GpuDeco": enabled, "FrameGenOutside": 0,
           "FrameGenRefresh": False}, "timeout_min": 10, "tag": tag}
    path = EVIDENCE / (tag+".json")
    path.write_text(json.dumps(run, ensure_ascii=False, indent=2), encoding="utf-8")
    result = subprocess.run([sys.executable, str(ROOT / "run_locked.py"), str(path),
                             "wt-partial", tag], cwd=ROOT, capture_output=True)
    (OUT / (tag+"-runner.txt")).write_bytes(result.stdout+result.stderr)
    print(tag, "exit", result.returncode, flush=True)
    # The launcher restores DLL/native probe; restore the user setting as well.
    settings = Path(r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\Settings.xml")
    import re
    text = settings.read_text(encoding="utf-8")
    settings.write_text(re.sub(r"<GpuDeco>\d+</GpuDeco>", "<GpuDeco>0</GpuDeco>", text), encoding="utf-8")
    log = (OUT / (tag+".log")).read_text(encoding="utf-8", errors="replace")
    if result.returncode or "단계 실패" in log or "오류로 그만둠" in log:
        raise SystemExit("Invalid run; inspect evidence before continuing")

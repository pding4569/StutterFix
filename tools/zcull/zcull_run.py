"""Claude execution entrypoint. --prepare-only and --summarize never launch/install anything."""
import argparse
import json
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(r"E:\StutterFixWork")
OUT = ROOT / "night-results"
TIMES = {"hl": 50, "dd": 55, "s7": 120, "ar": 262, "wf": 260, "sh": 88}
SUMMARY = re.compile(r"\[zcull\] renderers=(\d+) occluders=(\d+) fragments_total=([\d.]+) fragments_alive=([\d.]+) culled=([\d.]+)%")


def prepare(key, wt):
    # Reuse the current bench definitions; do not retype map paths or silently use another preset.
    sys.path.insert(0, str(ROOT))
    import bench_v14 as bench
    overrides, commands = bench.CFG2["rlNR"]
    settings = dict(bench.CLEAN)
    settings.update(overrides)
    if settings.get("GpuDeco") != 0 or settings.get("RunLod"):
        raise ValueError("rlNR drifted: original-renderer configuration required")
    at = TIMES[key]
    map_path = Path(bench.M) / bench.MAPS[key][0]
    if not map_path.is_file():
        raise FileNotFoundError(map_path)
    steps = ["timeout 900", "wait 2", "auto on", "fpscap 0", "open " + str(map_path)]
    steps += list(commands)
    steps += [f"seek {at} 8", f"waitsongpos {at} 400", "pause", "wait 2", "zcull census", "resume", "quit"]
    path = OUT / f"zcull-{key}-steps.json"
    path.write_text(json.dumps(dict(steps=steps, settings=settings, timeout_min=15, tag=f"zcull-{key}"), ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def result(key):
    path = OUT / f"zcull-{key}.txt"
    text = path.read_text(encoding="utf-8", errors="replace") if path.exists() else ""
    matches = SUMMARY.findall(text)
    log_path = OUT / f"zcull-{key}.log"
    raw = log_path.read_text(encoding="utf-8", errors="replace") if log_path.exists() else text
    failed = any(s in raw for s in ("단계 실패", "오류로 그만둠", "harmony patch failed", "[zcull] failed:", "[zcull] cancelled:"))
    if failed or not matches or "[zcull] complete;" not in text:
        return None
    return matches[-1]


def main():
    parser = argparse.ArgumentParser(description="Z-cull paused census; default launches six locked game runs (Claude only).")
    parser.add_argument("--prepare-only", action="store_true", help="write steps only; no game, DLL replacement, or lock")
    parser.add_argument("--summarize", action="store_true", help="read existing results only")
    parser.add_argument("--maps", default=",".join(TIMES))
    parser.add_argument("--worktree", default="wt-v14prod")
    args = parser.parse_args()
    keys = args.maps.split(",")
    if any(k not in TIMES for k in keys):
        parser.error("unknown map")
    OUT.mkdir(exist_ok=True)
    failed = False
    failed_maps = set()
    if not args.summarize:
        if not args.prepare_only and not (ROOT / args.worktree / "bin/PlayerAuto/StutterFix.dll").is_file():
            parser.error("build PlayerAuto first")
        for key in keys:
            steps = prepare(key, args.worktree)
            if args.prepare_only:
                print(f"PREPARED {key} {TIMES[key]}s {steps}")
                continue
            # run_locked -> GameLock -> run_json supplies the established install/restore lifecycle.
            # This file is prepared by Codex; only Claude executes this branch.
            start = time.time_ns()
            completed = subprocess.run([sys.executable, str(ROOT / "run_locked.py"), str(steps), args.worktree,
                                        f"zcull-{key}", "--keys", "[zcull],단계 실패,오류로 그만둠", "--tail", "40"],
                                       cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
            (OUT / f"zcull-{key}-launcher.txt").write_text(completed.stdout + completed.stderr, encoding="utf-8")
            path = OUT / f"zcull-{key}.txt"
            fresh = path.exists() and path.stat().st_mtime_ns >= start
            if completed.returncode or not fresh or result(key) is None:
                print(f"FAILED {key}: see zcull-{key}-launcher.txt / .log", flush=True)
                failed = True
                failed_maps.add(key)
            else:
                print(f"COMPLETE {key}", flush=True)
    if args.prepare_only:
        return 0
    rows = ["map\tseconds\trenderers\toccluders\tfragments_total\tfragments_alive\tculled_percent"]
    for key in keys:
        values = None if key in failed_maps else result(key)
        if values is None:
            failed = True
            rows.append(f"{key}\t{TIMES[key]}\tMISSING_OR_FAILED")
        else:
            rows.append("\t".join((key, str(TIMES[key]), *values)))
    table = "\n".join(rows) + "\n"
    print(table, end="")
    (OUT / "zcull-summary.tsv").write_text(table, encoding="utf-8")
    return 1 if failed else 0


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())

"""Serial 3440x1440 Present(0) experiment; no game installation/packaging."""
import argparse
import csv
import json
import math
import statistics
import subprocess
from pathlib import Path

HERE = Path(__file__).resolve().parent


def distribution(values):
    values = sorted(v for v in values if math.isfinite(v))
    if not values:
        return None
    return dict(count=len(values), mean_ms=statistics.mean(values),
                median_ms=statistics.median(values),
                p95_ms=values[math.ceil(len(values) * .95) - 1])


def summarize(path, seconds, base, multiplier):
    with path.open(newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    generated = [r for r in rows if r["real"] == "0"]
    per_second = []
    for second in range(int(seconds)):
        group = [r for r in rows if second <= float(r["time_s"]) < second + 1]
        per_second.append(dict(second=second, output=len(group),
                               real=sum(int(r["real"]) for r in group)))
    return dict(base=base, multiplier=multiplier, seconds=seconds,
                output_fps=len(rows) / seconds,
                real_fps=sum(int(r["real"]) for r in rows) / seconds,
                generated_count=len(generated),
                generated_gpu=distribution(float(r["gpu_output_ms"]) for r in generated),
                real_gpu=distribution(float(r["gpu_output_ms"]) for r in rows if r["real"] == "1"),
                gpu_valid_count=sum(math.isfinite(float(r["gpu_output_ms"])) for r in rows),
                output_count=len(rows), per_second=per_second,
                physical_display_fps=None,
                fps_basis="successful Present calls / requested wall-clock seconds; physical scanout unverified")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--seconds", type=float, default=15)
    p.add_argument("--bases", type=int, nargs="+", default=[400, 200, 100])
    p.add_argument("--gpu-timing", type=int, choices=[0, 1], default=1)
    p.add_argument("--output", type=Path, default=HERE / "results" / "unsynced")
    args = p.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    results = []
    for base in args.bases:
        baseline = None
        for multiplier in [1, 2, 3, 4]:
            stem = args.output / f"{base}-{multiplier}x"
            cmd = [str(HERE / "out" / "FrameGenLab.exe"), "--base", str(base),
                   "--mode", "none" if multiplier == 1 else "hybrid",
                   "--multiplier", str(max(2, multiplier)), "--seconds", str(args.seconds),
                   "--sync", "0", "--gpu-timing", str(args.gpu_timing),
                   "--width", "3440", "--height", "1440", "--borderless",
                   "--csv", str(stem.with_suffix(".csv"))]
            print(f"Running source {base}, {multiplier}x", flush=True)
            proc = subprocess.run(cmd, capture_output=True, text=True)
            stem.with_suffix(".log").write_text(proc.stdout + proc.stderr, encoding="utf-8")
            if proc.returncode or "size=3440x1440" not in proc.stdout or "sync=0" not in proc.stdout:
                raise RuntimeError(proc.stdout + proc.stderr)
            result = summarize(stem.with_suffix(".csv"), args.seconds, base, multiplier)
            if multiplier == 1:
                baseline = result["real_fps"]
            result["real_loss_pct_vs_none"] = 100 * (baseline - result["real_fps"]) / baseline
            results.append(result)
            (args.output / "summary.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
            print(f"output={result['output_fps']:.1f} real={result['real_fps']:.1f} GPU={result['generated_gpu']}", flush=True)


if __name__ == "__main__":
    main()

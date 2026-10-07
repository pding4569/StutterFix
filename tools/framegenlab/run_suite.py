"""Run real D3D11 outputs and retain PresentMon display metrics (not just Present calls)."""
import argparse
import csv
import json
import math
import shutil
import statistics
import subprocess
import time
from collections import Counter
from pathlib import Path


def percentile(values, p):
    values = sorted(values)
    return values[min(len(values)-1, int((len(values)-1)*p))] if values else None


def number(row, key):
    try:
        value = row.get(key)
        if value is None:
            value = next((v for k,v in row.items() if k.lower()==key.lower()), "")
        v = float(value)
        return v if math.isfinite(v) else None
    except (ValueError, TypeError):
        return None


def summarize(pm_path, app_path, process_id=None):
    with pm_path.open(encoding="utf-8-sig", newline="") as f:
        rows = [r for r in csv.DictReader(f) if (r.get("ProcessID") == str(process_id) if process_id else r.get("Application", "").lower() == "framegenlab.exe")]
    def timestamp(row):
        v = number(row, "TimeInSeconds")
        return v if v is not None else (number(row, "TimeInMs") or 0)/1000
    times = [timestamp(r) for r in rows]
    times = [t for t in times if t is not None]
    if not times:
        raise RuntimeError("PresentMon has no timestamped FrameGenLab rows")
    # Discard one second at both ends; do not trim CPU and display data independently by percentile.
    rows = [r for r in rows if min(times)+1 <= timestamp(r) <= max(times)-1]
    # PM 2.5's v1 schema can omit Dropped: a numeric display latency identifies displayed frames.
    displayed = [r for r in rows if r.get("Dropped", "0") == "0" and number(r,"MsUntilDisplayed") is not None and (number(r, "MsBetweenDisplayChange") or 0) > 0]
    intervals = [number(r, "MsBetweenDisplayChange") for r in displayed]
    if len(intervals) < 100:
        raise RuntimeError(f"Only {len(intervals)} displayed intervals; display capture unverified")
    modes = Counter(r.get("PresentMode", "unknown") for r in displayed)
    result = {
        "presents": len(rows), "displayed": len(displayed),
        "dropped": sum(r.get("Dropped") == "1" or number(r,"MsUntilDisplayed") is None for r in rows),
        "display_fps": 1000/statistics.mean(intervals),
        "display_ms_mean": statistics.mean(intervals),
        "display_ms_p50": percentile(intervals, .5),
        "display_ms_p95": percentile(intervals, .95),
        "display_ms_p99": percentile(intervals, .99),
        "display_ms_max": max(intervals), "present_modes": dict(modes),
        "display_interval_histogram_ms": dict(Counter(round(v, 1) for v in intervals).most_common(8)),
    }
    for key in ("MsGPUBusy", "MsGPUActive", "MsUntilDisplayed", "MsBetweenPresents"):
        values = [number(r, key) for r in displayed]
        values = [v for v in values if v is not None and v >= 0]
        if values:
            result[key+"_mean"] = statistics.mean(values)
    with app_path.open(newline="") as f:
        app = list(csv.DictReader(f))
    stable = [r for r in app if 1 <= float(r["time_s"]) <= float(app[-1]["time_s"])-1]
    result["cpu_submit_ms_mean"] = statistics.mean(float(r["cpu_submit_ms"]) for r in stable)
    result["cpu_submit_ms_p95"] = percentile([float(r["cpu_submit_ms"]) for r in stable], .95)
    result["real_source_updates"] = sum(int(r["real"]) for r in stable)
    result["generated_outputs"] = sum(not int(r["real"]) for r in stable)
    return result


def summarize_dxgi(app_path):
    with Path(str(app_path)+".display.csv").open(newline="") as f:
        records = list(csv.DictReader(f))
    if len(records)<100:
        raise RuntimeError("DXGI displayed statistics unavailable")
    end = float(records[-1]["observed_s"])
    records = [r for r in records if 1<=float(r["observed_s"])<=end-1]
    pairs = list(zip(records,records[1:]))
    periods = [(float(b["sync_qpc_s"])-float(a["sync_qpc_s"]))/(int(b["sync_refresh_count"])-int(a["sync_refresh_count"]))
               for a,b in pairs if int(b["sync_refresh_count"])>int(a["sync_refresh_count"])]
    period = statistics.median(periods)
    def displayed_time(r):
        return float(r["sync_qpc_s"])+(int(r["present_refresh_count"])-int(r["sync_refresh_count"]))*period
    consecutive = [(a,b) for a,b in pairs if int(b["present_count"])-int(a["present_count"])==1]
    intervals = [(displayed_time(b)-displayed_time(a))*1000 for a,b in consecutive]
    if len(intervals)<100 or min(intervals)<=0:
        raise RuntimeError("Insufficient consecutive positive display intervals")
    with app_path.open(newline="") as f:
        app = list(csv.DictReader(f))
    stable = [r for r in app if 1<=float(r["time_s"])<=float(app[-1]["time_s"])-1]
    app_ids = {int(r['present_id']): r for r in stable if 'present_id' in r}
    latencies = [(displayed_time(r)-float(app_ids[int(r['present_count'])]['qpc_start_s']))*1000
                 for r in records if int(r['present_count']) in app_ids]
    if latencies and min(latencies)<-1:
        raise RuntimeError("DXGI PresentCount/QPC latency mapping is inconsistent")
    result = {"measurement": "DXGI GetFrameStatistics (PresentMon unverified)",
        "fps_basis": "inverse mean of consecutively observed PresentCount intervals; not a complete display count",
        "display_fps": 1000/statistics.mean(intervals), "display_ms_mean": statistics.mean(intervals),
        "display_ms_p50": percentile(intervals,.5), "display_ms_p95": percentile(intervals,.95),
        "display_ms_p99": percentile(intervals,.99), "display_ms_max": max(intervals),
        "consecutive_display_intervals": len(intervals), "observed_statistics": len(records),
        "unobserved_present_numbers": sum(max(0,int(b['present_count'])-int(a['present_count'])-1) for a,b in pairs),
        "measured_refresh_hz": 1/period,
        "refresh_interval_counts": dict(Counter(int(b['present_refresh_count'])-int(a['present_refresh_count']) for a,b in consecutive)),
        "cpu_submit_ms_mean": statistics.mean(float(r['cpu_submit_ms']) for r in stable),
        "real_source_updates": sum(int(r['real']) for r in stable),
        "generated_outputs": sum(not int(r['real']) for r in stable),
        "input_latency": "not measured", "gpu_time": "not measured"}
    if latencies:
        result.update(output_start_to_display_ms_mean=statistics.mean(latencies),output_start_to_display_ms_p95=percentile(latencies,.95),latency_samples=len(latencies))
    return result


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--presentmon", type=Path, default=Path(r"C:\SFBundle\PresentMon.exe"))
    ap.add_argument("--seconds", type=int, default=20)
    ap.add_argument("--base", type=float, default=0, help="0 = measured monitor Hz / multiplier")
    ap.add_argument("--hz", type=float, default=0)
    ap.add_argument("--multipliers", type=int, nargs="+", default=[2, 3, 4])
    ap.add_argument("--modes", nargs="+", choices=["none", "reproj", "hybrid"], default=["hybrid"])
    ap.add_argument("--borderless", action="store_true")
    ap.add_argument("--native-only", action="store_true", help="Use DXGI displayed statistics; does not claim a PresentMon pass")
    ap.add_argument("--out", type=Path, default=Path(__file__).parent/"results"/time.strftime("%Y%m%d-%H%M%S"))
    ap.add_argument("--pm-out", type=Path, help="ASCII capture directory for PresentMon builds that cannot write Unicode paths")
    args = ap.parse_args()
    if args.seconds < 5:
        ap.error("--seconds must be at least 5")
    exe = Path(__file__).parent/"out"/"FrameGenLab.exe"
    if not exe.is_file() or (not args.native_only and not args.presentmon.is_file()):
        raise RuntimeError("Build FrameGenLab and provide PresentMon first")
    args.out.mkdir(parents=True, exist_ok=False)
    pm_out = args.pm_out or args.out
    pm_out.mkdir(parents=True, exist_ok=True)
    results = []
    for mode in args.modes:
        for multiplier in args.multipliers:
            label = f"{mode}-{multiplier}x"
            print(f"Running {label}", flush=True)
            pm_csv = pm_out/(label+"-presentmon.csv")
            app_csv = args.out/(label+"-app.csv")
            cmd = [str(exe), "--multiplier", str(multiplier), "--base", str(args.base), "--hz", str(args.hz),
                "--seconds", str(args.seconds), "--mode", mode, "--csv", str(app_csv),
                "--capture", str(args.out/(label+".ppm"))]
            if args.borderless:
                cmd += ["--borderless"]
            app_process = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, errors="replace")
            with (args.out/(label+"-pm.log")).open("w", encoding="utf-8") as log:
                # PresentMon's console input handler can stop immediately on inherited EOF.
                # Give it its own hidden console and default standard handles (not redirected pipes).
                startup = subprocess.STARTUPINFO()
                startup.dwFlags = subprocess.STARTF_USESHOWWINDOW
                startup.wShowWindow = subprocess.SW_HIDE
                pm = None if args.native_only else subprocess.Popen([str(args.presentmon), "--process_id", str(app_process.pid),
                    "--session_name", f"SFFrameGenLab-{time.time_ns()}", "--output_file", str(pm_csv),
                    "--v1_metrics", "--timed", str(args.seconds+4), "--terminate_after_timed", "--no_console_stats"],
                    startupinfo=startup, creationflags=subprocess.CREATE_NEW_CONSOLE)
                if pm:
                    log.write(f"PresentMon pid={pm.pid}; hidden console, no standard-handle redirection\n"); log.flush()
                try:
                    stdout, stderr = app_process.communicate(timeout=args.seconds+30)
                    app = subprocess.CompletedProcess(cmd, app_process.returncode, stdout, stderr)
                    (args.out/(label+"-app.log")).write_text(app.stdout+app.stderr, encoding="utf-8")
                    print(app.stdout.strip(), flush=True)
                    if app.returncode:
                        raise RuntimeError(app.stderr or f"App exit={app.returncode}")
                    if pm:
                        pm.wait(timeout=30)
                    if pm and pm.returncode:
                        raise RuntimeError(f"PresentMon exit={pm.returncode}; see {label}-pm.log")
                finally:
                    if app_process.poll() is None:
                        app_process.terminate(); app_process.wait(timeout=10)
                    if pm and pm.poll() is None:
                        pm.terminate(); pm.wait(timeout=10)
            result = {"label": label, "base_option": args.base, "hz_option": args.hz, "borderless": args.borderless,
                      "app_metadata": app.stdout.splitlines(), **(summarize_dxgi(app_csv) if args.native_only else summarize(pm_csv, app_csv, app_process.pid))}
            if not args.native_only and pm_out.resolve() != args.out.resolve():
                shutil.copy2(pm_csv, args.out/pm_csv.name)
            results.append(result)
            (args.out/"summary.json").write_text(json.dumps(results, indent=2, ensure_ascii=False), encoding="utf-8")
            basis = "Consecutive displayed intervals" if args.native_only else "Displayed"
            print(f"{basis}: equivalent {result['display_fps']:.1f} FPS, p50/p95/p99 {result['display_ms_p50']:.2f}/{result['display_ms_p95']:.2f}/{result['display_ms_p99']:.2f}ms, measurement={result.get('measurement', 'PresentMon')}", flush=True)
    print(args.out/"summary.json", flush=True)


if __name__ == "__main__":
    main()

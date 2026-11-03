"""Collect completed round03 logs; run only after all game processes exit."""
import hashlib
import json
from pathlib import Path
import re
import statistics
import subprocess

ROOT = Path(r"E:\StutterFixWork")
HERE = Path(__file__).resolve().parent
OUT = ROOT / "night-results"
MOD = Path(r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix")
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
processes = subprocess.check_output(["tasklist", "/FO", "CSV"], text=True)
assert "A Dance of Fire and Ice.exe" not in processes
records = []
admissions = []
for tag in ["round03-windflower-admission", "round03-arche-admission"] + [
        f"round03-windflower-{i}-g{enabled}" for i, enabled in enumerate((0, 1, 1, 0))]:
    path = OUT / f"{tag}.log"
    text = path.read_text(encoding="utf-8", errors="replace")
    assert "단계 실패" not in text and "오류로 그만둠" not in text
    assert "[자동 시험] 끝 - 게임을 끕니다" in text
    selected = [line for line in text.splitlines() if "임무단계" not in line and any(
        key in line for key in ("PREFLIGHT_DENSITY", "[FPS 구간]", "diff GPU CPU threshold=",
        "framecmp RESULT", "framecmp VERDICT", "[GPU 장식] 켜짐:",
        "[GPU 장식] 건너뜀:", "[자동 시험] 끝"))]
    (HERE / f"{tag}.txt").write_text("\n".join(selected)+"\n", encoding="utf-8")
    record = {"tag": tag, "raw_log": str(path), "raw_sha256": sha(path)}
    preflight = re.search(r"PREFLIGHT_DENSITY eligible_upper=(\d+) initially_visible=(\d+) active_fallback=(\d+) horizon_s=([\d.]+) mean_visible_channels=([\d.]+) target_refs=(\d+) census_ms=([\d.]+) admit=(\w+)", text)
    if preflight:
        record["preflight"] = dict(zip(("eligible_upper", "initially_visible", "active_fallback", "horizon_s", "mean_visible_channels",
            "target_refs", "census_ms", "admit"), preflight.groups()))
    if "admission" in tag:
        admissions.append(record)
        continue
    enabled = int(tag[-1])
    fps = float(re.search(re.escape(tag)+r" : ([\d.]+) FPS", text)[1])
    record.update(setting=enabled, fps=fps, ms_from_rounded_fps=1000/fps,
                  gpu_activated="[GPU 장식] 켜짐: 소유" in text)
    assert not record["gpu_activated"]
    if enabled:
        pixel = re.search(r"diff GPU CPU threshold=3 maxRGBA=([\d,]+) changed=(\d+)/(\d+)", text)
        assert pixel and int(pixel[2]) == 0
        record["pixel"] = {"max_rgba": [int(x) for x in pixel[1].split(",")],
                           "over_3": int(pixel[2]), "pixels": int(pixel[3]),
                           "noise_and_positive_controls": "not run; verdict reports FAIL for missing controls"}
    records.append(record)
integrity = {"installed_sha256": sha(MOD / "StutterFix.dll"),
             "restore_sha256": sha(ROOT / "restore/StutterFix-rc-player.dll"),
             "gpuprobe_present": (MOD / "gpuprobe.dll").exists(),
             "GpuDeco": int(re.search(r"<GpuDeco>(\d+)</GpuDeco>", (MOD / "Settings.xml").read_text(encoding="utf-8"))[1]),
             "game_process_present": False}
assert integrity["installed_sha256"] == integrity["restore_sha256"]
assert not integrity["gpuprobe_present"] and integrity["GpuDeco"] == 0
off = statistics.mean(r["ms_from_rounded_fps"] for r in records if not r["setting"])
on = statistics.mean(r["ms_from_rounded_fps"] for r in records if r["setting"])
off_fps = statistics.mean(r["fps"] for r in records if not r["setting"])
on_fps = statistics.mean(r["fps"] for r in records if r["setting"])
summary = {"base_commit": "13aba64", "candidate": "initial-visible changing-channel density exceeds slot and active fallback proxy",
    "decision": "REJECTED: blocks known-beneficial Arche as well as Windflower; candidate source reverted",
    "window_song_s": [12, 22], "performance_runs": 4, "admission_only_runs": 2,
    "runs": records, "admissions": admissions, "off_mean_ms": off, "candidate_mean_ms": on,
    "delta_ms": on-off, "delta_ms_percent": (on/off-1)*100,
    "off_mean_fps": off_fps, "candidate_mean_fps": on_fps,
    "delta_fps_percent": (on_fps/off_fps-1)*100,
    "off_pair_spread_ms": abs(records[0]["ms_from_rounded_fps"]-records[3]["ms_from_rounded_fps"]),
    "on_pair_spread_ms": abs(records[1]["ms_from_rounded_fps"]-records[2]["ms_from_rounded_fps"]),
    "metric_scope": "1000 / rounded FPS; not CPU/GPU execution time or whole-song result",
    "pixel_scope": "two frozen CPU self-comparison pairs after admission rejection at window end, filters included and UI excluded; missing noise and positive controls; not whole-song parity",
    "preserved_images": {name: sha(HERE / name) for name in ("windflower-b2-gpu.png", "windflower-b2-cpu.png")},
    "six_map_classifier_complete": False, "default_GpuDeco": 0, "integrity": integrity}
(HERE / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
print(json.dumps({key: summary[key] for key in ("off_mean_ms", "candidate_mean_ms", "delta_ms",
    "delta_ms_percent", "delta_fps_percent", "off_pair_spread_ms", "on_pair_spread_ms", "integrity")}, indent=2))


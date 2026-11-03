"""Collect completed logs and validate restoration; never run during a game."""
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
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
assert "A Dance of Fire and Ice.exe" not in subprocess.check_output(["tasklist", "/FO", "CSV"], text=True)
records = []
tags = ["round04-windflower-admission", "round04-arche-admission"] + [
    f"round04-windflower-{i}-g{g}" for i, g in enumerate((0, 1, 1, 0))]
for tag in tags:
    path = OUT / f"{tag}.log"
    log = path.read_text(encoding="utf-8", errors="replace")
    assert "단계 실패" not in log and "오류로 그만둠" not in log
    assert "[자동 시험] 끝 - 게임을 끕니다" in log
    selected = [l for l in log.splitlines() if "임무단계" not in l and any(k in l for k in (
        "PREFLIGHT_FUTURE", "[FPS 구간]", "diff GPU CPU threshold=", "framecmp VERDICT",
        "framecmp RESULT", "[GPU 장식] 켜짐:", "[GPU 장식] 건너뜀:", "[자동 시험] 끝"))]
    (HERE / f"{tag}.txt").write_text("\n".join(selected)+"\n", encoding="utf-8")
    row = {"tag": tag, "raw_log": str(path), "raw_sha256": sha(path),
           "gpu_activated": "[GPU 장식] 켜짐: 소유" in log}
    assert not row["gpu_activated"]
    pre = re.search(r"PREFLIGHT_FUTURE (.+)", log)
    if pre:
        row["preflight"] = dict(re.findall(r"(\w+)=([^ ]+)", pre[1]))
        assert row["preflight"]["admit"] == "False"
    if "admission" not in tag:
        fps = float(re.search(re.escape(tag)+r" : ([\d.]+) FPS", log)[1])
        row.update(setting=int(tag[-1]), fps=fps, ms_from_rounded_fps=1000/fps)
        if row["setting"]:
            m = re.search(r"diff GPU CPU threshold=3 maxRGBA=([\d,]+) changed=(\d+)/(\d+)", log)
            assert m and int(m[2]) == 0
            row["pixel"] = {"max_rgba": m[1], "over_3": int(m[2]), "total": int(m[3]),
                            "scope": "CPU self comparison; no noise or positive controls"}
    records.append(row)
runs = records[2:]
off = statistics.mean(r["ms_from_rounded_fps"] for r in runs if r["setting"] == 0)
on = statistics.mean(r["ms_from_rounded_fps"] for r in runs if r["setting"] == 1)
integrity = {"installed_sha256": sha(MOD / "StutterFix.dll"), "restore_sha256": sha(ROOT / "restore/StutterFix-rc-player.dll"),
             "gpuprobe_present": (MOD / "gpuprobe.dll").exists(), "game_process_present": False,
             "GpuDeco": int(re.search(r"<GpuDeco>(\d+)</GpuDeco>", (MOD / "Settings.xml").read_text(encoding="utf-8"))[1])}
assert integrity["installed_sha256"] == integrity["restore_sha256"]
assert not integrity["gpuprobe_present"] and integrity["GpuDeco"] == 0
summary = {"base_commit": "bd0645d", "decision": "REJECTED: future-visibility density blocks known-beneficial Arche; source reverted",
           "candidate": "128 midpoint forward-replay visible changing channels exceed eligible slots plus active fallback",
           "admission_only_runs": 2, "performance_runs": 4, "window_song_s": [12, 22], "records": records,
           "off_mean_ms": off, "candidate_mean_ms": on, "delta_ms": on-off, "delta_ms_percent": (on/off-1)*100,
           "off_pair_spread_ms": abs(runs[0]["ms_from_rounded_fps"]-runs[3]["ms_from_rounded_fps"]),
           "on_pair_spread_ms": abs(runs[1]["ms_from_rounded_fps"]-runs[2]["ms_from_rounded_fps"]),
           "metric_scope": "inverse rounded FPS; not CPU/GPU time or whole song",
           "six_map_classifier_complete": False, "default_GpuDeco": 0, "integrity": integrity,
           "images": {p.name: sha(p) for p in HERE.glob("*.png")}}
(HERE / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
print(json.dumps({k: summary[k] for k in ("off_mean_ms", "candidate_mean_ms", "delta_ms", "delta_ms_percent", "off_pair_spread_ms", "on_pair_spread_ms", "integrity")}, indent=2))

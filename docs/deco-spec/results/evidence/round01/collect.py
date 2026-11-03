"""Collect completed runs only; never invoke while the game is running."""
import hashlib
import json
from pathlib import Path
import re
import statistics

ROOT = Path(r"E:\StutterFixWork")
HERE = Path(__file__).resolve().parent
MOD = Path(r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix")
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
records = []
for i, enabled in enumerate((0, 1, 1, 0)):
    tag = f"round01-exit-{i}-g{enabled}"
    path = ROOT / "night-results" / (tag+".log")
    text = path.read_text(encoding="utf-8", errors="replace")
    assert "단계 실패" not in text and "오류로 그만둠" not in text
    fps = float(re.search(re.escape(tag)+r" : ([\d.]+) FPS", text)[1])
    lines = [s for s in text.splitlines() if "임무단계" not in s and any(k in s for k in (
        "PREFLIGHT", "[FPS 구간]", "diff GPU CPU threshold=", "framecmp RESULT",
        "[GPU 장식] 건너뜀:", "[자동 시험] 끝"))]
    (HERE / (tag+".txt")).write_text("\n".join(lines)+"\n", encoding="utf-8")
    if enabled:
        assert "admit=False" in text and "before_collection=True" in text
        assert "threshold=3 maxRGBA=0,0,0,0 changed=0/3686400" in text
        assert "[GPU 장식] 켜짐: 소유" not in text
    records.append({"tag": tag, "setting": enabled, "fps": fps, "ms_from_rounded_fps": 1000/fps,
                    "raw_log": str(path), "raw_sha256": sha(path), "gpu_active": False})
arche = ROOT / "night-results/round01-arche-admission.log"
text = arche.read_text(encoding="utf-8", errors="replace")
assert "candidate_upper=28714 remaining_renderers=33134 admit=False" in text
(HERE / "round01-arche-admission.txt").write_text("\n".join(s for s in text.splitlines()
    if "PREFLIGHT" in s or "[GPU 장식] 건너뜀:" in s)+"\n", encoding="utf-8")
integrity = {"installed_sha256": sha(MOD / "StutterFix.dll"),
             "restore_sha256": sha(ROOT / "restore/StutterFix-rc-player.dll"),
             "gpuprobe_present": (MOD / "gpuprobe.dll").exists(),
             "GpuDeco": int(re.search(r"<GpuDeco>(\d+)</GpuDeco>", (MOD / "Settings.xml").read_text(encoding="utf-8"))[1])}
assert integrity["installed_sha256"] == integrity["restore_sha256"]
assert not integrity["gpuprobe_present"] and integrity["GpuDeco"] == 0
summary = {"base_commit": "df390b6", "candidate": "initial image population dominates other scene renderers",
    "decision": "REJECTED; beneficial workload also rejected; source reverted",
    "window_song_s": [90, 100], "run_count": 4, "admission_only_count": 1,
    "runs": records, "arche_log_sha256": sha(arche), "integrity": integrity,
    "off_mean_ms": statistics.mean(r["ms_from_rounded_fps"] for r in records if not r["setting"]),
    "candidate_mean_ms": statistics.mean(r["ms_from_rounded_fps"] for r in records if r["setting"]),
    "pixel_scope": "two frozen CPU-path self comparisons after early rejection; not GPU parity proof",
    "default_GpuDeco": 0, "six_map_classifier_complete": False}
(HERE / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
for name in ("round01-build.txt", "round01-restored-build.txt"):
    (HERE / name).write_bytes((ROOT / "night-results" / name).read_bytes())
print(json.dumps(summary, ensure_ascii=False, indent=2))

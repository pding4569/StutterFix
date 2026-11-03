"""Extract bounded G1 observations after games exit; never launch a game."""
import hashlib
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT.parent
if (WORK / "game.lock").exists():
    raise SystemExit("게임 잠금이 있는 동안 G1 로그 분석/복사를 실행하지 않습니다.")
OUT = ROOT / "docs/deco-spec/results/evidence/g1"
ART = WORK / "g1-artifacts"
MOD = Path(r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix")
RUNS = {
    "g1-original-dd": ("f3bcc9f", "g1-original-dd.json", False),
    "g1-dd-missingfix": ("8f85a66", "partial2-dd.json", True),
    "g1-exit-restore-pixel12": ("3421147", "g1-exit-restore-pixel12.json", False),
    "g1-wf-restore-pixel12": ("c550a52", "g1-wf-restore-pixel12.json", False),
    "g1-ar-restore-pixel262": ("8391551", "g1-ar-restore-pixel262.json", False),
    "g1-ar-262-controls": ("8391551", "g1-ar-262-controls.json", False),
}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

OUT.mkdir(parents=True, exist_ok=True)
(ART / "logs").mkdir(parents=True, exist_ok=True)
(ART / "pixels").mkdir(parents=True, exist_ok=True)
summary = {"acceptance": "BLOCKED_PIXEL", "runs": {}}
for name, (commit, spec, full_song) in RUNS.items():
    source = WORK / "night-results" / (name + ".log")
    raw = source.read_text(encoding="utf-8-sig", errors="replace")
    shutil.copy2(source, ART / "logs" / source.name)
    shutil.copy2(WORK / spec, OUT / (name + ".steps.json"))
    patterns = ("표 거부 사유:", "image_admission ", "live_prepare_candidates ", "준비 검사 장식 제외:",
                "직접 텍스처 한도 초과 정리:", "켜짐: 소유 ", "[곡] 평균 ", "[곡 시작] 화면:", "[framecmp] diff ",
                "[framecmp] noise outside=", "[framecmp] framecmp RESULT", "[framecmp] framecmp VERDICT",
                "original_guard ", "original_targets ", "take_refusal ", "originals_restored=", "cost scope=")
    selected = [line for line in raw.splitlines() if any(p in line for p in patterns)]
    (OUT / (name + ".excerpt.txt")).write_text("\n".join(selected) + "\n", encoding="utf-8")
    fps = re.findall(r"(\d+)s (\d+)\(", raw)
    summary["runs"][name] = {"source_commit": commit, "full_song": full_song,
        "raw": str(ART / "logs" / source.name), "raw_sha256": sha(source),
        "worst_logged_10s_fps": min((int(v) for _, v in fps), default=None),
        "recovery_count": len(re.findall(r"originals_restored=\w+ reason=recovery:", raw)),
        "step_failures": [s for s in raw.splitlines() if "단계 실패" in s or "오류로 그만둠" in s]}
for name in ("AR262_C", "AR262_I", "AR262_A", "AR262_A2"):
    source = MOD / "framecmp" / (name + ".png")
    dest = ART / "pixels" / source.name
    shutil.copy2(source, dest)
    summary.setdefault("control_images", {})[name] = {"path": str(dest), "sha256": sha(dest)}
summary["final_restoration"] = {
    "installed_dll_sha256": sha(MOD / "StutterFix.dll"),
    "restore_dll_sha256": sha(WORK / "restore/StutterFix-rc-player.dll"),
    "dll_equal": sha(MOD / "StutterFix.dll") == sha(WORK / "restore/StutterFix-rc-player.dll"),
    "gpuprobe_absent": not (MOD / "gpuprobe.dll").exists(),
    "gpu_deco_zero": bool(re.search(r"<GpuDeco>\s*0\s*</GpuDeco>", (MOD / "Settings.xml").read_text(encoding="utf-8-sig"))),
}
summary["final_builds"] = {p: sha(ROOT / p) for p in ("bin/Player/StutterFix.dll", "bin/PlayerAuto/StutterFix.dll")}
(OUT / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"runs": summary["runs"], "restoration": summary["final_restoration"]}, ensure_ascii=False, indent=2))

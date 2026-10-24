"""Offline evidence only; never starts or controls a game."""
import hashlib
import json
import re
from pathlib import Path
from harness_gate import assess

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "tools/optimization/out"


def collect(name):
    folder = OUT / name
    path = folder / "manifest.json"
    if not path.is_file():
        return {"status": "NOT_RUN"}
    manifest = json.loads(path.read_text(encoding="utf-8-sig"))
    if "ended" not in manifest:
        return {"status": "RUNNING", "dll": manifest["source_dll"]}
    lines = (folder / "Player.log").read_text(encoding="utf-8-sig", errors="replace").splitlines()
    # Deliberately omit map paths, raw logs, and game data from public evidence.
    selected = [x for x in lines if any(s in x for s in (
        "[gpudeco]", "[TileCull reload]", "[framecmp] diff ", "[framecmp] noise outside=",
        "[곡 시작] 화면:", "[곡] 평균", "[임무완주]", "[임무30] 시작", "[히트음 예약 묶기]"))]
    errors = [x for x in lines if "[StutterFix]" in x and any(s in x for s in (
        "[자동 시험] 실패", "[자동 시험] 단계 실패", "[자동 시험] 오류", "[장식굽기/그림자] 중단:",
        "restore_error=", "resume_error=", "resume_dispose_rejected=", "shutdown_disposed=False",
        "[안정성] GpuDecoLive 실패", "source_capacity_exhausted", "activation_resume_error="))]
    # ENDED is a lifecycle state, never a success verdict. A native crash can
    # truncate the managed log before an error line is written.
    if manifest.get("trial_error"):
        errors.append("manifest_trial_error="+manifest["trial_error"])
    if "reload_dll" in manifest and "new-dll-reenabled" not in manifest:
        errors.append("reload_reactivation_incomplete")
    probe=folder/"procdump.log"
    if probe.exists():
        raw=probe.read_bytes()
        text=raw.decode("utf-16-le",errors="replace") if b"\x00" in raw[:100] else raw.decode("utf-8",errors="replace")
        if "STACK_OVERFLOW" in text:errors.append("native_stack_overflow_observed")
        if "Unhandled: C0000005" in text:errors.append("native_unhandled_access_violation")
    rows = []
    pairs = []
    for x in lines:
        if "[framecmp] diff " in x:
            names=re.search(r"diff (\w+) (\w+)",x)
            count=re.search(r"changed=(\d+)/(\d+)",x)
            if names and count:
                pairs.append({"a":names[1],"b":names[2],"different":int(count[1]),"pixels":int(count[2]),
                              "max_rgba":list(map(int,re.search(r"maxRGBA=([\d,]+)",x)[1].split(",")))})
        if "[framecmp] diff A B " in x:
            match = re.search(r"changed=(\d+)/(\d+)", x)
            rows.append({"different": int(match[1]), "pixels": int(match[2]),
                         "max_rgba": list(map(int, re.search(r"maxRGBA=([\d,]+)", x)[1].split(",")))})
    return {
        "status": "ENDED", "dll": manifest["source_dll"],
        "native": manifest.get("source_native"), "bundle": manifest["bundle"],
        "restored": sum(manifest.get("restored", {}).values()),
        "restore_total": len(manifest.get("restored", {})),
        "game_running_after": manifest.get("game_running"),
        "pixels": rows, "pixel_pairs":pairs, "selected": selected, "errors": errors,
        "fuse_instrumentation_commands": sum("[임무단계] fuse " in x for x in lines),
    }


names = ["round23-song-smoke", "round23-song-full", "round24-song-full",
         "round25-song-pixels", "round25-song-g2", "round26-song-pixels", "round26b-song-pixels262", "round26-song-g2",
         "round27-cost-split", "round28-hardware-pixels", "round28-hardware-g2", "round29-resume", "round29c-resume",
         "round30-hitsound", "round31-resume-keys", "round32-gpu-keys", "round33-current-g2",
         "round34-anchor-elision-pixels", "round35-anchor-elision-g2", "round36-recovery", "round37-song-full",
         "round38-song-full", "round39a-pixels-88", "round39b-pixels-262", "round39c-pixels-270", "round40-hello-full",
         "round41-pixels270", "round42-pixels270", "round43-activation", "round44-native270",
         "round45a-pixels-88", "round45b-pixels-262", "round45c-pixels-270", "round45d-pixels-276", "round45e-controls",
         "round46-song-full", "round47-fg", "round48-shutdown", "round49-current-g2", "round50-off-full", "round52-resume-frame",
         "round53-fg", "round54-guard-owner", "round55-take-reason", "round56-natural-fg2", "round56-natural-fg4", "round57-resources", "round57b-resources", "round58-reload", "round58b-reload-dump", "round59-reload-control", "round59b-reload-control", "round61-reload-fixed", "round62-cost-hot", "round63-hello-full", "round64-cpu-full", "round65-gpu-full"]
evidence = {"scope": "whole-song research, not Player adoption",
            "latest_user_priority": "CPU .5ms advisory for future design; preserve legacy raw verdicts. Prioritize per-decoration CPU work removal, visual/sorting equivalence, FPS/hitches and full-song/fault recovery.",
            "zero_decoration_CPU_cost": "UNMEASURED", "runs": {n: collect(n) for n in names}}
for trial, pixel in {"round26-song-g2": "round26b-song-pixels262", "round28-hardware-g2": "round28-hardware-pixels",
                     "round33-current-g2":"round32-gpu-keys", "round35-anchor-elision-g2":"round34-anchor-elision-pixels",
                     "round49-current-g2":"round45b-pixels-262"}.items():
    if evidence["runs"][trial]["status"] == "ENDED" and evidence["runs"][pixel]["status"] == "ENDED":
        def meta(n):
            return json.loads((OUT/n/"manifest.json").read_text(encoding="utf-8-sig"))
        evidence["runs"][trial]["evidence_gate"] = assess(meta(trial), (OUT/trial/"Player.log").read_text(encoding="utf-8-sig", errors="replace"), meta(pixel))
evidence["current_artifacts"] = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
                                 for name in ("bin/PlayerAuto/StutterFix.dll", "native/sfnative.dll")}
target = ROOT / "tools/gpudeco-lab/evidence/song-summary.json"
target.write_text(json.dumps(evidence, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
for name, run in evidence["runs"].items():
    print(name, run["status"], "restored", run.get("restored"), "pixels", run.get("pixels"))

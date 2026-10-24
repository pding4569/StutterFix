"""Local evidence gate inspired by Harness2; not the upstream agent framework.

No model calls, game launches, instruction/memory edits, or threshold mutation.
Only a complete, uninstrumented G2 with matching paired artifacts is admissible.
"""
import argparse
import json
import math
from pathlib import Path
import re


def fields(line):
    return dict(re.findall(r"([A-Za-z_][A-Za-z_0-9]*)=([^\s]+)", line))


def assess(manifest, log, paired=None):
    issues = []
    stamps = ("source_dll", "source_native", "bundle")
    for key in stamps:
        if not re.fullmatch(r"[0-9a-f]{64}", str(manifest.get(key, ""))):
            issues.append("missing_or_invalid_" + key)
    if paired is None:
        issues.append("paired_pixel_artifacts_unverified")
    elif any(manifest.get(k) != paired.get(k) for k in stamps):
        issues.append("paired_artifacts_mismatch")
    restored = manifest.get("restored", {})
    if not manifest.get("ended") or manifest.get("game_running") is not False or len(restored) != 8 or not all(v is True for v in restored.values()):
        issues.append("incomplete_quit_or_byte_restoration")
    diagnostic = ("cost_probe_enabled=True", "threadcpu enabled=True", "[framecmp] capture-context",
                  "resume_key_fixture", "resume_roundtrip records=", "resume_handoff unobserved_order_excluded=",
                  "recovery_audit stage=", "recovery_fixture stage=", "recovery_force continued=", "CPU_census running_recs=",
                  "activation_values channel=", "activation_fixture stage=", "transition_fixture kind=", "resume_frame_fixture kind=", "original_guard_trace=True", "resource_probe marked=", "resource_probe checked=", "[TileCull reload]")
    if any(token in log for token in diagnostic) or re.search(r"\[임무단계\] fuse (begin|end|on)\b", log):
        issues.append("instrumented_not_clean_G2")
    if any(token in log for token in ("[자동 시험] 실패", "[자동 시험] 단계 실패", "[자동 시험] 오류", "resume_error=", "restore_error=", "shutdown_disposed=False")):
        issues.append("runtime_or_test_error")
    windows, costs, checkpoints, verdicts = {}, {}, set(), []
    for line in log.splitlines():
        row = fields(line)
        if "[gpudeco] FPS mode=" in line:
            idx = int(row["index"])
            if idx in windows:
                issues.append("duplicate_window")
            windows[idx] = row
        elif "[gpudeco] cost window=" in line:
            costs[int(row["window"])] = row
        elif "[gpudeco] stability checkpoint=" in line and row.get("pass") == "True":
            checkpoints.add(row["checkpoint"])
        elif "[gpudeco] GPUDECO_G2=" in line:
            verdicts.append(row)
    metrics = {}
    complete = set(windows) == {0, 1, 2, 3} and set(costs) == {1, 2}
    if not complete:
        issues.append("four_windows_or_two_costs_missing")
    else:
        for idx, w in windows.items():
            if w.get("valid") != "True" or w.get("mode") != ("on" if idx in (1, 2) else "off"):
                issues.append("invalid_window")
            if int(w.get("spikes_30ms", "-1")) != 0:
                issues.append("window_spikes_or_missing_counter")
        off = sum(float(windows[i]["fps"]) for i in (0, 3)) / 2
        on = sum(float(windows[i]["fps"]) for i in (1, 2)) / 2
        cpu = sum(float(costs[i]["total_ms"]) for i in (1, 2)) / 2
        if not all(math.isfinite(x) and x > 0 for x in (off, on, cpu)):
            issues.append("invalid_numeric_metrics")
        else:
            gain = (on / off - 1) * 100
            metrics = {"off_fps": off, "on_fps": on, "gain_pct": gain, "cpu_ms": cpu}
            if gain < 25:
                issues.append("gain_below_25pct")
            if cpu > 0.5:
                issues.append("cpu_above_0.5ms")
        for c in costs.values():
            if c.get("native_errors") != "0" or int(c.get("native_callbacks", "0")) <= 0:
                issues.append("native_callback_or_error_contract")
    if not {"pause_resume", "song_end", "retry_restart", "reopen_toggle"}.issubset(checkpoints):
        issues.append("stability_checkpoints_missing")
    if len(verdicts) != 1 or verdicts[0].get("GPUDECO_G2") != "PASS":
        issues.append("raw_G2_not_PASS")
    return {"schema": 1, "admissible_G2_evidence": not issues, "issues": sorted(set(issues)),
            "metrics": metrics, "artifacts": {k: manifest.get(k) for k in stamps},
            "full_song_quality": "UNMEASURED", "public_adoption": False,
            "lessons": ["Keep thresholds fixed; a failed probe is evidence, not a pass.",
                        "Same-artifact G2 admission alone does not establish whole-song visual equivalence."]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trial", type=Path)
    parser.add_argument("--paired", type=Path, help="paired pixel trial manifest (matching DLL/native/bundle)")
    args = parser.parse_args()
    paired = json.loads(args.paired.read_text(encoding="utf-8-sig")) if args.paired else None
    result = assess(json.loads((args.trial / "manifest.json").read_text(encoding="utf-8-sig")),
                    (args.trial / "Player.log").read_text(encoding="utf-8-sig", errors="replace"), paired)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["admissible_G2_evidence"] else 2


if __name__ == "__main__":
    raise SystemExit(main())

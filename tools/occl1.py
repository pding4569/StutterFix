"""Occlusion experiment. Copy at E:/StutterFixWork/occl1.py; only Claude runs it.

python occl1.py <map key> <CFG2 setting> [--dry-run]
Dry run only writes the reviewable JSON and does not start the game.
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(r"E:\StutterFixWork")
sys.path.insert(0, str(ROOT))


def steps_for(mk, path, at, commands, map_root):
    def grab(tag):
        # FrameCompare.Step returns false until Grab's async readback completes.
        # Grab yields one frame, then WaitForEndOfFrame; save cannot run early.
        return [f"framecmp grab {mk}_{tag}", f"framecmp save {mk}_{tag}", "framecmp clear"]

    def window(tag):
        # Keep expensive framecmp state census/readback OUTSIDE the GPU window.
        return ["gpuprobe reset", "wait 4", "gpuprobe state", f"ownprobe mark-occl1-{tag}"] + grab(tag)

    st = ["timeout 900", "wait 2", "auto on", "fpscap 0", "occlude collect", "open " + map_root + path]
    st += list(commands) + [f"seek {at} 8", f"waitsongpos {at} 400", "pause", "wait 2",
                            "gpuprobe on", "wait 2", "framecmp filters on", "framecmp mask none"]
    st += window("all") + ["occlude census", "wait 1", "occlude hide on", "wait 1"]
    st += window("hide") + ["occlude hide off", "wait 1"] + window("all2")
    # Adjacent ON/OFF pairs, twice. No gpuprobe window, census or wait between
    # the two captures; each grab itself waits for readback before the toggle.
    for pair in (1, 2):
        st += ["occlude hide on"] + grab(f"pair{pair}_hide")
        st += ["occlude hide off"] + grab(f"pair{pair}_all")
    st += ["occlude hide off", "gpuprobe off", "resume", "wait 1", "quit"]
    return st


def compare(src, mk):
    import numpy as np
    from PIL import Image

    def load(tag):
        return np.asarray(Image.open(src / f"{mk}_{tag}.png").convert("RGBA")).astype(np.int16)

    for a, b in [("all", "hide"), ("all", "all2"),
                 ("pair1_hide", "pair1_all"), ("pair2_hide", "pair2_all")]:
        x, y = load(a), load(b)
        if x.shape != y.shape:
            raise ValueError(f"dimension mismatch: {a} {x.shape}, {b} {y.shape}")
        delta = np.abs(x - y).max(axis=2)
        print(mk, a, b, f"pixels={delta.size} >0={np.count_nonzero(delta > 0)} "
              f">8={np.count_nonzero(delta > 8)} maxRGBA={delta.max()} "
              f">0pct={(delta > 0).mean()*100:.6f} >8pct={(delta > 8).mean()*100:.6f}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("map")
    parser.add_argument("setting")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    import bench_v14 as B
    mk, cn = args.map, args.setting
    path, at = B.MAPS[mk]
    # Handoff's 7777 reference frame is 124s (bench_v14 defaults to 120s).
    if mk == "s7":
        at = 124
    cfg, commands = B.CFG2[cn]
    name = f"occl1-{mk}-{cn}"
    js = ROOT / f"{name}.json"
    js.write_text(json.dumps(dict(steps=steps_for(mk, path, at, commands, B.M),
                                 settings=dict(B.CLEAN, **cfg), timeout_min=20, tag=name),
                             ensure_ascii=False, indent=2), encoding="utf-8")
    if args.dry_run:
        print(f"DRY_RUN {js}")
        return
    src = Path(r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\framecmp")
    tags = ("all", "hide", "all2", "pair1_hide", "pair1_all", "pair2_hide", "pair2_all")
    # Reject stale images from a previous run, even if this run fails before grab.
    for tag in tags:
        (src / f"{mk}_{tag}.png").unlink(missing_ok=True)
    result = subprocess.run([sys.executable, "run_locked.py", str(js), "wt-v14prod", name,
                             "--probe", "--keys", "gpuprobe],occlude],ownprobe],framecmp],단계 실패,오류로 그만둠",
                             "--tail", "1"], cwd=ROOT, capture_output=True, text=True,
                            encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    log = (ROOT / "night-results" / f"{name}.txt").read_text(encoding="utf-8", errors="replace")
    cam = ps = None
    for line in log.splitlines():
        if ("단계 실패" in line or "오류로 그만둠" in line or "[occlude] failed" in line
                or "[occlude] census timed out" in line):
            raise RuntimeError(line)
        if "camera:Camera#" in line and "mean_frame_ms=" in line:
            cam = line.split("mean_frame_ms=", 1)[1].split()[0]
        if "ps_invocations_per_frame=" in line:
            ps = line.split("ps_invocations_per_frame=", 1)[1].split()[0]
        if "mark-occl1-" in line:
            tag = line.split("mark-occl1-", 1)[1].split()[0]
            if cam is None or ps is None:
                raise RuntimeError(f"incomplete GPU window: {tag} cam={cam} ps={ps}")
            print(mk, cn, tag, "cam_ms", cam, "ps_invocations_per_frame", ps, flush=True)
            cam = ps = None
        if "[occlude]" in line:
            print(line, flush=True)
    compare(src, mk)
    print("OCCL1_DONE", flush=True)


if __name__ == "__main__":
    main()

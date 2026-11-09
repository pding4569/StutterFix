import json, subprocess, sys, os
sys.path.insert(0, r"E:\StutterFixWork")
import bench_v14 as B
mk, cn = sys.argv[1], sys.argv[2]; path, at = B.MAPS[mk]; name = f"gp5-{mk}-{cn}"
cfg0, cmds = B.CFG2[cn]
def win(tag): return ["gpuprobe reset", "wait 4", "gpuprobe state", f"ownprobe mark-gp5-{tag}"]
st = ["timeout 900", "wait 2", "auto on", "fpscap 0", "open " + B.M + path] + cmds + [f"seek {at} 8", f"waitsongpos {at} 400", "pause", "wait 2", "gpuprobe on", "wait 2"] + win("all")
for mn, g in [(2048, "decomip"), (2048, "decoquarter"), (1024, "decomip"), (1024, "decoquarter")]:
    st += [f"decotexmin {mn}", f"hidegroup {g} on", "wait 1"] + win(f"{g}{mn}") + [f"hidegroup {g} off", "wait 1"]
st += win("all2") + ["gpuprobe off", "resume", "wait 1", "quit"]
cfg = dict(B.CLEAN, **cfg0)
js = B.ROOT / f"{name}.json"; js.write_text(json.dumps(dict(steps=st, settings=cfg, timeout_min=14, tag=name), ensure_ascii=False), encoding="utf-8")
subprocess.run([sys.executable, "run_locked.py", str(js), "wt-v14prod", name, "--probe", "--keys", "gpuprobe],hidegroup],ownprobe],단계 실패", "--tail", "1"], cwd=B.ROOT, capture_output=True)
js.unlink(missing_ok=True)
t = (B.ROOT / "night-results" / f"{name}.txt").read_text(encoding="utf-8", errors="replace")
cam = ps = None
for line in t.splitlines():
    if "camera:Camera#" in line: cam = line.split("mean_frame_ms=")[1].split()[0]
    if "ps_invocations_per_frame" in line: ps = int(line.split("ps_invocations_per_frame=")[1].split()[0])//1000000
    if "hidegroup]" in line: print("  ", line.split("hidegroup] ")[1][:160])
    if "mark-gp5-" in line: print(mk, cn, line.split("mark-gp5-")[1].split()[0], "cam_ms", cam, "psM", ps, flush=True); cam = ps = None
    if "단계 실패" in line: print(line.strip()[:200])
print("GP5_DONE", flush=True)

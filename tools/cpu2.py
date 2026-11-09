import json, subprocess, sys
sys.path.insert(0, r"E:\StutterFixWork")
import bench_v14 as B
mk, cn = sys.argv[1], sys.argv[2]; path, at = B.MAPS[mk]; name = f"cpu2-{mk}-{cn}"
cfg0, cmds = B.CFG2[cn]
st = ["timeout 900", "wait 2", "auto on", "fpscap 0", "open " + B.M + path] + cmds + ["cpubreak on", f"seek {at} 8", f"waitsongpos {at} 400", "wait 2", "cpubreak begin", "wait 10", "cpubreak end", "wait 1", "quit"]
cfg = dict(B.CLEAN, **cfg0)
js = B.ROOT / f"{name}.json"; js.write_text(json.dumps(dict(steps=st, settings=cfg, timeout_min=14, tag=name), ensure_ascii=False), encoding="utf-8")
subprocess.run([sys.executable, "run_locked.py", str(js), "wt-v14prod", name, "--keys", "CPU 분해],단계 실패", "--tail", "1"], cwd=B.ROOT, capture_output=True)
js.unlink(missing_ok=True)
t = (B.ROOT / "night-results" / f"{name}.txt").read_text(encoding="utf-8", errors="replace")
for line in t.splitlines():
    if "프레임당" in line or "단계 실패" in line:
        body = line.split("CPU 분해] ")[-1]
        for part in body.split(", "): print(mk, cn, part[:160])
print("CPU2_DONE", flush=True)

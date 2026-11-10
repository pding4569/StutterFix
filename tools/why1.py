import json, subprocess, sys
sys.path.insert(0, r"E:\StutterFixWork")
import bench_v14 as B
mk = sys.argv[1]; path, at = B.MAPS[mk]; name = f"why1-{mk}"
cfg0, cmds = B.CFG2["gBsSpT"]
st = ["timeout 900", "wait 2", "auto on", "fpscap 0", "open " + B.M + path] + cmds + [f"seek {at} 8", f"waitsongpos {at} 400", "wait 1", "runlod why", "runlod state", "wait 1", "quit"]
js = B.ROOT / f"{name}.json"; js.write_text(json.dumps(dict(steps=st, settings=dict(B.CLEAN, **cfg0), timeout_min=12, tag=name), ensure_ascii=False), encoding="utf-8")
subprocess.run([sys.executable, "run_locked.py", str(js), "wt-v14prod", name, "--keys", "runlod],단계 실패", "--tail", "1"], cwd=B.ROOT, capture_output=True)
js.unlink(missing_ok=True)
t = (B.ROOT / "night-results" / f"{name}.txt").read_text(encoding="utf-8", errors="replace")
for line in t.splitlines():
    if "why:" in line:
        for part in line.split("why:")[1].split(" | "): print(mk, part.strip()[:260])
    elif "runlod] on=" in line: print(mk, line.split("runlod] ")[1][:200])
print("WHY_DONE")

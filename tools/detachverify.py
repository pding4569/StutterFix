import json, subprocess, sys
sys.path.insert(0, r"E:\StutterFixWork")
import bench_v14 as B
mk = sys.argv[1]; path, at = B.MAPS[mk]; name = f"dv-{mk}"
cfg0, cmds = B.CFG2["rlNR"]
st = (["timeout 900", "wait 2", "auto on", "fpscap 0", "rtsize 3440 1440", "open " + B.M + path] + cmds
      + [f"seek {at} 8", f"waitsongpos {at} 400", "pause", "wait 2",
         "framecmp grab a0", "framecmp grab a1", "framecmp assertzero a0 a1",
         "decodetach verify a", "decodetach force", "wait 3", "decodetach verify b", "framecmp grab b", "framecmp diff a0 b",
         "decodetach unforce", "wait 3", "decodetach verify b", "framecmp grab c", "framecmp diff a0 c",
         "decodetach force", "resume", "tightlate verify", "wait 14", "tightlate", "decodetach unforce", "wait 2", "quit"])
cfg = dict(B.CLEAN, **cfg0)
js = B.ROOT / f"{name}.json"; js.write_text(json.dumps(dict(steps=st, settings=cfg, timeout_min=14, tag=name), ensure_ascii=False), encoding="utf-8")
subprocess.run([sys.executable, "run_locked.py", str(js), "wt-v14prod", name, "--keys", "framecmp],decodetach],tightlate],단계 실패,valid=", "--tail", "1"], cwd=B.ROOT, capture_output=True)
js.unlink(missing_ok=True)
print("DV_DONE", mk, flush=True)

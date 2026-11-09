import json, subprocess, sys
sys.path.insert(0, r"E:\StutterFixWork")
import bench_v14 as B
mk, cn = sys.argv[1], sys.argv[2]; path, at = B.MAPS[mk]; name = f"gp3-{mk}-{cn}"
cfg0, cmds = B.CFG2[cn]
groups = ["shader:Hidden/BlendModes/SpritesDefault/Grab", "shader:Legacy Shaders/Particles/Additive", "shader:Sprites/Tile", "shader:Sprites/Default",
          "shader:Mobile/Particles/Additive", "shader:Sprites/Mask", "size:2048-100000", "size:1024-2047", "size:0-1023"]
st = ["timeout 900", "wait 2", "auto on", "fpscap 0", "open " + B.M + path] + cmds + [f"seek {at} 8", f"waitsongpos {at} 400", "pause", "wait 2", "gpuprobe on", "wait 2"]
for g in groups: st += [f"gpuhide {g} 3", "wait 13"]
st += ["gpuprobe off", "resume", "wait 1", "quit"]
cfg = dict(B.CLEAN, **cfg0)
js = B.ROOT / f"{name}.json"; js.write_text(json.dumps(dict(steps=st, settings=cfg, timeout_min=16, tag=name), ensure_ascii=False), encoding="utf-8")
subprocess.run([sys.executable, "run_locked.py", str(js), "wt-v14prod", name, "--probe", "--keys", "gpuhide],단계 실패", "--tail", "1"], cwd=B.ROOT, capture_output=True)
js.unlink(missing_ok=True)
t = (B.ROOT / "night-results" / f"{name}.txt").read_text(encoding="utf-8", errors="replace")
for line in t.splitlines():
    if ("gpuhide] group=" in line and "changed_renderable" in line) or "camera:Camera#" in line and "delta_ms_est" in line or "단계 실패" in line or "unknown" in line:
        print(mk, cn, line.split("gpuhide] ")[-1][:260], flush=True)
print("GP3_DONE", flush=True)

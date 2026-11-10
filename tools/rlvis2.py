import json, subprocess, sys, os
sys.path.insert(0, r"E:\StutterFixWork")
import bench_v14 as B
mk, cn = sys.argv[1], sys.argv[2]; path, at = B.MAPS[mk]; name = f"rlvis2-{mk}-{cn}"
cfg0, cmds = B.CFG2[cn]
def g(tag): return [f"framecmp grab {mk}{cn}_{tag}", f"framecmp save {mk}{cn}_{tag}", "framecmp clear"]
st = ["timeout 900", "wait 2", "auto on", "fpscap 0", "open " + B.M + path] + cmds + [f"seek {at} 8", f"waitsongpos {at} 400", "pause", "wait 2", "framecmp filters on", "framecmp mask none", "runlod state"] + g("off") + g("off2") + ["runlod force", "runlod resume", "wait 1"] + g("on") + ["runlod state", "runlod force off", "resume", "wait 1", "quit"]
js = B.ROOT / f"{name}.json"; js.write_text(json.dumps(dict(steps=st, settings=dict(B.CLEAN, **cfg0), timeout_min=12, tag=name), ensure_ascii=False), encoding="utf-8")
subprocess.run([sys.executable, "run_locked.py", str(js), "wt-v14prod", name, "--keys", "runlod],단계 실패", "--tail", "1"], cwd=B.ROOT, capture_output=True)
js.unlink(missing_ok=True)
t = (B.ROOT / "night-results" / f"{name}.txt").read_text(encoding="utf-8", errors="replace")
for line in t.splitlines():
    if "runlod] on=" in line: print(mk, cn, line.split("runlod] ")[1][:120])
from PIL import Image; import numpy as np, shutil
src = r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\framecmp"
def L(n): return np.asarray(Image.open(os.path.join(src, n + ".png")).convert('RGB')).astype(int)
try:
    A = L(f"{mk}{cn}_off")
    for k in ["off2", "on"]:
        Bm = L(f"{mk}{cn}_{k}"); d = np.abs(A - Bm).max(axis=2); mse = ((A - Bm) ** 2).mean()
        print(mk, cn, "off vs", k, ">8 %.2f%% >32 %.2f%% PSNR %.1f" % ((d > 8).mean() * 100, (d > 32).mean() * 100, 10 * np.log10(255 ** 2 / max(mse, 1e-9))))
    for k in ["on", "off"]: shutil.copy(os.path.join(src, f"{mk}{cn}_{k}.png"), str(B.ROOT / f"rlvis2-{mk}-{cn}-{k}.png"))
except Exception as e: print("cmp ERR", e)
print("RLVIS_DONE", flush=True)

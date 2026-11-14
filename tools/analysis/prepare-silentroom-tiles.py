"""Generate fresh-from-start AUTOTEST plans only. No game launch/install/build."""
import importlib.util
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
bench_path = ROOT.parent / "bench_v14.py"
os.environ["BENCH_WT"] = ROOT.name
os.environ["BENCH_GAME"] = "1"
os.environ["BENCH_RT"] = "1920x1080"
spec = importlib.util.spec_from_file_location("silentroom_bench", bench_path)
bench = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bench)
out = ROOT / "out/silentroom-tiles/plans"
out.mkdir(parents=True, exist_ok=True)
features = ["FloorAnim", "ZeroTween", "TweenGuard", "EffectSplit", "RecolorDefer", "RecolorSplit", "MeshWarm", "MoveFinish", "DormantSkip"]
# Keep both the existing Claude baseline and a tester-style default baseline.
# The latter avoids adopting rlNR's extra experimental CPU/GPU settings.
base = dict(bench.CLEAN, **{key: True for key in features})
variants = {"defaults": ({}, []), "alloff": ({key: False for key in features}, []),
            **{key.lower() + "off": ({key: False}, []) for key in features},
            "tileculloff": ({}, ["tilecullmin 10000000"]),
            "waitskipoff": ({"EngineWaitSkip": False}, ["waitskip off"]),
            "existingA": (bench.CFG2["srA"][0], list(bench.CFG2["srA"][1]))}
for name, (overrides, commands) in variants.items():
    tag = "silentroom-" + name
    capture = []
    for t in (0.5, 1, 1.5, 1.75, 1.95, 2.2, 3, 5, 10, 13.5, 14, 15):
        label = tag + "-" + str(t).replace(".", "p")
        capture += [f"waitsongpos {t} 400", "floorstate 0 1 50 69 70 329 330 400 564 565 1186",
                    f"framecmp grab {label}", f"framecmp save {label}", "framecmp clear"]
    steps = bench.steps("[노가다 크루] Silentroom - 1KXYZO/level.adofai", 0.1, tag, commands, False, capture)
    steps = steps[:steps.index("phasewindow begin")] + ["phasewindow off", "quit"]
    assert any(s.startswith("game ") for s in steps) and not any(s.startswith(("seek ", "open ")) for s in steps)
    plan = dict(tag=tag, timeout_min=15, settings=dict(base, **overrides), steps=steps)
    (out / (tag + ".json")).write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Prepared {len(variants)} plans in {out}; no game launched.")

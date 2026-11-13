"""Offline bench plan admission checks; never call bench.main/run_locked."""
import ast
import importlib.util
import os
from pathlib import Path

root = Path(__file__).resolve().parents[2]
bench_file = root.parent / "bench_v14.py"
spec = importlib.util.spec_from_file_location("scriptpath_bench", bench_file)
b = importlib.util.module_from_spec(spec)
spec.loader.exec_module(b)
original = repr(b.CFG2["rlNR"])
out = root / "out/scriptpath/plans"
out.mkdir(exist_ok=True, parents=True)
import json
for game in (False, True):
    if game:
        os.environ["BENCH_GAME"] = "1"
    else:
        os.environ.pop("BENCH_GAME", None)
    for key in ("spFloor", "spConductor", "spCanvas", "spExact", "spVerify"):
        cfg, start = b.CFG2[key]
        assert (cfg, start) == b.CFG2["rlNR"]
        assert cfg is not b.CFG2["rlNR"][0] and start is not b.CFG2["rlNR"][1]
        plan = b.steps("map", 50, key, start, False, b.CFG_MID[key], b.CFG_POST[key])
        activate = b.CFG_MID[key][0]
        assertion = b.CFG_POST[key][0]
        assert plan.index("waitsongpos 50 400") < plan.index(activate) < plan.index("fpsmark start")
        assert plan.index(f"fpsmark end {key}") < plan.index(assertion) < plan.index("scriptskip off")
        assert plan[-1] == "quit"
        (out / (key + ("-game" if game else "-editor") + ".json")).write_text(
            json.dumps(dict(steps=plan, settings=dict(b.CLEAN, **cfg)), indent=2), encoding="utf-8")
    assert not any(x.startswith("scriptskip") for x in b.steps("map", 50, "baseline", [], False))
assert repr(b.CFG2["rlNR"]) == original
assert "[scriptskip" in b.KEYS
for key, marker in b.CFG_ACCEPT.items():
    assert b.scriptpath_admitted(key, marker)
    assert not b.scriptpath_admitted(key, "[FPS 구간] printed before failed assertion")
    assert not b.scriptpath_admitted(key, marker + " 단계 실패")
    assert not b.scriptpath_admitted(key, marker + " scriptskip exact INVALID")
assert not b.scriptpath_admitted("spVerify", b.CFG_ACCEPT["spExact"])
assert b.scriptpath_admitted("rlNR", "baseline")
ast.parse(bench_file.read_text(encoding="utf-8"))
print("PASS: five isolated rlNR copies; 10 editor/game plans; post-seek activation; post-FPS assertion/restoration; unchanged baseline; diagnostic log filter; Python syntax. No game/runner/install invoked.")

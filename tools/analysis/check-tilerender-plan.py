"""Offline plan contract checks; never launches a game or runner."""
import ast
import importlib.util
import json
import os
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location("tilerender_plan", root / "tilerender_run.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
bench = module.load_bench()
original = repr(bench.CFG2["rlNR"])
output = Path(__file__).resolve().parents[2] / "out" / "tilerender-plans"
output.mkdir(exist_ok=True)
for key, at in {"hl": 50, "sh": 88, "wf": 260, "s7": 120}.items():
    value = module.plan(key, "tilerender-" + key)
    steps = value["steps"]
    assert f"seek {at} 8" in steps
    assert steps.index("tilerender census") < steps.index("pause")
    assert steps.index("tilerender on") < steps.index("framecmp grab b")
    assert steps.count("tilerender assertactive") == 2
    assert "framecmp assertpixels a b" in steps
    assert "framecmp assertzero a a2" in steps
    assert "framecmp assertzero a restored png" in steps
    assert steps.index("tilerender off") < steps.index("framecmp grab restored")
    with (output / (key + ".json")).open("w", encoding="utf-8") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
assert repr(bench.CFG2["rlNR"]) == original
assert bench.CFG2["trOn"] == bench.CFG2["rlNR"]
for index in (0, 1):
    assert bench.CFG2["trOn"][index] is not bench.CFG2["rlNR"][index]
normal = bench.steps("map", 50, "normal", [], False)
assert "tilerender on" not in normal
for game in (False, True):
    if game:
        os.environ["BENCH_GAME"] = "1"
    else:
        os.environ.pop("BENCH_GAME", None)
    candidate = bench.steps("map", 50, "candidate", [], False, bench.CFG_MID["trOn"])
    assert candidate.index("waitsongpos 50 400") < candidate.index("tilerender on") < candidate.index("fpsmark start")
for file in (root / "tilerender_run.py", root / "bench_v14.py"):
    ast.parse(file.read_text(encoding="utf-8"))
sys.argv = ["bench_v14.py", "guard-test", "hl", "trOn"]
try:
    bench.main()
except SystemExit as error:
    assert str(error).startswith("trOn INVALID:")
else:
    raise AssertionError("live benchmark unexpectedly admitted")
print("PASS: four map plans/times, isolated config copies, playback census, active guards,")
print("candidate pixels versus noise/restoration assertions, post-seek MID (editor/game),")
print("live ABBA preflight refusal, Python syntax. No game/runner/install invoked.")

"""Offline command-sequence checks; never load or launch the game."""
import importlib.util
import json
from pathlib import Path

spec = importlib.util.spec_from_file_location("steps", Path(__file__).with_name("framecmp-steps.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
cases = [module.build("Arche.adofai"), module.build("Arche.adofai", png=True),
         module.build("Arche.adofai", "matshare on", "matshare off", True)]
count = 0
for case in cases:
    steps = case["steps"]
    assert len(steps) == 29
    assert steps.index("framecmp mask center") < steps.index("framecmp noise N1 N2")
    assert steps.index("pause") < steps.index("framecmp noise N1 N2") < steps.index("framecmp grab A")
    assert steps[20].startswith("framecmp assertzero A A2")
    assert steps[22:24] == ["framecmp control order", "framecmp control hide"]
    assert steps[-4:] == ["resume", "framecmp result", "stop", "quit"]
    assert json.loads(json.dumps(case)) == case
    count += 7
assert cases[1]["steps"][20] == "framecmp assertzero A A2 png"
assert cases[2]["steps"][14] == "matshare on" and cases[2]["steps"][17] == "matshare off"
example = Path(__file__).resolve().parents[2] / "docs/handoff/framecmp-example-steps.json"
assert json.loads(example.read_text(encoding="utf-8")) == cases[0]
print(f"framecmp steps: {count + 3} checks passed; JSON only, no game launch")

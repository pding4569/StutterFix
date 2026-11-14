"""Check generated evidence consistency; this does not validate game pixels."""
import hashlib
import json
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
out = root / "out/silentroom-tiles"
for name in ("native-exports.txt", "game-move-il.txt", "game-scrub-il.txt", "build-native.log", "build-dev.log", "build-player.log", "build-auto.log"):
    path = out / name
    raw = path.read_bytes()
    if raw.startswith(b"\xff\xfe"):
        path.write_text(raw.decode("utf-16"), encoding="utf-8")
exports = {m[2]: int(m[1]) for m in re.finditer(r"^\s*(\d+)\s+[0-9A-F]+\s+[0-9A-F]+\s+(\w+)", (out / "native-exports.txt").read_text(encoding="utf-8"), re.M)}
defs = {m[1]: int(m[2]) for m in re.finditer(r"^\s*(\w+)\s+@(\d+)", (root / "native/sfnative/sfnative.def").read_text(), re.M)}
assert exports == defs and len(exports) == 73
plans = sorted((out / "plans").glob("*.json"))
assert len(plans) == 14
for path in plans:
    plan = json.loads(path.read_text(encoding="utf-8"))
    assert any(s.startswith("game ") for s in plan["steps"])
    assert not any(s.startswith(("seek ", "open ")) for s in plan["steps"])
    assert sum(s.startswith("framecmp grab ") for s in plan["steps"]) == 12
    assert plan["steps"].count("floorstate 0 1 50 69 70 329 330 400 564 565 1186") == 12
t = json.loads((out / "timeline.json").read_text(encoding="utf-8"))
assert len(t["play"]) == 19 and all(r["duration_s"] == 0 for r in t["play"])
assert t["initial"][330]["scale"] == 1.5 and t["initial"][564]["appear"] == "None"
assert 1.836 < next(r["start_s"] for r in t["play"] if r["source_index"] == 100) < 1.838
builds = {}
for edition in ("dev", "player", "auto"):
    text = (out / f"build-{edition}.log").read_text(encoding="utf-8")
    warnings = re.findall(r"경고 (\d+)개", text)
    errors = re.findall(r"오류 (\d+)개", text)
    assert errors and errors[-1] == "0"
    builds[edition] = dict(errors=int(errors[-1]), warnings=int(warnings[-1]))
assert "OK " in (out / "build-native.log").read_text(encoding="utf-8")
result = dict(builds=builds, native_exports=len(exports), plans=len(plans), timeline_sanity="PASS",
              native_sha256=hashlib.sha256((root / "native/sfnative.dll").read_bytes()).hexdigest(),
              game_run=False, pixel_parity="UNVERIFIED")
(out / "verification.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result, indent=2))

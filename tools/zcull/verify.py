"""Offline package / runner checks. Never invokes run_locked or touches installed DLLs."""
import importlib.util
import json
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("zcull_runner", REPO / "tools/zcull/zcull_run.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)
player = (REPO / "bin/Player/StutterFix.dll").read_bytes()
auto = (REPO / "bin/PlayerAuto/StutterFix.dll").read_bytes()
for name in ("zcull", "ZCull", "ResearchZCull", "ZCullReduce"):
    for encoding in ("utf-8", "utf-16-le"):
        assert name.encode(encoding) not in player, ("production leak", name, encoding)
assert b"StutterFix.zcull" in auto and b"ZCullPolicy" in auto
assert (runner.ROOT / "zcull_run.py").read_bytes() == (REPO / "tools/zcull/zcull_run.py").read_bytes()
for key, at in runner.TIMES.items():
    value = json.loads((runner.OUT / f"zcull-{key}-steps.json").read_text(encoding="utf-8"))
    assert value["settings"]["GpuDeco"] == 0 and not value["settings"]["RunLod"]
    assert value["steps"][-7:] == [f"seek {at} 8", f"waitsongpos {at} 400", "pause", "wait 2", "zcull census", "resume", "quit"]
with tempfile.TemporaryDirectory(prefix="zcull-parser-") as temp:
    runner.OUT = Path(temp)
    path = runner.OUT / "zcull-hl.txt"
    summary = "[zcull] renderers=2 occluders=1 fragments_total=512.000 fragments_alive=384.000 culled=25.000%"
    path.write_text(summary, encoding="utf-8")
    assert runner.result("hl") is None  # Incomplete readback is never a completed run.
    path.write_text(summary + "\n[zcull] complete;", encoding="utf-8")
    assert runner.result("hl") == ("2", "1", "512.000", "384.000", "25.000")
    (runner.OUT / "zcull-hl.log").write_text("단계 실패", encoding="utf-8")
    assert runner.result("hl") is None
print("ZCULL_PACKAGE=PASS player_strings=absent auto_types_and_resource=present runner_mirror=identical plans=6 parser_cases=3")

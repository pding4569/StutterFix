"""Saved T3 evidence + edition metadata; never loads game code."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
out = root / "docs/handoff/tilerange-evidence"
scanner = root / "tools/ilscan/bin/Release/net8.0/ILScan.exe"
prefix = "t3b" if "--t3b" in sys.argv else "t3"

def read(path):
    data = path.read_bytes()
    return data.decode("utf-16" if data.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8-sig")

builds = {}
for edition, folder in [("dev", "Debug"), ("player", "Player"), ("autotest", "PlayerAuto")]:
    log = read(out / f"{prefix}-build-{edition}.txt")
    errors = re.findall(r"오류 (\d+)개", log)
    warnings = re.findall(r"경고 (\d+)개", log)
    assert errors and int(errors[-1]) == 0, edition
    dll = root / "bin" / folder / "StutterFix.dll"
    present = {}
    for typename in ["FloorRange", "FloorRangeWriters", "FloorRangeLedger"]:
        env = {k: v for k, v in os.environ.items() if k not in {"METHOD", "TYPES", "TILE_READERS", "TILE_WRITERS", "DECO_READERS", "DECO_GUARDS"}}
        env["TYPES"] = "StutterFix." + typename
        result = subprocess.run([str(scanner), str(dll), "ZZZ"], env=env, capture_output=True, encoding="utf-8", check=True)
        present[typename] = "== StutterFix." + typename in result.stdout
        assert present[typename] == (edition != "player" and typename != "FloorRangeLedger"), (edition, typename)
    builds[edition] = {"sha256": hashlib.sha256(dll.read_bytes()).hexdigest(), "errors": 0, "warnings": int(warnings[-1]), "types": present}

fixture = read(out / f"{prefix}-fixture.txt").strip()
metadata = read(out / f"{prefix}-writer-metadata.txt").strip().splitlines()[-1]
loadcheck = read(out / ("t3b-loadcheck-dev.txt" if prefix == "t3b" else "t3-loadcheck.txt")).strip()
assert ("PASS T3 checks=76" if prefix == "t3b" else "PASS T3 checks=70") in fixture
assert "PASS metadata targets=53 exact=1 each" in metadata
summary = {"scope": "T3 default-off DEV/AUTOTEST write invalidation and pristine skips; no game/install/commit/profsamp", "builds": builds,
           "fixture": fixture, "writer_metadata": metadata, "loadcheck": loadcheck}
if prefix == "t3b":
    summary["writer_rewrite"] = read(out / "t3b-writer-rewrite.txt").strip()
    assert "PASS writer-rewrite checks=16" in summary["writer_rewrite"]
    summary["loadchecks"] = {edition: read(out / f"t3b-loadcheck-{edition}.txt").strip() for edition in ["dev", "player", "autotest"]}
    assert all("틀림 0" in value for value in summary["loadchecks"].values())
(out / f"{prefix}-validation-summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(summary, ensure_ascii=False))

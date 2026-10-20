"""Validate saved evidence and edition metadata without executing game code."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

root = Path(__file__).resolve().parents[2]
out = root / "docs/handoff/tilerange-evidence"
scanner = root / "tools/ilscan/bin/Release/net8.0/ILScan.exe"

def read(path):
    data = path.read_bytes()
    return data.decode("utf-16" if data.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8-sig")

builds = {}
for edition, folder in [("dev", "Debug"), ("player", "Player"), ("autotest", "PlayerAuto")]:
    log = read(out / f"build-{edition}.txt")
    warnings = re.findall(r"경고 (\d+)개", log)
    errors = re.findall(r"오류 (\d+)개", log)
    assert errors and int(errors[-1]) == 0, edition + " build not successful"
    dll = root / "bin" / folder / "StutterFix.dll"
    present = {}
    for typename in ["FloorRange", "FloorRangeLedger"]:
        env = {k: v for k, v in os.environ.items() if k not in {"TILE_READERS", "METHOD", "DECO_READERS", "DECO_GUARDS"}}
        env["TYPES"] = "StutterFix." + typename
        result = subprocess.run([str(scanner), str(dll), "ZZZ"], env=env, capture_output=True, encoding="utf-8", check=True)
        present[typename] = "== StutterFix." + typename in result.stdout
        assert present[typename] == (edition != "player"), (edition, typename)
    builds[edition] = {"sha256": hashlib.sha256(dll.read_bytes()).hexdigest(),
                       "errors": int(errors[-1]), "warnings": int(warnings[-1]), "research_types": present}

inventory = json.loads((out / "readers-inventory.json").read_text(encoding="utf-8"))
assert all(row["exit"] == 0 for row in inventory), "reader scan failure"
current = next(row for row in inventory if row["path"] == str(root / "bin/Debug/StutterFix.dll"))
assert current["sha256"] == builds["dev"]["sha256"], "inventory is from another developer DLL"
fixtures = {"ledger": read(out / "fixture-ledger.txt").strip(), "integration": read(out / "fixture-integration.txt").strip()}
assert "PASS assertions=128767" in fixtures["ledger"]
assert "PASS integration checks=69" in fixtures["integration"]
summary = {"scope": "default-off DEV/AUTOTEST zero-opacity research; T2 incomplete; no game execution/install/commit",
           "builds": builds, "fixtures": fixtures,
           "readers": {"files": len(inventory), "failed": 0,
                       "rows": sum(len(row["rows"]) for row in inventory),
                       "non_pe_skipped": sum(row.get("skip") == "non-PE resource fork" for row in inventory)},
           "loadcheck": read(out / "loadcheck-dev.txt").strip()}
(out / "validation-summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(summary, ensure_ascii=False))

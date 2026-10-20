"""Metadata-only tile state audit. Does not launch or load game assemblies."""
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
GAME = Path(r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice")
OUT = ROOT / "docs/handoff/tilerange-evidence"
OUT.mkdir(exist_ok=True)
paths = set((GAME / "A Dance of Fire and Ice_Data/Managed").rglob("*.dll"))
paths.update((GAME / "Mods").rglob("*.dll"))
paths.add(ROOT / "bin/Debug/StutterFix.dll")
scanner = ROOT / "tools/ilscan/bin/Release/net8.0/ILScan.exe"
env = dict(os.environ, TILE_READERS="1")

def scan(path):
    with path.open("rb") as stream:
        if stream.read(2) != b"MZ":
            return {"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                    "exit": 0, "rows": [], "error": "", "skip": "non-PE resource fork"}
    try:
        result = subprocess.run([str(scanner), str(path), "ZZZ"], env=env,
                                capture_output=True, encoding="utf-8", errors="strict", timeout=15)
    except subprocess.TimeoutExpired:
        return {"path": str(path), "sha256": "", "exit": 124, "rows": [], "error": "metadata scan timeout (15s)"}
    return {"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "exit": result.returncode, "rows": [row for row in result.stdout.splitlines() if "\t" in row], "error": result.stderr[:2000]}

with concurrent.futures.ThreadPoolExecutor(max_workers=4) as workers:
    results = []
    with (OUT / "scan-progress.jsonl").open("w", encoding="utf-8") as progress:
        for result in workers.map(scan, sorted(paths)):
            results.append(result)
            progress.write(json.dumps({k: v for k, v in result.items() if k != "rows"}, ensure_ascii=False) + "\n")
            progress.flush()
(OUT / "readers-inventory.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
with (OUT / "readers.tsv").open("w", encoding="utf-8-sig") as stream:
    stream.write("assembly\tmethod\top\ttarget\tdecision\n")
    for result in results:
        for row in result["rows"]:
            target = row.split("\t")[-1]
            if target in {"scrFloor::opacity", "scrFloor::dontChangeMySprite", "scrFloor::floorRenderer"}:
                decision = "materialize-before-read-or-write"
            elif target == "scrFloor::moveTweens":
                decision = "materialize-and-fence-chunk-before-alias"
            else:
                decision = "eager-unchanged-zero-opacity-does-not-mutate-transform"
            stream.write(result["path"] + "\t" + row + "\t" + decision + "\n")
print(json.dumps({"dlls": len(results), "ok": sum(r["exit"] == 0 for r in results),
                  "failed": sum(r["exit"] != 0 for r in results),
                  "rows": sum(len(r["rows"]) for r in results)}))

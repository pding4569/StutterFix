"""Read-only metadata inventory; never loads a game/mod assembly into a runtime."""
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
GAME = Path(r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice")
OUT = ROOT / "docs/handoff/decoengine-evidence"
OUT.mkdir(exist_ok=True)
paths = set((GAME / "A Dance of Fire and Ice_Data/Managed").rglob("*.dll"))
paths.update((GAME / "Mods").rglob("*.dll"))
paths.add(ROOT / "bin/Debug/StutterFix.dll")
scanner = ROOT / "tools/ilscan/bin/Release/net8.0/ILScan.exe"
env = dict(os.environ, DECO_READERS="1")

def scan(path):
    result = subprocess.run([str(scanner), str(path), "ZZZ"], env=env,
                            capture_output=True, encoding="utf-8", errors="strict")
    return {
        "path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "exit": result.returncode, "rows": result.stdout.splitlines(),
        "error": result.stderr[:2000],
    }

with concurrent.futures.ThreadPoolExecutor(max_workers=4) as workers:
    results = list(workers.map(scan, sorted(paths)))
(OUT / "readers-inventory.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
with (OUT / "readers.tsv").open("w", encoding="utf-8-sig") as stream:
    stream.write("assembly\tmethod\top\ttarget\n")
    for result in results:
        for row in result["rows"]:
            stream.write(result["path"] + "\t" + row + "\n")
print(json.dumps({"dlls": len(results), "ok": sum(r["exit"] == 0 for r in results),
                  "failed": sum(r["exit"] != 0 for r in results),
                  "readers": sum(len(r["rows"]) for r in results)}))

"""Prepare real-map verification/pixel plans only. Intentionally has no run option."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path

WT = Path(__file__).resolve().parents[2]
ROOT = WT.parent
AT = {"hl": 50, "sh": 88, "ar": 262, "wf": 260, "s7": 120, "dd": 55}


def plan(key, mode):
    spec = importlib.util.spec_from_file_location("scriptpath_prepare_bench", ROOT / "bench_v14.py")
    b = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(b)
    cfg, commands = b.CFG2["rlNR"]
    at = AT[key]
    steps = ["timeout 900", "wait 2", "auto on", "fpscap 0",
             "open " + b.M + b.MAPS[key][0]] + list(commands)
    steps += ["phasewindow on", f"seek {at} 8", f"waitsongpos {at} 400"]
    if mode == "verify":
        steps += ["scriptskip exact verify", "wait 12", "scriptskip exact assert", "scriptskip off"]
    else:
        steps += ["pause", "wait 2", "framecmp filters on", "framecmp mask none",
                  "framecmp grab a", "framecmp grab a2", "framecmp assertzero a a2",
                  "scriptskip exact on", "wait 1", "scriptskip exact assert",
                  "framecmp grab b", "framecmp assertzero a b png",
                  "scriptskip exact off", "wait 1", "framecmp grab restored",
                  "framecmp assertzero a restored png", "framecmp result", "framecmp clear", "resume"]
    steps += ["phasewindow off", "quit"]
    return dict(steps=steps, settings=dict(b.CLEAN, **cfg), timeout_min=15, tag=f"scriptpath-{key}-{mode}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--maps", default="hl,sh,ar,wf,s7,dd")
    args = parser.parse_args()
    output = WT / "out/scriptpath/plans"
    output.mkdir(parents=True, exist_ok=True)
    for key in args.maps.split(","):
        if key not in AT:
            parser.error("unknown map " + key)
        for mode in ("verify", "pixels"):
            value = plan(key, mode)
            (output / (value["tag"] + ".json")).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    managed = Path(r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed")
    hashes = {name: hashlib.sha256((managed / name).read_bytes()).hexdigest().upper()
              for name in ("Assembly-CSharp.dll", "Rewired_Core.dll", "UnityEngine.UI.dll",
                           "Unity.TextMeshPro.dll", "UnityModManager/UnityModManager.dll")}
    (WT / "out/scriptpath/dll-hashes.json").write_text(json.dumps(hashes, indent=2), encoding="utf-8")
    print(f"Prepared verification/pixel JSONs in {output}; recorded read-only DLL hashes. No game/runner/install invoked.")


if __name__ == "__main__":
    main()

"""Read existing resolved stacks only; no game, DLL install or process control."""
import collections
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ENTRIES = ["scrFloor.Update", "scrConductor.Update", "Canvas:SendWillRenderCanvases",
           "scrFloor.LateUpdate", "scrVfxPlus.Update", "InputManager_Base:Update",
           "UnityModManager/UI:Update", "EventSystem:Update", "scrHUDText.Update",
           "PlanetSprite:LateUpdate", "SteamController:Update", "scnEditor:Update",
           "PerfOverlay:LateUpdate"]


def summarize(path):
    stacks = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        frames = [x[2:] if x[:2] in ("M:", "X:", "U:") else x
                  for x in line.split(";") if x]
        if frames:
            stacks.append(list(reversed(frames)))
    output = {"samples": len(stacks), "entries": {}}
    for entry in ENTRIES:
        sub = []
        for stack in stacks:
            # Exactly the engine entry, not the same method nested in another entry.
            invoke = next((i for i, x in enumerate(stack) if "runtime-invoke" in x), None)
            if invoke is None:
                continue
            tail = stack[invoke + 1:]
            if any(entry in x for x in tail[:1]):
                sub.append(tail)
        inclusive = collections.Counter()
        for stack in sub:
            inclusive.update(set(stack[1:]))
        pct = 100 * len(sub) / len(stacks)
        output["entries"][entry] = dict(count=len(sub), pct=pct,
            estimated_ms_at_4_35=pct * .0435,
            inside=[dict(frame=k, count=v, pct_all=100*v/len(stacks), pct_entry=100*v/len(sub))
                    for k, v in inclusive.most_common(14)])
    return output


if __name__ == "__main__":
    result = {key: summarize(ROOT.parent / "night-results" / f"nat-{key}.resolved.txt")
              for key in ("hl", "sh")}
    out = ROOT / "out/scriptpath"
    out.mkdir(parents=True, exist_ok=True)
    (out / "stacks.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    lines = []
    for key, value in result.items():
        lines.append(f"## {key}: {value['samples']}")
        for entry, data in value["entries"].items():
            lines.append(f"{entry}: {data['pct']:.3f}% / {data['estimated_ms_at_4_35']:.4f}ms (4.35ms normalization)")
            for row in data["inside"][:8]:
                lines.append(f"  {row['pct_all']:.3f}% all / {row['pct_entry']:.1f}% entry: {row['frame']}")
    (out / "stacks.txt").write_text("\n".join(lines), encoding="utf-8")
    print("\n".join(lines))

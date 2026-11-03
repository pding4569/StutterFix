"""Read-only, bounded Player/GPUDECO source inventory. Does not build or launch."""
from pathlib import Path
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SYMBOLS = {"GPUDECO"}


def expression(s):
    s = re.sub(r"\b[A-Za-z_]\w*\b", lambda m: str(m[0] in SYMBOLS), s)
    return bool(eval(s.replace("&&", " and ").replace("||", " or ").replace("!", " not ").strip(), {"__builtins__": {}}))


def active_lines(text):
    active, stack, result = True, [], []
    for line in text.splitlines():
        m = re.match(r"\s*#(if|elif|else|endif)\b(.*)", line)
        if m:
            op, arg = m.groups()
            if op == "if":
                yes = expression(arg)
                stack.append([active, yes])
                active = active and yes
            elif op == "elif":
                parent, taken = stack[-1]
                yes = expression(arg)
                active = parent and not taken and yes
                stack[-1][1] |= yes
            elif op == "else":
                parent, taken = stack[-1]
                active = parent and not taken
                stack[-1][1] = True
            else:
                active = stack.pop()[0]
            result.append("")
        else:
            result.append(line if active else "")
    assert not stack
    return result


def included(condition):
    if not condition:
        return True
    s = condition.replace("$(Edition)", "Player").replace("$(AutoTestBuild)", "0")
    s = re.sub(r"\bOr\b", "or", s)
    s = re.sub(r"\bAnd\b", "and", s)
    return bool(eval(s, {"__builtins__": {}}))


def main():
    project = ET.parse(ROOT / "StutterFix.csproj")
    compiled, excluded = {}, []
    for item in project.iter("Compile"):
        name = item.attrib["Include"]
        if included(item.get("Condition")):
            compiled[name] = active_lines((ROOT / name).read_text(encoding="utf-8-sig"))
        else:
            excluded.append(name)
    policy = (ROOT / "DrawCollapsePolicy.cs").read_text(encoding="utf-8-sig").split("internal static int MaxSpans=700;")[0] + "internal static int MaxSpans=700;"
    flags = []
    for match in re.finditer(r"internal static (bool|int|float)\s+([^;\n]+);", policy):
        if "(" in match[2]:
            continue
        for field in match[2].split(","):
            name, value = field.strip().split("=")
            refs, writers = [], []
            pattern = re.compile(r"\bDrawCollapsePolicy\." + name + r"\b")
            write = re.compile(r"\bDrawCollapsePolicy\." + name + r"\s*=(?!=)")
            for filename, lines in compiled.items():
                for number, line in enumerate(lines, 1):
                    line = line.split("//")[0]
                    if pattern.search(line) or filename == "DrawCollapsePolicy.cs" and re.search(r"\b" + name + r"\b", line):
                        refs.append(f"{filename}:{number}")
                    if write.search(line):
                        writers.append(f"{filename}:{number}")
            flags.append(dict(name=name, initial=value, product_writers=writers, references=refs))
    methods = {}
    for filename, names in {
        "GpuDecoDrawCollapseRenderer.cs": ["BuildBarrierOrderClones", "DispatchDrawCollapse", "ValidateDrawCollapse", "DynamicOrderTick", "ExcludedVisibilityTick", "BuildDrawCollapseSpans"],
        "GpuDecoRenderMerge.cs": ["PrepareGpuMerge", "DispatchGpuMerge", "GpuRenderMergeCommand"],
        "GpuDecoPackets.cs": ["CompileGpuPackets", "GpuPacketCommand", "TryGpuPacketRun"],
        "GpuDecoTrim.cs": ["TryTrimOwnedSprite"],
    }.items():
        source = (ROOT / filename).read_text(encoding="utf-8-sig")
        clean = re.sub(r'@?"(?:\\.|[^"\\])*"|//[^\n]*|/\*.*?\*/', lambda m: "\n" * m[0].count("\n"), source, flags=re.S)
        for name in names:
            m = re.search(r"^\s*(?:internal |private |public )?static [^\n=]+\b" + name + r"\([^\n]*\)\s*\{", clean, re.M)
            if not m:
                continue
            start = clean.index("{", m.start())
            depth, end = 1, start + 1
            while depth:
                depth += (clean[end] == "{") - (clean[end] == "}")
                end += 1
            first = clean.count("\n", 0, m.start()) + 1
            last = clean.count("\n", 0, end) + 1
            methods[f"{filename}:{name}"] = dict(first=first, last=last, physical_lines=last-first+1)
    report = dict(scope="Player without AUTOTEST; GPUDECO defined; source inventory, not compiler reachability proof",
                  flags=flags, excluded_files=excluded,
                  methods=methods,
                  files={name: dict(physical_lines=len((ROOT / name).read_text(encoding="utf-8-sig").splitlines()),
                                    player_nonblank_lines=sum(bool(x.strip()) for x in lines))
                         for name, lines in compiled.items() if name.startswith(("GpuDeco", "DrawCollapse"))})
    dest = ROOT / "docs/deco-spec/results/evidence/simplify"
    dest.mkdir(parents=True, exist_ok=True)
    (dest / "source-inventory.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    rows = ["| 필드 | Player 초기값 | Player 쓰기 지점 | 읽기/참조 행 수 |", "|---|---|---|---|"]
    for flag in flags:
        rows.append(f"| `{flag['name']}` | `{flag['initial']}` | {', '.join(flag['product_writers']) or '없음 (고정)'} | {len(flag['references'])} |")
    (dest / "policy-fields.md").write_text("\n".join(rows) + "\n", encoding="utf-8")
    print(f"Player source inventory: {len(flags)} fields, {len(compiled)} included, {len(excluded)} excluded; no game/build")


if __name__ == "__main__":
    main()

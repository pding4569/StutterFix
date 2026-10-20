"""Offline IL evidence for the finite writer manifest. Never invokes game code."""
import os
import re
import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / "docs/handoff/tilerange-evidence"
scanner = root / "tools/ilscan/bin/Release/net8.0/ILScan.exe"
env = {k: v for k, v in os.environ.items() if k not in {"METHOD", "TYPES", "TILE_WRITERS", "TILE_READERS", "DECO_GUARDS", "DECO_READERS"}}
env["RAW_IL"] = "*"
raw = subprocess.run([str(scanner), sys.argv[1], "ZZZ"], env=env, capture_output=True, encoding="utf-8", check=True).stdout
blocks = [b for b in re.split(r"(?m)(?=^\S)", raw) if b.strip()]
manifest = re.findall(r'new Spec\("([^"]+)","([^"]+)","([^"]+)",(\d+)\)', (root / "FloorRangeWriterManifest.cs").read_text(encoding="utf-8-sig"))
assert len(manifest) == 53
names = {t + "::" + m for t, m, _, _ in manifest}
fields = ["lastPos", "lastScale", "opacityLastFrame", "glowMultiplierLastFrame"]
readers = [b.splitlines()[0] + line for b in blocks for line in b.splitlines()[1:] if any("ldfld scrFloor::" + f in line for f in fields)]
assert all(row.startswith("scrFloor::LateUpdate ") for row in readers), readers
selected = [b for b in blocks if b.splitlines()[0] in names or b.startswith("scrFloor::Update\n") or any("ldfld scrFloor::" + f + "\n" in b for f in fields)]
(out / "t3b-game-raw-il.txt").write_text("".join(selected), encoding="utf-8")
(out / "t3b-bookkeeping-readers.txt").write_text("\n".join(readers) + "\n", encoding="utf-8")
table = ["| 대상 | IL 근거 (원본 오프셋) | 판단 |", "|---|---|---|"]
for t, m, _, mode in manifest:
    name = t + "::" + m
    b = next(b for b in blocks if b.splitlines()[0] == name)
    writes = []
    for line in b.splitlines()[1:]:
        if re.search(r"(?:stfld|ldflda) scrFloor::(?:opacity|startPos|startRot|tweenRot)$", line.strip()) or re.search(r"(?:call|callvirt) UnityEngine.Transform::(?:set_position|set_localPosition|set_eulerAngles|set_localEulerAngles|set_localScale|set_rotation|SetPositionAndRotation)$", line.strip()):
            writes.append(line.strip().replace("UnityEngine.Transform::", "").replace("scrFloor::", ""))
    evidence = "<br>".join(writes) or "직접 인증 필드/Transform 쓰기 없음"
    verdict = {"0": "실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음", "1": "수명/명시적 상태 변경 진입 표시 유지", "2": "맵 인증 폐기 유지", "3": "기존 효과 실행 전 인증 폐기 유지", "4": "현재/인접 타일 효과 진입 표시 유지", "5": "freeroam 소유 타일 표시 유지"}[mode]
    if mode == "0" and not writes:
        verdict = "변환할 직접 쓰기 없음; 기존 대상 유지"
    if name == "scrFloor::LateUpdate":
        evidence += "<br>장부 stfld: 035F/0375/0381/038D; 분기 0336 → 0353"
        verdict = "장부 4개 제외를 명시; isTweening 조건부 선 Transform 쓰기 유지"
    if name == "ThirdSun::Update":
        verdict = "0043은 vortex 시각 연출 회전(001F ldfld vortex); 장부 아님, 타일 참조 검사 유지"
    if name == "PuzzleRoom::Update":
        verdict = "0373 주소 다음 0378 ldfld Vector3.y는 읽기이나 기존 보수적 표시 유지; 장부 아님"
    table.append(f"| `{name}` | {evidence} | {verdict} |")
(out / "t3b-writer-audit.md").write_text("\n".join(table) + "\n", encoding="utf-8")
print(f"PASS audit targets={len(manifest)} bookkeeping-readers={len(readers)}; no game execution")

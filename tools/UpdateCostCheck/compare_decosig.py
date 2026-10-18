"""Exact text comparison of existing decosig outputs; no numeric tolerance.
This does not turn the decimal decosig format into full float-bit coverage.
"""
import argparse
from pathlib import Path
p = argparse.ArgumentParser()
p.add_argument("a", type=Path)
p.add_argument("b", type=Path)
a = p.parse_args()
x, y = a.a.read_text(encoding="utf-8-sig").splitlines(), a.b.read_text(encoding="utf-8-sig").splitlines()
n = max(len(x), len(y))
diffs = [i for i in range(n) if i >= len(x) or i >= len(y) or x[i] != y[i]]
print(f"rows_a={len(x)} rows_b={len(y)} different={len(diffs)}")
for i in diffs[:10]:
    print(f"row {i}: A={x[i] if i < len(x) else '<missing>'}")
    print(f"row {i}: B={y[i] if i < len(y) else '<missing>'}")
raise SystemExit(1 if diffs else 0)

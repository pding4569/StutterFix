"""Compare dumpbin output with the original checked-in DLL, preserving ordinals."""
import pathlib
import re
import sys


def exports(path):
    # PowerShell Set-Content may produce UTF-16 or the local Windows code page.
    raw = pathlib.Path(path).read_bytes()
    text = raw.decode('utf-16') if raw.startswith((b'\xff\xfe', b'\xfe\xff')) else raw.decode('utf-8', errors='replace')
    return {name: int(ordinal) for ordinal, name in re.findall(r'^\s+(\d+)\s+[0-9A-F]+\s+[0-9A-F]+\s+(sf_\S+)', text, re.M)}


before, after = map(exports, sys.argv[1:3])
assert len(before) == 60, before
assert len(after) == 65, after
assert all(after.get(name) == ordinal for name, ordinal in before.items())
assert sorted(after[name] for name in after.keys() - before.keys()) == list(range(61, 66))
print('PASS native exports: original 60 names/ordinals preserved; new 5 appended @61..65')

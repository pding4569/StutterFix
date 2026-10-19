"""Validate research artifact encoding and read-only map fingerprints."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
p = ROOT / 'docs/handoff/codex-decotrack-research-20261019-result.md'
b = p.read_bytes()
assert b[:3] == bytes([239, 187, 191]), b[:3]
s = b.decode('utf-8-sig', errors='strict')
assert '\ufffd' not in s
assert '\ufeff' not in s
assert '\uc0c8 \ud6a8\uacfc\uc640 \uc790\ub3d9 \ubcc0\ud658\uc740 \uc124\uacc4\ud560 \uc218 \uc788\ub2e4' in s
assert '\ud55c\uae00' in s
assert '\ubbf8\ud655\uc815' in s
print('BOM:', b[:3].hex(' '), 'bytes:', len(b), 'lines:', len(s.splitlines()),
      'Hangul:', sum('\uac00' <= c <= '\ud7a3' for c in s), 'replacement_chars:', s.count('\ufffd'))
for row in json.loads((Path(__file__).parent/'out/summary.json').read_text(encoding='utf-8')):
    assert hashlib.sha256(Path(row['path']).read_bytes()).hexdigest() == row['sha256']
    assert row['target_counts']['sum'] == row['deco_event_counts']['sum']
    assert sum(row['target_counts']['bins'].values()) == row['move']
    assert sum(row['deco_event_counts']['bins'].values()) == row['decorations']
    print(row['label'], 'source hash unchanged; distribution sums agree')

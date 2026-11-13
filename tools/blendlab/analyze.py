"""Produce compact, machine-readable mismatch tables from the independent fixture."""
import csv
import json
from pathlib import Path
root = Path(__file__).parent
data = (root / 'result.txt').read_bytes()
text = data.decode('utf-16' if data[:2] in (b'\xff\xfe', b'\xfe\xff') else 'utf-8-sig')
(root / 'result.txt').write_text(text, encoding='utf-8')
rows = []
for line in text.splitlines():
    if not line.startswith('formula '):
        continue
    row = dict(word.split('=', 1) for word in line.split()[1:])
    for channel in ('rgb', 'alpha'):
        index = int(row[channel + '_first'])
        row[channel + '_first_dst'] = index & 255 if index != 0xffffffff else ''
        row[channel + '_first_color'] = (index >> 8) & 255 if index != 0xffffffff else ''
        row[channel + '_first_alpha'] = ((index >> 16) & 255) / 255 if row['special'] == '0' and index != 0xffffffff else row['alpha'] if index != 0xffffffff else ''
    rows.append(row)
assert len(rows) == 200
with (root / 'mismatches.csv').open('w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)
summary = {}
for mode in range(5):
    chosen = [r for r in rows if int(r['mode']) == mode]
    summary[mode] = {'zero_mismatch_candidates_all_inputs': [c for c in range(10) if all(int(r['rgb_bad']) == 0 and int(r['alpha_bad']) == 0 for r in chosen if int(r['candidate']) == c)],
                     'float_even_exhaustive': next(r for r in chosen if r['candidate'] == '0' and r['special'] == '0'),
                     'q8_exhaustive': next(r for r in chosen if r['candidate'] == '6' and r['special'] == '0')}
repeat = [line for line in text.splitlines() if line.startswith('repeat ')]
assert len(repeat) == 15 and all('rgb_alpha_mismatches=0 ' in line for line in repeat)
summary['repeat'] = {'groups': len(repeat), 'k': [1, 255], 'pixels_per_group_per_k': 65536, 'transitions': len(repeat) * 65536 * 255, 'channel_comparisons_k2_to_255': len(repeat) * 65536 * 254 * 2, 'mismatches': 0, 'k1': 'measured LUT definition; not independent comparison'}
(root / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
print(json.dumps(summary, indent=2))

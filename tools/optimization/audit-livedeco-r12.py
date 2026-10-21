"""Read run5 evidence; do not execute/install games or invoke game methods."""
from decimal import Decimal
from hashlib import sha256
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
out = root / 'docs/handoff'
paths = [out / 'ga-autoloop-run5-evidence.txt', Path('E:/StutterFixWork/night-results/al-ga-5.log')]

def read(path):
    data = path.read_bytes()
    return data.decode('utf-16' if data.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')

def values(line):
    return dict(re.findall(r'(\w+)=([^ ]+)', line))

record = ['Existing run5 evidence only. No new game, installation, commit or push.',
          'Cost averages below use logged samples; observer/Harmony/native setter costs are not separately timed.']
for path in paths:
    record.append(f'path={path.as_posix()} sha256={sha256(path.read_bytes()).hexdigest()}')
lines = read(paths[1]).splitlines()
index = None
cost = None
rows = []
for n, line in enumerate(lines, 1):
    if '[livedeco2] window begin ' in line:
        index = int(values(line)['index'])
    if '[livedeco] cost ' in line:
        cost = (n, line, values(line))
    if '[livedeco] owned_ts ' in line and cost:
        ts = values(line)
        if index in (1, 2) and Decimal('263.5') <= Decimal(ts['t']) <= Decimal('274.5'):
            rows.append((index, cost[2], ts))
            record.extend([f'L{cost[0]}: {cost[1]}', f'L{n}: {line}'])
        cost = None
    if any(token in line for token in ('[livedeco2] PROFILE ', '[livedeco2] PIXEL ', '[livedeco2] FPS ', '[livedeco2] GATE2=')):
        record.append(f'L{n}: {line}')
    if any(token in line for token in ('[livedeco] begin variant=A', '[livedeco] vertexcheck ', '[livedeco] excluded reason=', '[livedeco] released reason=')):
        record.append(f'L{n}: {line}')
fps = [values(line) for line in lines if '[livedeco2] FPS ' in line]
assert [p['mode'] for p in fps] == ['off', 'on', 'on', 'off']
off = sum(Decimal(p['fps']) for p in fps if p['mode'] == 'off') / 2
on = sum(Decimal(p['fps']) for p in fps if p['mode'] == 'on') / 2
record.append(f'fps_arithmetic_means off={off} on={on} gain_pct={(on / off - 1) * 100}')
for window in (1, 2):
    selected = [(c, ts) for w, c, ts in rows if w == window]
    assert selected
    samples = sum(int(c['samples']) for c, _ in selected)
    record.append(f'window={window} bounded_report_intervals={len(selected)} samples={samples} ts_min={selected[0][1]["t"]} ts_max={selected[-1][1]["t"]}')
    for key in ('scan_ms', 'reconcile_ms', 'delta_ms', 'vertex_ms', 'upload_ms', 'audit_ms', 'upload_calls', 'dirty_images'):
        mean = sum(Decimal(c[key]) * int(c['samples']) for c, _ in selected) / samples
        record.append(f'window={window} weighted_{key}={mean:.9f}')
    for key in ('dirty', 'cache_hits', 'admissions', 'write_callbacks', 'watcher_visits', 'heap_delta'):
        mean = sum(Decimal(c[key]) for c, _ in selected) / samples
        record.append(f'window={window} {key}_per_frame={mean:.9f}')
    ratios = [Decimal(ts['owned']) / Decimal(ts['awake']) * 100 for _, ts in selected]
    record.append(f'window={window} owned_min={min(int(ts["owned"]) for _, ts in selected)} owned_max={max(int(ts["owned"]) for _, ts in selected)} coverage_min_pct={min(ratios):.6f} coverage_max_pct={max(ratios):.6f}')
    record.append(f'window={window} scan_min={min(Decimal(c["scan_ms"]) for c, _ in selected)} scan_max={max(Decimal(c["scan_ms"]) for c, _ in selected)}')
pixels = [values(line) for line in lines if '[livedeco2] PIXEL ' in line]
assert [p['t'] for p in pixels] == ['262', '270', '276']
(out / 'gate2-A-r12-prior-evidence.txt').write_text('\n'.join(record) + '\n', encoding='utf-8')
print('\n'.join(record[-40:]))

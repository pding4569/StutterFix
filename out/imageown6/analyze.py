import collections
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
raw_path = root.parent / 'night-results/al-im-5.log'
raw = raw_path.read_text(encoding='utf-8-sig').splitlines()
evidence = (root / 'docs/handoff/im-autoloop-run5-evidence.txt').read_text(encoding='utf-8-sig').splitlines()
direct = [s for s in evidence if s.startswith('[StutterFix]')]
cursor = 0
matched = []
for line in direct:
    found = next((i for i in range(cursor, len(raw)) if raw[i] == line or
                  (len(line) == 1500 and raw[i].startswith(line))), None)
    assert found is not None, line[:120]
    matched.append(found + 1)
    cursor = found + 1
print(f'run5 ordered direct evidence: {len(direct)} matched; raw lines {matched[0]}..{matched[-1]}')

def actual(token):
    return [(i + 1, s) for i, s in enumerate(raw) if token in s and '[자동 시험] 시작:' not in s]

groups = []
pattern = re.compile(r'size_format=(\d+x\d+)/(.+)/tier=(\d+) textures=(\d+) addressed=(\d+) ')
for n, line in actual('mode=collapsecoverage_group '):
    m = pattern.search(line)
    assert m, line
    size, fmt, tier, count, addressed = m.groups()
    groups.append(dict(size=size, format=fmt, tier=int(tier), textures=int(count), addressed=int(addressed), raw_line=n))
assert sum(g['textures'] for g in groups) == 93
assert sum(g['addressed'] for g in groups) == 89
assert sum(g['textures'] - g['addressed'] for g in groups) == 4
small = sum(g['textures'] for g in groups if g['tier'] > 0)
exact = next(g['textures'] for g in groups if g['size'] == '7680x4320' and g['format'] == 'RGBA_DXT5_UNorm')
assert small + exact + 36 == 93
assert exact == 4
formats = collections.defaultdict(lambda: [0, 0])
for g in groups:
    formats[g['format']][0] += g['textures']
    formats[g['format']][1] += g['addressed']
print(f'groups={len(groups)} textures=93 addressed=89 missing=4; small_array_textures={small} exact_array_textures={exact} direct_required=36')
print('formats=' + json.dumps(dict(formats), ensure_ascii=False, sort_keys=True))
for token in ('mode=collapsewindow ', 'DRAWCOLLAPSE_ARRAY_TOTAL ', 'mode=collapsecoverage_missing ', 'mode=collapsecoverage ', 'IMAGEOWN=FAIL', "단계 실패 'gpudeco live prepare'"):
    for n, line in actual(token):
        print(f'raw line {n}: {line}')
coverage = actual('mode=collapsecoverage ')
assert len(coverage) == 1 and 'registered=93 addressed=89 missing=4 missing_direct=4 missing_array=0 direct_required=36 direct_limit=32 arrays_used=4 array_limit=4' in coverage[0][1]
assert any('selected_quad_variants=10 skipped_quad_variants=93 registered=93' in s for _, s in actual('mode=collapsewindow '))
assert any('bytes_known=296026112 total_mb=282.312500 planned_arrays_known=4 direct_textures=32 active_texture_slots=36 shader_declared_slots=36' in s for _, s in actual('DRAWCOLLAPSE_ARRAY_TOTAL '))
for token in ('IMAGEOWN_WINDOW', 'IMAGEOWN_PIXEL', 'IMAGEOWN=PASS', 'on=True scope=song_union', 'originals_restored=True', 'native_contexts='):
    assert not actual(token), (token, actual(token))
print('live activation / integrated pixel / ABBA / restore / native-context results absent; NA')

env = os.environ.copy()
env['METHOD'] = 'scrVisualDecoration'
env.pop('TYPES', None)
game_dll = Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data/Managed/Assembly-CSharp.dll')
assert game_dll.is_file(), game_dll
scan = subprocess.run(['dotnet', str(root / 'tools/ilscan/bin/Release/net8.0/ILScan.dll'), str(game_dll), 'ZZZ'], env=env, capture_output=True)
(out / 'game-visual-il.txt').write_bytes(scan.stdout + scan.stderr)
assert scan.returncode == 0
print('read-only game ILScan: exit0; game_dll_sha256=' + hashlib.sha256(game_dll.read_bytes()).hexdigest())
(out / 'groups.json').write_text(json.dumps(groups, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

protected = list(root.glob('GpuDecoImage*.cs')) + [root / 'LazySprite.cs', root / 'GpuDecoDrawCollapse.cs', root / 'tools/optimization/imageown-steps.py']
protected += list((root / 'tools/optimization/out').glob('imageown*-steps.json'))
snapshot = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
snapshot_path = out / 'preserved.json'
if snapshot_path.exists():
    assert json.loads(snapshot_path.read_text(encoding='utf-8')) == snapshot
    print(f'image source/helper/generator/existing plans preserved: {len(snapshot)} SHA256 matched')
else:
    snapshot_path.write_text(json.dumps(snapshot, indent=2) + '\n', encoding='utf-8')
    print(f'image source/helper/generator/existing plans baseline: {len(snapshot)} SHA256 captured')
for p in (raw_path, root / 'docs/handoff/im-autoloop-run5-evidence.txt', root / 'DrawCollapsePolicy.cs', root / 'tools/gpudeco-lab/GpuDecoBatch.shader'):
    print(f'current_read_sha256 {p.name}={hashlib.sha256(p.read_bytes()).hexdigest()}')
print('current owner policy: ' + next(s.strip() for s in (root / 'DrawCollapsePolicy.cs').read_text(encoding='utf-8-sig').splitlines() if 'const int DirectBindings=' in s))

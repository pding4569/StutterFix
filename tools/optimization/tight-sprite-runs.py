"""Player paused-frame geometry gates; exclude captures from performance windows."""
from pathlib import Path
import json
import subprocess
import sys
import math
import hashlib

ROOT = Path('E:/StutterFixWork')
OUT = ROOT/'night-results'
key = sys.argv[1] if len(sys.argv) > 1 else 'dd'
mode = sys.argv[2] if len(sys.argv) > 2 else ''
maps = {'dd': ('DDONGSSADA3302 asansis remake/level.adofai', 52),
        'wf': ('Windflower/Windflower.adofai', 258),
        'hl': ('HELLO (BPM) 2026/level.adofai', 46),
        's7': ('7777/7777/level.adofai', 120),
        'sh': ('Exit from ship/Exit from SHiP_T2_.adofai', 88),
        'ar': (str(next(p for p in Path('D:/얼불춤 맵 파일/Arche').glob('*.adofai') if p.name.endswith(' FULL.adofai')).relative_to(Path('D:/얼불춤 맵 파일'))), 270)}
chart, at = maps[key]
manifest = ROOT/f'wt-partial/docs/deco-spec/results/evidence/tight-{key}.tsv'
metadata = json.loads(manifest.with_suffix('.json').read_text(encoding='utf-8'))
chart_path = Path('D:/얼불춤 맵 파일')/chart
assert chart_path.resolve() == Path(metadata['chart']).resolve()
assert hashlib.sha256(chart_path.read_bytes()).hexdigest() == metadata['chart_sha256']
for line in manifest.read_text(encoding='utf-8').splitlines()[1:]:
    cells = line.split('\t')
    assert len(cells) == 9, cells[:1]
    assert int(cells[1]) > 0 and int(cells[2]) > 0
    for column in (3, 5, 7):
        assert math.isfinite(float(cells[column]))
        if cells[column+1]:
            numbers = [float(x) for x in cells[column+1].split(',')]
            assert len(numbers) >= 6 and len(numbers) % 2 == 0 and all(map(math.isfinite, numbers))
steps = ['timeout 900', 'wait 2', 'auto on', 'fpscap 0', 'game D:/얼불춤 맵 파일/'+chart,
         'wait 4', 'coldopen stats', 'press', f'waitsongpos {at} 600', 'pause',
         'framecmp ui exclude', 'framecmp filters on', 'framecmp mask none',
         'framecmp noise N0 N1']
groups = [('', manifest, 'override', (4,6,8)), ('data', manifest, 'data', (4,6,8))]
if mode == 'diagnose':
    groups = [('clone', manifest, 'clone', (4,)),
              ('', manifest.with_name('tight-dd-pad4.tsv'), 'pad4', (4,)),
              ('', manifest.with_name('tight-dd-pad8.tsv'), 'pad8', (4,))]
for api, source, label, caps in groups:
    steps += ['framecmp clear', 'framecmp grab A'+label]
    for cap in caps:
        base, b, r = 'A'+label, f'B{label}{cap}', f'R{label}{cap}'
        steps += [f'tightsprite {api}{cap} {source}', f'framecmp grab {b}',
                  f'framecmp diff {base} {b} 0 png', 'tightsprite off',
                  f'framecmp grab {r}', f'framecmp diff {base} {r} 0 png']
steps += ['framecmp clear', 'resume', 'quit']
name = f'tight-{key}-v7-{mode or "pixel"}'
fixture = OUT/(name+'.json')
fixture.write_text(json.dumps(dict(steps=steps, settings={'GpuDeco': 0,
    'LowCompressImages': True, 'FrameGenOutside': 0, 'FrameGenRefresh': False},
    timeout_min=16, tag=name), ensure_ascii=False), encoding='utf-8')
subprocess.run([sys.executable, str(ROOT/'run_locked.py'), str(fixture), 'wt-partial', name,
                '--keys', 'tightsprite,framecmp,미리 푼 것,단계 실패,오류로', '--tail', '20'], cwd=ROOT, check=True)

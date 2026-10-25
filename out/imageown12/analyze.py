"""Offline run3 evidence verification; no game, installation, or automation calls."""
from pathlib import Path
import hashlib
import json
import os
import subprocess

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
raw_path = root.parent / 'night-results/al-im-3.log'
evidence_path = root / 'docs/handoff/im-autoloop-run3-evidence.txt'
raw = raw_path.read_text(encoding='utf-8-sig').splitlines()
direct = [s for s in evidence_path.read_text(encoding='utf-8-sig').splitlines() if s.startswith('[StutterFix]')]
cursor = 0
matches = []
for line in direct:
    i = next(i for i in range(cursor, len(raw)) if raw[i] == line or (len(line) == 1500 and raw[i].startswith(line)))
    matches.append(i + 1)
    cursor = i + 1
print(f'Ordered direct evidence: {len(matches)} lines; raw {matches[0]}..{matches[-1]}')
keys = ('mode=collapsecoverage ', 'mode=collapsecheck ', 'IMAGEOWN_PIXEL second=', 'IMAGEOWN=FAIL ', 'activation_ownership ', 'resumed_recs=', 'shutdown_disposed=', '[framecmp] diff O B ', '[framecmp] diff G N ', '[framecmp] diff G G2 ', '[framecmp] diff O O2 ', '[framecmp] diff B BN ')
selected = [(i+1, s) for i,s in enumerate(raw) if any(k in s for k in keys)]
for i,s in selected:
    print(f'{i}: {s}')
assert not any('IMAGEOWN_WINDOW ' in s for s in raw)
assert sum('IMAGEOWN_PIXEL second=' in s for s in raw) == 2
assert 'ImageOwnPixelMax=3;' in (root/'GpuDecoImageGeometryFixture.cs').read_text(encoding='utf-8-sig')

game_dll = Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data/Managed/Assembly-CSharp.dll')
env = os.environ.copy()
env['METHOD'] = 'scrVisualDecoration'
env.pop('TYPES', None)
scan = subprocess.run(['dotnet', str(root/'tools/ilscan/bin/Release/net8.0/ILScan.dll'), str(game_dll), 'ZZZ'], env=env, capture_output=True)
assert scan.returncode == 0
(out/'game-scrVisualDecoration-il.txt').write_bytes(scan.stdout+scan.stderr)
decompile = Path('E:/StutterFixWork/decompiled/Assembly-CSharp/scrVisualDecoration.cs').read_text(encoding='utf-8-sig')
assert 'spriteRenderer.material.SetPointSamplingVariant(!smoothing);' in decompile
assert 'spriteRenderer.sprite.texture.wrapMode = TextureWrapMode.Clamp;' in decompile
assert 'set_wrapMode' in scan.stdout.decode('utf-8-sig') and 'SetPointSamplingVariant' in scan.stdout.decode('utf-8-sig')
print('Read-only game decompile / current DLL ILScan sampling and wrap calls agree.')

paths = list(root.glob('*.cs')) + [root/'StutterFix.csproj',root/'tools/optimization/imageown-steps.py',root/'tools/gpudeco-lab/GpuDecoBatch.shader',root/'tools/gpudeco-lab/GpuDeco.compute',root/'tools/gpudeco-lab/evidence/drawcollapse5/bundle/stutterfix_gpudeco',root/'native/sfnative.dll',root/'docs/handoff/codex-al-im-4.md',evidence_path,raw_path,game_dll]
paths += list((root/'tools/optimization/out').glob('imageown*-steps.json'))
paths += [p for p in (root/'.codex/config.toml',root.parent/'nogame.flag',root.parent/'autoloop.stop') if p.exists()]
snapshot = {str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
baseline = out/'preserved.json'
if baseline.exists():
    assert snapshot == json.loads(baseline.read_text(encoding='utf-8')), 'protected file changed'
    (out/'preserved-after.json').write_text(json.dumps(snapshot,indent=2)+'\n',encoding='utf-8')
else:
    baseline.write_text(json.dumps(snapshot,indent=2)+'\n',encoding='utf-8')
    for name in ('codex-imageown-result.md','codex-imageown-summary.txt','codex-master-20261022.md','im-autoloop-ledger.md','imageown-autoloop-ledger.md'):
        (out/('before-'+name)).write_bytes((root/'docs/handoff'/name).read_bytes())
print(f'Protected baseline verified: {len(snapshot)} files. No shared PNG used or attributed to run3.')

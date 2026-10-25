import hashlib
import json
import os
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
raw_path = root.parent / 'night-results/al-im-6.log'
evidence_path = root / 'docs/handoff/im-autoloop-run6-evidence.txt'
raw = raw_path.read_text(encoding='utf-8-sig').splitlines()
direct = [s for s in evidence_path.read_text(encoding='utf-8-sig').splitlines() if s.startswith('[StutterFix]')]
cursor = 0
matched = []
for line in direct:
    found = next((i for i in range(cursor, len(raw)) if raw[i] == line or
                  (len(line) == 1500 and raw[i].startswith(line))), None)
    assert found is not None, line[:120]
    matched.append(found + 1)
    cursor = found + 1
print(f'run6 direct evidence ordered: {len(direct)}; raw lines {matched[0]}..{matched[-1]}')

def actual(token):
    return [(i + 1, s) for i, s in enumerate(raw) if token in s and i + 1 != matched[0]]

expected = {
    'mode=collapsewindow ': 'selected_quad_variants=10 skipped_quad_variants=93 registered=93',
    'DRAWCOLLAPSE_ARRAY_TOTAL ': 'bytes_known=304414720 total_mb=290.312500 planned_arrays_known=4 direct_textures=34 active_texture_slots=38 shader_declared_slots=52',
    'mode=collapsecoverage ': 'registered=93 addressed=93 missing=0 missing_direct=0 missing_array=0 direct_required=34 direct_limit=48 arrays_used=4 array_limit=4 variant_entries_missing=0 valid=True',
    'DRAWCOLLAPSE_PREPARED ': 'spans=82 included=14595 arrays=4 bytes=304414720 copy_calls=59 texture_fallback_items=0',
    'activation_ownership ': 'allocated=28523 GPU_owned=28523 CPU_fallback=0',
    'activation_rollback ': 'restored_recs=11821 saved_recs=11821 CPU_fallback=0 continuity=RESTORED',
    'shutdown_disposed=': 'shutdown_disposed=True native_contexts=0',
}
for token, value in expected.items():
    found = actual(token)
    assert len(found) == 1 and value in found[0][1], (token, found)
    print(f'raw line {found[0][0]}: {found[0][1]}')
for token in ('image collapse changed mip/wrap', 'diff O O2 threshold=0'):
    found = actual(token)
    assert len(found) == 2, (token, found)
    for n, line in found:
        print(f'raw line {n}: {line}')
for token in ('IMAGEOWN_WINDOW', 'IMAGEOWN_PIXEL', 'IMAGEOWN=PASS', 'on=True scope=song_union', 'mode=collapsecoverage_group ', 'mode=collapse_runtime_missing '):
    assert not actual(token), (token, actual(token))
print('handoff budget: direct_required34 > direct_limit32; total_required38 > total_limit36; external limit48 is distinct')
print('integrated pixels/FPS/spikes/full-song/installation restore: NA; failed texture ID/mip/wrap values: absent')

game_dll = Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data/Managed/Assembly-CSharp.dll')
assert game_dll.is_file()
for name in ('scrVisualDecoration', 'scrDecorationManager', 'TextureManager'):
    env = os.environ.copy()
    env['METHOD'] = name
    env.pop('TYPES', None)
    scan = subprocess.run(['dotnet', str(root / 'tools/ilscan/bin/Release/net8.0/ILScan.dll'), str(game_dll), 'ZZZ'], env=env, capture_output=True)
    (out / f'game-{name}-il.txt').write_bytes(scan.stdout + scan.stderr)
    assert scan.returncode == 0
print('read-only installed game ILScan: 3 types exit0; dll_sha256=' + hashlib.sha256(game_dll.read_bytes()).hexdigest())

protected = list(root.glob('*.cs')) + [root / 'StutterFix.csproj', root / 'tools/optimization/imageown-steps.py',
    root / 'tools/gpudeco-lab/GpuDecoBatch.shader', root / 'tools/gpudeco-lab/GpuDeco.compute',
    root / 'tools/gpudeco-lab/evidence/drawcollapse5/bundle/stutterfix_gpudeco', root / 'native/sfnative.dll',
    root / 'docs/handoff/codex-al-im-7.md', evidence_path, raw_path]
protected += list((root / 'tools/optimization/out').glob('imageown*-steps.json'))
protected += [p for p in (root / '.codex/config.toml', root.parent / 'nogame.flag', root.parent / 'autoloop.stop') if p.exists()]
snapshot = {os.path.relpath(p, root): hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
snapshot_path = out / 'preserved.json'
if snapshot_path.exists():
    assert json.loads(snapshot_path.read_text(encoding='utf-8')) == snapshot
    print(f'protected source/assets/plans/instruction/raw: {len(snapshot)} SHA256 unchanged')
else:
    snapshot_path.write_text(json.dumps(snapshot, indent=2) + '\n', encoding='utf-8')
    print(f'protected source/assets/plans/instruction/raw: {len(snapshot)} SHA256 baseline captured')

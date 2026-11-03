"""Offline evidence collection; never starts or modifies the game."""
import collections
import hashlib
import json
import re
import shutil
import uuid
from pathlib import Path

ROOT = Path('E:/StutterFixWork')
WT = ROOT / 'wt-partial'
OUT = ROOT / 'night-results'
DEST = WT / 'docs/deco-spec/results/evidence/engine-extreme'
DEST.mkdir(parents=True, exist_ok=True)

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

summary = {'runs': {}, 'native': {}}
invalid = {'engine-phase-dd-deco-image', 'engine-phase-dd-deco-particle', 'engine-phase-dd-all'}
for path in sorted((WT / 'docs/deco-spec/results/engine-steps').glob('*.json')):
    name = path.stem
    logpath = OUT / (name + '.log')
    log = logpath.read_text(encoding='utf-8', errors='replace')
    data = json.loads((OUT / (name + '.integrity.json')).read_text(encoding='utf-8'))
    assert all(data[k] for k in ('restored_dll', 'probe_absent', 'gpu_default_zero', 'completed'))
    assert not data['step_failure']
    data.update(spec_sha256=sha(path), raw_log=str(logpath), raw_log_sha256=sha(logpath),
                valid_ablation=name not in invalid,
                exclusion_reason='gpuprobe on cleared the earlier gpusplit hide' if name in invalid else None)
    if name == 'engine-phase-dd-all-v2':
        assert data['split_retained']
    fps = re.search(r'\[FPS 구간\] engine : ([\d.]+) FPS', log)
    if fps:
        data['diagnostic_fps'] = float(fps[1])
        data['diagnostic_frame_ms_from_rounded_fps'] = 1000 / float(fps[1])
    summary['runs'][name] = data
    excerpt = [line for line in log.splitlines() if any(s in line for s in
        ('phase_window', '[gpuprobe]', '[FPS 구간]', '[gpusplit] state=',
         '[gpusplit] hide=', '[gpusplit] restored=', '[profsamp]', 'sampling_error='))]
    (DEST / (name + '.txt')).write_text('\n'.join(excerpt) + '\n', encoding='utf-8')

for key in ('dd', 'exit'):
    name = 'engine-native-' + key
    frames = OUT / (name + '.frames.txt')
    resolved = OUT / (name + '.resolved.txt')
    lines = resolved.read_text(encoding='utf-8').splitlines()
    raw_lines = frames.read_text(encoding='utf-8-sig').splitlines()
    assert len(lines) == len(raw_lines) > 10000
    wait = collections.Counter(s for s in lines if s.startswith('[ntdll.dll+0x9E0F4]'))
    copies = collections.Counter(s for s in lines if s.startswith('[Unity] memcpy_repmovs;'))
    copy_callers = collections.Counter()
    for stack, count in copies.items():
        # Keep the native copy caller chain through the render queue.
        copy_callers[';'.join(stack.split(';')[:8])] += count
    data = {'samples': len(lines), 'frames_sha256': sha(frames), 'resolved_sha256': sha(resolved),
            'wait_leaf_samples': sum(wait.values()),
            'wait_stacks': wait.most_common(8),
            'memcpy_repmovs_samples': sum(copies.values()),
            'memcpy_callers': copy_callers.most_common(12)}
    summary['native'][key] = data
    (DEST / (name + '-callers.json')).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    shutil.copyfile(OUT / (name + '.sym.txt'), DEST / (name + '.sym.txt'))

game = Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice')
unity = game / 'UnityPlayer.dll'
rsds = []
for match in re.finditer(rb'RSDS(.{16})(.{4})([^\x00\r\n]{1,300}\.pdb)\x00', unity.read_bytes(), re.DOTALL):
    rsds.append({'guid': str(uuid.UUID(bytes_le=match[1])), 'age': int.from_bytes(match[2], 'little'),
                 'pdb': match[3].decode('utf-8', errors='replace')})
pdb = ROOT / 'symbols/UnityPlayer_Win64_player_mono_x64.pdb/0BD6084081424C169079D97BFE0214691/UnityPlayer_Win64_player_mono_x64.pdb'
summary['symbols'] = {'unity_sha256': sha(unity), 'unity_rsds': rsds, 'pdb': str(pdb), 'pdb_sha256': sha(pdb)}
assert any(r['guid'].replace('-', '').upper() == '0BD6084081424C169079D97BFE021469' and r['age'] == 1 for r in rsds), rsds
mod = game / 'Mods/StutterFix'
summary['final_integrity'] = {'restored_dll': sha(mod/'StutterFix.dll') == sha(ROOT/'restore/StutterFix-rc-player.dll'),
                            'probe_absent': not (mod/'gpuprobe.dll').exists(),
                            'gpu_default_zero': bool(re.search(r'<GpuDeco>0</GpuDeco>', (mod/'Settings.xml').read_text(encoding='utf-8-sig')))}
assert all(summary['final_integrity'].values())
for name in ('engine-player-build.txt', 'engine-final-build.txt', 'engine-sym-build.txt', 'engine-player-profscan.txt'):
    text = (OUT/name).read_text(encoding='utf-8-sig', errors='replace')
    (DEST/name).write_text(text, encoding='utf-8')
(DEST/'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'native': summary['native'], 'symbols': summary['symbols']['unity_rsds'],
                  'integrity': summary['final_integrity']}, ensure_ascii=False, indent=2))

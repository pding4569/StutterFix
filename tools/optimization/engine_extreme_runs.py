"""Fresh-process engine diagnostics. Game execution only through run_locked.py."""
import hashlib, json, re, shutil, subprocess, sys
from pathlib import Path

ROOT = Path('E:/StutterFixWork')
WT = ROOT / 'wt-partial'
OUT = ROOT / 'night-results'
SPECS = WT / 'docs/deco-spec/results/engine-steps'
MOD = Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix')
MAPS = {'dd': ('DDONGSSADA3302 asansis remake/level.adofai', 50),
        'hello': ('HELLO (BPM) 2026/level.adofai', 46),
        'exit': ('Exit from ship/Exit from SHiP_T2_.adofai', 88)}

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def run(key, group=None):
    chart, start = MAPS[key]
    profile = group == 'native'
    if profile:
        group = None
    name = 'engine-phase-' + key + ('-' + group + '-v2' if group else '')
    if profile:
        name = 'engine-native-' + key
    SPECS.mkdir(parents=True, exist_ok=True)
    spec = {'tag': name, 'timeout_min': 15,
            'settings': {'GpuDeco': 0, 'FrameGenOutside': 0, 'FrameGenRefresh': False},
            'steps': ['timeout 900', 'wait 2', 'auto on', 'fpscap 0',
                      'game D:/얼불춤 맵 파일/' + chart, 'press',
                      f'waitsongpos {start-3} 400', 'phasewindow on', 'gpuprobe on',
                      f'waitsongpos {start} 60', 'gpuprobe reset', 'phasewindow begin',
                      'fpsmark start', f'waitsongpos {start+15} 60',
                      'fpsmark end engine', 'phasewindow end', 'gpuprobe state',
                      'gpuprobe off', 'phasewindow off', 'quit']}
    if profile:
        spec['steps'] = spec['steps'][:6] + [f'waitsongpos {start} 400', 'profsamp begin 1000 native',
                          'wait 15', 'profsamp end frames', 'wait 1', 'quit']
    if group:
        at = spec['steps'].index('gpuprobe on') + 1
        spec['steps'][at:at] = ['gpusplit hide ' + group, 'gpusplit state']
        spec['steps'].insert(spec['steps'].index('gpuprobe state'), 'gpusplit state')
        spec['steps'].insert(-1, 'gpusplit off')
    path = SPECS / (name + '.json')
    path.write_text(json.dumps(spec, ensure_ascii=False, indent=2), encoding='utf-8')
    evidence = {'dll_sha256': sha(WT / 'bin/PlayerAuto/StutterFix.dll')}
    result = subprocess.run([sys.executable, str(ROOT/'run_locked.py'), str(path),
                             'wt-partial', name, '--probe', '--keys',
                             'phase_window,[gpuprobe],[gpusplit],[profsamp],native_unwind,FPS 구간,단계 실패,오류로', '--tail', '20'],
                            cwd=ROOT, capture_output=True, text=True, encoding='utf-8', errors='replace')
    print(result.stdout, flush=True)
    print(result.stderr[-500:], flush=True)
    log = (OUT/(name+'.log')).read_text(encoding='utf-8', errors='replace')
    evidence.update(returncode=result.returncode,
                    restored_dll=sha(MOD/'StutterFix.dll') == sha(ROOT/'restore/StutterFix-rc-player.dll'),
                    probe_absent=not (MOD/'gpuprobe.dll').exists(),
                    gpu_default_zero=bool(re.search(r'<GpuDeco>0</GpuDeco>', (MOD/'Settings.xml').read_text(encoding='utf-8-sig'))),
                    step_failure='단계 실패' in log or '오류로 그만둠' in log,
                    completed=('native_unwind=True native_frames=' in log if profile else
                               'phase_window end frames=' in log and '[gpuprobe] ready=1' in log))
    if profile:
        frames = OUT / (name + '.frames.txt')
        source = MOD / 'profsamp-frames.txt'
        # Never accept a frame file retained from a previous process.
        assert source.stat().st_mtime >= path.stat().st_mtime, 'stale sample file'
        shutil.copyfile(source, frames)
        evidence['frames_sha256'] = sha(frames)
    if group:
        state = re.findall(r'\[gpusplit\] state=(\S+) selected=(\d+)', log)
        evidence['split_retained'] = len(state) >= 2 and all(s == group and int(n) > 0 for s, n in state)
    (OUT/(name+'.integrity.json')).write_text(json.dumps(evidence, indent=2), encoding='utf-8')
    assert result.returncode == 0 and evidence['completed'] and not evidence['step_failure'], evidence
    assert all(evidence[k] for k in ('restored_dll', 'probe_absent', 'gpu_default_zero')), evidence
    if group:
        assert evidence['split_retained'], evidence

for arg in sys.argv[1:] or ['dd', 'hello', 'exit']:
    key, _, group = arg.partition(':')
    run(key, group or None)

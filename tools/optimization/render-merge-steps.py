"""render-merge 한 프로세스 시험 계획만 생성한다. 실행/설치 기능 없음."""
import argparse
import json
from pathlib import Path


def build(chart, bundle=None, mono_dir=None):
    root = Path(__file__).resolve().parents[2]
    bundle = bundle or root / 'tools/gpudeco-lab/evidence/bundle/stutterfix_gpudeco'
    steps = ['timeout 600', 'auto on', 'decobake on', 'gpudeco off',
             'gpudeco reset', 'gpudeco costprobe off', 'gpudeco effectcache off',
             'gpudeco nombp off', 'gpudeco rendermerge reset',
             'gpudeco rendermerge environment',
             'gpudeco nativebatch on', 'gpudeco nativecopy on8masked',
             'gpudeco zeroalpha on', 'gpudeco bundle ' + str(bundle),
             'open ' + str(chart), 'gpuprobe off', 'gpusplit off',
             'matshare off', 'livedeco off',
             # Never naturally play from zero, including initial preparation.
             'seek 260 12', 'gpudeco scheduled',
             'gpudeco rendermerge playbackready', 'waitsongpos 250 40',
             'pause', 'decobake ready',
             'gpudeco live scope song', 'gpudeco rendermerge prepare',
             'gpudeco live prepare', 'resume', 'stop']

    def enter(target=260, merged=False, ready=False):
        # Both modes freeze setup at the same boundary. The measured window
        # starts only after resuming and warming for at least four song seconds.
        out = ['gpudeco live off', 'gpudeco rendermerge off', 'stop',
               f'seek {target} 12', 'gpudeco rendermerge playbackready', 'decobake ready',
               f'waitsongpos {target - 4} 40', 'pause',
               'gpudeco packets compiled', 'gpudeco live on']
        if ready or merged:
            out += ['gpudeco rendermerge ready']
        out += ['gpudeco rendermerge ' + ('on' if merged else 'off'), 'resume']
        return out

    # A=independent GPU, B=merged GPU. Always the same seek/window/settings.
    for i, merged in enumerate((False, True, True, False)):
        steps += enter(merged=merged)
        steps += ['waitsongpos 260 40', 'fuse begin', 'gpudeco micro begin',
                  f'gpudeco rendermerge begin {i}', 'waitsongpos 270 30',
                  'gpudeco rendermerge end', 'gpudeco micro end', 'fuse end']

    # Pixels use a fresh seek after every long capture, avoiding the portal
    # callback previously observed after resuming a long paused capture.
    for i, target in enumerate((262, 270)):
        steps += enter(ready=True)
        steps += [f'waitsongpos {target} 40', 'pause', 'gpudeco live off',
                  'framecmp clear', 'framecmp mask none', 'framecmp filters on',
                  'framecmp ui exclude', 'framecmp grab O',
                  'gpudeco live on', 'gpudeco rendermerge ready',
                  'framecmp noise A N', 'gpudeco rendermerge on',
                  'framecmp grab B', 'gpudeco rendermerge off',
                  'framecmp grab A2', f'gpudeco rendermerge pixel {i}']
        steps += [f'framecmp save {name}' for name in ('O', 'A', 'B', 'A2')]
        steps += ['framecmp clear', 'gpudeco live off', 'resume', 'stop']

    # Actual retry, map reopen, and the same DecoBake.Shutdown path used by
    # module unload. Resource IDs include merged meshes and game objects.
    steps += enter(merged=True)
    steps += ['waitsongpos 260 40', 'gpudeco rendermerge watch retry',
              'retry', 'wait 2', 'gpudeco rendermerge stability retry',
              'stop']
    steps += enter(target=300, merged=True)
    steps += ['waitsongpos 300 40', 'gpudeco rendermerge watch song_end',
              'waitend 40', 'gpudeco rendermerge stability song_end',
              'gpudeco rendermerge off', 'stop']
    steps += enter(merged=True)
    steps += ['waitsongpos 260 40', 'gpudeco rendermerge watch reopen',
              'pause', 'gpudeco resources mark', 'resume', 'stop',
              'open ' + str(chart), 'seek 260 12', 'gpudeco scheduled',
              'gpudeco rendermerge playbackready', 'waitsongpos 250 40',
              'pause', 'decobake ready', 'gpudeco rendermerge stability reopen',
              'resume', 'stop', 'wait 2', 'gpudeco resources clean',
              'gpudeco live scope song', 'gpudeco rendermerge prepare',
              'seek 260 12', 'gpudeco scheduled',
              'gpudeco rendermerge playbackready', 'waitsongpos 250 40', 'pause', 'decobake ready',
              'gpudeco live prepare', 'resume', 'stop']
    steps += enter(merged=True)
    steps += ['pause', 'gpudeco resources mark', 'decobake off', 'wait 2',
              'gpudeco resources clean', 'gpudeco rendermerge stability unload',
              'resume', 'stop']
    # Optional T2: external controller extracts addr.txt from this trace while
    # the process waits in monosyms, then does the named ETW pass offline.
    if mono_dir:
        steps += ['decobake on', 'seek 260 12', 'gpudeco scheduled',
                  'gpudeco rendermerge playbackready', 'waitsongpos 250 40', 'pause',
                  'decobake ready', 'gpudeco live scope song',
                  'gpudeco rendermerge prepare', 'gpudeco live prepare',
                  'resume', 'stop']
        steps += enter(merged=True)
        steps += ['waitsongpos 260 40', 'trace rendermerge-managed 8',
                  'fuse begin', 'wait 8', 'fuse end',
                  'gpudeco live off', 'gpudeco rendermerge off', 'stop',
                  f'monosyms "{mono_dir / "rendermerge-addr.txt"}" "{mono_dir / "rendermerge-syms.txt"}"']
    steps += ['gpudeco rendermerge result', 'quit']
    return dict(steps=steps, settings={'FrameGenOutside': 0, 'FrameGenRefresh': False},
                timeout_min=25, checks={}, tag='rendermerge-260-270-abba-startsync2',
                notes={'A': 'independent GPU', 'B': 'merged GPU',
                       'environment': 'requires existing 2560x1440@120; checks only, never changes display settings',
                       'start_sync': 'wait for hasSongStarted after every seek; PrepVfx is not music-start completion',
                       'lifetime': 'actual retry/reopen/portal; DecoBake.Shutdown and marked Unity/native resources',
                       'full_umm_unload': 'UNMEASURED',
                       'mono': 'same-process external address extraction required' if mono_dir else 'prior named trace preserved; no repeat'})


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--chart', type=Path, required=True)
    parser.add_argument('--bundle', type=Path)
    parser.add_argument('--mono-dir', type=Path, help='새 주소 파일을 외부 장치가 제공하는 폴더; 과거 프로세스 파일 재사용 금지')
    parser.add_argument('--output', '-o', type=Path, required=True)
    args = parser.parse_args()
    for path in (args.chart, args.bundle, args.mono_dir):
        if path is not None and any(c in str(path) for c in '\r\n'):
            parser.error('경로에 줄바꿈을 쓸 수 없음')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(build(args.chart, args.bundle, args.mono_dir), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

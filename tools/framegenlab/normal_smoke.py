"""Validate the ordinary Player+AutoTest build, without research instrumentation."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
from measure_maps import sf, ROOT


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--map', type=Path, required=True)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--mode', type=int, choices=[0, *range(2, 9)], default=0)
    p.add_argument('--refresh', action='store_true', help='General match-refresh mode, including zero-generation above refresh')
    p.add_argument('--refresh-rest', action='store_true', help='Explicit default-OFF snapshot-rest candidate, requires refresh')
    p.add_argument('--full-song', action='store_true')
    p.add_argument('--switch', action='store_true')
    p.add_argument('--monitor-smoke', action='store_true', help='Verify real/output/both counters and capture all three monitor layouts')
    p.add_argument('--settings', type=Path, help='Temporary compatibility settings JSON')
    p.add_argument('--no-shot', action='store_true', help='Submission/source sample without a later screenshot')
    p.add_argument('--fx-state', action='store_true', help='Explicit endpoint-only FX readiness counters; no per-frame instrumentation')
    p.add_argument('--steam-exe', type=Path, help='Use Steam -applaunch instead of the URL handler; keeps saved launch options')
    a = p.parse_args()
    if a.steam_exe:
        if not a.steam_exe.is_file() or a.steam_exe.name.lower()!='steam.exe':p.error('Existing Steam executable required')
        sf.launch_game=lambda: subprocess.Popen([str(a.steam_exe),'-applaunch','977950'])
    if a.refresh_rest and not a.refresh:p.error('--refresh-rest requires --refresh')
    if (Path(__file__).resolve().parent/'out/stop-outside-batch').exists():
        raise RuntimeError('Batch deliberately stopped before installation; preserve current evidence')
    if not a.map.is_file() or sf.game_running():
        p.error('Existing map and closed game required')
    if a.switch and (a.mode or a.full_song):
        p.error('Switch test starts OFF and is separate from full-song tests')
    if a.monitor_smoke and (not a.mode or a.switch or a.full_song):
        p.error('Monitor test uses an active multiplier and is separate from switch/full-song tests')
    extra = json.loads(a.settings.read_text(encoding='utf8')) if a.settings else {}
    if not isinstance(extra, dict) or any(k.startswith('FrameGen') for k in extra):
        p.error('Compatibility settings must be an object without FrameGen controls')
    mod = Path(sf.MOD_DIR)
    diagnostic = mod/'framegen-outside'
    if diagnostic.exists():
        raise RuntimeError('Preserve prior research output first')
    a.out.mkdir(parents=True, exist_ok=False)
    original = {n: (mod/n).read_bytes() for n in ['StutterFix.dll', 'sfnative.dll', 'Settings.xml']}
    for n, data in original.items():
        (a.out/(n+'.original')).write_bytes(data)
    shot = mod/'shots'/'framegen-normal.png'
    old_shot = shot.read_bytes() if shot.exists() else None
    monitor_shots = {mod/'shots'/('framegen-monitor-'+n+'.png'): None for n in ['icon','mini','detail','settings','graphics']}
    if a.monitor_smoke:
        monitor_shots = {p:p.read_bytes() if p.exists() else None for p in monitor_shots}
    binary = (ROOT/'bin/PlayerAuto/StutterFix.dll').read_bytes()
    try:
        (mod/'StutterFix.dll').write_bytes(binary)
        steps = ['wait 3', 'fgstate', 'game '+str(a.map), 'auto on', 'press', 'wait 10', 'fgstate']
        if a.fx_state:steps += ['fxstate']
        if a.monitor_smoke:
            steps += ['fgmonitor'] # Missing setting uses Auto by default; do not set it before this sample.
            for source in [0,1,2,-1]:
                steps += ['set OverlayFpsSource '+str(source), 'wait 3', 'fgmonitor']
            for layout,name in [(1,'icon'),(2,'mini'),(3,'detail')]:
                steps += ['set OverlayMode '+str(layout), 'wait 2', 'shot framegen-monitor-'+name, 'wait 1']
            steps += ['ui 4.1', 'wait 2', 'shot framegen-monitor-settings', 'wait 1',
                      'ui 3', 'wait 2', 'shot framegen-monitor-graphics', 'wait 1', 'ui close', 'fgstate',
                      'set FrameGenOutside 0','wait 3','fgstate','fgmonitor']
        elif a.switch:
            for mode in [2, 4, 3, 5, 6, 7, 8, 1, 9, 0, 2]:
                steps += ['set FrameGenOutside '+str(mode), 'wait 3', 'fgstate']
            steps += ['set LowHalfRender true', 'wait 3', 'fgstate',
                      'set LowHalfRender false', 'wait 3', 'fgstate',
                      'set FrameGenOutside 0', 'wait 3', 'fgstate']
        else:
            steps += ['wait 35', 'fgstate']
            if a.fx_state:steps += ['fxstate']
            if not a.no_shot:
                steps += ['wait 5', 'shot framegen-normal', 'wait 3']
            if a.full_song:
                steps += ['waitend 600', 'wait 3', 'fgstate']
            steps += ['set FrameGenOutside 0', 'wait 3', 'fgstate']
            if not a.mode:
                steps += ['fgmonitor']
        steps += ['quit']
        summary, metrics = sf.sf_run(steps, settings={**extra, 'FrameGenOutside': a.mode, 'FrameGenRefresh': a.refresh, 'FrameGenRefreshRest':a.refresh_rest, 'FrameStats': False, 'LowHalfRender': False},
                                    timeout_min=12 if a.full_song else 4, tag='framegen-normal')
        log = sf.read_text(sf.PLAYER_LOG)
        (a.out/'game.log').write_text(log, encoding='utf8')
        (a.out/'run.txt').write_text(summary, encoding='utf8')
        states = [dict(active=m[0]=='True', installed=int(m[1]), sources=int(m[2]), generated=int(m[3]), runtime_initialized=m[4]=='True', status=m[5])
                  for m in re.findall(r'\[프레임상태\] active=(True|False) native_installed=(\d+) sources=(\d+) generated=(\d+) runtime_initialized=(True|False) status=([^\r\n]*)', log)]
        # A delayed shutdown retains live buffers safely; the observed final
        # installed=0 and switch reactivation checks below prove cleanup completed.
        if metrics['errors'] or any(s['status'] for s in states) or any(s in log for s in ['Crash!!!', '단계 실패', 'native failure', '[안정성] 안전 모드로 켬']) or '시간 초과로 끔' in summary:
            raise RuntimeError('Normal build failed; inspect saved logs, no automatic retry')
        if diagnostic.exists():
            raise RuntimeError('Normal build must not produce research files')
        if not states or states[-1]['active'] or states[-1]['installed']:
            raise RuntimeError('Native connection not removed after OFF')
        ratios = []
        clocks = [dict(frame=int(m[0]), ticks=int(m[1]), hz=int(m[2]))
                  for m in re.findall(r'\[프레임시각\] frame=(\d+) ticks=(\d+) hz=(\d+)', log)]
        sample = None
        if not a.switch and not a.monitor_smoke and len(clocks) == len(states):
            dt = (clocks[2]['ticks']-clocks[1]['ticks'])/clocks[1]['hz']
            frames = clocks[2]['frame']-clocks[1]['frame']
            submitted = (states[2]['sources']-states[1]['sources']+states[2]['generated']-states[1]['generated']) if a.mode else frames
            sample = dict(seconds=dt, unity_frames=frames, real_fps=frames/dt, submitted=submitted,
                          output_fps=submitted/dt, output_over_source=submitted/frames,
                          off_output_inferred_from_unity=a.mode==0)
        if a.switch:
            expected = [False, False, True, True, True, True, True, True, True, False, False, False, True, False, True, False]
            if [s['active'] for s in states] != expected or [bool(s['installed']) for s in states] != expected:
                raise RuntimeError('Switch/HalfRender suspension state mismatch: '+str(states))
        elif a.mode:
            if not states[1]['active'] or not states[2]['active']:
                raise RuntimeError('Active normal build not observed')
            ds = states[2]['sources']-states[1]['sources']
            dg = states[2]['generated']-states[1]['generated']
            if ds <= 0 or dg < 0 or (dg==0 and not a.refresh):
                raise RuntimeError('Missing normal build successful source/generated Presents')
            ratios.append((ds+dg)/ds)
        elif any(s['active'] or s['installed'] or s['generated'] or s['sources'] or s['runtime_initialized'] for s in states):
            raise RuntimeError('Default OFF unexpectedly initialized output')
        if a.full_song and ('곡 끝남' not in summary or '곡이 끝나지 않음' in summary):
            raise RuntimeError('Actual song completion not confirmed')
        monitor = [dict(mode=int(m[0]), shown=float(m[1]), real=float(m[2]), counting=m[3]=='True')
                   for m in re.findall(r'\[출력 모니터\] mode=(-?\d+) shown=([\d.]+) real=([\d.]+) counting=(True|False)', log)]
        if a.monitor_smoke:
            if [r['mode'] for r in monitor] != [-1,0,1,2,-1,-1] or [r['counting'] for r in monitor] != [True,False,True,True,True,False]:
                raise RuntimeError('Monitor source selection did not match: '+str(monitor))
            for row in monitor:
                target = a.mode if row['counting'] else 1
                if row['real']<=0 or abs(row['shown']/row['real']-target)>target*.1:
                    raise RuntimeError('Monitor FPS did not match successful submission ratio: '+str(row))
            for p in monitor_shots:
                (a.out/p.name).write_bytes(p.read_bytes())
        elif not a.mode and not a.switch:
            if not monitor or any(r['counting'] or abs(r['shown']-r['real'])>.001 for r in monitor):
                raise RuntimeError('Default OFF monitor unexpectedly counted native output: '+str(monitor))
        if not a.no_shot and not a.switch and shot.exists():
            (a.out/'screen.png').write_bytes(shot.read_bytes())
        result = dict(mode=a.mode, refresh=a.refresh, refresh_rest=a.refresh_rest, full_song=a.full_song, switch=a.switch, compatibility_settings=extra,
                      monitor_smoke=a.monitor_smoke, monitor_samples=monitor,
                      managed_sha256=hashlib.sha256(binary).hexdigest(),
                      native_sha256=hashlib.sha256((mod/'sfnative.dll').read_bytes()).hexdigest(),
                      states=states, clocks=clocks, sample=sample, observed_submission_ratios=ratios, game_metrics=metrics,
                      no_research_directory=not diagnostic.exists())
        (a.out/'summary.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
        print(json.dumps(result, ensure_ascii=False), flush=True)
    finally:
        if sf.game_running():
            sf.sf_quit()
        if sf.game_running():
            raise RuntimeError('Game still running; close normally before restoring backups')
        for n, data in original.items():
            (mod/n).write_bytes(data)
        if old_shot is not None:
            shot.write_bytes(old_shot)
        else:
            shot.unlink(missing_ok=True)
        if a.monitor_smoke:
            for p, data in monitor_shots.items():
                if data is None: p.unlink(missing_ok=True)
                else: p.write_bytes(data)
        print('Installed managed/native DLL and exact settings restored', flush=True)


if __name__ == '__main__':
    main()

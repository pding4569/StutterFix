"""Generate M4d native-on ABBA and paused pixel plans. Never launch/install."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import sys

sys.dont_write_bytecode=True
root=Path(__file__).resolve().parents[2]

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--milestone', choices=('m4d','m4e','m4f','m4g','m4h','m4i','m4j','m4k','m4l','m4m'), default='m4d')
    p.add_argument('--maps',default='hl50,wf,dd,sh')
    p.add_argument('--rt',default='3440x1440')
    p.add_argument('--output',type=Path,default=root/'out/direct-renderer/m4d/plans')
    args=p.parse_args()
    for key in ('BENCH_PRE','BENCH_MID','BENCH_POST','BENCH_GAME'):
        if os.environ.get(key):raise SystemExit('clear ambient '+key+' before controlled M4d plan generation')
    os.environ['BENCH_WT']=root.name;os.environ['BENCH_RT']=args.rt
    spec=importlib.util.spec_from_file_location('bench_v14',root.parent/'bench_v14.py')
    bench=importlib.util.module_from_spec(spec);spec.loader.exec_module(bench)
    assert bench.WT.resolve()==root.resolve()
    settings,commands=bench.CFG2['ppGpuCam'];settings=dict(bench.CLEAN,**settings)
    assert settings['GpuDeco']==0 and settings['RunLod'] is False
    commands=list(commands)+['dr2 off','drfull off','drfull assert off']
    if args.milestone in ('m4e','m4f','m4g','m4h','m4i','m4j','m4k','m4l','m4m'):commands += ['drfull grab native']
    if args.milestone in ('m4h','m4i','m4j','m4k','m4l','m4m'):commands += ['drfull explicit on','drfull explicit forward','drfull precision batch']
    if args.milestone in ('m4i','m4j','m4k','m4l','m4m'):commands += ['drfull submit immediate','drfull constants ring','drfull particles explicit','drfull probe clear','drfull pressure restore','framecmp clear']
    if args.milestone in ('m4j','m4k','m4l','m4m'):commands += ['drfull drawgpu off']
    if args.milestone in ('m4f','m4g','m4h','m4i','m4j','m4k','m4l','m4m'):commands += ['drfull warm']
    assert not any(c.startswith(('hidegroup','scriptskip','drx ','dr bench')) for c in commands)
    args.output.mkdir(parents=True,exist_ok=True)
    manifest=dict(workspace=str(root),branch='claude/drfull',launch=False,preset='ppGpuCam',rt=args.rt,
                  measure_order=['A1','B1','B2','A2'],duration_s=12,
                  validity=['candidate assert on before and after interval','native completed draws > 0','errors=0 dropped=0',
                            'no automatic OFF/sample invalid','originalMask==restoredMask','pixel gate before interpreting FPS'],plans=[])
    for map_key in args.maps.split(','):
        path,at=bench.MAPS[map_key]
        for label in ('plan','pixel','A1','B1','B2','A2'):
            candidate=label.startswith('B') or label=='pixel';name=f'{args.milestone}-{map_key}-{label}'
            mid=list(bench.CFG_MID['ppGpuCam'])
            if label=='plan':
                mid += ['drfull plan','drfull census','drfull assert off']
                steps=bench.steps(path,at,name,commands,False,mid,[])
                steps=steps[:steps.index('phasewindow begin')]+['phasewindow off','drfull assert off','quit']
            elif label=='pixel':
                # Include UI avoids FrameCompare's independent camera-mask lease
                # overwriting the renderer lease. Freeze game/planets first.
                mid += ['framecmp ui include','framecmp mask none','pause','wait 1',
                        'drfull on','wait 2','drfull assert on','drfull state','framecmp grab d1','drfull assert on',
                        'drfull off','drfull assert off','wait 1','framecmp grab d0','framecmp diff d0 d1 0 png',
                        'framecmp save d1','framecmp save d0','framecmp grab d00','framecmp diff d0 d00 0',
                        'framecmp assertzero d0 d00','framecmp assertlimit d0 d1 0.1 3 png',
                        'resume','drfull assert off']
                steps=bench.steps(path,at,name,commands,False,mid,[])
                steps=steps[:steps.index('phasewindow begin')]+['phasewindow off','drfull off','drfull assert off','quit']
            else:
                assertion='drfull assert on' if candidate else 'drfull assert off'
                mid += ['drfull on' if candidate else 'drfull off','wait 2',assertion,'drfull state','cpuwindow begin']
                post=['cpuwindow end',assertion,'drfull state','drfull off','drfull assert off','drfull state']+list(bench.CFG_POST['ppGpuCam'])
                steps=bench.steps(path,at,name,commands,False,mid,post)
            if args.milestone in ('m4e','m4f','m4g','m4i','m4j','m4k','m4l','m4m'):
                expanded=[]
                for index, step in enumerate(steps):
                    # Freeze the song during incremental setup. Without this,
                    # candidate preparation advances to a different workload.
                    freeze_setup = label not in ('plan','pixel') and step in ('drfull on','drfull off') and index > steps.index(f'waitsongpos {at} 400') and index < steps.index('fpsmark start')
                    if freeze_setup:expanded.append('pause')
                    if step == 'drfull on' and label == 'pixel':expanded.append('drfull colors')
                    expanded.append(step)
                    if step == 'drfull on':expanded.append('drfull wait')
                    if freeze_setup:expanded.append('resume')
                    if step == 'drfull plan':expanded.append('drfull colors')
                    if step == 'framecmp grab d1':expanded.append('drfull colors')
                    if step == 'drfull state':expanded.append('drfull why')
                steps=expanded
            if args.milestone in ('m4f','m4g','m4h','m4i','m4j','m4k','m4l','m4m'):
                steps=[s for s in steps if s != 'drfull cold' and (candidate or s != 'drfull warm')]
                at_quit=steps.index('quit')
                steps.insert(at_quit,'drfull cold')
            if args.milestone in ('m4g','m4h','m4i','m4j','m4k','m4l','m4m'):
                expanded=[]
                for step in steps:
                    if step in ('fpsmark start','pause') or step.startswith('fpsmark end '):expanded.append('drfull assert clean')
                    expanded.append(step)
                steps=expanded
                manifest['validity'] += [] if 'safe=0 on A/B and pixel runs' in manifest['validity'] else ['safe=0 on A/B and pixel runs']
            assert all(not (c.startswith('wait ') and float(c.split()[1])>=180) for c in steps)
            plan=dict(tag=name,steps=steps,settings=settings,timeout_min=15,
                      measurement=dict(scope='classification only; exclude from ABBA' if label=='plan' else 'paused pixels and off/off noise' if label=='pixel' else 'original shader native M4d' if candidate else 'Unity reference',
                                       song_start_s=at,duration_s=0 if label in ('plan','pixel') else 12,candidate=candidate,pixel_gate=label=='pixel',
                                       pixel_changed_percent_lt=0.1,pixel_max_channel_le=3,off_off_noise=0,classification=label=='plan'))
            file=args.output/(name+'.json');file.write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8');manifest['plans'].append(str(file))
    (args.output/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print('PLAN ONLY',len(manifest['plans']),'JSON files; no launch/install;',args.output)

if __name__=='__main__':main()

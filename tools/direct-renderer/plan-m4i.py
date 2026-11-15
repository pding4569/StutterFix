"""Offline-only M4i plans: normal pixel/ABBA and same-process diagnosis."""
import argparse
import copy
import json
import runpy
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'out/direct-renderer/m4i/plans'
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--probe-first', type=int, default=0)
p.add_argument('--probe-count', type=int, default=1)
args = p.parse_args()
assert args.probe_first >= 0 and args.probe_count > 0
sys.dont_write_bytecode = True
sys.argv = [str(Path(__file__).with_name('plan-m4c.py')), '--milestone', 'm4i', '--output', str(out)]
runpy.run_path(sys.argv[0], run_name='__main__')
manifest = json.loads((out/'manifest.json').read_text(encoding='utf-8'))
manifest.update(prerequisites=['JALib disabled in every run', 'same loaded assets/render size/quality', 'pixel gate before FPS claims', 'no competing play or DLL replacement'], diagnostics=[])
manifest['validity'] += ['DXGI pressure correlation is not page-migration proof', 'actual immediate availability/counters must confirm selected path', 'mip limit may not lower non-mipmapped assets: check observed usage/budget', 'probe indices are session-local; reject out-of-range/stale identities', 'particle baked route experimental and excluded from ordinary ABBA']

def save(plan, name, steps, scope):
    plan = copy.deepcopy(plan)
    plan.update(tag=name, steps=steps)
    plan['measurement'].update(scope=scope, diagnostic=True, pixel_gate=False)
    path = out/(name+'.json')
    path.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')
    manifest['diagnostics'].append(str(path))

for mode in ('immediate','deferred'):
    for label in ('B1','B2','pixel'):
        base = json.loads((out/f'm4i-dd-{label}.json').read_text(encoding='utf-8'))
        old, name = base['tag'], f'm4i-dd-submit-{mode}-{label}'
        steps = [('drfull submit '+mode if s == 'drfull submit immediate' else s.replace(old,name)) for s in base['steps']]
        save(base, name, steps, 'DD submission control; compare paired GPU native/category/VRAM observations')

# Frozen scene in one process: deferred/immediate/immediate/deferred. Both
# high-pressure and mip-limited variants retain the same loaded assets/session.
base = json.loads((out/'m4i-dd-pixel.json').read_text(encoding='utf-8'))
prefix = base['steps'][:base['steps'].index('drfull on')]
for pressure in ('loaded','mip2'):
    name = f'm4i-dd-submit-crossover-{pressure}'
    steps = list(prefix)
    if pressure == 'mip2': steps += ['drfull pressure mip 2','wait 3']
    for i, mode in enumerate(('deferred','immediate','immediate','deferred')):
        steps += ['drfull submit '+mode,'drfull on','drfull wait','wait 2','drfull assert on','drfull state',
                  'framecmp grab c'+str(i),'framecmp save c'+str(i),'cpuwindow begin','fpsmark start','wait 12',
                  f'fpsmark end {name}-{i}-{mode}','cpuwindow end','drfull assert on','drfull state','drfull off','drfull assert off']
    steps += ['framecmp assertpixels c0 c1','framecmp assertpixels c1 c2','framecmp assertpixels c2 c3',
              'drfull pressure restore','drfull submit immediate','resume','drfull cold','quit']
    save(base,name,steps,'same-session frozen DD crossover; parity exact; pressure must be observed, not assumed')

# Retain ON reference, turn OFF once, and restore between every independent
# hide. No seek/reload/restart is permitted inside the bisection sequence.
base = json.loads((out/'m4i-hl50-pixel.json').read_text(encoding='utf-8'))
prefix = base['steps'][:base['steps'].index('drfull on')]
name = 'm4i-hl50-bisect'
steps = prefix + ['drfull on','drfull wait','wait 2','drfull assert on','drfull probe list','drfull colors',
                  'framecmp grab onref','framecmp save onref','drfull state','drfull off','drfull assert off',
                  'wait 1','framecmp grab offref','framecmp grab offnoise','framecmp assertzero offref offnoise',
                  'framecmp save offref','drfull probe particles','wait 1','framecmp grab no_particles',
                  'framecmp diff no_particles onref 0 png','framecmp save no_particles','drfull probe restore']
steps += ['drfull probe bisect','framecmp drop offnoise','wait 1','framecmp grab restored','framecmp assertzero offref restored']
for suffix, first, count in [('range',args.probe_first,args.probe_count),
                              ('left',args.probe_first,max(1,args.probe_count//2)),
                              ('right',args.probe_first+args.probe_count//2,args.probe_count-args.probe_count//2)]:
    steps += [f'drfull probe hide {first} {count}','wait 1',f'framecmp grab {suffix}',
              f'framecmp diff {suffix} onref 0 png',f'framecmp save {suffix}','drfull probe restore',
              'wait 1','framecmp grab restored', 'framecmp assertzero offref restored']
steps += ['drfull probe clear','resume','drfull cold','quit']
save(base,name,steps,'same-session ON-reference bisection; choose ranges from current TSV, never another session')
bisect_suffix = steps[len(prefix):]
for key in ('sh','dd','wf'):
    other=json.loads((out/f'm4i-{key}-pixel.json').read_text(encoding='utf-8'))
    other_prefix=other['steps'][:other['steps'].index('drfull on')]
    save(other,f'm4i-{key}-bisect',other_prefix+bisect_suffix,'same-session native/explicit/Canvas-graphic identity attribution; greedy candidate, verify leaf pixels')

name = 'm4i-hl50-particles-pixel'
steps = prefix + ['drfull on','drfull wait','wait 2','drfull assert on','drfull probe list','framecmp grab explicit',
                  'drfull state','drfull off','drfull particles baked','drfull on','drfull wait','wait 2',
                  'drfull assert on','framecmp grab baked','drfull state','drfull off','drfull particles explicit',
                  'wait 1','framecmp grab original','framecmp grab noise','framecmp assertzero original noise',
                  'framecmp diff original explicit 0 png','framecmp diff original baked 0 png',
                  'framecmp diff explicit baked 0 png','framecmp save explicit','framecmp save baked','framecmp save original',
                  'drfull probe clear','resume','drfull cold','quit']
save(base,name,steps,'same-session original/explicit/baked particles: compare pixels and GPU explicit time; no performance acceptance')

for key in ('sh','dd','wf'):
    base = json.loads((out/f'm4i-{key}-pixel.json').read_text(encoding='utf-8'))
    for kind, command, anchor in [('local','drfull precision local','drfull precision batch'),
                                   ('unity-grab','drfull grab unity','drfull grab native')]:
        old,name = base['tag'],f'm4i-{key}-{kind}-pixel'
        steps = [(command if s==anchor else s.replace(old,name)) for s in base['steps']]
        steps[steps.index('quit'):steps.index('quit')] = ['drfull precision batch','drfull grab native']
        save(base,name,steps,'precision/Grab route attribution control; actual decoration requires same-session renderer bisection')

(out/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2),encoding='utf-8')
print(f'PLAN ONLY M4i regular={len(manifest["plans"])} diagnostic={len(manifest["diagnostics"])}; game/install=none')

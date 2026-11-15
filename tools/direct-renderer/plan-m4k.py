"""Offline-only M4k plans: normal pixel/ABBA and same-process diagnosis."""
import argparse
import copy
import json
import runpy
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'out/direct-renderer/m4k/plans'
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--probe-first', type=int, default=0)
p.add_argument('--probe-count', type=int, default=1)
args = p.parse_args()
assert args.probe_first >= 0 and args.probe_count > 0
sys.dont_write_bytecode = True
sys.argv = [str(Path(__file__).with_name('plan-m4c.py')), '--milestone', 'm4k', '--output', str(out)]
runpy.run_path(sys.argv[0], run_name='__main__')
manifest = json.loads((out/'manifest.json').read_text(encoding='utf-8'))
for filename in manifest['plans']:
    path = Path(filename)
    plan = json.loads(path.read_text(encoding='utf-8'))
    i = plan['steps'].index('drfull off') + 1
    plan['steps'][i:i] = ['drfull resources default', 'drfull drawgpu off']
    if Path(filename).name.startswith('m4k-dd-'):
        load = next(i for i,s in enumerate(plan['steps']) if s.startswith('open '))
        plan['steps'][load:load] = ['set ImageMaxSide 0']
        plan['settings']['ImageMaxSide'] = 0
    path.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')
manifest.update(prerequisites=['JALib disabled in every run', 'same loaded assets/render size/quality', 'pixel gate before FPS claims', 'no competing play or DLL replacement'], diagnostics=[])
manifest['validity'] += ['DXGI pressure correlation is not page-migration proof', 'actual immediate availability/counters must confirm selected path', 'ImageMaxSide must be set BEFORE opening/reloading map; verify actual texture dimensions and DXGI usage', 'probe indices are session-local; reject out-of-range/stale identities', 'draw GPU diagnostics alter workload; excluded from ordinary ABBA']

def save(plan, name, steps, scope):
    plan = copy.deepcopy(plan)
    plan.update(tag=name, steps=steps)
    plan['measurement'].update(scope=scope, diagnostic=True, pixel_gate=False)
    path = out/(name+'.json')
    path.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')
    manifest['diagnostics'].append(str(path))

# Retain ON reference, turn OFF once, and restore between every independent
# hide. No seek/reload/restart is permitted inside the bisection sequence.
base = json.loads((out/'m4k-hl50-pixel.json').read_text(encoding='utf-8'))
prefix = base['steps'][:base['steps'].index('drfull on')]
name = 'm4k-hl50-bisect'
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
for key in ('hl50','sh','dd','wf'):
    other=json.loads((out/f'm4k-{key}-pixel.json').read_text(encoding='utf-8'))
    other_prefix=other['steps'][:other['steps'].index('drfull on')]
    save(other,f'm4k-{key}-bisect',other_prefix+bisect_suffix,'same-session native/explicit/Canvas-graphic identity attribution; greedy candidate, verify leaf pixels')

name = 'm4k-hl50-particles-pixel'
steps = prefix + ['drfull on','drfull wait','wait 2','drfull assert on','drfull probe list','framecmp grab explicit',
                  'drfull state','drfull off','drfull particles baked','drfull on','drfull wait','wait 2',
                  'drfull assert on','framecmp grab baked','drfull state','drfull off','drfull particles explicit',
                  'wait 1','framecmp grab original','framecmp grab noise','framecmp assertzero original noise',
                  'framecmp diff original explicit 0 png','framecmp diff original baked 0 png',
                  'framecmp diff explicit baked 0 png','framecmp save explicit','framecmp save baked','framecmp save original',
                  'drfull probe clear','resume','drfull cold','quit']
save(base,name,steps,'same-session original/explicit/baked particles: compare pixels and GPU explicit time; no performance acceptance')

for key in ('hl50','sh','dd','wf'):
    base = json.loads((out/f'm4k-{key}-pixel.json').read_text(encoding='utf-8'))
    for kind, command, anchor in [('local','drfull precision local','drfull precision batch'),
                                   ('unity-grab','drfull grab unity','drfull grab native')]:
        old,name = base['tag'],f'm4k-{key}-{kind}-pixel'
        steps = [(command if s==anchor else s.replace(old,name)) for s in base['steps']]
        steps[steps.index('quit'):steps.index('quit')] = ['drfull precision batch','drfull grab native']
        save(base,name,steps,'precision/Grab route attribution control; actual decoration requires same-session renderer bisection')


# DD bounded windows preserve native draw batching. Sweep windows until all
# visible ordinals are covered. Diagnostic runs do not produce ABBA acceptance.
base=json.loads((out/'m4k-dd-pixel.json').read_text(encoding='utf-8'))
prefix=base['steps'][:base['steps'].index('drfull on')]
for first in range(0,384,128):
    name=f'm4k-dd-drawgpu-{first:04d}'
    steps=prefix+['drfull drawgpu on '+str(first)+' 128 4','drfull on','drfull wait','wait 2',
                 'drfull assert on','drfull probe list','drfull state','wait 12','drfull drawgpu dump','drfull drawgpu assert',
                 'drfull state','drfull off','drfull assert off','drfull drawgpu off','resume','drfull cold','quit']
    save(base,name,steps,'same-session paused DD draw setup+draw GPU elapsed; join IDs/textures; extend sweep if ordinals exceed 2047')
# Image resizing happens at decode, so every pair opens a fresh map with the
# same setting; never apply ImageMaxSide to an already loaded DD texture set.
for label in ('A1','B1','B2','A2','pixel'):
    base=json.loads((out/f'm4k-dd-{label}.json').read_text(encoding='utf-8'))
    name=f'm4k-dd-imagemax1024-{label}'
    steps=[s.replace(base['tag'],name) for s in base['steps']]
    load=next(i for i,s in enumerate(steps) if s.startswith(('open ','openplay ','openeditor ','gameopen ')))
    steps[load:load]=['set ImageMaxSide 1024']
    save(base,name,steps,'fresh-load DD ImageMaxSide=1024 paired A/B; settings restored by AutoTest finally; compare pixel only at same 1024 quality')
    plan=json.loads((out/(name+'.json')).read_text(encoding='utf-8'))
    plan['settings']['ImageMaxSide']=1024
    plan['measurement'].update(image_max_side=1024,require_usage_below_budget=True,require_observed_texture_dimensions=True)
    (out/(name+'.json')).write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
manifest['dd_protocol']={
    'rejected_by_prior_game_evidence':['deferred command-list submission explains 41ms','mip2 proves lower VRAM for no-mip textures'],
    'draw_probe':['run windows until no more visible ordinals; rerun shifted windows for boundaries','rank aggregate GPU time per draw/texture; compare sum to segment_us once per frame/segment, not once per row','if draw sum does not explain segment cost, inspect prepareGpu/constants/unmeasured gaps','capture original Unity and native draws with RenderDoc/PIX: actual PS/VS SRV formats, MostDetailedMip, mip count, sRGB view, sampler filter/aniso/MinLOD/MaxLOD and shader sampling instructions','timestamps measure GPU elapsed including stalls, not GPU busy or page migrations'],
    'low_pressure':['apply ImageMaxSide=1024 before every fresh map load; close prior map or use fresh process to avoid cached full-size textures','require measured DXGI usage below budget with headroom, else lower to 512 and repeat matched A/B','same JALib/render size/quality/loaded assets and seek; pixel/noise gate within 1024 condition before ABBA','compare original-size ABBA versus 1024 ABBA separately; different-quality cross-condition pixels are not a parity gate','restore temporary setting in finally; original full-size assets require re-open after restoration'],
    'attribution':'41ms hardware draw identities and remaining actual decoration pixels are UNMEASURED until authorized game tests'}
# Resource ablations change ONE factor at a time, in the same frozen session.
# Grab-off is explicitly a non-parity diagnostic; never accept its FPS as a win.
for limit in (0, 1024):
    base = json.loads((out/'m4k-dd-pixel.json').read_text(encoding='utf-8'))
    prefix = base['steps'][:base['steps'].index('drfull on')]
    if limit:
        load = next(i for i, s in enumerate(prefix) if s.startswith('open '))
        prefix[load:load] = [f'set ImageMaxSide {limit}']
    for label, command in [('grab-off','drfull resources grab off'), ('ring-off','drfull constants legacy'),
                           ('pretransform-off','drfull resources pretransform off'), ('rename','drfull resources stable off'),
                           ('stale-bindings','drfull resources trim off')]:
        name = f'm4k-dd-resources-{limit or "full"}-{label}'
        steps = prefix + ['drfull drawgpu on 0 256 4']
        for index, setting in enumerate(('drfull resources default', command, command, 'drfull resources default')):
            steps += ['drfull resources default', 'drfull constants ring', setting, 'drfull on', 'drfull wait', 'wait 2',
                      'drfull assert on', 'drfull state', 'wait 12', 'drfull drawgpu dump', 'drfull drawgpu assert', 'drfull state',
                      'drfull off', 'drfull assert off']
        steps += ['drfull drawgpu off','drfull resources default','drfull constants ring','resume','drfull cold','quit']
        save(base,name,steps,'same-session factor A/B/B/A; query overhead included; compare resource phases/VRAM; Grab-off changes image and is NOT performance acceptance')
        path = out/(name+'.json'); plan = json.loads(path.read_text(encoding='utf-8'))
        if limit: plan['settings']['ImageMaxSide'] = limit
        plan['measurement'].update(image_max_side=limit, changes_pixels=label=='grab-off')
        path.write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
manifest['dd_protocol']['m4k'] = ['native IDs != visible ordinal; observed prior DD=315 draws, sweep 0/128/256; extend only when max_visible permits',
    'all primary runs reset resources/default and disable draw queries',
    'resource controls alone do not prove page migration; correlate with VidMm ETW/PIX for the expensive intervals',
    'prepare_gpu_us and prepare_constants_us repeat per row: count once per frame/segment; grab_us is inside gpu_us',
    'original Unity GrabPass count and per-renderer culling require an original Unity frame capture; native counters cannot measure them']
(out/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2),encoding='utf-8')
print(f'PLAN ONLY M4k regular={len(manifest["plans"])} diagnostic={len(manifest["diagnostics"])}; game/install=none')

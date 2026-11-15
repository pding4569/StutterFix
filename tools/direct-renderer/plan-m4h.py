"""Generate M4h controls and diagnostics. Does not launch or install anything."""
import json, runpy, sys
from pathlib import Path
sys.dont_write_bytecode = True
root = Path(__file__).resolve().parents[2]
out = root / 'out/direct-renderer/m4h/plans'
sys.argv[1:1] = ['--milestone', 'm4h', '--output', str(out)]
runpy.run_path(str(Path(__file__).with_name('plan-m4c.py')), run_name='__main__')
manifest = json.loads((out / 'manifest.json').read_text(encoding='utf-8'))
manifest['prerequisites'] = ['JALib disabled for ALL A/B/pixel/diagnostic runs', 'same render size, quality and loaded assets', 'no competing play or DLL replacement']
manifest['validity'] += ['VRAM budget exceedance is evidence of pressure, not proof of page migration', 'skip-explicit/explicit-all/precision-local diagnostics excluded from ABBA', 'camera timeline starts at BeforeForwardOpaque, excludes earlier work', 'repeat DD runs to capture both fast and slow conditions']
manifest['diagnostics'] = []
for label in ('B1', 'B2'):
    base = json.loads((out / f'm4h-dd-{label}.json').read_text(encoding='utf-8'))
    for kind, command, anchor in [('skip-explicit', 'drfull skip explicit', 'drfull explicit on'),
                                  ('explicit-all', 'drfull explicit all', 'drfull explicit forward')]:
        plan = json.loads(json.dumps(base))
        old, name = base['tag'], f'm4h-dd-{kind}-{label}'
        plan['tag'] = name
        plan['steps'] = [command if step == anchor else step.replace(old, name) for step in plan['steps']]
        # Reset persistent diagnostics even when multiple plans share a process.
        end = plan['steps'].index('quit')
        plan['steps'][end:end] = ['drfull explicit on', 'drfull explicit forward', 'drfull precision batch']
        plan['measurement'].update(scope='diagnostic only; excluded from ABBA and parity', diagnostic=kind, pixel_gate=False)
        path = out / (name + '.json')
        path.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')
        manifest['diagnostics'].append(str(path))
for key in ('dd', 'sh'):
    base = json.loads((out / f'm4h-{key}-pixel.json').read_text(encoding='utf-8'))
    old, name = base['tag'], f'm4h-{key}-precision-local-pixel'
    base['tag'] = name
    base['steps'] = ['drfull precision local' if s == 'drfull precision batch' else s.replace(old, name) for s in base['steps']]
    base['steps'].insert(base['steps'].index('quit'), 'drfull precision batch')
    base['measurement'].update(scope='local vertices versus Unity; compare with normal pixel plan', diagnostic='precision-local')
    path = out / (name + '.json')
    path.write_text(json.dumps(base, ensure_ascii=False, indent=2), encoding='utf-8')
    manifest['diagnostics'].append(str(path))
(out / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'PLAN ONLY M4h regular={len(manifest["plans"])} diagnostic={len(manifest["diagnostics"])}; no launch/install')

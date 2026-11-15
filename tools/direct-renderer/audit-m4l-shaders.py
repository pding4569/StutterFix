"""Prove the partial-Grab contract from the current original-DXBC disassembly."""
import hashlib
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
folder = root / 'out/direct-renderer/m4bc/shaders'
catalog = json.loads((folder / 'bindings.json').read_text(encoding='utf-8'))
shader = next(s for s in catalog['shaders'] if s['name'] == 'Hidden/BlendModes/SpritesDefault/Grab')
variants = [v for v in shader['passes'][0]['variants'] if 'PIXELSNAP_ON' not in v['keywords']]
vs_hash = '6433501484174391f4ffb64b16a53b964df159ba7aed3752bb1f9de51c5f51b7'
assert len(variants) == 100
samples = 0
for variant in variants:
    assert variant['vs']['sha256'] == vs_hash, 'new/deformed vertex program: use full Grab'
    stage = variant['ps']
    code = folder / (stage['sha256'] + '.dxbc')
    assert hashlib.sha256(code.read_bytes()).hexdigest() == stage['sha256']
    slot = next(t['slot'] for t in stage['textures'] if t['name'] == '_GrabTexture')
    lines = (folder / (stage['sha256'] + '.asm')).read_text().splitlines()
    # Track only exact screen XY/W expressions, component by component. Other
    # register writes invalidate expressions; a sample consumes BEFORE writing.
    registers = {}
    seen = 0
    for line in lines:
        match = re.match(r'(\w+) (r\d+)\.([xyzw]+), (.*)', line)
        if not match:
            continue
        op, dst, mask, operands = match.groups()
        if op.startswith('sample') and re.search(rf'\bt{slot}\.', operands):
            coord = re.match(r'(r\d+)\.([xyzw]+),', operands)
            assert op == 'sample' and coord, (variant['keywords'], line)
            reg, swizzle = coord.groups()
            assert [registers.get((reg, c)) for c in swizzle[:2]] == ['screenX', 'screenY'], (variant['keywords'], line)
            seen += 1
        value = {}
        div = re.fullmatch(r'v3\.([xyzw]{4}), v3\.wwww', operands) if op == 'div' else None
        if div:
            for c in mask:
                source = div[1]['xyzw'.index(c)]
                value[c] = {'x': 'screenX', 'y': 'screenY'}.get(source)
        for c in mask:
            registers[(dst, c)] = value.get(c)
    assert seen == 1, (variant['keywords'], seen)
    samples += seen
result = dict(shader=shader['name'], eligible_variants=len(variants), screen_samples=samples,
              vertex_sha256=vs_hash, pixel_snap='full copy', unknown_shader='full copy',
              actual_indexed_vertex_rect_check=True, gpu_readback=False)
out = root / 'out/direct-renderer/m4l'
out.mkdir(parents=True, exist_ok=True)
(out / 'shader-contract.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(f'PASS M4l screen Grab contract variants={len(variants)} samples={samples} VS=1 unknown=full')

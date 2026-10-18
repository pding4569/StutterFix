"""Offline-only GPU-free evidence: PNG bytes, saved census, serialized shaders/cameras.

Run with Python 3.12 and UnityPy/Pillow available; never launches Unity or writes Mods.
"""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path

from PIL import Image
import UnityPy


def run():
    ap = argparse.ArgumentParser()
    ap.add_argument('--game-data', type=Path, required=True)
    ap.add_argument('--map', type=Path, required=True)
    ap.add_argument('--census-dir', type=Path, required=True)
    ap.add_argument('--out', type=Path, required=True)
    args = ap.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    result = {'png': [], 'census': [], 'assets': [], 'errors': []}
    for name in ('fog.png', 'nes_large_fog.png', 'nev_fog.png', 'nes_black_fog.png'):
        files = sorted(args.map.rglob(name))
        for path in files:
            with Image.open(path) as img:
                rgba = img.convert('RGBA')
                colors = Counter(rgba.get_flattened_data() if hasattr(rgba, 'get_flattened_data') else rgba.getdata())
                rgbs = {c[:3] for c in colors}
                visible_rgbs = {c[:3] for c in colors if c[3] > 0}
                result['png'].append({'path': str(path), 'size': rgba.size,
                    'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                    'extrema': rgba.getextrema(), 'unique_rgb': len(rgbs),
                    'unique_rgb_nonzero_alpha': len(visible_rgbs),
                    'constant_rgb': next(iter(rgbs)) if len(rgbs) == 1 else None,
                    'zero_alpha_pixels': sum(n for c, n in colors.items() if c[3] == 0),
                    'rgba_bytes': rgba.width * rgba.height * 4})
        if not files:
            result['errors'].append('PNG missing: ' + name)
    for path in sorted(args.census_dir.glob('gpu-census-*.tsv')):
        lines = path.read_text(encoding='utf-8-sig').splitlines()
        result['census'].append({'path': str(path),
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'records': [s for s in lines if s.startswith(('# camera=', '# OnRenderImage'))
                        and (not s.startswith('# OnRenderImage') or 'active=True' in s)]})
    # Scan small serialized scene files plus all shared asset metadata. Do not load resS payloads.
    paths = sorted(args.game_data.glob('sharedassets*.assets'))
    paths += [args.game_data / 'resources.assets', args.game_data / 'globalgamemanagers.assets']
    paths += sorted(p for p in args.game_data.glob('level*') if p.is_file() and '.' not in p.name)
    shader_count = camera_count = 0
    for path in paths:
        if not path.is_file():
            continue
        try:
            env = UnityPy.load(str(path))
            for obj in env.objects:
                kind = obj.type.name
                if kind not in ('Shader', 'Camera'):
                    continue
                try:
                    tree = obj.read_typetree()
                    if kind == 'Shader':
                        shader_count += 1
                        parsed = tree.get('m_ParsedForm', {})
                        name = parsed.get('m_Name', tree.get('m_Name', ''))
                        if not (name.startswith(('Sprites/', 'ADOFAI/', 'CameraFilterPack'))
                                or 'Bloom' in name):
                            continue
                        safe = name.replace('/', '_').replace(':', '_')
                        target = args.out / (safe + '-' + str(obj.path_id) + '.json')
                        target.write_text(json.dumps(tree, ensure_ascii=True, indent=2, default=str), encoding='utf-8')
                        result['assets'].append({'file': str(path), 'path_id': obj.path_id,
                                                 'type': kind, 'name': name, 'tree': str(target)})
                    else:
                        camera_count += 1
                        go = obj.read().m_GameObject.read()
                        result['assets'].append({'file': str(path), 'path_id': obj.path_id,
                            'type': kind, 'name': go.m_Name,
                            'settings': {k: v for k, v in tree.items() if any(
                                s in k.lower() for s in ('clear', 'background', 'depth', 'target', 'msaa', 'hdr', 'culling'))}})
                except Exception as exc:
                    result['errors'].append(str(path) + ':' + str(obj.path_id) + ':' + repr(exc))
        except Exception as exc:
            result['errors'].append(str(path) + ':' + repr(exc))
    result['shader_objects'] = shader_count
    result['camera_objects'] = camera_count
    (args.out / 'audit.json').write_text(json.dumps(result, ensure_ascii=True, indent=2), encoding='utf-8')
    print(json.dumps({'png': len(result['png']), 'assets': len(result['assets']),
                      'shaders': shader_count, 'cameras': camera_count, 'errors': len(result['errors'])}))
    if result['errors']:
        raise RuntimeError('Audit incomplete; inspect errors in audit.json')


if __name__ == '__main__':
    run()

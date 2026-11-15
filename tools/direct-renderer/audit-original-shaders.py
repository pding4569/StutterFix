"""M4a offline inventory. Reads installed assets; writes only explicit output paths.

DXBC reachability is not a claim that a material's active variant can be bound.
Use --deps to reuse a local UnityPy installation without installing packages.
"""
import argparse
import ctypes
import gc
import hashlib
import json
from pathlib import Path
import struct
import sys


def dxbc_info(code):
    if len(code) < 32 or code[:4] != b'DXBC':
        raise ValueError('invalid DXBC header')
    size, chunks = struct.unpack_from('<II', code, 24)
    if size != len(code) or chunks > (size - 32) // 4:
        raise ValueError('invalid DXBC size/table')
    tags, stage = [], None
    for i in range(chunks):
        off = struct.unpack_from('<I', code, 32 + i * 4)[0]
        if off < 32 + chunks * 4 or off + 8 > size:
            raise ValueError('invalid DXBC chunk offset')
        tag = code[off:off + 4].decode('ascii')
        n = struct.unpack_from('<I', code, off + 4)[0]
        if off + 8 + n > size:
            raise ValueError('invalid DXBC chunk extent')
        tags.append(tag)
        if tag in ('SHDR', 'SHEX') and n >= 4:
            kind = struct.unpack_from('<I', code, off + 8)[0] >> 16
            stage = {0: 'ps', 1: 'vs', 2: 'gs', 3: 'hs', 4: 'ds', 5: 'cs'}.get(kind, 'unknown')
    if stage is None:
        raise ValueError('missing shader instruction chunk')
    return stage, tags


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--game-data', type=Path, required=True)
    p.add_argument('--deps', type=Path)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--catalog', type=Path, required=True)
    args = p.parse_args()
    if args.deps:
        sys.path.insert(0, str(args.deps.resolve()))
    sys.dont_write_bytecode = True
    import UnityPy
    import lz4.block
    args.output.mkdir(parents=True, exist_ok=True)
    compiler = ctypes.WinDLL('d3dcompiler_47.dll')
    compiler.D3DDisassemble.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_uint, ctypes.c_char_p, ctypes.POINTER(ctypes.c_void_p)]
    compiler.D3DDisassemble.restype = ctypes.c_long

    def validate(code):
        blob = ctypes.c_void_p()
        data = ctypes.create_string_buffer(code)
        hr = compiler.D3DDisassemble(data, len(code), 0, None, ctypes.byref(blob))
        if hr < 0 or not blob:
            raise ValueError(f'D3DDisassemble failed: {hr}')
        table = ctypes.cast(ctypes.cast(blob, ctypes.POINTER(ctypes.c_void_p))[0], ctypes.POINTER(ctypes.c_void_p))
        ctypes.WINFUNCTYPE(ctypes.c_ulong, ctypes.c_void_p)(table[2])(blob)

    rows, sources = {}, []
    names = [args.game_data / 'globalgamemanagers.assets', args.game_data / 'resources.assets'] + sorted(args.game_data.glob('sharedassets*.assets'))
    targets = {'Sprites/Tile', 'Sprites/Default', 'ADOFAI/FloorMesh', 'ADOFAI/Frames', 'ADOFAI/TiledSpriteWithGap', 'ADOFAI/HoldShader', 'Legacy Shaders/Particles/Additive'}
    errors = []
    for asset in names:
        env = UnityPy.load(str(asset))
        seen = 0
        for obj in env.objects:
            if obj.type.name != 'Shader':
                continue
            seen += 1
            tree = obj.read_typetree()
            parsed = tree.get('m_ParsedForm') or {}
            name = parsed.get('m_Name') or tree.get('m_Name')
            if not name:
                raise ValueError(f'unnamed shader in {asset.name}:{obj.path_id}')
            programs, failures = [], []
            compressed = bytes(tree.get('compressedBlob', []))
            for i, platform in enumerate(tree.get('platforms', [])):
                if platform != 4:  # Unity ShaderCompilerPlatform D3D11
                    continue
                offsets, lengths, sizes = tree['offsets'][i], tree['compressedLengths'][i], tree['decompressedLengths'][i]
                if not isinstance(offsets, list):
                    offsets, lengths, sizes = [offsets], [lengths], [sizes]
                if not len(offsets) == len(lengths) == len(sizes):
                    raise ValueError('compressed shader block table mismatch')
                for block, (offset, length, size) in enumerate(zip(offsets, lengths, sizes)):
                    try:
                        raw = lz4.block.decompress(compressed[offset:offset + length], uncompressed_size=size)
                        pos = 0
                        while True:
                            start = raw.find(b'DXBC', pos)
                            if start < 0:
                                break
                            if start + 28 > len(raw):
                                raise ValueError('truncated DXBC header')
                            n = struct.unpack_from('<I', raw, start + 24)[0]
                            if n < 32 or start + n > len(raw):
                                raise ValueError('truncated DXBC program')
                            code = raw[start:start + n]
                            stage, tags = dxbc_info(code)
                            validate(code)
                            programs.append(dict(block=block, offset=start, size=n, stage=stage, chunks=tags, sha256=hashlib.sha256(code).hexdigest()))
                            pos = start + n
                    except Exception as e:
                        failures.append(f'block {block}: {e}')
            vs = sum(x['stage'] == 'vs' for x in programs)
            ps = sum(x['stage'] == 'ps' for x in programs)
            row = dict(shader=name, source=asset.name, path_id=obj.path_id, programs=programs,
                       vertex=vs, pixel=ps, other=len(programs)-vs-ps,
                       rdef=sum('RDEF' in x['chunks'] for x in programs),
                       unity_metadata=bool(parsed.get('m_SubShaders')),
                       extractable='ERROR' if failures else 'YES' if vs and ps else 'NO_DIRECT_VS_PS', errors=failures)
            if name in rows:
                prior = rows[name]
                row['identities'] = prior.get('identities', [dict(source=prior['source'], path_id=prior['path_id'])]) + [dict(source=asset.name, path_id=obj.path_id)]
                row['source'] = prior['source'] + ',' + asset.name
                row['programs'] = prior['programs'] + programs
                for field in ('vertex', 'pixel', 'other', 'rdef'):
                    row[field] += prior[field]
                row['errors'] = prior['errors'] + failures
                row['extractable'] = 'ERROR' if row['errors'] else 'YES_MULTIPLE_ASSETS' if row['vertex'] and row['pixel'] else 'NO_DIRECT_VS_PS'
            rows[name] = row
            errors.extend(f'{name}: {e}' for e in failures)
            if name in targets or name.startswith('Hidden/BlendModes/'):
                # Bindings, pass states, keyword indices and cbuffer offsets are
                # retained for the next stage; no inferred variant pairing here.
                safe = hashlib.sha256((name + asset.name + str(obj.path_id)).encode()).hexdigest()[:16]
                (args.output / (safe + '-metadata.json')).write_text(json.dumps(dict(shader=name, parsed=parsed), ensure_ascii=False, indent=2), encoding='utf-8')
        sources.append(dict(file=asset.name, bytes=asset.stat().st_size, shaders=seen))
        print(f'{asset.name}: shaders={seen}', flush=True)
        del env
        gc.collect()
    args.catalog.parent.mkdir(parents=True, exist_ok=True)
    lines = ['shader\textractable\tvertexPrograms\tpixelPrograms\totherPrograms\trdefPrograms\tunityMetadata\tsource']
    for name, row in sorted(rows.items()):
        lines.append('\t'.join(map(str, (name, row['extractable'], row['vertex'], row['pixel'], row['other'], row['rdef'], 'YES' if row['unity_metadata'] else 'NO', row['source']))))
    args.catalog.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    (args.output / 'inventory.json').write_text(json.dumps(dict(reader=UnityPy.__version__, read_only=True, sources=sources, shaders=list(rows.values()), errors=errors), ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'shaders={len(rows)} programs={sum(len(r["programs"]) for r in rows.values())} errors={len(errors)}')
    if errors:
        raise SystemExit('\n'.join(errors))


if __name__ == '__main__':
    main()

"""Read compiled game shader metadata/instructions; never modify game assets."""
import ctypes
import gc
import hashlib
import json
import struct
import sys
from pathlib import Path

root=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(root/'out/asset-reader-deps'))
import UnityPy
import lz4.block

game=Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data')
out=root/'out/original-tile-shader';out.mkdir(exist_ok=True)
compiler=ctypes.WinDLL('d3dcompiler_47.dll')
def disassemble(blob):
    output=ctypes.c_void_p()
    hr=compiler.D3DDisassemble(blob,ctypes.c_size_t(len(blob)),0,None,ctypes.byref(output))
    if hr!=0:return None
    table=ctypes.cast(ctypes.cast(output,ctypes.POINTER(ctypes.c_void_p))[0],ctypes.POINTER(ctypes.c_void_p))
    try:
        pointer=ctypes.WINFUNCTYPE(ctypes.c_void_p,ctypes.c_void_p)(table[3])(output)
        length=ctypes.WINFUNCTYPE(ctypes.c_size_t,ctypes.c_void_p)(table[4])(output)
        return ctypes.string_at(pointer,length).decode('latin1')
    finally:ctypes.WINFUNCTYPE(ctypes.c_ulong,ctypes.c_void_p)(table[2])(output)

names=['sharedassets1.assets']+[p.name for p in sorted(game.glob('sharedassets*.assets')) if p.name not in ('sharedassets0.assets','sharedassets1.assets')]
for name in names:
    path=game/name;env=UnityPy.load(str(path));seen=0
    for obj in env.objects:
        if obj.type.name!='Shader':continue
        seen+=1
        try:
            tree=obj.read_typetree();parsed=tree.get('m_ParsedForm',{})
            shader=parsed.get('m_Name') or tree.get('m_Name')
        except Exception:continue
        if shader!='Sprites/Tile':continue
        # Save only this shader's state and disassembly as bounded audit evidence.
        result=dict(shader=shader,source_asset=str(path),path_id=obj.path_id,reader=UnityPy.__version__,read_only=True,passes=parsed.get('m_SubShaders'),programs=[])
        (out/'state.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        compressed=bytes(tree['compressedBlob'])
        for i,platform in enumerate(tree['platforms']):
            if platform!=4:continue
            offsets=tree['offsets'][i];lengths=tree['compressedLengths'][i];sizes=tree['decompressedLengths'][i]
            if not isinstance(offsets,list):offsets,lengths,sizes=[offsets],[lengths],[sizes]
            for block,(offset,length,size) in enumerate(zip(offsets,lengths,sizes)):
                raw=lz4.block.decompress(compressed[offset:offset+length],uncompressed_size=size);position=0
                while True:
                    start=raw.find(b'DXBC',position)
                    if start<0:break
                    size=struct.unpack_from('<I',raw,start+24)[0];position=start+max(4,size)
                    if size<32 or start+size>len(raw):continue
                    code=raw[start:start+size];text=disassemble(code)
                    if text is None:continue
                    number=len(result['programs']);file=out/('program-'+str(number)+'.txt');file.write_text(text,encoding='utf-8')
                    result['programs'].append(dict(file=file.name,sha256=hashlib.sha256(code).hexdigest()))
        (out/'state.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps(dict(found=True,source=name,programs=len(result['programs']),output=str(out))),flush=True);sys.exit(0)
    print(json.dumps(dict(source=name,shaders_read=seen,target_found=False)),flush=True)
    del env;gc.collect()
raise SystemExit('Sprites/Tile not found in the bounded asset search')

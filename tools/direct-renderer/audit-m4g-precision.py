"""Disassemble old/new pretransform CS against original Unity VS order, offline."""
import ctypes,importlib.util,json,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[2]
out=root/'out/direct-renderer/m4g';out.mkdir(parents=True,exist_ok=True)
spec=importlib.util.spec_from_file_location('catalog',Path(__file__).with_name('export-original-shaders.py'))
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
dll=ctypes.WinDLL('d3dcompiler_47.dll')
dll.D3DCompile.argtypes=[ctypes.c_void_p,ctypes.c_size_t,ctypes.c_char_p,ctypes.c_void_p,ctypes.c_void_p,ctypes.c_char_p,ctypes.c_char_p,ctypes.c_uint,ctypes.c_uint,ctypes.POINTER(ctypes.c_void_p),ctypes.POINTER(ctypes.c_void_p)]
dll.D3DCompile.restype=ctypes.c_long
def blob(p):
    vt=ctypes.cast(ctypes.cast(p,ctypes.POINTER(ctypes.c_void_p))[0],ctypes.POINTER(ctypes.c_void_p))
    data=ctypes.WINFUNCTYPE(ctypes.c_void_p,ctypes.c_void_p)(vt[3])(p)
    size=ctypes.WINFUNCTYPE(ctypes.c_size_t,ctypes.c_void_p)(vt[4])(p)
    result=ctypes.string_at(data,size)
    ctypes.WINFUNCTYPE(ctypes.c_ulong,ctypes.c_void_p)(vt[2])(p)
    return result
def compile(source):
    begin=source.index('static const char source[]=R"(')+len('static const char source[]=R"(')
    source=source[begin:source.index(')";',begin)].encode()
    data=ctypes.create_string_buffer(source);code=ctypes.c_void_p();error=ctypes.c_void_p()
    hr=dll.D3DCompile(data,len(source),None,None,None,b'main',b'cs_5_0',(1<<15)|(1<<13),0,ctypes.byref(code),ctypes.byref(error))
    message=blob(error).decode(errors='replace') if error else ''
    assert hr>=0,message
    return module.disassembler()(blob(code))
old=subprocess.check_output(['git','show','HEAD:native/sfnative/drfull.cpp'],cwd=root).decode('utf-8-sig')
new=(root/'native/sfnative/drfull.cpp').read_text(encoding='utf-8-sig')
for name,source in [('before',old),('after',new)]:
    (out/f'pretransform-{name}.asm').write_text(compile(source),encoding='ascii')
audit=json.loads((root/'out/direct-renderer/m4bc/shaders/bindings.json').read_text(encoding='utf-8'))
rows=[]
for shader in audit['shaders']:
    if shader['name'] not in ('Sprites/Tile','Sprites/Default','ADOFAI/FloorMesh'):continue
    for variant in shader['passes'][0]['variants']:
        if 'ETC1_EXTERNAL_ALPHA' in variant['keywords']:continue
        asm=(root/'out/direct-renderer/m4bc/shaders'/(variant['vs']['sha256']+'.asm')).read_text()
        math=[x for x in asm.splitlines() if x.startswith(('mul ','mad ','add '))]
        rows.append(dict(shader=shader['name'],keywords=variant['keywords'],first_instructions=math[:9]))
(out/'precision-audit.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print('PASS original VS and before/after CS disassembly saved; arithmetic evidence only, live Unity pixels unmeasured')

"""M4b: join Unity's entry/parameter indices with stripped DXBC, fail closed.

No game process or installation writes. The output is a versioned binary resource
and a readable audit (including source hashes and exact disassembly).
"""
import argparse
import ctypes
import hashlib
import json
import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TARGETS = {'Sprites/Tile', 'Sprites/Default', 'Sprites/Mask', 'ADOFAI/FloorMesh', 'ADOFAI/Frames',
           'ADOFAI/TiledSpriteWithGap', 'ADOFAI/HoldShader', 'Legacy Shaders/Particles/Additive'}


class Reader:
    def __init__(self, b): self.b, self.p = b, 0
    def integer(self):
        n, = struct.unpack_from('<i', self.b, self.p); self.p += 4; return n
    def string(self):
        n = self.integer()
        assert 0 <= n <= len(self.b)-self.p
        s = self.b[self.p:self.p+n].decode('utf-8'); self.p = (self.p+n+3)&~3
        return s


def parameters(b):
    r = Reader(b)
    assert r.integer() == 202012090, 'unrecognized Unity parameter format'
    cbs = {}
    for _ in range(r.integer()):
        name, size = r.string(), r.integer()
        vs = []
        for _ in range(r.integer()):
            vn = r.string()
            typ, rows, cols, array, matrix, offset = [r.integer() for _ in range(6)]
            assert typ in (0,1,2,3) and 1 <= rows <= 4 and 1 <= cols <= 4 and array == 0, (vn,typ,rows,cols,array,matrix,offset)
            vs.append(dict(name=vn, offset=offset, type=typ, rows=rows, cols=cols, matrix=matrix))
        assert r.integer() == 0, 'struct cbuffer unsupported'
        if name: cbs[name] = dict(name=name, size=size, variables=vs)
        else: assert size == 0 and not vs
    bindings, textures = {}, []
    for _ in range(r.integer()):
        name, array, slot, kind = r.string(), r.integer(), r.integer(), r.integer()
        if array == 1:
            assert kind == 0
            bindings[name] = slot
        elif array == 0:
            dim=r.integer()
            textures.append(dict(name=name,slot=slot,sampler=kind,dim=dim))
        else: raise ValueError(('unrecognized resource binding',name,array,slot,kind))
    assert r.p == len(b), ('parameter trailing bytes',r.p,len(b))
    return cbs, bindings, textures


def disassembler():
    dll = ctypes.WinDLL('d3dcompiler_47.dll')
    dll.D3DDisassemble.argtypes = [ctypes.c_void_p,ctypes.c_size_t,ctypes.c_uint,ctypes.c_char_p,ctypes.POINTER(ctypes.c_void_p)]
    dll.D3DDisassemble.restype = ctypes.c_long
    def disassemble(code):
        out = ctypes.c_void_p(); data = ctypes.create_string_buffer(code)
        hr = dll.D3DDisassemble(data,len(code),0,None,ctypes.byref(out))
        assert hr >= 0 and out
        vt = ctypes.cast(ctypes.cast(out,ctypes.POINTER(ctypes.c_void_p))[0],ctypes.POINTER(ctypes.c_void_p))
        get = ctypes.WINFUNCTYPE(ctypes.c_void_p,ctypes.c_void_p)(vt[3])
        size = ctypes.WINFUNCTYPE(ctypes.c_size_t,ctypes.c_void_p)(vt[4])
        try: return ctypes.string_at(get(out),size(out)).rstrip(b'\0').decode('ascii')
        finally: ctypes.WINFUNCTYPE(ctypes.c_ulong,ctypes.c_void_p)(vt[2])(out)
    return disassemble


def chunks(code):
    assert code[:4] == b'DXBC' and struct.unpack_from('<I',code,24)[0] == len(code)
    result = {}
    for i in range(struct.unpack_from('<I',code,28)[0]):
        off, = struct.unpack_from('<I',code,32+4*i)
        tag, size = struct.unpack_from('<4sI',code,off)
        assert off+8+size <= len(code)
        result[tag] = code[off+8:off+8+size]
    return result


def signature(code, tag=b'ISGN'):
    b = chunks(code)[tag]; n, = struct.unpack_from('<I',b)
    result = []
    for i in range(n):
        off, index, system, typ, reg, mask = struct.unpack_from('<6I',b,8+i*24)
        name = b[off:b.index(b'\0',off)].decode('ascii')
        result.append(dict(name=name,index=index,system=system,type=typ,register=reg,mask=mask&255))
    return result


def merge_parameters(common, extra, names):
    cbs, bindings, textures = extra
    for cb in common['m_ConstantBuffers']:
        name = names[cb['m_NameIndex']]
        target = cbs.setdefault(name,dict(name=name,size=cb['m_Size'],variables=[]))
        assert target['size'] == cb['m_Size']
        for key, matrix in [('m_VectorParams',False),('m_MatrixParams',True)]:
            for v in cb[key]:
                assert v['m_ArraySize'] == 0
                value = dict(name=names[v['m_NameIndex']],offset=v['m_Index'],type=v['m_Type'],
                             rows=v['m_RowCount'] if matrix else 1,cols=4 if matrix else v['m_Dim'],matrix=int(matrix))
                prior = next((x for x in target['variables'] if x['name']==value['name']),None)
                if prior: assert prior == value
                else: target['variables'].append(value)
        assert not cb['m_StructParams']
    for b in common['m_ConstantBufferBindings']:
        name, slot = names[b['m_NameIndex']], b['m_Index']
        assert name not in bindings or bindings[name] == slot
        bindings[name] = slot
    for t in common['m_TextureParams']:
        value=dict(name=names[t['m_NameIndex']],slot=t['m_Index'],sampler=t['m_SamplerIndex'],dim=t['m_Dim'])
        prior=next((x for x in textures if x['name']==value['name']),None)
        if prior: assert prior==value
        else: textures.append(value)
    return cbs, bindings, textures


def stage(program, record, param_index, entries, names, keyword_names, disassemble, output):
    assert not program['m_CommonParameters']['m_BufferParams'] and not program['m_CommonParameters']['m_UAVParams'], 'buffer/UAV resources require a separate lease'
    entry = entries[record['m_BlobIndex']]
    r = Reader(entry); assert r.integer() == 202012090
    gpu = r.integer(); r.p += 16
    keywords = [r.string() for _ in range(r.integer())]
    expect = [keyword_names[i] for i in record['m_KeywordIndices']]
    assert sorted(keywords)==sorted(expect), ('keyword index mismatch',keywords,expect)
    n=r.integer(); code_with_header=entry[r.p:r.p+n]
    start=code_with_header.find(b'DXBC'); assert start >= 0
    size=struct.unpack_from('<I',code_with_header,start+24)[0]
    code=code_with_header[start:start+size]
    asm=disassemble(code)
    st='vs' if gpu in (15,16) else 'ps' if gpu in (17,18) else None
    assert st and asm.lstrip().startswith('//') and re.search(r'^'+st+r'_[45]_0',asm,re.M)
    try: cbs, bindings, textures=merge_parameters(program['m_CommonParameters'],parameters(entries[param_index]),names)
    except Exception:
        (output/('failed-params-'+hashlib.sha256(entries[param_index]).hexdigest()[:8]+'.bin')).write_bytes(entries[param_index]);raise
    declarations = {int(a):int(b)*16 for a,b in re.findall(r'dcl_constantbuffer CB(\d+)\[(\d+)\]',asm)}
    bound = []
    for name, slot in sorted(bindings.items(),key=lambda x:x[1]):
        cb = cbs[name]
        # The declaration can omit unused trailing metadata fields.
        assert slot in declarations and declarations[slot] <= cb['size'], (name,slot,declarations,cb['size'])
        cb=dict(cb,slot=slot,declared_size=declarations[slot])
        for v in cb['variables']:
            assert v['offset'] % 4 == 0 and v['offset']+4*v['rows']*v['cols'] <= cb['size']
        bound.append(cb)
    assert set(declarations)==set(bindings.values()), ('unmapped bytecode CB',declarations,bindings)
    # Every referenced DWORD component must be covered by named Unity metadata.
    # This detects partial/incorrect parameter-blob merges despite absent RDEF.
    covered={}
    for cb in bound:
        words=set()
        for v in cb['variables']:
            words.update(range(v['offset']//4,v['offset']//4+v['rows']*v['cols']))
        covered[cb['slot']]=words
    for slot,reg,swizzle in re.findall(r'cb(\d+)\[(\d+)\]\.([xyzw]+)',asm):
        used={int(reg)*4+'xyzw'.index(c) for c in swizzle}
        assert used<=covered.get(int(slot),set()), ('unnamed bytecode constant component',slot,reg,swizzle,used-covered.get(int(slot),set()))
    texdecl={int(s) for s in re.findall(r'dcl_resource_texture2d\s+\([^\n]+\)\s+t(\d+)',asm)}
    assert not re.search(r'dcl_(?:resource_(?!texture2d)|uav_)',asm), 'unsupported bytecode resource declaration'
    samplerdecl={int(s) for s in re.findall(r'dcl_sampler s(\d+)',asm)}
    assert texdecl == {t['slot'] for t in textures}, (texdecl,textures)
    assert samplerdecl == {t['sampler'] for t in textures if t['sampler']>=0}, (samplerdecl,textures)
    sig=signature(code)
    sha=hashlib.sha256(code).hexdigest()
    (output/(sha+'.dxbc')).write_bytes(code)
    (output/(sha+'.asm')).write_text(asm,encoding='ascii')
    return dict(stage=st,blob=record['m_BlobIndex'],parameter_blob=param_index,keywords=sorted(keywords),
                sha256=sha,code=code,cbs=bound,textures=textures,inputs=sig)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--deps',type=Path,default=Path('E:/StutterFixWork/wt-v14prod/out/asset-reader-deps'))
    p.add_argument('--game-data',type=Path,default=Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data'))
    p.add_argument('--output',type=Path,default=ROOT/'out/direct-renderer/m4bc/shaders')
    p.add_argument('--resource',type=Path,default=ROOT/'effects/direct-full/original-shaders.bin')
    args=p.parse_args(); sys.dont_write_bytecode=True;sys.path.insert(0,str(args.deps))
    import UnityPy, lz4.block
    args.output.mkdir(parents=True,exist_ok=True)
    disassemble=disassembler(); shaders={}; issues=[]; sources=[]; duplicates=[]; ambiguous=set()
    def canonical(s):
        passes=[]
        for p in s['passes']:
            state=dict(p['state']);state.pop('gpuProgramID',None)
            vs=[]
            for v in p['variants']:
                item={'keywords':v['keywords']}
                for key in ('vs','ps'):
                    st=v[key];item[key]={k:st[k] for k in ('sha256','cbs','textures','inputs')}
                vs.append(item)
            passes.append(dict(index=p['index'],grab=p['grab_before'],state=state,variants=vs))
        return json.dumps(dict(name=s['name'],keywords=s['keywords'],properties=s['properties'],passes=passes),sort_keys=True)
    paths=[args.game_data/'Resources/unity_builtin_extra',args.game_data/'Resources/unity default resources',
           args.game_data/'globalgamemanagers.assets',args.game_data/'resources.assets']+sorted(args.game_data.glob('sharedassets*.assets'))
    for asset in paths:
        env=UnityPy.load(str(asset))
        for obj in env.objects:
            if obj.type.name!='Shader': continue
            tree=obj.read_typetree(); parsed=tree.get('m_ParsedForm') or {}; name=parsed.get('m_Name') or tree.get('m_Name')
            if name not in TARGETS and not (name or '').startswith('Hidden/BlendModes/'): continue
            try:
                if not tree.get('compressedBlob') or 4 not in tree['platforms']: raise ValueError('no original D3D11 program in asset')
                i=tree['platforms'].index(4)
                blocks=[lz4.block.decompress(bytes(tree['compressedBlob'])[o:o+n],uncompressed_size=s) for o,n,s in zip(tree['offsets'][i],tree['compressedLengths'][i],tree['decompressedLengths'][i])]
                raw=blocks[0]; count=struct.unpack_from('<I',raw)[0]; entries=[]
                for j in range(count):
                    off,n,segment=struct.unpack_from('<III',raw,4+j*12)
                    assert off+n<=len(blocks[segment]);entries.append(blocks[segment][off:off+n])
                passes=[]
                for si,sub in enumerate(parsed['m_SubShaders'][:1]):
                    grab = False
                    for pi,ps in enumerate(sub['m_Passes']):
                        if ps['m_Type']==2:
                            grab=True;continue
                        if ps['m_Type']!=0: raise ValueError('UsePass requires Unity fallback')
                        names={v:k for k,v in ps['m_NameIndices']}; stages={}
                        for kind in ('progVertex','progFragment'):
                            prog=ps[kind]; records=prog['m_PlayerSubPrograms'][3];params=prog['m_ParameterBlobIndices'][3]
                            assert len(records)==len(params)
                            stages[kind]=[stage(prog,r,p,entries,names,parsed['m_KeywordNames'],disassemble,args.output) for r,p in zip(records,params) if not any(parsed['m_KeywordNames'][k] in ('INSTANCING_ON','SOFTPARTICLES_ON','FOG_LINEAR','FOG_EXP','FOG_EXP2') for k in r['m_KeywordIndices'])]
                        vs,fs=stages.values(); masks={tuple(x['keywords']) for x in vs}|{tuple(x['keywords']) for x in fs}
                        variants=[]
                        for keywords in sorted(masks):
                            v=next((x for x in vs if x['keywords']==list(keywords)),None)
                            f=next((x for x in fs if x['keywords']==list(keywords)),None)
                            if v is None or f is None: raise ValueError('stage keyword sets do not pair exactly')
                            # Cross-check link signature semantic/type/mask, ignoring SV_Position.
                            outs=signature(v['code'],b'OSGN')
                            for x in f['inputs']:
                                if x['system']: continue
                                assert any(o['name']==x['name'] and o['index']==x['index'] and o['type']==x['type'] and o['mask']&x['mask']==x['mask'] for o in outs)
                            variants.append(dict(keywords=list(keywords),vs=v,ps=f))
                        passes.append(dict(index=pi,grab_before=grab,state=ps['m_State'],variants=variants));grab=False
                if not passes or any(not p['variants'] for p in passes): raise ValueError('no executable pass (UsePass/fallback)')
                candidate=dict(name=name,source=asset.name,path_id=obj.path_id,keywords=parsed['m_KeywordNames'],properties=parsed['m_PropInfo']['m_Props'],passes=passes)
                if name in shaders:
                    same=canonical(shaders[name])==canonical(candidate)
                    duplicates.append(dict(shader=name,source=asset.name,path_id=obj.path_id,exact_program_and_binding_match=same))
                    if not same: ambiguous.add(name);raise ValueError('duplicate shader name has different verified programs/bindings')
                    continue
                shaders[name]=candidate
                print('OK',name,'variants',sum(len(p['variants']) for p in passes),flush=True)
            except Exception as e:
                issues.append(dict(shader=name,source=asset.name,reason=str(e)))
                print('FALLBACK',name,repr(e),flush=True)
        sources.append(dict(path=str(asset),sha256=hashlib.sha256(asset.read_bytes()).hexdigest()))
    for name in ambiguous:shaders.pop(name,None)
    # Stable wire resource. BinaryReader matches this exact schema; no JSON parser in frame loop.
    b=bytearray()
    def integer(n): b.extend(struct.pack('<i',n))
    def string(s):
        x=s.encode('utf-8'); integer(len(x));b.extend(x)
    def stateval(x):
        b.extend(struct.pack('<f',float(x['val'])));string('' if x['name'] in ('','<noninit>') else x['name'])
    integer(0x34465244);integer(1);integer(len(shaders))
    for sh in sorted(shaders.values(),key=lambda x:x['name']):
        string(sh['name']);integer(len(sh['keywords']))
        for kw in sh['keywords']:string(kw)
        integer(len(sh['properties']))
        for prop in sh['properties']:
            string(prop['m_Name']);integer(prop['m_Type'])
            b.extend(struct.pack('<4f',*[prop[f'm_DefValue[{i}]'] for i in range(4)]));string(prop['m_DefTexture']['m_DefaultName'])
        integer(len(sh['passes']))
        for ps in sh['passes']:
            integer(ps['index']);integer(int(ps['grab_before']));s=ps['state'];blend=s['rtBlend0']
            for k in ('srcBlend','destBlend','srcBlendAlpha','destBlendAlpha','blendOp','blendOpAlpha','colMask'):stateval(blend[k])
            for k in ('zClip','zTest','zWrite','culling','offsetFactor','offsetUnits','alphaToMask','stencilReadMask','stencilWriteMask','stencilRef'):stateval(s[k])
            for face in ('stencilOp','stencilOpFront','stencilOpBack'):
                for k in ('pass','fail','zFail','comp'):stateval(s[face][k])
            integer(len(ps['variants']))
            for v in ps['variants']:
                integer(len(v['keywords']))
                for kw in v['keywords']:string(kw)
                for kind in ('vs','ps'):
                    st=v[kind];integer(len(st['code']));b.extend(st['code']);integer(len(st['cbs']))
                    for cb in st['cbs']:
                        string(cb['name']);integer(cb['slot']);integer(cb['size']);integer(cb['declared_size']);integer(len(cb['variables']))
                        for x in cb['variables']:
                            string(x['name'])
                            for k in ('offset','type','rows','cols','matrix'):integer(x[k])
                    integer(len(st['textures']))
                    for x in st['textures']:
                        string(x['name']);integer(x['slot']);integer(x['sampler'])
                    integer(len(st['inputs']))
                    for x in st['inputs']:
                        string(x['name'])
                        for k in ('index','system','type','register','mask'):integer(x[k])
    args.resource.parent.mkdir(parents=True,exist_ok=True);args.resource.write_bytes(b)
    audit=dict(schema=1,sources=sources,shaders=list(shaders.values()),fallbacks=issues,duplicates=duplicates,resource_sha256=hashlib.sha256(b).hexdigest())
    (args.output/'bindings.json').write_text(json.dumps(audit,default=lambda x:None if isinstance(x,bytes) else x,ensure_ascii=False,indent=2),encoding='utf-8')
    assert {'Sprites/Tile','ADOFAI/FloorMesh'}<=shaders.keys(), 'required shaders failed validation'
    print('RESOURCE',len(b),'bytes',len(shaders),'shaders; issues',len(issues))


if __name__=='__main__':main()

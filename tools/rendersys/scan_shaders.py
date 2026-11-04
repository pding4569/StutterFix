"""셰이더 안의 D3D11 바이트코드를 꺼내 명령어·텍스처 샘플 수를 잰다."""
import collections,ctypes,os,re,statistics as st,struct,sys,UnityPy
import lz4.block as L
ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(),0x4000)
D=r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data"
O=sys.argv[1]
d3d=ctypes.WinDLL("d3dcompiler_47.dll")
class Blob(ctypes.Structure): pass
def disasm(b):
    out=ctypes.c_void_p()
    hr=d3d.D3DDisassemble(b,ctypes.c_size_t(len(b)),0,None,ctypes.byref(out))
    if hr!=0: return None
    vt=ctypes.cast(ctypes.cast(out,ctypes.POINTER(ctypes.c_void_p))[0],ctypes.POINTER(ctypes.c_void_p))
    ptr=ctypes.WINFUNCTYPE(ctypes.c_void_p,ctypes.c_void_p)(vt[3])(out)
    size=ctypes.WINFUNCTYPE(ctypes.c_size_t,ctypes.c_void_p)(vt[4])(out)
    s=ctypes.string_at(ptr,size).decode("latin1")
    ctypes.WINFUNCTYPE(ctypes.c_ulong,ctypes.c_void_p)(vt[2])(out)
    return s
env=UnityPy.load(*[os.path.join(D,n) for n in os.listdir(D) if n.endswith(".assets") or n=="globalgamemanagers"])
rows=[];seen=set()
for o in env.objects:
    if o.type.name!="Shader": continue
    try: t=o.read_typetree()
    except Exception: continue
    nm=(t.get("m_ParsedForm") or {}).get("m_Name") or t.get("m_Name")
    if not nm or nm in seen: continue
    seen.add(nm)
    plats=t.get("platforms",[]);offs=t.get("offsets",[]);cl=t.get("compressedLengths",[]);dl=t.get("decompressedLengths",[]);blob=bytes(t.get("compressedBlob",[]))
    ps=[];vs=0;passes=sum(len(ss.get("m_Passes",[])) for ss in (t.get("m_ParsedForm") or {}).get("m_SubShaders",[]))
    for i,pl in enumerate(plats):
        if pl!=4: continue
        of=offs[i];cls=cl[i];dls=dl[i]
        of=of[0] if isinstance(of,list) else of;cls=cls[0] if isinstance(cls,list) else cls;dls=dls[0] if isinstance(dls,list) else dls
        try: raw=L.decompress(blob[of:of+cls],uncompressed_size=dls)
        except Exception as e: continue
        pos=0
        while True:
            k=raw.find(b"DXBC",pos)
            if k<0: break
            size=struct.unpack_from("<I",raw,k+24)[0]
            if size<32 or k+size>len(raw): pos=k+4;continue
            s=disasm(raw[k:k+size]);pos=k+size
            if not s: continue
            ins=[l.strip() for l in s.split("\n") if l.strip() and not l.lstrip().startswith("//") and not l.strip().startswith("dcl_")]
            head=ins[0] if ins else ""
            body=ins[1:]
            samp=sum(1 for l in body if l.startswith("sample") or l.startswith("ld") or l.startswith("gather"))
            loops=sum(1 for l in body if l.startswith("loop"))
            if head.startswith("ps_"): ps.append((len(body),samp,loops))
            else: vs+=1
    if ps:
        n=[x[0] for x in ps];sm=[x[1] for x in ps]
        rows.append((nm,passes,len(ps),int(st.median(n)),max(n),int(st.median(sm)),max(sm),max(x[2] for x in ps)))
    else: rows.append((nm,passes,0,0,0,0,0,0))
rows.sort(key=lambda r:-r[4])
open(O+"/shader-stats.tsv","w",encoding="utf-8").write("셰이더\t패스\tPS변형\t명령중앙\t명령최대\t샘플중앙\t샘플최대\t루프\n"+"\n".join("\t".join(map(str,r)) for r in rows))
print("SH_DONE",len(rows))

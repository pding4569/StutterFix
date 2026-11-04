"""타일·장식·공 등 런타임 프리팹의 트리를 구성 요소 활성 상태와 함께 뽑는다."""
import os,sys,UnityPy
D=r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data"
O=sys.argv[1]
import ctypes;ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(),0x4000)
names=[n for n in os.listdir(D) if n=="globalgamemanagers.assets" or n=="resources.assets" or (n.startswith("sharedassets4") and n.endswith(".assets"))]
env=UnityPy.load(*[os.path.join(D,n) for n in names])
files={}
for o in env.objects: files.setdefault(id(o.assets_file),{})[o.path_id]=o
def deref(owner,p):
    try:
        fid,pid=p["m_FileID"],p["m_PathID"]
        if pid==0: return None
        f=owner.assets_file
        if fid!=0:
            ext=f.externals[fid-1];b=os.path.basename(ext.path).lower()
            f=next((x for k,x in f.parent.files.items() if os.path.basename(k).lower()==b),None)
            if f is None: return None
        return files.get(id(f),{}).get(pid)
    except Exception: return None
def script(o):
    try:
        r=o.reader;r.Position=o.byte_start;r.read_int();r.read_long();en=r.read_u_byte();r.align_stream();fid=r.read_int();pid=r.read_long();m=deref(o,{"m_FileID":fid,"m_PathID":pid})
        t=m.read_typetree() if m is not None else {};return t.get("m_ClassName","?"),en
    except Exception as e: return "MB?",1
def comp(c):
    n=c.type.name
    if n in("Transform","RectTransform"): return None
    if n=="MonoBehaviour":
        s,en=script(c);return s+("" if en else "(off)")
    try:
        t=c.read_typetree();en=t.get("m_Enabled",1);ex=""
        if n in("SpriteRenderer","MeshRenderer","ParticleSystemRenderer"): ex=f" 레이어={t.get('m_SortingLayer')},순서={t.get('m_SortingOrder')},재질={len(t.get('m_Materials',[]))}"
        return n+("" if en else "(off)")+ex
    except Exception: return n
def walk(o,d,out):
    g=o.read_typetree();cs=[];tr=None
    for c in g.get("m_Component",[]):
        co=deref(o,c.get("component",c))
        if co is None: continue
        if co.type.name in("Transform","RectTransform"): tr=co
        v=comp(co)
        if v: cs.append(v)
    out.append("  "*d+f"{g.get('m_Name')}{'' if g.get('m_IsActive') else ' (비활성)'} L{g.get('m_Layer')} ["+", ".join(cs)+"]")
    if tr is not None:
        for ch in tr.read_typetree().get("m_Children",[]):
            co=deref(tr,ch)
            if co is None: continue
            go=deref(co,co.read_typetree().get("m_GameObject",{}))
            if go is not None: walk(go,d+1,out)
W={"VisualDecoration","TextDecoration","ParticleDecoration","ObjectDecoration","PrefabDecoration","Floor","floorMeshLong","HitTextMesh","HitText","Planet","PlanetRed","PlanetBlue","HoldRenderer","Hold"}
out=[]
for o in env.objects:
    if o.type.name=="GameObject":
        try: g=o.read_typetree()
        except Exception: continue
        n=g.get("m_Name","")
        if n in W or n.endswith("Decoration") and not n.startswith("ff"):
            # 부모 없는 것만
            try:
                tr=next(deref(o,c.get("component",c)) for c in g["m_Component"] if deref(o,c.get("component",c)).type.name in("Transform","RectTransform"))
                if tr.read_typetree().get("m_Father",{}).get("m_PathID",1)!=0: continue
            except Exception: continue
            out.append(f"#### {o.assets_file.name} {n}");walk(o,0,out)
open(O+"/prefabs.txt","w",encoding="utf-8").write("\n".join(out))
print("PF_DONE",len(out))

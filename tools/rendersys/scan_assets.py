"""게임 에셋 전체를 읽어 장면 구조·카메라·재질·셰이더·프리팹·설정을 표로 뽑는다. 텍스처 내용은 읽지 않는다."""
import collections,glob,json,os,sys,UnityPy
D=r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data"
O=sys.argv[1]
import ctypes;ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(),0x4000)
def clean(v):
    if isinstance(v,float) and v!=v: return "NaN"
    return v
names=[n for n in os.listdir(D) if n=="globalgamemanagers" or n.startswith("level") and "." not in n or n.endswith(".assets")]
names.sort(key=lambda n:(not n.startswith("global"),len(n),n))
env=UnityPy.load(*[os.path.join(D,n) for n in names])
print("files",len(env.files),"objects",len(env.objects),flush=True)
# 경로 ID로 객체 찾기
files={}
for o in env.objects: files.setdefault(id(o.assets_file),{})[o.path_id]=o
def deref(owner,p):
    try:
        fid,pid=p["m_FileID"],p["m_PathID"]
        if pid==0: return None
        f=owner.assets_file
        if fid!=0:
            ext=f.externals[fid-1]; f=next((x for k,x in f.parent.files.items() if os.path.basename(k).lower()==os.path.basename(ext.path).lower() or k.lower()==os.path.basename(ext.path).lower()),None)
            if f is None: return None
        return files.get(id(f),{}).get(pid)
    except Exception: return None
cache={}
def tt(o):
    k=(id(o.assets_file),o.path_id)
    if k not in cache:
        try: cache[k]=o.read_typetree()
        except Exception as e: cache[k]={"__err":str(e)}
    return cache[k]
def script(o):
    try:
        r=o.reader;r.Position=o.byte_start
        r.read_int();r.read_long();r.read_u_byte();r.align_stream()
        fid=r.read_int();pid=r.read_long()
        m=deref(o,{"m_FileID":fid,"m_PathID":pid})
        if m is None: return "스크립트없음"
        t=tt(m);return (t.get("m_Namespace")+"." if t.get("m_Namespace") else "")+t.get("m_ClassName","?")
    except Exception as e: return "MonoBehaviour?"
types=collections.Counter()
for o in env.objects: types[(o.assets_file.name,o.type.name)]+=1
with open(O+"/asset-types.tsv","w",encoding="utf-8") as f:
    f.write("파일\t형식\t개수\n")
    for (a,b),c in sorted(types.items()): f.write(f"{a}\t{b}\t{c}\n")
tot=collections.Counter()
for (a,b),c in types.items(): tot[b]+=c
print(tot.most_common(40),flush=True)
# 게임 오브젝트 → 구성 요소
TR=("Transform","RectTransform")
rows=[];cams=[];scriptcount=collections.Counter();rendercount=collections.Counter();canv=[]
for o in env.objects:
    if o.type.name!="GameObject": continue
    g=tt(o)
    if "__err" in g: continue
    comps=[];tr=None
    for c in g.get("m_Component",[]):
        co=deref(o,c.get("component",c))
        if co is None: comps.append("?");continue
        n=co.type.name
        if n in TR: tr=co
        if n=="MonoBehaviour":
            n=script(co); scriptcount[n]+=1
        elif "Renderer" in n or n in("Camera","Canvas","SpriteMask","ParticleSystem"): rendercount[n]+=1
        comps.append(n)
        if co.type.name=="Camera":
            t=tt(co);cams.append(dict(file=o.assets_file.name,go=g.get("m_Name"),enabled=t.get("m_Enabled"),clear=t.get("m_ClearFlags"),depth=t.get("m_Depth"),mask=t.get("m_CullingMask",{}).get("m_Bits"),ortho=t.get("orthographic"),size=t.get("orthographic size"),target=t.get("m_TargetTexture"),hdr=t.get("m_HDR"),msaa=t.get("m_AllowMSAA"),near=t.get("near clip plane"),far=t.get("far clip plane")))
    parent=None
    if tr is not None:
        tv=tt(tr);po=deref(tr,tv.get("m_Father",{}));
        if po is not None:
            pv=tt(po);gp=deref(po,pv.get("m_GameObject",{}))
            parent=gp.path_id if gp is not None else None
    rows.append((o.assets_file.name,o.path_id,parent,g.get("m_Name"),g.get("m_IsActive"),g.get("m_Layer"),",".join(comps)))
with open(O+"/gameobjects.tsv","w",encoding="utf-8") as f:
    f.write("파일\tid\t부모\t이름\t활성\t레이어\t구성요소\n")
    for r in rows: f.write("\t".join(str(x) for x in r)+"\n")
json.dump(cams,open(O+"/cameras.json","w",encoding="utf-8"),ensure_ascii=False,indent=0,default=str)
with open(O+"/scripts-in-assets.tsv","w",encoding="utf-8") as f:
    for k,v in scriptcount.most_common(): f.write(f"{v}\t{k}\n")
with open(O+"/renderers-in-assets.tsv","w",encoding="utf-8") as f:
    for k,v in rendercount.most_common(): f.write(f"{v}\t{k}\n")
# 재질과 셰이더
shaders={};mats=[]
for o in env.objects:
    if o.type.name=="Shader":
        try:
            t=tt(o);shaders[(o.assets_file.name,o.path_id)]=t.get("m_ParsedForm",{}).get("m_Name") or t.get("m_Name")
        except Exception: pass
for o in env.objects:
    if o.type.name=="Material":
        t=tt(o);so=deref(o,t.get("m_Shader",{}));sn=shaders.get((so.assets_file.name,so.path_id)) if so is not None else None
        mats.append((o.assets_file.name,t.get("m_Name"),sn,t.get("m_CustomRenderQueue"),",".join(t.get("m_ValidKeywords",[]) or [])))
with open(O+"/materials.tsv","w",encoding="utf-8") as f:
    f.write("파일\t재질\t셰이더\t큐\t키워드\n")
    for r in mats: f.write("\t".join(str(x) for x in r)+"\n")
with open(O+"/shaders.tsv","w",encoding="utf-8") as f:
    c=collections.Counter(m[2] for m in mats)
    f.write("셰이더\t재질 수\n")
    for (a,b),n in sorted(shaders.items(),key=lambda kv:str(kv[1])): f.write(f"{n}\t{c.get(n,0)}\n")
# 설정
s={}
for o in env.objects:
    if o.assets_file.name=="globalgamemanagers" and o.type.name in("TagManager","QualitySettings","GraphicsSettings","TimeManager","BuildSettings","Physics2DSettings","AudioManager","PlayerSettings","ScriptMapper","RuntimeInitializeOnLoadManager"):
        s[o.type.name]=tt(o)
json.dump(s,open(O+"/settings.json","w",encoding="utf-8"),ensure_ascii=False,indent=0,default=str)
print("SCAN_DONE gameobjects",len(rows),"cameras",len(cams),"materials",len(mats),"shaders",len(shaders),"scripts",len(scriptcount),flush=True)

"""디컴파일 전체에서 클래스·프레임 함수·렌더링 API 사용을 표로 뽑는다."""
import collections,json,os,re,sys
R,O=sys.argv[1],sys.argv[2]
MSG="Awake OnEnable Start FixedUpdate Update LateUpdate OnPreCull OnBecameVisible OnBecameInvisible OnWillRenderObject OnPreRender OnRenderObject OnPostRender OnRenderImage OnGUI OnDisable OnDestroy OnApplicationFocus OnApplicationPause OnApplicationQuit OnMouseDown OnMouseUp OnMouseEnter OnMouseExit OnMouseOver OnMouseDrag OnMouseUpAsButton OnTriggerEnter2D OnTriggerStay2D OnTriggerExit2D OnCollisionEnter2D OnAudioFilterRead OnAnimatorMove".split()
API={"Graphics":r"\bGraphics\.\w+","GL":r"\bGL\.\w+","CommandBuffer":r"\bCommandBuffer\b","RenderTexture":r"\bRenderTexture\b","Camera":r"\bCamera\b","Blit":r"\bBlit\b","Material":r"\bMaterial\b","Shader":r"\bShader\.\w+","Mesh":r"\bMesh\b","SpriteRenderer":r"\bSpriteRenderer\b","MeshRenderer":r"\bMeshRenderer\b","LineRenderer":r"\bLineRenderer\b","TrailRenderer":r"\bTrailRenderer\b","ParticleSystem":r"\bParticleSystem\b","SpriteMask":r"\bSpriteMask\b","Canvas":r"\bCanvas\b","Image":r"\bImage\b","TMP":r"\bTextMeshPro\w*\b","Text":r"\bText\b","DOTween":r"\bDOTween\b|\.DO[A-Z]\w+\(","Transform":r"\.(position|localPosition|rotation|localRotation|localScale|eulerAngles|localEulerAngles)\s*=[^=]","Audio":r"\bAudioSource\b|\bAudioManager\b","Instantiate":r"\bInstantiate\b","Coroutine":r"\bStartCoroutine\b","Harmony":r"\bHarmony\b"}
DECL=re.compile(r"^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected|sealed|abstract|static|partial|\s)*\b(class|struct|interface|enum)\s+(\w+)(?:<[^>]*>)?(?:\s*:\s*([^\n{]+))?",re.M)
rows=[];frame=[];apis=collections.defaultdict(collections.Counter)
for asm in ("Assembly-CSharp","Assembly-CSharp-firstpass"):
    for dp,_,fs in os.walk(os.path.join(R,asm)):
        for fn in fs:
            if not fn.endswith(".cs"): continue
            p=os.path.join(dp,fn);s=open(p,encoding="utf-8",errors="replace").read();lines=s.count("\n")+1
            rel=os.path.relpath(p,R)
            ds=[(m.group(1),m.group(2),(m.group(3) or "").strip()) for m in DECL.finditer(s)]
            top=next((d for d in ds if d[0]=="class" and d[1]==fn[:-3]),ds[0] if ds else ("?",fn[:-3],""))
            msgs=[m for m in MSG if re.search(r"\b(?:void|IEnumerator)\s+"+m+r"\s*\(",s)]
            for k,r in API.items():
                n=len(re.findall(r,s))
                if n: apis[k][rel]=n
            rows.append(dict(asm=asm,file=rel,cls=top[1],kind=top[0],base=top[2].split(",")[0].strip() if top[2] else "",lines=lines,msgs=msgs,subtypes=len(ds)))
with open(O+"/code-classes.tsv","w",encoding="utf-8") as f:
    f.write("어셈블리\t파일\t클래스\t종류\t부모\t줄\t유니티 함수\n")
    for r in sorted(rows,key=lambda r:-r["lines"]): f.write("\t".join(str(r[k]) if k!="msgs" else ",".join(r[k]) for k in("asm","file","cls","kind","base","lines","msgs"))+"\n")
with open(O+"/code-frame-functions.tsv","w",encoding="utf-8") as f:
    f.write("클래스\t부모\t줄\tUpdate류\t렌더 콜백\n")
    for r in sorted(rows,key=lambda r:-r["lines"]):
        u=[m for m in r["msgs"] if m in("Update","LateUpdate","FixedUpdate","OnGUI")]
        v=[m for m in r["msgs"] if m in("OnPreCull","OnPreRender","OnPostRender","OnRenderImage","OnRenderObject","OnWillRenderObject","OnBecameVisible","OnBecameInvisible")]
        if u or v: f.write(f"{r['cls']}\t{r['base']}\t{r['lines']}\t{','.join(u)}\t{','.join(v)}\n")
json.dump({k:dict(v.most_common(40)) for k,v in apis.items()},open(O+"/code-api-use.json","w",encoding="utf-8"),ensure_ascii=False,indent=0)
pre=collections.Counter();l=collections.Counter()
for r in rows:
    m=re.match(r"(scn|scr|ffx|CameraFilterPack|Rd|RD|Level|Editor|Prop|PropertyControl|DG|TMPro|Steam|Mobile|Dialog|UI|Sky)",r["cls"]);k=m.group(1) if m else ("폴더:"+r["file"].split(os.sep)[1] if r["file"].count(os.sep)>1 else "기타")
    pre[k]+=1;l[k]+=r["lines"]
with open(O+"/code-groups.tsv","w",encoding="utf-8") as f:
    f.write("묶음\t파일\t줄\n")
    for k,v in l.most_common(): f.write(f"{k}\t{pre[k]}\t{v}\n")
print("files",len(rows),"lines",sum(r["lines"] for r in rows),"frame",sum(1 for r in rows if set(r["msgs"])&{"Update","LateUpdate","FixedUpdate","OnGUI"}),"render",sum(1 for r in rows if set(r["msgs"])&{"OnPreCull","OnPreRender","OnPostRender","OnRenderImage","OnRenderObject"}))

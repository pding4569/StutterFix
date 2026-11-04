"""맵 파일 전체에서 이벤트·장식·효과 통계를 뽑는다. 맵별 표는 저장소 밖에 쓰고 맵 이름은 문서에 넣지 않는다."""
import collections,ctypes,json,os,re,sys
ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(),0x4000)
ROOT,OUT=sys.argv[1],sys.argv[2]
ET=re.compile(r'"eventType"\s*:\s*"(\w+)"')
F=lambda k:re.compile(r'"'+k+r'"\s*:\s*"?([^",}\]]*)"?')
BM,MK,HB,SY,LC,DP,PX,FL,DU,TC,IM,RT=[F(k) for k in("blendMode","maskingType","hitbox","syncFloorDepth","lockToCamera","depth","parallax","filter","duration","trackColorType","decorationImage","relativeTo")]
ADF=re.compile(r'"filterProperties"')
rows=[]
for dp,ds,fs in os.walk(ROOT):
    if "헤르츠" in dp: ds[:]=[];continue
    for fn in fs:
        if not fn.lower().endswith(".adofai"): continue
        p=os.path.join(dp,fn)
        try: s=open(p,encoding="utf-8-sig",errors="replace").read()
        except Exception: continue
        r=collections.Counter();r["bytes"]=len(s)
        m=re.search(r'"angleData"\s*:\s*\[([^\]]*)\]',s)
        r["tiles"]=m.group(1).count(",")+1 if m else 0
        if not m:
            m=re.search(r'"pathData"\s*:\s*"([^"]*)"',s);r["tiles"]=len(m.group(1)) if m else 0
        imgs=set();flt=collections.Counter()
        for line in s.split("\n"):
            g=ET.search(line)
            if not g: continue
            e=g.group(1);r["ev:"+e]+=1
            if e in("AddDecoration","AddText","AddObject","AddParticle"):
                g2=BM.search(line)
                if g2 and g2.group(1) not in("None",""): r["deco_blend"]+=1;r["blend:"+g2.group(1)]+=1
                g2=MK.search(line)
                if g2 and g2.group(1) not in("None",""): r["deco_mask"]+=1
                g2=HB.search(line)
                if g2 and g2.group(1) not in("None",""): r["deco_hitbox"]+=1
                if (g2:=SY.search(line)) and g2.group(1) in("Enabled","true"): r["deco_syncfloor"]+=1
                if (g2:=LC.search(line)) and g2.group(1) in("Enabled","true"): r["deco_lockcam"]+=1
                if (g2:=DP.search(line)):
                    try: r["deco_front" if float(g2.group(1))<0 else "deco_back"]+=1
                    except: pass
                g2=re.search(r'"parallax"\s*:\s*\[\s*([-\d.]+)\s*,\s*([-\d.]+)',line)
                if g2 and (float(g2.group(1))!=0 or float(g2.group(2))!=0): r["deco_parallax"]+=1
                if (g2:=RT.search(line)): r["reltype:"+g2.group(1)]+=1
                if (g2:=IM.search(line)) and g2.group(1): imgs.add(g2.group(1))
            elif e=="MoveDecorations":
                if (g2:=DU.search(line)):
                    try:
                        if float(g2.group(1))==0: r["movedeco_zero"]+=1
                    except: pass
            elif e=="MoveTrack":
                if (g2:=DU.search(line)):
                    try:
                        if float(g2.group(1))==0: r["movetrack_zero"]+=1
                    except: pass
            elif e=="SetFilter":
                if (g2:=FL.search(line)): flt[g2.group(1)]+=1
            elif e in("ColorTrack","RecolorTrack"):
                if (g2:=TC.search(line)) and g2.group(1) not in("Single",""): r["trackcolor_anim"]+=1;r["tc:"+g2.group(1)]+=1
        r["images"]=len(imgs)
        for k,v in flt.items(): r["flt:"+k]+=v
        rows.append(dict(r,_p=os.path.relpath(p,ROOT)))
json.dump(rows,open(OUT,"w",encoding="utf-8"),ensure_ascii=False)
print("SV_DONE",len(rows))

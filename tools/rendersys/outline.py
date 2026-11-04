"""클래스 파일의 함수 목록과 줄 수를 뽑는다. python outline.py <decompiled> <출력> 클래스..."""
import os,re,sys
R,O=sys.argv[1],sys.argv[2]
idx={}
for dp,_,fs in os.walk(os.path.join(R,"Assembly-CSharp")):
    for f in fs:
        if f.endswith(".cs"): idx.setdefault(f[:-3],os.path.join(dp,f))
for dp,_,fs in os.walk(os.path.join(R,"Assembly-CSharp-firstpass")):
    for f in fs:
        if f.endswith(".cs"): idx.setdefault(f[:-3],os.path.join(dp,f))
M=re.compile(r"^(\t|\t\t)(?:(?:public|private|protected|internal|static|virtual|override|sealed|abstract|async|new|unsafe|extern)\s+)*([\w<>\[\],\. ]+?)\s+(\w+)\s*(?:<[^>]*>)?\(([^)]*)\)\s*$")
with open(O,"w",encoding="utf-8") as out:
    for c in sys.argv[3:]:
        p=idx.get(c)
        if not p: out.write(f"## {c} 없음\n");continue
        L=open(p,encoding="utf-8",errors="replace").read().split("\n")
        ms=[]
        for i,l in enumerate(L):
            m=M.match(l)
            if m and i+1<len(L) and L[i+1].strip()=="{":
                ind=m.group(1)
                j=i+2
                while j<len(L) and L[j].rstrip()!=ind+"}": j+=1
                ms.append((j-i,m.group(3),i+1))
        out.write(f"## {c} {os.path.relpath(p,R)} {len(L)}줄\n")
        out.write(" ".join(f"{n}:{k}@{ln}" for k,n,ln in sorted(ms,key=lambda x:x[2]))+"\n")

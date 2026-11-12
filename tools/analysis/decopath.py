"""장식 경로 안쪽: 진입점(scrDecorationManager.LateUpdate / DOTweenComponent.Update) 안에서
   가장 안쪽 StutterFix.* 함수와 그 함수가 부르는 바깥 함수(엔진 API 등)를 집계한다.  python decopath.py <frames> [이름]"""
import sys, collections
fn=sys.argv[1]; name=sys.argv[2] if len(sys.argv)>2 else fn
S=[]
for ln in open(fn,encoding='utf-8',errors='replace'):
    t=[x for x in ln.rstrip('\n').split(';') if x]
    t=[('[Unity] ?'+x[2:]) if x.startswith('U:') else (x[2:] if x[:2] in ('M:','X:') else x) for x in t]
    if t: S.append(list(reversed(t)))
n=len(S)
def cl(x): return x.replace('(wrapper dynamic-method) MonoMod.Utils.DynamicMethodDefinition:','PATCHED:').replace('(wrapper managed-to-native) ','native:').replace('UnityEngine.','UE.')[:88]
for entry in ('scrDecorationManager.LateUpdate','DOTweenComponent.Update'):
    sub=[s for s in S if any(entry in x for x in s)]
    print(f"\n#### [{name}] {entry}: {100*len(sub)/n:.2f}% of samples")
    c=collections.Counter(); own=collections.Counter()
    for s in sub:
        last=-1
        for i,x in enumerate(s):
            if x.startswith('StutterFix.'): last=i
        if last<0: c[('(우리 코드 밖)', cl(s[-1]))]+=1; continue
        own[cl(s[last])]+=1
        callee=cl(s[last+1]) if last+1<len(s) else '(자기 자신)'
        c[(cl(s[last]),callee)]+=1
    print("  -- 가장 안쪽 우리 함수(표본 %)")
    for k,v in own.most_common(9): print(f"    {100*v/n:5.2f}%  {k}")
    print("  -- 우리 함수 -> 바깥 호출")
    for (a,b),v in c.most_common(14): print(f"    {100*v/n:5.2f}%  {a}  ->  {b}")

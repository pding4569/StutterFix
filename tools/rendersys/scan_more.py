"""스크립트 실행 순서와 설정값 요약."""
import json,os,sys,UnityPy
D=r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data"
O=sys.argv[1]
import ctypes;ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(),0x4000)
order=[]
env=UnityPy.load(os.path.join(D,"globalgamemanagers.assets"))
for o in env.objects:
    if o.type.name=="MonoScript":
        try:
            t=o.read_typetree();e=t.get("m_ExecutionOrder",0)
            if e: order.append((e,t.get("m_ClassName"),t.get("m_AssemblyName")))
        except Exception as x: pass
order.sort()
open(O+"/script-order.tsv","w",encoding="utf-8").write("\n".join(f"{a}\t{b}\t{c}" for a,b,c in order))
print("order",order)

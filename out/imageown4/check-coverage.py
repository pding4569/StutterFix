"""게임 없이 실제 준비/부분 갱신 메서드를 추출해 가짜 Unity 자원으로 검증."""
from pathlib import Path
import re
import subprocess

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent

def method(path, signature):
    source = (root / path).read_text(encoding='utf-8-sig')
    start = source.index(signature)
    brace = source.index('{', start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

methods = '\n'.join([
    method('GpuDecoImagePlayback.cs', 'static void RegisterGpuImageCollapseVariants('),
    method('GpuDecoImagePlayback.cs', 'static void RememberGpuImageCollapseAddresses('),
    method('GpuDecoDrawCollapse.cs', 'static void UpdateDrawCollapseTextureSlot('),
])
policy = (root / 'DrawCollapsePolicy.cs').read_text(encoding='utf-8-sig')
limits = '\n'.join(re.findall(r'internal const int (?:ArrayBindings|DirectBindings)=\d+;', policy))
program = r'''
using System;
using System.Collections.Generic;
using System.Linq;
enum TextureWrapMode { Clamp, Repeat }
class Texture2D {
    public int id,width=512,height=512,mipmapCount=1,tier;
    public string graphicsFormat="test";
    public TextureWrapMode wrapModeU,wrapModeV;
    public int GetInstanceID()=>id;
    public static implicit operator bool(Texture2D x)=>x!=null;
}
class Sprite {
    public Texture2D texture;
    public static implicit operator bool(Sprite x)=>x!=null;
}
class Asset { public Sprite sprite; }
class Material {
    public bool point;
    public bool IsKeywordEnabled(string s)=>point;
    public static implicit operator bool(Material x)=>x!=null;
}
struct Vector4 {
    public float x,y,z,w;
    public Vector4(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}
}
class ComputeBuffer {
    public int writes,offset,count;public Vector4[] last;
    public void SetData(Vector4[] v,int from,int to,int n){writes++;offset=to;count=n;last=v.ToArray();}
}
class GpuDecoImageVariant {
    public enum Kind { ReadyQuad, NullArgument }
    public Kind Status;public Asset Source;public int TextureId;
}
class DrawCollapsePolicy { LIMITS }
class P {
    static bool gpuImageCandidate,gpuImageOwn;
    static GpuDecoImageVariant[] gpuImageVariants;
    static Dictionary<int,Texture2D> gpuImageCollapseRegisteredTextures;
    static Dictionary<int,Vector4> gpuImageCollapseAddresses;
    static ComputeBuffer gpuImageCollapseAddressOwner,gpuCollapseTexture;
    static bool[] gpuCollapseIncluded=new bool[64];
    static List<object> gpuCollapseArrays=new List<object>();
    static Vector4[] gpuImageCollapseTextureUpload=new Vector4[2];
    static List<string> logs=new List<string>();static int checks;
    static void GpuLog(string s)=>logs.Add(s);
    static void RequireLive(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    static int CollapseSize(Texture2D t)=>t.tier;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static void Reject(Action a,string why){bool rejected=false;try{a();}catch(InvalidOperationException){rejected=true;}Check(rejected,why);}
    static GpuDecoImageVariant Variant(Texture2D t)=>new GpuDecoImageVariant {Source=new Asset{sprite=new Sprite{texture=t}},TextureId=t.id};
    static Dictionary<int,Vector4> Addresses(IEnumerable<Texture2D> ts)=>ts.ToDictionary(t=>t.id,t=>new Vector4(4,0,1,1));
    static void Reset(){logs.Clear();gpuImageCandidate=gpuImageOwn=true;gpuImageVariants=Array.Empty<GpuDecoImageVariant>();
        gpuImageCollapseRegisteredTextures=null;gpuImageCollapseAddresses=null;gpuImageCollapseAddressOwner=null;
        gpuCollapseTexture=new ComputeBuffer();gpuCollapseArrays.Clear();}
    METHODS
    static void Main(){
        Reset();gpuImageCandidate=false;RememberGpuImageCollapseAddresses(new Dictionary<int,Vector4>());
        Check(ReferenceEquals(gpuImageCollapseAddressOwner,gpuCollapseTexture)&&logs.Count==0,"non-image path unchanged");
        Reset();Reject(()=>RememberGpuImageCollapseAddresses(new Dictionary<int,Vector4>()),"missing registration fails");
        var original=new Texture2D{id=1};var future=new Texture2D{id=2};
        Reset();gpuImageVariants=new[]{Variant(future)};var registry=new Dictionary<int,Texture2D>{{1,original}};
        RegisterGpuImageCollapseVariants(registry);
        Check(registry.Count==2&&ReferenceEquals(registry[2],future),"future-only borrowed texture added");
        RememberGpuImageCollapseAddresses(Addresses(registry.Values));
        Check(logs.Any(x=>x.Contains("missing=0")&&x.Contains("valid=True")),"full union accepted");
        UpdateDrawCollapseTextureSlot(3,future,new Material{point=true});
        Check(gpuCollapseTexture.writes==1&&gpuCollapseTexture.offset==6&&gpuCollapseTexture.count==2,"one 32-byte slot update");
        Check(gpuCollapseTexture.last[0].x==132,"point sampling bit preserved");
        future.wrapModeU=TextureWrapMode.Repeat;
        Reject(()=>UpdateDrawCollapseTextureSlot(3,future,new Material()),"wrap guard retained");
        Check(gpuCollapseTexture.writes==1,"failed wrap never uploaded");future.wrapModeU=TextureWrapMode.Clamp;
        Reset();var all=Enumerable.Range(1,33).Select(i=>new Texture2D{id=i}).ToArray();
        gpuImageVariants=new[]{Variant(all[32])};registry=all.Take(32).ToDictionary(t=>t.id);
        RegisterGpuImageCollapseVariants(registry);
        Reject(()=>RememberGpuImageCollapseAddresses(Addresses(all.Take(32))),"initial32 covered but future33 rejected in preparation");
        Check(logs.Any(x=>x.Contains("direct_required=33 direct_limit=32")&&x.Contains("missing_direct=1")),"binding overflow quantified");
        Check(logs.Any(x=>x.Contains("variant_entries_missing=1 valid=False")),"future missing counted");
        Check(gpuImageCollapseAddresses==null&&gpuImageCollapseAddressOwner==null&&gpuCollapseTexture.writes==0,"failed coverage never published/uploaded");
        Check(logs.Any(x=>x.Contains("IMAGEOWN=FAIL")&&x.Contains("COLLAPSE_TEXTURE_COVERAGE")),"explicit early failure result");
        Reset();var array=new Texture2D{id=9,tier=512};gpuImageVariants=new[]{Variant(array),Variant(array)};
        RegisterGpuImageCollapseVariants(new Dictionary<int,Texture2D>{{1,original}});
        Reject(()=>RememberGpuImageCollapseAddresses(new Dictionary<int,Vector4>()),"mixed omitted union rejected");
        Check(logs.Any(x=>x.Contains("missing=2 missing_direct=1 missing_array=1")&&x.Contains("variant_entries_missing=2")),"unique textures and selector entries distinct");
        Reset();gpuImageVariants=new[]{Variant(future)};RegisterGpuImageCollapseVariants(new Dictionary<int,Texture2D>{{1,original}});
        RememberGpuImageCollapseAddresses(Addresses(new[]{original,future}));logs.Clear();
        Reject(()=>UpdateDrawCollapseTextureSlot(0,new Texture2D{id=77},new Material()),"unknown runtime texture rejected");
        Check(logs.Any(x=>x.Contains("collapse_runtime_missing")&&x.Contains("registered=False")),"runtime identity drift distinguished");
        gpuImageCollapseAddresses.Remove(2);logs.Clear();
        Reject(()=>UpdateDrawCollapseTextureSlot(0,future,new Material()),"registered but unaddressed texture rejected");
        Check(logs.Any(x=>x.Contains("collapse_runtime_missing")&&x.Contains("registered=True")),"runtime truncation distinguished");
        Check(gpuCollapseTexture.writes==0,"runtime missing never uploaded");
        Console.WriteLine($"IMAGEOWN_COVERAGE_CHECK checks={checks} failures=0 actual_methods=3 Unity_execution=False");
    }
}
'''.replace('LIMITS', limits).replace('METHODS', methods)
harness = out / 'coverage-check'
harness.mkdir(exist_ok=True)
(harness / 'Program.cs').write_text(program, encoding='utf-8')
(harness / 'CoverageCheck.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>', encoding='utf-8')
result = subprocess.run(['dotnet', 'run', '--project', str(harness / 'CoverageCheck.csproj'), '-v', 'q'], capture_output=True)
(out / 'coverage-check.txt').write_bytes(result.stdout + result.stderr)
print(result.stdout.decode('utf-8', errors='replace'))
if result.returncode:
    print(result.stderr.decode('utf-8', errors='replace'))
raise SystemExit(result.returncode)

"""Exercise the production UV gate and slot upload with CPU-only Unity adapters."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent

def method(path, signature):
    source = (root / path).read_text(encoding='utf-8-sig')
    start = source.index(signature)
    brace = source.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

methods = '\n'.join([
    method('GpuDecoImagePlayback.cs', 'static bool GpuImageCollapseUvInUnitRange('),
    method('GpuDecoImagePlayback.cs', 'static void RequireGpuImageCollapseSampling('),
    method('GpuDecoDrawCollapse.cs', 'static void UpdateDrawCollapseTextureSlot('),
])
program = r'''
using System;
using System.Collections.Generic;
using System.Linq;
enum TextureWrapMode { Clamp, Repeat, Mirror }
class Texture2D {
    public int width=64,height=32,mipmapCount=1;public string graphicsFormat="stub";
    public TextureWrapMode wrapModeU,wrapModeV;
    public int GetInstanceID()=>1;
    public static implicit operator bool(Texture2D t)=>t!=null;
}
class Material { public bool point;public bool IsKeywordEnabled(string s)=>point;public static implicit operator bool(Material m)=>m!=null; }
struct Vector4 {public float x,y,z,w;public Vector4(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}}
class ComputeBuffer {
    public int writes,offset,count;public Vector4[] last;
    public void SetData(Vector4[] v,int from,int to,int n){writes++;offset=to;count=n;last=v.ToArray();}
}
class Item {public float[] UV;}
class Data {public Item[] Items=new Item[2];}
class Slice {public Data Data=new Data();}
class P {
    static ComputeBuffer gpuCollapseTexture,gpuImageCollapseAddressOwner;
    static Dictionary<int,Vector4> gpuImageCollapseAddresses;
    static Dictionary<int,Texture2D> gpuImageCollapseRegisteredTextures;
    static bool[] gpuCollapseIncluded=new bool[2];
    static Vector4[] gpuImageCollapseTextureUpload=new Vector4[2];
    static List<Slice> gpuLiveSlices=new List<Slice>{new Slice()};
    static List<string> logs=new List<string>();static int checks;
    static int CollapseSize(Texture2D t)=>0;
    static void GpuLog(string s)=>logs.Add(s);
    static void RequireLive(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static void Reject(Action a,string reason){
        int before=gpuCollapseTexture.writes;bool rejected=false;
        try{a();}catch(InvalidOperationException ex){Check(ex.Message==reason,"distinct rejection reason");rejected=true;}
        Check(rejected,"rejection required");Check(before==gpuCollapseTexture.writes,"reject before texture slot write");
    }
    METHODS
    static void Main(){
        var tex=new Texture2D();var mat=new Material();
        gpuCollapseTexture=new ComputeBuffer();gpuImageCollapseAddressOwner=gpuCollapseTexture;
        gpuImageCollapseAddresses=new Dictionary<int,Vector4>{{1,new Vector4(4,2,.5f,.25f)}};
        gpuImageCollapseRegisteredTextures=new Dictionary<int,Texture2D>{{1,tex}};
        var good=new float[]{0,0,1,0,0,1,1,1};
        gpuLiveSlices[0].Data.Items[0]=new Item{UV=Enumerable.Repeat(2f,8).ToArray()};
        gpuLiveSlices[0].Data.Items[1]=new Item{UV=good};
        foreach(TextureWrapMode u in Enum.GetValues<TextureWrapMode>())
        foreach(TextureWrapMode v in Enum.GetValues<TextureWrapMode>()){
            tex.wrapModeU=u;tex.wrapModeV=v;UpdateDrawCollapseTextureSlot(1,tex,mat);
            Check(tex.wrapModeU==u&&tex.wrapModeV==v,"shared wrapping unchanged");
            Check(gpuCollapseTexture.offset==2&&gpuCollapseTexture.count==2,"correct item: two Vector4 upload");
            Check(gpuCollapseTexture.last[0].x==4&&gpuCollapseTexture.last[0].y==2,"texture address preserved");
            Check(gpuCollapseTexture.last[1].x==.5f&&gpuCollapseTexture.last[1].y==.25f,"UV scale preserved");
            Check(gpuCollapseTexture.last[1].z==.5f/64*.5f&&gpuCollapseTexture.last[1].w==.25f/32*.5f,"half texel inset preserved");
        }
        mat.point=true;UpdateDrawCollapseTextureSlot(1,tex,mat);
        Check(gpuCollapseTexture.last[0].x==132,"point sampling bit preserved");
        foreach(float value in new[]{-1e-4f,0f,1f,1f+1e-4f})
        for(int c=0;c<8;c++){
            var uv=good.ToArray();uv[c]=value;gpuLiveSlices[0].Data.Items[1]=new Item{UV=uv};
            int before=gpuCollapseTexture.writes;UpdateDrawCollapseTextureSlot(1,tex,mat);
            Check(gpuCollapseTexture.writes==before+1,"inclusive allowed component "+c);
        }
        foreach(float value in new[]{MathF.BitDecrement(-1e-4f),MathF.BitIncrement(1f+1e-4f),-1f,2f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        for(int c=0;c<8;c++){
            var uv=good.ToArray();uv[c]=value;gpuLiveSlices[0].Data.Items[1]=new Item{UV=uv};
            Reject(()=>UpdateDrawCollapseTextureSlot(1,tex,mat),"image_uv_outside_unit_range");
            Check(logs.Last().Contains("mode=collapse_uv_rejected item=1 texture_id=1"),"UV rejection diagnostic identifies item/texture");
        }
        foreach(var uv in new float[][]{null,new float[0],new float[7],new float[9]}){
            gpuLiveSlices[0].Data.Items[1]=new Item{UV=uv};
            Reject(()=>UpdateDrawCollapseTextureSlot(1,tex,mat),"image_uv_outside_unit_range");
        }
        gpuLiveSlices[0].Data.Items[1]=new Item{UV=good};
        foreach(int mip in new[]{0,2,4}){tex.mipmapCount=mip;Reject(()=>UpdateDrawCollapseTextureSlot(1,tex,mat),"image collapse changed mips");}
        tex.mipmapCount=1;gpuImageCollapseAddresses.Clear();
        Reject(()=>UpdateDrawCollapseTextureSlot(1,tex,mat),"image_texture_not_registered_in_collapse");
        gpuImageCollapseAddresses.Add(1,new Vector4(0,3,1,1));
        UpdateDrawCollapseTextureSlot(1,tex,mat);
        Check(gpuCollapseTexture.last[0].x==128&&gpuCollapseTexture.last[0].y==3,"array address and point bit preserved");
        gpuImageCollapseAddressOwner=new ComputeBuffer();
        Reject(()=>UpdateDrawCollapseTextureSlot(1,tex,mat),"image collapse address hooks not connected in PrepareDrawCollapse");
        Console.WriteLine($"IMAGEOWN_SAMPLING_CHECK checks={checks} failures=0 actual_methods=3 Unity_execution=False");
    }
}
'''.replace('METHODS', methods)
harness = out / 'sampling-check'
harness.mkdir(exist_ok=True)
(harness / 'Program.cs').write_text(program, encoding='utf-8')
(harness / 'SamplingCheck.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>', encoding='utf-8')
result = subprocess.run(['dotnet','run','--project',str(harness/'SamplingCheck.csproj'),'-v','q'],capture_output=True)
(out/'sampling-check.txt').write_bytes(result.stdout+result.stderr)
print(result.stdout.decode('utf-8',errors='replace'))
if result.returncode: print(result.stderr.decode('utf-8',errors='replace'))
raise SystemExit(result.returncode)

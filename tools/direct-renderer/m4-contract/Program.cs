using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using StutterFix;
using N = StutterFix.DirectFullNative;
using S = StutterFix.DirectFullShaders;
static class Program
{
    static void Check(bool b,string why) { if(!b)throw new Exception(why); }
    static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    static unsafe void Main(string[] args)
    {
        string root=Path.GetFullPath(args[0]);
        IntPtr library=NativeLibrary.Load(Path.Combine(root,"native/sfnative.dll"));
        NativeLibrary.SetDllImportResolver(typeof(N).Assembly,(name,assembly,path)=>name=="sfnative"?library:IntPtr.Zero);
        N.ValidateAbi();
        Check(sizeof(N.FrameActive)==432&&sizeof(N.CpuGeometry)==40&&sizeof(N.BatchPerformance)==88,"M4f appended opcode payload ABI");
        Check((int)N.Op.CommitPacked==12&&(int)N.Op.CommitActiveWorld==16,"existing opcode values preserved");
        Check(sizeof(N.Attribute)==20&&sizeof(N.Buffer)==16&&sizeof(N.Patch)==8&&sizeof(N.Texture)==40,"nested ABI");
        var shaders=S.Load();Check(shaders.Count==21,"shader count");Check(ReferenceEquals(shaders,S.Load()),"resource reload");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"out/direct-renderer/m4bc/shaders/bindings.json")));
        int variants=0,stages=0,variables=0,textures=0;
        foreach(var a in doc.RootElement.GetProperty("shaders").EnumerateArray())
        {
            var s=shaders[a.GetProperty("name").GetString()];int pi=0;
            foreach(var p in a.GetProperty("passes").EnumerateArray())
            {
                var pass=s.Passes[pi++];Check(pass.Index==p.GetProperty("index").GetInt32()&&pass.Grab==p.GetProperty("grab_before").GetBoolean(),"pass/Grab linkage");
                int vi=0;
                foreach(var v in p.GetProperty("variants").EnumerateArray())
                {
                    var variant=pass.Variants[vi++];var m=new UnityEngine.Material();foreach(string k in variant.Keywords)m.Keywords.Add(k);
                    Check(ReferenceEquals(S.Select(s,pass,m),variant),"variant keyword resolution");variants++;
                    foreach(var (key,stage) in new[]{("vs",variant.Vs),("ps",variant.Ps)})
                    {
                        var expected=v.GetProperty(key);Check(Hash(stage.Code)==expected.GetProperty("sha256").GetString(),"DXBC resource mismatch");stages++;
                        int bi=0;
                        foreach(var cb in expected.GetProperty("cbs").EnumerateArray())
                        {
                            var b=stage.Buffers[bi++];Check(b.Name==cb.GetProperty("name").GetString()&&b.Slot==cb.GetProperty("slot").GetInt32()&&b.Size==cb.GetProperty("size").GetInt32(),"CB resource mismatch");
                            int xi=0;foreach(var x in cb.GetProperty("variables").EnumerateArray()) {var value=b.Variables[xi++];Check(value.Name==x.GetProperty("name").GetString()&&value.Offset==x.GetProperty("offset").GetInt32(),"CB variable mismatch");variables++;}
                        }
                        textures+=stage.Textures.Length;
                    }
                }
                UnityEngine.Shader.Keywords.Add("STEREO_INSTANCING_ON");bool rejected=false;
                try { S.Select(s,pass,new UnityEngine.Material()); } catch(InvalidOperationException){rejected=true;}
                UnityEngine.Shader.Keywords.Clear();Check(rejected,"unverified keyword accepted");
            }
        }
        Console.WriteLine($"PASS production resource loader shaders={shaders.Count} variants={variants} stages={stages} variables={variables} textures={textures}; exact DXBC/CB/pass/keyword roundtrip; ABI Create={sizeof(N.Create)} Program={sizeof(N.Program)} Geometry={sizeof(N.Geometry)} Draw={sizeof(N.Draw)} Frame={sizeof(N.Frame)} Stats={sizeof(N.Stats)} Pose={sizeof(N.Pose)}; Unity methods=not executed");
    }
}

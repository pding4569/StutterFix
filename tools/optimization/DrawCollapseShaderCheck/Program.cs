using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

// Real Microsoft SM5 compilation, without Editor licensing or a game process.
// Raster entry checks substitute only UnityObjectToClipPos; this is NOT a Unity
// import, bundle, driver execution, or game Sprites/Tile compatibility proof.
unsafe class Program
{
    [DllImport("d3dcompiler_47.dll",CallingConvention=CallingConvention.StdCall)]
    static extern int D3DCompile(byte[] data,nuint size,string name,nint defines,nint include,string entry,string target,uint flags,uint flags2,out nint code,out nint error);
    [DllImport("d3dcompiler_47.dll",CallingConvention=CallingConvention.StdCall)]
    static extern int D3DDisassemble(byte[] data,nuint size,uint flags,string comments,out nint text);
    static byte[] Blob(nint value)
    {
        if(value==0)return Array.Empty<byte>();
        nint* table=*(nint**)value;
        var pointer=(delegate* unmanaged[Stdcall]<nint,nint>)table[3];
        var length=(delegate* unmanaged[Stdcall]<nint,nuint>)table[4];
        var bytes=new byte[checked((int)length(value))];Marshal.Copy(pointer(value),bytes,0,bytes.Length);return bytes;
    }
    static int Main(string[] args)
    {
        string root=Path.GetFullPath(args.Length==0?".":args[0]);int count=0;
        string compute=File.ReadAllText(Path.Combine(root,"tools/gpudeco-lab/GpuDeco.compute"));
        string raster=File.ReadAllText(Path.Combine(root,"tools/gpudeco-lab/GpuDecoBatch.shader"));
        string policy=File.ReadAllText(Path.Combine(root,"DrawCollapsePolicy.cs"));
        int arrays=int.Parse(Regex.Match(policy,@"const int ArrayBindings=(\d+);").Groups[1].Value);
        int direct=int.Parse(Regex.Match(policy,@"const int DirectBindings=(\d+);").Groups[1].Value);
        if(arrays+direct>64 || Regex.Matches(raster,@"Texture2DArray _Tex\d+;").Count!=arrays
            || Regex.Matches(raster,@"Texture2D _Big\d+;").Count!=direct)
        {Console.Error.WriteLine("Generated shader texture declarations exceed Unity cap or differ from runtime policy");return 1;}
        if(!raster.Contains("#pragma multi_compile_local _ NO_DIRECT_TEXTURES"))
        {Console.Error.WriteLine("Missing runtime material local multi_compile variant");return 1;}
        Console.WriteLine("SHADER_TEXTURE_BINDINGS arrays="+arrays+" direct="+direct+" declared="+(arrays+direct)+" unity_limit=64");
        raster=raster.Split("HLSLPROGRAM")[1].Split("ENDHLSL")[0]
            .Replace("#include \"UnityCG.cginc\"","float4 UnityObjectToClipPos(float4 p) { return p; }");
        string baseline=args.Length>1?File.ReadAllText(args[1]).Split("HLSLPROGRAM")[1].Split("ENDHLSL")[0]
            .Replace("#include \"UnityCG.cginc\"","float4 UnityObjectToClipPos(float4 p) { return p; }"):null;
        foreach(var entry in new[]{("EvaluateLive","cs_5_0",compute,"default"),("EvaluateCollapse","cs_5_0",compute,"default"),
            ("vert","vs_5_0",raster,"default"),("frag","ps_5_0",raster,"default"),
            ("vert","vs_5_0","#define NO_DIRECT_TEXTURES 1\n"+raster,"NO_DIRECT_TEXTURES"),
            ("frag","ps_5_0","#define NO_DIRECT_TEXTURES 1\n"+raster,"NO_DIRECT_TEXTURES")})
        {
            byte[] source=Encoding.UTF8.GetBytes(entry.Item3);nint code=0,error=0;
            try
            {
                // This check validates SM5 syntax/resource limits. Avoid the
                // optimizer's excessive branch folding on resource choices.
                int hr=D3DCompile(source,(nuint)source.Length,"drawcollapse",0,0,entry.Item1,entry.Item2,(1u<<11)|4u,0,out code,out error);
                string messages=Encoding.UTF8.GetString(Blob(error));
                if(hr<0){Console.Error.WriteLine(messages);return 1;}
                Console.WriteLine("SHADER_COMPILE entry="+entry.Item1+" target="+entry.Item2+" bytes="+Blob(code).Length+" status=PASS");count++;
                byte[] compiled=Blob(code);
                if(entry.Item1=="frag")
                {
                    nint disassembly=0;
                    try
                    {
                        if(D3DDisassemble(compiled,(nuint)compiled.Length,0,null,out disassembly)<0)
                            throw new InvalidOperationException("Could not inspect compiled texture bindings");
                        string assembly=Encoding.UTF8.GetString(Blob(disassembly));
                        int arrayCount=Regex.Matches(assembly,@"(?m)^//\s+_Tex\d+\s+texture\b").Count;
                        int directCount=Regex.Matches(assembly,@"(?m)^//\s+_Big\d+\s+texture\b").Count;
                        bool noDirect=entry.Item4=="NO_DIRECT_TEXTURES";
                        if(arrayCount!=arrays || directCount!=(noDirect?0:direct) || (noDirect && assembly.Contains("_Big")))
                            throw new InvalidOperationException("Compiled resource counts differ from expected variant: arrays="+arrayCount+" direct="+directCount);
                        Console.WriteLine("SHADER_VARIANT_BINDINGS variant="+entry.Item4+" arrays="+arrayCount+" direct="+directCount+" status=PASS");
                    }
                    finally {if(disassembly!=0)Marshal.Release(disassembly);}
                }
                if(baseline!=null && entry.Item4=="default" && entry.Item2!="cs_5_0")
                {
                    byte[] oldSource=Encoding.UTF8.GetBytes(baseline);nint oldCode=0,oldError=0;
                    try
                    {
                        int oldHr=D3DCompile(oldSource,(nuint)oldSource.Length,"drawcollapse",0,0,entry.Item1,entry.Item2,(1u<<11)|4u,0,out oldCode,out oldError);
                        if(oldHr<0 || !compiled.SequenceEqual(Blob(oldCode)))
                            throw new InvalidOperationException("Default variant differs from baseline: "+entry.Item1+" "+Encoding.UTF8.GetString(Blob(oldError)));
                        Console.WriteLine("SHADER_BASELINE_EQUAL entry="+entry.Item1+" bytecode=IDENTICAL status=PASS");
                    }
                    finally {if(oldCode!=0)Marshal.Release(oldCode);if(oldError!=0)Marshal.Release(oldError);}
                }
                if(messages.Length>0)Console.WriteLine(messages.Trim());
            }
            finally {if(code!=0)Marshal.Release(code);if(error!=0)Marshal.Release(error);}
        }
        Console.WriteLine("DRAWCOLLAPSE_SHADER_CHECK=PASS entries="+count+" optimization=skipped unity_import=UNVERIFIED device_execution=UNVERIFIED");return 0;
    }
}

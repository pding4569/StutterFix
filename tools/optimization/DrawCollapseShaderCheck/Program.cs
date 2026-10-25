using System;
using System.IO;
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
        Console.WriteLine("SHADER_TEXTURE_BINDINGS arrays="+arrays+" direct="+direct+" declared="+(arrays+direct)+" unity_limit=64");
        raster=raster.Split("HLSLPROGRAM")[1].Split("ENDHLSL")[0]
            .Replace("#include \"UnityCG.cginc\"","float4 UnityObjectToClipPos(float4 p) { return p; }");
        foreach(var entry in new[]{("EvaluateLive","cs_5_0",compute),("EvaluateCollapse","cs_5_0",compute),("vert","vs_5_0",raster),("frag","ps_5_0",raster)})
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
                if(messages.Length>0)Console.WriteLine(messages.Trim());
            }
            finally {if(code!=0)Marshal.Release(code);if(error!=0)Marshal.Release(error);}
        }
        Console.WriteLine("DRAWCOLLAPSE_SHADER_CHECK=PASS entries="+count+" optimization=skipped unity_import=UNVERIFIED device_execution=UNVERIFIED");return 0;
    }
}

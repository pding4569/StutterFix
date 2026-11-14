using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
string managed=@"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed";
AppDomain.CurrentDomain.AssemblyResolve+=(_,a)=>{string p=Path.Combine(managed,new AssemblyName(a.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
string path=Path.Combine(managed,"DOTween.dll");var asm=Assembly.LoadFrom(path);var ease=asm.GetType("DG.Tweening.Ease",true)!;
var method=asm.GetType("DG.Tweening.Core.Easing.EaseManager",true)!.GetMethod("Evaluate",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{ease,asm.GetType("DG.Tweening.EaseFunction",true)!,typeof(float),typeof(float),typeof(float),typeof(float)},null)!;
var wrapper=new DynamicMethod("InstalledDOTween",typeof(float),new[]{typeof(int),typeof(float)},typeof(Program),true);var il=wrapper.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldnull);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Ldc_R4,1f);il.Emit(OpCodes.Ldc_R4,1.70158f);il.Emit(OpCodes.Ldc_R4,0.3f);il.Emit(OpCodes.Call,method);il.Emit(OpCodes.Ret);
var eval=(Func<int,float,float>)wrapper.CreateDelegate(typeof(Func<int,float,float>));
string[] names={"Linear","InQuad","OutQuad","InOutQuad","InSine","OutSine","InOutSine","OutBack","OutExpo"};
long[] maxUlp=new long[names.Length];double[] maxAbs=new double[names.Length];int[] exact=new int[names.Length];int[] counts=new int[names.Length];
foreach(var row in File.ReadLines(args[0])){var p=row.Split(',');int e=int.Parse(p[0]),j=int.Parse(p[1]);float native=float.Parse(p[2],CultureInfo.InvariantCulture),expected=eval(Convert.ToInt32(Enum.Parse(ease,names[e])),j/1000f);
    long Order(float f){int b=BitConverter.SingleToInt32Bits(f);return b<0?int.MinValue-(long)b:b;}
    long ulp=Math.Abs(Order(native)-Order(expected));maxUlp[e]=Math.Max(maxUlp[e],ulp);maxAbs[e]=Math.Max(maxAbs[e],Math.Abs((double)native-expected));if(ulp==0)exact[e]++;counts[e]++;
}
Console.WriteLine("DOTween_sha256="+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
for(int e=0;e<names.Length;e++){Console.WriteLine(FormattableString.Invariant($"{names[e]} samples={counts[e]} exact={exact[e]} max_ulp={maxUlp[e]} max_abs={maxAbs[e]:R}"));if(counts[e]!=1001||maxAbs[e]>1e-5)throw new Exception("easing tolerance failed");}
Console.WriteLine("PASS actual installed DOTween Evaluate, .NET 8 offline runtime (game Mono runtime not executed)");
if(args.Length>1){
    foreach(string edition in new[]{"Player","PlayerAuto","Debug"}){
        string dll=Path.Combine(args[1],"bin",edition,"StutterFix.dll");using var pe=new PEReader(File.OpenRead(dll));var reader=pe.GetMetadataReader();
        bool type=reader.TypeDefinitions.Any(h=>reader.GetString(reader.GetTypeDefinition(h).Name)=="DirectRenderer");
        var found=new List<string>();for(var h=MetadataTokens.UserStringHandle(1);!h.IsNil;h=reader.GetNextHandle(h)){string s=reader.GetUserString(h);if(s=="dr"||s.StartsWith("dr ")||s.StartsWith("[dr]")||s.StartsWith("DR."))found.Add(s);}
        if(edition=="Player"?(type||found.Count!=0):(!type||found.Count==0))throw new Exception("edition isolation failed: "+edition);
        Console.WriteLine($"PASS {edition}: DirectRenderer_type={type} dr_user_strings={found.Count}");
    }
}

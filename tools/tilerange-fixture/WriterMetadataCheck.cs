// Reflection metadata only: no game method or static initializer is executed.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
class WriterMetadataCheck
{
    static int Main(string[] args)
    {
        string dll=Path.GetFullPath(args[0]), manifest=File.ReadAllText(args[1]);
        string managed=Path.GetDirectoryName(dll);
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>
        {
            var path=Path.Combine(managed,new AssemblyName(e.Name).Name+".dll");
            return File.Exists(path)?Assembly.LoadFrom(path):null;
        };
        var asm=Assembly.LoadFrom(dll); int targets=0;
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly;
        foreach(Match match in Regex.Matches(manifest,"new Spec\\(\"([^\"]+)\",\"([^\"]+)\",\"([^\"]+)\",([0-9]+)\\)"))
        {
            string typeName=match.Groups[1].Value,name=match.Groups[2].Value,sig=match.Groups[3].Value;
            var t=asm.GetType(typeName,true);
            var found=t.GetMethods(flags).Cast<MethodBase>().Concat(t.GetConstructors(flags)).Where(m=>m.Name==name&&Convert.ToHexString(m.Module.ResolveSignature(m.MetadataToken))==sig).ToArray();
            if(found.Length!=1) throw new Exception(typeName+"::"+name+" matches="+found.Length);
            var m=found[0];
            if(m.IsAbstract||m.ContainsGenericParameters||m.GetMethodBody()==null) throw new Exception("not patchable managed body: "+m);
            Console.WriteLine("OK "+typeName+"::"+m+" signature="+sig); targets++;
        }
        if(targets<20||targets>64) throw new Exception("unexpected target count "+targets);
        var effect=asm.GetType("ffxPlusBase",true).GetField("floor",flags);
        var previous=asm.GetType("ffxFloorAppearPlus",true).GetField("prevFloor",flags);
        if(effect?.FieldType!=asm.GetType("scrFloor")||previous?.FieldType!=effect.FieldType) throw new Exception("effect field types");
        var renderer=asm.GetType("FloorRenderer",true).GetField("material",flags);
        if(renderer==null) throw new Exception("material is not the expected field");
        Console.WriteLine("PASS metadata targets="+targets+" exact=1 each; effect fields and material field verified; no Harmony detours executed");
        return 0;
    }
}

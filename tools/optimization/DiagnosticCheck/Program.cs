using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var excluded="AutoTest AllocProbe TimeProbe FrameScan HeapPath SceneScan FloorProf FilterGpuProbe FilterHalfProbe FilterHalfPerf FreeFpsProbe ScriptProbe FilterTrace MergeProbe RenderVerify UiProf RenderCallbackScan FrameRateScreenWatch ParticleTextWatch BlendProbe StartProbe UpdateFixture TimerResolutionInfo DecorationAudit MaterialPoolFixture AlphaCropPlan AlphaBoundsProbe AlphaCropFixture AnimationAudit".Split(' ');
HashSet<string> Types(string path)
{
    using var stream=File.OpenRead(path);using var pe=new PEReader(stream);var md=pe.GetMetadataReader();
    return md.TypeDefinitions.Select(h=>md.GetString(md.GetTypeDefinition(h).Name)).ToHashSet();
}
var player=Types(args[0]);var developer=Types(args[1]);int n=0;
foreach(string name in excluded)
{
    if(player.Contains(name)||!developer.Contains(name))throw new Exception("Edition isolation failed: "+name);
    n++;
}
foreach(string name in new[]{"PerfOverlay","PresentWatch","GfxProbe","FrameGen","GcControl","ScreenEffects"})
    if(!player.Contains(name))throw new Exception("Runtime module removed: "+name);
using(var file=File.OpenRead(args[0]))using(var pe=new PEReader(file))
{
    var md=pe.GetMetadataReader();
    foreach(var h in md.MethodDefinitions)
    {
        string name=md.GetString(md.GetMethodDefinition(h).Name);
        if(name=="LifecycleFixture"||name=="TestInstall"||name=="CullSnapshot"||name=="AngleFixture"||name=="BindFixture"||name=="UnbindFixture"||name=="SuspendFixture"||name=="EscapeFixture"||name=="VerifyPrimitivePlans"||name=="PrimitiveLifetimeFixture")throw new Exception("Test hook in Player: "+name);
    }
    var pool=md.TypeDefinitions.First(h=>md.GetString(md.GetTypeDefinition(h).Name)=="DecorationMaterialPool");
    var poolMethods=md.GetTypeDefinition(pool).GetMethods().Select(h=>md.GetString(md.GetMethodDefinition(h).Name)).ToHashSet();
    foreach(string name in new[]{"Created","ExactKey","BeforeCull","MaterialRead","RestoreInstanceAliases"})
        if(poolMethods.Contains(name))throw new Exception("Unsafe research material pooling in Player: "+name);
}
Console.WriteLine($"Player diagnostic types absent {n}/{excluded.Length}, developer present {n}/{excluded.Length}; required runtime types 6/6 retained");
Console.WriteLine("Player material sharing: blocking stub only; research core methods absent 5/5");

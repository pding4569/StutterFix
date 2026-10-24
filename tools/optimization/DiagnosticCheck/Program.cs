using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var excluded="AutoTest AllocProbe TimeProbe FrameScan HeapPath SceneScan FloorProf FilterGpuProbe FilterHalfProbe FilterHalfPerf FreeFpsProbe ScriptProbe FilterTrace MergeProbe RenderVerify UiProf RenderCallbackScan FrameRateScreenWatch ParticleTextWatch BlendProbe StartProbe UpdateFixture TimerResolutionInfo DecorationAudit MaterialPoolFixture AlphaCropPlan AlphaBoundsProbe AlphaCropFixture AnimationAudit AllocScan SlowScan PhaseWatch RenderWatch SamplerWatch LoopProfiler DecoDiag".Split(' ');
HashSet<string> Types(string path)
{
    using var stream=File.OpenRead(path);using var pe=new PEReader(stream);var md=pe.GetMetadataReader();
    return md.TypeDefinitions.Select(h=>md.GetString(md.GetTypeDefinition(h).Name)).ToHashSet();
}
var player=Types(args[0]);var developer=Types(args[1]);int n=0;
if(player.Contains("HiddenDecoSynergy") || !developer.Contains("HiddenDecoSynergy"))
    throw new Exception("Hidden decoration research isolation failed");
using(var stream=File.OpenRead(args[0]))using(var pe=new PEReader(stream))
{
    var md=pe.GetMetadataReader();
    if(md.MethodDefinitions.Any(h=>md.GetString(md.GetMethodDefinition(h).Name)=="HiddenFixture"))
        throw new Exception("Hidden decoration fixture leaked into Player");
}
foreach(string name in player)
    if(name.StartsWith("GpuDeco",StringComparison.Ordinal)||name.StartsWith("LiveDeco",StringComparison.Ordinal)||name.StartsWith("DecoBake",StringComparison.Ordinal))
        throw new Exception("GPU decoration research leaked into Player: "+name);
if(args.Skip(2).Contains("--research-only"))
{
    foreach(string prefix in new[]{"GpuDeco","LiveDeco","DecoBake"})
        if(!developer.Any(name=>name.StartsWith(prefix,StringComparison.Ordinal)))throw new Exception("Missing developer research positive control: "+prefix);
    Console.WriteLine("Player GpuDeco/LiveDeco/DecoBake/HiddenDecoSynergy types and HiddenFixture absent; developer controls present 4/4. Legacy diagnostic inventory NOT EVALUATED.");
    return;
}
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

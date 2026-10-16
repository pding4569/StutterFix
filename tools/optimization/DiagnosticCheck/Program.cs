using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var excluded="AutoTest AllocProbe TimeProbe FrameScan HeapPath SceneScan FloorProf FilterGpuProbe FilterHalfProbe FilterHalfPerf FreeFpsProbe ScriptProbe FilterTrace MergeProbe RenderVerify UiProf RenderCallbackScan FrameRateScreenWatch ParticleTextWatch BlendProbe StartProbe UpdateFixture".Split(' ');
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
        if(name=="LifecycleFixture"||name=="TestInstall"||name=="CullSnapshot")throw new Exception("Test hook in Player: "+name);
    }
}
Console.WriteLine($"Player diagnostic types absent {n}/{excluded.Length}, developer present {n}/{excluded.Length}; required runtime types 6/6 retained");

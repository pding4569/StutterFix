using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using StutterFix;

int checks=0;
void Check(bool value,string reason) { checks++; if(!value)throw new Exception(reason); }
var life=new GpuDecoPlayerLifetime();
Check(!life.TryPrepare() && !life.TryActivate(),"disabled lifetime cannot prepare or activate");
for(int i=0;i<100;i++)
{
    life.NewRound();
    Check(!life.TryActivate(),"restart waits for preparation");
    Check(life.TryPrepare() && !life.TryPrepare(),"exactly one preparation per round");
    life.Ready(); Check(life.TryActivate() && !life.TryActivate(),"exactly one activation per round");
    life.Fail(); life.Ready();
    Check(!life.TryPrepare() && !life.TryActivate(),"failure cannot retry within round");
}
life.NewRound();life.TryPrepare();life.Fail();life.Ready();
Check(!life.TryActivate() && !life.TryPrepare(),"preparation failure also latches");
Check(GpuDecoPlayerPolicy.FitsTextures(52,48,new[]{1,1,1,1},2048),"52 texture boundary admitted");
Check(!GpuDecoPlayerPolicy.FitsTextures(53,48,new[]{5},2048),"53 textures use ownership only");
Check(!GpuDecoPlayerPolicy.FitsTextures(185,48,new[]{137},2048),"whole-song 185 union uses safe mode");
Check(!GpuDecoPlayerPolicy.FitsTextures(49,49,Array.Empty<int>(),2048),"49 direct textures overflow even below 52 total");
Check(!GpuDecoPlayerPolicy.FitsTextures(5,0,new[]{1,1,1,1,1},2048),"five formats/tiers overflow arrays");
Check(GpuDecoPlayerPolicy.FitsTextures(8,0,new[]{8},2),"four array slices banks admitted");
Check(!GpuDecoPlayerPolicy.FitsTextures(9,0,new[]{9},2),"slice limit participates in budget");
Check(!GpuDecoPlayerPolicy.FitsTextures(1,0,new[]{1},0),"no array support rejected");
int orders=0;var guard=new DrawCollapseGuard(false,_=>orders++);
guard.Check(false,"drawcollapse proxy/barrier order changed renderer=1");
guard.Check(false,"drawcollapse owned item=1 sort key changed name=test");
Check(orders==2,"product order violations only log");
foreach(string reason in new[]{"drawcollapse representative lost","drawcollapse camera lost","image_texture_not_registered_in_collapse"})
{
    bool thrown=false;try{guard.Check(false,reason);}catch(InvalidOperationException){thrown=true;}
    Check(thrown,"serious failure must throw: "+reason);
}
Check(DrawCollapsePolicy.KeepAfterBarrier && DrawCollapsePolicy.SkipOrderSweep && DrawCollapsePolicy.GlobalVerts
    && !DrawCollapsePolicy.TileComposite && !DrawCollapsePolicy.DisableOwnedRenderers && !DrawCollapsePolicy.DirectDispatch
    && !DrawCollapsePolicy.TightAllOwned && DrawCollapsePolicy.TightLargeQuads,"measured defaults unchanged");

if(args.Length!=3)throw new ArgumentException("player.dll auto.dll bundle");
byte[] bundleHash=SHA256.HashData(File.ReadAllBytes(args[2]));
foreach(var item in new[]{(args[0],false),(args[1],true)})
{
    using var stream=File.OpenRead(item.Item1);using var pe=new PEReader(stream);
    var md=pe.GetMetadataReader();
    var types=md.TypeDefinitions.Select(h=>(Handle:h,Def:md.GetTypeDefinition(h)))
        .Select(x=>(x.Handle,x.Def,Name:md.GetString(x.Def.Name))).ToArray();
    bool Has(string name)=>types.Any(t=>t.Name==name);
    Check(Has("GpuDecoPlayer") && Has("GpuDecoGpu") && Has("GpuDecoImageTimeline"),"runtime types included");
    foreach(string diagnostic in new[]{"AutoTest","FrameCompare","GpuHide","GpuProbe","GpuCensus","GpuDecoCostProbe","GpuDecoSnapshot"})
        Check(Has(diagnostic)==item.Item2,"diagnostic boundary: "+diagnostic);
    if(!item.Item2)
    {
        Check(!types.Any(t=>t.Name.StartsWith("GpuDeco") && (t.Name.Contains("Fixture") || t.Name.Contains("Probe") || t.Name.Contains("PixelGate"))),"no GPU fixture/probe/pixel gate types");
        var bake=types.Single(t=>t.Name=="DecoBake");
        Check((bake.Def.Attributes&TypeAttributes.BeforeFieldInit)==0,"runtime has an explicit lazy initializer");
        string[] methods=bake.Def.GetMethods().Select(h=>md.GetString(md.GetMethodDefinition(h).Name)).ToArray();
        Check(!methods.Contains("GpuStep") && !methods.Contains("GpuDrawCollapseCommand") && !methods.Contains("AdoptCommand"),"test command entry points excluded");
    }
    var resource=md.ManifestResources.Select(h=>md.GetManifestResource(h)).Single(r=>md.GetString(r.Name)=="StutterFix.gpudeco");
    Check(resource.Implementation.IsNil,"GPU bundle embedded, not linked");
    int bundleBytes=checked((int)new FileInfo(args[2]).Length);
    var data=pe.GetSectionData(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress).GetReader((int)resource.Offset,4+bundleBytes);
    int size=data.ReadInt32();byte[] bytes=data.ReadBytes(size);
    Check(SHA256.HashData(bytes).SequenceEqual(bundleHash),"embedded bytes match the supplied drawcollapse5 bundle");
    Console.WriteLine($"assembly={Path.GetFileName(Path.GetDirectoryName(item.Item1))} autotest={item.Item2} types={types.Length} embedded_bytes={size} bundle_sha256={Convert.ToHexString(bundleHash)}");
}
Console.WriteLine($"GpuDecoPlayerCheck checks={checks} errors=0 scope=offline_policy_and_PE_metadata_no_game_or_assembly_load");

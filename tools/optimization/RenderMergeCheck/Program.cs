using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using StutterFix;

int checks=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
RenderMergeGatePolicy Complete()
{
    var gate=new RenderMergeGatePolicy();
    for(int i=0;i<4;i++)
    {
        bool merged=i==1||i==2;
        gate.Population(i,merged,23547,23547,0,merged?0:1900);
        gate.Window(i,merged,merged?220:165,true,0,109,600);
    }
    for(int i=0;i<2;i++)gate.Pixel(i,0,0,10,2,10,2,true);
    for(int i=0;i<4;i++)gate.Stability(i,true);
    return gate;
}
Check(new RenderMergeGatePolicy().Verdict()=="ERROR","no evidence");
Check(Complete().Verdict()=="PASS","all thresholds inclusive");
{
    Check(RenderMergeLayoutPolicy.OmitAuxiliary(true,false,false),"dormant admitted auxiliary is not a draw barrier");
    Check(!RenderMergeLayoutPolicy.OmitAuxiliary(false,false,false),"unknown hidden renderer stays a barrier");
    Check(!RenderMergeLayoutPolicy.OmitAuxiliary(true,true,false),"CPU fallback auxiliary stays a barrier");
    bool rejected=false;
    try {RenderMergeLayoutPolicy.OmitAuxiliary(true,false,true);} catch(InvalidOperationException){rejected=true;}
    Check(rejected,"live auxiliary cannot silently disappear from ordering");
    var map=Enumerable.Repeat(uint.MaxValue,16).ToArray();
    // Noncontiguous source items and a change with the same visible count.
    // Resolve a real quad's two triangles through the production mapping.
    int[] triangles={0,2,1,2,3,1};
    foreach(bool swapped in new[]{false,true,false})
    {
        RenderMergeLayoutPolicy.MapQuad(map,4,7,!swapped);
        RenderMergeLayoutPolicy.MapQuad(map,8,2,swapped);
        var first=triangles.Select(i=>map[4+i]).ToArray();
        var second=triangles.Select(i=>map[8+i]).ToArray();
        Check(first.SequenceEqual(triangles.Select(i=>28u+(!swapped?(uint)i:0u))),"visible corners/winding and hidden degeneracy");
        Check(second.SequenceEqual(triangles.Select(i=>8u+(swapped?(uint)i:0u))),"equal-count membership swap reaches the right source");
        Check(map.Take(4).Concat(map.Skip(12)).All(x=>x==uint.MaxValue),"partial map preserves neighbor groups");
    }
    foreach(int count in new[]{1,100000,111382,235470})
        Check(RenderMergeLayoutPolicy.ValidResourceObjects(count,4L*count+17),"bounded full-population mark readable");
    Check(!RenderMergeLayoutPolicy.ValidResourceObjects(111382,4L*111382+16),"truncated object payload rejected before allocation");
    Check(!RenderMergeLayoutPolicy.ValidResourceObjects(111382,4L*111382+18),"trailing payload rejected");
    Check(!RenderMergeLayoutPolicy.ValidResourceObjects(0,17)
        && !RenderMergeLayoutPolicy.ValidResourceObjects(int.MaxValue,4L*int.MaxValue+17),"empty/oversized marks rejected");
}
foreach(var population in new[]{(26,26,0,0),(23547,19999,3548,1900),(23547,23547,0,0),
    (23547,23546,0,1900),(23547,23547,-1,1900)})
{
    var gate=new RenderMergeGatePolicy();
    Check(!gate.Population(0,false,population.Item1,population.Item2,population.Item3,population.Item4)
        && gate.Error,"small/empty/inconsistent baseline rejected");
}
{
    var gate=new RenderMergeGatePolicy();
    Check(gate.Population(0,false,20000,20000,0,1),"population lower bound inclusive");
    Check(gate.Population(1,true,20000,20000,0,0),"merged mode can hide independent renderers");
    Check(!gate.Population(2,true,20001,20000,1,0),"fallback population changes invalidate ABBA");
    gate=Complete();gate.PopulationSeen[2]=false;
    Check(gate.Verdict()=="ERROR","missing population evidence cannot pass");
    gate=new RenderMergeGatePolicy();
    Check(!gate.Population(1,true,23547,23547,0,0),"B cannot establish the A population");
}
for(int i=0;i<4;i++)
{
    var gate=Complete();gate.Seen[i]=false;Check(gate.Verdict()=="ERROR","each ABBA window required");
    gate=Complete();gate.StabilitySeen[i]=false;Check(gate.Verdict()=="ERROR","each lifecycle required");
}
for(int i=0;i<2;i++){var gate=Complete();gate.Pixels[i]=false;Check(gate.Verdict()=="ERROR","each pixel point required");}
foreach(double bad in new[]{0d,-1d,double.NaN,double.PositiveInfinity})
{
    var gate=new RenderMergeGatePolicy();gate.Window(0,false,bad,true,0,1,1);Check(gate.Error,"invalid FPS");
}
foreach(var scenario in new[]{(219.99,109,600,0,"FPS_FAIL"),(220d,151,600,0,"FPS_FAIL"),
    (220d,109,601,0,"FPS_FAIL"),(220d,0,600,0,"FPS_FAIL"),(220d,109,0,0,"FPS_FAIL"),
    (220d,109,600,1,"STABILITY_FAIL")})
{
    var gate=Complete();gate.Seen[1]=false;
    gate.Window(1,true,scenario.Item1,true,scenario.Item4,scenario.Item2,scenario.Item3);
    Check(gate.Verdict()==scenario.Item5,"FPS/count/stutter limits");
}
{
    var gate=Complete();gate.Seen[1]=false;gate.Window(1,true,220,false,0,109,600);Check(gate.Verdict()=="ERROR","invalid live window");
    gate=Complete();gate.Window(1,true,220,true,0,109,600);Check(gate.Verdict()=="ERROR","duplicate window");
    gate=new RenderMergeGatePolicy();gate.Window(0,true,220,true,0,109,600);Check(gate.Error,"ABBA mode mismatch");
    gate=Complete();gate.Pixels[0]=false;gate.Pixel(0,1,1,10,2,10,2,true);Check(gate.Verdict()=="PIXEL_FAIL","same aggregate errors do not establish equivalence");
    gate=Complete();gate.Pixels[0]=false;gate.Pixel(0,0,0,10,2,11,2,true);Check(gate.Verdict()=="PIXEL_FAIL","original-reference error increased");
    gate=Complete();gate.Pixels[0]=false;gate.Pixel(0,0,0,10,2,10,3,true);Check(gate.Verdict()=="PIXEL_FAIL","max original-reference error increased");
    gate=Complete();gate.Pixels[0]=false;gate.Pixel(0,0,0,10,2,10,2,false);Check(gate.Verdict()=="STABILITY_FAIL","capture noise/restoration");
    gate=Complete();gate.StabilitySeen[0]=false;gate.Stability(0,false);Check(gate.Verdict()=="STABILITY_FAIL","observed lifecycle failure");
    gate=Complete();gate.Stability(0,true);Check(gate.Verdict()=="ERROR","duplicate lifecycle evidence");
}
string temp=Path.Combine(Path.GetTempPath(),"sf-rendermerge-check-"+Guid.NewGuid()+".txt");
try
{
    File.WriteAllText(temp,"# exact instruction addresses\n!0x1abc A:Method + 0x17\n1abd B:Other\n");
    var syms=MonoSymbols.Read(temp);
    Check(syms.Count==2,"symbols parsed");
    Check(syms.Resolve(0x1abc,"old")=="mono-jit!A:Method + 0x17","exact symbol match");
    Check(syms.Resolve(0x1abe,"unknown")=="unknown","no nearest-address attribution");
    Check(MonoSymbols.Address("0xFFFF",out ulong address)&&address==65535,"hex prefix");
    Check(!MonoSymbols.Address("garbage",out _)&&!MonoSymbols.Address("0",out _),"bad/null addresses");
    File.WriteAllText(temp,"1abc First\n1abc Second\n");
    bool rejected=false;try{MonoSymbols.Read(temp);}catch(InvalidDataException){rejected=true;}
    Check(rejected,"conflicting names rejected");
    File.WriteAllText(temp,"1abc\n");rejected=false;try{MonoSymbols.Read(temp);}catch(InvalidDataException){rejected=true;}
    Check(rejected,"malformed symbol row rejected");
}
finally {File.Delete(temp);}
if(args.Length==3)
{
    for(int i=0;i<args.Length;i++)
    {
        using var stream=File.OpenRead(args[i]);using var pe=new PEReader(stream);var md=pe.GetMetadataReader();
        var types=md.TypeDefinitions.Select(h=>md.GetString(md.GetTypeDefinition(h).Name)).ToHashSet();
        Check(types.Contains("MergeWindow")==(i!=0),"merged renderer isolated from Player");
        Check(types.Contains("RenderMergeGatePolicy")==(i==2),"acceptance gate only in AutoTest");
    }
}
Console.WriteLine($"RenderMerge production gate/Mono symbol loader: {checks} checks passed; game pixels/FPS/lifetime UNMEASURED");

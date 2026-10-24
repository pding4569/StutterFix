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
    for(int i=0;i<4;i++)gate.Window(i,i==1||i==2,i==1||i==2?220:165,true,0,109,600);
    for(int i=0;i<2;i++)gate.Pixel(i,0,0,10,2,10,2,true);
    for(int i=0;i<4;i++)gate.Stability(i,true);
    return gate;
}
Check(new RenderMergeGatePolicy().Verdict()=="ERROR","no evidence");
Check(Complete().Verdict()=="PASS","all thresholds inclusive");
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

using System;
using System.Collections.Generic;
using StutterFix;

int checks=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
Check(DrawCollapsePolicy.TextureBindings<=64 && DrawCollapsePolicy.ArrayBindings==4
    && DrawCollapsePolicy.DirectBindings>=26,"Unity shader cap includes arrays and borrowed textures; measured 26 direct sources fit");
Check(string.Join(",",DrawCollapsePolicy.AdmitArrayGroups(new[]{11,5,39},2048))=="11,5,39","measured three tiers fit without fallback");
Check(string.Join(",",DrawCollapsePolicy.AdmitArrayGroups(new[]{1,1,1,1,1},2048))=="1,1,1,1,0","fifth format/tier uses independent fallback");
Check(string.Join(",",DrawCollapsePolicy.AdmitArrayGroups(new[]{9,1},2))=="8,0","array depth splits consume bindings and overflow falls back");
Check(string.Join(",",DrawCollapsePolicy.AdmitArrayGroups(new[]{3,4,1},2))=="3,4,0","partial array capacity is not reused across incompatible tiers");
Check(DrawCollapsePolicy.AdmitArrayGroups(new[]{int.MaxValue},int.MaxValue)[0]==int.MaxValue,"capacity arithmetic does not overflow");
DrawCollapsePolicy.Row Row(int rank,bool owned,int texture=1,int renderer=0,int slot=0,string signature="s",int order=0)
    =>new DrawCollapsePolicy.Row(new SceneRenderOrder.Key(0,order,3000,0,rank,renderer,slot),owned,renderer,texture,signature);
DrawCollapsePolicy.Counts Count(params DrawCollapsePolicy.Row[] rows)=>DrawCollapsePolicy.Count(new List<DrawCollapsePolicy.Row>(rows));
Check(Count().Spans==0,"empty scene is no evidence, not one span");
Check(Count(Row(1,false)).BarrierVisible==0,"leading and trailing nonowned draws do not split owned spans");
var c=Count(Row(1,false),Row(4,true,1),Row(2,true,1),Row(3,true,2),Row(5,false));
Check(c.OwnedVisible==3 && c.BarrierVisible==0 && c.Spans==1 && c.SameTextureRuns==3,
    "material painter order retained through texture A/B/A without regrouping");
c=Count(Row(1,true),Row(2,false,renderer:10),Row(3,false,renderer:11),Row(4,true));
Check(c.Spans==2 && c.BarrierVisible==2 && c.SameTextureRuns==2,"consecutive barriers create one break");
c=Count(Row(1,true),Row(2,false,renderer:10),Row(3,false,renderer:10,slot:1),Row(4,true));
Check(c.BarrierVisible==1 && c.Spans==2,"multi-slot barrier counted once as renderer");
c=Count(Row(1,true),Row(2,true,signature:"other"),Row(3,true,order:1));
Check(c.Spans==1 && c.SameTextureRuns==3,"material signature and bucket constrain same-texture step A only");
c=Count(Row(1,true,renderer:10),Row(1,false,renderer:11),Row(2,true));
Check(c.AmbiguousTies==1,"same-material tie cannot silently authorize merging");
c=Count(Row(1,false,renderer:10),Row(1,true,renderer:11));
Check(c.AmbiguousTies==1,"leading barrier tie also invalidates painter order");
c=Count(Row(1,true,renderer:10),Row(1,false,renderer:11));
Check(c.AmbiguousTies==1,"trailing barrier tie also invalidates painter order");
c=Count(Row(1,true),Row(-2,false,renderer:8),Row(-3,true));
Check(c.Spans==2,"signed material IDs retain their observed painter order");
var population=new List<DrawCollapsePolicy.Row>();
for(int i=0;i<23547;i++)
{
    population.Add(Row(i*2,true));
    if(i%1000==999)population.Add(Row(i*2+1,false,renderer:i));
}
c=DrawCollapsePolicy.Count(population);
Check(c.OwnedVisible==23547 && c.Spans==24 && c.BarrierVisible==23,"whole-song population does not overflow or confuse slots");
foreach(var pair in new[]{(0,"eligible"),(150,"eligible"),(151,"hold_151_to_400"),(400,"hold_151_to_400"),(401,"too_many_barriers")})
    Check(DrawCollapsePolicy.SpanDecision(pair.Item1)==pair.Item2,"inclusive gate threshold "+pair.Item1);
// Byte-exact mip arithmetic, including sub-block compressed tails and odd dimensions.
Check(DrawCollapsePolicy.TextureBytes(2048,2048,1,4,4,16)==4194304,"DXT5 retains one MiB per 1024 square");
Check(DrawCollapsePolicy.TextureBytes(2048,2048,1,4,4,16)*301==1262485504L,"301 full-size DXT5 base levels");
Check(DrawCollapsePolicy.TextureBytes(2048,2048,1,1,1,4)*301==5049942016L,"rejected RGBA32 worst case explicit");
Check(DrawCollapsePolicy.TextureBytes(5,7,1,4,4,16)==64,"compressed blocks round up");
Check(DrawCollapsePolicy.TextureBytes(4,4,3,4,4,8)==24,"DXT1 mip tails consume a whole block each");
Check(DrawCollapsePolicy.TextureBytes(4,4,3,1,1,4)==84,"RGBA mip chain");
Check(DrawCollapsePolicy.TextureBytes(5,3,3,1,1,1)==18,"non-square odd mip chain");
bool overflow=false;try{DrawCollapsePolicy.TextureBytes(int.MaxValue,int.MaxValue,1,1,1,16);}
catch(OverflowException){overflow=true;}Check(overflow,"memory overflow fails closed");
foreach(var values in new[]{(0,4,1),(4,4,0),(1,1,2)})
{
    bool rejected=false;try{DrawCollapsePolicy.TextureBytes(values.Item1,values.Item2,values.Item3,4,4,16);}
    catch(ArgumentException){rejected=true;}Check(rejected,"invalid dimensions/mips rejected");
}
var plan=DrawCollapsePolicy.Plan(new List<DrawCollapsePolicy.Row>{Row(5,true,renderer:5),Row(1,true,renderer:1),
    Row(3,false,renderer:20),Row(2,true,renderer:2),Row(4,true,renderer:4)});
Check(plan.Count==2 && string.Join(",",plan[0])=="1,2" && string.Join(",",plan[1])=="4,5","mapped slots retain painter order around barrier");
bool tie=false;try{DrawCollapsePolicy.Plan(new List<DrawCollapsePolicy.Row>{Row(1,false,renderer:1),Row(1,true,renderer:2)});}
catch(InvalidOperationException){tie=true;}Check(tie,"implementation planner refuses ambiguous ties");
Check(DrawCollapsePolicy.SmallDxtTier(100,100,true,1)==512,"small block-aligned DXT5 tier");
Check(DrawCollapsePolicy.SmallDxtTier(1000,2000,true,1)==2048,"non-square DXT5 tier");
Check(DrawCollapsePolicy.SmallDxtTier(1080,1080,true,1)==2048,"1080 DXT5 padding tier");
foreach(var item in new[]{(101,100,true,1),(512,512,false,1),(512,512,true,2),(5000,5000,true,1)})
    Check(DrawCollapsePolicy.SmallDxtTier(item.Item1,item.Item2,item.Item3,item.Item4)==0,"unsupported array copy borrows source 2D");
DrawCollapseGatePolicy Gate(int maxdiff=3,double fps=220,int draws=300,int spikes=0,bool valid=true)
{
    var g=new DrawCollapseGatePolicy();
    for(int i=0;i<4;i++){g.Window(i,i==1||i==2?fps:168,draws,spikes,valid);g.Restore(i,true);}
    for(int i=0;i<3;i++)g.Pixel(i,maxdiff,true);
    return g;
}
Check(Gate().Passed,"inclusive FPS/draw/pixel boundary succeeds with complete evidence");
Check(!new DrawCollapseGatePolicy().Passed,"empty evidence never passes");
Check(!Gate(maxdiff:4).Passed,"pixel max4 fails even with sufficient FPS");
Check(!Gate(fps:219.999).Passed,"FPS below220 fails");
Check(!Gate(draws:301).Passed,"draw301 fails");
Check(!Gate(spikes:1).Passed,"30ms spike fails");
Check(!Gate(valid:false).Passed,"ownership/environment failure cannot pass");
Check(!Gate(fps:double.NaN).Passed && !Gate(fps:double.PositiveInfinity).Passed,"nonfinite measurement fails");
var duplicate=Gate();duplicate.Pixel(0,0,true);Check(!duplicate.Passed,"duplicate evidence fails");
var unrestored=Gate();unrestored.Restores[3]=false;Check(!unrestored.Passed,"missing restore evidence fails");
Console.WriteLine("DrawCollapseCheck checks="+checks+" errors=0 scope=offline_policy_only");

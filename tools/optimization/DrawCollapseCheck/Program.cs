using System;
using System.Collections.Generic;
using StutterFix;

int checks=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
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
Console.WriteLine("DrawCollapseCheck checks="+checks+" errors=0 scope=offline_policy_only");

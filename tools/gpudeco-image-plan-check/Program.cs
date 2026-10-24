using System;
using StutterFix;
using Input=StutterFix.GpuDecoImageTimeline.Input;

int checks=0;
void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
void Reject(Action run,string label){bool rejected=false;try{run();}catch(ArgumentException){rejected=true;}Check(rejected,label);}
var targets=new[]{2,0,2};
var input=new[]{new Input(0,11,5,1,"private/path/A",targets),new Input(1,12,5,0,null,new[]{1}),
    new Input(2,11,5,1,"private/path/a",new[]{2}),new Input(3,13,3,1,"",new[]{1})};
var p=GpuDecoImageTimeline.Compile(4,input);
Check(p.EventCount==3&&p.SelectorCount==3&&p.TargetReferences==5,"counts and case-sensitive selectors");
Check(p.DecorationsWithImages==3&&p.EmptySelectorEvents==1&&p.RepeatedSources==1,"empty used selector and repeated IDs");
Check(p.Targets(0).SequenceEqual(new[]{2,0,2}),"target order and duplicates");
Check(p.EventsForDecoration(2).SequenceEqual(new[]{0,0,1}),"CSR occurrence order and duplicates");
Check(p.Event(2).Ordinal==3&&p.Event(2).Scheduled==3,"do not reorder by scheduled time");
Check(p.EventsForDecoration(3).Length==0,"no fabricated initial image");
Check(p.HeaderMatches(0,"private/path/A",1),"exact selector and flags");
Check(!p.HeaderMatches(0,"private/path/a",1)&&!p.HeaderMatches(0,"private/path/A",3),"changed image or flags reject");
Check(p.HeaderMatches(2,"",1)&&!p.HeaderMatches(2,null,1),"empty used name is not an unused field");
targets[0]=1;input[0]=new Input(0,1,0,1,"changed",new[]{1});
Check(p.Targets(0)[0]==2&&p.Selector(0)=="private/path/A","snapshot does not alias mutable inputs");
Reject(()=>GpuDecoImageTimeline.Compile(-1,Array.Empty<Input>()),"negative decoration count");
Reject(()=>GpuDecoImageTimeline.Compile(2,new[]{new Input(0,1,0,1,"A",new[]{2})}),"outside target");
Reject(()=>GpuDecoImageTimeline.Compile(2,new[]{new Input(0,1,double.NaN,1,"A",new[]{0})}),"nonfinite clock");
Reject(()=>GpuDecoImageTimeline.Compile(2,new[]{new Input(0,1,0,0,"A",new[]{0})}),"selector without used flag");
Reject(()=>GpuDecoImageTimeline.Compile(2,new[]{new Input(0,1,0,1,null,new[]{0})}),"used flag without selector");
Reject(()=>GpuDecoImageTimeline.Compile(2,new[]{new Input(0,1,0,1,"A",null)}),"missing target list");
Reject(()=>GpuDecoImageTimeline.Compile(2,new[]{new Input(0,1,0,16,null,new[]{0})}),"unknown visual flag");
Reject(()=>GpuDecoImageTimeline.Compile(2,new[]{new Input(0,1,0,1,"A",new[]{0}),new Input(0,1,0,1,"B",new[]{0})}),"duplicate occurrence ordinal");
var many=new Input[4097];for(int i=0;i<many.Length;i++)many[i]=new Input(i,i,i,1,"image"+i,new[]{0});
Reject(()=>GpuDecoImageTimeline.Compile(1,many),"selector capacity");
var empty=GpuDecoImageTimeline.Compile(0,Array.Empty<Input>());
Check(empty.EventCount==0&&empty.DecorationCount==0,"empty map");
Console.WriteLine("image_plan_checks="+checks+" failed=0 pixel_equivalence=UNMEASURED GPU_image_playback=False");

using StutterFix;

int checks=0;
void Check(bool ok,string why) { checks++; if(!ok) throw new Exception(why); }
byte[] Save(GpuDecoData data) { using var stream=new MemoryStream(); data.Write(stream); return stream.ToArray(); }
GpuDecoData Load(byte[] bytes) { using var stream=new MemoryStream(bytes); return GpuDecoData.Read(stream); }
void Reject(byte[] bytes,string why) { try { Load(bytes); } catch (Exception ex) when (ex is IOException || ex is InvalidDataException) { checks++; return; } throw new Exception(why); }
Check(!GpuDecoCoverage.OwnedLow(24,17) && GpuDecoCoverage.Auxiliary(24), "sparse 276s must remain auxiliary");
Check(!GpuDecoCoverage.OwnedLow(799,0), "sparse scene incorrectly gated");
Check(GpuDecoCoverage.OwnedLow(800,559), "below 70 percent accepted");
Check(!GpuDecoCoverage.OwnedLow(800,560) && !GpuDecoCoverage.Auxiliary(800), "70 percent boundary rejected");
Check(GpuDecoCoverage.OwnedLow(801,560), "fractional coverage truncated");
Check(!GpuDecoCoverage.OwnedLow(1000,700), "owned below 800 at 70 percent rejected");
Check(!GpuDecoCoverage.OwnedLow(2134,1730) && !GpuDecoCoverage.OwnedLow(2270,1875), "real primary coverage rejected");
var item=new GpuDecoData.Item { Placement="Global",Triangles=new[]{0,2,1,2,3,1} };
for(int ch=0;ch<16;ch++) item.Tracks[ch]=Array.Empty<DecoBakeCore.Segment>();
item.Tracks[5]=new[] {
    new DecoBakeCore.Segment { T0=269.123456789,Duration=2,Start=1,End=3,Target=5,Over=1.70158f,Ease=1,IsScale=true },
    new DecoBakeCore.Segment { T0=269.123456789,Duration=0,Start=3,End=7,Target=9,Ease=1,IsScale=true } };
var data=new GpuDecoData { Fingerprint="fixture",RawTime=270,BakeTime=269.9999999,Pitch=1,TweenScale=1,PixelsPerWorld=72,Items=new[]{item} };
byte[] bytes=Save(data); var loaded=Load(bytes);
Check(loaded.Items[0].Tracks[5][0].T0==item.Tracks[5][0].T0,"double time lost");
Check(loaded.Items[0].Tracks[5][0].IsScale && loaded.Items[0].Tracks[5][0].Target==5,"scale target/end lost");
Check(DecoBakeCore.Find(loaded.Items[0].Tracks[5],269.123456789)==1,"identical time last segment");
Check(loaded.BakeTime==data.BakeTime && loaded.RawTime==270,"clock time lost");
Check(loaded.Items[0].Constants.Length==8,"two-axis texture scale layout");
Reject(bytes[..^1],"truncated export accepted");
Reject(bytes.Concat(new byte[]{0}).ToArray(),"trailing bytes accepted");
var corrupt=(byte[])bytes.Clone(); corrupt[0]=0; Reject(corrupt,"wrong magic accepted");
corrupt=(byte[])bytes.Clone(); corrupt[4]=99; Reject(corrupt,"wrong schema accepted");
data.Pitch=0; Reject(Save(data),"zero clock pitch accepted"); data.Pitch=1;
item.Tracks[5][1].T0=1; Reject(Save(data),"unsorted track accepted"); item.Tracks[5][1].T0=269.123456789;
item.Tracks[5][1].Duration=-1; Reject(Save(data),"negative duration accepted"); item.Tracks[5][1].Duration=0;
item.Tracks[5][1].Over=float.NaN; Reject(Save(data),"nonfinite segment accepted"); item.Tracks[5][1].Over=0;
item.Triangles[0]=4; Reject(Save(data),"invalid quad index accepted");
item.Triangles[0]=0;
data.Frame=200; data.MeanProgressStep=data.MeanFrameSeconds=1f/120; data.FrameSamples=128;
data.Sources=new[]{new GpuDecoData.SourceClock { Id=123,StartFrame=101,StartSong=269.9,Progress=.42f }};
item.Anchors[0]=new GpuDecoData.Anchor { Valid=true,Clock=0,Segment=new DecoBakeCore.Segment { Duration=2,Start=10,Target=30,End=30,Ease=1 } };
item.Tracks[5][0].Source=77; item.Tracks[5][0].Replacement=true;
loaded=Load(Save(data));
Check(loaded.Schema==2 && loaded.Sources[0].Progress==.42f && loaded.Sources[0].StartFrame==101,"source progress/start lost");
Check(loaded.Items[0].Tracks[5][0].Source==77 && loaded.Items[0].Tracks[5][0].Replacement,"source/replacement lost");
float Linear(int id,float t,float duration,float over,float period)=>t/duration;
Check(Math.Abs(loaded.Eval(loaded.Items[0],0,Linear)-14.2f)<1e-5,"anchor evaluation did not use real progress");
loaded.BakeTime+=100;
Check(Math.Abs(loaded.Eval(loaded.Items[0],0,Linear)-14.2f)<1e-5,"paused anchor changed with song time");
loaded.Items[0].Actual[0]=9999;
Check(Math.Abs(loaded.Eval(loaded.Items[0],0,Linear)-14.2f)<1e-5,"truth output leaked into input");
loaded.Sources[0].Progress=.5f;
Check(loaded.Eval(loaded.Items[0],0,Linear)==15,"source progress update ignored");
var anchor=item.Anchors[0]; anchor.Clock=1; item.Anchors[0]=anchor; Reject(Save(data),"missing source clock accepted");
anchor.Clock=-1; item.Anchors[0]=anchor; Reject(Save(data),"negative running clock accepted");
anchor.Segment.Duration=0; anchor.Segment.End=17; item.Anchors[0]=anchor;
loaded=Load(Save(data)); Check(loaded.Eval(loaded.Items[0],0,Linear)==17,"completed endpoint accessed negative clock");
data.Sources[0].Progress=-1; Reject(Save(data),"negative progress accepted"); data.Sources[0].Progress=.42f;
data.Sources[0].StartFrame=201; Reject(Save(data),"future source start accepted"); data.Sources[0].StartFrame=101;
data.MeanFrameSeconds=0; Reject(Save(data),"missing frame mean accepted"); data.MeanFrameSeconds=1f/120;
data.MeanProgressStep=0; Reject(Save(data),"missing source step accepted"); data.MeanProgressStep=1f/120;
data.FrameSamples=257; Reject(Save(data),"frame sample count outside ring accepted"); data.FrameSamples=128;
foreach(string directory in args)
foreach(string path in Directory.GetFiles(directory,"*.gpudeco").Order())
{
    using var stream=File.OpenRead(path); var legacy=GpuDecoData.Read(stream);
    Check(legacy.Schema==1 && legacy.Sources.Length==0 && legacy.Items.All(d=>d.Anchors.All(a=>!a.Valid)),"legacy truth fabricated anchors");
    try { Save(legacy); throw new Exception("legacy relabeled as v2"); } catch (InvalidDataException) { checks++; }
    Console.WriteLine($"legacy={Path.GetFileName(path)} schema={legacy.Schema} owned={legacy.Items.Length} awake={legacy.Awake} sources=0 anchored_comparison=UNAVAILABLE");
}
Console.WriteLine($"GPU decoration data contract: {checks} PASS; GPU/game evidence UNMEASURED");

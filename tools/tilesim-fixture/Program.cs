#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StutterFix;
using UnityEngine;
using DG.Tweening;

namespace UnityEngine
{
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z) { this.x=x; this.y=y; this.z=z; } }
    public static class Time { public static int frameCount; }
    public class Camera { public bool orthographic = true; public float pixelHeight = 1440, orthographicSize = 5; }
}
namespace DG.Tweening { public enum Ease { Linear, InQuad } public static class DOTween { public static float timeScale = 1; } }
public class scrFloor { public int GetInstanceID() => 123; }
public class scrConductor { public static scrConductor instance = new scrConductor(); public double songposition_minusi; public Song song = new Song(); public class Song { public float pitch = 1; } }
public class scrCamera { public static scrCamera instance = new scrCamera(); public Camera camobj = new Camera(); }
public class Level { public List<scrFloor> listFloors = new List<scrFloor>(); }
public class ADOBase { public static Level lm = new Level(); }
namespace StutterFix
{
    internal static class FloorBake {
        internal static FloorBakeCore.Table CaptureTable;
        internal static int Generation = 7;
        internal struct Start { internal int Source, Frame; internal double Song; }
    }
    internal static class Hitch { internal static bool Playing; }
    internal static class TileCost { internal static bool Active; }
    internal static class ZeroTween
    {
        internal static bool CanEase = true;
        internal static float Eval(Ease e, float t, float d, float o, float p) { float x=t/d; return e==Ease.InQuad ? x*x : x; }
    }
    internal static class Main
    {
        internal static EntryType Entry = new EntryType();
        internal class EntryType { public LoggerType Logger = new LoggerType(); }
        internal class LoggerType { public List<string> Lines = new List<string>(); public void Log(string s) { Lines.Add(s); Console.WriteLine(s); } }
    }
    internal static class FloorAnim
    {
        internal static bool Enabled=true, Installed=true, DefaultPath=true;
        internal static long Errors, Created, Completed, Killed, Dropped, ZeroWrites, Real, Fallbacks;
        internal static List<Rec> Records = new List<Rec>();
        internal sealed class Rec
        {
            internal scrFloor F; internal int Key; internal Ease E;
            internal float Dur=1, Pos, Over, Period, FStart, FEnd, FChange, FLast;
            internal Vector3 VStart,VEnd,VChange,VLast;
            internal bool Started, Running=true;
        }
        internal static List<Rec> SimRecords() => Records;
        internal static bool SimDefaultPath() => DefaultPath;
    }
}
static class Program
{
    static int checks;
    static void Require(bool ok, string label) { if(!ok) throw new Exception(label); checks++; }
    static FloorAnim.Rec New(scrFloor f,int key,float start,float end, Ease ease=Ease.Linear)
    {
        var r=new FloorAnim.Rec { F=f,Key=key,FStart=start,FEnd=end,E=ease };
        if(key==3) r.VEnd=new Vector3(end,0,0);
        if(key==4) r.VEnd=new Vector3(0,end,0);
        FloorAnim.Records.Add(r); FloorAnim.Created++;
        if(TileSim.Capturing) TileSim.Created(r);
        return r;
    }
    // Oracle uses Rec arithmetic and does NOT read replay outputs.
    static void Startup(FloorAnim.Rec r)
    {
        r.Started=true; r.FChange=r.FEnd-r.FStart;
        r.VStart=new Vector3(r.FStart,r.FStart,1);
        r.VChange=new Vector3(r.VEnd.x-r.VStart.x,r.VEnd.y-r.VStart.y,r.VEnd.z-r.VStart.z);
        TileSim.Started(r);
    }
    static void Value(FloorAnim.Rec r)
    {
        float e=ZeroTween.Eval(r.E,r.Pos,r.Dur,r.Over,r.Period);
        r.FLast=r.FStart+r.FChange*e;
        r.VLast=new Vector3(r.VStart.x+r.VChange.x*e,r.VStart.y+r.VChange.y*e,1);
    }
    static void Complete(FloorAnim.Rec r)
    {
        if(!r.Started) Startup(r);
        r.Pos=r.Dur; r.Running=false; Value(r); FloorAnim.Killed++;
        TileSim.Completed(r);
    }
    static void Frame(float dt)
    {
        TileSim.BeginFrame(FloorAnim.Records,false);
        if(!TileSim.Capturing) return;
        if(!(dt<1E-06f && dt> -1E-06f)) foreach(var r in FloorAnim.Records)
        {
            if(!r.Running) continue;
            if(!r.Started) Startup(r);
            float to=r.Pos+dt;
            if(to>=r.Dur) { to=r.Dur; r.Running=false; FloorAnim.Completed++; }
            r.Pos=to; Value(r); TileSim.Stepped(r);
        }
        TileSim.EndFrame(FloorAnim.Records,dt);
    }
    static void Deadline() => typeof(TileSim).GetField("deadline",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,0L);
    static void VerifyZero(string label)
    {
        int begin=StutterFix.Main.Entry.Logger.Lines.Count;
        TileSim.Command("verify");
        var lines=StutterFix.Main.Entry.Logger.Lines.Skip(begin).ToArray();
        Require(lines.Count(s=>s.Contains("verify ")&&s.Contains("frames="))==2,label+" modes");
        Require(lines.Where(s=>s.Contains("verify ")).All(s=>s.Contains("mismatch=0")),label+" mismatch");
        Require(!lines.Any(s=>s.Contains("판정불가")),label+" samples");
    }
    static void Run()
    {
        Console.WriteLine(TileSim.SelfTest());
        TileSim.Command("capture 4"); Require(!TileSim.Capturing,"reject not-playing");
        Hitch.Playing=true; var floor=new scrFloor(); ADOBase.lm.listFloors.Add(floor);
        var recs=new List<FloorAnim.Rec>();
        for(int k=0;k<6;k++) recs.Add(New(floor,k,2+k,12+k));
        // Start capture with both started and lazy initial records.
        recs[0].Started=true; recs[0].FChange=10; recs[0].Pos=.125f;
        TileSim.Command("capture 4"); Require(TileSim.Capturing,"capture started");
        Frame(0); Frame(-.125f); Frame(.375f);
        Complete(recs[0]); // external Kill(true), then same-slot replacement
        var replacement=New(floor,0,12,20,Ease.InQuad);
        recs[1].Running=false; FloorAnim.Dropped++; TileSim.Cancelled(recs[1]);
        Complete(recs[2]); // complete before first step / reused record object
        recs[2].Pos=0; recs[2].Started=false; recs[2].Running=true; recs[2].FEnd=55;
        TileSim.Created(recs[2]);
        Frame(1E-07f); Frame(.25f); Frame(2);
        Deadline(); Frame(0); Require(!TileSim.Capturing,"capture stopped"); VerifyZero("lifecycle");
        int benchStart=StutterFix.Main.Entry.Logger.Lines.Count;
        TileSim.Command("bench");
        Require(StutterFix.Main.Entry.Logger.Lines.Skip(benchStart).Any(s=>s.Contains("bench-during-song")),"reject live bench");
        Hitch.Playing=false; TileSim.Command("bench"); Hitch.Playing=true;
        var bench=StutterFix.Main.Entry.Logger.Lines.Skip(benchStart).Where(s=>s.Contains("bench mode=")).ToArray();
        Require(bench.Length==2 && bench.All(s=>s.Contains("unaccounted")),"bench both modes and residual");
        Require(bench[0].Split("checksum=")[1].Split(' ')[0]==bench[1].Split("checksum=")[1].Split(' ')[0],"bench output checksum parity");
        // Inject a wrong live value to prove verifier can fail.
        FloorAnim.Records.Clear(); New(floor,5,4,20);
        TileSim.Command("capture 4");
        TileSim.BeginFrame(FloorAnim.Records,false);
        var bad=FloorAnim.Records[0]; Startup(bad); bad.Pos=.5f; Value(bad); bad.FLast+=1;
        TileSim.Stepped(bad); Deadline(); TileSim.EndFrame(FloorAnim.Records,.5f);
        int before=StutterFix.Main.Entry.Logger.Lines.Count; TileSim.Command("verify");
        Require(StutterFix.Main.Entry.Logger.Lines.Skip(before).Any(s=>s.Contains("mismatch=1")),"detect injected mismatch");
        TileSim.Command("capture 4"); FloorAnim.DefaultPath=false; Frame(.1f);
        Require(!TileSim.Capturing,"invalidate path change");
        before=StutterFix.Main.Entry.Logger.Lines.Count; TileSim.Command("verify");
        Require(StutterFix.Main.Entry.Logger.Lines.Skip(before).Any(s=>s.Contains("유효한 완료 테이프 없음")),"reject invalid tape");
        FloorAnim.DefaultPath=true; TileSim.Shutdown();
        FloorBake.CaptureTable = FloorBakeCore.Compile(new[] { new FloorBakeCore.Tile { Id=123, Initial=new float[6] } },
            Array.Empty<FloorBakeCore.Event>(), 1, 1, (id,t,d,o,p)=>t/d, System.Threading.CancellationToken.None);
        TileSim.Command("capture 4");
        UnityEngine.Time.frameCount=200; scrConductor.instance.songposition_minusi=530;
        TileSim.RecordBakeStart(new FloorBake.Start { Source=9, Frame=200, Song=530 });
        Deadline(); Frame(.1f);
        var tape=TileSim.BakeTape;
        Require(tape.BakeTable==FloorBake.CaptureTable && tape.BakeGeneration==7 && tape.TileIds[0]==123,"bake table/identity/generation frozen");
        Require(tape.Frames[0].SongTime==530 && tape.Frames[0].UnityFrame==200 && tape.Frames[0].PixelsPerWorld==144,"clock/frame/actual viewport scale recorded");
        Require(tape.Starts.Count==1 && tape.Starts[0].Source==9,"start instance recorded");
        TileSim.InvalidateBake("changed"); Require(tape.BakeInvalid=="changed","old tape invalidated explicitly");
        TileSim.Command("capture 4"); Frame(.1f);
        scrConductor.instance.songposition_minusi=529; Deadline(); Frame(.1f);
        Require(TileSim.BakeTape.BakeInvalid.Contains("backwards"),"backward clock invalidates bake comparison");
        TileSim.Shutdown(); Hitch.Playing=false;
        Console.WriteLine("TileSim fixture passed="+checks+"; synthetic .NET only; no Unity/Mono claim");
    }
    static void Main(string[] args) { Run(); }
}


#endif

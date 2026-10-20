#if DEV || AUTOTEST
// Pure managed capture/replay fixture. No game assemblies, installation or process.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StutterFix;
using UnityEngine;
using DG.Tweening;

namespace UnityEngine
{
    public static class Time { public static int frameCount; public static float deltaTime, smoothDeltaTime; }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
}
namespace DG.Tweening { public enum Ease { Linear, InQuad } public static class DOTween { public static float timeScale = 1; public static bool useSmoothDeltaTime; } }
public class scrConductor
{
    public static scrConductor instance = new scrConductor(); public static float calibration_i;
    public double songposition_minusi, addoffset, dspTimeSongPosZero, crotchetAtStart, adjustedCountdownTicks;
    public bool separateCountdownTime; public Song song = new Song();
    public class Song { public float pitch = 1; }
}
public class scnEditor { public static scnEditor instance; }
public class scrCamera
{
    public static scrCamera instance;
    public Camera camobj;
    public class Camera { public bool orthographic; public float orthographicSize; }
}
public class scrDecoration
{
    public int GetInstanceID() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    public Vector2 Position, Scale = new Vector2(1, 1);
    public float Rotation, Opacity = 1;
    public Color Color = new Color(1, 1, 1, 1);
}
public class scrDecorationManager
{
    public static scrDecorationManager instance = new scrDecorationManager();
    public List<scrDecoration> allDecorations = new List<scrDecoration>();
}
namespace StutterFix
{
    internal static class DecoBake
    {
        internal struct StartStamp { internal int Source, Frame; internal double Song; }
        internal static int Generation => -1;
        internal static DecoBakeCore.Table CaptureTable => null;
        internal static double BakeTime => scrConductor.instance.songposition_minusi;
    }
    internal static class Hitch { internal static bool Playing; }
    internal static class ZeroTween
    {
        internal static bool CanEase = true;
        internal static float Eval(Ease e, float t, float d, float o, float p) { float x = t / d; return e == Ease.InQuad ? x * x : x; }
    }
    internal static class Main
    {
        internal static readonly EntryType Entry = new EntryType();
        internal class EntryType { public LoggerType Logger = new LoggerType(); }
        internal class LoggerType
        {
            public List<string> Lines = new List<string>();
            public void Log(string line) { Lines.Add(line); Console.WriteLine(line); }
        }
    }
    internal static class DecoEngine { internal static bool Enabled; }
    internal static class NativeMotion { internal static bool Enabled; }
    internal static partial class DecoAnim
    {
        internal static bool Enabled = true, Installed = true, batchOn, GroupEnabled;
        internal static int AblateMask;
        internal static long Errors;
        private static readonly List<Rec> recs = new List<Rec>();
        internal sealed class Rec
        {
            internal scrDecoration D; internal int Key;
            internal float Dur, Pos, Over, Period, FStart, FEnd, FChange, FLast;
            internal Vector2 VStart, VEnd, VChange, VLast;
            internal Color CStart, CEnd, CChange, CLast;
            internal bool Started, Running = true; internal Ease E;
        }
        private static Vector2 pivotPosRef(scrDecoration d) => d.Position;
        private static Vector2 scaleRef(scrDecoration d) => d.Scale;
        private static float rotRef(scrDecoration d) => d.Rotation;
        private static float opaRef(scrDecoration d) => d.Opacity;
        private static Color colRef(scrDecoration d) => d.Color;
    }
}
internal static class Program
{
    private static int checks;
    private static void Check(bool b, string message) { if (!b) throw new Exception(message); checks++; }
    private static DecoSim.Tape Tape() => (DecoSim.Tape)typeof(DecoSim).GetField("tape", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
    private static DecoAnim.Rec Scalar(scrDecoration d, int k, float a, float b, bool started = true)
        => new DecoAnim.Rec { D = d, Key = k, Dur = 1, FStart = a, FEnd = b, FChange = b - a, E = Ease.InQuad, Started = started };
    private static void Advance(DecoAnim.Rec r, float dt)
    {
        if (!r.Running || Math.Abs(dt) < 1e-6f) return;
        if (!r.Started)
        {
            r.Started = true;
            if (r.Key == 7 || r.Key == 8)
            {
                r.VStart = r.D.Scale;
                r.VChange = new Vector2(r.VEnd.x - r.VStart.x, r.VEnd.y - r.VStart.y);
            }
            DecoSim.Started(r);
        }
        r.Pos = Math.Min(r.Dur, r.Pos + dt);
        float x = r.Pos / r.Dur, e = r.E == Ease.InQuad ? x * x : x;
        if (r.Key == 11)
        {
            var v = r.VStart; v.x += r.VChange.x * e; v.y += r.VChange.y * e; r.VLast = v;
        }
        else if (r.Key == 7 || r.Key == 8)
        {
            var v = r.D.Scale;
            if (r.Key == 7) v.x = r.VStart.x + r.VChange.x * e;
            else v.y = r.VStart.y + r.VChange.y * e;
            r.VLast = v; r.D.Scale = v;
        }
        else if (r.Key == 9)
        {
            var c = r.CStart;
            c.r += r.CChange.r * e; c.g += r.CChange.g * e; c.b += r.CChange.b * e; c.a += r.CChange.a * e;
            r.CLast = c;
        }
        else r.FLast = r.FStart + r.FChange * e;
        if (r.Pos >= r.Dur) r.Running = false;
        DecoSim.Stepped(r);
    }
    private static void Main()
    {
        Check(DecoSim.SelfTest().Contains("passed=10"), "built-in selftest");
        var d = new scrDecoration { Scale = new Vector2(3, 4) };
        scrDecorationManager.instance.allDecorations.AddRange(new[] { d, new scrDecoration(), new scrDecoration(), new scrDecoration() });
        var recs = DecoAnim.SimRecords();
        var x = Scalar(d, 1, -2.1f, 17.3f); x.Pos = .125f; recs.Add(x);
        var scale = new DecoAnim.Rec { D = d, Key = 8, Dur = 1, VEnd = new Vector2(6, 8), E = Ease.Linear }; recs.Add(scale);
        var color = new DecoAnim.Rec { D = d, Key = 9, Dur = 1, Started = true, E = Ease.InQuad,
            CStart = new Color(.1f, .2f, .3f, .4f), CEnd = new Color(1, .8f, .6f, .2f), CChange = new Color(.9f, .6f, .3f, -.2f) }; recs.Add(color);
        var cancel = Scalar(d, 5, 0, 9); recs.Add(cancel);
        foreach (int k in new[] { 2, 3, 4, 10, 12, 13 }) recs.Add(Scalar(d, k, -0.75f, 0.91f));
        recs.Add(new DecoAnim.Rec { D = d, Key = 7, Dur = 1, VEnd = new Vector2(6, 8), E = Ease.Linear });
        recs.Add(new DecoAnim.Rec { D = d, Key = 11, Dur = 1, Started = true, E = Ease.InQuad,
            VStart = new Vector2(.2f, .3f), VEnd = new Vector2(.8f, .1f), VChange = new Vector2(.6f, -.2f) });
        Hitch.Playing = true; DecoSim.Command("capture NaN"); Check(!DecoSim.Capturing, "NaN rejected");
        DecoSim.Command("capture 120"); Check(DecoSim.Capturing, "capture starts");
        for (int f = 0; f < 7; f++)
        {
            if (f == 2)
            {
                // Kill(true), complete sample, and same Rec object reused before next update.
                x.Pos = x.Dur; x.FLast = x.FEnd; x.Running = false; DecoSim.Completed(x);
                x.FStart = x.FEnd; x.FEnd = -3; x.FChange = x.FEnd - x.FStart; x.Pos = 0; x.Running = true;
                DecoSim.Created(x);
                cancel.Running = false; DecoSim.Cancelled(cancel); recs.Remove(cancel);
            }
            DecoSim.BeginFrame(recs, false);
            float dt = f == 0 ? 0 : .25f;
            foreach (var r in recs) Advance(r, dt);
            DecoSim.EndFrame(recs, dt);
        }
        Hitch.Playing = false; DecoSim.BeginFrame(recs, false);
        var tape = Tape(); Check(tape.Valid && tape.Frames.Count == 7, "complete tape on song stop");
        DecoSim.Command("verify");
        Check(StutterFix.Main.Entry.Logger.Lines.Count(s => s.Contains("frames=7 comparisons=") && s.Contains("mismatch=0")) == 2, "single/parallel tape verification");
        var sample = tape.Samples[0]; var broken = sample; broken.X += 1; tape.Samples[0] = broken;
        StutterFix.Main.Entry.Logger.Lines.Clear(); DecoSim.Command("verify");
        Check(StutterFix.Main.Entry.Logger.Lines.Count(s => s.Contains("불일치 mode=")) >= 2, "intentional corruption detected both modes"); tape.Samples[0] = sample;
        DecoSim.Command("bench");
        Check(StutterFix.Main.Entry.Logger.Lines.Count(s => s.Contains("bench mode=") && s.Contains("frames=301")) == 2, "benchmark at least 300 frames");
        // Sparse outputs and terminal target override, including non-unit ease endpoint.
        var eval = new DecoSim.DecoBatchEval(tape.SlotCount, tape.DecorationCount, 2); eval.Reset(tape);
        Check(eval.ScaleX[0] == 3 && eval.Opacity[0] == 1, "baseline outputs seeded");
        foreach (var frame in tape.Frames)
        {
            for (int i = frame.EventBegin; i < frame.EventEnd; i++) eval.Process(tape.Events[i]);
            eval.Evaluate(frame.Dt, true);
        }
        Check(eval.PosX[0] == -3 && eval.ScaleY[0] == 8 && eval.Alpha[0] == .2f, "terminal output targets");
        Hitch.Playing = true; DecoSim.Command("capture 120");
        DecoSim.BeginFrame(recs, true); Check(!DecoSim.Capturing && !Tape().Valid, "research switch invalidates");
        DecoSim.Command("capture 120"); Tape().Reserved = 512L * 1024 * 1024;
        DecoSim.Created(x); Check(!DecoSim.Capturing && !Tape().Valid, "memory cap stops capture");
        x.Running = true; DecoSim.Command("capture 120"); DecoSim.BeginFrame(recs, false);
        DecoSim.Stepped(x); DecoSim.Created(x);
        Check(!DecoSim.Capturing && Tape().Reason.Contains("재진입"), "reentrant lifecycle rejects unsupported tape");
        Console.WriteLine("fixture passed=" + checks + "; managed stubs only, no Mono/Unity performance claim");
    }
}
#endif

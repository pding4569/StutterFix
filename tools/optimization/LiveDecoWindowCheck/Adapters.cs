namespace UnityEngine
{
    internal static class Time { internal static int frameCount; internal static float realtimeSinceStartup; }
}

namespace StutterFix
{
    internal sealed class scrConductor
    {
        internal static scrConductor instance = new();
        internal double songposition_minusi;
        public static implicit operator bool(scrConductor value) => value != null;
    }
    internal static class Main { internal static readonly EntryData Entry = new(); }
    internal sealed class EntryData { internal readonly LoggerData Logger = new(); }
    internal sealed class LoggerData
    {
        internal readonly List<string> Lines = new();
        internal void Log(string value) => Lines.Add(value);
    }
    internal static class Hitch { internal static bool Playing = true; }
    internal static class DecoAnim
    {
        internal static bool Prof;
        internal static long[] ProfN = new long[16], ProfApply = new long[16], ProfSet = new long[16];
    }
    internal static class FrameCompare
    {
        internal static bool Paused;
        internal sealed class Result
        {
            internal int[] Max = { 1, 0, 0, 0 };
            internal long Changed = 1; // Synthetic nonzero pixels must never become PASS.
            internal string Regions = "adapter";
        }
        internal static Result GatePixel(out long restored, out bool stable)
        { restored = 0; stable = true; return new(); }
    }
    internal static class LiveDecoRenderer
    {
        internal static bool Active, Ready;
        internal static int Owned;
        internal static double TransitionMs;
        internal static float enabledAt;
        internal static float EnabledSeconds => !Ready ? 0 : UnityEngine.Time.realtimeSinceStartup - enabledAt;
    }
    internal static class LiveDeco
    {
        internal static void Stop()
        {
            LiveDecoGate2.RendererStopped();
            LiveDecoRenderer.Active = LiveDecoRenderer.Ready = false;
            LiveDecoRenderer.Owned = 0; LiveDecoRenderer.TransitionMs = 0;
        }
    }
    internal static class AutoTest
    {
        internal static string Stopped;
        internal static void StopLiveDecoTest(string reason) { Stopped = reason; }
    }
}

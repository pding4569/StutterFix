using System.Diagnostics;
using System.Globalization;

namespace StutterFix;

internal enum TransparencySortMode { Default, Orthographic, Perspective, CustomAxis }
internal struct Vector3 { internal float x, y, z; internal Vector3(float z) { x = y = 0; this.z = z; } }
internal sealed class Transform { internal Vector3 position, forward = new(1); }
internal sealed class Camera
{
    internal bool orthographic = true;
    internal TransparencySortMode transparencySortMode;
    internal Vector3 transparencySortAxis = new(1);
    internal Transform transform = new();
    internal int cullingMask = 652183;
}
internal static class GraphicsSettings
{
    internal static TransparencySortMode transparencySortMode;
    internal static Vector3 transparencySortAxis = new(1);
}
internal static class Time { internal static int frameCount; internal static float realtimeSinceStartup; }
internal static class LiveDecoChanges { internal static int Methods = 1723, Sites = 4039; }
internal static class LiveDecoGate2
{
    internal static string Error;
    internal static void Fail(string reason) { Error ??= reason; }
}
// Actual Unity submission/geometry and lifecycle are deliberately adapted.
// Production StartRendering, PreCull, SortSupported and EnabledSeconds are extracted unchanged.
internal static partial class ActivationAdapter
{
    internal static Camera camera;
    internal static bool Active, starting;
    internal static int lastFrame, ownedCount, Renders, Stops, reports;
    internal static int NextOwned = 2026;
    internal static long enableBegin, enableAlloc, setupEnd;
    internal static double transitionMs, prepareMs, registerMs, patchMs;
    internal static float enabledAt, nextReport, sortAxisZ, sortCameraZ;
    internal static int sortMask;
    internal static TransparencySortMode sortMode;
    internal static string error;
    internal static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    internal static readonly Dictionary<int, int> watchers = new();
    internal static readonly List<string> logs = new();
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static string Ms(long ticks) => (ticks * 1000.0 / Stopwatch.Frequency).ToString("F6", Inv);
    static void Log(string text) => logs.Add(text);
    static string State() => "on=" + Active;
    static void ReportCost(string phase) { reports++; Log("phase=" + phase); }
    static void RenderFrame()
    {
        if (!SortSupported()) throw new InvalidOperationException("sort/parent changed");
        lastFrame = Time.frameCount; Renders++; ownedCount = NextOwned;
        Time.realtimeSinceStartup += 2; // Simulated expensive first admission.
    }
    static void Stop() { Active = starting = false; ownedCount = 0; Stops++; }
    internal static bool Validate() => SortSupported();
    internal static void Cull(Camera c) => PreCull(c);
    internal static void Cancel() => Stop();
    internal static void Prepare(bool paused = false)
    {
        camera = new Camera(); Active = starting = false; lastFrame = -1;
        Time.frameCount = 1; Time.realtimeSinceStartup = paused ? 500 : 100;
        Renders = Stops = reports = ownedCount = 0; NextOwned = 2026;
        prepareMs = registerMs = patchMs = 0;
        LiveDecoGate2.Error = null; logs.Clear();
        GraphicsSettings.transparencySortMode = TransparencySortMode.Default;
        if (!SortSupported()) throw new Exception("initial camera unsupported");
        enableBegin = setupEnd = Stopwatch.GetTimestamp(); enableAlloc = GC.GetAllocatedBytesForCurrentThread();
        Active = starting = true; nextReport = float.PositiveInfinity;
    }
}

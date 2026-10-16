using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.Json;
using StutterFix;

// Tests only this process's owned, short-lived worker. No game installation or
// main/render thread scheduling is changed by this standalone program.
int checks = 0; Exception failure = null; ulong before = 0, applied = 0, after = 0, foreign = 0;
string unavailable = null;
void Check(bool ok, string reason) { checks++; if (!ok) throw new Exception(reason); }
var worker = new Thread(() =>
{
    try
    {
        if (!OperatingSystem.IsWindows() || Native.GetActiveProcessorGroupCount() != 1)
        { unavailable = "single-group Windows required"; return; }
        var thread = Native.GetCurrentThread();
        if (!Native.GetThreadGroupAffinity(thread, out var original) || original.Group != 0)
            throw new Exception("worker affinity unavailable");
        before = original.Mask.ToUInt64();
        if ((before & ~1UL) == 0) { unavailable = "no remaining eligible CPU"; return; }
        Hitch.Playing = true;
        ThreadScheduling.Configure(0, false, false); ThreadScheduling.Tick();
        Native.GetThreadGroupAffinity(thread, out var value);
        Check(value.Mask.ToUInt64() == before, "default off unchanged");
        ThreadScheduling.Configure(0, false, true); ThreadScheduling.Tick();
        Native.GetThreadGroupAffinity(thread, out value); applied = value.Mask.ToUInt64();
        Check(applied == (before & ~1UL), "explicit exclusion applied");
        ThreadScheduling.Configure(0, false, false); ThreadScheduling.Tick();
        Native.GetThreadGroupAffinity(thread, out value); after = value.Mask.ToUInt64();
        Check(after == before, "turning off restores owned mask");
        ThreadScheduling.Configure(0, false, true); ThreadScheduling.Tick();
        Hitch.Playing = false; ThreadScheduling.Tick();
        Native.GetThreadGroupAffinity(thread, out value);
        Check(value.Mask.ToUInt64() == before, "leaving play restores owned mask");
        Hitch.Playing = true; ThreadScheduling.Tick();
        ThreadScheduling.Shutdown();
        Native.GetThreadGroupAffinity(thread, out value);
        Check(value.Mask.ToUInt64() == before, "unload restores owned mask");
        if ((applied & (applied - 1)) != 0)
        {
            ThreadScheduling.Configure(0, false, true); ThreadScheduling.Tick();
            foreign = applied & (~applied + 1);
            if (Native.SetThreadAffinityMask(thread, new UIntPtr(foreign)) == UIntPtr.Zero)
                throw new Exception("foreign replacement fixture failed");
            ThreadScheduling.Shutdown();
            Native.GetThreadGroupAffinity(thread, out value);
            Check(value.Mask.ToUInt64() == foreign, "foreign replacement preserved");
            // This worker owns the fixture's replacement too; restore it now.
            if (Native.SetThreadAffinityMask(thread, original.Mask) == UIntPtr.Zero)
                throw new Exception("fixture restore failed");
        }
    }
    catch (Exception ex) { failure = ex; }
    finally { ThreadScheduling.Shutdown(); Hitch.Playing = false; }
});
worker.IsBackground = true; worker.Start();
if (!worker.Join(5000)) throw new Exception("owned worker did not complete");
if (failure != null) throw failure;
Console.WriteLine(JsonSerializer.Serialize(new
{
    conditions = checks, errors = 0, unavailable, before, applied, after, foreign,
    owned_worker_only = true, game_process_changed = false, irq_distribution_measured = false,
    game_performance_measured = false
}, new JsonSerializerOptions { WriteIndented = true }));

namespace UnityEngine { internal static class Time { internal static float unscaledTime => Environment.TickCount64 * .001f; } }
namespace StutterFix { internal static class Hitch { internal static bool Playing; } }
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct GroupAffinity { internal UIntPtr Mask; internal ushort Group, R0, R1, R2; }
    [DllImport("kernel32.dll")] internal static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")] internal static extern ushort GetActiveProcessorGroupCount();
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool GetThreadGroupAffinity(IntPtr thread, out GroupAffinity value);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern UIntPtr SetThreadAffinityMask(IntPtr thread, UIntPtr mask);
}

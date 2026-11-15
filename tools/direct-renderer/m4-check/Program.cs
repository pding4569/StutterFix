using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using HarmonyLib;
using StutterFix;
using UnityEngine;
using UnityEngine.SceneManagement;

static class Program
{
    static int cases;
    static void Check(bool condition, string why) { if (!condition) throw new Exception(why); }
    static void Throws(Action f) { bool threw = false; try { f(); } catch (InvalidOperationException) { threw = true; } Check(threw, "expected refusal"); }
    static Camera Fresh(int mask = -1)
    {
        DirectFull.Stop(); CheckClean();
        var c = new Camera { cullingMask = mask }; GpuDecoScene.Main = c;
        StutterFix.Main.Config.GpuDeco = 0; StutterFix.Main.Config.RunLod = false;
        DrProbe.Active = DirectDeco.Active = DirectDeco.Enabled = false; Harmony.FailPatchAt = -1;
        return c;
    }
    static void CheckClean()
    {
        Check(!DirectFull.Active && Camera.Subscribers == 0 && SceneManager.Subscribers == 0 && Harmony.Patches.Count == 0, "cleanup leak");
    }
    static void Restores(Action end)
    {
        var c = Fresh(); DirectFull.Command("ceil");
        Check(c.cullingMask != -1, "nothing removed"); end();
        Check(c.cullingMask == -1, "original mask not restored"); CheckClean(); cases++;
    }
    static void Audit(string root)
    {
        const string managed = "D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data/Managed";
        using (var stream = File.OpenRead(Path.Combine(managed, "Assembly-CSharp.dll")))
        using (var pe = new PEReader(stream))
        {
            var md = pe.GetMetadataReader();
            foreach (var pair in new[] { ("scrController", "Restart"), ("scrController", "ResetCustomLevel"), ("scrController", "QuitToMainMenu"),
                ("scnEditor", "SwitchToEditMode"), ("scnEditor", "QuitToMenu"), ("scnEditor", "Play"), ("scnGame", "ResetScene"), ("scnGame", "PrepVfx") })
            {
                var type = md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t => md.GetString(t.Name) == pair.Item1);
                var methods = type.GetMethods().Select(md.GetMethodDefinition).Where(m => md.GetString(m.Name) == pair.Item2).ToArray();
                Check(methods.Length > 0, "missing game endpoint " + pair);
                if (pair.Item2 == "PrepVfx") Check(methods.Any(m => m.GetParameters().Select(md.GetParameter).Count(p => p.SequenceNumber > 0) == 4), "PrepVfx arity changed");
            }
        }
        foreach (var edition in new[] {"Release", "Player", "PlayerAuto"})
        {
            using var stream = File.OpenRead(Path.Combine(root, "bin", edition, "StutterFix.dll"));
            using var pe = new PEReader(stream);
            var md = pe.GetMetadataReader();
            bool present = md.TypeDefinitions.Select(md.GetTypeDefinition).Any(t => md.GetString(t.Name) == "DirectFull");
            bool resource = md.ManifestResources.Select(md.GetManifestResource).Any(r => md.GetString(r.Name) == "StutterFix.drfull.shader-census");
            Check(present == (edition != "Player") && resource == present, "edition/default-off boundary " + edition);
        }
        Console.WriteLine("PASS installed game metadata endpoints=8; DEV/PlayerAuto include diagnostic+catalog; Player excludes both; no game methods executed");
    }
    static int Main(string[] args)
    {
        try { Run(args); return 0; }
        catch (Exception e) { Console.Error.WriteLine("FAIL " + e); return 1; }
    }
    static void Run(string[] args)
    {
        var c = Fresh(); DirectFull.Command("ceil"); int masked = c.cullingMask;
        Check(DirectFull.Command("assert ceil").Contains("PASS"), "active assertion");
        Throws(() => DirectFull.Command("assert off"));
        DirectFull.Command("ceil"); Check(c.cullingMask == masked, "nested activation changed mask");
        var oldDriver = (DirectFullLifetime)GameObject.LastComponent;
        DirectFull.Command("off"); DirectFull.Command("off"); Check(c.cullingMask == -1, "repeat off failed"); CheckClean(); cases++;
        Check(DirectFull.Command("assert off").Contains("PASS"), "off assertion");
        Throws(() => DirectFull.Command("assert ceil"));
        DirectFull.Command("ceil");
        DirectFull.LifetimeEnded(oldDriver); Check(DirectFull.Active, "deferred old destruction stopped new lease");
        DirectFull.LifetimeEnded((DirectFullLifetime)GameObject.LastComponent); Check(c.cullingMask == -1, "current driver cleanup"); CheckClean();
        c = Fresh(1 << 9); DirectFull.Command("ceil"); Check(c.cullingMask == 0, "zero live mask"); DirectFull.Stop(); Check(c.cullingMask == 1 << 9, "zero live mask restore"); cases++;
        c = Fresh(0); bool invalid = false; try { DirectFull.Command("ceil"); } catch (ArgumentException) { invalid = true; }
        Check(invalid && c.cullingMask == 0, "empty original must refuse unchanged"); CheckClean(); cases++;
        var random = new Random(1404);
        for (int i = 0; i < 10000; i++)
        {
            int original = random.Next() | (1 << 9) | (i % 2 == 0 ? int.MinValue : 0);
            c = Fresh(original); DirectFull.Command("ceil"); DirectFull.Stop(); Check(c.cullingMask == original, "random mask restoration");
        }
        CheckClean(); cases++;
        Restores(() => SceneManager.Changed()); Restores(() => SceneManager.Unloaded()); Restores(() => DirectFull.Stop());
        foreach (var pair in new[] { (typeof(scrController), "Restart"), (typeof(scrController), "ResetCustomLevel"), (typeof(scrController), "QuitToMainMenu"),
            (typeof(scnEditor), "SwitchToEditMode"), (typeof(scnEditor), "QuitToMenu"), (typeof(scnEditor), "Play"), (typeof(scnGame), "ResetScene"), (typeof(scnGame), "PrepVfx") })
            Restores(() => Harmony.Invoke(pair.Item1, pair.Item2));
        c = Fresh(); DirectFull.Command("ceil"); c.cullingMask = 123; DirectFull.CheckLease(); Check(c.cullingMask == -1, "external mask drift restoration"); CheckClean(); cases++;
        c = Fresh(); DirectFull.Command("ceil"); c.isActiveAndEnabled = false; DirectFull.CheckLease(); Check(c.cullingMask == -1, "camera disable restoration"); CheckClean(); cases++;
        c = Fresh(); DirectFull.Command("ceil"); c.Dead = true; DirectFull.CheckLease(); CheckClean(); cases++;
        for (int n = 1; n <= 8; n++)
        {
            c = Fresh(); Harmony.FailPatchAt = n; Throws(() => DirectFull.Command("ceil")); Check(c.cullingMask == -1, "activation rollback"); CheckClean();
        }
        cases++;
        c = Fresh(); StutterFix.Main.Config.GpuDeco = 1; Throws(() => DirectFull.Command("ceil")); CheckClean(); cases++;
        c = Fresh(); DrProbe.Active = true; Throws(() => DirectFull.Command("ceil")); CheckClean(); cases++;
        c = Fresh(); c.Buffers = new[] {new UnityEngine.Rendering.CommandBuffer {name="DR.cbtest"}}; Throws(() => DirectFull.Command("ceil")); CheckClean(); cases++;
        c = Fresh(); DirectFull.Command("ceil"); DirectDeco.Enabled = true; DirectFull.CheckLease(); Check(c.cullingMask == -1, "competitor cleanup"); CheckClean(); cases++;
        c = Fresh(); DirectFull.Command("ceil");
        for (int i = 0; i < 100; i++) { DirectFull.CheckLease(); Camera.Cull(c); }
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++) { DirectFull.CheckLease(); Camera.Cull(c); }
        long delta = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Check(delta == 0, "steady lifecycle checks allocate " + delta); DirectFull.Stop(); CheckClean(); cases++;
        Throws(() => DirectFull.Command("on")); cases++;
        Console.WriteLine($"PASS {cases} lifecycle/refusal cases; 10000 random masks; 100000 steady checks allocated={delta} bytes; Unity native callbacks/game performance=UNMEASURED");
        PreparationCases();
        if (args.Length > 0) Audit(Path.GetFullPath(args[0]));
    }
    static bool disposed;
    static System.Collections.IEnumerator PreparationSteps(int mode)
    {
        try
        {
            if (mode == 1) throw new InvalidOperationException("injected preparation failure");
            yield return null;
            if (mode == 2) { System.Threading.Thread.Sleep(6); yield return null; }
            DirectFull.FixturePublish();
        }
        finally { disposed = true; }
    }
    static void PreparationCases()
    {
        var c = Fresh(); disposed = false; DirectFull.FixturePrepare(PreparationSteps(0), c);
        DirectFull.PrepareTick(); Check(disposed && !DirectFull.FixturePreparing && c.cullingMask == -1, "preparation completion"); DirectFull.Stop(); CheckClean();
        c = Fresh(); disposed = false; DirectFull.FixturePrepare(PreparationSteps(1), c);
        DirectFull.PrepareTick(); Check(disposed && !DirectFull.FixturePreparing && c.cullingMask == -1, "preparation failure cleanup"); CheckClean();
        c = Fresh(); disposed = false; DirectFull.FixturePrepare(PreparationSteps(2), c);
        var old = (DirectFullPreparation)GameObject.LastComponent;
        DirectFull.PrepareTick(); Check(!disposed && DirectFull.FixturePreparing && c.cullingMask == -1, "preparation must yield with original mask");
        Throws(() => DirectFull.Command("assert off")); DirectFull.Stop(); Check(disposed, "cancel did not dispose iterator"); CheckClean();
        disposed = false; DirectFull.FixturePrepare(PreparationSteps(2), c); DirectFull.PreparationEnded(old);
        Check(DirectFull.FixturePreparing, "old preparation driver cancelled new preparation"); c.cullingMask = 123;
        DirectFull.PrepareTick(); Check(!DirectFull.FixturePreparing && c.cullingMask == 123, "external camera edit was overwritten"); CheckClean();
        Console.WriteLine("PASS preparation completion/failure/budget-yield/cancel/stale-driver/camera-drift; original Unity mask preserved; production pump with stubs");
    }
}

using System;
using StutterFix;

class RendererStub { internal bool Off, Destroyed, Enabled = true, Active = true; }
class Program
{
    static int checks;
    static void Check(bool value) { checks++; if (!value) throw new Exception("check " + checks); }
    static void Main()
    {
        // Exhaust the decision space: exclusions and ownership must partition all scene renderers.
        for (int bits = 0; bits < 512; bits++)
        {
            bool B(int n) => (bits & (1 << n)) != 0;
            var got = GpuSplitPolicy.Classify(B(0), B(1), B(2), B(3), B(4), B(5), B(6), B(7), B(8));
            var expected = !B(0) || !B(1) || B(2) ? GpuSplitGroup.None :
                B(3) && B(6) ? GpuSplitGroup.Particle : B(3) && B(5) ? GpuSplitGroup.Text :
                B(3) && B(4) ? GpuSplitGroup.Image : B(7) ? GpuSplitGroup.Tiles : B(8) ? GpuSplitGroup.Planets : GpuSplitGroup.Other;
            Check(got == expected);
            Check(got == 0 || ((int)got & ((int)got - 1)) == 0);
        }
        Check(GpuSplitPolicy.Parse("deco-text+deco-particle") == (GpuSplitGroup.Text | GpuSplitGroup.Particle));
        Check(GpuSplitPolicy.Parse("all") == GpuSplitGroup.All);
        Check(GpuSplitPolicy.Parse("none") == GpuSplitGroup.None);
        foreach (var bad in new[] { "", "bad", "tiles+", "none+tiles", "all+other" })
        { bool thrown = false; try { GpuSplitPolicy.Parse(bad); } catch (ArgumentException) { thrown = true; } Check(thrown); }
        var a = new RendererStub(); var b = new RendererStub { Off = true, Enabled = false, Active = false };
        var c = new RendererStub();
        var ledger = new GpuSplitLedger<RendererStub>(x => !x.Destroyed, x => x.Off, (x, v) => x.Off = v);
        ledger.Hide(a); ledger.Hide(b); ledger.Hide(a);
        Check(ledger.Count == 2 && a.Off && b.Off && !b.Enabled && !b.Active);
        a.Off = false; Check(ledger.Enforce() == 1 && a.Off);
        ledger.Hide(a); // refresh must retain original false, not overwrite with hidden true
        ledger.Enforce();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) ledger.Enforce();
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated);
        ledger.Hide(c); c.Destroyed = true;
        Check(ledger.Count == 3);
        ledger.Restore(); Check(!a.Off && b.Off && !b.Enabled && !b.Active && ledger.Count == 0);
        ledger.Restore(); Check(!a.Off && b.Off);
        a.Off = true; ledger.Hide(a); ledger.Restore(); Check(a.Off);
        ledger.Hide(b); b.Destroyed = true; ledger.Restore(); Check(ledger.Count == 0);
        Console.WriteLine($"GpuSplit policy/restore checks={checks}, errors=0; Unity runtime not exercised");
    }
}

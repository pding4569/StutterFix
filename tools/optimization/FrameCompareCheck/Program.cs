using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using StutterFix;

int n = 0;
void Check(bool value, string reason) { n++; if (!value) throw new Exception(reason); }
var a = new byte[65 * 35 * 4]; var b = (byte[])a.Clone();
var r = FrameCompareMetrics.Compare(a, b, 65, 35, 0);
Check(r.Changed == 0 && r.Ratio == 0 && double.IsPositiveInfinity(r.Psnr) && r.Regions == "", "identical negative control");
b[(34 * 65 + 64) * 4 + 3] = 255;
r = FrameCompareMetrics.Compare(a, b, 65, 35, 0);
Check(r.Changed == 1 && r.Max[3] == 255 && r.Max[0] == 0, "alpha-only difference");
Check(r.Ratio == 1.0 / (65 * 35) && Math.Abs(r.Psnr - 10 * Math.Log10(a.Length)) < 1e-10, "ratio/PSNR");
Check(r.Regions == "(64,32,1,3):255", "partial edge tile/bottom-left coordinates");
Check(FrameCompareMetrics.Compare(a, b, 65, 35, 255).Changed == 0, "strict > threshold");
b[0] = 1; b[1] = 2; b[2] = 3; b[3] = 4;
r = FrameCompareMetrics.Compare(a, b, 65, 35, 2);
Check(r.Changed == 2 && r.Max[0] == 1 && r.Max[1] == 2 && r.Max[2] == 3 && r.Regions.StartsWith("(64,32,1,3):255;"), "per channel max, count once, descending tile L1");
var all = new byte[64 * 64 * 4]; Array.Fill(all, (byte)255);
r = FrameCompareMetrics.Compare(new byte[all.Length], all, 64, 64, 0);
Check(r.Changed == 4096 && r.Ratio == 1 && r.Psnr == 0, "full positive control");
Check(r.Regions.StartsWith("(0,0,32,32):"), "tile ties deterministic");
foreach (int bad in new[] { -1, 256 })
{
    bool rejected = false; try { FrameCompareMetrics.Compare(a, b, 65, 35, bad); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "threshold guard");
}
bool mismatch = false; try { FrameCompareMetrics.Compare(a, new byte[4], 65, 35, 0); } catch (ArgumentException) { mismatch = true; }
Check(mismatch, "dimensions guard");
Console.WriteLine($"FrameCompareMetrics: {n} checks passed (offline RGBA8 only; no live GPU evidence)");
if (args.Length == 3)
{
    for (int i = 0; i < args.Length; i++)
    {
        using var stream = File.OpenRead(args[i]); using var pe = new PEReader(stream); var md = pe.GetMetadataReader();
        var names = md.TypeDefinitions.Select(h => md.GetString(md.GetTypeDefinition(h).Name)).ToHashSet();
        foreach (var name in new[] { "FrameCompare", "FrameCompareMetrics" })
            Check(names.Contains(name) == (i != 0), "edition isolation: " + args[i] + " " + name);
        if (i == 0) Check(!md.TypeReferences.Any(h => md.GetString(md.GetTypeReference(h).Name).StartsWith("FrameCompare")), "Player has no framecmp type references");
    }
    Console.WriteLine("FrameCompare edition metadata: 7 checks passed; Player absent (including references), Developer/PlayerAuto present");
}

using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;
using System.Collections.Generic;
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
int maskStart = n;
var pixels = new byte[100 * 100 * 4]; var altered = (byte[])pixels.Clone();
altered[(50 * 100 + 50) * 4] = 4;
altered[0] = 7;
var center = FrameCompareMetrics.Mask.Parse("maskpct=0.5,0.5,0.2,0.2");
r = FrameCompareMetrics.Compare(pixels, altered, 100, 100, 0, new[] { center });
Check(r.InsideChanged == 1 && r.Changed == 1 && r.InsidePixels == 400 && r.OutsidePixels == 9600, "center mask inside/outside counts");
Check(r.Max[0] == 7 && r.Ratio == 1.0 / 9600 && r.Regions == "(0,0,32,32):7", "outside metrics only");
Check(Math.Abs(r.Psnr - 10 * Math.Log10(255.0 * 255 * 9600 * 4 / 49)) < 1e-10, "outside PSNR denominator");
r = FrameCompareMetrics.Compare(pixels, altered, 100, 100, 0, new[] { center, center, FrameCompareMetrics.Mask.Parse("mask=-1,-1,2,2") });
Check(r.Changed == 0 && r.InsideChanged == 2 && r.InsidePixels == 401, "overlapping masks union; clipped negative origin");
Check(double.IsPositiveInfinity(r.Psnr), "excluded-only differences yield outside zero");
Check(FrameCompareMetrics.Compare(pixels, altered, 100, 100, 0).Changed == 2, "none retains full frame");
r = FrameCompareMetrics.Compare(pixels, altered, 100, 100, 4, new[] { center });
Check(r.InsideChanged == 0 && r.Changed == 1, "threshold applies inside and outside");
var edge = FrameCompareMetrics.Mask.Parse("mask=0,0,1,1");
Check(edge.Contains(0, 0, 100, 100) && !edge.Contains(1, 0, 100, 100) && !edge.Contains(0, 1, 100, 100), "bottom-left half-open edges");
r = FrameCompareMetrics.Compare(pixels, altered, 100, 100, 0, new[] { FrameCompareMetrics.Mask.Parse("maskpct=0.5,0.5,1,1") });
Check(r.OutsidePixels == 0 && double.IsNaN(r.Ratio) && double.IsNaN(r.Psnr), "no outside evidence not perfect score");
foreach (var invalid in new[] { "mask=1,2,0,4", "mask=NaN,0,1,1", "mask=0,Infinity,1,1", "maskpct=50,50,20,20", "mask=1,2,3", "bogus=0,0,1,1" })
{
    bool rejected = false;
    try { FrameCompareMetrics.Mask.Parse(invalid); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "mask guard " + invalid);
}
Console.WriteLine($"FrameCompare masks: {n - maskStart} checks passed (synthetic; no live evidence)");
int stateStart = n;
FrameCompareState Snapshot(bool visible, string field, string value)
{
    var state = new FrameCompareState();
    var entry = new FrameCompareState.Entry { Label = "fixture", Relevant = visible };
    entry.Fields[field] = value; state.Entries.Add("renderer:1", entry); return state;
}
var baseline = Snapshot(true, "enabled", "True");
Check(baseline.Compare(Snapshot(true, "enabled", "True")).Changed == 0, "identical state");
var delta = baseline.Compare(Snapshot(false, "enabled", "False"));
Check(delta.Changed > 0 && delta.Details.Contains("enabled=True->False"), "visible to hidden identifies field");
Check(Snapshot(false, "matrix", "1").Compare(Snapshot(false, "matrix", "2")).Changed == 0, "hidden transform drift ignored");
Check(Snapshot(false, "matrix", "1").Compare(Snapshot(false, "matrix", "2")).Ignored == 1, "hidden drift counted");
Check(new FrameCompareState().Compare(baseline).Changed == 1, "visible renderer added");
Check(baseline.Compare(new FrameCompareState()).Changed == 1, "visible renderer removed");
Check(new FrameCompareState().Compare(Snapshot(false, "enabled", "False")).Ignored == 1, "hidden membership counted");
foreach (string field in new[] { "size", "RT", "projection", "forceRenderingOff", "sortingOrder", "materials", "planetFrame" })
    Check(Snapshot(true, field, "old").Compare(Snapshot(true, field, "new")).Details.Contains(field + "=old->new"), "diagnostic field " + field);
Check(Snapshot(false, "enabled", "False").Compare(baseline).Changed > 0, "hidden to visible rejected");
Console.WriteLine($"FrameCompareState: {n - stateStart} checks passed (synthetic snapshots; no Unity lifecycle evidence)");
int controlStart = n;
Check(FrameCompareControlPolicy.FirstBehind(-100, -2), "signed negative IDs ascending");
Check(!FrameCompareControlPolicy.FirstBehind(-2, -100), "reverse allocation order");
Check(FrameCompareControlPolicy.FirstBehind(int.MinValue, int.MaxValue), "signed IDs across zero; no subtraction overflow");
foreach (string count in new[] { "NA", "INCONCLUSIVE", "-1", "", "9223372036854775808" })
    Check(FrameCompareControlPolicy.Verdict(count, true, true) == "FAIL", "missing/invalid counts fail: " + count);
Check(FrameCompareControlPolicy.Verdict("0", false, true) == "PASS", "negative zero passes");
Check(FrameCompareControlPolicy.Verdict("1", false, true) == "FAIL", "negative nonzero fails");
Check(FrameCompareControlPolicy.Verdict("0", false, false) == "FAIL", "negative unstable state fails despite zero pixels");
Check(FrameCompareControlPolicy.Verdict("0", true, true) == "FAIL", "positive zero fails");
Check(FrameCompareControlPolicy.Verdict("1", true, true) == "PASS", "positive nonzero passes");
Check(FrameCompareControlPolicy.Verdict("1", true, false) == "FAIL", "positive failed restoration fails");
// Analytic opaque overlap exercises the real signed-ID policy and masked pixel comparator.
// It models the rule; it cannot establish that Unity follows the rule in the main chain.
byte[] Overlap(bool swap, bool hide)
{
    var image = new byte[100 * 100 * 4];
    bool redFront = FrameCompareControlPolicy.FirstBehind(swap ? -2 : -100, swap ? -100 : -2) == false;
    for (int y = 12; y < 24; y++) for (int x = 12; x < 24; x++)
    {
        int offset = (y * 100 + x) * 4;
        image[offset + (redFront || hide ? 0 : 2)] = 255; image[offset + 3] = 255;
    }
    return image;
}
var overlap = Overlap(false, false);
Check(FrameCompareMetrics.Compare(overlap, Overlap(true, false), 100, 100, 0, new[] { center }).Changed == 144, "material swap changes opaque overlap outside exclusion");
Check(FrameCompareMetrics.Compare(overlap, Overlap(false, true), 100, 100, 0, new[] { center }).Changed == 144, "hide front reveals red outside exclusion");
Check(FrameCompareMetrics.Compare(overlap, Overlap(false, false), 100, 100, 0, new[] { center }).Changed == 0, "restored overlap negative zero");
Console.WriteLine($"FrameCompare controls: {n - controlStart} checks passed (analytic rule model and verdict; no live rendering evidence)");
// Decode instruction boundaries: never search raw byte substrings for tokens.
IEnumerable<(OpCode Op, int Token)> Tokens(byte[] il)
{
    var codes = typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode))
        .Select(f => (OpCode)f.GetValue(null)).ToDictionary(c => c.Value);
    for (int p = 0; p < il.Length;)
    {
        short code = il[p++]; if (code == 0xfe) code = (short)(0xfe00 | il[p++]);
        var op = codes[code]; int size = op.OperandType switch {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, p),
            _ => 4 };
        if (op.OperandType is OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok)
            yield return (op, BitConverter.ToInt32(il, p));
        p += size;
    }
}
if (args.Length >= 3)
{
    int editionStart = n;
    for (int i = 0; i < 3; i++)
    {
        using var stream = File.OpenRead(args[i]); using var pe = new PEReader(stream); var md = pe.GetMetadataReader();
        var names = md.TypeDefinitions.Select(h => md.GetString(md.GetTypeDefinition(h).Name)).ToHashSet();
        foreach (var name in new[] { "FrameCompare", "FrameCompareMetrics", "FrameCompareState", "FrameCompareControlPolicy" })
            Check(names.Contains(name) == (i != 0), "edition isolation: " + args[i] + " " + name);
        if (i == 0) Check(!md.TypeReferences.Any(h => md.GetString(md.GetTypeReference(h).Name).StartsWith("FrameCompare")), "Player has no framecmp type references");
        else
        {
            var type = md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t => md.GetString(t.Name) == "FrameCompare");
            var freeze = type.GetMethods().Select(md.GetMethodDefinition).Single(m => md.GetString(m.Name) == "FreezePlanets");
            var tokens = Tokens(pe.GetMethodBody(freeze.RelativeVirtualAddress).GetILBytes()).ToArray();
            foreach (string target in new[] { "PlanetRenderer", "PlanetSprite" })
                Check(tokens.Any(t => t.Op == OpCodes.Ldtoken && MetadataTokens.Handle(t.Token).Kind == HandleKind.TypeReference
                    && md.GetString(md.GetTypeReference((TypeReferenceHandle)MetadataTokens.Handle(t.Token)).Name) == target),
                    "production diagnostic pause includes " + target);
            Check(tokens.Count(t => t.Op == OpCodes.Call && MetadataTokens.Handle(t.Token).Kind == HandleKind.MethodDefinition
                && md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)MetadataTokens.Handle(t.Token)).Name) == "FreezePlanetUpdate") == 2,
                "both game callbacks use the production strict hook selector");
            var grab = md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t => md.GetString(t.Name).StartsWith("<Grab>d__", StringComparison.Ordinal));
            var move = grab.GetMethods().Select(md.GetMethodDefinition).Single(m => md.GetString(m.Name) == "MoveNext");
            var calls = Tokens(pe.GetMethodBody(move.RelativeVirtualAddress).GetILBytes())
                .Where(t => t.Op == OpCodes.Call || t.Op == OpCodes.Callvirt).Select(t => {
                    var handle = MetadataTokens.Handle(t.Token);
                    return handle.Kind == HandleKind.MethodDefinition ? md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)handle).Name)
                        : handle.Kind == HandleKind.MemberReference ? md.GetString(md.GetMemberReference((MemberReferenceHandle)handle).Name) : "";
                }).ToArray();
            int request = Array.IndexOf(calls, "Request");
            Check(request > 0 && calls.Take(request).Count(c => c == "State") == 2
                && calls.Skip(request + 1).Count(c => c == "State") == 1,
                "both synchronous submission snapshots precede GPU readback deadline; completion audit follows");
        }
    }
    Console.WriteLine($"FrameCompare edition/pause metadata: {n - editionStart} checks passed; Player absent, both unscaled callbacks included");
}
if (args.Length == 4)
{
    using var stream = File.OpenRead(args[3]); using var pe = new PEReader(stream); var md = pe.GetMetadataReader();
    foreach (string name in new[] { "PlanetRenderer", "PlanetSprite" })
    {
        var type = md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t => md.GetString(t.Name) == name);
        Check(type.GetMethods().Select(md.GetMethodDefinition).Count(m => md.GetString(m.Name) == "LateUpdate"
            && !m.Attributes.HasFlag(System.Reflection.MethodAttributes.Static) && m.GetGenericParameters().Count == 0
            && md.GetBlobBytes(m.Signature).SequenceEqual(new byte[] { 0x20, 0, 1 })) == 1,
            "installed-game unique instance void LateUpdate/0: " + name);
    }
    Console.WriteLine("FrameCompare installed-game pause hooks: 2 unique exact-signature targets passed; no game methods invoked");
}

using System.Text;
using Microsoft.Diagnostics.Symbols;

if (args.Length >= 3 && args[0] == "--find-wait")
{
    using var rdr = new SymbolReader(TextWriter.Null);
    rdr.SecurityCheck = _ => true;
    var m = rdr.OpenNativeSymbolFile(args[1]);
    string RawName(uint rva)
    {
        try
        {
            string s = m.FindNameForRva(rva);
            if (string.IsNullOrEmpty(s)) return "";
            int p = s.IndexOf('(');
            return p > 0 ? s.Substring(0, p) : s;
        }
        catch { return ""; }
    }
    uint waitStart = 0, waitEnd = 0, fwdStart = 0, fwdEnd = 0;
    for (uint rva = 0x1000; rva < 0x2200000; rva += 16)
    {
        string n = RawName(rva);
        if (n.Length == 0) continue;
        if (waitStart == 0 && n.Contains("WaitForLastPresentationAndGetTimestamp"))
        {
            uint s = rva; while (s > 0x1000 && RawName(s - 1) == n) s--;
            uint e = rva; while (e < s + 0x2000 && RawName(e + 1) == n) e++;
            waitStart = s; waitEnd = e + 1;
        }
        if (fwdStart == 0 && n.Contains("TimeUpdateWaitForLastPresentationAndUpdateTimeRegistrator") && n.Contains("Forward"))
        {
            uint s = rva; while (s > 0x1000 && RawName(s - 1) == n) s--;
            uint e = rva; while (e < s + 0x1000 && RawName(e + 1) == n) e++;
            fwdStart = s; fwdEnd = e + 1;
        }
        if (waitStart != 0 && fwdStart != 0) break;
    }
    var outLines = new[] { $"WaitFn=0x{waitStart:X},0x{waitEnd:X}", $"ForwardFn=0x{fwdStart:X},0x{fwdEnd:X}" };
    File.WriteAllLines(args[2], outLines, new UTF8Encoding(false));
    Console.WriteLine(string.Join(" | ", outLines));
    return;
}

var lines = File.ReadAllLines(args[0]); int top = args.Length > 2 ? int.Parse(args[2]) : 40;
var resolved = args.Length > 3 ? new List<string>() : null;
using var reader = new SymbolReader(TextWriter.Null);
reader.SecurityCheck = _ => true;
var mod = reader.OpenNativeSymbolFile(args[1]);
var cache = new Dictionary<uint, string>();
string Name(uint rva)
{
    if (cache.TryGetValue(rva, out var s)) return s;
    string n = null; try { n = mod.FindNameForRva(rva); } catch { }
    if (string.IsNullOrEmpty(n)) n = "U:?" + (rva & 0xFFFF0000).ToString("X");
    int p = n.IndexOf('('); if (p > 0) n = n.Substring(0, p);
    cache[rva] = n; return n;
}
var leaf = new Dictionary<string, int>(); var incl = new Dictionary<string, int>(); var nearM = new Dictionary<string, int>();
int total = 0;
foreach (var line in lines)
{
    if (line.Length == 0) continue; total++;
    var fr = line.Split(';'); var seen = new HashSet<string>(); string firstMgd = null;
    var stack = resolved == null ? null : new List<string>();
    for (int i = 0; i < fr.Length; i++)
    {
        string f = fr[i]; string nm;
        if (f.StartsWith("U:")) nm = "[Unity] " + Name(Convert.ToUInt32(f.Substring(2), 16));
        else if (f.StartsWith("M:")) { nm = f.Substring(2); firstMgd ??= nm; }
        else nm = f.Substring(2);
        stack?.Add(nm);
        if (i == 0) leaf[nm] = leaf.GetValueOrDefault(nm) + 1;
        if (seen.Add(nm)) incl[nm] = incl.GetValueOrDefault(nm) + 1;
    }
    resolved?.Add(string.Join(";", stack!));
    if (fr[0].StartsWith("U:") && firstMgd != null) nearM[firstMgd] = nearM.GetValueOrDefault(firstMgd) + 1;
}
void Print(string title, Dictionary<string, int> d)
{
    Console.WriteLine("== " + title + " (표본 " + total + ")");
    foreach (var kv in d.OrderByDescending(k => k.Value).Take(top)) Console.WriteLine($"{100.0 * kv.Value / total,5:F1}%  {kv.Key}");
}
Print("리프", leaf); Print("스택 어디든(입력에 기록된 프레임만)", incl); Print("리프가 UnityPlayer 일 때 가장 가까운 관리 함수", nearM);
if (resolved != null) File.WriteAllLines(args[3], resolved, new UTF8Encoding(false));

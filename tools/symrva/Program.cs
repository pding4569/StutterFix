// profsamp-frames.txt(표본마다 한 줄, 프레임을 ';' 로 이음: U:<RVA 16진>=UnityPlayer, M:<관리 함수>, X:<모듈+RVA>)의 UnityPlayer RVA 를 PDB 로 함수 이름으로 풀어 집계한다.
// 사용: SymRva <frames.txt> <UnityPlayer.pdb> [상위N=40]
using System.Text;
using Microsoft.Diagnostics.Symbols;

var lines = File.ReadAllLines(args[0]); int top = args.Length > 2 ? int.Parse(args[2]) : 40;
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
    for (int i = 0; i < fr.Length; i++)
    {
        string f = fr[i]; string nm;
        if (f.StartsWith("U:")) nm = "[Unity] " + Name(Convert.ToUInt32(f.Substring(2), 16));
        else if (f.StartsWith("M:")) { nm = f.Substring(2); firstMgd ??= nm; }
        else nm = f.Substring(2);
        if (i == 0) leaf[nm] = leaf.GetValueOrDefault(nm) + 1;
        if (seen.Add(nm)) incl[nm] = incl.GetValueOrDefault(nm) + 1;
    }
    // 리프가 UnityPlayer 일 때 가장 가까운 관리 호출자별 묶음
    if (fr[0].StartsWith("U:") && firstMgd != null) nearM[firstMgd] = nearM.GetValueOrDefault(firstMgd) + 1;
}
void Print(string title, Dictionary<string, int> d)
{
    Console.WriteLine("== " + title + " (표본 " + total + ")");
    foreach (var kv in d.OrderByDescending(k => k.Value).Take(top)) Console.WriteLine($"{100.0 * kv.Value / total,5:F1}%  {kv.Key}");
}
Print("리프", leaf); Print("스택 어디든(깊이 12 한계)", incl); Print("리프가 UnityPlayer 일 때 가장 가까운 관리 함수", nearM);

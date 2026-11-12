using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using StutterFix;
namespace StutterFix
{
    internal static class ProfSamp
    {
        internal static bool Running=>false;
        internal static string ResolveLeaf(ulong a,out bool managed){managed=false;return "fixture:0x"+a.ToString("X");}
        internal static void ClearLeafCache(){}
    }
}
class Program
{
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void Work(int ms){var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<ms)Thread.SpinWait(256);}
    static void Main(string[] args)
    {
        string path=Path.GetFullPath(args[0]);Require(!File.Exists(path),"preserve completed fixture");
        Console.WriteLine(LeafProbe.Command("begin 50 "+path));Work(600);Console.WriteLine(LeafProbe.Command("end"));
        using var document=JsonDocument.Parse(File.ReadAllText(path));var r=document.RootElement;
        Require(r.GetProperty("Samples").GetInt32()>=10,"insufficient samples");
        Require(r.GetProperty("ResumeFailures").GetInt32()==0&&r.GetProperty("ContextFailures").GetInt32()==0&&r.GetProperty("ForeignSuspensions").GetInt32()==0,"native capture failure");
        Require(r.GetProperty("WorkerStopped").GetBoolean()&&r.GetProperty("HandlesClosed").GetBoolean(),"sampler resource lifecycle");
        Require(r.GetProperty("Error").ValueKind==JsonValueKind.Null,"pause budget exceeded");
        Console.WriteLine(LeafProbe.Command("begin 50 "+path+"-cancelled"));Work(120);LeafProbe.Stop();
        foreach(string name in new[]{"target","worker","wake","addresses"}){
            object value=typeof(LeafProbe).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Require(value==null||value is IntPtr pointer&&pointer==IntPtr.Zero,"cancel retained "+name);
        }
        Console.WriteLine("LEAF_CHECK passed=True normal_end=True cancelled=True handles=0 samples="+r.GetProperty("Samples")+" max_pause_upper_bound_ms="+r.GetProperty("MaxPauseUpperBoundMs"));
    }
}

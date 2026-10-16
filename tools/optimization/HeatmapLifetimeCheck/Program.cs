using System;
using StutterFix;
int checks=0;
void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
HitchHeatmap.Enabled=true;HitchHeatmap.Reset();
scrController.instance=new scrController{currFloor=new Floor{seqID=123}};
HitchHeatmap.Record(31);HitchHeatmap.Complete();
Check(HitchHeatmap.Describe().Contains("123"),"first completed song recorded");
HitchHeatmap.Reset();HitchHeatmap.Complete();
Check(!HitchHeatmap.Describe().Contains("123"),"empty second completion must not report first song's floor");
Check(HitchHeatmap.Describe().Contains("포탈"),"empty completion retains portal identity");
HitchHeatmap.Reset();scrController.instance.currFloor.seqID=456;HitchHeatmap.Record(41);
Check(HitchHeatmap.Describe().Contains("456")&&!HitchHeatmap.Describe().Contains("123"),"new nonempty record replaces old summary");
HitchHeatmap.Complete();int logs=Main.Entry.Logger.Count;HitchHeatmap.Complete();
Check(Main.Entry.Logger.Count==logs,"portal callback idempotent");
HitchHeatmap.Record(99);Check(!HitchHeatmap.Describe().Contains("99.00"),"post-portal frames not added");
HitchHeatmap.Reset();Check(HitchHeatmap.Describe().Contains("이전"),"previous summary explicitly labeled during a new empty attempt");
HitchHeatmap.Shutdown();HitchHeatmap.Record(25);
Check(!HitchHeatmap.Describe().Contains("456"),"unload clears prior report");
Console.WriteLine($"Heatmap lifetime conditions {checks}, errors 0; no game performance or visual claim");

public class Floor{public int seqID;}
public class scrController{public static scrController instance;public Floor currFloor;public void OnLandOnPortal(){}}
public class scnGame{public void ResetScene(){}}
namespace StutterFix
{
    internal interface IOptimizationModule{string Key{get;}void Install(HarmonyLib.Harmony harmony);void Uninstall();}
    internal static class Main{internal static Entry Entry=new Entry();}
    internal class Entry{internal Logger Logger=new Logger();}
    internal class Logger{internal int Count;internal void Log(string text){Count++;}}
}
namespace HarmonyLib
{
    public class Harmony{public void Patch(System.Reflection.MethodInfo method,HarmonyMethod prefix=null,HarmonyMethod postfix=null){}}
    public class HarmonyMethod{public HarmonyMethod(Type type,string method){}}
    public static class AccessTools{public static System.Reflection.MethodInfo Method(Type type,string name)=>type.GetMethod(name);}
}

using System;
using System.Text;
using HarmonyLib;
namespace StutterFix
{
    // Rare, already-classified frame events only. No per-decoration probes or new frame timers.
    internal static class HitchHeatmap
    {
        internal static bool Enabled;
        private static readonly HitchHistogram current=new HitchHistogram();
        private static bool completed;
        private static string previous="";
        internal static void Record(double ms)
        {
            if(!Enabled||completed)return;
            int floor=-1;
            try{var c=scrController.instance;if(c!=null&&c.currFloor!=null)floor=c.currFloor.seqID;}catch{}
            try{current.Add(floor,ms);}catch{Enabled=false;}
        }
        internal static string Describe()
        {
            var top=current.Top();
            if(top.Length==0&&current.Missing==0&&current.Overflow==0)return previous.Length>0?previous:"기록 0 · 끊김 없음의 증명 아님";
            var sb=new StringBuilder(completed?"포탈 도착 시점":"진행·중도 종료 기록");
            foreach(var x in top)sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"\n- 타일 {0}: {1}회 / 최악 {2:F2}ms / 합계 {3:F2}ms",x.Floor,x.Count,x.Worst,x.Total);
            sb.AppendFormat("\n타일 미확인 {0} / 용량 초과 {1}",current.Missing,current.Overflow);return sb.ToString();
        }
        public static void Reset()
        {
            if(!Enabled)return;
            try
            {
                if(current.Top().Length>0||current.Missing>0||current.Overflow>0)previous=Describe();
                current.Clear();completed=false;
            }
            catch{Enabled=false;}
        }
        public static void Complete()
        {
            if(!Enabled||completed)return;
            completed=true;
            try{Main.Entry.Logger.Log("[끊김 상위 타일] "+Describe().Replace("\n"," | "));}catch{}
        }
        internal static void Shutdown(){Enabled=false;current.Clear();previous="";completed=false;}
    }
    internal sealed class HitchHeatmapModule:IOptimizationModule
    {
        internal static readonly HitchHeatmapModule Instance=new HitchHeatmapModule();
        public string Key=>"heatmap";
        public void Install(Harmony harmony)
        {
            var reset=AccessTools.Method(typeof(scnGame),"ResetScene");
            var complete=AccessTools.Method(typeof(scrController),"OnLandOnPortal");
            if(reset==null||complete==null)throw new MissingMethodException("Heatmap lifecycle unavailable");
            harmony.Patch(reset,prefix:new HarmonyMethod(typeof(HitchHeatmap),nameof(HitchHeatmap.Reset)));
            harmony.Patch(complete,postfix:new HarmonyMethod(typeof(HitchHeatmap),nameof(HitchHeatmap.Complete)));
        }
        public void Uninstall(){HitchHeatmap.Shutdown();}
    }
}

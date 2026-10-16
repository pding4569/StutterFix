using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace StutterFix
{
    internal static class TagIndex
    {
        internal static bool Enabled;
        internal static long Hits,Compiled,Invalidations;
        private static AccessTools.FieldRef<Dictionary<string,List<scrDecoration>>,int> dictVersion;
        private static AccessTools.FieldRef<List<string>,int> listVersion;
        private static Dictionary<string,List<scrDecoration>> source;
        private static CompiledTagTable<scrDecoration> table;
        private static int version;
        private static ConditionalWeakTable<ffxMoveDecorationsPlus,Plan> plans;
        internal sealed class Plan
        {
            internal List<string> Tags;internal int Version;internal int[] Ids;
        }
        internal static void Configure(bool enabled)
        {
            if(Enabled==enabled)return;
            Forget();Enabled=enabled;
        }
        internal static bool Install(Harmony harmony)
        {
            try
            {
                var method=AccessTools.Method(typeof(scnGame),"FinishCustomLevelLoading",new[]{typeof(int),typeof(bool)});
                if(method==null)return false;
                harmony.Patch(method,postfix:new HarmonyMethod(typeof(TagIndex),nameof(Begin)){priority=Priority.Last});
                return true;
            }
            catch{Forget();return false;}
        }
        private static bool InitVersions()
        {
            if(dictVersion!=null && listVersion!=null)return true;
            try
            {
                var d=typeof(Dictionary<string,List<scrDecoration>>);var l=typeof(List<string>);
                string df=AccessTools.Field(d,"_version")!=null?"_version":"version";
                string lf=AccessTools.Field(l,"_version")!=null?"_version":"version";
                dictVersion=AccessTools.FieldRefAccess<Dictionary<string,List<scrDecoration>>,int>(df);
                listVersion=AccessTools.FieldRefAccess<List<string>,int>(lf);
                // Some runtimes do not version value replacement. Disable the
                // candidate on those runtimes instead of trusting stale rows.
                var probe=new Dictionary<string,List<scrDecoration>>();
                probe.Add("x",new List<scrDecoration>());int v=dictVersion(probe);
                probe["x"]=new List<scrDecoration>();if(v==dictVersion(probe))throw new NotSupportedException("Unversioned dictionary replacement");
                v=dictVersion(probe);probe.Remove("x");if(v==dictVersion(probe))throw new NotSupportedException("Unversioned dictionary removal");
                probe.Add("x",null);v=dictVersion(probe);probe.Clear();if(v==dictVersion(probe))throw new NotSupportedException("Unversioned dictionary clear");
                var lp=new List<string>{"x"};v=listVersion(lp);lp[0]="y";if(v==listVersion(lp))throw new NotSupportedException("Unversioned list replacement");
                return true;
            }
            catch{dictVersion=null;listVersion=null;return false;}
        }
        internal static void Begin()
        {
            Forget();if(!Enabled)return;
            try
            {
                if(!InitVersions()){Main.Entry.Logger.Log("[태그 색인] 변경 감지 불가: 기존 조회 사용");return;}
                var mgr=scrDecorationManager.instance;if(mgr==null)return;
                var src=mgr.taggedDecorations;if(src==null)return;
                int v=dictVersion(src);var built=new CompiledTagTable<scrDecoration>(src);
                if(v!=dictVersion(src))return;
                plans=new ConditionalWeakTable<ffxMoveDecorationsPlus,Plan>();source=src;version=v;table=built;
                int prepared=0;
                foreach(var fx in UnityEngine.Resources.FindObjectsOfTypeAll<ffxMoveDecorationsPlus>())
                {
                    if(fx==null||!ReferenceEquals(FastMove.ManagerForIndex(fx),mgr))continue;
                    var tags=FastMove.TagsForIndex(fx);if(tags==null)continue;
                    try
                    {
                        plans.Add(fx,new Plan{Tags=tags,Version=listVersion(tags),Ids=built.Compile(tags)});
                        Compiled++;prepared++;
                    }
                    catch{ /* A malformed effect remains on the original path. */ }
                }
                if(v!=dictVersion(src)){Forget();return;}
                Main.Entry.Logger.Log("[태그 색인] 준비 "+built.Rows.Length+"개, 효과 "+prepared+"개");
            }
            catch{Forget();}
        }
        internal static bool TryPlan(ffxMoveDecorationsPlus fx,Dictionary<string,List<scrDecoration>> dict,List<string> tags,out Plan plan)
        {
            plan=null;if(!Enabled||table==null||tags==null)return false;
            try
            {
                if(!ReferenceEquals(source,dict)||dictVersion(dict)!=version)
                {Invalidations++;Forget();return false;}
                int v=listVersion(tags);Plan p;
                if(!plans.TryGetValue(fx,out p))
                {
                    p=new Plan{Tags=tags,Version=v,Ids=table.Compile(tags)};
                    plans.Add(fx,p);Compiled++;
                }
                else if(!ReferenceEquals(p.Tags,tags)||p.Version!=v)
                {
                    p.Tags=tags;p.Version=v;p.Ids=table.Compile(tags);Compiled++;
                }
                plan=p;Hits++;return true;
            }
            catch{Forget();return false;}
        }
        internal static bool Get(Plan plan,int tag,out List<scrDecoration> row)
        {
            int id=plan.Ids[tag];row=id<0?null:table.Rows[id];return id>=0;
        }
        internal static void Forget()
        {
            source=null;table=null;plans=null;
        }
        internal static void Shutdown(){Enabled=false;Forget();}
    }
}

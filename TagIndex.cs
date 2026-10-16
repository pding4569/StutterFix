using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace StutterFix
{
    internal static class TagIndex
    {
        internal static bool Enabled;
        internal static bool NativePlans;
        internal static long Hits,Compiled,Invalidations;
        private static AccessTools.FieldRef<Dictionary<string,List<scrDecoration>>,int> dictVersion;
        private static AccessTools.FieldRef<List<string>,int> listVersion;
        private static Dictionary<string,List<scrDecoration>> source;
        private static CompiledTagTable<scrDecoration> table;
        private static int version;
        private static ConditionalWeakTable<ffxMoveDecorationsPlus,Plan> plans;
        private static PrimitiveIdStore primitive;
        internal static int PrimitiveUsed=>primitive?.Used??0;
        internal static int PrimitiveCapacity=>primitive?.Capacity??0;
        internal sealed class Plan
        {
            internal List<string> Tags;internal int Version;internal int[] Ids;
            internal PrimitiveIdStore.Slice NativeIds;
        }
        internal static void Configure(bool enabled,bool nativePlans=false)
        {
            if(Enabled==enabled&&NativePlans==nativePlans)return;
            Forget();Enabled=enabled;NativePlans=nativePlans;
        }
        internal static bool Install(Harmony harmony)
        {
            try
            {
                var method=AccessTools.Method(typeof(scnGame),"FinishCustomLevelLoading",new[]{typeof(int),typeof(bool)});
                if(method==null)return false;
                harmony.Patch(method,postfix:new HarmonyMethod(typeof(TagIndex),nameof(Begin)){priority=Priority.Last});
                // Keep the new primitive store and existing object table scoped
                // to this map even when another map is loaded before playback.
                foreach(var candidate in typeof(scnGame).GetMethods(AccessTools.all))
                    if(candidate.DeclaringType==typeof(scnGame)&&candidate.Name=="LoadLevel"&&!candidate.IsAbstract)
                        harmony.Patch(candidate,prefix:new HarmonyMethod(typeof(TagIndex),nameof(Forget)));
                var quit=AccessTools.Method(typeof(scrController),"QuitToMainMenu");
                if(quit!=null)harmony.Patch(quit,prefix:new HarmonyMethod(typeof(TagIndex),nameof(Forget)));
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
                var effects=UnityEngine.Resources.FindObjectsOfTypeAll<ffxMoveDecorationsPlus>();
                if(NativePlans)
                {
                    long count=0;
                    foreach(var fx in effects)
                    {
                        if(fx==null||!ReferenceEquals(FastMove.ManagerForIndex(fx),mgr))continue;
                        var tags=FastMove.TagsForIndex(fx);if(tags!=null)count+=tags.Count;
                        if(count>=2*1024*1024)break;
                    }
                    try{primitive=new PrimitiveIdStore((int)Math.Max(1L,Math.Min(2*1024*1024,count)));}catch{primitive=null;}
                }
                int prepared=0;
                foreach(var fx in effects)
                {
                    if(fx==null||!ReferenceEquals(FastMove.ManagerForIndex(fx),mgr))continue;
                    var tags=FastMove.TagsForIndex(fx);if(tags==null)continue;
                    try
                    {
                        plans.Add(fx,BuildPlan(tags));
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
                    p=BuildPlan(tags);
                    plans.Add(fx,p);Compiled++;
                }
                else if(!ReferenceEquals(p.Tags,tags)||p.Version!=v)
                {
                    p=BuildPlan(tags);plans.Remove(fx);plans.Add(fx,p);Compiled++;
                }
                plan=p;Hits++;return true;
            }
            catch{Forget();return false;}
        }
        private static Plan BuildPlan(List<string> tags)
        {
            var p=new Plan{Tags=tags,Version=listVersion(tags)};
            if(!table.CompileNative(tags,primitive,out p.NativeIds))p.Ids=table.Compile(tags);
            return p;
        }
        internal static bool Get(Plan plan,int tag,Dictionary<string,List<scrDecoration>> original,out List<scrDecoration> row)
        {
            int id;
            if(plan.Ids!=null)id=plan.Ids[tag];
            else if(!plan.NativeIds.TryGet(tag,out id))return original.TryGetValue(plan.Tags[tag],out row);
            row=id<0?null:table.Rows[id];return id>=0;
        }
        internal static void Forget()
        {
            source=null;table=null;plans=null;
            var owned=primitive;primitive=null;
            try{owned?.Dispose();}catch{ /* Slices keep the owner alive; allocator finalization remains safe. */ }
        }
        internal static void Shutdown(){Enabled=NativePlans=false;Forget();}
#if DEV || AUTOTEST
        internal static string VerifyPrimitivePlans()
        {
            if(!Enabled||table==null||source==null||plans==null)return "available=0";
            int effects=0,native=0,values=0,mismatch=0,skipped=0;
            foreach(var fx in UnityEngine.Resources.FindObjectsOfTypeAll<ffxMoveDecorationsPlus>())
            {
                if(fx==null)continue;Plan p;
                if(!plans.TryGetValue(fx,out p)){skipped++;continue;}effects++;if(p.Ids==null)native++;
                if(p.Tags==null||p.Version!=listVersion(p.Tags)||dictVersion(source)!=version){mismatch++;continue;}
                for(int i=0;i<p.Tags.Count;i++)
                {
                    List<scrDecoration> expected,actual;bool a=source.TryGetValue(p.Tags[i],out expected),b=Get(p,i,source,out actual);
                    values++;if(a!=b||!ReferenceEquals(expected,actual))mismatch++;
                }
            }
            return "available=1 effects="+effects+" native_plans="+native+" values="+values+" mismatch="+mismatch+" skipped_not_current="+skipped+" primitive_ints="+PrimitiveUsed+" objects_modified=0";
        }
        internal static string PrimitiveLifetimeFixture()
        {
            var owned=primitive;
            if(owned==null)return "available=0";
            PrimitiveIdStore.Slice sample=default;bool found=false;
            foreach(var fx in UnityEngine.Resources.FindObjectsOfTypeAll<ffxMoveDecorationsPlus>())
            {
                Plan p;if(fx!=null&&plans.TryGetValue(fx,out p)&&p.Ids==null&&p.NativeIds.Count>0)
                {sample=p.NativeIds;found=true;break;}
            }
            bool before=found&&sample.TryGet(0,out _);Forget();
            bool after=sample.TryGet(0,out _),released=!owned.Active;
            return "available=1 prior_slice_readable="+before+" stale_slice_readable="+after+" owned_released="+released+" used_ints="+PrimitiveUsed+" game_objects_modified=0";
        }
#endif
    }
}

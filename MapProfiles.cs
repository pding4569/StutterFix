using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;
using ADOFAI;
using UnityEngine.SceneManagement;

namespace StutterFix
{
    internal static class MapProfiles
    {
        internal static bool Enabled;
        internal static string Status="",CurrentKey="";
#if DEV || AUTOTEST
        internal static int LoadCalls,LoadedCalls,FinishCalls,KeyMisses;
        internal static string LastArgument="";
#endif
        private sealed class KeyData{internal string Hash;}
        internal sealed class LoadState{internal string Path;internal long Length,Ticks;}
        private static ConditionalWeakTable<LevelData,KeyData> keys;
        private static Dictionary<string,object> baseline;
        internal static void Configure(bool enabled)
        {
            if(Enabled==enabled)return;
            if(!enabled)ReleaseLayer();Enabled=enabled;
        }
        internal static void Install(Harmony harmony)
        {
            var load=AccessTools.Method(typeof(LevelData),"LoadLevel",new[]{typeof(string),typeof(ADOFAI.LoadResult).MakeByRefType()});
            var finish=AccessTools.Method(typeof(scnGame),"LoadLevel",new[]{typeof(string),typeof(ADOFAI.LoadResult).MakeByRefType()});
            if(load==null||finish==null||load.IsStatic||load.ReturnType!=typeof(bool))throw new MissingMethodException("Level profile context unavailable");
            keys=new ConditionalWeakTable<LevelData,KeyData>();
            harmony.Patch(load,prefix:new HarmonyMethod(typeof(MapProfiles),nameof(LoadPrefix)),postfix:new HarmonyMethod(typeof(MapProfiles),nameof(Loaded)));
            harmony.Patch(finish,postfix:new HarmonyMethod(typeof(MapProfiles),nameof(Finished)){priority=Priority.Last});
            SceneManager.activeSceneChanged+=SceneChanged;
        }
        public static void LoadPrefix(LevelData __instance,object[] __args,out LoadState __state)
        {
            __state=null;if(!Enabled)return;
#if DEV || AUTOTEST
            LoadCalls++;LastArgument=__args.Length>0?Convert.ToString(__args[0]):"empty";
#endif
            try
            {
                keys.Remove(__instance);
                foreach(var arg in __args)if(arg is string path&&path.EndsWith(".adofai",StringComparison.OrdinalIgnoreCase)&&File.Exists(path))
                {
                    var file=new FileInfo(Path.GetFullPath(path));
                    __state=new LoadState{Path=file.FullName,Length=file.Length,Ticks=file.LastWriteTimeUtc.Ticks};break;
                }
            }
            catch{Status="맵 식별 확인 안 됨";}
        }
        public static void Loaded(LevelData __instance,bool __result,LoadState __state)
        {
#if DEV || AUTOTEST
            LoadedCalls++;
#endif
            if(!Enabled||!__result||__state==null)return;
            try
            {
                var file=new FileInfo(__state.Path);
                if(file.Length!=__state.Length||file.LastWriteTimeUtc.Ticks!=__state.Ticks)return;
                string hash;using(var input=File.OpenRead(__state.Path))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();
                file.Refresh();if(file.Length!=__state.Length||file.LastWriteTimeUtc.Ticks!=__state.Ticks)return;
                keys.Remove(__instance);keys.Add(__instance,new KeyData{Hash=hash});
            }
            catch{Status="맵 식별 실패: 전역 설정 사용";}
        }
        private static MapProfileStore Store()
        {
            string directory=Main.Config.MapProfilesPath;
            return new MapProfileStore(string.IsNullOrWhiteSpace(directory)?Path.Combine(Main.Entry.Path,"map-profiles"):directory);
        }
        public static void Finished(scnGame __instance,bool __result)
        {
#if DEV || AUTOTEST
            FinishCalls++;
#endif
            if(!Enabled||!__result)return;
            try
            {
                ReleaseLayer();KeyData key;
                if(__instance==null||__instance.levelData==null||keys==null||!keys.TryGetValue(__instance.levelData,out key))
                {
#if DEV || AUTOTEST
                    KeyMisses++;
#endif
                    return;
                }
                baseline=RenderProfileValues.Capture(Main.Config);CurrentKey=key.Hash;
                var values=Store().Read(CurrentKey);if(values!=null)RenderProfileValues.Apply(Main.Config,values);
                Status=values==null?"이 맵: 전역 화면 설정":"이 맵: 저장한 화면 설정";
                Main.ApplyConfig();
            }
            catch{try{ReleaseLayer();Main.ApplyConfig();}catch{}Status="프로필 적용 실패: 전역 설정 사용";}
        }
        internal static void Save(object settings,Action globalSave)
        {
            if(!Enabled||!ReferenceEquals(settings,Main.Config)||CurrentKey.Length==0){globalSave();return;}
            if(!Main.Config.MapProfilesEnabled){ReleaseLayer();globalSave();return;}
            bool saved=false;
            try{saved=Store().Write(CurrentKey,RenderProfileValues.Capture(settings));}catch{}
            if(saved)
            {
                RenderProfileValues.WithBaseline(settings,baseline,globalSave);Status="이 맵의 화면 설정 저장됨";
            }
            else
            {
                baseline=RenderProfileValues.Capture(settings);globalSave();Status="맵별 저장 실패: 전역 설정으로 저장";
            }
        }
        internal static void ReleaseLayer()
        {
            if(baseline!=null&&Main.Config!=null)RenderProfileValues.Apply(Main.Config,baseline);
            baseline=null;CurrentKey="";
        }
        internal static void DiscardLayer(){baseline=null;CurrentKey="";}
        private static void SceneChanged(Scene previous,Scene next)
        {
            if(!Enabled||CurrentKey.Length==0||next.name=="scnGame"||next.name=="scnEditor")return;
            try{ReleaseLayer();Main.ApplyConfig();}catch{Status="전역 설정 복구 확인 안 됨";}
        }
        internal static void Shutdown()
        {
            try{SceneManager.activeSceneChanged-=SceneChanged;}
            finally{try{ReleaseLayer();}finally{Enabled=false;keys=null;}}
        }
    }
    internal sealed class MapProfilesModule:IOptimizationModule
    {
        internal static readonly MapProfilesModule Instance=new MapProfilesModule();
        public string Key=>"profiles";
        public void Install(Harmony harmony){MapProfiles.Install(harmony);}
        public void Uninstall(){MapProfiles.Shutdown();}
    }
}

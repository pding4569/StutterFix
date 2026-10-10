using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityModManagerNet;

// Temporary, separate UMM mod: exercise the unmodified release ZIP, then remove.
// No performance claims: this helper controls playback and observes completion.
public static class Main
{
    static UnityModManager.ModEntry entry;
    static Harmony harmony;
    static string map;
    static int phase;
    static float since, started;
    static bool pending, won, oldAuto, autoChanged;
    public static bool Load(UnityModManager.ModEntry e)
    {
        entry=e; map=File.ReadAllText(Path.Combine(e.Path,"map.txt")).Trim();
        if(!File.Exists(map)) throw new FileNotFoundException("Release map missing");
        harmony=new Harmony("StutterFix.ReleaseCheck");
        harmony.Patch(AccessTools.Method(typeof(scrPlayerManager),"AnyValidInputWasTriggered"),
            prefix:new HarmonyMethod(typeof(Main),nameof(Press)));
        harmony.Patch(AccessTools.Method(typeof(scrController),"OnLandOnPortal"),
            postfix:new HarmonyMethod(typeof(Main),nameof(Finished)));
        e.OnUpdate=Update; e.OnUnload=Unload;
        started=Time.realtimeSinceStartup;
        e.Logger.Log("Release ZIP unchanged; separate playback helper enabled");
        return true;
    }
    public static bool Press(ref bool __result)
    {
        if(!pending)return true;
        pending=false; __result=true; return false;
    }
    public static void Finished()
    {
        if(phase!=3)return;
        won=true; since=Time.realtimeSinceStartup; phase=4;
        entry.Logger.Log("COMPLETE OnLandOnPortal");
    }
    static void Update(UnityModManager.ModEntry e,float dt)
    {
        try
        {
            float now=Time.realtimeSinceStartup;
            if(now-started>600)throw new TimeoutException("Release playback incomplete");
            if(phase==0)
            {
                string scene=SceneManager.GetActiveScene().name;
                if(now<5||ADOBase.controller==null||scene=="scnSplash"||scene=="scnLoading"||scene=="scnIntro")return;
                phase=1; ADOBase.controller.LoadCustomLevel(map); e.Logger.Log("OPEN requested");
            }
            else if(phase==1)
            {
                if(SceneManager.GetActiveScene().name!="scnGame"||ADOBase.customLevel==null||ADOBase.customLevel.isLoading)return;
                phase=2; since=now;
            }
            else if(phase==2&&now-since>2)
            {
                oldAuto=RDC.auto; autoChanged=true; RDC.auto=true;
                pending=true; phase=3; e.Logger.Log("PLAY requested");
            }
            else if(phase==4&&now-since>8)
            {
                Cleanup(); phase=5; e.Logger.Log("QUIT complete="+won); Application.Quit();
            }
        }
        catch(Exception ex)
        {
            e.Logger.Log("FAILED "+ex); Cleanup(); phase=5; e.OnUpdate=null; Application.Quit();
        }
    }
    static void Cleanup()
    {
        pending=false;
        if(autoChanged){RDC.auto=oldAuto;autoChanged=false;}
        harmony?.UnpatchAll("StutterFix.ReleaseCheck");
    }
    static bool Unload(UnityModManager.ModEntry e){Cleanup();e.OnUpdate=null;return true;}
}

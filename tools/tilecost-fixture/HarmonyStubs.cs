#if DEV || AUTOTEST
// Accounting-only fixture: actual game's older Harmony/MonoMod cannot initialize on .NET 8.
// This substitutes patch registration, NOT the TileCost accounting callbacks.
using System;
using System.Reflection;
namespace HarmonyLib
{
    public static class AccessTools { public const BindingFlags all=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance; }
    public static class Priority { public const int First=800,Last=0; }
    public class HarmonyMethod { public int priority; public HarmonyMethod(Type t,string n) { } }
    public class Harmony
    {
        public string Id; public static int Registered,Unpatched;
        public Harmony(string id) { Id=id; }
        public void Patch(MethodInfo m,HarmonyMethod prefix,HarmonyMethod finalizer) { Registered++; }
        public void UnpatchAll(string id) { Unpatched++; }
    }
}
#endif

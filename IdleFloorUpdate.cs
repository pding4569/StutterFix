using System;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Research default off. Original Update only writes for animated colors/icons
    // after Start; read the current fields on every call so runtime transitions
    // cannot be hidden by a stale admission cache.
    internal static class IdleFloorUpdate
    {
        internal static bool Enabled, Verify;
        static long skipped, checkedCount, mismatch;
        static readonly AccessTools.FieldRef<scrFloor,bool> started =
            AccessTools.FieldRefAccess<scrFloor,bool>("didRunStart");
        internal static void Install(Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(scrFloor),"Update"),
                prefix:new HarmonyMethod(typeof(IdleFloorUpdate),nameof(Prefix)),
                postfix:new HarmonyMethod(typeof(IdleFloorUpdate),nameof(Postfix)));
        }
        static bool Idle(scrFloor f)
        {
            if(!started(f) || (f.specialColorType!=TrackColorType.Single && f.specialColorType!=TrackColorType.Stripes))return false;
            var icon=f.floorIcon;
            return icon!=FloorIcon.AnimatedRabbit && icon!=FloorIcon.AnimatedSnail
                && icon!=FloorIcon.AnimatedDoubleRabbit && icon!=FloorIcon.AnimatedDoubleSnail;
        }
        internal struct Sample { internal bool Check; internal Color Color; }
        static bool Prefix(scrFloor __instance,out Sample __state)
        {
            __state=default;
            if(!Enabled || !Idle(__instance))return true;
            if(Verify) {__state.Check=true;__state.Color=__instance.floorRenderer.color;return true;}
#if AUTOTEST
            skipped++;
#endif
            return false;
        }
        static void Postfix(scrFloor __instance,Sample __state)
        {
            if(!__state.Check)return;
            checkedCount++;
            var actual=__instance.floorRenderer.color;
            for(int c=0;c<4;c++)if(BitConverter.SingleToInt32Bits(actual[c])!=BitConverter.SingleToInt32Bits(__state.Color[c]))
            {mismatch++;break;}
        }
        internal static string Command(string arg)
        {
            if(arg=="on" || arg=="off" || arg=="check")
            {Enabled=arg!="off";Verify=arg=="check";skipped=checkedCount=mismatch=0;}
            else if(arg!="state" && arg!="verify")throw new ArgumentException("idlefloor on|off|check|state|verify");
            if(arg=="verify" && (!Verify || checkedCount==0 || mismatch!=0))throw new InvalidOperationException("idlefloor oracle absent or mismatch");
            return "enabled="+Enabled+" verify="+Verify+" skipped="+skipped+" checked="+checkedCount+" mismatch="+mismatch;
        }
    }
}

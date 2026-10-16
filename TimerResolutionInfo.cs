using System;
using System.Runtime.InteropServices;
namespace StutterFix
{
    internal static class TimerResolutionInfo
    {
        [DllImport("ntdll.dll",ExactSpelling=true)]private static extern int NtQueryTimerResolution(out uint maximum,out uint minimum,out uint current);
        internal static string Read()
        {
            try
            {
                uint maximum,minimum,current;int status=NtQueryTimerResolution(out maximum,out minimum,out current);
                return status>=0?"coarsest_100ns="+maximum+" finest_100ns="+minimum+" current_100ns="+current+" requests_changed=0":"unsupported status="+status;
            }
            catch{return "확인 안 됨 · requests_changed=0";}
        }
    }
}

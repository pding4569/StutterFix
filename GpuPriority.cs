#if DEV || AUTOTEST
using System;
using System.Runtime.InteropServices;

namespace StutterFix
{
    // 진단: 이 프로세스의 GPU 스케줄 우선순위(WDDM). 다른 프로그램(배경 화면, 오버레이 등)이 GPU 를 같이 쓸 때
    // 게임 명령이 먼저 돌게 한다. 0 Idle, 1 BelowNormal, 2 Normal(기본), 3 AboveNormal, 4 High, 5 Realtime(권한 필요).
    internal static class GpuPriority
    {
        [DllImport("gdi32.dll")] static extern int D3DKMTSetProcessSchedulingPriorityClass(IntPtr process, int priority);
        [DllImport("gdi32.dll")] static extern int D3DKMTGetProcessSchedulingPriorityClass(IntPtr process, out int priority);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        static int original = -1;
        internal static string Command(string arg)
        {
            int before; int g = D3DKMTGetProcessSchedulingPriorityClass(GetCurrentProcess(), out before);
            if (arg.Trim() == "state") return "now=" + before + " status=0x" + g.ToString("X");
            if (original < 0 && g == 0) original = before;
            int want = arg.Trim() == "off" ? (original >= 0 ? original : 2) : int.Parse(arg.Trim());
            int s = D3DKMTSetProcessSchedulingPriorityClass(GetCurrentProcess(), want);
            int after; D3DKMTGetProcessSchedulingPriorityClass(GetCurrentProcess(), out after);
            return "before=" + before + " want=" + want + " status=0x" + s.ToString("X") + " after=" + after;
        }
        internal static void Restore() { if (original >= 0) { D3DKMTSetProcessSchedulingPriorityClass(GetCurrentProcess(), original); original = -1; } }
    }
}
#endif

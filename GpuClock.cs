using System;
using System.IO;
using System.Runtime.InteropServices;

namespace StutterFix
{
    // NVIDIA 그래픽카드의 지금 클럭·성능 상태를 읽는다(드라이버에 들어 있는 nvml.dll). 다른 그래픽카드나 못 불러오면 조용히 안 한다.
    //
    // 첫 판만 곡 중 화면 대기가 1.7ms 늘고(약 200 FPS) 같은 맵 두 번째 판은 대기가 없다(약 318 FPS). 화면 출력 방식(BitBlt/Flip)과
    // 무관했고, 디스코드 화면 공유를 켜 두면 첫 판에도 대기가 없었다. 화면 공유는 GPU 를 계속 쓰므로 그래픽카드가 절전 클럭으로
    // 내려가지 않는다 - 첫 판에는 그래픽카드가 낮은 클럭에 머물러 화면 넘기기가 늦는지 확인하려고 곡 중 1초마다 적는다.
    internal static unsafe class GpuClock
    {
        private static int state;   // 0 아직, 1 사용 가능, -1 없음
        private static IntPtr dev;
        private static bool reasonsOk = true;
        internal static string Status = "안 불러옴";

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryW(string path);
        [DllImport("nvml", EntryPoint = "nvmlInit_v2")] private static extern int nvmlInit();
        [DllImport("nvml", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")] private static extern int nvmlDeviceGetHandleByIndex(uint index, out IntPtr device);
        [DllImport("nvml")] private static extern int nvmlDeviceGetClockInfo(IntPtr device, int type, out uint clock);
        [DllImport("nvml")] private static extern int nvmlDeviceGetPerformanceState(IntPtr device, out int pstate);
        [DllImport("nvml")] private static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out Utilization u);
        [DllImport("nvml")] private static extern int nvmlDeviceGetCurrentClocksThrottleReasons(IntPtr device, out ulong reasons);
        [StructLayout(LayoutKind.Sequential)] private struct Utilization { public uint Gpu, Memory; }
        private const int ClockGraphics = 0, ClockMem = 2;
        internal const ulong ReasonIdle = 0x1;

        internal struct Sample { public bool Ok; public uint Gr, Mem, Util; public int PState; public ulong Reasons; }

        // 작업 스레드에서만 부른다(처음 한 번 초기화가 수십 ms 걸릴 수 있다)
        internal static Sample Read()
        {
            var s = new Sample();
            if (state == 0) Init();
            if (state != 1) return s;
            try
            {
                if (nvmlDeviceGetClockInfo(dev, ClockGraphics, out s.Gr) != 0) return s;
                nvmlDeviceGetClockInfo(dev, ClockMem, out s.Mem);
                if (nvmlDeviceGetPerformanceState(dev, out s.PState) != 0) s.PState = -1;
                Utilization u; if (nvmlDeviceGetUtilizationRates(dev, out u) == 0) s.Util = u.Gpu;
                if (reasonsOk) { try { ulong r; if (nvmlDeviceGetCurrentClocksThrottleReasons(dev, out r) == 0) s.Reasons = r; } catch { reasonsOk = false; } }   // 드라이버에 따라 없을 수 있다
                s.Ok = true;
            }
            catch { state = -1; }
            return s;
        }

        private static void Init()
        {
            state = -1;
            try
            {
                string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvml.dll");
                if (!File.Exists(p)) { Status = "NVIDIA 아님(nvml.dll 없음)"; return; }
                if (LoadLibraryW(p) == IntPtr.Zero) { Status = "nvml.dll 불러오기 실패"; return; }
                int r = nvmlInit();
                if (r != 0) { Status = "nvml 초기화 실패 (" + r + ")"; return; }
                if (nvmlDeviceGetHandleByIndex(0, out dev) != 0) { Status = "그래픽카드 못 찾음"; return; }
                state = 1; Status = "사용 중";
            }
            catch (Exception ex) { Status = "실패: " + ex.Message; }
        }
    }
}

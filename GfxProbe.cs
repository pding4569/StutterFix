using System;
using System.Runtime.InteropServices;

namespace StutterFix
{
    // (개발자용) 유니티 D3D11 장치의 프레임 대기 상태를 직접 읽는다 - 판마다 갈리는 "화면 대기 1.7ms 상태" 원인 확인용.
    //
    // UnityPlayer.dll(6000.3.10f1) 을 공개 심볼로 역어셈블해서 확인한 것 (C:\SFBundle\UnityDis):
    //   TimeUpdate.WaitForLastPresentation -> GfxDeviceD3D11::WaitForLastPresentationAndGetTimestamp:
    //     전역 설정(S)+0x360 이 켜져 있으면 스왑체인 대기 객체(DXGI frame latency waitable, S+0x350)를,
    //     아니면 장치+0x6750 의 세마포어를 기다린다(메인 스레드, 프레임마다 한 번).
    //   그래픽 스레드의 GfxDeviceD3D11::PushEventQuery (화면 넘긴 뒤): 프레임마다 GPU 이벤트 쿼리를 목록(+0x6740, 개수 +0x6748)에 넣고,
    //     개수가 최대 대기 프레임 수(+0x1EAC) 이상이면 가장 오래된 쿼리가 GPU 에서 끝날 때까지 돌며 기다렸다가 빼고,
    //     빚(+0x6758)이 있으면 빚을 하나 갚고 없으면 세마포어를 하나 올린다.
    //   최대 대기 프레임 수가 2면 메인 스레드는 한 프레임 전 것(이미 끝남)을, 1이면 방금 넘긴 프레임의 GPU 완료를 기다린다(느린 판 1.7ms 와 맞음).
    // 주소는 모두 ReadProcessMemory 로 읽는다(틀린 주소여도 실패만 하고 게임은 안 튕긴다). 버전이 다르면 가상 함수표 확인에서 멈춘다.
    internal static unsafe class GfxProbe
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string name);
        [DllImport("kernel32.dll")] private static extern IntPtr TlsGetValue(uint index);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool ReadProcessMemory(IntPtr proc, IntPtr addr, byte* buf, IntPtr size, out IntPtr read);
        [DllImport("ntdll.dll")] private static extern int NtQuerySemaphore(IntPtr h, int cls, int* info, int len, out int ret);

        private const long RvaSettings = 0x212F5A0, RvaTlsIndex = 0x2084470, RvaClientVtbl = 0x1DA8D00, RvaD3D11Vtbl1 = 0x1D66F28, RvaD3D11Vtbl2 = 0x1D66F70;

        private static bool Read(long addr, void* dst, int size)
        {
            IntPtr got;
            return addr != 0 && ReadProcessMemory(GetCurrentProcess(), (IntPtr)addr, (byte*)dst, (IntPtr)size, out got) && (long)got == size;
        }
        private static long Ptr(long addr) { long v = 0; return Read(addr, &v, 8) ? v : 0; }
        private static int I32(long addr) { int v = -1; return Read(addr, &v, 4) ? v : -1; }
        private static byte U8(long addr) { byte v = 0xFF; return Read(addr, &v, 1) ? v : (byte)0xFF; }

        private static long basePtr, device, settings;
        private static string status = "";

        // 메인 스레드에서 부른다(장치 포인터가 스레드 로컬에 있다)
        private static bool Resolve()
        {
            if (device != 0) return true;
            if (status.Length > 0) return false;
            basePtr = (long)GetModuleHandleW("UnityPlayer.dll");
            if (basePtr == 0) { status = "UnityPlayer.dll 없음"; return false; }
            uint tls = (uint)I32(basePtr + RvaTlsIndex);
            long client = (long)TlsGetValue(tls);
            if (Ptr(client) != basePtr + RvaClientVtbl) { status = "GfxDeviceClient 가상 함수표가 다름(유니티 버전이 다름?)"; return false; }
            long dev = Ptr(client + 0x2688);
            long vt = Ptr(dev);
            if (vt != basePtr + RvaD3D11Vtbl1 && vt != basePtr + RvaD3D11Vtbl2) { status = "GfxDeviceD3D11 가상 함수표가 다름"; return false; }
            device = dev;
            settings = Ptr(basePtr + RvaSettings);
            return true;
        }

        private static string Sem(long handle)
        {
            if (handle == 0) return "없음";
            int* info = stackalloc int[2]; int ret;
            int st = NtQuerySemaphore((IntPtr)handle, 0, info, 8, out ret);
            return st == 0 ? info[0] + "/" + info[1] : "조회 실패 0x" + st.ToString("X");
        }

        // ── 스왑체인 대기 객체(DXGI frame latency waitable) 여유분 ──
        // 2026-09-27: 느린 판과 빠른 판 모두 "스왑체인 대기 객체" 방식, 최대 대기 프레임 2 로 유니티 쪽 값은 같았다. 대기 객체는 세마포어이고
        // (NtQuerySemaphore 가 형식 오류가 아니라 권한 오류), 유니티가 받은 핸들에는 조회 권한이 없다. 조회·올리기 권한으로 복제해 둔다.
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool DuplicateHandle(IntPtr srcProc, IntPtr src, IntPtr dstProc, out IntPtr dst, uint access, bool inherit, uint options);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReleaseSemaphore(IntPtr h, int count, out int previous);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
        private const uint SEMAPHORE_QUERY_STATE = 0x0001, SEMAPHORE_MODIFY_STATE = 0x0002, SYNCHRONIZE = 0x00100000;
        private static IntPtr latencyDup; private static long latencyOrig;

        private static IntPtr LatencyHandle()
        {
            if (!Resolve()) return IntPtr.Zero;
            long h = Ptr(settings + 0x350);
            if (h == 0) return IntPtr.Zero;
            if (h == latencyOrig && latencyDup != IntPtr.Zero) return latencyDup;
            if (latencyDup != IntPtr.Zero) { CloseHandle(latencyDup); latencyDup = IntPtr.Zero; }   // 스왑체인이 새로 만들어졌다
            IntPtr d;
            if (!DuplicateHandle(GetCurrentProcess(), (IntPtr)h, GetCurrentProcess(), out d, SEMAPHORE_QUERY_STATE | SEMAPHORE_MODIFY_STATE | SYNCHRONIZE, false, 0))
            { LastError = "복제 실패 " + Marshal.GetLastWin32Error(); return IntPtr.Zero; }
            latencyDup = d; latencyOrig = h;
            return d;
        }
        internal static string LastError = "";

        internal static string LatencyCount() { var h = LatencyHandle(); return h == IntPtr.Zero ? "못 봄(" + LastError + ")" : Sem((long)h); }

        // 여유분 하나 돌려주기. 돌려주기 전 값을 준다(-1 이면 실패)
        internal static int GiveBack()
        {
            var h = LatencyHandle();
            if (h == IntPtr.Zero) return -1;
            int prev;
            if (!ReleaseSemaphore(h, 1, out prev)) { LastError = "올리기 실패 " + Marshal.GetLastWin32Error(); return -1; }
            return prev;
        }
        internal static void Close() { if (latencyDup != IntPtr.Zero) { CloseHandle(latencyDup); latencyDup = IntPtr.Zero; } }

        // ── DXGI 스왑체인의 최대 대기 프레임 수 (IDXGISwapChain2, 전역 설정 S+0x328) ──
        // 유니티 SetMaximumFrameLatencyForWaitableObject 가 이 포인터의 가상 함수 0x100(GetMaximumFrameLatency), 0xF8(SetMaximumFrameLatency),
        // 0x108(GetFrameLatencyWaitableObject) 을 부른다(IDXGISwapChain2 의 32, 31, 33번째 함수와 같음).
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetLatencyFn(IntPtr self, out uint v);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetLatencyFn(IntPtr self, uint v);
        private static long SwapChain() { return Resolve() ? Ptr(settings + 0x328) : 0; }
        private static long VtblFn(long obj, int offset) { long vt = Ptr(obj); return vt == 0 ? 0 : Ptr(vt + offset); }

        internal static int DxgiMaxLatency()
        {
            try
            {
                long sc = SwapChain(); long fn = VtblFn(sc, 0x100);
                if (sc == 0 || fn == 0) return -1;
                uint v; int hr = Marshal.GetDelegateForFunctionPointer<GetLatencyFn>((IntPtr)fn)((IntPtr)sc, out v);
                return hr >= 0 ? (int)v : -2;
            }
            catch { return -3; }
        }

        internal static int SetDxgiMaxLatency(uint v)
        {
            try
            {
                long sc = SwapChain(); long fn = VtblFn(sc, 0xF8);
                if (sc == 0 || fn == 0) return -1;
                return Marshal.GetDelegateForFunctionPointer<SetLatencyFn>((IntPtr)fn)((IntPtr)sc, v);
            }
            catch { return -3; }
        }

        internal static string Snapshot()
        {
            try
            {
                if (!Resolve()) return "[프레임 대기 상태] 못 읽음: " + status;
                byte waitable = U8(settings + 0x360);
                return string.Format("[프레임 대기 상태] 방식 {0} | 최대 대기 프레임 {1} (DXGI " + DxgiMaxLatency() + ") | 쿼리 목록 {2}개 | 빚 {3} | 세마포어 {4} | 스왑체인 대기 객체 {5} | 프레임 기다림 {6}/{7}",
                    waitable == 1 ? "스왑체인 대기 객체" : waitable == 0 ? "세마포어" : "?(" + waitable + ")",
                    I32(device + 0x1EAC), Ptr(device + 0x6748), I32(device + 0x6758), Sem(Ptr(device + 0x6750)), LatencyCount(),
                    Ptr(device + 0x5E68), Ptr(device + 0x5E70));
            }
            catch (Exception ex) { return "[프레임 대기 상태] 실패: " + ex.Message; }
        }
    }
}

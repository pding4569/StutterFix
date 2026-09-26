using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace StutterFix
{
    // sfnative.dll (이 모드가 직접 쓴 C 코드, native/sfnative/sfnative.c): PNG 필터 되돌리기와 DXT 압축을 네이티브로 한다.
    //
    // Arche 이미지 불러오기에서 작업 스레드 시간(합계)의 대부분이 필터 되돌리기 16초와 DXT 압축 34초였다(유니티 Mono 는 C# 을 느리게 돈다).
    // 같은 계산을 C 로 옮겼다. 결과 검증(2026-09-26):
    //   필터 되돌리기: 맵 폴더 PNG 전부의 모든 줄에서 C# 결과와 바이트 비교
    //   DXT: Arche 이미지 251장, 블록 6,280만 개 전부 C# 과 같음, 원본 대비 오차 같음
    // DXT 압축은 ISPC(인텔 SPMD 컴파일러)로 같은 계산을 블록 여러 개씩 동시에 한다(native/sfnative/sfdxt.ispc, CPU 에 따라 SSE2/SSE4.1/AVX2).
    //   2026-09-27: Arche 이미지 251장 블록 6,280만 개가 세 경로 모두 C# 과 전부 같음. 10억 픽셀 11.9초 -> AVX2 3.5초(SSE4.1 5.9, SSE2 8.4).
    // 개발자용은 곡 불러오기 중 일부 호출을 C# 으로도 해서 계속 비교한다(다르면 로그).
    // DLL 을 못 불러오면 Ready 가 false 로 남고 C# 길을 쓴다.
    internal static unsafe class SfNative
    {
        internal static bool Ready;
        internal static bool Enabled = true;
        internal static string Status = "안 불러옴";
        internal static long UnfilterRows, DxtRows, DevChecks, DevMismatch;
        internal static string FirstMismatch = "";

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryW(string path);
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] private static extern int sf_version();
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] private static extern void sf_dxt_init();
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] private static extern int sf_unfilter_to(byte* d, byte* s, byte* p, int n, int bpp, int filter);
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] private static extern int sf_unfilter_inplace(byte* c, byte* p, int n, int bpp, int filter);
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] private static extern void sf_dxt_encode_rows(byte* src, int w, int h, int layout, int dxt5, byte* dst, int by0, int by1);

        // 전체 경로로 먼저 올려 두면 DllImport("sfnative") 가 이미 올라온 것을 쓴다(libdeflate 와 같은 방식).
        internal static void Init(string path)
        {
            if (Ready) return;
            try
            {
                if (!File.Exists(path)) { Status = "DLL 없음"; return; }
                if (LoadLibraryW(path) == IntPtr.Zero) { Status = "DLL 불러오기 실패 (" + Marshal.GetLastWin32Error() + ")"; return; }
                int v = sf_version();
                if (v != 1) { Status = "버전이 다름 (" + v + ")"; return; }
                sf_dxt_init();
                Ready = true; Status = "사용 중";
            }
            catch (Exception ex) { Ready = false; Status = "실패: " + ex.Message; }
        }

        private static bool Use { get { return Ready && Enabled; } }
        private static int dxtCounter, rowCounter;
        [ThreadStatic] private static byte[] devA, devB;

        internal static void EncodeRows(byte* src, int w, int h, int layout, bool dxt5, byte* dst, int by0, int by1)
        {
            if (!Use) { DxtEncoder.EncodeRows(src, w, h, layout, dxt5, dst, by0, by1); return; }
            sf_dxt_encode_rows(src, w, h, layout, dxt5 ? 1 : 0, dst, by0, by1);
            Interlocked.Add(ref DxtRows, by1 - by0);
            // 개발자용: 16번에 한 번은 같은 줄 묶음을 C# 으로도 압축해 비교
            if (Edition.Dev && Interlocked.Increment(ref dxtCounter) % 16 == 0)
            {
                int bw = w / 4, bs = dxt5 ? 16 : 8;
                long off = (long)by0 * bw * bs, len = (long)(by1 - by0) * bw * bs;
                if (len <= 0 || len > 64L << 20) return;
                if (devA == null || devA.Length < len) devA = new byte[len];
                fixed (byte* t = devA)
                {
                    // C# 은 dst 전체 기준 오프셋에 쓰므로 by0 만큼 앞으로 당긴 주소를 준다
                    DxtEncoder.EncodeRows(src, w, h, layout, dxt5, t - off, by0, by1);
                    Check(dst + off, t, len, "DXT " + w + "x" + h + " 줄 " + by0 + "~" + by1);
                }
            }
        }

        // PngDecoder.UnfilterTo 와 같은 약속: d = 결과 줄, s = 원본 줄, p = 결과의 윗줄
        internal static bool UnfilterTo(byte* d, byte* s, byte* p, int n, int bpp, int filter)
        {
            int r = sf_unfilter_to(d, s, p, n, bpp, filter);
            Interlocked.Increment(ref UnfilterRows);
            if (r != 0 && Edition.Dev && Interlocked.Increment(ref rowCounter) % 256 == 0) DevRow(d, s, p, n, bpp, filter);
            return r != 0;
        }

        // PngDecoder.UnfilterPtr 와 같은 약속: c = 이번 줄(제자리), p = 윗줄
        internal static bool UnfilterInPlace(byte* c, byte* p, int n, int bpp, int filter)
        {
            byte[] keep = null;
            bool dev = Edition.Dev && Interlocked.Increment(ref rowCounter) % 256 == 0;
            if (dev) { keep = new byte[n]; Marshal.Copy((IntPtr)c, keep, 0, n); }
            int r = sf_unfilter_inplace(c, p, n, bpp, filter);
            Interlocked.Increment(ref UnfilterRows);
            if (dev && r != 0) fixed (byte* k = keep) DevRow(c, k, p, n, bpp, filter);
            return r != 0;
        }

        internal static bool Available { get { return Use; } }

        // 개발자용: 원본 줄 s 를 C# 규칙으로 되돌려 네이티브 결과 d 와 비교
        private static void DevRow(byte* d, byte* s, byte* p, int n, int bpp, int filter)
        {
            if (devB == null || devB.Length < n) devB = new byte[Math.Max(n, 4096)];
            fixed (byte* t = devB)
            {
                Buffer.MemoryCopy(s, t, n, n);
                PngDecoder.ManagedUnfilter(t, p, n, bpp, filter);
                Check(d, t, n, "필터 " + filter + " bpp " + bpp + " 길이 " + n);
            }
        }

        private static void Check(byte* a, byte* b, long n, string what)
        {
            Interlocked.Increment(ref DevChecks);
            for (long i = 0; i < n; i++)
                if (a[i] != b[i])
                {
                    if (Interlocked.Increment(ref DevMismatch) == 1) FirstMismatch = what + " 위치 " + i;
                    return;
                }
        }

        internal static string Summary()
        {
            if (!Ready) return " | 네이티브(sfnative): " + Status;
            return string.Format(" | 네이티브(sfnative): 필터 되돌린 줄 {0}, DXT 압축 줄 묶음 {1}{2}", UnfilterRows, DxtRows,
                Edition.Dev ? string.Format(", C# 과 대조 {0}번 중 다름 {1}{2}", DevChecks, DevMismatch, DevMismatch > 0 ? " (처음: " + FirstMismatch + ")" : "") : "");
        }
        internal static void ResetStats() { UnfilterRows = DxtRows = DevChecks = DevMismatch = 0; FirstMismatch = ""; }
    }
}

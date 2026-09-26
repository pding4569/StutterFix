using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace StutterFix
{
    // turbojpeg.dll (libjpeg-turbo 3.2.0, BSD 계열 라이선스: native/libjpeg-turbo-LICENSE.md): JPG 장식 이미지를 작업 스레드에서 푼다.
    //
    // 게임은 JPG 도 메인 스레드에서 한 장씩 ImageConversion.LoadImage 로 푼다. PNG 는 이미 작업 스레드에서 미리 풀고 있었지만
    // JPG 는 원래 방식이었다(맵 폴더 JPG 2,068장, 합계 약 77억 픽셀).
    // 결과 검증(2026-09-26): 맵 폴더의 JPG 2,068장 전부 유니티 6000.3.10f1 LoadImage 결과(RGB24, 아래 줄부터)와 바이트 전부 같음.
    //   맞는 설정은 libjpeg-turbo 기본값(정확한 DCT, 부드러운 업샘플)뿐이고 빠른 DCT·빠른 업샘플은 다르다.
    //   (유니티 -nographics 는 최대 텍스처 8192 라 큰 2장은 그래픽을 켠 유니티로 따로 확인)
    // 경고(손상된 파일 등)도 실패로 보고 원래 방식으로 넘긴다. CMYK 처럼 RGB 로 못 바꾸는 것도 실패로 원래 방식.
    // 개발자용은 곡 불러오기 중 일부 JPG 를 유니티로도 풀어 계속 비교한다(ImagePrefetch.SameAsUnity).
    internal static unsafe class TurboJpeg
    {
        internal static bool Ready;
        internal static bool Enabled = true;
        internal static string Status = "안 불러옴";
        internal static long Images, Failures, Ticks;

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryW(string path);
        [DllImport("turbojpeg", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr tj3Init(int initType);
        [DllImport("turbojpeg", CallingConvention = CallingConvention.Cdecl)] private static extern int tj3Set(IntPtr h, int param, int value);
        [DllImport("turbojpeg", CallingConvention = CallingConvention.Cdecl)] private static extern int tj3Get(IntPtr h, int param);
        [DllImport("turbojpeg", CallingConvention = CallingConvention.Cdecl)] private static extern int tj3DecompressHeader(IntPtr h, byte* buf, UIntPtr size);
        [DllImport("turbojpeg", CallingConvention = CallingConvention.Cdecl)] private static extern int tj3Decompress8(IntPtr h, byte* buf, UIntPtr size, byte* dst, int pitch, int pixelFormat);
        [DllImport("turbojpeg", CallingConvention = CallingConvention.Cdecl)] private static extern void tj3Destroy(IntPtr h);
        private const int TJINIT_DECOMPRESS = 1;
        private const int TJPARAM_BOTTOMUP = 1, TJPARAM_JPEGWIDTH = 5, TJPARAM_JPEGHEIGHT = 6, TJPARAM_FASTUPSAMPLE = 9, TJPARAM_FASTDCT = 10;
        private const int TJPF_RGB = 0;

        // 전체 경로로 먼저 올려 두면 DllImport("turbojpeg") 가 이미 올라온 것을 쓴다(libdeflate, sfnative 와 같은 방식).
        internal static void Init(string path)
        {
            if (Ready) return;
            try
            {
                if (!File.Exists(path)) { Status = "DLL 없음"; return; }
                if (LoadLibraryW(path) == IntPtr.Zero) { Status = "DLL 불러오기 실패 (" + Marshal.GetLastWin32Error() + ")"; return; }
                var h = tj3Init(TJINIT_DECOMPRESS);
                if (h == IntPtr.Zero) { Status = "해독기 만들기 실패"; return; }
                tj3Destroy(h);
                Ready = true; Status = "사용 중";
            }
            catch (Exception ex) { Ready = false; Status = "실패: " + ex.Message; }
        }

        internal static bool Available { get { return Ready && Enabled; } }

        internal static bool IsJpg(byte[] b, int len) { return len > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF; }

        // 스레드마다 해독기 하나. 작업 스레드가 끝날 때 FreeThread 로 푼다.
        [ThreadStatic] private static IntPtr handle;

        internal static void FreeThread()
        {
            if (handle == IntPtr.Zero) return;
            try { tj3Destroy(handle); } catch { }
            handle = IntPtr.Zero;
        }

        // 유니티 LoadImage 와 같은 결과: RGB24, 아래 줄부터. 가로·세로가 maxSide 를 넘으면 유니티도 못 올리므로 원래 방식으로 둔다.
        // 성공하면 pixels 는 Marshal.AllocHGlobal 로 잡은 것(가져간 쪽이 푼다).
        internal static bool TryDecode(byte[] data, int len, int maxSide, out int width, out int height, out IntPtr pixels, out long size)
        {
            width = height = 0; pixels = IntPtr.Zero; size = 0;
            if (!Available) return false;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (handle == IntPtr.Zero) handle = tj3Init(TJINIT_DECOMPRESS);
                if (handle == IntPtr.Zero) return Fail();
                fixed (byte* src = data)
                {
                    if (tj3DecompressHeader(handle, src, (UIntPtr)len) != 0) return Fail();
                    int w = tj3Get(handle, TJPARAM_JPEGWIDTH), h = tj3Get(handle, TJPARAM_JPEGHEIGHT);
                    if (w <= 0 || h <= 0 || w > maxSide || h > maxSide) return Fail();
                    long n = (long)w * h * 3;
                    if (n > int.MaxValue) return Fail();   // LoadRawTextureData 는 int 크기
                    tj3Set(handle, TJPARAM_BOTTOMUP, 1);
                    tj3Set(handle, TJPARAM_FASTUPSAMPLE, 0);
                    tj3Set(handle, TJPARAM_FASTDCT, 0);
                    IntPtr p;
                    try { p = Marshal.AllocHGlobal((IntPtr)n); } catch { return Fail(); }
                    // 0 이 아니면 오류이거나 경고(손상된 데이터 등): 둘 다 원래 방식으로
                    if (tj3Decompress8(handle, src, (UIntPtr)len, (byte*)p, w * 3, TJPF_RGB) != 0) { Marshal.FreeHGlobal(p); return Fail(); }
                    width = w; height = h; pixels = p; size = n;
                    Interlocked.Increment(ref Images);
                    return true;
                }
            }
            catch { return Fail(); }
            finally { Interlocked.Add(ref Ticks, Stopwatch.GetTimestamp() - t0); }
        }

        private static bool Fail() { Interlocked.Increment(ref Failures); return false; }

        internal static string Summary()
        {
            if (!Ready) return " | JPG(libjpeg-turbo): " + Status;
            return string.Format(" | JPG(libjpeg-turbo): {0}장 {1:F0}ms, 못 푼 것 {2}장{3}", Images, Ticks * 1000.0 / Stopwatch.Frequency, Failures,
                Edition.Dev ? string.Format(", 유니티와 대조 {0}장 중 다름 {1}", DevChecks, DevMismatch) : "");
        }
        internal static long DevChecks, DevMismatch;
        internal static void ResetStats() { Images = Failures = Ticks = DevChecks = DevMismatch = 0; }
    }
}

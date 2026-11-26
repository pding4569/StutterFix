using System;
using System.Runtime.InteropServices;

namespace StutterFix
{
    internal static class Platform
    {
        public static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static readonly bool IsSupported = IsWindows || IsLinux;
    }

    internal static class NativeLib
    {
        private const int RTLD_NOW = 2;
        private const int RTLD_GLOBAL = 0x100;

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string path);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("libdl.so.2", EntryPoint = "dlopen", CharSet = CharSet.Ansi)]
        private static extern IntPtr DlOpen2(string filename, int flags);

        [DllImport("libdl.so.2", EntryPoint = "dlsym", CharSet = CharSet.Ansi)]
        private static extern IntPtr DlSym2(IntPtr handle, string symbol);

        [DllImport("libdl.so.2", EntryPoint = "dlerror")]
        private static extern IntPtr DlError2();

        [DllImport("libc.so.6", EntryPoint = "dlopen", CharSet = CharSet.Ansi)]
        private static extern IntPtr DlOpenC(string filename, int flags);

        [DllImport("libc.so.6", EntryPoint = "dlsym", CharSet = CharSet.Ansi)]
        private static extern IntPtr DlSymC(IntPtr handle, string symbol);

        [DllImport("libc.so.6", EntryPoint = "dlerror")]
        private static extern IntPtr DlErrorC();

        private static volatile bool useLibc;
        [ThreadStatic] private static string lastError;

        public static IntPtr Load(string pathOrName)
        {
            lastError = null;
            if (string.IsNullOrEmpty(pathOrName)) { lastError = "empty path"; return IntPtr.Zero; }
            try
            {
                if (Platform.IsWindows)
                {
                    IntPtr h = LoadLibraryW(pathOrName);
                    if (h == IntPtr.Zero) lastError = "Win32 error " + Marshal.GetLastWin32Error();
                    return h;
                }
                if (Platform.IsLinux)
                {
                    IntPtr h = LinuxDlOpen(pathOrName, RTLD_NOW | RTLD_GLOBAL);
                    if (h == IntPtr.Zero) lastError = LinuxDlError() ?? "dlopen failed";
                    return h;
                }
                lastError = "unsupported platform";
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                return IntPtr.Zero;
            }
        }

        public static IntPtr LoadAny(params string[] candidates)
        {
            if (candidates == null || candidates.Length == 0) { lastError = "no candidates"; return IntPtr.Zero; }
            string err = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                string c = candidates[i];
                if (string.IsNullOrEmpty(c)) continue;
                IntPtr h = Load(c);
                if (h != IntPtr.Zero) return h;
                err = lastError;
            }
            lastError = err ?? "load failed";
            return IntPtr.Zero;
        }

        public static IntPtr GetSymbol(IntPtr handle, string symbol)
        {
            if (handle == IntPtr.Zero || string.IsNullOrEmpty(symbol)) return IntPtr.Zero;
            try
            {
                if (Platform.IsWindows) return GetProcAddress(handle, symbol);
                if (Platform.IsLinux) return LinuxDlSym(handle, symbol);
                return IntPtr.Zero;
            }
            catch { return IntPtr.Zero; }
        }

        public static bool HasSymbol(IntPtr handle, string symbol)
        {
            return GetSymbol(handle, symbol) != IntPtr.Zero;
        }

        public static string LastError()
        {
            return lastError ?? "";
        }

        private static IntPtr LinuxDlOpen(string path, int flags)
        {
            if (!useLibc)
            {
                try { return DlOpen2(path, flags); }
                catch (DllNotFoundException) { useLibc = true; }
            }
            return DlOpenC(path, flags);
        }

        private static IntPtr LinuxDlSym(IntPtr handle, string symbol)
        {
            if (!useLibc)
            {
                try { return DlSym2(handle, symbol); }
                catch (DllNotFoundException) { useLibc = true; }
            }
            return DlSymC(handle, symbol);
        }

        private static string LinuxDlError()
        {
            try
            {
                IntPtr p;
                if (!useLibc)
                {
                    try { p = DlError2(); }
                    catch (DllNotFoundException) { useLibc = true; p = DlErrorC(); }
                }
                else p = DlErrorC();
                return p != IntPtr.Zero ? Marshal.PtrToStringAnsi(p) : null;
            }
            catch { return null; }
        }
    }
}

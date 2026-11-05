using System;
using System.Runtime.InteropServices;

namespace StutterFix
{
    // 엔진의 SpriteMask 렌더 노드 배열은 노드를 넣을 때마다 딱 필요한 크기로 다시 할당해 전체를 복사한다.
    // (SpriteMaskJobs::SpriteMaskPrepareRendererNodes<0> 안의 resize_buffer_nocheck 호출, 노드 하나 0x1B0 바이트)
    // 넘기는 크기를 두 배로 바꿔 복사를 제곱에서 로그 수준으로 줄인다. Exit 단판 +11.8%.
    // 바이트가 다르면(엔진 업데이트) 건드리지 않는다.
    //   48 8B D3  mov rdx, rbx   ->  49 8B CF  mov rcx, r15
    //   49 8B CF  mov rcx, r15   ->  8D 14 1B  lea edx, [rbx+rbx]
    internal static class EnginePatch
    {
        private const int Rva = 0x1148649;
        private static readonly byte[] Guard = { 0x48, 0x3B, 0xD8, 0x76, 0x0B };   // cmp rbx,rax / jbe (바꾸지 않는 앞부분)
        private static readonly byte[] Original = { 0x48, 0x8B, 0xD3, 0x49, 0x8B, 0xCF };
        private static readonly byte[] Patched = { 0x49, 0x8B, 0xCF, 0x8D, 0x14, 0x1B };
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string name);
        [DllImport("kernel32.dll")] private static extern bool VirtualProtect(IntPtr addr, UIntPtr size, uint protect, out uint old);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(IntPtr process, IntPtr addr, UIntPtr size);
        private static bool applied;

        internal static void MaskGrow(bool on)
        {
            if (on == applied) return;
            IntPtr module = GetModuleHandleW("UnityPlayer.dll");
            if (module == IntPtr.Zero) return;
            IntPtr target = (IntPtr)(module.ToInt64() + Rva);
            for (int i = 0; i < Guard.Length; i++)
                if (Marshal.ReadByte(target, i - Guard.Length) != Guard[i]) { Skipped(); return; }
            byte[] want = on ? Original : Patched, put = on ? Patched : Original;
            for (int i = 0; i < want.Length; i++)
                if (Marshal.ReadByte(target, i) != want[i]) { Skipped(); return; }
            if (!VirtualProtect(target, (UIntPtr)put.Length, 0x40, out uint old)) { Skipped(); return; }
            Marshal.Copy(put, 0, target, put.Length);
            VirtualProtect(target, (UIntPtr)put.Length, old, out _);
            FlushInstructionCache(GetCurrentProcess(), target, (UIntPtr)put.Length);
            applied = on;
            Main.Entry.Logger.Log("[엔진 패치] 마스크 노드 배열 늘리기 " + (on ? "켬" : "끔"));
        }

        private static void Skipped() { Main.Entry.Logger.Log("[엔진 패치] 이 엔진 버전과 달라 건너뜀"); }
    }
}

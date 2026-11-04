#if DEV || AUTOTEST
using System;
using System.Runtime.InteropServices;

namespace StutterFix
{
    // 시험 전용: 엔진의 SpriteMask 렌더 노드 배열은 노드를 넣을 때마다 딱 필요한 크기로 다시 할당해 전체를 복사한다.
    // (SpriteMaskJobs::SpriteMaskPrepareRendererNodes<0> 안의 resize_buffer_nocheck 호출, 노드 하나 0x1B0 바이트)
    // 넘기는 크기를 두 배로 바꾸면 복사가 제곱에서 로그 수준으로 준다. 같은 바이트가 아니면 건드리지 않는다.
    //   48 8B D3  mov rdx, rbx        ->  49 8B CF  mov rcx, r15
    //   49 8B CF  mov rcx, r15        ->  8D 14 1B  lea edx, [rbx+rbx]
    internal static class EnginePatch
    {
        private const int MaskGrowRva = 0x1148649;
        private static readonly byte[] Before = { 0x48, 0x3B, 0xD8, 0x76, 0x0B, 0x48, 0x8B, 0xD3, 0x49, 0x8B, 0xCF };   // cmp rbx,rax / jbe / 교체 구간
        private static readonly byte[] After = { 0x49, 0x8B, 0xCF, 0x8D, 0x14, 0x1B };
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string name);
        [DllImport("kernel32.dll")] private static extern bool VirtualProtect(IntPtr addr, UIntPtr size, uint protect, out uint old);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(IntPtr process, IntPtr addr, UIntPtr size);

        internal static void MaskGrow()
        {
            IntPtr module = GetModuleHandleW("UnityPlayer.dll");
            if (module == IntPtr.Zero) throw new InvalidOperationException("UnityPlayer.dll 없음");
            IntPtr check = (IntPtr)(module.ToInt64() + MaskGrowRva - 5);
            for (int i = 0; i < Before.Length; i++)
                if (Marshal.ReadByte(check, i) != Before[i]) throw new InvalidOperationException("엔진 바이트가 다름: " + i);
            IntPtr target = (IntPtr)(module.ToInt64() + MaskGrowRva);
            if (!VirtualProtect(target, (UIntPtr)After.Length, 0x40, out uint old)) throw new InvalidOperationException("VirtualProtect 실패");
            Marshal.Copy(After, 0, target, After.Length);
            VirtualProtect(target, (UIntPtr)After.Length, old, out _);
            FlushInstructionCache(GetCurrentProcess(), target, (UIntPtr)After.Length);
            Main.Entry.Logger.Log("[enginepatch] 마스크 노드 배열 두 배로 늘리기 적용");
        }
    }
}
#endif

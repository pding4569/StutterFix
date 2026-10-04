using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 이미지 미리 압축해 넣기.
    // 이미지 미리 풀기(ImagePrefetch)의 작업 스레드가 PNG 를 푼 뒤 DXT 로도 압축해 두면(DxtEncoder), 메인 스레드에서 그 이미지를
    // 텍스처에 넣을 때 압축 결과를 여기 맡겨 둔다. 그리고
    //   - 누가(PACL2 의 이미지 손실 압축 등) Texture2D.Compress(false) 를 부르면 유니티 압축 대신 미리 한 결과를 넣는다.
    //   - (저사양 '이미지 압축해서 불러오기') 게임이 이미지를 그래픽카드에 올릴 때(Texture2D.Apply) 압축 결과로 바꿔 넣는다.
    // 텍스처 크기·형식이 미리 풀 때와 다르면(게임이 줄였다든지) 넣지 않고 원래대로 둔다. 고품질 압축(Compress(true))은 유니티에 맡긴다.
    // 불러오기가 끝날 때 쓰이지 않은 압축 결과는 버린다.
    internal static class TexCompress
    {
        internal static bool OwnOption;      // 저사양: 누가 압축을 부르지 않아도 이미지를 압축해서 올린다
        internal static long Substituted, SkippedAgain, Mismatch, EncodeTicks;
        private sealed class Pre { public IntPtr Blocks; public long Size; public bool Dxt5; public int W, H; public TextureFormat Src; }
        private sealed class RefEq : IEqualityComparer<Texture2D>
        {
            public bool Equals(Texture2D a, Texture2D b) { return ReferenceEquals(a, b); }
            public int GetHashCode(Texture2D o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly Dictionary<Texture2D, Pre> pending = new Dictionary<Texture2D, Pre>(new RefEq());
        private static readonly HashSet<Texture2D> ours = new HashSet<Texture2D>(new RefEq());
        // 처음부터 압축 결과로 넣은 것(ImagePrefetch.LoadImage). 곧 이어 압축이 불리는지(PACL2 Compress / 저사양 옵션의 Apply) 센다.
        private static readonly HashSet<Texture2D> direct = new HashSet<Texture2D>(new RefEq());
        internal static long Direct, DirectConfirmed;
        private static int registeredThisLoad;
        private static bool verifying;
        private static int verifiedThisLoad;

        // 작업 스레드가 미리 압축할지 (PACL2 손실 압축이 켜져 있거나, 저사양 옵션)
        internal static bool Planned { get { return OwnOption || Compat.Pacl2Lossy; } }

        internal static void Install(Harmony h)
        {
            try
            {
                foreach (var m in typeof(Texture2D).GetMethods())
                    if (m.Name == "Compress" && m.GetParameters().Length == 1)
                        h.Patch(m, prefix: new HarmonyMethod(typeof(TexCompress), nameof(CompressPrefix)) { priority = Priority.First });
                var apply = AccessTools.Method(typeof(Texture2D), "Apply", new[] { typeof(bool), typeof(bool) });
                if (apply != null) h.Patch(apply, prefix: new HarmonyMethod(typeof(TexCompress), nameof(ApplyPrefix)) { priority = Priority.First });
                Main.Entry.Logger.Log("[이미지 압축] 설치");
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[이미지 압축] 설치 실패: " + ex.Message); }
        }

        // 메인 스레드: 미리 푼 이미지를 텍스처에 넣은 직후 (압축 결과의 주인이 여기로 넘어온다)
        internal static void Register(Texture2D tex, IntPtr blocks, long size, bool dxt5, int w, int h, TextureFormat src)
        {
            registeredThisLoad++;
            Pre old;
            if (pending.TryGetValue(tex, out old)) Marshal.FreeHGlobal(old.Blocks);
            pending[tex] = new Pre { Blocks = blocks, Size = size, Dxt5 = dxt5, W = w, H = h, Src = src };
        }

        public static bool CompressPrefix(Texture2D __instance, bool __0)
        {
            if (verifying || (object)__instance == null) return true;
            if (ours.Contains(__instance)) { SkippedAgain++; if (direct.Remove(__instance)) DirectConfirmed++; return false; }   // 이미 미리 압축한 것을 넣었다
            if (__0) return true;   // 고품질은 유니티에
            return !Substitute(__instance);
        }

        public static void ApplyPrefix(Texture2D __instance)
        {
            if (!OwnOption || verifying || (object)__instance == null) return;
            if (direct.Count > 0 && direct.Remove(__instance)) { DirectConfirmed++; return; }   // 저사양 옵션: 이 Apply 에서 압축했을 것
            if (pending.Count == 0) return;
            if (pending.ContainsKey(__instance)) Substitute(__instance);
        }

        private static unsafe bool Substitute(Texture2D tex)
        {
            Pre p;
            if (!pending.TryGetValue(tex, out p)) return false;
            pending.Remove(tex);
            try
            {
                if (tex.width != p.W || tex.height != p.H || tex.format != p.Src || !tex.isReadable) { Mismatch++; return false; }
                if (Edition.Dev && verifiedThisLoad < 3) { verifiedThisLoad++; Verify(tex, p); }
                tex.Reinitialize(p.W, p.H, p.Dxt5 ? TextureFormat.DXT5 : TextureFormat.DXT1, false);
                tex.LoadRawTextureData(p.Blocks, (int)p.Size);
                ours.Add(tex);
                Substituted++;
                return true;
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[이미지 압축] 넣기 실패, 원래대로: " + ex.Message); return false; }
            finally { Marshal.FreeHGlobal(p.Blocks); }
        }

        // (개발자용) 같은 이미지를 유니티 압축으로도 해 보고, 원본 대비 평균 오차와 형식을 비교
        private static unsafe void Verify(Texture2D tex, Pre p)
        {
            Texture2D copy = null;
            try
            {
                var orig = tex.GetRawTextureData<byte>();
                int layout = p.Src == TextureFormat.ARGB32 ? 1 : p.Src == TextureFormat.RGB24 ? 2 : 0;
                byte* op = (byte*)Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(orig);
                copy = new Texture2D(p.W, p.H, p.Src, false);
                copy.LoadRawTextureData(orig);
                verifying = true;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                try { copy.Compress(false); } finally { verifying = false; }
                double unityMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                var u = copy.GetRawTextureData<byte>();
                byte* up = (byte*)Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(u);
                bool sameFmt = copy.format == (p.Dxt5 ? TextureFormat.DXT5 : TextureFormat.DXT1);
                double eo = DxtEncoder.MeanError(op, p.W, p.H, layout, (byte*)p.Blocks, p.Dxt5);
                double eu = sameFmt ? DxtEncoder.MeanError(op, p.W, p.H, layout, up, p.Dxt5) : -1;
                Main.Entry.Logger.Log(string.Format("[이미지 압축 검증] {0}x{1} {2}: 원본과의 평균 오차 - 이 모드 {3:F2}, 유니티 {4:F2} (/255), 유니티 형식 {5}{6}, 유니티 압축 {7:F0}ms",
                    p.W, p.H, p.Src, eo, eu, copy.format, sameFmt ? " (같음)" : " (다름)", unityMs));
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[이미지 압축 검증] 실패: " + ex.Message); }
            finally { verifying = false; if (copy != null) UnityEngine.Object.Destroy(copy); }
        }

        // 미리 압축한 결과를 처음부터 넣어도 되는가: 이번 불러오기에서 원래 길(원본을 넣고 압축 순간에 바꿔 넣기)로 넣은 처음 16장이
        // 모두 압축됐으면(PACL2 가 이 맵의 이미지를 압축한다는 뜻) 나머지는 원본을 넣지 않고 압축 결과를 바로 넣는다.
        // 게임 LoadTexture 는 LoadImage 바로 뒤에 Apply(PACL2 는 그 앞에서 Compress)를 부르므로 결과 텍스처는 같다(DXT, 읽기 불가).
        // 원본(2048x2048 이면 16MB)을 넣고 버리는 일이 없어진다.
        internal static bool DirectAllowed
        {
            get { return Planned && registeredThisLoad >= 16 && Substituted == registeredThisLoad && Mismatch == 0 && pending.Count == 0; }
        }

        internal static void MarkDirect(Texture2D tex) { ours.Add(tex); direct.Add(tex); Direct++; }

        // 불러오기가 끝나면: 쓰이지 않은 압축 결과를 버리고 요약
        internal static string EndLoad()
        {
            int unused = pending.Count;
            foreach (var kv in pending) Marshal.FreeHGlobal(kv.Value.Blocks);
            pending.Clear(); ours.Clear(); verifiedThisLoad = 0;
            long notConfirmed = direct.Count; direct.Clear(); registeredThisLoad = 0;
            if (Substituted + unused + Mismatch + Direct == 0) return "";
            string s = string.Format(", 미리 압축 넣음 {0}장 (쓰지 않음 {1}, 크기·형식 달라 원래대로 {2}, 압축 {3:F0}ms/작업 스레드 합)",
                Substituted, unused, Mismatch, EncodeTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency)
                + (Direct > 0 ? string.Format(", 처음부터 압축으로 넣음 {0}장 (뒤이어 압축 불림 {1}, 안 불림 {2})", Direct, DirectConfirmed, notConfirmed) : "");
            Substituted = Mismatch = SkippedAgain = 0; EncodeTicks = 0; Direct = DirectConfirmed = 0;
            return s;
        }
    }
}

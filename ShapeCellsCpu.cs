using System;
using System.Collections.Generic;

namespace StutterFix
{
    // 형태 칸(GPU 장식이 투명 여백을 그리지 않게)의 재료: 이미지를 푸는 작업 스레드에서 16x16 칸마다 알파>0 텍셀이 있는지 미리 계산한다.
    // 맵을 열 때 이미 하는 PNG 풀기 직후라 GPU 에 텍스처를 다 올려 읽을 필요(로딩 +4초)도, 켤 때 다시 계획할 필요도 없다.
    // 여백 4 텍셀: 쌍선형 샘플(1 텍셀)과 DXT 압축 블록(4x4)까지 덮는다. 줄은 유니티 텍스처 원본 배열 순서(v = y/높이).
    internal static class ShapeCellsCpu
    {
        internal const int Margin = 4;
        // 칸 수: 긴 변 256 이상은 32x32(가는 선·윤곽 이미지에서 더 깎인다, thin_triangle 19.7% -> 10.5%), 그보다 작으면 16x16
        internal static int GridFor(int w, int h) => Math.Max(w, h) >= 256 ? 32 : 16;
        static readonly Dictionary<int, ulong[]> byTexture = new Dictionary<int, ulong[]>();
        static readonly object gate = new object();
        internal sealed class OpaqueInfo
        {
            internal ulong[] Bits;
            internal bool All;
            internal int Width, Height;
        }
        sealed class OpaqueEntry { internal WeakReference<UnityEngine.Texture2D> Texture; internal OpaqueInfo Info; internal uint Version; internal bool Armed; }
        static readonly Dictionary<int, OpaqueEntry> opaqueByTexture = new Dictionary<int, OpaqueEntry>();

        // 작업 스레드: format 4 = RGBA32(알파 +3), 5 = ARGB32(알파 +0). 그 밖(알파 없음)은 null = 전부 불투명.
        internal static volatile bool CollectOpaque;
        internal static unsafe ulong[] Compute(IntPtr px, int w, int h, int format)
            => Compute(px, w, h, format, out _);
        internal static unsafe ulong[] Compute(IntPtr px, int w, int h, int format, out OpaqueInfo opaque)
        {
            opaque = null;
            // Unlike occupancy, opacity must examine EVERY texel, including small images.
            // 진단 전용(occlude): 4개 맵에서 화면 전체 불투명 가리개 0개라 기본 끔. 켜면 다음 맵 열기부터 모든 텍셀을 훑는다.
            if (CollectOpaque && px != IntPtr.Zero && w > 0 && h > 0 && (format == 3 || format == 4 || format == 5))
            {
                int grid = GridFor(w, h), alpha = format == 4 ? 3 : 0;
                var ob = new ulong[grid * grid / 64];
                for (int i = 0; i < ob.Length; i++) ob[i] = ulong.MaxValue;
                bool all = true;
                if (format != 3) for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    if (((byte*)px)[((long)y * w + x) * 4 + alpha] != 255)
                    {
                        int bit = (y * grid / h) * grid + x * grid / w;
                        ob[bit >> 6] &= ~(1UL << (bit & 63)); all = false;
                    }
                opaque = new OpaqueInfo { Bits = ob, All = all, Width = w, Height = h };
            }
            if (px == IntPtr.Zero || w < 64 && h < 64) return null;
            int ao = format == 4 ? 3 : format == 5 ? 0 : -1;
            if (ao < 0) return null;
            int Grid = GridFor(w, h);
            // 촘촘한 칸(직통 렌더러의 빈 칸 뺀 도형용): 한 번 훑으면서 촘촘한 격자에 표시하고, 기존 격자는 그것을 묶어서 만든다
            // (칸 번호가 정수 내림이라 floor(floor(x*kG/w)/k) == floor(x*G/w), 결과가 기존과 같다).
            int k = FineFactor(w, h), FG = Grid * k;
            var fine = new ulong[FG * FG / 64];
            byte* p = (byte*)px;
            // 줄마다 알파>0 인 텍셀의 칸 범위(여백 포함)를 표시한다. 같은 칸을 이미 표시했으면 그 칸 끝까지 건너뛴다.
            var rowCells = new bool[FG];
            for (int y = 0; y < h; y++)
            {
                Array.Clear(rowCells, 0, FG); bool any = false;
                byte* row = p + (long)y * w * 4 + ao;
                for (int x = 0; x < w; x++)
                {
                    if (row[x * 4] == 0) continue;
                    int c0 = Math.Max(0, x - Margin) * FG / w, c1 = Math.Min(w - 1, x + Margin) * FG / w;
                    for (int c = c0; c <= c1; c++) rowCells[c] = true;
                    any = true;
                    int cellEnd = (c1 + 1) * w / FG;   // 이 칸 끝 근처까지 건너뛰되 다음 칸 여백은 다시 본다
                    int skip = cellEnd - Margin - 1; if (skip > x) x = skip;
                }
                if (!any) continue;
                int r0 = Math.Max(0, y - Margin) * FG / h, r1 = Math.Min(h - 1, y + Margin) * FG / h;
                for (int r = r0; r <= r1; r++) for (int c = 0; c < FG; c++) if (rowCells[c]) { int b = r * FG + c; fine[b >> 6] |= 1UL << (b & 63); }
            }
            var bits = new ulong[Grid * Grid / 64];
            if (k == 1) Array.Copy(fine, bits, bits.Length);
            else for (int fy = 0; fy < FG; fy++) for (int fx = 0; fx < FG; fx++)
            {
                int fb = fy * FG + fx; if ((fine[fb >> 6] & (1UL << (fb & 63))) == 0) continue;
                int b = (fy / k) * Grid + fx / k; bits[b >> 6] |= 1UL << (b & 63);
            }
            if (k > 1) lock (gate) fineOf.Add(bits, fine);
            return bits;
        }
        // 촘촘한 칸 배율: 긴 변 1024 이상 8배(32 -> 256칸), 512 이상 4배, 256 이상 2배
        internal static int FineFactor(int w, int h) { int m = Math.Max(w, h); return m >= 1024 ? 8 : m >= 512 ? 4 : m >= 256 ? 2 : 1; }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ulong[], ulong[]> fineOf = new System.Runtime.CompilerServices.ConditionalWeakTable<ulong[], ulong[]>();
        // 텍스처의 가장 촘촘한 칸(없으면 기존 칸)
        internal static bool TryGetFine(int textureId, out ulong[] bits)
        {
            lock (gate)
            {
                if (!byTexture.TryGetValue(textureId, out bits) || bits == null) return false;
                if (fineOf.TryGetValue(bits, out var f)) bits = f;
                return true;
            }
        }
        // 메인 스레드: 텍스처에 픽셀을 넣을 때
        internal static void Register(UnityEngine.Texture2D tex, ulong[] bits)
        {
            if (!tex || bits == null) return;
            lock (gate) byTexture[tex.GetInstanceID()] = bits;
        }
        internal static bool TryGet(int textureId, out ulong[] bits) { lock (gate) return byTexture.TryGetValue(textureId, out bits); }
        internal static void RegisterOpaque(UnityEngine.Texture2D tex, OpaqueInfo info)
        {
            if (!tex || info == null) return;
            // Decoded opacity does not prove opacity after a lossy texture conversion.
            if (tex.format != UnityEngine.TextureFormat.RGBA32 && tex.format != UnityEngine.TextureFormat.ARGB32
                && tex.format != UnityEngine.TextureFormat.RGB24) return;
            lock (gate) opaqueByTexture[tex.GetInstanceID()] = new OpaqueEntry { Texture = new WeakReference<UnityEngine.Texture2D>(tex), Info = info, Version = tex.updateCount };
        }
        internal static bool TryGetOpaque(UnityEngine.Texture2D tex, out ulong[] bits, out bool allOpaque)
        {
            bits = null; allOpaque = false;
            if (!tex) return false;
            lock (gate)
            {
                if (!opaqueByTexture.TryGetValue(tex.GetInstanceID(), out var entry) || !entry.Texture.TryGetTarget(out var registered) || registered != tex
                    || tex.width != entry.Info.Width || tex.height != entry.Info.Height
                    || (tex.format != UnityEngine.TextureFormat.RGBA32 && tex.format != UnityEngine.TextureFormat.ARGB32
                        && tex.format != UnityEngine.TextureFormat.RGB24)) return false;
                // Register is before the game's initial Apply. Arm after loading, on first query.
                if (!entry.Armed) { entry.Version = tex.updateCount; entry.Armed = true; }
                else if (tex.updateCount != entry.Version) return false;
                bits = entry.Info.Bits; allOpaque = entry.Info.All; return true;
            }
        }
        internal static int Count { get { lock (gate) return byTexture.Count; } }
    }
}

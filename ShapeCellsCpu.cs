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

        // 작업 스레드: format 4 = RGBA32(알파 +3), 5 = ARGB32(알파 +0). 그 밖(알파 없음)은 null = 전부 불투명.
        internal static unsafe ulong[] Compute(IntPtr px, int w, int h, int format)
        {
            if (px == IntPtr.Zero || w < 64 && h < 64) return null;
            int ao = format == 4 ? 3 : format == 5 ? 0 : -1;
            if (ao < 0) return null;
            int Grid = GridFor(w, h);
            var bits = new ulong[Grid * Grid / 64];
            byte* p = (byte*)px;
            // 줄마다 알파>0 인 텍셀의 칸 범위(여백 포함)를 표시한다. 같은 칸을 이미 표시했으면 그 칸 끝까지 건너뛴다.
            var rowCells = new bool[Grid];
            for (int y = 0; y < h; y++)
            {
                Array.Clear(rowCells, 0, Grid); bool any = false;
                byte* row = p + (long)y * w * 4 + ao;
                for (int x = 0; x < w; x++)
                {
                    if (row[x * 4] == 0) continue;
                    int c0 = Math.Max(0, x - Margin) * Grid / w, c1 = Math.Min(w - 1, x + Margin) * Grid / w;
                    for (int c = c0; c <= c1; c++) rowCells[c] = true;
                    any = true;
                    int cellEnd = (c1 + 1) * w / Grid;   // 이 칸 끝 근처까지 건너뛰되 다음 칸 여백은 다시 본다
                    int skip = cellEnd - Margin - 1; if (skip > x) x = skip;
                }
                if (!any) continue;
                int r0 = Math.Max(0, y - Margin) * Grid / h, r1 = Math.Min(h - 1, y + Margin) * Grid / h;
                for (int r = r0; r <= r1; r++) for (int c = 0; c < Grid; c++) if (rowCells[c]) { int b = r * Grid + c; bits[b >> 6] |= 1UL << (b & 63); }
            }
            return bits;
        }
        // 메인 스레드: 텍스처에 픽셀을 넣을 때
        internal static void Register(UnityEngine.Texture2D tex, ulong[] bits)
        {
            if (!tex || bits == null) return;
            lock (gate) byTexture[tex.GetInstanceID()] = bits;
        }
        internal static bool TryGet(int textureId, out ulong[] bits) { lock (gate) return byTexture.TryGetValue(textureId, out bits); }
        internal static int Count { get { lock (gate) return byTexture.Count; } }
    }
}

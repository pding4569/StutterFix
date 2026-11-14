using System;

namespace StutterFix
{
    // 고정 순서의 희소 켜짐 집합. 풀의 비활성 엔트리는 프레임마다 확인하지 않는다.
    internal sealed class DirectRenderVisibilityIndex
    {
        ulong[] words = Array.Empty<ulong>();
        int count;
        internal void Reset(int size)
        {
            int n = (size + 63) >> 6;
            if (words.Length < n) words = new ulong[n];
            else Array.Clear(words, 0, words.Length);
            count = size;
        }
        internal void Set(int index, bool on)
        {
            if ((uint)index >= (uint)count) return;
            ulong bit = 1UL << (index & 63);
            if (on) words[index >> 6] |= bit; else words[index >> 6] &= ~bit;
        }
        internal int Next(int after)
        {
            int start = after + 1;
            if ((uint)start >= (uint)count) return -1;
            int word = start >> 6;
            ulong value = words[word] & (ulong.MaxValue << (start & 63));
            while (value == 0) { if (++word >= ((count + 63) >> 6)) return -1; value = words[word]; }
            int bit = 0;
            if ((value & 0xffffffffUL) == 0) { bit += 32; value >>= 32; }
            if ((value & 0xffffUL) == 0) { bit += 16; value >>= 16; }
            if ((value & 0xffUL) == 0) { bit += 8; value >>= 8; }
            if ((value & 0xfUL) == 0) { bit += 4; value >>= 4; }
            if ((value & 3UL) == 0) { bit += 2; value >>= 2; }
            if ((value & 1UL) == 0) bit++;
            return (word << 6) + bit;
        }
    }
}

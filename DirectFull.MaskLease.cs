using System;

namespace StutterFix
{
    // No Unity dependency: the original mask is data, including 0 and bit 31.
    // Re-entering must never replace that original with an already masked value.
    internal sealed class DirectFullMaskLease
    {
        internal bool Active { get; private set; }
        internal int Original { get; private set; }
        internal int Removed { get; private set; }
        internal int Expected => Original & ~Removed;

        internal void Begin(int original, int removed)
        {
            if (Active) throw new InvalidOperationException("drfull mask lease already active");
            if ((original & removed) == 0) throw new ArgumentException("drfull: no visible map layers");
            Original = original; Removed = removed; Active = true;
        }

        internal void Clear() { Active = false; Original = Removed = 0; }
    }
}

using System;
using System.Collections.Generic;

namespace StutterFix
{
    // 목록/소유권 경계에서만 필터링한다. null·중복·CPU fallback의 순서를 보존한다.
    internal sealed class GpuLateListCache<T> where T : class
    {
        private List<T> source;
        private bool hasItems;
        private int sourceVersion, sourceCount, ownershipEpoch;
        internal T[] Items = Array.Empty<T>();
        internal int Count { get; private set; }
        internal int Skipped { get; private set; }
        internal long Rebuilds { get; private set; }
        internal bool Ready => source != null;

        internal void Prepare(List<T> list, int version, int ownership, Func<T, bool> skip, Action<T> skipped)
        {
            if (ReferenceEquals(source, list) && sourceVersion == version
                && sourceCount == list.Count && ownershipEpoch == ownership) return;
            // 콜백 예외 뒤 부분 결과를 다음 프레임에서 재사용하지 않는다.
            source = null;
            hasItems = true;
            if (Items.Length < list.Count) Array.Resize(ref Items, list.Count);
            Array.Clear(Items, 0, Items.Length);
            Count = 0; Skipped = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (skip(item)) { skipped(item); Skipped++; }
                else Items[Count++] = item;
            }
            sourceVersion = version; sourceCount = list.Count; ownershipEpoch = ownership;
            source = list; Rebuilds++;
        }

        internal void Clear()
        {
            if (!hasItems) return;
            source = null;
            Array.Clear(Items, 0, Items.Length);
            Count = Skipped = 0; hasItems = false;
        }
    }
}

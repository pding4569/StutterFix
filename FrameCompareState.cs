#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;

namespace StutterFix
{
    // Unity-independent snapshot comparison, shared with the offline fixture.
    internal sealed class FrameCompareState
    {
        internal sealed class Entry
        {
            internal string Label;
            internal bool Relevant;
            internal readonly SortedDictionary<string, string> Fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        }
        internal readonly SortedDictionary<string, Entry> Entries = new SortedDictionary<string, Entry>(StringComparer.Ordinal);
        internal string Summary;
        internal sealed class Delta
        {
            internal int Changed, Ignored;
            internal string Details;
        }
        internal Delta Compare(FrameCompareState other)
        {
            var result = new Delta(); var details = new List<string>(); var hidden = new List<string>();
            foreach (string key in Entries.Keys.Union(other.Entries.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                Entries.TryGetValue(key, out var a); other.Entries.TryGetValue(key, out var b);
                bool relevant = (a?.Relevant ?? false) || (b?.Relevant ?? false);
                var target = relevant ? details : hidden;
                if (a == null || b == null)
                {
                    if (relevant) result.Changed++; else result.Ignored++;
                    if (target.Count < 12) target.Add(key + " " + (a?.Label ?? b?.Label) + (a == null ? " added" : " removed") + (relevant ? "" : " ignored-hidden"));
                    continue;
                }
                foreach (string field in a.Fields.Keys.Union(b.Fields.Keys).OrderBy(k => k, StringComparer.Ordinal))
                {
                    a.Fields.TryGetValue(field, out string x); b.Fields.TryGetValue(field, out string y);
                    if (x == y) continue;
                    if (relevant) result.Changed++; else result.Ignored++;
                    if (target.Count < 12) target.Add(key + " " + a.Label + "." + field + "=" + x + "->" + y + (relevant ? "" : " ignored-hidden"));
                }
                if (a.Relevant != b.Relevant)
                {
                    result.Changed++;
                    if (details.Count < 12) details.Add(key + " relevance=" + a.Relevant + "->" + b.Relevant);
                }
            }
            result.Details = string.Join("; ", details.Concat(hidden).Take(12));
            return result;
        }
    }
}
#endif

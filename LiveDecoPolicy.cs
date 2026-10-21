using System;
using System.Collections.Generic;

namespace StutterFix
{
    internal static class LiveDecoPolicy
    {
        internal static bool RendererLive(bool exists, bool sceneValid, bool enabled, bool activeInHierarchy, bool forcedOff)
            => exists && sceneValid && enabled && activeInHierarchy && !forcedOff;

        internal readonly struct Item
        {
            internal readonly SceneRenderOrder.Key Key;
            internal readonly bool Owned;
            internal readonly string Signature;
            internal readonly int UnityLayer;
            internal Item(SceneRenderOrder.Key key, bool owned, string signature = "", int unityLayer = 0)
            { Key = key; Owned = owned; Signature = signature; UnityLayer = unityLayer; }
        }
        internal sealed class Result
        {
            internal int Candidates, BarrierRuns, MeshRuns, KeySplits, SignatureSplits, LayerSplits, AmbiguousTies;
            internal readonly List<int> First = new List<int>();
            internal readonly List<int> Counts = new List<int>();
        }
        // All live scene renderer/material slots, in original sort order. No texture
        // split: gate 1 assumes the atlas problem will be solved in gate 2.
        internal static Result Count(IReadOnlyList<Item> rows)
        {
            var result = new Result(); bool previousOwned = false;
            for (int i = 0; i < rows.Count; i++)
            {
                var a = rows[i];
                if (i > 0 && rows[i - 1].Key.CompareTo(a.Key) > 0) throw new ArgumentException("unsorted rows");
                if (!a.Owned) { previousOwned = false; continue; }
                result.Candidates++;
                bool newRun = !previousOwned;
                if (newRun) result.BarrierRuns++;
                else
                {
                    var b = rows[i - 1];
                    if (!a.Key.SameBucket(b.Key)) { result.KeySplits++; newRun = true; }
                    else if (a.Signature != b.Signature) { result.SignatureSplits++; newRun = true; }
                    else if (a.UnityLayer != b.UnityLayer) { result.LayerSplits++; newRun = true; }
                }
                if (newRun) { result.MeshRuns++; result.First.Add(i); result.Counts.Add(0); }
                result.Counts[result.Counts.Count - 1]++;
                previousOwned = true;
            }
            // A tied bucket with both owned and external draws cannot be ordered by an
            // invented renderer-ID fallback. Refuse a conclusive gate for that snapshot.
            for (int start = 0; start < rows.Count;)
            {
                int end = start + 1; bool own = rows[start].Owned, external = !own;
                while (end < rows.Count && rows[start].Key.SameMaterialTie(rows[end].Key))
                { own |= rows[end].Owned; external |= !rows[end].Owned; end++; }
                if (own && external) result.AmbiguousTies++;
                start = end;
            }
            return result;
        }
        internal static string Gate(int live, Result r, bool orderSupported, bool propertyBlocksVerified = true)
        {
            if (live <= 0) return "INCONCLUSIVE";
            if (!orderSupported || !propertyBlocksVerified || r.AmbiguousTies != 0) return "INCONCLUSIVE";
            return r.MeshRuns <= 200 && (long)r.Candidates * 5 >= (long)live * 4 ? "PASS" : "FAIL";
        }
    }
}

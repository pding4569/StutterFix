#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    internal sealed partial class DecoRender
    {
        readonly Dictionary<string, List<Renderer>> orderedKeys = new Dictionary<string, List<Renderer>>();
        readonly Dictionary<Renderer, RenderOrder> orderedState = new Dictionary<Renderer, RenderOrder>();
        readonly Dictionary<string, List<Group>> runsByKey = new Dictionary<string, List<Group>>();
        readonly Dictionary<Renderer, Item> orderedCandidates = new Dictionary<Renderer, Item>();
        readonly HashSet<string> topologyKeys = new HashSet<string>();
        readonly HashSet<string> reorderKeys = new HashSet<string>();
        long topologyKeyChanges, topologyRebuilds, topologyFullRebuilds;
        int topologyChangedLast;

        void SortKeyRenderers(List<Renderer> list, Camera camera)
        {
            var order = orderedState;
            foreach (var r in list) {
                var m = r.sharedMaterial;
                order[r] = new RenderOrder { Queue = m ? m.renderQueue : int.MinValue,
                    Material = m ? m.GetInstanceID() : 0, Id = r.GetInstanceID(), Distance = SortDistance(r, camera) };
            }
            list.Sort((a,b) => {
                var x = order[a]; var y = order[b]; int c = x.Queue.CompareTo(y.Queue);
                if (c == 0) c = y.Distance.CompareTo(x.Distance);
                if (c == 0) c = x.Material.CompareTo(y.Material);
                return c != 0 ? c : x.Id.CompareTo(y.Id);
            });
        }

        // Visibility-only changes use the on-time order, including waiting barriers.
        // Structural/setup changes still take the conservative discovery path below.
        bool RebuildChangedKeys(Camera camera)
        {
            bool changed = false;
            topologyChangedLast = topologyKeys.Count; topologyKeyChanges += topologyChangedLast;
            foreach (var key in topologyKeys) {
                List<Renderer> list;
                if (!orderedKeys.TryGetValue(key, out list)) continue;
                topologyRebuilds++;
                if (reorderKeys.Contains(key)) {
                    list.RemoveAll(r => !r);
                    SortKeyRenderers(list, camera);
                }
                List<Group> old;
                if (!runsByKey.TryGetValue(key, out old)) old = new List<Group>();
                var byFirst = new Dictionary<int, Group>();
                foreach (var g in old) byFirst[g.Items[0].Id] = g;
                var used = new HashSet<Group>();
                var plans = new List<List<Item>>(); List<Item> run = null;
                foreach (var r in list) {
                    if (!r) continue;
                    Item i;
                    if (!orderedCandidates.TryGetValue(r, out i)) {
                        if (Drawing(r)) run = null;
                        continue;
                    }
                    if (!mergeCameras || run == null || run[0].GroupKey != i.GroupKey
                        || orderedState[run[0].R].Distance != orderedState[r].Distance) {
                        run = new List<Item>(); plans.Add(run);
                    }
                    run.Add(i);
                }
                foreach (var plan in plans) {
                    Group match;
                    if (byFirst.TryGetValue(plan[0].Id, out match) && match.RunKey == plan[0].GroupKey
                        && match.Items.Count == plan.Count) {
                        for (int n = 0; n < plan.Count; n++) if (!ReferenceEquals(match.Items[n], plan[n])) { match = null; break; }
                    } else match = null;
                    if (match == null) { match = CreateRun(plan, plan[0].GroupKey); changed = true; }
                    used.Add(match);
                }
                foreach (var g in old) if (!used.Contains(g)) { ReleaseGroup(g); groups.Remove(g); changed = true; }
                runsByKey[key] = new List<Group>(used);
            }
            topologyKeys.Clear(); reorderKeys.Clear();
            return changed;
        }

    }
}
#endif

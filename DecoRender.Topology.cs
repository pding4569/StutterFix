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

        readonly Dictionary<string, int> topologyCauses = new Dictionary<string, int> {
            {"on",0}, {"world-z",0}, {"sort-distance",0}, {"sort-axis",0}, {"merge-cameras",0},
            {"mesh-active",0}, {"setup",0}, {"watch",0}, {"other",0}, {"camera-list",0}, {"prune",0}
        };
        readonly Dictionary<string, int> pendingCauses = new Dictionary<string, int>();
        readonly HashSet<string> retiredKeys = new HashSet<string>();
        readonly Dictionary<string, int> topologyCauseItems = new Dictionary<string, int>();
        readonly Dictionary<string, int> pendingCauseItems = new Dictionary<string, int>();
        string lastTopologyCause = "none", lastBuildCauses = "none";
        int topologyCauseLogs, zChangedItems, zSamples, pendingZItems;
        double pendingMaxDeltaZ;
        readonly System.Text.StringBuilder pendingZExamples = new System.Text.StringBuilder();
        double maxDeltaZ;
        readonly System.Text.StringBuilder zExamples = new System.Text.StringBuilder();

        void TopologyCause(string cause, int affectedItems = 0)
        {
            Count(topologyCauses, cause); Count(pendingCauses, cause); lastTopologyCause = cause;
            for (int n = 0; n < affectedItems; n++) CountCauseItem(cause);
        }
        void CountCauseItem(string cause)
        { Count(topologyCauseItems, cause); Count(pendingCauseItems, cause); }
        void DirtyKey(string key, string cause, bool reorder = true)
        {
            TopologyCause(cause, cause == "sort-distance" ? 1 : 0); topologyKeys.Add(key);
            if (reorder) reorderKeys.Add(key);
        }
        void DirtyAllKeys(string cause)
        {
            TopologyCause(cause, items.Count);
            foreach (var key in orderedKeys.Keys) { topologyKeys.Add(key); reorderKeys.Add(key); }
        }
        // Unsupported structural changes retire only this key for the rest of the session.
        // Never rediscover the entire scene from a per-frame decoration notification.
        void RetireKey(string key, string cause)
        {
            DirtyKey(key, cause); fallbackKeys.Add(key);
            if (!retiredKeys.Add(key)) return;
            for (int n = items.Count - 1; n >= 0; n--) {
                var i = items[n]; if (OrderKey(i) != key) continue;
                CountCauseItem(cause); RestoreSource(i); orderedCandidates.Remove(i.R); residents.Remove(i.D);
                items.RemoveAt(n); vertexCount -= i.Vertices.Length;
                if (i.Moved) movingCandidates = Math.Max(0, movingCandidates - 1);
            }
        }
        void RetireMaterialUsers(int materialId)
        {
            var affected = new HashSet<string>();
            foreach (var i in items) if (i.MaterialId == materialId) affected.Add(OrderKey(i));
            foreach (var key in affected) RetireKey(key, "shared-material-event");
        }
        void RecordZChange(Item i, float oldZ, float newZ)
        {
            TopologyCause("world-z", 1); zChangedItems++; pendingZItems++;
            pendingMaxDeltaZ = Math.Max(pendingMaxDeltaZ, Math.Abs((double)newZ - oldZ));
            if (pendingZItems <= 10) pendingZExamples.Append('(').Append(i.Index).Append(',')
                .Append(oldZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(newZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(") ");
            maxDeltaZ = Math.Max(maxDeltaZ, Math.Abs((double)newZ - oldZ));
            if (zSamples++ < 10) zExamples.Append('(').Append(i.Index).Append(',')
                .Append(oldZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(newZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(") ");
        }
        void LogTopologyBuild(string mode)
        {
            lastBuildCauses = Counts(pendingCauses);
            if (topologyCauseLogs++ < 20) Log("topology-cause build=" + topologyCauseLogs + " mode=" + mode
                + " causes={" + lastBuildCauses + "} cause-items={" + Counts(pendingCauseItems) + "} changed-keys=" + topologyChangedLast
                + " z-items=" + pendingZItems + " max-abs-dz=" + pendingMaxDeltaZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " first10={" + pendingZExamples + "}");
            pendingCauses.Clear(); pendingCauseItems.Clear(); pendingZItems = 0; pendingMaxDeltaZ = 0; pendingZExamples.Clear();
        }
        string TopologyCauseState() => "\n topology-causes={" + Counts(topologyCauses) + "} last=" + lastTopologyCause
            + " cause-items/events={" + Counts(topologyCauseItems) + "}"
            + " last-build={" + lastBuildCauses + "} pending={" + Counts(pendingCauses) + "} retired-keys=" + retiredKeys.Count
            + " z-items=" + zChangedItems + " max-abs-dz=" + maxDeltaZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " first10={" + zExamples + "}";

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
        // Unsupported structural/setup changes retire their affected key to originals.
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
                    if (retiredKeys.Contains(key) || !orderedCandidates.TryGetValue(r, out i)) {
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

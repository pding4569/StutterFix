#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    internal sealed partial class DecoRender
    {
        const int SafetySlices = 128, UniformBudget = 4;
        readonly HashSet<Item> structuralDirty = new HashSet<Item>();
        readonly HashSet<Item> safetySet = new HashSet<Item>();
        readonly List<Item> safetyWork = new List<Item>(), uniformWork = new List<Item>();
        readonly List<Item> pruneRemoved = new List<Item>();
        readonly HashSet<BarrierWatch> barrierEvents = new HashSet<BarrierWatch>();
        readonly Dictionary<Renderer, BarrierWatch> barrierByRenderer = new Dictionary<Renderer, BarrierWatch>();
        readonly Dictionary<Renderer, Item> residentRenderers = new Dictionary<Renderer, Item>();
        readonly Samples uniformSamples = new Samples(), safetySamples = new Samples();
        readonly Samples callbackOverheadSamples = new Samples();
        readonly Samples safetyFrameSamples = new Samples();
        double frameSafetyMs;
        Harmony changeHooks;
        int safetyCursor, uniformCursor, barrierCursor, safetyFrame = -1;
        int structuralPolled, structuralEvents, uniformPolled;
        long lateStructuralChanges, lateBarrierChanges, lateVisibilityChanges;
        double uniformMs;
        bool takeoverActive;
        int takeoverFallbackCount;
        int hookCount;
        int slowBarrierCursor;

        void InstallChangeHooks()
        {
            changeHooks = new Harmony("StutterFix.DecoRender.Safety");
            var structural = new HashSet<string> { "SetSprite", "SetDepth", "SetMaskingDepth",
                "SetMaskingType", "SetMaskingTarget", "SetTile", "SetBlendMode", "EnableMeshRenderer" };
            foreach (var m in typeof(scrVisualDecoration).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
                string post = m.Name == "EnableMeshRenderer" ? nameof(MeshChangePost) : structural.Contains(m.Name) ? nameof(StructurePost)
                    : m.Name == "SetVisible" ? nameof(VisibilityPost) : null;
                if (post == null) continue;
                changeHooks.Patch(m, postfix: new HarmonyMethod(typeof(DecoRender), post)); hookCount++;
            }
        }
        static void MeshChangePost(scrVisualDecoration __instance)
        {
            var s = session; bool old;
            if (ReferenceEquals(s, null) || s.stopping) return;
            bool current = MeshOn(__instance);
            if (s.meshStates.TryGetValue(__instance, out old) && old != current) StructurePost(__instance);
            ObserveMesh(__instance, current);
        }
        void RemoveChangeHooks()
        {
            changeHooks?.UnpatchAll(changeHooks.Id); changeHooks = null;
            structuralDirty.Clear(); barrierEvents.Clear(); residentRenderers.Clear(); barrierByRenderer.Clear();
        }
        static void StructurePost(scrVisualDecoration __instance)
        {
            var s = session;
            if (ReferenceEquals(s, null) || s.stopping) return;
            Notify(__instance);
            s.notifiedOwners.Add(__instance);
            Item i;
            if (s.residents.TryGetValue(__instance, out i)) s.structuralDirty.Add(i);
            // Structural changes can alter shared materials and masks used by other keys.
            // Those users are retired by PruneChanged when validation observes a change.
        }
        static void VisibilityPost(scrVisualDecoration __instance)
        {
            Notify(__instance);
            var s = session; Item i;
            if (!ReferenceEquals(s, null)) s.notifiedOwners.Add(__instance);
            if (!ReferenceEquals(s, null) && !s.stopping && s.residents.TryGetValue(__instance, out i))
                s.RefreshSourceState(i, false);
        }
        internal static void NotifyRenderer(Renderer r)
        {
            var s = session;
            if (ReferenceEquals(s, null) || s.stopping || ReferenceEquals(r, null)) return;
            BarrierWatch w;
            if (s.barrierByRenderer.TryGetValue(r, out w)) {
                s.barrierEvents.Add(w);
                if (s.preparedFrame == Time.frameCount) s.fallbackKeys.Add(w.Key);
            }
            Item i;
            if (s.residentRenderers.TryGetValue(r, out i)) {
                s.RefreshSourceState(i, false); s.signalled.Add(i);
                if (s.preparedFrame == Time.frameCount) s.fallbackKeys.Add(OrderKey(i));
            }
        }
        void RefreshSourceState(Item i, bool delayed)
        {
            bool alive = i.R && i.SourceObject;
            bool enabled = alive && i.R.enabled, active = alive && i.SourceObject.activeInHierarchy;
            bool off = alive && SourceOff(i);
            if (delayed && (i.CachedEnabled != enabled || i.CachedActive != active || i.CachedOff != off)) {
                lateVisibilityChanges++; signalled.Add(i); fallbackKeys.Add(OrderKey(i));
            }
            i.CachedEnabled = enabled; i.CachedActive = active; i.CachedOff = off;
        }
        void CheckSafetyBudget()
        {
            if (safetyFrame == Time.frameCount) return;
            safetyFrame = Time.frameCount; uniformMs = 0;
            long start = Stopwatch.GetTimestamp();
            safetyWork.Clear(); safetySet.Clear(); uniformWork.Clear();
            foreach (var i in structuralDirty) if (i.Resident && safetySet.Add(i)) safetyWork.Add(i);
            structuralEvents = safetyWork.Count;
            // Event validation includes uniforms; polling uniform reads has its own small budget.
            PruneChanged(safetyWork);
            structuralDirty.Clear(); safetyWork.Clear();
            int count = items.Count;
            structuralPolled = Math.Min(count, (count + SafetySlices - 1) / SafetySlices);
            for (int n = 0; n < structuralPolled; n++) {
                if (safetyCursor >= count) safetyCursor = 0;
                var i = items[safetyCursor++];
                RefreshSourceState(i, true);
                if (safetySet.Add(i)) safetyWork.Add(i);
            }
            PruneChanged(safetyWork, false, true);
            count = items.Count; uniformPolled = Math.Min(count, UniformBudget);
            for (int n = 0; n < uniformPolled; n++) {
                if (uniformCursor >= count) uniformCursor = 0;
                uniformWork.Add(items[uniformCursor++]);
            }
            // These checks also validate source structure, before touching its snapshot.
            PruneChanged(uniformWork, true, true);
            uniformSamples.Add(uniformMs);
            safetySamples.Add(Milliseconds(Stopwatch.GetTimestamp() - start));
        }
        string SafetyState() => "\n safety: slices=" + SafetySlices + " structural-polled=" + structuralPolled
            + " structural-events=" + structuralEvents + " uniforms-polled=" + uniformPolled + " hooks=" + hookCount
            + " late-changes={structure:" + lateStructuralChanges + ",visibility:" + lateVisibilityChanges
            + ",barrier:" + lateBarrierChanges + "} barrier-slices=8"
            + "\n UniformsUnchanged/subset-prune=" + uniformSamples.State()
            + "\n safety-checks/subset-prune=" + safetySamples.State()
            + "\n checks+takeover+restore/completed-frame=" + safetyFrameSamples.State()
            + "\n callback-orchestration/outside-prepare=" + callbackOverheadSamples.State();
    }
}
#endif

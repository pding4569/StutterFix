#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    // Explicit picture diagnostic. Nothing registered or allocated per frame while off.
    internal sealed class GpuSplit : MonoBehaviour
    {
        static GpuSplit instance;
        internal static bool Active => instance != null;
        readonly GpuSplitLedger<Renderer> ledger = new GpuSplitLedger<Renderer>(r => r, r => r.forceRenderingOff, (r, value) => r.forceRenderingOff = value);
        GpuSplitGroup groups;
        string selector;
        int frame = -1;
        long overwrites;

        internal static void Command(string arg)
        {
            arg = arg.Trim();
            if (arg == "off" || arg == "hide none") { Stop(); Log("hide=none restored=1"); return; }
            if (arg == "state") { Log("state=" + (Active ? instance.selector : "none") + " selected=" + (Active ? instance.ledger.Count : 0) + " external_unhide_writes=" + (Active ? instance.overwrites : 0)); return; }
            if (arg == "refresh")
            {
                if (!Active) throw new InvalidOperationException("gpusplit refresh: no hidden group");
                try { instance.Scan(); } catch { Stop(); throw; }
                return;
            }
            if (!arg.StartsWith("hide ", StringComparison.Ordinal)) throw new ArgumentException("gpusplit hide <group>|refresh|state|off");
            string target = arg.Substring(5).Trim();
            var group = GpuSplitPolicy.Parse(target); // Invalid input must not change the current session.
            if (GpuHide.Busy || GpuCensus.Busy) throw new InvalidOperationException("gpuhide/census active");
            if (Active && instance.groups == group)
            {
                try { instance.Scan(); } catch { Stop(); throw; }
                return;
            }
            Stop();
            var go = new GameObject("StutterFix.GpuSplit"); DontDestroyOnLoad(go);
            var p = go.AddComponent<GpuSplit>(); instance = p; p.groups = group; p.selector = target;
            try { p.Scan(); Camera.onPreCull += Enforce; }
            catch { Stop(); throw; }
        }

        void Scan()
        {
            Camera main = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                if (c.gameObject.scene.IsValid() && c.name == "Camera" && c.gameObject.activeInHierarchy)
                {
                    if (main) throw new InvalidOperationException("gpusplit: ambiguous main Camera");
                    main = c;
                }
            if (!main) throw new InvalidOperationException("gpusplit: main Camera missing");
            // TileCull's explicit renderer/topGlow ownership also covers detached glow renderers.
            var floors = new HashSet<Renderer>();
            foreach (var f in Resources.FindObjectsOfTypeAll<scrFloor>())
            {
                if (!f.gameObject.scene.IsValid()) continue;
                if (f.floorRenderer != null && f.floorRenderer.renderer) floors.Add(f.floorRenderer.renderer);
                if (f.topGlow) floors.Add(f.topGlow);
            }
            var planets = new HashSet<Renderer>();
            foreach (var p in Resources.FindObjectsOfTypeAll<PlanetRenderer>())
            {
                if (!p.gameObject.scene.IsValid()) continue;
                AddPlanet(p.tailParticles, planets); AddPlanet(p.tailParticlesCoop, planets);
                AddPlanet(p.coreParticles, planets); AddPlanet(p.sparks, planets); AddPlanet(p.deathExplosion, planets);
            }
            int addedBefore = ledger.Count;
            var counts = new int[6];
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r || !r.gameObject.scene.IsValid()) continue;
                bool deco = r.GetComponentInParent<scrDecoration>(true) != null;
                var group = GpuSplitPolicy.Classify(true, (main.cullingMask & (1 << r.gameObject.layer)) != 0,
                    r.GetComponentInParent<Canvas>(true) != null, deco, r is SpriteRenderer,
                    r.GetComponentInParent<TMPro.TMP_Text>(true) != null || r.GetComponentInParent<TextMesh>(true) != null,
                    r is ParticleSystemRenderer, floors.Contains(r) || r.GetComponentInParent<scrFloor>(true) != null,
                    planets.Contains(r) || r.GetComponentInParent<scrPlanet>(true) != null || r.GetComponentInParent<PlanetRenderer>(true) != null);
                for (int i = 0; i < counts.Length; i++) if ((int)group == (1 << i)) counts[i]++;
                if ((group & groups) != 0) ledger.Hide(r);
            }
            Log("hide=" + selector + " selected=" + ledger.Count + " added=" + (ledger.Count - addedBefore)
                + " census_image_text_particle_tiles_planets_other=" + string.Join(",", counts)
                + " camera_id=" + main.GetInstanceID() + " mask=" + main.cullingMask + " external_unhide_writes=" + overwrites);
        }
        static void AddPlanet(Component owner, HashSet<Renderer> renderers)
        {
            if (!owner) return;
            foreach (var r in owner.GetComponentsInChildren<Renderer>(true)) renderers.Add(r);
        }
        static void Enforce(Camera camera)
        {
            var p = instance;
            if (!p || p.frame == Time.frameCount) return;
            p.frame = Time.frameCount;
            p.overwrites += p.ledger.Enforce(); // List iteration + delegates only, no per-frame allocation/scan.
        }
        static void Log(string message) => Main.Entry.Logger.Log("[gpusplit] " + message);
        internal static void Stop()
        {
            Camera.onPreCull -= Enforce;
            var p = instance; instance = null;
            if (!p) return;
            p.ledger.Restore();
            Log("restored=" + p.selector + " external_unhide_writes=" + p.overwrites);
            Destroy(p.gameObject);
        }
        void OnDisable() { if (instance == this) Stop(); else ledger.Restore(); }
        void OnDestroy() { if (instance == this) Stop(); else ledger.Restore(); }
    }
}
#endif

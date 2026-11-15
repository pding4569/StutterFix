#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace StutterFix
{
    internal static unsafe partial class DirectFull
    {
        sealed class ProbeRenderer
        {
            internal Renderer Renderer; internal Behaviour Toggle; internal bool Forced; internal string Path, Route;
            internal UnityEngine.Object Identity => Renderer ? (UnityEngine.Object)Renderer : Toggle;
            internal bool Alive => Renderer || Toggle;
            internal bool Enabled => Renderer ? Renderer.enabled : Toggle.enabled;
            internal void Force(bool value) { if (Renderer) Renderer.forceRenderingOff = value; else if (Toggle) Toggle.enabled = !value; }
        }
        static readonly List<ProbeRenderer> probeRenderers = new List<ProbeRenderer>();
        static bool probeHidden;
        static int bisectFirst, bisectCount, bisectPhase;
        static double bisectLeft;
        static double bisectBaseline;
        static readonly List<string> bisectRows = new List<string>();
        // Greedy pixel-guided isolation. Every trial starts from restored OFF;
        // both siblings are measured. It identifies a candidate, not all causes.
        internal static bool ProbeBisectStep()
        {
            if (!FrameCompare.Paused || Active || preparing || probeRenderers.Count == 0) throw new InvalidOperationException("bisect requires paused/off with probe list + onref/offref/offnoise shots from this session");
            if (bisectPhase == 0)
            {
                var noise = FrameCompare.CompareShots("offref", "offnoise", out bool stable);
                if (noise.Changed != 0 || !stable) throw new InvalidOperationException("bisect noise/control state changed");
                var baseline = FrameCompare.CompareShots("offref", "onref", out _);
                if (baseline.Changed == 0) { RestoreProbe(); Log("bisect reference already identical; no candidate"); return true; }
                bisectBaseline = baseline.Psnr;
                bisectFirst = 0; bisectCount = probeRenderers.Count; bisectRows.Clear();
                bisectRows.Add("first\tcount\tchanged\tpsnr\tmaxR\tmaxG\tmaxB\tmaxA"); bisectPhase = 1;
                bisectRows.Add(FormattableString.Invariant($"baseline\t{probeRenderers.Count}\t{baseline.Changed}\t{baseline.Psnr:R}\t{baseline.Max[0]}\t{baseline.Max[1]}\t{baseline.Max[2]}\t{baseline.Max[3]}"));
            }
            if (bisectPhase == 1 || bisectPhase == 3)
            {
                int left = Math.Max(1, bisectCount / 2);
                int first = bisectPhase == 1 ? bisectFirst : bisectFirst + left;
                int count = bisectPhase == 1 ? left : bisectCount - left;
                ProbeCommand("hide " + first + " " + count); bisectPhase++; return false;
            }
            if (!FrameCompare.Step("grab probe_test")) return false;
            var score = FrameCompare.CompareShots("probe_test", "onref", out _);
            int half = Math.Max(1, bisectCount / 2), testedFirst = bisectPhase == 2 ? bisectFirst : bisectFirst + half;
            int testedCount = bisectPhase == 2 ? half : bisectCount - half;
            bisectRows.Add(FormattableString.Invariant($"{testedFirst}\t{testedCount}\t{score.Changed}\t{score.Psnr:R}\t{score.Max[0]}\t{score.Max[1]}\t{score.Max[2]}\t{score.Max[3]}"));
            RestoreProbe();
            if (bisectCount == 1)
            {
                var q = probeRenderers[bisectFirst];
                string output = Path.Combine(Main.Entry.Path, "drfull-bisect-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".tsv");
                File.WriteAllLines(output, bisectRows, new UTF8Encoding(false));
                FrameCompare.Step("save probe_test");
                Log("bisect candidate index=" + bisectFirst + " instance=" + (q.Alive ? q.Identity.GetInstanceID() : 0) + " path=" + q.Path + " route=" + q.Route + " improved_over_full_off=" + (score.Psnr > bisectBaseline) + " trace=" + output + " greedy_candidate_only=True all_flags_restored=True");
                bisectPhase = 0; return true;
            }
            if (bisectPhase == 2) { bisectLeft = score.Psnr; bisectPhase = 3; return false; }
            if (score.Psnr > bisectLeft) { bisectFirst += half; bisectCount -= half; } else bisectCount = half;
            bisectPhase = 1; return false;
        }
        static long particleBakes, particleVertices, particleEmpty;
        static bool bakedParticles; // Experimental until same-session pixel validation.
        sealed class ParticleBake { internal Mesh Body, Trails; }
        static readonly Dictionary<ParticleSystemRenderer, ParticleBake> particleMeshes = new Dictionary<ParticleSystemRenderer, ParticleBake>();
        static void RestoreProbe()
        {
            foreach (var item in probeRenderers) if (item.Alive) item.Force(item.Forced);
            probeHidden = false;
        }
        static string ProbeCommand(string arg)
        {
            var parts = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) throw new ArgumentException("probe list|hide first count|particles|restore|clear");
            if (parts[0] == "restore" || parts[0] == "clear")
            {
                RestoreProbe(); bisectPhase = 0; if (parts[0] == "clear") probeRenderers.Clear(); return "probe restored=True";
            }
            if (parts[0] == "list")
            {
                if (!Rendering) throw new InvalidOperationException("probe list requires on: capture actual native/explicit routes before off");
                RestoreProbe(); bisectPhase = 0; probeRenderers.Clear();
                foreach (var source in sources)
                {
                    var r = source.R; if (!r) continue;
                    string path = r.name; for (var t = r.transform.parent; t; t = t.parent) path = t.name + "/" + path;
                    string route = "";
                    if (source.Passes != null) foreach (var b in source.Passes) route += (b.NativeId == 0 ? "explicit" : "native:" + b.NativeId) + ":" + b.Slot + ":" + (b.Material && b.Material.shader ? b.Material.shader.name : "null") + ";";
                    probeRenderers.Add(new ProbeRenderer { Renderer = r, Forced = r.forceRenderingOff, Path = path, Route = route });
                }
                var seenGraphics = new HashSet<int>();
                foreach (var unit in canvasUnits) foreach (var cr in unit.Graphics)
                {
                    var g = cr ? cr.GetComponent<Graphic>() : null;
                    if (!g || !seenGraphics.Add(g.GetInstanceID())) continue;
                    string path = g.name; for (var t = g.transform.parent; t; t = t.parent) path = t.name + "/" + path;
                    var material = cr.materialCount > 0 ? cr.GetMaterial(0) : null;
                    probeRenderers.Add(new ProbeRenderer { Toggle = g, Forced = !g.enabled, Path = path,
                        Route = "canvas-graphic:run=" + unit.Run + ":isolated=" + unit.Isolated + ":shader=" + (material && material.shader ? material.shader.name : "null") });
                }
                probeRenderers.Sort((a,b) => { int n = string.CompareOrdinal(a.Identity.GetType().Name, b.Identity.GetType().Name); if (n == 0) n = string.CompareOrdinal(a.Path, b.Path); return n != 0 ? n : a.Identity.GetInstanceID().CompareTo(b.Identity.GetInstanceID()); });
                var text = new StringBuilder("index\tinstance\ttype\tpath\troute\tenabled\tforced\n");
                for (int i = 0; i < probeRenderers.Count; i++)
                {
                    var q = probeRenderers[i]; var r = q.Identity;
                    text.Append(i).Append('\t').Append(r.GetInstanceID()).Append('\t').Append(r.GetType().Name).Append('\t').Append(Clean(q.Path)).Append('\t').Append(Clean(q.Route)).Append('\t').Append(q.Enabled).Append('\t').Append(q.Forced).Append('\n');
                }
                string output = Path.Combine(Main.Entry.Path, "drfull-probe-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".tsv");
                File.WriteAllText(output, text.ToString(), new UTF8Encoding(false));
                return "probe count=" + probeRenderers.Count + " output=" + output + " same_session_only=True";
            }
            if (Active || preparing) throw new InvalidOperationException("probe hide requires off; retain the on reference capture in FrameCompare");
            if (probeRenderers.Count == 0) throw new InvalidOperationException("probe list must first capture the active renderer set");
            int first = 0, count = probeRenderers.Count; bool particles = parts[0] == "particles";
            if (!particles && (parts.Length != 3 || parts[0] != "hide" || !int.TryParse(parts[1], out first) || !int.TryParse(parts[2], out count) || first < 0 || count < 0 || first > probeRenderers.Count - count)) throw new ArgumentException("probe hide first count: range from this session's TSV");
            RestoreProbe(); int hidden = 0;
            for (int i = first; i < first + count; i++)
            {
                var item = probeRenderers[i];
                if (item.Alive && (!particles || item.Renderer is ParticleSystemRenderer)) { item.Force(true); hidden++; }
            }
            probeHidden = true; return "probe hidden=" + hidden + " first=" + first + " count=" + count + " diagnostic_only=True";
        }
        static void UpdateParticleMeshes()
        {
            if (!bakedParticles) return;
            foreach (var source in explicitSources)
            {
                if (!(source.R is ParticleSystemRenderer r) || !source.ExplicitVisible) continue;
                if (!particleMeshes.TryGetValue(r, out var bake))
                {
                    bake = new ParticleBake { Body = new Mesh { name = "DRF particle body" }, Trails = new Mesh { name = "DRF particle trails" } };
                    bake.Body.MarkDynamic(); bake.Trails.MarkDynamic(); particleMeshes.Add(r, bake);
                }
                r.BakeMesh(bake.Body, camera, true); particleBakes++; particleVertices += bake.Body.vertexCount;
                if (bake.Body.vertexCount == 0) particleEmpty++;
                var system = r.GetComponent<ParticleSystem>();
                if (system && system.trails.enabled) r.BakeTrailsMesh(bake.Trails, camera, true); else bake.Trails.Clear();
                // DrawMesh captures the matrix/MPB. Refresh only in this diagnostic.
                orderCommandsReady = false; orderDirty = true;
            }
        }
        static bool RecordBakedParticle(CommandBuffer cb, Entry e, int pass)
        {
            if (!bakedParticles || !(e.Source.R is ParticleSystemRenderer r)) return false;
            if (r.renderMode == ParticleSystemRenderMode.Mesh && r.enableGPUInstancing) throw new InvalidOperationException("baked particle diagnostic requires non-instanced original shader; use explicit control");
            if (!particleMeshes.TryGetValue(r, out var bake)) return true;
            var mesh = e.Slot > 0 && e.Fallback == r.trailMaterial ? bake.Trails : bake.Body;
            if (mesh.vertexCount != 0)
            {
                var block = e.Source.SlotBlock; block.Clear(); r.GetPropertyBlock(block, e.Slot);
                if (block.isEmpty) { block.Clear(); r.GetPropertyBlock(block); }
                cb.DrawMesh(mesh, Matrix4x4.Translate(r.transform.position), e.Fallback, Math.Min(e.Slot, mesh.subMeshCount - 1), pass, block);
            }
            return true;
        }
        static void ReleaseParticleMeshes()
        {
            foreach (var b in particleMeshes.Values) { UnityEngine.Object.Destroy(b.Body); UnityEngine.Object.Destroy(b.Trails); }
            particleMeshes.Clear(); particleBakes = particleVertices = particleEmpty = 0;
        }
    }
}
#endif

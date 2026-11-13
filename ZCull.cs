#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // Explicit, paused, single snapshot. No renderer/material mutation or camera command buffer.
    internal sealed class ZCull : MonoBehaviour
    {
        sealed class Draw
        {
            internal Renderer R;
            internal Material Source, Measure;
            internal SceneRenderOrder.Key Key;
            internal int Slot, Segment;
            internal bool Safe, Cover, Tie;
            internal uint Total, Alive, Pixels;
            internal Texture StackTexture;
            internal Color StackColor;
            internal Vector3 StackSize;
        }
        static readonly AccessTools.FieldRef<scrVisualDecoration, DecorationBlendMode> Blend =
            AccessTools.FieldRefAccess<scrVisualDecoration, DecorationBlendMode>("blendMode");
        static ZCull active;
        static bool waiting, succeeded;
        internal static bool Busy => active != null;
        readonly List<Draw> draws = new List<Draw>();
        readonly List<Material> materials = new List<Material>();
        AssetBundle bundle;
        Shader shader;
        ComputeShader reduce;
        ComputeBuffer counts;
        RenderTexture coverage, depth;
        Camera camera;
        AsyncGPUReadbackRequest request;
        bool submitted;
        float deadline;
        int kernel, rendererCount;
        static readonly int DepthId = Shader.PropertyToID("_SfZDepth");
        static void Log(string s) => Main.Entry.Logger.Log("[zcull] " + s);
        static string Clean(string s) => (s ?? "null").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
        static string F(double n) => n.ToString("F3", CultureInfo.InvariantCulture);

        // AutoTest polls the SAME step until the async snapshot is complete. A following
        // resume must never invalidate a pending readback or turn a failure into a success.
        internal static bool Step(string arg)
        {
            if (arg.Trim() == "stop") { waiting = false; Stop(); return true; }
            if (arg.Trim() != "census") throw new ArgumentException("zcull census|stop");
            if (!waiting) { Command(arg); waiting = true; }
            if (Busy) return false;
            waiting = false;
            if (!succeeded) throw new InvalidOperationException("zcull census failed; see [zcull] diagnostic log");
            return true;
        }

        internal static void Command(string arg)
        {
            if (arg.Trim() == "stop") { Stop(); return; }
            if (arg.Trim() != "census") throw new ArgumentException("zcull census|stop");
            if (Busy || GpuHide.Busy || GpuCensus.Busy || GpuSplit.Busy || GpuSplitPixelDiff.Busy || Occlude.Busy)
                throw new InvalidOperationException("stop active render diagnostics first");
            if (!GpuPlaybackState.Paused) throw new InvalidOperationException("zcull census requires paused playback");
            if (Main.Config.GpuDeco != 0 || Main.Config.RunLod)
                throw new InvalidOperationException("use rlNR: GpuDeco=0, RunLod=false (original renderers required)");
            succeeded = false;
            var go = new GameObject("StutterFix.ZCull"); DontDestroyOnLoad(go);
            active = go.AddComponent<ZCull>();
            try
            {
                active.camera = GpuDecoScene.FindMain();
                active.deadline = Time.realtimeSinceStartup + 30;
                Camera.onPreRender += active.Capture;
                Log("queued census; paused Camera snapshot; discard diagnostic frame/window from performance results");
            }
            catch { Stop(); throw; }
        }
        void Capture(Camera c)
        {
            if (c != camera) return;
            Camera.onPreRender -= Capture;
            try
            {
                if (!GpuPlaybackState.Paused) throw new InvalidOperationException("playback resumed before snapshot");
                if (!c.orthographic || c.stereoEnabled) throw new InvalidOperationException("orthographic mono camera required");
                if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback
                    || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
                    throw new InvalidOperationException("RFloat/compute/AsyncGPUReadback unavailable");
                // Built and embedded separately from production bundles; never loads an installed DLL.
                using (var s = typeof(ZCull).Assembly.GetManifestResourceStream("StutterFix.zcull"))
                {
                    if (s == null) throw new InvalidOperationException("diagnostic shader bundle missing");
                    var bytes = new byte[checked((int)s.Length)];
                    int n = 0, read;
                    while (n < bytes.Length && (read = s.Read(bytes, n, bytes.Length - n)) > 0) n += read;
                    if (n != bytes.Length) throw new EndOfStreamException();
                    bundle = AssetBundle.LoadFromMemory(bytes);
                }
                if (!bundle) throw new InvalidOperationException("diagnostic bundle load failed");
                shader = bundle.LoadAsset<Shader>("Assets/ZCull.shader");
                reduce = bundle.LoadAsset<ComputeShader>("Assets/ZCullReduce.compute");
                if (!shader || !shader.isSupported || !reduce) throw new InvalidOperationException("diagnostic shaders unavailable");
                kernel = reduce.FindKernel("Reduce");
                var cameraRT = c.targetTexture;
                Main.Entry.Logger.Log("[stack] camera_target=" + (cameraRT
                    ? "format=" + cameraRT.format + " graphicsFormat=" + cameraRT.graphicsFormat + " sRGB=" + cameraRT.sRGB
                    : "backbuffer") + " allowHDR=" + c.allowHDR);
                var gameCamera = scrCamera.instance;
                var gameRT = gameCamera == null ? null : AccessTools.Field(typeof(scrCamera), "camRT").GetValue(gameCamera) as RenderTexture;
                Main.Entry.Logger.Log("[stack] game_camRT=" + (gameRT
                    ? "format=" + gameRT.format + " graphicsFormat=" + gameRT.graphicsFormat + " sRGB=" + gameRT.sRGB
                    : "null"));
                Discover(c);
                if (draws.Count == 0 || draws.Count > 200000) throw new InvalidOperationException("invalid draw count " + draws.Count);
                int w = c.pixelWidth, h = c.pixelHeight;
                if (w <= 0 || h <= 0 || (long)w * h > 33554432) throw new InvalidOperationException("unsupported camera dimensions");
                coverage = new RenderTexture(w, h, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear) { name = "ZCull.Coverage", antiAliasing = 1 };
                depth = new RenderTexture(w, h, 24, RenderTextureFormat.Depth) { name = "ZCull.Depth", antiAliasing = 1 };
                if (!coverage.Create() || !depth.Create()) throw new InvalidOperationException("RT allocation failed");
                counts = new ComputeBuffer(draws.Count * 3, sizeof(uint)); counts.SetData(new uint[draws.Count * 3]);
                Measure(c);
                request = AsyncGPUReadback.Request(counts); submitted = true;
                deadline = Time.realtimeSinceStartup + 120;
                Log("submitted draws=" + draws.Count + " resolution=" + w + "x" + h
                    + " MSAA=1 scope=single-sample-geometry-coverage known_sprite_segments_only=True"
                    + " barriers=" + draws.Count(d => !d.Safe) + " ambiguous=" + draws.Count(d => d.Tie)
                    + " note=unknown-shader-deformation-and-multipass-not-reproduced;not-a-hardware-fragment-counter");
                Log("barrier reasons: " + string.Join(", ", Reasons.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + "=" + kv.Value))); Reasons.Clear(); if (Faint.Length > 0) Log("faint layers:" + Faint); Faint.Clear(); FaintN = 0;
            }
            catch (Exception e) { Log("failed: " + e); Stop(); }
        }
        void Discover(Camera c)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(c.cullingMatrix);
            var mode = c.transparencySortMode == TransparencySortMode.Default ? GraphicsSettings.transparencySortMode : c.transparencySortMode;
            var axis = c.transparencySortMode == TransparencySortMode.Default ? GraphicsSettings.transparencySortAxis : c.transparencySortAxis;
            if (mode == TransparencySortMode.Perspective) throw new InvalidOperationException("perspective transparency sorting unsupported");
            var ids = new HashSet<int>();
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r || !r.gameObject.scene.IsValid() || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff
                    || r is SpriteMask || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly
                    || (c.cullingMask & (1 << r.gameObject.layer)) == 0 || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                var sr = r as SpriteRenderer;
                var group = r.GetComponentInParent<SortingGroup>();
                // SortingGroup internal order is opaque to this census: use the outer group's
                // external key and put every draw in that bucket behind a conservative barrier.
                if (group) for (var p = group.transform.parent; p; p = p.parent)
                { var outer = p.GetComponent<SortingGroup>(); if (outer && outer.enabled) group = outer; }
                Vector3 pos = group ? group.transform.position : sr && sr.spriteSortPoint == SpriteSortPoint.Pivot ? sr.transform.position : r.bounds.center;
                float distance = mode == TransparencySortMode.CustomAxis ? Vector3.Dot(pos, axis) : Vector3.Dot(pos - c.transform.position, c.transform.forward);
                r.GetSharedMaterials(materials);
                for (int slot = 0; slot < materials.Count; slot++)
                {
                    var m = materials[slot]; if (!m || !m.shader) continue;
                    // Flattening depth must not make near/far-clipped geometry into a cover.
                    Vector3 forward = c.transform.forward, e = r.bounds.extents;
                    float z = Vector3.Dot(r.bounds.center - c.transform.position, forward);
                    float radius = Mathf.Abs(forward.x) * e.x + Mathf.Abs(forward.y) * e.y + Mathf.Abs(forward.z) * e.z;
                    LastBarrier = false; bool clipOK = !group && z - radius > c.nearClipPlane && z + radius < c.farClipPlane; bool spriteOK = clipOK && SafeSprite(sr, m); if (group) { No("sortingGroup"); } bool safe = spriteOK || (clipOK && !LastBarrier);
                    var d = new Draw { R = r, Source = m, Slot = slot, Safe = safe,
                        Key = new SceneRenderOrder.Key(group ? SortingLayer.GetLayerValueFromID(group.sortingLayerID) : SortingLayer.GetLayerValueFromID(r.sortingLayerID),
                            group ? group.sortingOrder : r.sortingOrder, m.renderQueue, distance, m.GetInstanceID(), r.GetInstanceID(), slot, SceneRenderOrder.ShaderRank(m)) };
                    d.Cover = spriteOK && materials.Count == 1 && Eligible(sr, m);
                    d.Measure = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    draws.Add(d); // Own the material immediately, including preparation failures.
                    d.Measure.CopyPropertiesFromMaterial(m);
                    d.Measure.shaderKeywords = Array.Empty<string>();
                    if (sr && sr.sprite) d.Measure.SetTexture("_MainTex", sr.sprite.texture);
                    d.Measure.SetFloat("_SfZTile", m.shader.name == "Sprites/Tile" ? 1 : 0);
                    ids.Add(r.GetInstanceID());
                }
            }
            rendererCount = ids.Count;
            draws.Sort((a, b) => a.Key.CompareTo(b.Key));
            var ties = ZCullPolicy.Ambiguous(draws.Select(d => d.Key).ToArray());
            // Unknown shaders, masks, groups, GrabPass etc. split segments. This prevents
            // erasing inputs to a screen-reading shader, even outside the covered pixel.
            var unsafeBuckets = draws.Where(d => !d.Safe).Select(d => (d.Key.Layer, d.Key.Order, d.Key.Queue)).ToHashSet();
            int segment = 0;
            for (int i = 0; i < draws.Count; i++)
            {
                var d = draws[i]; d.Tie = ties[i];
                if (unsafeBuckets.Contains((d.Key.Layer, d.Key.Order, d.Key.Queue))) d.Safe = false;
                d.Cover &= d.Safe && !d.Tie;
                if (!d.Safe) segment++;
                d.Segment = segment;
                if (!d.Safe) segment++;
            }
            foreach (var d in draws)
            {
                var sr = d.R as SpriteRenderer;
                // A census candidate, not approval to replace a renderer. Unknown overrides,
                // sorting ties and non-normal blend paths break adjacency conservatively.
                if (!d.Safe || d.Tie || !sr || !sr.sprite || d.Slot != 0
                    || sr.sharedMaterials.Length != 1
                    || sr.sprite.associatedAlphaSplitTexture || sr.sprite.packed
                    || !SafeSprite(sr, d.Source)) continue;
                d.StackTexture = sr.sprite.texture;
                d.StackColor = sr.color;
                d.StackSize = sr.bounds.size;
                if (!Finite(d.StackColor.r) || !Finite(d.StackColor.g) || !Finite(d.StackColor.b)
                    || !Finite(d.StackColor.a) || d.StackColor.a < 0 || d.StackColor.a > 1)
                    d.StackTexture = null;
            }
        }
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static bool SameStack(Draw a, Draw b) => a.StackTexture && b.StackTexture
            && a.Source.shader == b.Source.shader && a.StackTexture == b.StackTexture && a.Segment == b.Segment
            && a.StackColor.r == b.StackColor.r && a.StackColor.g == b.StackColor.g && a.StackColor.b == b.StackColor.b;
        void StackCensus(double total)
        {
            var stacks = new List<List<Draw>>();
            for (int i = 0; i < draws.Count;)
            {
                int end = i + 1;
                while (end < draws.Count && SameStack(draws[end - 1], draws[end])) end++;
                if (draws[i].StackTexture && end - i >= 2) stacks.Add(draws.GetRange(i, end - i));
                i = end;
            }
            stacks = stacks.OrderByDescending(g => g.Sum(d => (double)d.Total)).ToList();
            var qualifying = stacks.Where(g => g.Count >= 4).ToList();
            double sum = qualifying.Sum(g => g.Sum(d => (double)d.Total));
            string top = qualifying.Count == 0 ? "none" : "tex=" + Clean(qualifying[0][0].StackTexture.name)
                + " layers=" + qualifying[0].Count + " frags=" + F(qualifying[0].Sum(d => (double)d.Total));
            Main.Entry.Logger.Log("[stack] fragments_total=" + F(total) + " in_stacks=" + F(sum)
                + " (" + F(total == 0 ? 0 : 100 * sum / total) + "%) stacks=" + qualifying.Count + " top: " + top);
            for (int n = 0; n < stacks.Count; n++)
            {
                var g = stacks[n]; var first = g[0]; double fragments = g.Sum(d => (double)d.Total);
                Main.Entry.Logger.Log("[stack] id=" + n + " tex=" + Clean(first.StackTexture.name)
                    + " texture_id=" + first.StackTexture.GetInstanceID() + " material_id=" + first.Source.GetInstanceID()
                    + " shader=" + Clean(first.Source.shader.name) + " layers=" + g.Count + " frags=" + F(fragments)
                    + " share_percent=" + F(total == 0 ? 0 : 100 * fragments / total)
                    + " rgb=" + F(first.StackColor.r) + "," + F(first.StackColor.g) + "," + F(first.StackColor.b)
                    + " alpha_hist=" + string.Join(",", g.GroupBy(d => d.StackColor.a).OrderBy(a => a.Key)
                        .Select(a => a.Key.ToString("R", CultureInfo.InvariantCulture) + ":" + a.Count()))
                    + " bounds_width=" + F(g.Min(d => d.StackSize.x)) + ".." + F(g.Max(d => d.StackSize.x))
                    + " bounds_height=" + F(g.Min(d => d.StackSize.y)) + ".." + F(g.Max(d => d.StackSize.y))
                    + " order=" + first.Key.Order + ".." + g[g.Count - 1].Key.Order);
            }
            Main.Entry.Logger.Log("[stack] scope=consecutive-compatible-draws summary_layers_min=4 detail_layers_min=2 metric=geometry-fragments;"
                + "not-overlap-proof;property-blocks-and-ambiguous-order-excluded;not-GPU-time-or-FPS");
        }
        internal static readonly System.Collections.Generic.Dictionary<string, int> Reasons = new System.Collections.Generic.Dictionary<string, int>();
        internal static bool LastBarrier; internal static readonly System.Text.StringBuilder Faint = new System.Text.StringBuilder(); internal static int FaintN;
        static bool No(string why) { Reasons.TryGetValue(why, out int n); Reasons[why] = n + 1; LastBarrier = why.StartsWith("maskInteraction") || why.StartsWith("isMask") || why.StartsWith("blend") || why.StartsWith("stencilProp") || why.StartsWith("sortingGroup") || why.Contains("Grab") || why.Contains("BlendMode") || why.Contains("Mask"); return false; }
        static bool SafeSprite(SpriteRenderer r, Material m)
        {
            if (!r) return No("not-sprite:" + (m && m.shader ? m.shader.name : "?"));
            if (!r.sprite) return No("no-sprite");
            if (r.drawMode != SpriteDrawMode.Simple) return No("drawMode");
            if (r.maskInteraction != SpriteMaskInteraction.None) return No("maskInteraction");
            if (r.GetComponentInParent<SortingGroup>()) return No("sortingGroup");
            if (m.passCount != 1) return No("passCount");
            if (m.renderQueue != 3000) return No("queue" + m.renderQueue);
            if (!m.GetShaderPassEnabled(m.GetPassName(0))) return No("passDisabled");
            if (m.shader.name != "Sprites/Default" && m.shader.name != "Sprites/Tile") return No("shader:" + m.shader.name);
            if (m.HasProperty("_Stencil") || m.HasProperty("_StencilComp") || m.HasProperty("_BLENDMODES_BlendStencilComp")) return No("stencilProp");
            foreach (var k in m.shader.keywordSpace.keywords)
            {
                if (m.IsKeywordEnabled(k)) return No("localKw:" + k.name);
                if (k.isOverridable && Shader.IsKeywordEnabled(k.name)) return No("globalKw:" + k.name);
            }
            if (m.shader.name == "Sprites/Tile" && (r.flipX || r.flipY)) return No("tileFlip");
            var deco = r.GetComponentInParent<scrVisualDecoration>();
            if (!deco) return true;
            if (deco.isMask()) return No("isMask");
            if ((int)Blend(deco) != 0) return No("blend" + (int)Blend(deco));
            if (deco.blendModeEffect && deco.blendModeEffect.enabled) return No("blendModeEffect");
            return true;
        }
        static bool Eligible(SpriteRenderer r, Material m)
        {
            var d = r.GetComponentInParent<scrVisualDecoration>();
            var s = r.sprite; var t = s.texture;
            if (!GpuCensus.IsDecoration(r)) return No("cover:notDecoration");
            if (!d) return No("cover:noVisualDeco");
            if (r.color.a > 0f && r.color.a < 0.05f && FaintN++ < 40) { var tr = r.transform; Faint.Append(" || faint tex=").Append(t ? t.name : "?").Append(" spr=").Append(s.name).Append(" rgba=").Append(r.color.ToString("F3")).Append(" pos=").Append(tr.position.ToString("F3")).Append(" rot=").Append(tr.eulerAngles.ToString("F2")).Append(" scl=").Append(tr.lossyScale.ToString("F3")).Append(" order=").Append(r.sortingOrder).Append(" go=").Append(tr.parent ? tr.parent.name : "-").Append("/").Append(r.gameObject.name); }
            if (r.color.a != 1) return No(r.color.a < 0.05f ? "cover:alpha255=" + (r.color.a * 255f).ToString("F2") + (r.bounds.size.x * r.bounds.size.y > 100f ? "/big" : "/small") : "cover:colorAlpha=" + (Mathf.Round(r.color.a * 20f) / 20f).ToString("F2"));
            if ((int)Blend(d) != 0 || (d.blendModeEffect && d.blendModeEffect.enabled)) return No("cover:blendMode");
            if (!t) return No("cover:noTexture");
            if (t.mipmapCount != 1) return No("cover:mipmaps");
            if (t.filterMode == FilterMode.Trilinear) return No("cover:trilinear");
            if (t.anisoLevel > 1) return No("cover:aniso");
            if (s.associatedAlphaSplitTexture || s.packed) return No("cover:packed");
            if (m.HasProperty("_Color") && m.GetColor("_Color").a != 1) return No("cover:matColorAlpha");
            if (m.HasProperty("_RendererColor") && m.GetColor("_RendererColor").a != 1) return No("cover:matRendererColorAlpha");
            if (m.HasProperty("_Flip")) { var f = m.GetVector("_Flip"); if (f.x != 1 || f.y != 1) return false; }
            if (r.HasPropertyBlock())
            {
                var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b);
                if (b.HasColor("_Color") && b.GetColor("_Color").a != 1) return No("mpbColorAlpha");
                if (b.HasColor("_RendererColor") && b.GetColor("_RendererColor").a != 1) return No("mpbRendererColorAlpha");
                if (b.HasVector("_Flip")) { var f = b.GetVector("_Flip"); if (f.x != 1 || f.y != 1) return No("mpbFlip"); }
                if (b.HasTexture("_MainTex") && b.GetTexture("_MainTex") != t) return No("mpbMainTex");
            }
            // No texture-wide alpha test or fullscreen rectangle requirement: actual sprite mesh/UV wins.
            return true;
        }
        void Measure(Camera c)
        {
            using (var cmd = new CommandBuffer { name = "ZCull single snapshot" })
            {
                cmd.SetViewProjectionMatrices(c.worldToCameraMatrix, GL.GetGPUProjectionMatrix(c.projectionMatrix, true));
                cmd.SetRenderTarget(coverage.colorBuffer, depth.depthBuffer);
                cmd.SetViewport(new Rect(0, 0, coverage.width, coverage.height));
                cmd.SetComputeIntParam(reduce, "Width", coverage.width); cmd.SetComputeIntParam(reduce, "Height", coverage.height);
                cmd.SetComputeTextureParam(reduce, kernel, "Coverage", coverage);
                cmd.SetComputeBufferParam(reduce, kernel, "Counts", counts);
                int start = 0;
                while (start < draws.Count)
                {
                    int end = start + 1;
                    while (end < draws.Count && draws[end].Segment == draws[start].Segment) end++;
                    cmd.ClearRenderTarget(true, true, Color.clear, 1f);
                    for (int i = end - 1; i >= start; i--) if (draws[i].Cover) DrawOne(cmd, i, 0, false);
                    for (int i = start; i < end; i++)
                    {
                        Sum(cmd, i, 2, 0); Sum(cmd, i, 1, 1);
                        if (draws[i].Cover) Sum(cmd, i, 3, 2);
                    }
                    start = end;
                }
                // Execute inside the main-camera callback; restore its matrices/target/viewport.
                cmd.SetViewProjectionMatrices(c.worldToCameraMatrix, GL.GetGPUProjectionMatrix(c.projectionMatrix, c.targetTexture != null));
                cmd.SetRenderTarget(c.targetTexture ? new RenderTargetIdentifier(c.targetTexture) : BuiltinRenderTextureType.CameraTarget);
                cmd.SetViewport(c.pixelRect);
                Graphics.ExecuteCommandBuffer(cmd);
            }
        }
        void DrawOne(CommandBuffer cmd, int i, int pass, bool counting)
        {
            cmd.SetGlobalFloat(DepthId, ZCullPolicy.Depth(i, draws.Count, counting));
            var d = draws[i]; cmd.DrawRenderer(d.R, d.Measure, d.Slot, pass);
        }
        void Sum(CommandBuffer cmd, int i, int pass, int metric)
        {
            cmd.ClearRenderTarget(false, true, Color.clear);
            DrawOne(cmd, i, pass, pass == 1);
            cmd.SetComputeIntParam(reduce, "Slot", i * 3 + metric);
            cmd.DispatchCompute(reduce, kernel, (coverage.width + 7) / 8, (coverage.height + 7) / 8, 1);
        }
        void Update()
        {
            if (!camera || !GpuPlaybackState.Paused) { Log("cancelled: camera lost or playback resumed"); Stop(); return; }
            if (Time.realtimeSinceStartup > deadline) { Log("failed: snapshot/readback timeout"); Stop(); return; }
            if (!submitted || !request.done) return;
            try
            {
                if (request.hasError) throw new InvalidOperationException("GPU readback failed");
                var values = request.GetData<uint>();
                double total = 0, alive = 0;
                for (int i = 0; i < draws.Count; i++)
                {
                    var d = draws[i]; d.Total = values[i * 3]; d.Alive = values[i * 3 + 1]; d.Pixels = values[i * 3 + 2];
                    if (d.Alive > d.Total) throw new InvalidOperationException("alive exceeds total; depth/raster validation failed");
                    total += d.Total; alive += d.Alive;
                }
                Log("renderers=" + rendererCount + " occluders=" + draws.Count(d => d.Cover)
                    + " fragments_total=" + F(total) + " fragments_alive=" + F(alive) + " culled=" + F(total == 0 ? 0 : 100 * (total - alive) / total) + "%");
                StackCensus(total);
                foreach (var g in draws.GroupBy(d => d.R).OrderByDescending(g => g.Sum(d => (double)d.Total - d.Alive)).Take(20))
                    Log("renderer name=" + Clean(g.Key.name) + " id=" + g.Key.GetInstanceID() + " layer=" + g.First().Key.Layer + " order=" + g.First().Key.Order
                        + " distance=" + F(g.First().Key.Distance) + " total=" + F(g.Sum(d => (double)d.Total)) + " culled=" + F(g.Sum(d => (double)d.Total - d.Alive)));
                foreach (var d in draws.Where(d => d.Cover).OrderByDescending(d => d.Pixels).Take(10))
                    Log("occluder name=" + Clean(d.R.name) + " id=" + d.R.GetInstanceID() + " layer=" + d.Key.Layer + " order=" + d.Key.Order + " covered_pixels=" + d.Pixels);
                Log("complete; metric=geometry-fragments single-sample conservative-segment-culling; GPU-time/FPS not measured");
                succeeded = true;
            }
            catch (Exception e) { Log("failed: " + e); }
            finally { Stop(); }
        }
        void Release()
        {
            Camera.onPreRender -= Capture;
            // Resources remain alive until their own readback has finished, including unload/cancel.
            if (submitted && !request.done) request.WaitForCompletion();
            submitted = false;
            if (counts != null) { counts.Release(); counts = null; }
            if (coverage) { coverage.Release(); Destroy(coverage); coverage = null; }
            if (depth) { depth.Release(); Destroy(depth); depth = null; }
            foreach (var d in draws) if (d.Measure) DestroyImmediate(d.Measure);
            draws.Clear();
            if (bundle) { bundle.Unload(true); bundle = null; }
        }
        void OnDestroy() { Release(); if (active == this) active = null; }
        internal static void Stop()
        {
            var p = active; if (!p) return;
            active = null; p.Release(); Destroy(p.gameObject);
        }
    }
}
#endif
